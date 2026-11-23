// pygame.Surface のかわり: ドットの絵(1ドット = Rgba 1つ)
//
// 画面(480×270)も、文字の画像も、キャラの画像も、すべて Surface。
// 重ねて描く(blit)ときの色の混ぜかたは pygame と同じ計算にしてある。
//
// 種類 (Kind):
//   Opaque   … 透明なところがない絵(pygame.Surface((w, h)) で作った板、画面そのもの、背景画像)
//   ColorKey … 文字の画像(pygame の font.render(text, False, color))。点があるところだけ描く
//   Alpha    … 1ドットごとに透明度がある絵(pygame.SRCALPHA、convert_alpha() した画像)
using System;

namespace QteRpg
{
    public enum SurfaceKind
    {
        Opaque,
        ColorKey,
        Alpha,
    }

    public sealed class Surface
    {
        public readonly int Width;
        public readonly int Height;
        /// <summary>左上から1行ずつ(上→下)に並んだドット</summary>
        public readonly Rgba[] Pixels;
        public SurfaceKind Kind;
        /// <summary>surface.set_alpha(): 全体の不透明度(255 = そのまま)</summary>
        public int AlphaMod = 255;

        public Surface(int width, int height, SurfaceKind kind = SurfaceKind.Opaque)
        {
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
            Kind = kind;
            Pixels = new Rgba[Width * Height];
            if (kind == SurfaceKind.Opaque)
            {
                var black = new Rgba(0, 0, 0, 255);
                for (int i = 0; i < Pixels.Length; i++)
                {
                    Pixels[i] = black;
                }
            }
        }

        public (int w, int h) Size => (Width, Height);

        /// <summary>surface.get_rect(): 位置 (0, 0) の、この絵と同じ大きさの四角形</summary>
        public PgRect GetRect() => new PgRect(0, 0, Width, Height);

        /// <summary>surface.get_rect(center=(x, y)) など</summary>
        public PgRect GetRect(Anchor anchor, int x, int y) => PgRect.Anchored(Width, Height, anchor, x, y);

        public bool HasAlphaChannel => Kind != SurfaceKind.Opaque;

        /// <summary>surface.copy()</summary>
        public Surface Copy()
        {
            var copy = new Surface(Width, Height, Kind) { AlphaMod = AlphaMod };
            Array.Copy(Pixels, copy.Pixels, Pixels.Length);
            return copy;
        }

        /// <summary>surface.set_alpha(alpha)</summary>
        public void SetAlpha(int alpha) => AlphaMod = Py.Clamp(alpha, 0, 255);

        /// <summary>surface.convert_alpha(): 1ドットごとの透明度を持つ絵にする</summary>
        public Surface ConvertAlpha()
        {
            var copy = Copy();
            copy.Kind = SurfaceKind.Alpha;
            copy.AlphaMod = 255;
            return copy;
        }

        /// <summary>surface.convert(): 透明度をすてる(背景画像用)</summary>
        public Surface ConvertOpaque()
        {
            var copy = Copy();
            copy.Kind = SurfaceKind.Opaque;
            copy.AlphaMod = 255;
            for (int i = 0; i < copy.Pixels.Length; i++)
            {
                copy.Pixels[i].A = 255;
            }
            return copy;
        }

        /// <summary>1ドットに色を置く(混ぜない。pygame の set_at と同じ)</summary>
        public void SetAt(int x, int y, Rgba color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return;
            }
            Pixels[y * Width + x] = Store(color);
        }

        public Rgba GetAt(int x, int y) => Pixels[y * Width + x];

        /// <summary>この絵に置くときの色(透明度のない絵なら A は 255)</summary>
        public Rgba Store(Rgba color)
        {
            if (Kind == SurfaceKind.Opaque)
            {
                color.A = 255;
            }
            return color;
        }

        /// <summary>surface.fill(color): 全体を1色でぬる</summary>
        public void Fill(Rgba color)
        {
            color = Store(color);
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = color;
            }
        }

        /// <summary>四角の範囲を1色でぬる(SDL_FillRect)。はみ出た部分は切る</summary>
        public void FillRect(PgRect rect, Rgba color)
        {
            int x0 = Math.Max(rect.X, 0), y0 = Math.Max(rect.Y, 0);
            int x1 = Math.Min(rect.X + rect.W, Width), y1 = Math.Min(rect.Y + rect.H, Height);
            color = Store(color);
            for (int y = y0; y < y1; y++)
            {
                int row = y * Width;
                for (int x = x0; x < x1; x++)
                {
                    Pixels[row + x] = color;
                }
            }
        }

        /// <summary>surface.fill(color, special_flags=BLEND_RGBA_MULT): 色をかけ合わせる(点滅の色つけ)</summary>
        public void FillMultiply(Rgba color)
        {
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = Multiply(color, Pixels[i]);
            }
        }

        static byte MulComp(int s, int d) => (byte)((d != 0 && s != 0) ? ((d * s) + 255) >> 8 : 0);

        static Rgba Multiply(Rgba s, Rgba d) =>
            new Rgba(MulComp(s.R, d.R), MulComp(s.G, d.G), MulComp(s.B, d.B), MulComp(s.A, d.A));

        /// <summary>ALPHA_BLEND_COMP (pygame の alphablit と同じ式)</summary>
        static int BlendComp(int s, int d, int a) => (((s - d) * a + s) >> 8) + d;

        /// <summary>surface.blit(src, (x, y)): src を (x, y) に重ねて描く</summary>
        public void Blit(Surface src, int x, int y) => Blit(src, x, y, false);

        /// <summary>surface.blit(src, rect): rect の左上に描く</summary>
        public void Blit(Surface src, PgRect rect) => Blit(src, rect.X, rect.Y, false);

        /// <summary>surface.blit(src, (x, y), special_flags=BLEND_RGBA_MULT)</summary>
        public void BlitMultiply(Surface src, int x, int y) => Blit(src, x, y, true);

        void Blit(Surface src, int x, int y, bool multiply)
        {
            int sx0 = Math.Max(0, -x), sy0 = Math.Max(0, -y);
            int sx1 = Math.Min(src.Width, Width - x), sy1 = Math.Min(src.Height, Height - y);
            if (sx0 >= sx1 || sy0 >= sy1)
            {
                return;
            }
            bool dstAlpha = HasAlphaChannel;
            int mod = src.AlphaMod;
            for (int sy = sy0; sy < sy1; sy++)
            {
                int srow = sy * src.Width;
                int drow = (sy + y) * Width + x;
                for (int sx = sx0; sx < sx1; sx++)
                {
                    Rgba s = src.Pixels[srow + sx];
                    int di = drow + sx;
                    if (multiply)
                    {
                        Pixels[di] = Store(Multiply(s, Pixels[di]));
                        continue;
                    }
                    switch (src.Kind)
                    {
                        case SurfaceKind.Alpha:
                            BlendAlpha(di, s, mod, dstAlpha);
                            break;
                        case SurfaceKind.ColorKey:
                            if (s.A == 0)
                            {
                                break;
                            }
                            if (mod >= 255)
                            {
                                Pixels[di] = new Rgba(s.R, s.G, s.B, 255);
                            }
                            else
                            {
                                BlendColorKeyAlpha(di, s, mod, dstAlpha);
                            }
                            break;
                        default: // Opaque
                            if (mod >= 255)
                            {
                                Pixels[di] = new Rgba(s.R, s.G, s.B, 255);
                            }
                            else
                            {
                                BlendSurfaceAlpha(di, s, mod, dstAlpha);
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>1ドットごとの透明度で混ぜる(pygame の alphablit_alpha)</summary>
        void BlendAlpha(int di, Rgba s, int mod, bool dstAlpha)
        {
            int sA = s.A;
            if (mod != 255)
            {
                sA = sA * mod / 255;
            }
            Rgba d = Pixels[di];
            int dA = dstAlpha ? d.A : 255;
            if (dA != 0)
            {
                d.R = (byte)BlendComp(s.R, d.R, sA);
                d.G = (byte)BlendComp(s.G, d.G, sA);
                d.B = (byte)BlendComp(s.B, d.B, sA);
                d.A = dstAlpha ? (byte)(sA + dA - (sA * dA) / 255) : (byte)255;
            }
            else
            {
                d = new Rgba(s.R, s.G, s.B, sA);
            }
            Pixels[di] = d;
        }

        /// <summary>文字の画像を、全体の不透明度(set_alpha)で混ぜる(SDL の Blit1toNAlphaKey と同じ式)</summary>
        void BlendColorKeyAlpha(int di, Rgba s, int alpha, bool dstAlpha)
        {
            Rgba d = Pixels[di];
            d.R = (byte)((s.R - d.R) * alpha / 255 + d.R);
            d.G = (byte)((s.G - d.G) * alpha / 255 + d.G);
            d.B = (byte)((s.B - d.B) * alpha / 255 + d.B);
            d.A = dstAlpha ? (byte)(alpha + d.A - alpha * d.A / 255) : (byte)255;
            Pixels[di] = d;
        }

        /// <summary>絵全体の不透明度(set_alpha)で混ぜる(SDL の surface alpha のブレンド)</summary>
        void BlendSurfaceAlpha(int di, Rgba s, int alpha, bool dstAlpha)
        {
            Rgba d = Pixels[di];
            d.R = (byte)(d.R + (((s.R - d.R) * alpha) >> 8));
            d.G = (byte)(d.G + (((s.G - d.G) * alpha) >> 8));
            d.B = (byte)(d.B + (((s.B - d.B) * alpha) >> 8));
            d.A = dstAlpha ? (byte)(alpha + d.A - alpha * d.A / 255) : (byte)255;
            Pixels[di] = d;
        }

        /// <summary>pygame.transform.scale(surface, (w, h)): 補間しない拡大・縮小(ドットはぼやけない)</summary>
        public Surface Scale(int width, int height)
        {
            var scaled = new Surface(width, height, Kind) { AlphaMod = AlphaMod };
            if (Width == 0 || Height == 0 || width <= 0 || height <= 0)
            {
                return scaled;
            }
            if (width == Width * 2 && height == Height * 2)
            {
                // ちょうど2倍(pygame は scale2x の生のコピー)
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        scaled.Pixels[y * width + x] = Pixels[(y / 2) * Width + x / 2];
                    }
                }
                return scaled;
            }
            // pygame の stretch() と同じ、誤差をためながら進める方法
            int dstW2 = width << 1, dstH2 = height << 1, srcW2 = Width << 1, srcH2 = Height << 1;
            int hErr = srcH2 - dstH2;
            int srcRow = 0;
            for (int y = 0; y < height; y++)
            {
                int wErr = srcW2 - dstW2;
                int srcX = 0;
                for (int x = 0; x < width; x++)
                {
                    scaled.Pixels[y * width + x] = Pixels[Math.Min(srcRow, Height - 1) * Width + Math.Min(srcX, Width - 1)];
                    while (wErr >= 0)
                    {
                        srcX++;
                        wErr -= dstW2;
                    }
                    wErr += srcW2;
                }
                while (hErr >= 0)
                {
                    srcRow++;
                    hErr -= dstH2;
                }
                hErr += srcH2;
            }
            return scaled;
        }
    }
}
