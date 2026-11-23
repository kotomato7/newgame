// ゲーム全体で使う小さな部品
//   Rgba     … 色 (R, G, B, A)
//   PgRect   … pygame.Rect と同じ動きをする四角形(位置と大きさ)
//   Anchor   … 文字や画像を「どこを基準に」置くか(center / midtop など)
//   Py       … Python と同じ結果になる計算(% や round など)
//   Log      … ターミナル(Unity では Console)に文字を出す
//   GameEvent… キー入力・マウス操作(pygame のイベントのかわり)
//   IPlatform / IBgmPlayer … Unity に頼る部分(画像の読みこみ・マウス移動・BGM)の窓口
//
// このフォルダ(Core)のコードは UnityEngine を使わない。Unity とのつなぎは Scripts/Unity にある
using System;
using System.Collections.Generic;

namespace QteRpg
{
    /// <summary>色 (R, G, B, A)。A は 255 = 不透明</summary>
    public struct Rgba : IEquatable<Rgba>
    {
        public byte R, G, B, A;

        public Rgba(int r, int g, int b, int a = 255)
        {
            R = (byte)r;
            G = (byte)g;
            B = (byte)b;
            A = (byte)a;
        }

        /// <summary>同じ色で、A(不透明度)だけ変えたもの</summary>
        public Rgba WithAlpha(int a) => new Rgba(R, G, B, a);

        public bool Equals(Rgba other) => R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object obj) => obj is Rgba other && Equals(other);
        public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;
        public override string ToString() => $"({R}, {G}, {B}, {A})";
    }

    /// <summary>文字や画像を置くときの基準の位置(pygame の get_rect(center=...) などと同じ)</summary>
    public enum Anchor
    {
        TopLeft,
        Center,
        MidTop,
        MidBottom,
        MidLeft,
        BottomLeft,
    }

    /// <summary>pygame.Rect と同じ四角形。x, y は左上、w, h は幅と高さ(ドット)</summary>
    public struct PgRect
    {
        public int X, Y, W, H;

        public PgRect(int x, int y, int w, int h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public int Left => X;
        public int Top => Y;
        public int Right => X + W;
        public int Bottom => Y + H;
        public int Width => W;
        public int Height => H;
        public int CenterX => X + W / 2;
        public int CenterY => Y + H / 2;

        /// <summary>基準の位置 anchor が (x, y) になるように置いた四角形</summary>
        public static PgRect Anchored(int w, int h, Anchor anchor, int x, int y)
        {
            var rect = new PgRect(0, 0, w, h);
            rect.SetAnchor(anchor, x, y);
            return rect;
        }

        public void SetAnchor(Anchor anchor, int x, int y)
        {
            switch (anchor)
            {
                case Anchor.TopLeft: X = x; Y = y; break;
                case Anchor.Center: X = x - W / 2; Y = y - H / 2; break;
                case Anchor.MidTop: X = x - W / 2; Y = y; break;
                case Anchor.MidBottom: X = x - W / 2; Y = y - H; break;
                case Anchor.MidLeft: X = x; Y = y - H / 2; break;
                case Anchor.BottomLeft: X = x; Y = y - H; break;
            }
        }

        /// <summary>rect.move(dx, dy)</summary>
        public PgRect Move(int dx, int dy) => new PgRect(X + dx, Y + dy, W, H);

        /// <summary>rect.inflate(dx, dy): 真ん中を変えずに大きさを変える</summary>
        public PgRect Inflate(int dx, int dy) => new PgRect(X - dx / 2, Y - dy / 2, W + dx, H + dy);

        /// <summary>rect.collidepoint(x, y)</summary>
        public bool CollidePoint(int px, int py) => px >= X && px < X + W && py >= Y && py < Y + H;

        public override string ToString() => $"<rect({X}, {Y}, {W}, {H})>";
    }

    /// <summary>Python と同じ結果になる計算</summary>
    public static class Py
    {
        /// <summary>Python の a % n (結果の符号は n と同じ)</summary>
        public static int Mod(int a, int n)
        {
            int r = a % n;
            return (r != 0 && ((r < 0) != (n < 0))) ? r + n : r;
        }

        /// <summary>Python の a % n (小数)</summary>
        public static double Mod(double a, double n)
        {
            double r = a % n;
            if (r != 0 && ((r < 0) != (n < 0)))
            {
                r += n;
            }
            return r;
        }

        /// <summary>Python の a // b (小さいほうへ切り捨てる割り算)</summary>
        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            return (a % b != 0 && ((a < 0) != (b < 0))) ? q - 1 : q;
        }

        /// <summary>Python の round(x): いちばん近い整数(ちょうど半分なら偶数のほう)</summary>
        public static int Round(double x) => (int)Math.Round(x, MidpointRounding.ToEven);

        public static int Clamp(int value, int low, int high) => Math.Max(low, Math.Min(value, high));
        public static double Clamp(double value, double low, double high) => Math.Max(low, Math.Min(value, high));

        public static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);

        public static double Dist((int x, int y) a, (int x, int y) b) => Hypot(a.x - b.x, a.y - b.y);
    }

    /// <summary>ターミナル(Unity では Console)に文字を出す。Python の print のかわり</summary>
    public static class Log
    {
        public static Action<string> Output = Console.WriteLine;
        public static Action<string> WarningOutput = Console.WriteLine;

        public static void Print(string message) => Output?.Invoke(message);
        public static void Warning(string message) => WarningOutput?.Invoke(message);
    }

    // ---------------- 入力 ----------------

    public enum GameEventType
    {
        KeyDown,          // キーを押した
        MouseMotion,      // マウスを動かした
        MouseButtonDown,  // マウスのボタンを押した (1 = 左, 3 = 右)
        MouseButtonUp,    // マウスのボタンをはなした
    }

    /// <summary>ゲームで使うキー</summary>
    public enum GameKey
    {
        None,
        Q, W, E, A, S, D, F, Z, X, C,
        Up, Down, Left, Right,
        Return, KeypadEnter, Space, Escape, Backspace,
        F1,
    }

    /// <summary>pygame のイベントのかわり。X, Y は内部解像度(480×270)のドット座標</summary>
    public struct GameEvent
    {
        public GameEventType Type;
        public GameKey Key;
        public int Button;
        public int X, Y;

        public (int x, int y) Pos => (X, Y);

        public static GameEvent KeyDown(GameKey key) => new GameEvent { Type = GameEventType.KeyDown, Key = key };

        public static GameEvent Mouse(GameEventType type, int x, int y, int button = 0) =>
            new GameEvent { Type = type, X = x, Y = y, Button = button };

        public override string ToString() => Type == GameEventType.KeyDown ? $"KeyDown({Key})" : $"{Type}({X}, {Y}, {Button})";
    }

    // ---------------- Unity に頼る部分の窓口 ----------------

    /// <summary>画像の読みこみ・マウスの移動など、Unity(または テスト用の環境)が用意するもの</summary>
    public interface IPlatform
    {
        /// <summary>画像ファイルを読む(読めなければ例外)。戻り値は透明度つきの Surface</summary>
        Surface LoadImage(string path);

        /// <summary>マウスを内部解像度のドット座標 (x, y) に移す</summary>
        void WarpMouse(int x, int y);
    }

    /// <summary>BGM(画面ごとの音楽)</summary>
    public interface IBgmPlayer
    {
        /// <summary>画面が切りかわったときに呼ぶ。その画面の曲に切りかえる</summary>
        void PlayScene(string sceneName);

        /// <summary>毎フレーム呼ぶ。フェードを進める</summary>
        void Update(double dt);
    }

    /// <summary>何もしない BGM(音が出せないとき・テスト用)</summary>
    public sealed class NullBgmPlayer : IBgmPlayer
    {
        public void PlayScene(string sceneName) { }
        public void Update(double dt) { }
    }

    public static class Platform
    {
        public static IPlatform Current;
    }

    /// <summary>Python の辞書の get(key, default) のかわり</summary>
    public static class DictExtensions
    {
        public static TValue GetOr<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue fallback)
        {
            return key != null && dict.TryGetValue(key, out var value) ? value : fallback;
        }
    }
}
