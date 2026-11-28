// メニュー画面 (Pygame 版の scenes/menu.py)
//
//   タイム … 自己ベストのタイム(3面までクリアしたときのタイム。速い順に3位まで)を見る(BestTimes.cs)
//   もどる … タイトルへ
using System.Collections.Generic;
using QteRpg.UI;

namespace QteRpg.Scenes
{
    public sealed class MenuScene : Scene
    {
        const int CENTER_X = Settings.SCREEN_WIDTH / 2;
        const int TIME_ROW_TOP = 116;  // タイムの画面: 1位の行の高さ(真ん中)
        const int TIME_ROW_GAP = 38;   // タイムの画面: 行の間隔

        readonly SelectList _menu;      // タイム / もどる
        readonly SelectList _backMenu;  // タイムの画面の「もどる」
        string _state = "menu";         // "menu"(タイム / もどる をえらぶ)か "times"(タイムを見ている)
        List<BestTime> _bestTimes = new List<BestTime>();
        Surface _background;

        public MenuScene(Game game) : base(game)
        {
            // 設定項目などはここに追加していく
            _menu = new SelectList(new[] { "タイム", "もどる" }, top: 140, centerX: CENTER_X);
            _backMenu = new SelectList(new[] { "もどる" }, top: 228, centerX: CENTER_X);
        }

        public override void OnEnter()
        {
            _menu.Reset();
            _state = "menu";
            // 背景は assets/images/Background/Menu から、メニューを開くたびに読みなおす(なければ黒)
            _background = Sprites.LoadBackground("Menu");
        }

        public override void HandleEvent(GameEvent e)
        {
            if (_state == "times")
            {
                // ESC・右クリックか「もどる」で、タイム / もどる の選択にもどる
                if (SelectList.IsCancel(e) || _backMenu.HandleEvent(e) == 0)
                {
                    _state = "menu";
                }
                return;
            }
            if (e.Type == GameEventType.KeyDown && e.Key == GameKey.Escape)
            {
                Game.ChangeScene("title");
                return;
            }
            int? choice = _menu.HandleEvent(e);
            if (choice == 0)  // タイム → 自己ベストを読んで表示する
            {
                _bestTimes = BestTimes.Load();
                _backMenu.Reset();
                _state = "times";
            }
            else if (choice == 1)  // もどる → タイトルへ
            {
                Game.ChangeScene("title");
            }
        }

        public override void Update(double dt)
        {
            _menu.Update(dt);
            _backMenu.Update(dt);
        }

        public override void Draw(Surface surface)
        {
            if (_background != null)
            {
                surface.Blit(_background, 0, 0);
            }
            else
            {
                surface.Fill(Settings.BLACK);
            }
            if (_state == "times")
            {
                DrawTimes(surface);
                return;
            }
            Utils.DrawText(surface, "メニュー", Settings.FONT_LARGE, Settings.WHITE, Anchor.Center, CENTER_X, 80);
            _menu.Draw(surface);
        }

        /// <summary>タイムの画面: 自己ベストの 1位〜3位。記録がない順位は「--:--:--」</summary>
        void DrawTimes(Surface surface)
        {
            Utils.DrawText(surface, "タイム", Settings.FONT_LARGE, Settings.WHITE, Anchor.Center, CENTER_X, 46);
            Utils.DrawText(surface, "3面クリアまでの自己ベスト", Settings.FONT_SMALL, Settings.GRAY, Anchor.Center, CENTER_X, 78);
            for (int i = 0; i < BestTimes.MAX; i++)
            {
                bool has = i < _bestTimes.Count;
                string time = has ? Utils.FormatCentis(_bestTimes[i].Centis) : "--:--:--";
                var color = !has ? Settings.GRAY : i == 0 ? Settings.GLOW : Settings.WHITE;  // 1位は金色
                Utils.DrawText(surface, $"{i + 1}位  {time}", Settings.FONT_LARGE, color, Anchor.Center, CENTER_X, TIME_ROW_TOP + i * TIME_ROW_GAP);
            }
            _backMenu.Draw(surface);
        }
    }
}
