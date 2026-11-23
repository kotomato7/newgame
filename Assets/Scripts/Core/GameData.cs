// ゲームの数値データ (Pygame 版の game_data.py)
//
// 数値は data フォルダの表(CSV)から読みこむ。変えたいときは Excel などで書きかえて保存するだけでよい
//   (Unity では Assets/StreamingAssets/data/ にある)
//   data/characters.csv   … キャラクターのパラメータ(攻撃力・防御力・HP・呪力・ダメージカット率K・QTE初期値)
//                           と、敵の画像フォルダ名(assets/images/Enemy/<フォルダ名>。英語)
//                           「名前」は画面に出す名前なので、変えても画像のフォルダは変わらない
//                           防御力・ダメージカット率K は敵だけ、QTE初期値 はプレイヤーだけが使う
//                           新しい敵の種族は、区分を「敵」にした行を1行足す
//   data/battle_rules.csv … バトルのルールの数値(QTE成功時のダメージ倍率、ひっさつのダメージ など)
//   data/talismans.csv    … じゅふ(呪符)。1行が呪符1枚(3枚まで)。上がるパラメータ(攻撃力)と上がる量
//   data/animation.csv    … キャラの画像アニメーションの速さ(動きごとの1コマの時間)
//   data/bgm.csv          … BGM の音量(画面ごと)。曲そのものは assets/bgm/<Title・Menu・Battle>/ に入れる
// ゲーム起動中でも、タイトルで「はじめる」を押すたびに読みなおす
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace QteRpg
{
    /// <summary>CSV の書き方がおかしいときのエラー(どこがおかしいかをメッセージに書く)</summary>
    public sealed class DataError : Exception
    {
        public DataError(string message) : base(message) { }
    }

    /// <summary>敵の種族のパラメータ</summary>
    public sealed class EnemySpeciesData
    {
        public int Attack, Hp, Mp, Defense;
        public double Cut;      // ダメージカット率K
        public string Folder;   // 画像フォルダの名前(assets/images/Enemy/<Folder>)
    }

    /// <summary>呪符1枚</summary>
    public sealed class Talisman
    {
        public string Stat;      // "attack"
        public string StatName;  // "攻撃力"
        public int Amount;
    }

    public static class GameData
    {
        public const int STAT_MAX = 100;  // 攻撃力・防御力・呪力 の上限(HP は上限なし)
        public const int QTE_MAX = 5;     // QTEポイントの上限(プレイヤーだけが持つ)

        static string DataDir => Path.Combine(Settings.BASE_DIR, "data");
        static string CharactersCsv => Path.Combine(DataDir, "characters.csv");
        static string RulesCsv => Path.Combine(DataDir, "battle_rules.csv");
        static string TalismansCsv => Path.Combine(DataDir, "talismans.csv");
        static string AnimationCsv => Path.Combine(DataDir, "animation.csv");
        static string BgmCsv => Path.Combine(DataDir, "bgm.csv");

        // BGM をつける画面: プログラムの中での画面の名前 → bgm.csv の「画面」
        public static readonly Dictionary<string, string> BGM_SCENES = new Dictionary<string, string>
        {
            { "title", "タイトル" }, { "menu", "メニュー" }, { "battle", "バトル" },
        };
        // プログラムの中での画面の名前 → assets/bgm と assets/images/Background のフォルダ名
        public static readonly Dictionary<string, string> SCENE_FOLDERS = new Dictionary<string, string>
        {
            { "title", "Title" }, { "menu", "Menu" }, { "battle", "Battle" },
        };
        public static readonly string[] MOTIONS = { "待機", "攻撃", "ダメージ", "ひっさつ", "じゅふ" };  // 画像の動きの種類(animation.csv の「動き」)
        // 動き → assets/images の中のフォルダ名
        public static readonly Dictionary<string, string> MOTION_FOLDERS = new Dictionary<string, string>
        {
            { "待機", "Normal" }, { "攻撃", "Attack" }, { "ダメージ", "Damage" }, { "ひっさつ", "Special" }, { "じゅふ", "Item" },
        };
        public const int TALISMAN_MAX = 3;  // 呪符は3枚まで
        public static readonly Dictionary<string, string> TALISMAN_STATS = new Dictionary<string, string> { { "攻撃力", "attack" } };  // 呪符で上げられるパラメータ

        // battle_rules.csv の「項目」→ プログラムの中での名前
        static readonly Dictionary<string, string> RULE_ITEMS = new Dictionary<string, string>
        {
            { "QTE成功で与えるダメージ倍率", "QTE_ATTACK_RATE" },
            { "ひっさつのダメージ(敵全体)", "SPECIAL_DAMAGE" },
            { "ひっさつで星をえがく制限時間(秒)", "SPECIAL_TIME" },
            { "防御力の効き(K)", "DEFENSE_K" },
        };

        // CSV の列名 → プログラムの中での名前
        static readonly (string column, string key)[] STAT_COLUMNS = { ("攻撃力", "attack"), ("HP", "hp"), ("呪力", "mp") };
        const string DEFENSE_COLUMN = "防御力";         // 敵だけが使う列
        const string CUT_COLUMN = "ダメージカット率K";  // 敵だけが使う列(小数も書ける)
        const string FOLDER_COLUMN = "フォルダ名";       // 敵だけが使う列(assets/images/Enemy の中の画像フォルダの名前)
        static readonly string[] REQUIRED_COLUMNS = { "区分", "名前", "攻撃力", "HP", "呪力", DEFENSE_COLUMN, CUT_COLUMN, FOLDER_COLUMN };

        // ---- 読みこんだ値(起動したときと、タイトルで「はじめる」を押したときに新しくなる) ----
        public static string PLAYER_NAME = "プレイヤー";
        public static Dictionary<string, int> PLAYER_STATS = new Dictionary<string, int>();  // {"attack": .., "hp": .., "mp": ..}
        public static int PLAYER_QTE_START = 0;    // QTEポイントの初期値
        public static Dictionary<string, EnemySpeciesData> ENEMY_SPECIES = new Dictionary<string, EnemySpeciesData>();  // {"鬼": ..., ...}
        public static double QTE_ATTACK_RATE = 1.0;  // QTE成功で与えるダメージ倍率(プレイヤーの攻撃)
        public static int SPECIAL_DAMAGE = 0;        // ひっさつで敵全体にあたえるダメージ
        public static double SPECIAL_TIME = 5.0;     // ひっさつで星をえがく制限時間(秒)
        public static double DEFENSE_K = 20.0;       // 防御力の効き。敵が受けるダメージ = 攻撃力 × K ÷ (K + 敵の防御力)
        public static List<Talisman> TALISMANS = new List<Talisman>();  // 呪符
        public static Dictionary<string, double> FRAME_TIMES = new Dictionary<string, double>();  // アニメーションの1コマの時間 {"待機": 0.2, ...}
        public static Dictionary<string, double> BGM_VOLUMES = new Dictionary<string, double>();  // BGM の音量(0〜1) {"タイトル": 0.6, ...}

        // ---------------- CSV を読む ----------------

        /// <summary>
        /// CSV を読んで、1行ずつ {列名: 値} の辞書にする
        /// Excel で保存すると Shift-JIS になることがあるので、UTF-8 がだめなら Shift-JIS で読む
        /// </summary>
        static List<Dictionary<string, string>> ReadRows(string path, string[] requiredColumns)
        {
            string name = Path.GetFileName(path);
            byte[] raw = File.ReadAllBytes(path);
            string text = Decode(raw, name);
            var records = Csv.Parse(text);
            var header = records.Count > 0 ? records[0].Select(Normalize).ToList() : new List<string>();
            var missing = requiredColumns.Where(column => !header.Contains(column)).ToList();
            if (missing.Count > 0)
            {
                throw new DataError($"{name} に列がありません: {string.Join("、", missing)}");
            }
            var rows = new List<Dictionary<string, string>>();
            for (int r = 1; r < records.Count; r++)
            {
                var record = records[r];
                var row = new Dictionary<string, string>();
                for (int c = 0; c < header.Count; c++)
                {
                    row[header[c]] = Normalize(c < record.Count ? record[c] : "");
                }
                rows.Add(row);
            }
            return rows;
        }

        static string Decode(byte[] raw, string name)
        {
            int start = (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF) ? 3 : 0;
            try
            {
                return new UTF8Encoding(false, true).GetString(raw, start, raw.Length - start);
            }
            catch (DecoderFallbackException)
            {
            }
            Encoding sjis;
            try
            {
                sjis = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            }
            catch (Exception)
            {
                // ビルドしたゲームでは Shift-JIS が使えないことがある
                throw new DataError($"{name} は UTF-8 ではないので読めません。UTF-8 で保存してください(この環境では Shift-JIS を読めません)");
            }
            try
            {
                return sjis.GetString(raw);
            }
            catch (DecoderFallbackException)
            {
                throw new DataError($"{name} の文字コードが読めません。UTF-8 か Shift-JIS で保存してください");
            }
        }

        /// <summary>前後の空白をとり、Excel の保存で変わってしまう文字をもとにもどす(〜 が ～ になる など)</summary>
        static string Normalize(string text) => (text ?? "").Trim().Replace("～", "〜");

        static string Get(Dictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value : "";

        /// <summary>Python の float(text)。数字でなければ false</summary>
        static bool TryFloat(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Python の int(float(value))。Excel で "10.0" のようになっても読めるように</summary>
        static int ToInt(string value, string where)
        {
            if (!TryFloat(value, out double number) || double.IsNaN(number) || double.IsInfinity(number))
            {
                throw new DataError($"{where} が数字ではありません: 「{value}」");
            }
            return (int)number;
        }

        static string Fmt(double value)
        {
            // Python の print と同じ見た目にする(1.0 → 1.0、2.5 → 2.5)
            if (value == Math.Floor(value) && Math.Abs(value) < 1e16)
            {
                return value.ToString("0.0", CultureInfo.InvariantCulture);
            }
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// CSV からプレイヤーと敵のパラメータを読む
        /// 戻り値: (プレイヤーの名前, プレイヤーのパラメータ, QTE初期値), {敵の種族名: パラメータ}
        /// </summary>
        static ((string name, Dictionary<string, int> stats, int qteStart) player, Dictionary<string, EnemySpeciesData> enemies)
            LoadCharacters()
        {
            string path = CharactersCsv;
            string fileName = Path.GetFileName(path);
            (string, Dictionary<string, int>, int)? player = null;
            var enemies = new Dictionary<string, EnemySpeciesData>();
            int lineNo = 1;  // 1行目は見出し
            foreach (var row in ReadRows(path, REQUIRED_COLUMNS))
            {
                lineNo++;
                string kind = Get(row, "区分"), name = Get(row, "名前");
                if (kind == "" && name == "")
                {
                    continue;  // 空の行は飛ばす
                }
                var stats = new Dictionary<string, int>();
                foreach (var (column, key) in STAT_COLUMNS)
                {
                    stats[key] = ToInt(Get(row, column), $"{fileName} の {lineNo}行目「{column}」");
                }
                foreach (var (column, key) in STAT_COLUMNS)
                {
                    int? upper = key == "hp" ? (int?)null : STAT_MAX;  // HP は上限なし
                    if (stats[key] < 0 || (upper != null && stats[key] > upper))
                    {
                        string limit = upper == null ? "0 以上" : $"0〜{upper}";
                        Log.Print($"[注意] {fileName} の {lineNo}行目「{column}」の {stats[key]} は、{limit} の中にそろえて使います");
                    }
                }
                if (kind == "プレイヤー")
                {
                    string qteText = Get(row, "QTE初期値");
                    int qteStart = ToInt(qteText == "" ? "0" : qteText, $"{fileName} の {lineNo}行目「QTE初期値」");
                    player = (name == "" ? "プレイヤー" : name, stats, qteStart);
                }
                else if (kind == "敵")
                {
                    if (name == "")
                    {
                        throw new DataError($"{fileName} の {lineNo}行目: 敵の名前が空です");
                    }
                    string where = $"{fileName} の {lineNo}行目「{CUT_COLUMN}」";
                    string text = Get(row, CUT_COLUMN);
                    if (text == "")
                    {
                        throw new DataError($"{where} が空です。敵には数字を書いてください");
                    }
                    if (!TryFloat(text, out double cut))
                    {
                        throw new DataError($"{where} が数字ではありません: 「{text}」");
                    }
                    if (cut < 0)
                    {
                        throw new DataError($"{where} は 0 以上にしてください: {Fmt(cut)}");
                    }
                    where = $"{fileName} の {lineNo}行目「{DEFENSE_COLUMN}」";
                    if (Get(row, DEFENSE_COLUMN) == "")
                    {
                        throw new DataError($"{where} が空です。敵には数字を書いてください");
                    }
                    int defense = ToInt(Get(row, DEFENSE_COLUMN), where);
                    if (!(0 <= defense && defense <= STAT_MAX))
                    {
                        Log.Print($"[注意] {where} の {defense} は、0〜{STAT_MAX} の中にそろえて使います");
                    }
                    string folder = Get(row, FOLDER_COLUMN);
                    if (folder == "")
                    {
                        throw new DataError($"{fileName} の {lineNo}行目「{FOLDER_COLUMN}」が空です。" +
                                            "敵には assets/images/Enemy の中の画像フォルダの名前(Oni など)を書いてください");
                    }
                    enemies[name] = new EnemySpeciesData
                    {
                        Attack = stats["attack"], Hp = stats["hp"], Mp = stats["mp"], Defense = defense, Cut = cut, Folder = folder,
                    };
                }
                else
                {
                    throw new DataError($"{fileName} の {lineNo}行目: 区分は「プレイヤー」か「敵」にしてください(今は「{kind}」)");
                }
            }
            if (player == null)
            {
                throw new DataError($"{fileName} にプレイヤーの行(区分が「プレイヤー」)がありません");
            }
            return (player.Value, enemies);
        }

        /// <summary>battle_rules.csv からルールの数値を読む。戻り値: {"QTE_ATTACK_RATE": 1.5, ...}</summary>
        static Dictionary<string, double> LoadRules()
        {
            string path = RulesCsv;
            string fileName = Path.GetFileName(path);
            var rules = new Dictionary<string, double>();
            int lineNo = 1;
            foreach (var row in ReadRows(path, new[] { "項目", "値" }))
            {
                lineNo++;
                string item = Get(row, "項目");
                if (item == "")
                {
                    continue;  // 空の行は飛ばす
                }
                if (!RULE_ITEMS.ContainsKey(item))
                {
                    throw new DataError($"{fileName} の {lineNo}行目: 知らない項目です「{item}」");
                }
                if (!TryFloat(Get(row, "値"), out double value))
                {
                    throw new DataError($"{fileName} の {lineNo}行目「{item}」の値が数字ではありません: 「{Get(row, "値")}」");
                }
                if (value < 0)
                {
                    throw new DataError($"{fileName} の {lineNo}行目「{item}」の値は 0 以上にしてください: {Fmt(value)}");
                }
                if ((RULE_ITEMS[item] == "SPECIAL_TIME" || RULE_ITEMS[item] == "DEFENSE_K") && value <= 0)
                {
                    throw new DataError($"{fileName} の {lineNo}行目「{item}」は 0 より大きくしてください: {Fmt(value)}");
                }
                rules[RULE_ITEMS[item]] = value;
            }
            var missing = RULE_ITEMS.Where(pair => !rules.ContainsKey(pair.Value)).Select(pair => pair.Key).ToList();
            if (missing.Count > 0)
            {
                throw new DataError($"{fileName} に項目がありません: {string.Join("、", missing)}");
            }
            return rules;
        }

        /// <summary>talismans.csv から呪符を読む(上から順に呪符1, 2, 3)</summary>
        static List<Talisman> LoadTalismans()
        {
            string path = TalismansCsv;
            string fileName = Path.GetFileName(path);
            var talismans = new List<Talisman>();
            int lineNo = 1;
            foreach (var row in ReadRows(path, new[] { "上がるパラメータ", "上がる量" }))
            {
                lineNo++;
                string statName = Get(row, "上がるパラメータ"), amountText = Get(row, "上がる量");
                if (statName == "" && amountText == "")
                {
                    continue;  // 空の行は飛ばす
                }
                if (!TALISMAN_STATS.ContainsKey(statName))
                {
                    throw new DataError($"{fileName} の {lineNo}行目「上がるパラメータ」は " +
                                        $"{string.Join("・", TALISMAN_STATS.Keys)} のどれかにしてください(今は「{statName}」)");
                }
                int amount = ToInt(amountText, $"{fileName} の {lineNo}行目「上がる量」");
                if (amount < 0)
                {
                    throw new DataError($"{fileName} の {lineNo}行目「上がる量」は 0 以上にしてください: {amount}");
                }
                talismans.Add(new Talisman { Stat = TALISMAN_STATS[statName], StatName = statName, Amount = amount });
            }
            if (talismans.Count == 0)
            {
                throw new DataError($"{fileName} に呪符が1枚もありません");
            }
            if (talismans.Count > TALISMAN_MAX)
            {
                throw new DataError($"{fileName} の呪符は {TALISMAN_MAX}枚までです(今は {talismans.Count}枚)");
            }
            return talismans;
        }

        /// <summary>animation.csv から動きごとの1コマの時間(秒)を読む。戻り値: {"待機": 0.2, ...}</summary>
        static Dictionary<string, double> LoadAnimation()
        {
            string path = AnimationCsv;
            string fileName = Path.GetFileName(path);
            var times = new Dictionary<string, double>();
            int lineNo = 1;
            foreach (var row in ReadRows(path, new[] { "動き", "1コマの時間(秒)" }))
            {
                lineNo++;
                string motion = Get(row, "動き");
                if (motion == "")
                {
                    continue;  // 空の行は飛ばす
                }
                if (!MOTIONS.Contains(motion))
                {
                    throw new DataError($"{fileName} の {lineNo}行目: 動きは {string.Join("・", MOTIONS)} のどれかにしてください(今は「{motion}」)");
                }
                string text = Get(row, "1コマの時間(秒)");
                if (!TryFloat(text, out double seconds))
                {
                    throw new DataError($"{fileName} の {lineNo}行目「{motion}」の時間が数字ではありません: 「{text}」");
                }
                if (seconds <= 0)
                {
                    throw new DataError($"{fileName} の {lineNo}行目「{motion}」の時間は 0 より大きくしてください: {Fmt(seconds)}");
                }
                times[motion] = seconds;
            }
            var missing = MOTIONS.Where(motion => !times.ContainsKey(motion)).ToList();
            if (missing.Count > 0)
            {
                throw new DataError($"{fileName} に動きがありません: {string.Join("、", missing)}");
            }
            return times;
        }

        /// <summary>bgm.csv から画面ごとの音量を読む。表は 0〜100、戻り値は 0〜1。戻り値: {"バトル": 0.6, ...}</summary>
        static Dictionary<string, double> LoadBgm()
        {
            string path = BgmCsv;
            string fileName = Path.GetFileName(path);
            var volumes = new Dictionary<string, double>();
            var names = BGM_SCENES.Values.ToList();
            int lineNo = 1;
            foreach (var row in ReadRows(path, new[] { "画面", "音量" }))
            {
                lineNo++;
                string name = Get(row, "画面");
                if (name == "")
                {
                    continue;  // 空の行は飛ばす
                }
                if (!names.Contains(name))
                {
                    throw new DataError($"{fileName} の {lineNo}行目: 画面は {string.Join("・", names)} のどれかにしてください(今は「{name}」)");
                }
                string text = Get(row, "音量");
                if (!TryFloat(text, out double value))
                {
                    throw new DataError($"{fileName} の {lineNo}行目「{name}」の音量が数字ではありません: 「{text}」");
                }
                if (!(0 <= value && value <= 100))
                {
                    throw new DataError($"{fileName} の {lineNo}行目「{name}」の音量は 0〜100 にしてください: {Fmt(value)}");
                }
                volumes[name] = value / 100;
            }
            var missing = names.Where(name => !volumes.ContainsKey(name)).ToList();
            if (missing.Count > 0)
            {
                throw new DataError($"{fileName} に画面がありません: {string.Join("、", missing)}");
            }
            return volumes;
        }

        /// <summary>表(CSV)を読みなおして、上の値を新しくする(どれかが読めなければ何も変えない)</summary>
        public static void Reload()
        {
            var (player, enemies) = LoadCharacters();
            var rules = LoadRules();
            var talismans = LoadTalismans();
            var frameTimes = LoadAnimation();
            var bgmVolumes = LoadBgm();
            BGM_VOLUMES = bgmVolumes;
            TALISMANS = talismans;
            FRAME_TIMES = frameTimes;
            (PLAYER_NAME, PLAYER_STATS, PLAYER_QTE_START) = player;
            ENEMY_SPECIES = enemies;
            QTE_ATTACK_RATE = rules["QTE_ATTACK_RATE"];
            SPECIAL_DAMAGE = (int)Math.Ceiling(Math.Round(rules["SPECIAL_DAMAGE"], 6));  // ダメージは整数(小数は切り上げ)
            SPECIAL_TIME = rules["SPECIAL_TIME"];
            DEFENSE_K = rules["DEFENSE_K"];
        }
    }

    /// <summary>CSV の読みとり(Python の csv モジュールと同じ書き方を読める: "..." でかこんだ値、"" で " を1つ)</summary>
    public static class Csv
    {
        public static List<List<string>> Parse(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false, fieldStarted = false;
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }
                        inQuotes = false;
                    }
                    else
                    {
                        field.Append(c);
                    }
                    i++;
                    continue;
                }
                if (c == '"' && field.Length == 0)
                {
                    inQuotes = true;
                    fieldStarted = true;
                }
                else if (c == ',')
                {
                    record.Add(field.ToString());
                    field.Clear();
                    fieldStarted = true;
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    if (fieldStarted || field.Length > 0 || record.Count > 0)
                    {
                        record.Add(field.ToString());
                        records.Add(record);
                    }
                    record = new List<string>();
                    field.Clear();
                    fieldStarted = false;
                }
                else
                {
                    field.Append(c);
                    fieldStarted = true;
                }
                i++;
            }
            if (fieldStarted || field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            return records;
        }
    }
}
