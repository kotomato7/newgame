// Unity とゲーム本体(Scripts/Core)をつなぐ MonoBehaviour (Pygame 版の main.py の run() にあたる)
//
//   ・毎フレーム: キー・マウスを読んで GameEvent にする → Game.Step() → 480×270 の絵をテクスチャにうつす
//   ・OnGUI で、その絵を画面いっぱいに「整数倍・補間なし」で拡大して表示する(ドットがくっきり残る)
//   ・ウィンドウの大きさは 960×540 (Settings.SCREEN_WIDTH × SCALE)。Project Settings の Resolution でも同じ値にしてある
//
// シーン(Assets/Scenes/Main.unity)の「Game」オブジェクトについている。参照が外れても Main シーンなら自動で作られる(Bootstrap)
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace QteRpg.UnityPort
{
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour, IPlatform
    {
        Game _game;
        BgmPlayer _bgm;
        Texture2D _texture;
        Color32[] _buffer;
        Rect _displayRect;              // 画面上で絵を表示している範囲(GUI 座標: 左上が原点)
        float _displayScale = 1f;
        Vector2 _lastMouse = new Vector2(float.NaN, float.NaN);
        string _fatalError;             // 起動できなかったときに画面に出すメッセージ
        readonly List<GameEvent> _events = new List<GameEvent>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // Main シーンに GameRoot が見つからないとき(スクリプトの参照が外れたときなど)は作る
            if (SceneManager.GetActiveScene().name == "Main" && FindAnyObjectByType<GameRoot>() == null)
            {
                new GameObject("Game").AddComponent<GameRoot>();
            }
        }

        void Awake()
        {
            Application.targetFrameRate = Settings.FPS;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;
#if !UNITY_EDITOR
            // ウィンドウの大きさを Pygame 版と同じ 960×540 にする
            Screen.SetResolution(Settings.SCREEN_WIDTH * Settings.SCALE, Settings.SCREEN_HEIGHT * Settings.SCALE, FullScreenMode.Windowed);
#endif
            Log.Output = message => Debug.Log(message);
            Log.WarningOutput = message => Debug.LogWarning(message);
            Settings.BASE_DIR = Application.streamingAssetsPath;
            Settings.SAVE_DIR = Application.persistentDataPath;  // 自己ベストのタイムなどを保存する場所
            Platform.Current = this;
            DisableIme();
            EnsureCamera();

            _texture = new Texture2D(Settings.SCREEN_WIDTH, Settings.SCREEN_HEIGHT, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Canvas",
            };
            _buffer = new Color32[Settings.SCREEN_WIDTH * Settings.SCREEN_HEIGHT];

            try
            {
                _bgm = new BgmPlayer(gameObject);
                _game = new Game(_bgm);
            }
            catch (Exception error)
            {
                _fatalError = "ゲームを始められませんでした:\n" + error.Message;
                Debug.LogException(error);
            }
        }

        /// <summary>カメラがなければ作る(画面のすき間を黒でぬるだけ。ゲームの絵は OnGUI で描く)</summary>
        static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.cullingMask = 0;
        }

        void Update()
        {
            if (_game == null)
            {
                return;
            }
            UpdateDisplayRect();
            _events.Clear();
            PollInput(_events);
            _game.Step(Time.deltaTime, _events);
            UploadCanvas();
            if (!_game.Running)
            {
                QuitGame();
            }
        }

        // ---------------- 表示 ----------------

        /// <summary>絵を表示する範囲: 画面に入るいちばん大きい整数倍(入らなければ縮小)で、真ん中に置く</summary>
        void UpdateDisplayRect()
        {
            float sx = (float)Screen.width / Settings.SCREEN_WIDTH;
            float sy = (float)Screen.height / Settings.SCREEN_HEIGHT;
            float scale = Mathf.Min(sx, sy);
            if (scale >= 1f)
            {
                scale = Mathf.Floor(scale);
            }
            float w = Settings.SCREEN_WIDTH * scale, h = Settings.SCREEN_HEIGHT * scale;
            _displayScale = scale;
            _displayRect = new Rect(Mathf.Floor((Screen.width - w) / 2f), Mathf.Floor((Screen.height - h) / 2f), w, h);
        }

        void UploadCanvas()
        {
            var canvas = _game.Canvas;
            int width = canvas.Width, height = canvas.Height;
            var pixels = canvas.Pixels;
            for (int y = 0; y < height; y++)
            {
                int src = y * width;
                int dst = (height - 1 - y) * width;  // テクスチャは下の行から並ぶので上下を入れかえる
                for (int x = 0; x < width; x++)
                {
                    var p = pixels[src + x];
                    _buffer[dst + x] = new Color32(p.R, p.G, p.B, 255);
                }
            }
            _texture.SetPixels32(_buffer);
            _texture.Apply(false, false);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            if (_fatalError != null)
            {
                GUI.Label(new Rect(10, 10, Screen.width - 20, Screen.height - 20), _fatalError);
                return;
            }
            if (_game != null)
            {
                GUI.DrawTexture(_displayRect, _texture, ScaleMode.StretchToFill, false);
            }
        }

        // ---------------- 入力 ----------------

        /// <summary>画面のマウス座標(左下が原点) → 内部解像度のドット座標(左上が原点)</summary>
        (int x, int y) ToCanvas(Vector2 screen)
        {
            float guiY = Screen.height - screen.y;
            return (Mathf.FloorToInt((screen.x - _displayRect.x) / _displayScale),
                    Mathf.FloorToInt((guiY - _displayRect.y) / _displayScale));
        }

        /// <summary>内部解像度のドット座標 → 画面のマウス座標(左下が原点)。ドットの真ん中を指す</summary>
        Vector2 ToScreen(int x, int y)
        {
            float sx = _displayRect.x + (x + 0.5f) * _displayScale;
            float guiY = _displayRect.y + (y + 0.5f) * _displayScale;
            return new Vector2(sx, Screen.height - guiY);
        }

#if ENABLE_INPUT_SYSTEM
        // 同じフレームに押されたキーは、この順番で処理する(A→S→D→F の攻撃QTE が順に入るように)
        static readonly (Key key, GameKey gameKey)[] KeyMap =
        {
            (Key.Q, GameKey.Q), (Key.W, GameKey.W), (Key.E, GameKey.E),
            (Key.A, GameKey.A), (Key.S, GameKey.S), (Key.D, GameKey.D), (Key.F, GameKey.F),
            (Key.Z, GameKey.Z), (Key.X, GameKey.X), (Key.C, GameKey.C),
            (Key.UpArrow, GameKey.Up), (Key.DownArrow, GameKey.Down), (Key.LeftArrow, GameKey.Left), (Key.RightArrow, GameKey.Right),
            (Key.Enter, GameKey.Return), (Key.NumpadEnter, GameKey.KeypadEnter), (Key.Space, GameKey.Space),
            (Key.Escape, GameKey.Escape), (Key.Backspace, GameKey.Backspace), (Key.F1, GameKey.F1),
        };

        void PollInput(List<GameEvent> events)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var pos = mouse.position.ReadValue();
                var (cx, cy) = ToCanvas(pos);
                if (pos != _lastMouse)
                {
                    events.Add(GameEvent.Mouse(GameEventType.MouseMotion, cx, cy));
                    _lastMouse = pos;
                }
                if (mouse.leftButton.wasPressedThisFrame) events.Add(GameEvent.Mouse(GameEventType.MouseButtonDown, cx, cy, 1));
                if (mouse.rightButton.wasPressedThisFrame) events.Add(GameEvent.Mouse(GameEventType.MouseButtonDown, cx, cy, 3));
                if (mouse.leftButton.wasReleasedThisFrame) events.Add(GameEvent.Mouse(GameEventType.MouseButtonUp, cx, cy, 1));
                if (mouse.rightButton.wasReleasedThisFrame) events.Add(GameEvent.Mouse(GameEventType.MouseButtonUp, cx, cy, 3));
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                foreach (var (key, gameKey) in KeyMap)
                {
                    if (keyboard[key].wasPressedThisFrame)
                    {
                        events.Add(GameEvent.KeyDown(gameKey));
                    }
                }
            }
        }

        public void WarpMouse(int x, int y)
        {
            var screen = ToScreen(x, y);
            Mouse.current?.WarpCursorPosition(screen);
            _lastMouse = screen;  // 移す前のマウスの動きは捨てる(pygame.event.clear(MOUSEMOTION) のかわり)
        }

        static void DisableIme()
        {
            // 日本語入力(IME)がキー操作を横取りしないようにする
            try
            {
                Keyboard.current?.SetIMEEnabled(false);
            }
            catch (Exception)
            {
            }
        }
#else
        static readonly (KeyCode key, GameKey gameKey)[] KeyMap =
        {
            (KeyCode.Q, GameKey.Q), (KeyCode.W, GameKey.W), (KeyCode.E, GameKey.E),
            (KeyCode.A, GameKey.A), (KeyCode.S, GameKey.S), (KeyCode.D, GameKey.D), (KeyCode.F, GameKey.F),
            (KeyCode.Z, GameKey.Z), (KeyCode.X, GameKey.X), (KeyCode.C, GameKey.C),
            (KeyCode.UpArrow, GameKey.Up), (KeyCode.DownArrow, GameKey.Down), (KeyCode.LeftArrow, GameKey.Left), (KeyCode.RightArrow, GameKey.Right),
            (KeyCode.Return, GameKey.Return), (KeyCode.KeypadEnter, GameKey.KeypadEnter), (KeyCode.Space, GameKey.Space),
            (KeyCode.Escape, GameKey.Escape), (KeyCode.Backspace, GameKey.Backspace), (KeyCode.F1, GameKey.F1),
        };

        void PollInput(List<GameEvent> events)
        {
            Vector2 pos = Input.mousePosition;
            var (cx, cy) = ToCanvas(pos);
            if (pos != _lastMouse)
            {
                events.Add(GameEvent.Mouse(GameEventType.MouseMotion, cx, cy));
                _lastMouse = pos;
            }
            if (Input.GetMouseButtonDown(0)) events.Add(GameEvent.Mouse(GameEventType.MouseButtonDown, cx, cy, 1));
            if (Input.GetMouseButtonDown(1)) events.Add(GameEvent.Mouse(GameEventType.MouseButtonDown, cx, cy, 3));
            if (Input.GetMouseButtonUp(0)) events.Add(GameEvent.Mouse(GameEventType.MouseButtonUp, cx, cy, 1));
            if (Input.GetMouseButtonUp(1)) events.Add(GameEvent.Mouse(GameEventType.MouseButtonUp, cx, cy, 3));
            foreach (var (key, gameKey) in KeyMap)
            {
                if (Input.GetKeyDown(key))
                {
                    events.Add(GameEvent.KeyDown(gameKey));
                }
            }
        }

        public void WarpMouse(int x, int y)
        {
            // 古い入力方式(Input Manager)にはマウスを動かす機能がないので、何もしない
        }

        static void DisableIme()
        {
            try
            {
                Input.imeCompositionMode = IMECompositionMode.Off;
            }
            catch (Exception)
            {
            }
        }
#endif

        // ---------------- 画像 ----------------

        /// <summary>PNG / JPG を読んで Surface にする(Pygame 版の pygame.image.load のかわり)</summary>
        public Surface LoadImage(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(bytes, false))
                {
                    throw new IOException("画像として読めません(Unity で読めるのは PNG と JPG です)");
                }
                int width = texture.width, height = texture.height;
                var pixels = texture.GetPixels32();
                var surface = new Surface(width, height, SurfaceKind.Alpha);
                for (int y = 0; y < height; y++)
                {
                    int src = (height - 1 - y) * width;  // テクスチャは下の行から並ぶ
                    int dst = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        var p = pixels[src + x];
                        surface.Pixels[dst + x] = new Rgba(p.r, p.g, p.b, p.a);
                    }
                }
                return surface;
            }
            finally
            {
                Destroy(texture);
            }
        }

        // ---------------- 終了 ----------------

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy()
        {
            _bgm?.Dispose();
            if (_texture != null)
            {
                Destroy(_texture);
            }
            if (ReferenceEquals(Platform.Current, this))
            {
                Platform.Current = null;
            }
        }
    }
}
