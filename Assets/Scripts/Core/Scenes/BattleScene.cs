// バトル画面(ホワイトボックス版) (Pygame 版の scenes/battle.py)
//
// 画面の状態 (_state):
//   "command" … たたかう / ひっさつ / にげる を選ぶ
//   "fight"   … たたかう の中身(こうげき / じゅふ / やめる)を選ぶ
//   "target"  … こうげきする敵を選ぶ(←→ かマウス)
//   "jufu"    … 使う呪符を選ぶ
//   "action"  … 戦闘中(この間は QTE の入力だけ受けつける)
//   "result"  … 勝ち / 負け の表示
//
// ターン(1回の戦闘):
//   プレイヤーがコマンドを決めたら始まる。順番はいつも同じ
//     1. 防御QTEターン … 敵のこうげき。リズムゲーム(UI/Rhythm.cs)
//     2. 攻撃QTEターン … プレイヤーの行動(こうげき / じゅふ)。プレイヤーはいつも最後
//   ひっさつを選んだときは 防御QTE はなく、ひっさつ(星をえがく)だけ
//   HPの丸の赤い部分(前のターンに減った分)は、次のターンが始まったときに欠ける
//
// 防御QTE(UI/Rhythm.cs):
//   敵の真下の列を、キー(Q W E / A S D / Z X C)が拍に合わせて落ちてくる。判定ラインで押すとガード
//   左の列 Q A Z = 左の敵、W S X = 真ん中の敵、E D C = 右の敵
//   生きている敵1体につき5個。キーはその敵の3つからランダム、どの敵から来るかの順番もランダム
//   QTEの間はダメージを受けない。全部終わってから、敵が 左→真ん中→右 の順に少しずつずらしてこうげきし、1体ずつダメージを受ける
//   受けるダメージ(敵1体ごと) = 攻撃力 − ダメージカット率K × (ガードできた数 ÷ その敵のキーの数)
//   (全部ガード = 攻撃力 − K、全部ミス = 攻撃力そのまま。K は data/characters.csv で敵ごとに決める)
//
// 攻撃QTE(UI/Qte.cs):
//   A → S → D → F の順に、時間内に押せたら成功。与えるダメージが倍率ぶん増え、QTEポイントが1増える
//   (倍率は data/battle_rules.csv で変えられる)
//
// じゅふ(呪符):
//   呪符(data/talismans.csv。3枚まで)を1枚えらんで使う。攻撃力が、そのバトルのあいだ上がる
//   1枚ずつ、1回のバトルで1回だけ使える(使った呪符はグレーになる)
//
// ひっさつ(攻撃QTEに5回成功して、QTEポイントが5つたまると選べる):
//   マウスで星をえがく(UI/StarDrawing.cs)。えがけたら敵全体にダメージ(量は data/battle_rules.csv)
//
// キャラのしくみは Characters.cs、パラメータの数値は data/characters.csv にある
//
// Python 版の「ジェネレーター(yield で1ステップずつ進む戦闘)」は、C# の IEnumerator(yield return)で同じように書いている
//   yield return null  … 次のフレームまで待つ
//   yield return 秒数  … その秒数だけ待つ
using System;
using System.Collections.Generic;
using System.Linq;
using QteRpg.UI;

namespace QteRpg.Scenes
{
    public sealed class BattleScene : Scene
    {
        const int W = Settings.SCREEN_WIDTH, H = Settings.SCREEN_HEIGHT;

        // コマンド欄の位置と大きさ (x, y, 幅, 高さ)
        static readonly PgRect COMMAND_RECT = new PgRect(8, H - 8 - 62, 86, 62);                            // 左下
        static readonly PgRect FIGHT_RECT = new PgRect(COMMAND_RECT.Right + 4, COMMAND_RECT.Top, 86, 62);   // その右
        const int MENU_GAP = 18;  // コマンドの行の間隔

        // 今回のバトルに出てくる敵(種族名)。data/characters.csv の「敵」の行にある名前を書く
        // 並び順 = 画面での位置(左・真ん中・右)
        static readonly string[] BATTLE_ENEMIES = { "怨霊", "鬼", "骸骨" };

        // 敵を置く場所: (画像がないときの白い四角の大きさ, 足元の位置)。左から順に
        // 真ん中は大きく表示する鬼なので、少し手前(下)に置いて頭が上のHPバーに重ならないようにする
        static readonly ((int w, int h) size, (int x, int y) foot)[] ENEMY_SLOTS =
        {
            ((32, 36), (W / 2 - 100, 92)),
            ((34, 38), (W / 2, 94)),
            ((34, 38), (W / 2 + 100, 92)),
        };

        // 敵の表示倍率(種族名: 倍率)。ここにない敵は 1 倍(画像そのままの大きさ)
        static readonly Dictionary<string, double> ENEMY_SCALES = new Dictionary<string, double>();  // 例: { { "鬼", 1.3 } }
        static readonly ((int w, int h) size, (int x, int y) foot) PLAYER_SLOT = ((40, 60), (W / 2, H - 8));  // 手前・下側の真ん中。近いので大きめ
        const int HP_BAR_TOP = 10;  // 敵のHPバーを出す高さ(画面の上部)

        const int SPECIAL_INDEX = 1;  // コマンド欄での「ひっさつ」の番号(0=たたかう, 1=ひっさつ, 2=にげる)
        const int JUFU_INDEX = 1;     // たたかう の中での「じゅふ」の番号(0=こうげき, 1=じゅふ, 2=やめる)
        const double NOTICE_TIME = 0.9;  // 「攻撃力 +5」などの表示時間

        const int EMBLEM_RADIUS = 34;  // 右下の紋章(HPの丸・QTEポイント)の大きさ
        static readonly (int x, int y) EMBLEM_CENTER = (W - EMBLEM_RADIUS - 10, H - EMBLEM_RADIUS - 10);

        // 戦闘の間(秒)
        const double HIT_DELAY = Actor.LUNGE_TIME / 2;  // 踏み込みはじめてから当たるまで
        const double AFTER_HIT = 0.45;            // 当たってから次へ進むまで
        const double BETWEEN_ACTIONS = 0.2;       // 行動と行動のあいだ
        const double RESULT_DELAY = 0.8;          // 決着してから結果を出すまで
        const double DEFENSE_END_WAIT = 0.6;      // 防御QTEが終わってから「ガード 12/15」などを見せる時間
        const double ENEMY_ATTACK_STAGGER = 0.1;  // 防御QTEのあと、敵が 左→真ん中→右 の順に踏み込むときのずらし(秒)
        const double QTE_RESULT_TIME = 0.7;       // SUCCESS! / Failed を表示する時間
        static readonly (int x, int y) QTE_CENTER = (W / 2, 150);   // QTE を出す位置(画面の真ん中あたり)
        static readonly (int x, int y) STAR_CENTER = (W / 2, 140);  // ひっさつで星をえがく場所
        const int STAR_RADIUS = 62;
        const int RHYTHM_TOP = 120;      // 防御QTEのキーが出てくる高さ(敵の名前の札のすぐ下)
        const int RHYTHM_JUDGE_Y = 186;  // 防御QTEの判定ラインの高さ
        static readonly int[] LANE_XS = ENEMY_SLOTS.Select(slot => slot.foot.x).ToArray();  // 防御QTEの列の位置(= 敵の位置)

        const GameKey DEBUG_DAMAGE_KEY = GameKey.F1;  // デバッグ: このキーでプレイヤーにダメージ(D は QTE で使うので F1)
        const int DEBUG_DAMAGE = 3;

        static readonly GameKey[] PREV_KEYS = new[] { GameKey.Left }.Concat(SelectList.UP_KEYS).ToArray();     // ターゲット選択で左へ
        static readonly GameKey[] NEXT_KEYS = new[] { GameKey.Right }.Concat(SelectList.DOWN_KEYS).ToArray();  // ターゲット選択で右へ

        // キャラのデータと見た目。バトル開始時(OnEnter)に作る
        Player _player;                                     // Player(パラメータ)
        List<Enemy> _enemies = new List<Enemy>();           // Enemy のリスト(パラメータ)
        Actor _playerActor;                                 // 見た目
        List<Actor> _enemyActors = new List<Actor>();
        List<HpBar> _hpBars = new List<HpBar>();            // 敵のHPバー
        Surface _background;

        // 右下の紋章(HPの丸 + 五芒星 + QTEポイント)
        readonly StatusEmblem _emblem = new StatusEmblem(EMBLEM_CENTER, EMBLEM_RADIUS);

        readonly SelectList _commandMenu;
        readonly SelectList _fightMenu;
        string _state = "command";
        int _turn = 1;
        int _targetIndex;             // ターゲット選択中の敵の番号
        IEnumerator<double?> _round;  // 戦闘を1ステップずつ進めるためのもの(下の RunRound)
        double _wait;                 // 次のステップまでの待ち時間
        string _result;               // "win" か "lose"
        double _resultTimer;
        AttackQTE _qte;               // 受付中の攻撃QTE(なければ null)
        DefenseRhythm _rhythm;        // 防御QTE(リズムゲーム)の途中(なければ null)
        Enemy _qteTarget;             // プレイヤーの QTE 中にねらっている敵(金色のわくを出す)
        StarDrawing _star;            // ひっさつで星をえがいている途中(なければ null)
        bool _qteFlash;               // 直前の QTE の結果(true / false)。SUCCESS! / Failed の表示用
        double _qteFlashTimer;
        SelectList _talismanMenu;     // 呪符をえらぶ欄(バトル開始時に表から作る)
        PgRect _talismanRect;
        HashSet<int> _usedTalismans = new HashSet<int>();  // このバトルで使った呪符の番号
        string _notice = "";          // 真ん中に少し出す文字(「攻撃力 +5」など)
        double _noticeTimer;
        bool _subResult;              // QTE・星えがきの結果(Python 版の「yield from の戻り値」のかわり)
        readonly Surface _shade = new Surface(W, H);  // 画面を暗くする黒い板

        public BattleScene(Game game) : base(game)
        {
            _commandMenu = new SelectList(new[] { "たたかう", "ひっさつ", "にげる" },
                                          top: COMMAND_RECT.Top + 1, left: COMMAND_RECT.Left + 16, gap: MENU_GAP);
            _fightMenu = new SelectList(new[] { "こうげき", "じゅふ", "やめる" },
                                        top: FIGHT_RECT.Top + 1, left: FIGHT_RECT.Left + 16, gap: MENU_GAP);
            _shade.Fill(Settings.BLACK);
        }

        public override void OnEnter()
        {
            // プレイヤーは Game が持っているものを使う(バトルをまたいで引きつぐ)
            _player = Game.Player;
            // 背景は assets/images/Background/Battle から、バトルのたびに読みなおす(なければ こい灰色)
            _background = Sprites.LoadBackground("Battle");
            // 画像は バトルのたびに assets/images から読みなおす(なければ白い四角)
            _playerActor = new Actor(_player, PLAYER_SLOT.size, PLAYER_SLOT.foot, labelSide: null, lungeDir: (0, -1),
                                     animations: Sprites.PlayerAnimations());
            // 敵は毎回新しく作る(種族のパラメータで満タンの状態)
            var missing = BATTLE_ENEMIES.Where(species => !GameData.ENEMY_SPECIES.ContainsKey(species)).ToList();
            if (missing.Count > 0)
            {
                Log.Print($"[パラメータ表にない敵は出しません] {string.Join("、", missing)}");
            }
            _enemies = BATTLE_ENEMIES.Where(species => GameData.ENEMY_SPECIES.ContainsKey(species))
                                     .Select(species => new Enemy(species)).ToList();
            _enemyActors = new List<Actor>();
            _hpBars = new List<HpBar>();
            for (int i = 0; i < Math.Min(_enemies.Count, ENEMY_SLOTS.Length); i++)
            {
                var enemy = _enemies[i];
                var (size, footPos) = ENEMY_SLOTS[i];
                double scale = ENEMY_SCALES.GetOr(enemy.Species, 1.0);
                size = (Py.Round(size.w * scale), Py.Round(size.h * scale));  // 画像がないときの白い四角も同じ倍率に
                var animations = Sprites.ScaleAnimations(Sprites.EnemyAnimations(enemy.Folder), scale);
                _enemyActors.Add(new Actor(enemy, size, footPos, lungeDir: (0, 1), animations: animations));
                _hpBars.Add(new HpBar(enemy, footPos.x, HP_BAR_TOP));
            }
            if (_enemies.Count > ENEMY_SLOTS.Length)
            {
                _enemies = _enemies.Take(ENEMY_SLOTS.Length).ToList();
            }

            _commandMenu.Reset();
            _fightMenu.Reset();
            _state = "command";
            _turn = 1;
            _round = null;
            _result = null;
            _qte = null;
            _rhythm = null;
            _qteTarget = null;
            _star = null;
            _qteFlashTimer = 0.0;
            _noticeTimer = 0.0;
            // 呪符: バトルのたびに全部使える状態にもどし、上がったパラメータももとにもどす
            _player.ClearBuffs();
            _usedTalismans = new HashSet<int>();
            BuildTalismanMenu();
            _emblem.StartTurn(_player);
            RefreshCommands();
        }

        /// <summary>呪符をえらぶ欄を作る。表示は「攻撃力+5」のように、上がるパラメータと量</summary>
        void BuildTalismanMenu()
        {
            var labels = GameData.TALISMANS.Select(t => $"{t.StatName}+{t.Amount}").Concat(new[] { "やめる" }).ToList();
            int height = 8 + MENU_GAP * labels.Count;
            _talismanRect = new PgRect(FIGHT_RECT.Left, FIGHT_RECT.Top - 4 - height, 104, height);  // たたかう欄の上
            _talismanMenu = new SelectList(labels, top: _talismanRect.Top + 1, left: _talismanRect.Left + 16, gap: MENU_GAP);
        }

        /// <summary>
        /// ひっさつは QTEポイントが5つたまっているときだけ選べて、文字が光る。それ以外はグレー
        /// じゅふは使える呪符が残っているときだけ選べる。使った呪符はグレー
        /// </summary>
        void RefreshCommands()
        {
            bool ready = _player.QteFull;
            _commandMenu.SetEnabled(SPECIAL_INDEX, ready);
            _commandMenu.SetGlowing(SPECIAL_INDEX, ready);
            if (_talismanMenu == null)
            {
                return;
            }
            int count = _talismanMenu.Labels.Count - 1;  // 呪符の数(最後の「やめる」をのぞく)
            for (int i = 0; i < count; i++)
            {
                _talismanMenu.SetEnabled(i, !_usedTalismans.Contains(i));
            }
            _fightMenu.SetEnabled(JUFU_INDEX, _usedTalismans.Count < count);
        }

        Actor ActorOf(Character unit)
        {
            if (unit == _player)
            {
                return _playerActor;
            }
            return _enemyActors[_enemies.IndexOf((Enemy)unit)];
        }

        /// <summary>unit の HP を amount 減らして、右上にダメージの数字を出す</summary>
        void DealDamage(Character unit, int amount)
        {
            unit.TakeDamage(amount);
            ActorOf(unit).ShowDamage(amount, unit == _player ? Settings.DAMAGE_RED : Settings.WHITE);
        }

        List<int> AliveIndexes()
        {
            var alive = new List<int>();
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].IsAlive)
                {
                    alive.Add(i);
                }
            }
            return alive;
        }

        // ---- 入力 ----
        public override void HandleEvent(GameEvent e)
        {
            if (e.Type == GameEventType.KeyDown && e.Key == DEBUG_DAMAGE_KEY &&
                (_state == "command" || _state == "fight" || _state == "target" || _state == "jufu"))
            {
                DealDamage(_player, DEBUG_DAMAGE);  // デバッグ用
                if (!_player.IsAlive)
                {
                    Finish("lose");
                }
                return;
            }

            switch (_state)
            {
                case "command":
                    HandleCommand(e);
                    break;
                case "fight":
                    HandleFight(e);
                    break;
                case "target":
                    HandleTarget(e);
                    break;
                case "jufu":
                    HandleJufu(e);
                    break;
                case "result":
                    if (_resultTimer >= RESULT_DELAY && SelectList.IsConfirm(e))
                    {
                        Game.ChangeScene("title");
                    }
                    break;
                case "action":
                    // 戦闘中は QTE の入力だけ受けつける
                    if (_qte != null)
                    {
                        _qte.HandleEvent(e);
                    }
                    else if (_rhythm != null)
                    {
                        _rhythm.HandleEvent(e);
                    }
                    else if (_star != null)
                    {
                        _star.HandleEvent(e);
                    }
                    break;
            }
        }

        void HandleCommand(GameEvent e)
        {
            int? choice = _commandMenu.HandleEvent(e);
            if (choice == 0)  // たたかう → サブメニューへ
            {
                _fightMenu.Reset();
                _state = "fight";
            }
            else if (choice == 1)  // ひっさつ(QTEポイントが5つのときだけ選べる)
            {
                if (_player.QteFull)
                {
                    StartRound("special");
                }
            }
            else if (choice == 2)  // にげる → タイトルへ
            {
                Game.ChangeScene("title");
            }
        }

        void HandleFight(GameEvent e)
        {
            if (SelectList.IsCancel(e))  // ESC・右クリックなどで1つ前にもどる
            {
                _state = "command";
                return;
            }
            int? choice = _fightMenu.HandleEvent(e);
            if (choice == 0)  // こうげき → 敵を選ぶ
            {
                var alive = AliveIndexes();
                if (alive.Count == 0)
                {
                    return;
                }
                if (!alive.Contains(_targetIndex))
                {
                    _targetIndex = alive[0];
                }
                _state = "target";
            }
            else if (choice == 1)  // じゅふ → 呪符をえらぶ(使える呪符があるときだけ選べる)
            {
                _talismanMenu.Reset();
                var unused = Enumerable.Range(0, _talismanMenu.Labels.Count - 1).Where(i => !_usedTalismans.Contains(i)).ToList();
                if (unused.Count > 0)
                {
                    _talismanMenu.Selected = unused[0];
                    _state = "jufu";
                }
            }
            else if (choice == 2)  // やめる → たたかう / ひっさつ / にげる にもどる
            {
                _state = "command";
            }
        }

        void HandleJufu(GameEvent e)
        {
            if (SelectList.IsCancel(e))
            {
                _state = "fight";
                return;
            }
            int? choice = _talismanMenu.HandleEvent(e);
            if (choice == null)
            {
                return;
            }
            if (choice == _talismanMenu.Labels.Count - 1)  // やめる
            {
                _state = "fight";
            }
            else  // グレー(使用ずみ)の呪符は SelectList が反応しないようにしている
            {
                StartRound("jufu", talisman: choice.Value);
            }
        }

        void HandleTarget(GameEvent e)
        {
            if (SelectList.IsCancel(e))
            {
                _state = "fight";
                return;
            }
            var alive = AliveIndexes();
            if (e.Type == GameEventType.KeyDown)
            {
                int pos = alive.IndexOf(_targetIndex);
                if (PREV_KEYS.Contains(e.Key))
                {
                    _targetIndex = alive[Py.Mod(pos - 1, alive.Count)];
                }
                else if (NEXT_KEYS.Contains(e.Key))
                {
                    _targetIndex = alive[Py.Mod(pos + 1, alive.Count)];
                }
                else if (SelectList.CONFIRM_KEYS.Contains(e.Key))
                {
                    StartRound("attack", _enemies[_targetIndex]);
                }
            }
            else if (e.Type == GameEventType.MouseMotion || e.Type == GameEventType.MouseButtonDown)
            {
                foreach (int i in alive)
                {
                    if (_enemyActors[i].Rect.Inflate(12, 12).CollidePoint(e.X, e.Y))
                    {
                        _targetIndex = i;
                        if (e.Type == GameEventType.MouseButtonDown && e.Button == 1)
                        {
                            StartRound("attack", _enemies[i]);
                        }
                        break;
                    }
                }
            }
        }

        // ---- 戦闘(1ターン) ----
        /// <summary>
        /// プレイヤーのコマンドが決まったら呼ぶ。ここからターンが始まる
        /// target は こうげきする敵、talisman は 使う呪符の番号
        /// </summary>
        void StartRound(string action, Enemy target = null, int talisman = 0)
        {
            _emblem.StartTurn(_player);  // 前のターンに減ったHP(赤い部分)がここで欠ける
            _state = "action";
            _round = RunRound(action, target, talisman);
            _wait = 0.0;
        }

        /// <summary>
        /// 1ターン分の戦闘。yield return で「何秒待つか」を返しながら1ステップずつ進む
        /// 順番: 防御QTE(敵のこうげき) → プレイヤーの行動。ひっさつのときは防御QTEなし
        /// </summary>
        IEnumerator<double?> RunRound(string action, Enemy target, int talisman)
        {
            if (action != "special")
            {
                foreach (var step in DefensePhase()) yield return step;
                if (CheckFinish())
                {
                    yield break;
                }
                yield return BETWEEN_ACTIONS;
            }
            foreach (var step in PlayerAction(action, target, talisman)) yield return step;
            if (CheckFinish())
            {
                yield break;
            }
            // 全員の行動が終わった → 次のターンへ
            _turn++;
            _state = "command";
        }

        /// <summary>
        /// 防御QTE: 敵のこうげきがリズムに合わせてキーになって落ちてくる
        /// QTEの間はダメージを受けない。全部終わってから、ガードできた数に応じたダメージをまとめて受ける
        ///   その敵から受けるダメージ = 攻撃力 − ダメージカット率K × (ガードできた数 ÷ その敵のキーの数)
        /// キーが1つもない敵は、このターンはこうげきしない
        /// </summary>
        IEnumerable<double?> DefensePhase()
        {
            var lanes = AliveIndexes();
            var rhythm = new DefenseRhythm(lanes);
            _rhythm = rhythm;
            while (!rhythm.Finished)
            {
                yield return null;  // 次のフレームまで待つ(入力は HandleEvent、時間は Update で進む)
            }
            _rhythm = null;
            if (rhythm.Notes.Count == 0)
            {
                yield break;
            }
            _notice = $"ガード {rhythm.Hits}/{rhythm.Notes.Count}";
            _noticeTimer = NOTICE_TIME;
            yield return DEFENSE_END_WAIT;

            // ガードの結果に応じて、敵が 左→真ん中→右 の順に ENEMY_ATTACK_STAGGER 秒ずつずらして踏み込む
            // それぞれの攻撃の動きが終わったときに、その敵の分のダメージを受ける
            var events = new List<(double when, int phase, int order, string kind, Enemy enemy, int damage)>();
            int order = 0;
            foreach (int lane in lanes)  // lanes は左から順
            {
                int count = rhythm.NotesInLane(lane);
                if (count == 0)
                {
                    continue;
                }
                var enemy = _enemies[lane];
                int damage = enemy.DamageAfterGuard((double)rhythm.HitsInLane(lane) / count);
                double start = order * ENEMY_ATTACK_STAGGER;
                double hit = start + Math.Max(HIT_DELAY, ActorOf(enemy).MotionTime("攻撃"));
                events.Add((start, 0, order, "start", enemy, damage));
                events.Add((hit, 1, order, "hit", enemy, damage));
                order++;
            }
            if (events.Count == 0)
            {
                yield break;
            }
            // 時刻の早い順(同じ時刻なら 踏み込み → 当たり)
            events = events.OrderBy(ev => ev.when).ThenBy(ev => ev.phase).ThenBy(ev => ev.order).ToList();
            double now = 0.0;
            foreach (var ev in events)
            {
                if (ev.when > now)
                {
                    yield return ev.when - now;
                    now = ev.when;
                }
                if (ev.kind == "start")
                {
                    var actor = ActorOf(ev.enemy);
                    actor.Lunge();
                    actor.Play("攻撃");
                }
                else if (_player.IsAlive)  // 倒れたあとの攻撃は当たらない
                {
                    DealDamage(_player, ev.damage);
                    _playerActor.Flash();
                    _playerActor.Play("ダメージ");
                }
            }
            yield return Math.Max(AFTER_HIT, _playerActor.MotionTime("ダメージ"));
        }

        IEnumerable<double?> PlayerAction(string action, Enemy target, int talisman)
        {
            var actor = _playerActor;
            if (action == "attack")
            {
                if (target == null || !target.IsAlive)  // ねらった敵がもういなければ別の敵へ
                {
                    target = _enemies.First(enemy => enemy.IsAlive);
                }
                _qteTarget = target;  // どの敵をねらっているか見せておく
                foreach (var step in RunAttackQte()) yield return step;
                bool success = _subResult;
                _qteTarget = null;
                foreach (var step in AttackMotion(actor)) yield return step;
                double rate = success ? GameData.QTE_ATTACK_RATE : 1.0;
                int damage = _player.NormalAttack(target, rate);
                ActorOf(target).ShowDamage(damage);
                foreach (var step in HitMotion(new List<Actor> { ActorOf(target) })) yield return step;
            }
            else if (action == "special")
            {
                // ひっさつ: QTEポイントを使って、マウスで星をえがく → えがけたら敵全体にダメージ
                _player.UseQtePoints();
                foreach (var step in RunStarDrawing()) yield return step;
                bool success = _subResult;
                if (success)
                {
                    actor.Flash(Settings.GLOW);
                    actor.Play("ひっさつ");
                    yield return Math.Max(HIT_DELAY, actor.MotionTime("ひっさつ"));  // ひっさつの動きが終わったら当たる
                    var hitActors = new List<Actor>();
                    foreach (var enemy in _enemies)
                    {
                        if (enemy.IsAlive)
                        {
                            DealDamage(enemy, GameData.SPECIAL_DAMAGE);
                            hitActors.Add(ActorOf(enemy));
                        }
                    }
                    foreach (var step in HitMotion(hitActors)) yield return step;
                }
            }
            else if (action == "jufu")
            {
                // じゅふ: 呪符を1枚使って、パラメータを上げる(このバトルのあいだ)
                var card = talisman < GameData.TALISMANS.Count ? GameData.TALISMANS[talisman] : null;
                _usedTalismans.Add(talisman);
                if (card != null)
                {
                    _player.RaiseStat(card.Stat, card.Amount);
                    _notice = $"{card.StatName} +{card.Amount}";
                    _noticeTimer = NOTICE_TIME;
                }
                actor.Flash(Settings.GLOW);
                actor.Play("じゅふ");
                yield return Math.Max(AFTER_HIT, actor.MotionTime("じゅふ"));
            }
        }

        /// <summary>攻撃する側: 踏み込み +「攻撃」の動き。動きの最後のコマまで行ったら当たる</summary>
        IEnumerable<double?> AttackMotion(Actor actor)
        {
            actor.Lunge();
            actor.Play("攻撃");
            yield return Math.Max(HIT_DELAY, actor.MotionTime("攻撃"));
        }

        /// <summary>攻撃を受けた側: 赤く点滅 +「ダメージ」の動き。動きが終わるまで待つ</summary>
        IEnumerable<double?> HitMotion(List<Actor> actors)
        {
            foreach (var target in actors)
            {
                target.Flash();
                target.Play("ダメージ");
            }
            double wait = AFTER_HIT;
            foreach (var target in actors)
            {
                wait = Math.Max(wait, target.MotionTime("ダメージ"));
            }
            yield return wait;
        }

        /// <summary>攻撃QTE(A→S→D→F)を出して、結果(成功 true / 失敗 false)が出るまで待つ。結果は _subResult</summary>
        IEnumerable<double?> RunAttackQte()
        {
            _qte = new AttackQTE();
            while (_qte.Result == null)
            {
                yield return null;  // 次のフレームまで待つ(入力は HandleEvent、時間は Update で進む)
            }
            bool success = _qte.Result.Value;
            _qte = null;
            if (success)
            {
                _player.AddQtePoint();  // 攻撃QTE に成功すると QTEポイントが1増える(5つで ひっさつ)
            }
            _qteFlash = success;
            _qteFlashTimer = QTE_RESULT_TIME;
            _subResult = success;
            yield return 0.3;  // 結果を少し見せてから攻撃
            _subResult = success;
        }

        /// <summary>星をえがくミニゲームを出して、結果(成功 true / 失敗 false)が出るまで待つ。結果は _subResult</summary>
        IEnumerable<double?> RunStarDrawing()
        {
            _star = new StarDrawing(STAR_CENTER, STAR_RADIUS, GameData.SPECIAL_TIME);
            var start = _star.StartPoint;  // マウスを 1 の点に置いておく
            WarpMouse(start);
            while (_star.Result == null)
            {
                yield return null;  // 次のフレームまで待つ
            }
            bool success = _star.Result.Value;
            yield return 0.25;  // 完成した星を少し見せる
            _star = null;
            _qteFlash = success;
            _qteFlashTimer = QTE_RESULT_TIME;
            yield return 0.3;
            _subResult = success;
        }

        /// <summary>マウスを pos(内部解像度のドット座標)に移す</summary>
        void WarpMouse((int x, int y) pos)
        {
            try
            {
                Platform.Current?.WarpMouse(pos.x, pos.y);
            }
            catch (Exception)
            {
                // マウスを動かせない環境では何もしない
            }
        }

        /// <summary>決着したか調べる。ついたら結果画面へ</summary>
        bool CheckFinish()
        {
            if (!_player.IsAlive)
            {
                Finish("lose");
                return true;
            }
            if (AliveIndexes().Count == 0)
            {
                Finish("win");
                return true;
            }
            return false;
        }

        void Finish(string result)
        {
            _result = result;
            _resultTimer = 0.0;
            _state = "result";
        }

        // ---- 更新・描画 ----
        public override void Update(double dt)
        {
            RefreshCommands();

            // 戦闘を進める(待ち時間が終わったら次のステップへ)
            _qte?.Update(dt);     // QTE の残り時間を減らす
            _rhythm?.Update(dt);  // 防御QTE の時間を進める
            _star?.Update(dt);
            _qteFlashTimer = Math.Max(0.0, _qteFlashTimer - dt);
            _noticeTimer = Math.Max(0.0, _noticeTimer - dt);
            if (_state == "action")
            {
                _wait -= dt;
                while (_state == "action" && _wait <= 0)
                {
                    if (_round == null || !_round.MoveNext())
                    {
                        break;
                    }
                    double? step = _round.Current;
                    if (step == null)  // 「次のフレームまで待つ」
                    {
                        _wait = 0.0;
                        break;
                    }
                    _wait += step.Value;
                }
            }
            if (_state == "result")
            {
                _resultTimer += dt;
            }

            for (int i = 0; i < _enemyActors.Count; i++)
            {
                var actor = _enemyActors[i];
                actor.Targeted = (_state == "target" && i == _targetIndex) || _enemies[i] == _qteTarget;
                actor.Update(dt);
            }
            _playerActor.Update(dt);
            foreach (var bar in _hpBars)
            {
                bar.Update(dt);
            }
            _emblem.Update(dt, _player);
            _commandMenu.Update(dt);
            _fightMenu.Update(dt);
            _talismanMenu?.Update(dt);
        }

        public override void Draw(Surface surface)
        {
            // 背景: 画像があればそれ、なければ画面全体が地面(こい灰色)
            if (_background != null)
            {
                surface.Blit(_background, 0, 0);
            }
            else
            {
                surface.Fill(Settings.GROUND);
            }

            for (int i = 0; i < Math.Min(_hpBars.Count, _enemyActors.Count); i++)
            {
                if (_enemyActors[i].Visible)
                {
                    _hpBars[i].Draw(surface);
                }
            }
            foreach (var actor in _enemyActors)
            {
                actor.Draw(surface);
            }
            _playerActor.Draw(surface);
            foreach (var actor in _enemyActors.Concat(new[] { _playerActor }))
            {
                actor.DrawDamagePopups(surface);  // ダメージの数字はキャラより上に描く
            }
            _emblem.Draw(surface, _player);

            Window.DrawWindow(surface, COMMAND_RECT);
            _commandMenu.Draw(surface, active: _state == "command");
            if (_state == "fight" || _state == "target" || _state == "jufu")
            {
                Window.DrawWindow(surface, FIGHT_RECT);
                _fightMenu.Draw(surface, active: _state == "fight");
            }
            if (_state == "jufu")
            {
                Window.DrawWindow(surface, _talismanRect);
                _talismanMenu.Draw(surface);
            }

            if (_star != null)
            {
                _shade.SetAlpha(170);
                surface.Blit(_shade, 0, 0);
                _star.Draw(surface);
            }
            else if (_qte != null)
            {
                QteView.DrawQte(surface, _qte, QTE_CENTER);
            }
            else if (_rhythm != null)
            {
                RhythmView.DrawRhythm(surface, _rhythm, LANE_XS, RHYTHM_TOP, RHYTHM_JUDGE_Y);
            }
            else if (_qteFlashTimer > 0)
            {
                QteView.DrawQteResult(surface, _qteFlash, QTE_CENTER);
            }
            else if (_noticeTimer > 0)
            {
                Utils.DrawText(surface, _notice, Settings.FONT_LARGE, Settings.GLOW, Anchor.Center, QTE_CENTER);
            }

            if (_state == "result")
            {
                DrawResult(surface);
            }
        }

        void DrawResult(Surface surface)
        {
            double progress = Math.Min(1.0, Math.Max(0.0, (_resultTimer - RESULT_DELAY * 0.5) / (RESULT_DELAY * 0.5)));
            if (progress <= 0)
            {
                return;
            }
            _shade.SetAlpha((int)(190 * progress));
            surface.Blit(_shade, 0, 0);
            if (progress < 1)
            {
                return;
            }
            string text = _result == "win" ? "しょうり！" : "まけてしまった…";
            var color = _result == "win" ? Settings.GLOW : Settings.WHITE;
            Utils.DrawText(surface, text, Settings.FONT_LARGE, color, Anchor.Center, W / 2, H / 2 - 16);
            Utils.DrawText(surface, "Enter / クリックでタイトルへ", Settings.FONT_SMALL, Settings.LIGHT_GRAY, Anchor.Center, W / 2, H / 2 + 20);
        }
    }
}
