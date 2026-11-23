// バトル画面でのキャラの見た目とアニメーション (Pygame 版の ui/actor.py)
//
//   画像(assets/images)があれば画像で、なければ白い四角(ホワイトボックス)で表示する
//   Play()       … 動きを再生する("攻撃" "ダメージ" "ひっさつ" "じゅふ" は1回だけ再生して「待機」にもどる)
//   Lunge()      … 攻撃するときに少し踏み込んで戻る
//   Flash()      … ダメージを受けたときなどに色を点滅させる
//   ShowDamage() … 受けたダメージの数字を、キャラの右上に出す(少し浮かんで消える)
//   倒れたら(HPが0)点滅とダメージの動きのあとに、すうっと消える
using System;
using System.Collections.Generic;
using System.Linq;

namespace QteRpg.UI
{
    public sealed class Actor
    {
        public const double LUNGE_TIME = 0.3;   // 踏み込んで戻るまでの時間(秒)
        const int LUNGE_DIST = 10;               // 踏み込む距離(ドット)
        const double FLASH_TIME = 0.4;           // 点滅する時間(秒)
        const double FADE_SPEED = 2.5;           // 倒れたときに消える速さ(1秒でどれだけ薄くなるか)
        const double DAMAGE_POPUP_TIME = 0.9;    // ダメージの数字を出しておく時間(秒)
        const double DAMAGE_POPUP_FADE = 0.3;    // そのうち、最後に薄くなって消えていく時間(秒)
        const int DAMAGE_POPUP_RISE = 6;         // 出ているあいだに上へ浮かぶ距離(ドット)
        const string IDLE = "待機";

        sealed class DamagePopup
        {
            public Surface Image;
            public double TimeLeft;
        }

        public readonly Character Unit;                              // パラメータを持つキャラ(Player / Enemy)
        readonly Dictionary<string, List<Surface>> _animations;      // {"待機": [画像, …], "攻撃": [...], ...}
        readonly (int x, int y) _footPos;
        public PgRect Rect;
        readonly string _labelSide;                                  // 名前を出す位置: "below"(下) か "right"(右)。null なら出さない
        readonly (int x, int y) _lungeDir;                           // 攻撃するときに踏み込む向き (x, y)
        readonly Surface _label;

        string _motion = IDLE;  // 今の動き
        int _frame;             // 今のコマ
        double _frameTimer;
        double _lungeTimer;
        double _flashTimer;
        Rgba _flashColor = Settings.DAMAGE_RED;
        public double Alpha = 1.0;      // 1 = 見えている、0 = 消えた
        public bool Targeted;           // ターゲットとして選ばれているか
        public bool Attacking;          // これからこうげきしてくるか(敵)
        double _timer;
        readonly Dictionary<(Surface, Rgba), Surface> _tinted = new Dictionary<(Surface, Rgba), Surface>();  // 点滅用に色をつけた画像
        List<DamagePopup> _damagePopups = new List<DamagePopup>();  // 受けたダメージの数字

        public Actor(Character unit, (int w, int h) size, (int x, int y) footPos, string labelSide = "below",
                     (int x, int y)? lungeDir = null, Dictionary<string, List<Surface>> animations = null)
        {
            Unit = unit;
            _animations = animations ?? new Dictionary<string, List<Surface>>();
            if (_animations.TryGetValue(IDLE, out var idle) && idle.Count > 0)
            {
                size = (idle[0].Width, idle[0].Height);  // 待機の1枚目の大きさを、このキャラの大きさにする
            }
            _footPos = footPos;
            Rect = PgRect.Anchored(size.w, size.h, Anchor.MidBottom, footPos.x, footPos.y);  // 足元の位置で置く
            _labelSide = labelSide;
            _lungeDir = lungeDir ?? (0, -1);
            _label = labelSide != null ? MakeLabel(unit.Name) : null;
        }

        // ---- アニメーション開始 ----
        /// <summary>動きを再生する。その動きの画像がなければ何もしない(待機のまま)</summary>
        public void Play(string motion)
        {
            if (_animations.ContainsKey(motion))
            {
                _motion = motion;
                _frame = 0;
                _frameTimer = 0.0;
            }
        }

        /// <summary>その動きを1回再生するのにかかる秒数(画像がなければ 0)</summary>
        public double MotionTime(string motion)
        {
            return _animations.TryGetValue(motion, out var frames) && frames.Count > 0 ? frames.Count * FrameTime(motion) : 0.0;
        }

        public void Lunge() => _lungeTimer = LUNGE_TIME;

        public void Flash(Rgba? color = null, double duration = FLASH_TIME)
        {
            _flashColor = color ?? Settings.DAMAGE_RED;
            _flashTimer = duration;
        }

        /// <summary>受けたダメージの数字を右上に出す。続けて受けたときは、前の数字の少し上に出す</summary>
        public void ShowDamage(int amount, Rgba? color = null)
        {
            string text = amount.ToString();
            var image = Utils.RenderText(text, Settings.FONT_SMALL, color ?? Settings.WHITE);
            var outline = Utils.RenderText(text, Settings.FONT_SMALL, Settings.BLACK);
            // 黒いふちどりをつけて、背景が明るくても読めるようにする
            var popup = new Surface(image.Width + 2, image.Height + 2, SurfaceKind.Alpha);
            foreach (var (ox, oy) in new[] { (0, 1), (2, 1), (1, 0), (1, 2) })
            {
                popup.Blit(outline, ox, oy);
            }
            popup.Blit(image, 1, 1);
            _damagePopups.Add(new DamagePopup { Image = popup, TimeLeft = DAMAGE_POPUP_TIME });
        }

        /// <summary>名前の札: コマンド欄と同じ 黒い背景 + 白いわく + 白い文字</summary>
        static Surface MakeLabel(string name)
        {
            var text = Utils.RenderText(name, Settings.FONT_SMALL, Settings.WHITE);
            var image = new Surface(text.Width + 8, text.Height + 2, SurfaceKind.Alpha);
            Window.DrawWindow(image, image.GetRect());
            var rect = image.GetRect();
            image.Blit(text, text.GetRect(Anchor.Center, rect.CenterX, rect.CenterY));
            return image;
        }

        public bool Visible => Alpha > 0;

        static double FrameTime(string motion) => GameData.FRAME_TIMES.GetOr(motion, 0.1);

        /// <summary>踏み込み中のずれ(行って戻る)</summary>
        (int dx, int dy) Offset()
        {
            if (_lungeTimer <= 0)
            {
                return (0, 0);
            }
            double progress = 1 - _lungeTimer / LUNGE_TIME;  // 0 → 1
            double dist = Math.Sin(Math.PI * progress) * LUNGE_DIST;
            return (Py.Round(_lungeDir.x * dist), Py.Round(_lungeDir.y * dist));
        }

        Surface CurrentImage()
        {
            if (!_animations.TryGetValue(_motion, out var frames) || frames.Count == 0)
            {
                return null;
            }
            return frames[Math.Min(_frame, frames.Count - 1)];
        }

        // ---- 更新・描画 ----
        public void Update(double dt)
        {
            _timer += dt;
            _lungeTimer = Math.Max(0.0, _lungeTimer - dt);
            _flashTimer = Math.Max(0.0, _flashTimer - dt);
            foreach (var popup in _damagePopups)
            {
                popup.TimeLeft -= dt;
            }
            _damagePopups = _damagePopups.Where(p => p.TimeLeft > 0).ToList();
            AdvanceFrames(dt);
            // 倒れたら、点滅とダメージの動きが終わってから消えていく
            if (!Unit.IsAlive && _flashTimer <= 0 && _motion == IDLE)
            {
                Alpha = Math.Max(0.0, Alpha - FADE_SPEED * dt);
            }
        }

        void AdvanceFrames(double dt)
        {
            if (!_animations.TryGetValue(_motion, out var frames) || frames.Count == 0)
            {
                return;
            }
            _frameTimer += dt;
            double frameTime = FrameTime(_motion);
            while (_frameTimer >= frameTime)
            {
                _frameTimer -= frameTime;
                _frame++;
                if (_frame >= frames.Count)
                {
                    _frame = 0;
                    if (_motion != IDLE)  // 1回再生の動きが終わった → 待機にもどる
                    {
                        _motion = IDLE;
                        _frameTimer = 0.0;
                        return;
                    }
                }
            }
        }

        Surface Tint(Surface image, Rgba color)
        {
            var key = (image, color);
            if (!_tinted.TryGetValue(key, out var tinted))
            {
                tinted = image.Copy();
                tinted.FillMultiply(new Rgba(color.R, color.G, color.B, 255));
                _tinted[key] = tinted;
            }
            return tinted;
        }

        /// <summary>ダメージの数字(キャラの右上)。倒れて消えたあとも、出ている数字は最後まで見せる</summary>
        public void DrawDamagePopups(Surface surface)
        {
            // 新しい数字ほど下(いつもの位置)に出し、前の数字は1行ずつ上へ押し上げる
            int count = _damagePopups.Count;
            for (int i = 0; i < count; i++)
            {
                var image = _damagePopups[i].Image;
                double timeLeft = _damagePopups[i].TimeLeft;
                double progress = 1 - timeLeft / DAMAGE_POPUP_TIME;  // 0 → 1
                int rise = Py.Round(DAMAGE_POPUP_RISE * Math.Min(1.0, progress * 3));  // はじめに すっと浮かぶ
                int stack = (count - 1 - i) * (image.Height - 4);
                var rect = image.GetRect(Anchor.BottomLeft, Rect.Right - 4, Rect.Top + 6 - rise - stack);
                if (timeLeft < DAMAGE_POPUP_FADE)
                {
                    image = image.Copy();
                    image.SetAlpha((int)(255 * timeLeft / DAMAGE_POPUP_FADE));
                }
                surface.Blit(image, rect);
            }
        }

        public void Draw(Surface surface)
        {
            if (!Visible)
            {
                return;
            }
            int alpha = (int)(255 * Alpha);
            var (dx, dy) = Offset();
            bool flashing = _flashTimer > 0 && (int)(_flashTimer / 0.06) % 2 == 0;

            // 足元の影(消えかけのときは描かない)
            if (Alpha >= 1)
            {
                var shadow = PgRect.Anchored(Rect.Width + 10, 8, Anchor.Center, Rect.CenterX, Rect.Bottom);
                QteRpg.Draw.Ellipse(surface, Settings.SHADOW, shadow);
            }

            var image = CurrentImage();
            PgRect body;
            if (image != null)
            {
                // 画像: 足元を立ち位置に合わせる。点滅中は色をつけた画像と交互に
                body = image.GetRect(Anchor.MidBottom, _footPos.x + dx, _footPos.y + dy);
                if (flashing)
                {
                    image = Tint(image, _flashColor);
                }
                if (Alpha < 1)
                {
                    image = image.Copy();
                    image.SetAlpha(alpha);
                }
                surface.Blit(image, body);
            }
            else
            {
                // 画像なし: 白い四角。点滅中は flashColor と白を交互に
                body = Rect.Move(dx, dy);
                var color = flashing ? _flashColor : Settings.WHITE;
                if (Alpha < 1)
                {
                    var box = new Surface(body.Width, body.Height);
                    box.Fill(color);
                    box.SetAlpha(alpha);
                    surface.Blit(box, body);
                }
                else
                {
                    QteRpg.Draw.Rect(surface, color, body);
                }
            }

            // こうげきしてくるとき: 赤いわく
            if (Attacking)
            {
                QteRpg.Draw.Rect(surface, Settings.DAMAGE_RED, body.Inflate(6, 6), 1);
            }

            // ターゲットに選ばれているとき: 金色のわくと ▼
            if (Targeted)
            {
                QteRpg.Draw.Rect(surface, Settings.GLOW, body.Inflate(6, 6), 1);
                int bob = (int)(_timer * 3) % 2;
                int x = body.CenterX, y = body.Top - 9 + bob;
                QteRpg.Draw.Polygon(surface, Settings.GLOW, new[] { (x - 4, y), (x + 4, y), (x, y + 4) });
            }

            // 名前
            var label = _label;
            if (label == null)
            {
                return;
            }
            if (Alpha < 1)
            {
                label = label.Copy();
                label.SetAlpha(alpha);
            }
            if (_labelSide == "right")
            {
                surface.Blit(label, label.GetRect(Anchor.MidLeft, Rect.Right + 6, Rect.CenterY));
            }
            else
            {
                surface.Blit(label, label.GetRect(Anchor.MidTop, Rect.CenterX, Rect.Bottom + 2));
            }
        }
    }
}
