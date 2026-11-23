// ドットフォント(DotGothic16)で文字を画像にする
//
// Pygame 版は font.render(text, False, color) (アンチエイリアスなし)で文字を描いていた。
// Unity 版は、その結果を1文字ずつ写しとったデータ(assets/fonts/DotGothic16-bitmap.bytes)を使って
// 同じドットの文字を作る。データは Tools/bake_font.py で作る(16 / 32 / 48 の3サイズ、フォントの全文字)。
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace QteRpg
{
    public sealed class BitmapFont
    {
        const int MissingGlyph = -1;  // フォントにない文字のかわりの絵

        sealed class Glyph
        {
            public int Advance;  // 次の文字までの幅
            public int Width;    // この文字だけを描いたときの画像の幅
            public int InkX, InkY, InkW, InkH;
            public byte[] Bits;

            public bool Ink(int x, int y)
            {
                int i = y * InkW + x;
                return (Bits[i >> 3] & (0x80 >> (i & 7))) != 0;
            }
        }

        sealed class SizeData
        {
            public int Size;
            public int Height;
            public Dictionary<int, Glyph> Glyphs = new Dictionary<int, Glyph>();
            public Glyph Missing;
        }

        readonly Dictionary<int, SizeData> _sizes = new Dictionary<int, SizeData>();
        readonly HashSet<int> _warnedSizes = new HashSet<int>();

        public static BitmapFont LoadFile(string path) => Load(File.ReadAllBytes(path));

        /// <summary>Tools/bake_font.py で作ったデータ(gzip)を読む</summary>
        public static BitmapFont Load(byte[] gzipped)
        {
            var font = new BitmapFont();
            using (var input = new GZipStream(new MemoryStream(gzipped), CompressionMode.Decompress))
            using (var reader = new BinaryReader(input))
            {
                var magic = reader.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != 'D' || magic[1] != 'G' || magic[2] != 'F' || magic[3] != '1')
                {
                    throw new InvalidDataException("フォントデータの形式がちがいます(Tools/bake_font.py で作りなおしてください)");
                }
                int sizeCount = reader.ReadInt32();
                for (int s = 0; s < sizeCount; s++)
                {
                    var data = new SizeData { Size = reader.ReadInt32(), Height = reader.ReadInt32() };
                    int glyphCount = reader.ReadInt32();
                    for (int g = 0; g < glyphCount; g++)
                    {
                        int codepoint = reader.ReadInt32();
                        var glyph = new Glyph
                        {
                            Advance = reader.ReadInt16(),
                            Width = reader.ReadInt16(),
                            InkX = reader.ReadByte(),
                            InkY = reader.ReadByte(),
                            InkW = reader.ReadByte(),
                            InkH = reader.ReadByte(),
                        };
                        glyph.Bits = reader.ReadBytes((glyph.InkW * glyph.InkH + 7) / 8);
                        if (codepoint == MissingGlyph)
                        {
                            data.Missing = glyph;
                        }
                        else
                        {
                            data.Glyphs[codepoint] = glyph;
                        }
                    }
                    font._sizes[data.Size] = data;
                }
            }
            return font;
        }

        /// <summary>そのサイズの行の高さ(font.get_height())</summary>
        public int GetHeight(int size)
        {
            var (data, scale) = Resolve(size);
            return data.Height * scale;
        }

        /// <summary>font.render(text, False, color): 文字を画像にする(点があるところだけ color、ほかは透明)</summary>
        public Surface Render(string text, int size, Rgba color)
        {
            var (data, scale) = Resolve(size);
            var glyphs = new List<(Glyph glyph, int pen)>();
            int pen = 0, width = 0;
            foreach (int codepoint in Codepoints(text))
            {
                if (!data.Glyphs.TryGetValue(codepoint, out var glyph))
                {
                    glyph = data.Missing;
                }
                if (glyph == null)
                {
                    continue;
                }
                glyphs.Add((glyph, pen));
                width = Math.Max(width, pen + glyph.Width);
                pen += glyph.Advance;
            }
            width = Math.Max(width, pen);
            var surface = new Surface(width * scale, data.Height * scale, SurfaceKind.ColorKey);
            var ink = new Rgba(color.R, color.G, color.B, 255);
            foreach (var (glyph, x0) in glyphs)
            {
                for (int y = 0; y < glyph.InkH; y++)
                {
                    for (int x = 0; x < glyph.InkW; x++)
                    {
                        if (!glyph.Ink(x, y))
                        {
                            continue;
                        }
                        int px = (x0 + glyph.InkX + x) * scale, py = (glyph.InkY + y) * scale;
                        for (int sy = 0; sy < scale; sy++)
                        {
                            for (int sx = 0; sx < scale; sx++)
                            {
                                if (px + sx < surface.Width && py + sy < surface.Height)
                                {
                                    surface.Pixels[(py + sy) * surface.Width + px + sx] = ink;
                                }
                            }
                        }
                    }
                }
            }
            return surface;
        }

        /// <summary>
        /// 作ってあるサイズのデータを使う。ないサイズは、16 の倍数なら 16 を整数倍に拡大、
        /// それ以外はいちばん近いサイズで代用する(ちがうサイズを使いたいときは Tools/bake_font.py の SIZES に足す)
        /// </summary>
        (SizeData data, int scale) Resolve(int size)
        {
            if (_sizes.TryGetValue(size, out var exact))
            {
                return (exact, 1);
            }
            if (_warnedSizes.Add(size))
            {
                Log.Warning($"[フォント] サイズ {size} のデータがないので代わりのサイズで描きます(Tools/bake_font.py の SIZES に足すと正しく描けます)");
            }
            if (size % 16 == 0 && _sizes.TryGetValue(16, out var baseData))
            {
                return (baseData, size / 16);
            }
            SizeData best = null;
            foreach (var data in _sizes.Values)
            {
                if (best == null || Math.Abs(data.Size - size) < Math.Abs(best.Size - size))
                {
                    best = data;
                }
            }
            if (best == null)
            {
                throw new InvalidDataException("フォントデータに文字が入っていません");
            }
            return (best, 1);
        }

        static IEnumerable<int> Codepoints(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    yield return char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                else
                {
                    yield return text[i];
                }
            }
        }
    }
}
