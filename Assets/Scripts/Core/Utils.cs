// あちこちで使う小さな便利関数 (Pygame 版の utils.py)
using System;

namespace QteRpg
{
    public static class Utils
    {
        static BitmapFont _font;

        /// <summary>ドットフォントを読み込む(1回だけ読んで使い回す)</summary>
        public static BitmapFont GetFont()
        {
            if (_font == null)
            {
                string path = Settings.FONT_PATH;
                try
                {
                    _font = BitmapFont.LoadFile(path);
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException(
                        $"フォントのデータ {path} が読めません。Tools/bake_font.py で作ってください: {error.Message}", error);
                }
            }
            return _font;
        }

        /// <summary>文字を画像(Surface)にする。アンチエイリアスなしでドットをくっきり保つ</summary>
        public static Surface RenderText(string text, int size, Rgba? color = null)
        {
            return GetFont().Render(text, size, color ?? Settings.WHITE);
        }

        /// <summary>太字の文字を画像にする: 同じ文字を1ドット右にずらして重ねる(線の太さが1ドット増える)</summary>
        public static Surface RenderBoldText(string text, int size, Rgba? color = null)
        {
            var image = RenderText(text, size, color);
            var bold = new Surface(image.Width + 1, image.Height, SurfaceKind.Alpha);
            bold.Blit(image, 0, 0);
            bold.Blit(image, 1, 0);
            return bold;
        }

        /// <summary>
        /// ふちどり + たてのグラデーションの文字を画像にする
        ///   中身: 上が topColor、下が bottomColor になるよう1行ずつ色を変える
        ///   ふち: outline ドットの太さで outlineColor で囲む
        /// </summary>
        public static Surface RenderTitleText(string text, int size, Rgba topColor, Rgba bottomColor,
                                              Rgba? outlineColor = null, int outline = 2)
        {
            var mask = RenderText(text, size, Settings.WHITE).ConvertAlpha();  // すき間を透明にしてからかけ合わせる
            int w = mask.Width, h = mask.Height;
            // グラデーションの板を作り、文字の形(白)とかけ合わせて、文字の部分だけ残す
            var fill = new Surface(w, h, SurfaceKind.Alpha);
            for (int y = 0; y < h; y++)
            {
                double t = (double)y / Math.Max(1, h - 1);
                var color = new Rgba(
                    Py.Round(topColor.R + (bottomColor.R - topColor.R) * t),
                    Py.Round(topColor.G + (bottomColor.G - topColor.G) * t),
                    Py.Round(topColor.B + (bottomColor.B - topColor.B) * t),
                    255);
                Draw.Line(fill, color, (0, y), (w - 1, y));
            }
            fill.BlitMultiply(mask, 0, 0);

            var edge = RenderText(text, size, outlineColor ?? Settings.WHITE);
            var image = new Surface(w + outline * 2, h + outline * 2, SurfaceKind.Alpha);
            for (int dx = -outline; dx <= outline; dx++)
            {
                for (int dy = -outline; dy <= outline; dy++)
                {
                    if ((dx != 0 || dy != 0) && dx * dx + dy * dy <= outline * outline + 1)  // 角を少し丸める
                    {
                        image.Blit(edge, outline + dx, outline + dy);
                    }
                }
            }
            image.Blit(fill, outline, outline);
            return image;
        }

        /// <summary>文字を描く。位置は Anchor.Center や Anchor.MidTop と (x, y) で指定する</summary>
        public static PgRect DrawText(Surface surface, string text, int size, Rgba color, Anchor anchor, int x, int y)
        {
            var image = RenderText(text, size, color);
            var rect = image.GetRect(anchor, x, y);
            surface.Blit(image, rect);
            return rect;
        }

        public static PgRect DrawText(Surface surface, string text, int size, Rgba color, Anchor anchor, (int x, int y) pos)
        {
            return DrawText(surface, text, size, color, anchor, pos.x, pos.y);
        }
    }
}
