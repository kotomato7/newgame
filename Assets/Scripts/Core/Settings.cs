// ゲーム全体の設定値をまとめたファイル (Pygame 版の settings.py)
using System.IO;

namespace QteRpg
{
    public static class Settings
    {
        // ---- 画面 ----
        // ドット感を出すため、小さな画面(内部解像度)に描いてから整数倍に拡大して表示する
        public const int SCREEN_WIDTH = 480;   // 内部解像度(ドット数)
        public const int SCREEN_HEIGHT = 270;
        public const int SCALE = 2;            // 表示倍率 (2 → 960x540 のウィンドウ。大きくしたければ 3 に)
        public const int FPS = 60;

        public const string GAME_TITLE = "キーマウ如律令";  // ゲーム名。タイトル画面に表示(ウィンドウ名は Project Settings の Product Name)

        // ---- 色 (R, G, B) ----
        public static readonly Rgba BLACK = new Rgba(0, 0, 0);
        public static readonly Rgba WHITE = new Rgba(255, 255, 255);
        public static readonly Rgba GRAY = new Rgba(120, 120, 120);
        public static readonly Rgba LIGHT_GRAY = new Rgba(190, 190, 190);
        public static readonly Rgba GROUND = new Rgba(28, 28, 28);       // バトル画面の地面
        public static readonly Rgba SHADOW = new Rgba(10, 10, 10);       // キャラの足元の影
        public static readonly Rgba GLOW = new Rgba(255, 214, 92);       // 光った QTEポイント・線の色
        public static readonly Rgba SHU = new Rgba(235, 97, 1);          // 朱色(しゅいろ)
        public static readonly Rgba ENJI = new Rgba(159, 32, 48);        // えんじ色(臙脂)
        public static readonly Rgba DAMAGE_RED = new Rgba(220, 48, 48);  // このターンに減ったHP(丸の赤い部分)

        // ---- フォント ----
        // DotGothic16 は 16px 単位で作られたドットフォント。
        // サイズは 16 の倍数 (16, 32, ...) にするとドットが崩れずきれいに出る
        public const int FONT_SMALL = 16;
        public const int FONT_LARGE = 32;
        public const int FONT_TITLE = 48;  // タイトル画面のゲーム名(16の倍数だとドットがくっきり)

        // ---- ファイルの場所 ----
        // Pygame 版のフォルダ(assets/ と data/)は、Unity では Assets/StreamingAssets の中にある。
        // ゲームを動かしているあいだも、このフォルダのファイルを直接読む(書きかえるとすぐ反映される)
        public static string BASE_DIR = ".";  // Unity では Application.streamingAssetsPath (GameRoot が設定する)
        // 自己ベストのタイムなど、ゲームが書きこむファイルを置く場所(ゲームのフォルダとは別。入れなおしても消えない)
        public static string SAVE_DIR = ".";  // Unity では Application.persistentDataPath (GameRoot が設定する)

        public static string FONT_PATH => Path.Combine(BASE_DIR, "assets", "fonts", "DotGothic16-bitmap.bytes");
    }
}
