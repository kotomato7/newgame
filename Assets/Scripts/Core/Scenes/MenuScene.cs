// メニュー画面 — 中身はあとで追加する (Pygame 版の scenes/menu.py)
using QteRpg.UI;

namespace QteRpg.Scenes
{
    public sealed class MenuScene : Scene
    {
        const int CENTER_X = Settings.SCREEN_WIDTH / 2;

        readonly SelectList _menu;
        Surface _background;

        public MenuScene(Game game) : base(game)
        {
            // 今は「もどる」だけ。設定項目などはここに追加していく
            _menu = new SelectList(new[] { "もどる" }, top: 170, centerX: CENTER_X);
        }

        public override void OnEnter()
        {
            _menu.Reset();
            // 背景は assets/images/Background/Menu から、メニューを開くたびに読みなおす(なければ黒)
            _background = Sprites.LoadBackground("Menu");
        }

        public override void HandleEvent(GameEvent e)
        {
            if (e.Type == GameEventType.KeyDown && e.Key == GameKey.Escape)
            {
                Game.ChangeScene("title");
                return;
            }
            if (_menu.HandleEvent(e) == 0)
            {
                Game.ChangeScene("title");
            }
        }

        public override void Update(double dt)
        {
            _menu.Update(dt);
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
            Utils.DrawText(surface, "メニュー", Settings.FONT_LARGE, Settings.WHITE, Anchor.Center, CENTER_X, 80);
            Utils.DrawText(surface, "（なかみはあとで）", Settings.FONT_SMALL, Settings.GRAY, Anchor.Center, CENTER_X, 124);
            _menu.Draw(surface);
        }
    }
}
