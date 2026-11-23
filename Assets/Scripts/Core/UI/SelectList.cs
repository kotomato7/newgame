// 縦に並んだ選択肢。キーボード(↑↓ / W S)とマウスで選んで決定する (Pygame 版の ui/select_list.py)
//
//   SetEnabled(番号, false) … その項目をグレーにして、決定しても反応しないようにする
//   SetGlowing(番号, true)  … その項目の文字を光らせる
using System;
using System.Collections.Generic;
using System.Linq;

namespace QteRpg.UI
{
    public sealed class SelectList
    {
        public static readonly GameKey[] UP_KEYS = { GameKey.Up, GameKey.W };
        public static readonly GameKey[] DOWN_KEYS = { GameKey.Down, GameKey.S };
        public static readonly GameKey[] CONFIRM_KEYS = { GameKey.Return, GameKey.KeypadEnter, GameKey.Space, GameKey.Z };
        public static readonly GameKey[] CANCEL_KEYS = { GameKey.Escape, GameKey.X, GameKey.Backspace };

        const int CURSOR_SPACE = 12;  // 文字の左側にあけるカーソル用のすき間

        /// <summary>決定操作か (Enter / Space / Z / 左クリック)</summary>
        public static bool IsConfirm(GameEvent e)
        {
            if (e.Type == GameEventType.KeyDown)
            {
                return CONFIRM_KEYS.Contains(e.Key);
            }
            return e.Type == GameEventType.MouseButtonDown && e.Button == 1;
        }

        /// <summary>もどる操作か (ESC / X / BackSpace / 右クリック)</summary>
        public static bool IsCancel(GameEvent e)
        {
            if (e.Type == GameEventType.KeyDown)
            {
                return CANCEL_KEYS.Contains(e.Key);
            }
            return e.Type == GameEventType.MouseButtonDown && e.Button == 3;
        }

        public readonly Rgba Color;
        public readonly List<string> Labels;
        public int Selected;
        double _timer;
        double _glowTimer;                             // 光る文字の明滅用(止めずに進め続ける)
        readonly HashSet<int> _disabled = new HashSet<int>();  // 選べない(グレーの)項目の番号
        readonly HashSet<int> _glowing = new HashSet<int>();   // 光らせる項目の番号

        readonly List<Surface> _images;
        readonly List<Surface> _grayImages;
        readonly List<Surface> _glowImages;
        readonly List<PgRect> _textRects = new List<PgRect>();
        readonly List<PgRect> _hitRects = new List<PgRect>();  // マウスが反応する範囲(カーソル部分も含めて少し広め)

        /// <summary>
        /// 位置は centerX(全体を中央ぞろえ) か left(文字の左端) のどちらかで指定する
        /// color … 文字とカーソル(▶)の色、bold … true で太字
        /// </summary>
        public SelectList(IList<string> labels, int top, int? centerX = null, int? left = null, int gap = 22,
                          int size = Settings.FONT_SMALL, Rgba? color = null, bool bold = false)
        {
            Func<string, int, Rgba?, Surface> render = bold ? (Func<string, int, Rgba?, Surface>)Utils.RenderBoldText : Utils.RenderText;
            Color = color ?? Settings.WHITE;
            Labels = labels.ToList();
            Selected = 0;

            _images = Labels.Select(label => render(label, size, Color)).ToList();
            _grayImages = Labels.Select(label => render(label, size, Settings.GRAY)).ToList();
            _glowImages = Labels.Select(label => render(label, size, Settings.GLOW)).ToList();
            // 一番長い文字に合わせて左端をそろえる
            int maxW = _images.Max(img => img.Width);
            int leftX = left ?? (centerX.Value - maxW / 2);

            for (int i = 0; i < _images.Count; i++)
            {
                var rect = _images[i].GetRect(Anchor.TopLeft, leftX, top + i * gap);
                _textRects.Add(rect);
                // 高さは行の間隔(gap)ぶん。となりの項目と重ならないようにする
                _hitRects.Add(new PgRect(leftX - CURSOR_SPACE, rect.CenterY - gap / 2, maxW + CURSOR_SPACE + 4, gap));
            }
        }

        public void Reset()
        {
            Selected = 0;
            _timer = 0.0;
        }

        public void SetEnabled(int index, bool enabled)
        {
            if (enabled)
            {
                _disabled.Remove(index);
            }
            else
            {
                _disabled.Add(index);
            }
        }

        public void SetGlowing(int index, bool glowing)
        {
            if (glowing)
            {
                _glowing.Add(index);
            }
            else
            {
                _glowing.Remove(index);
            }
        }

        /// <summary>決定。グレーの項目なら反応しない(null)</summary>
        int? Confirm(int index) => _disabled.Contains(index) ? (int?)null : index;

        int? IndexAt((int x, int y) pos)
        {
            for (int i = 0; i < _hitRects.Count; i++)
            {
                if (_hitRects[i].CollidePoint(pos.x, pos.y))
                {
                    return i;
                }
            }
            return null;
        }

        void Move(int step)
        {
            Selected = Py.Mod(Selected + step, Labels.Count);
            _timer = 0.0;
        }

        /// <summary>決定されたら選んだ番号を返す。それ以外は null</summary>
        public int? HandleEvent(GameEvent e)
        {
            if (e.Type == GameEventType.KeyDown)
            {
                if (UP_KEYS.Contains(e.Key))
                {
                    Move(-1);
                }
                else if (DOWN_KEYS.Contains(e.Key))
                {
                    Move(1);
                }
                else if (CONFIRM_KEYS.Contains(e.Key))
                {
                    return Confirm(Selected);
                }
            }
            else if (e.Type == GameEventType.MouseMotion)
            {
                var index = IndexAt(e.Pos);
                if (index != null && index.Value != Selected)
                {
                    Selected = index.Value;
                    _timer = 0.0;
                }
            }
            else if (e.Type == GameEventType.MouseButtonDown && e.Button == 1)
            {
                var index = IndexAt(e.Pos);
                if (index != null)
                {
                    Selected = index.Value;
                    return Confirm(index.Value);
                }
            }
            return null;
        }

        public void Update(double dt)
        {
            _timer += dt;
            _glowTimer += dt;
        }

        /// <summary>active=false のときはカーソルを灰色で止めて表示(サブメニューを開いている間など)</summary>
        public void Draw(Surface surface, bool active = true)
        {
            for (int i = 0; i < _textRects.Count; i++)
            {
                var rect = _textRects[i];
                if (_disabled.Contains(i))
                {
                    surface.Blit(_grayImages[i], rect);
                }
                else if (_glowing.Contains(i))
                {
                    DrawGlowing(surface, i, rect);
                }
                else
                {
                    surface.Blit(_images[i], rect);
                }
            }

            // 選択中の項目の左に ▶ カーソル(操作中は1ドットだけ左右にゆれる)
            var selectedRect = _textRects[Selected];
            int wiggle = active ? (int)(_timer * 3) % 2 : 0;
            var color = active ? Color : Settings.GRAY;
            int x = selectedRect.Left - CURSOR_SPACE + wiggle;
            int y = selectedRect.CenterY;
            QteRpg.Draw.Polygon(surface, color, new[] { (x, y - 3), (x, y + 3), (x + 3, y) });
        }

        /// <summary>文字のまわりに半透明の光をにじませてから、金色の文字を描く</summary>
        void DrawGlowing(Surface surface, int i, PgRect rect)
        {
            double pulse = 0.5 + 0.5 * Math.Sin(_glowTimer * 4);  // 0〜1 でゆっくり明滅
            var halo = _glowImages[i].Copy();
            halo.SetAlpha((int)(60 + 80 * pulse));
            foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                surface.Blit(halo, rect.Move(dx, dy));
            }
            surface.Blit(_glowImages[i], rect);
        }
    }
}
