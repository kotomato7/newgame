// キャラの画像(アニメーションのコマ)を読みこむ (Pygame 版の ui/sprites.py)
//
//   assets/images/Player/<動きのフォルダ>/01.png, 02.png …
//   assets/images/Enemy/<フォルダ名>/<動きのフォルダ>/01.png, 02.png …
//     動きのフォルダ … Normal(待機) Attack(攻撃) Damage(ダメージ) Special(ひっさつ) Item(じゅふ)
//     フォルダ名     … data/characters.csv の「フォルダ名」(Oni など)
//   assets/images/Background/<Title・Menu・Battle>/01.png … 背景
//   (Unity では Assets/StreamingAssets の中)
//
//   ・番号の小さい順に並べる(01, 2, 10 … のように桁がそろっていなくてもよい)
//   ・画像ではないファイル(説明の .txt や Unity が作る .meta など)は無視する
//   ・読めない画像は Console に知らせて、とばす
//   ・Unity で読める画像は PNG と JPG だけ
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace QteRpg.UI
{
    public static class Sprites
    {
        static string ImageDir => Path.Combine(Settings.BASE_DIR, "assets", "images");
        static string BackgroundDir => Path.Combine(ImageDir, "Background");
        static readonly HashSet<string> IMAGE_EXTS = new HashSet<string> { ".png", ".jpg", ".jpeg" };
        static readonly Regex Digits = new Regex(@"\d+");

        /// <summary>ファイル名の中の番号で並べる(番号がなければ名前順で後ろへ)</summary>
        sealed class FrameOrder : IComparer<string>
        {
            public static readonly FrameOrder Instance = new FrameOrder();

            static (int group, double number, string name) Key(string path)
            {
                string name = Path.GetFileName(path);
                var matches = Digits.Matches(Path.GetFileNameWithoutExtension(path));
                if (matches.Count == 0)
                {
                    return (1, 0, name);
                }
                double number = 0;
                foreach (char c in matches[matches.Count - 1].Value)
                {
                    number = number * 10 + char.GetNumericValue(c);
                }
                return (0, number, name);
            }

            public int Compare(string a, string b)
            {
                var ka = Key(a);
                var kb = Key(b);
                int c = ka.group.CompareTo(kb.group);
                if (c != 0) return c;
                c = ka.number.CompareTo(kb.number);
                if (c != 0) return c;
                return string.CompareOrdinal(ka.name, kb.name);
            }
        }

        static List<string> ImageFiles(string folder)
        {
            return Directory.GetFiles(folder)
                .Where(path => IMAGE_EXTS.Contains(Path.GetExtension(path).ToLowerInvariant()))
                .OrderBy(path => path, FrameOrder.Instance)
                .ToList();
        }

        static Surface LoadImage(string path) => Platform.Current.LoadImage(path).ConvertAlpha();

        /// <summary>
        /// folder の中の動きごとのフォルダから画像を読む。戻り値: {"待機": [画像, 画像, …], ...}
        /// 画像が1枚もない動きは入れない
        /// </summary>
        public static Dictionary<string, List<Surface>> LoadAnimations(string folder)
        {
            var animations = new Dictionary<string, List<Surface>>();
            foreach (string motion in GameData.MOTIONS)
            {
                string motionDir = Path.Combine(folder, GameData.MOTION_FOLDERS[motion]);
                if (!Directory.Exists(motionDir))
                {
                    continue;
                }
                var frames = new List<Surface>();
                foreach (string path in ImageFiles(motionDir))
                {
                    try
                    {
                        frames.Add(LoadImage(path));
                    }
                    catch (Exception error)
                    {
                        Log.Print($"[画像を読めなかったので、とばします] {path}: {error.Message}");
                    }
                }
                if (frames.Count > 0)
                {
                    animations[motion] = frames;
                }
            }
            return animations;
        }

        /// <summary>
        /// すべてのコマを scale 倍の大きさにする(1 ならそのまま)
        /// 補間しない拡大なので、ドットはぼやけない(ただし整数倍でないとドットの幅が少し不ぞろいになる)
        /// </summary>
        public static Dictionary<string, List<Surface>> ScaleAnimations(Dictionary<string, List<Surface>> animations, double scale)
        {
            if (scale == 1)
            {
                return animations;
            }
            var scaled = new Dictionary<string, List<Surface>>();
            foreach (var pair in animations)
            {
                scaled[pair.Key] = pair.Value
                    .Select(frame => frame.Scale(Py.Round(frame.Width * scale), Py.Round(frame.Height * scale)))
                    .ToList();
            }
            return scaled;
        }

        public static Dictionary<string, List<Surface>> PlayerAnimations() => LoadAnimations(Path.Combine(ImageDir, "Player"));

        /// <summary>folderName … data/characters.csv の「フォルダ名」(Oni など)</summary>
        public static Dictionary<string, List<Surface>> EnemyAnimations(string folderName)
        {
            string folder = Path.Combine(ImageDir, "Enemy", folderName);
            if (!Directory.Exists(folder))
            {
                Log.Print($"[画像] 敵の画像フォルダ {folder} がないので、白い四角で表示します");
            }
            return LoadAnimations(folder);
        }

        /// <summary>
        /// assets/images/Background/<sceneFolder>/ の中の画像を1枚読み、画面の大きさ(480×270)に合わせて返す
        /// 画像が複数あるときは番号(名前)のいちばん小さいものを使う。画像がなければ null
        /// </summary>
        public static Surface LoadBackground(string sceneFolder)
        {
            string folder = Path.Combine(BackgroundDir, sceneFolder);
            if (!Directory.Exists(folder))
            {
                return null;
            }
            foreach (string path in ImageFiles(folder))
            {
                Surface image;
                try
                {
                    image = Platform.Current.LoadImage(path).ConvertOpaque();
                }
                catch (Exception error)
                {
                    Log.Print($"[背景画像を読めなかったので、とばします] {path}: {error.Message}");
                    continue;
                }
                if (image.Width != Settings.SCREEN_WIDTH || image.Height != Settings.SCREEN_HEIGHT)
                {
                    // 大きさがちがうときは画面いっぱいに引きのばす(ドットがぼやけない拡大)
                    image = image.Scale(Settings.SCREEN_WIDTH, Settings.SCREEN_HEIGHT);
                }
                return image;
            }
            return null;
        }
    }
}
