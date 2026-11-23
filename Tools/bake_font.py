"""DotGothic16 の文字を、pygame とまったく同じドット(アンチエイリアスなし)のデータにして保存する

Unity 版はこのデータを使って文字を描く。Pygame 版の font.render(text, False, color) と同じ見た目になる。
  ・作るサイズ … 16 / 32 / 48 (Settings.cs の FONT_SMALL / FONT_LARGE / FONT_TITLE)
  ・入れる文字 … フォントに入っている文字ぜんぶ
  ・ほかのサイズの文字を使いたくなったら、SIZES に足してこのスクリプトを実行しなおす

使い方 (pygame と numpy が必要。Pygame_wAI の .venv でも動く):
    python Tools/bake_font.py
出力:
    Assets/StreamingAssets/assets/fonts/DotGothic16-bitmap.bytes

ファイルの中身 (gzip で圧縮、数値はリトルエンディアン):
    "DGF1"  int32 サイズの数
    サイズごとに: int32 サイズ, int32 行の高さ, int32 文字の数
      文字ごとに: int32 文字コード(-1 はフォントにない文字の代わりの絵), int16 送り幅, int16 1文字だけ描いたときの幅,
                  uint8 x, uint8 y, uint8 幅, uint8 高さ (インクのある範囲), ビット列(左上から1行ずつ、1 = 点あり)
"""
import gzip
import os
import struct
import sys
from pathlib import Path

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
import numpy as np  # noqa: E402
import pygame  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
FONT_PATH = ROOT / "Assets" / "StreamingAssets" / "assets" / "fonts" / "DotGothic16-Regular.ttf"
OUT_PATH = ROOT / "Assets" / "StreamingAssets" / "assets" / "fonts" / "DotGothic16-bitmap.bytes"
SIZES = [16, 32, 48]
MISSING = -1  # フォントにない文字のかわりに描く絵(.notdef)の番号


def font_codepoints(path):
    """フォントに入っている文字コードの一覧(fontTools があれば使い、なければ全部ためす)"""
    try:
        from fontTools.ttLib import TTFont
        return sorted(cp for cp in TTFont(str(path)).getBestCmap() if cp >= 0x20)
    except ImportError:
        print("fontTools がないので、U+0020〜U+FFFF を1文字ずつ調べます(少し時間がかかります)")
        return None


def glyph_record(font, ch):
    metrics = font.metrics(ch)[0]
    advance = metrics[4] if metrics else 0
    try:
        surf = font.render(ch, False, (255, 255, 255))
    except pygame.error:  # 幅が 0 の文字(結合文字など)
        surf = None
    width = surf.get_width() if surf is not None else 0
    ink = np.zeros((width, font.get_height()), dtype=bool)
    if width > 0:
        ink = pygame.surfarray.pixels_alpha(surf.convert_alpha()) > 0
    xs, ys = np.nonzero(ink)
    if len(xs):
        x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    else:
        x0 = x1 = y0 = y1 = 0
    bits = ink[x0:x1, y0:y1].T.flatten()  # 行ごと(y)に左から右(x)
    packed = np.packbits(bits.astype(np.uint8)).tobytes() if len(bits) else b""
    return advance, width, (int(x0), int(y0), int(x1 - x0), int(y1 - y0)), packed


def main():
    if not FONT_PATH.exists():
        sys.exit(f"フォントが見つかりません: {FONT_PATH}")
    pygame.init()
    pygame.display.set_mode((1, 1))
    codepoints = font_codepoints(FONT_PATH)
    out = bytearray(b"DGF1")
    out += struct.pack("<i", len(SIZES))
    for size in SIZES:
        font = pygame.font.Font(str(FONT_PATH), size)
        cps = codepoints
        if cps is None:
            cps = [cp for cp in range(0x20, 0x10000)
                   if not 0xD800 <= cp <= 0xDFFF and font.metrics(chr(cp))[0] is not None]
        missing_char = next(chr(cp) for cp in range(0xE000, 0xF900) if cp not in set(cps))
        records = [(MISSING, missing_char)] + [(cp, chr(cp)) for cp in cps]
        out += struct.pack("<iii", size, font.get_height(), len(records))
        for cp, ch in records:
            advance, width, (x, y, w, h), packed = glyph_record(font, ch)
            out += struct.pack("<ihhBBBB", cp, advance, width, x, y, w, h)
            out += packed
        print(f"size {size}: {len(records)} 文字, 行の高さ {font.get_height()}")
    OUT_PATH.write_bytes(gzip.compress(bytes(out), compresslevel=9))
    print(f"保存しました: {OUT_PATH} ({OUT_PATH.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
