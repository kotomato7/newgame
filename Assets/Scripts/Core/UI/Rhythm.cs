// 防御QTE(リズムゲーム) (Pygame 版の ui/rhythm.py)
//
// 敵のこうげきが、テンポ(BPM)に合わせて「キー」になって落ちてくる。
// 判定ラインに来たときにそのキーを押せばガード成功。
//
// キーと敵の対応(キーボードの並びと、画面の敵の並びが同じ):
//     Q W E
//     A S D
//     Z X C
//   左の列 (Q A Z) = 左の敵、真ん中の列 (W S X) = 真ん中の敵、右の列 (E D C) = 右の敵
//
// ルール
//   ・判定ラインに来た時刻の前後 GOOD_WINDOW 秒のあいだに押せたら成功(ガード)
//   ・押せないまま通りすぎたら失敗(ミス)
//   ・受付中のキーがあるときに、ちがうキーを押すと、そのキーは失敗(でたらめ押し対策)
//   ・倒れた敵のキーは出ない
//
// キーの出かた(MakeRandomPattern)
//   ・生きている敵1体につき NOTES_PER_ENEMY(5)個のキーが流れてくる
//   ・どのキーが出るかは、その敵の3つのキーからランダム(例: 左の敵なら Q・A・Z のどれか)
//   ・3体のキーがどの順番で流れてくるかもランダム
//   ・1つが1拍
using System;
using System.Collections.Generic;
using System.Linq;

namespace QteRpg.UI
{
    /// <summary>落ちてくるキー1つ</summary>
    public sealed class Note
    {
        public readonly string Key;   // "Q" など
        public readonly int Lane;     // 0=左 1=真ん中 2=右(= 敵の位置)
        public readonly double Time;  // 判定ラインに着く時刻(秒)
        public bool? Hit;             // null = まだ、true = ガード、false = ミス

        public Note(string key, double time)
        {
            Key = key;
            Lane = DefenseRhythm.KEY_LANES[key];
            Time = time;
            Hit = null;
        }
    }

    /// <summary>「ガード」「ミス」の表示</summary>
    public sealed class RhythmPopup
    {
        public int Lane;
        public string Text;
        public Rgba Color;
        public double Timer;
    }

    public sealed class DefenseRhythm
    {
        public const int BPM = 100;               // テンポ(1分間の拍の数)。100 なら1拍 0.6秒
        public const int NOTES_PER_ENEMY = 5;     // 敵1体あたりのキーの数(= こうげきの回数)
        public const double GOOD_WINDOW = 0.15;   // 受付時間: 判定ラインに来た時刻の前後この秒数
        public const int TRAVEL_BEATS = 2;        // キーが出てきてから判定ラインに着くまでの拍数
        public const int LEAD_BEATS = 3;          // 防御QTEが始まってから、最初のキーが判定ラインに着くまでの拍数
        public const double POPUP_TIME = 0.5;     // 「ガード」「ミス」の表示時間

        public static readonly Dictionary<string, int> KEY_LANES = new Dictionary<string, int>
        {
            { "Q", 0 }, { "A", 0 }, { "Z", 0 }, { "W", 1 }, { "S", 1 }, { "X", 1 }, { "E", 2 }, { "D", 2 }, { "C", 2 },
        };
        public static readonly Dictionary<int, string[]> LANE_KEYS = new Dictionary<int, string[]>  // 列(敵)ごとのキー
        {
            { 0, new[] { "Q", "A", "Z" } }, { 1, new[] { "W", "S", "X" } }, { 2, new[] { "E", "D", "C" } },
        };
        public static readonly Dictionary<GameKey, string> KEY_CODES = new Dictionary<GameKey, string>
        {
            { GameKey.Q, "Q" }, { GameKey.W, "W" }, { GameKey.E, "E" },
            { GameKey.A, "A" }, { GameKey.S, "S" }, { GameKey.D, "D" },
            { GameKey.Z, "Z" }, { GameKey.X, "X" }, { GameKey.C, "C" },
        };

        static readonly Random Rng = new Random();

        /// <summary>
        /// キーの並びを作る関数。ふだんは MakeRandomPattern(ランダム)。
        /// 決まった並びでためしたいときは、ここを別の関数に差しかえる
        /// </summary>
        public static Func<IEnumerable<int>, List<string>> PatternMaker = lanes => MakeRandomPattern(lanes);

        /// <summary>
        /// 生きている敵の列ごとに perEnemy 個ずつキーを選び、全体の順番をシャッフルしたリストを返す
        ///   1. 敵ごとに「列の番号」を perEnemy 個ならべる   例: [0,0,0,0,0, 1,1,1,1,1, 2,2,2,2,2]
        ///   2. その並びをシャッフルする(どの敵から来るかの順番がランダムになる)
        ///   3. 1つずつ、その列の3つのキーからランダムに1つ選ぶ
        /// </summary>
        public static List<string> MakeRandomPattern(IEnumerable<int> activeLanes, int perEnemy = NOTES_PER_ENEMY, Random rng = null)
        {
            rng = rng ?? Rng;
            var lanes = new List<int>();
            foreach (int lane in activeLanes.Distinct().OrderBy(lane => lane))
            {
                for (int i = 0; i < perEnemy; i++)
                {
                    lanes.Add(lane);
                }
            }
            for (int i = lanes.Count - 1; i > 0; i--)  // シャッフル
            {
                int j = rng.Next(i + 1);
                (lanes[i], lanes[j]) = (lanes[j], lanes[i]);
            }
            return lanes.Select(lane => LANE_KEYS[lane][rng.Next(LANE_KEYS[lane].Length)]).ToList();
        }

        public readonly double Beat;
        public double Time;
        public readonly HashSet<int> ActiveLanes;
        public readonly List<Note> Notes = new List<Note>();
        readonly double _endTime;
        List<Note> _judged = new List<Note>();  // 判定が出たばかりのキー(バトル側が PopJudged で受け取る)
        public List<RhythmPopup> Popups = new List<RhythmPopup>();

        /// <summary>
        /// activeLanes … 生きている敵の位置の番号(0〜2)。ここにない列のキーは出さない
        /// pattern … キーの並び("-" はおやすみの拍)。省略すると PatternMaker でランダムに作る
        /// </summary>
        public DefenseRhythm(IEnumerable<int> activeLanes, IList<string> pattern = null, int bpm = BPM)
        {
            var lanes = activeLanes.ToList();
            if (pattern == null)
            {
                pattern = PatternMaker(lanes);
            }
            Beat = 60.0 / bpm;
            Time = 0.0;
            ActiveLanes = new HashSet<int>(lanes);
            for (int i = 0; i < pattern.Count; i++)
            {
                string key = pattern[i].Trim().ToUpperInvariant();
                if (KEY_LANES.TryGetValue(key, out int lane) && ActiveLanes.Contains(lane))
                {
                    Notes.Add(new Note(key, (LEAD_BEATS + i) * Beat));
                }
            }
            double last = Notes.Count > 0 ? Notes[Notes.Count - 1].Time : LEAD_BEATS * Beat;
            _endTime = last + GOOD_WINDOW;
        }

        // ---- 情報 ----
        public int NotesInLane(int lane) => Notes.Count(note => note.Lane == lane);

        /// <summary>その列(敵)のキーのうち、ガードできた数</summary>
        public int HitsInLane(int lane) => Notes.Count(note => note.Lane == lane && note.Hit == true);

        public int Hits => Notes.Count(note => note.Hit == true);

        public bool Finished => Time > _endTime && Notes.All(note => note.Hit != null);

        /// <summary>今、受付時間に入っているキー(早い順)</summary>
        public List<Note> PendingInWindow() =>
            Notes.Where(note => note.Hit == null && Math.Abs(note.Time - Time) <= GOOD_WINDOW).ToList();

        /// <summary>前に呼んでから判定が出たキーを返す</summary>
        public List<Note> PopJudged()
        {
            var judged = _judged;
            _judged = new List<Note>();
            return judged;
        }

        // ---- 入力・更新 ----
        public void HandleEvent(GameEvent e)
        {
            if (e.Type != GameEventType.KeyDown || !KEY_CODES.ContainsKey(e.Key))
            {
                return;
            }
            var pending = PendingInWindow();
            if (pending.Count == 0)
            {
                return;  // 受付中のキーがないときは何もしない
            }
            string key = KEY_CODES[e.Key];
            var match = pending.Where(note => note.Key == key).ToList();
            if (match.Count > 0)
            {
                Judge(match[0], true);
            }
            else
            {
                Judge(pending[0], false);  // ちがうキー
            }
        }

        public void Update(double dt)
        {
            Time += dt;
            foreach (var note in Notes)
            {
                if (note.Hit == null && Time > note.Time + GOOD_WINDOW)
                {
                    Judge(note, false);  // 通りすぎた
                }
            }
            foreach (var popup in Popups)
            {
                popup.Timer -= dt;
            }
            Popups = Popups.Where(p => p.Timer > 0).ToList();
        }

        void Judge(Note note, bool hit)
        {
            note.Hit = hit;
            _judged.Add(note);
            if (hit)
            {
                Popups.Add(new RhythmPopup { Lane = note.Lane, Text = "ガード", Color = Settings.GLOW, Timer = POPUP_TIME });
            }
            else
            {
                Popups.Add(new RhythmPopup { Lane = note.Lane, Text = "ミス", Color = Settings.DAMAGE_RED, Timer = POPUP_TIME });
            }
        }
    }

    public static class RhythmView
    {
        const int NOTE_SIZE = 18;
        static readonly Rgba LANE_COLOR = new Rgba(55, 55, 55);

        static Rgba Mix(Rgba a, Rgba b, double t) => new Rgba(
            Py.Round(a.R + (b.R - a.R) * t), Py.Round(a.G + (b.G - a.G) * t), Py.Round(a.B + (b.B - a.B) * t));

        /// <summary>laneXs … 列ごとの x 座標(敵の位置)、topY … キーが出てくる高さ、judgeY … 判定ラインの高さ</summary>
        public static void DrawRhythm(Surface surface, DefenseRhythm rhythm, IList<int> laneXs, int topY, int judgeY)
        {
            double beatPos = rhythm.Time / rhythm.Beat;
            double pulse = 1 - Py.Mod(beatPos, 1.0);  // 拍の頭で 1、次の拍にかけて 0 へ(判定ラインが拍に合わせて光る)
            if (rhythm.Time < 0)
            {
                pulse = 0;
            }

            // 列(縦の線)と判定ライン
            var lanes = rhythm.ActiveLanes.OrderBy(lane => lane).ToList();
            foreach (int lane in lanes)
            {
                int x = laneXs[lane];
                Draw.Line(surface, LANE_COLOR, (x, topY), (x, judgeY), 1);
            }
            int left = laneXs[0] - 34, right = laneXs[laneXs.Count - 1] + 34;
            Draw.Line(surface, Mix(Settings.GRAY, Settings.WHITE, pulse), (left, judgeY), (right, judgeY), 1);

            // 判定ラインの上のわく(受付中のキーがある列は光る)
            var pendingLanes = new HashSet<int>(rhythm.PendingInWindow().Select(note => note.Lane));
            foreach (int lane in lanes)
            {
                var box = PgRect.Anchored(NOTE_SIZE + 4, NOTE_SIZE + 4, Anchor.Center, laneXs[lane], judgeY);
                var color = pendingLanes.Contains(lane) ? Settings.GLOW : Mix(Settings.GRAY, Settings.WHITE, pulse);
                Draw.Rect(surface, Settings.BLACK, box);
                Draw.Rect(surface, color, box, 1);
            }

            // 落ちてくるキー
            double travel = DefenseRhythm.TRAVEL_BEATS * rhythm.Beat;
            foreach (var note in rhythm.Notes)
            {
                if (note.Hit != null)
                {
                    continue;
                }
                double progress = 1 - (note.Time - rhythm.Time) / travel;  // 0 = 出てきた所、1 = 判定ライン
                if (progress < 0)
                {
                    continue;
                }
                int y = Py.Round(topY + (judgeY - topY) * progress);
                var box = PgRect.Anchored(NOTE_SIZE, NOTE_SIZE, Anchor.Center, laneXs[note.Lane], y);
                Draw.Rect(surface, Settings.BLACK, box);
                Draw.Rect(surface, Settings.WHITE, box, 1);
                Utils.DrawText(surface, note.Key, Settings.FONT_SMALL, Settings.WHITE, Anchor.Center, box.CenterX, box.CenterY - 1);
            }

            // 「ガード」「ミス」(少し上にあがりながら消える)
            foreach (var popup in rhythm.Popups)
            {
                int rise = Py.Round((1 - popup.Timer / DefenseRhythm.POPUP_TIME) * 8);
                Utils.DrawText(surface, popup.Text, Settings.FONT_SMALL, popup.Color, Anchor.Center, laneXs[popup.Lane], judgeY - 22 - rise);
            }

            // 始まりの合図(最初のキーが出てくるまで)
            double firstSpawn = (DefenseRhythm.LEAD_BEATS - DefenseRhythm.TRAVEL_BEATS) * rhythm.Beat;
            if (rhythm.Time < firstSpawn + rhythm.Beat * 0.5)
            {
                Utils.DrawText(surface, "ぼうぎょ！", Settings.FONT_LARGE, Settings.WHITE, Anchor.Center,
                               Py.FloorDiv(left + right, 2), Py.FloorDiv(topY + judgeY, 2));
            }
        }
    }
}
