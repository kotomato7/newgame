// ゲームの本体 (Pygame 版の main.py の Game クラス)
//
// Unity とのつなぎ(毎フレームの呼び出し・キー入力・画面への表示)は Scripts/Unity/GameRoot.cs がする。
// ここでは、Pygame 版と同じく「小さな画面(480×270)に描く」ところまでを受けもつ。
using System;
using System.Collections.Generic;
using System.IO;
using QteRpg.Scenes;

namespace QteRpg
{
    public sealed class Game
    {
        /// <summary>ここ(小さい画面)に描いてから、ウィンドウいっぱいに拡大する</summary>
        public readonly Surface Canvas = new Surface(Settings.SCREEN_WIDTH, Settings.SCREEN_HEIGHT);
        public bool Running = true;

        /// <summary>プレイヤーのデータ(画面をまたいで使うので Game が持つ)</summary>
        public Player Player;

        /// <summary>BGM(画面が切りかわるたびに、その画面の曲を流す。assets/bgm と data/bgm.csv)</summary>
        public readonly IBgmPlayer Bgm;

        readonly Dictionary<string, Scene> _scenes;
        Scene _scene;

        public Game(IBgmPlayer bgm = null)
        {
            // 起動したときに1回読む(Pygame 版で game_data を import したときと同じ。読めなければエラーで止まる)
            GameData.Reload();
            NewGame();

            Bgm = bgm ?? new NullBgmPlayer();

            // 画面(シーン)の一覧。新しい画面を作ったらここに登録する
            _scenes = new Dictionary<string, Scene>
            {
                { "title", new TitleScene(this) },
                { "battle", new BattleScene(this) },
                { "menu", new MenuScene(this) },
            };
            _scene = null;
            ChangeScene("title");
        }

        /// <summary>最初から始める。パラメータ表(CSV)を読みなおし、プレイヤーを初期パラメータで作り直す</summary>
        public void NewGame()
        {
            try
            {
                GameData.Reload();
            }
            catch (Exception error) when (error is DataError || error is IOException || error is UnauthorizedAccessException)
            {
                // 書きかけの表などで読めないときは、前に読めた値のまま続ける
                Log.Warning($"[パラメータ表を読めなかったので、前の値のまま続けます] {error.Message}");
            }
            Player = new Player();
        }

        public void ChangeScene(string name)
        {
            _scene = _scenes[name];
            _scene.OnEnter();
            Bgm.PlayScene(name);  // その画面の BGM にする(曲がなければ止める)
        }

        public void Quit()
        {
            Running = false;
        }

        /// <summary>1フレーム分すすめる(Pygame 版の run() の while の中身)。dt は前のフレームからの経過秒数</summary>
        public void Step(double dt, IEnumerable<GameEvent> events)
        {
            foreach (var e in events)
            {
                _scene.HandleEvent(e);
            }

            _scene.Update(dt);
            Bgm.Update(dt);  // BGM のフェード(切りかえ)を進める
            _scene.Draw(Canvas);
        }
    }
}
