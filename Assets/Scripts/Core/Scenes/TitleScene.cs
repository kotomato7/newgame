// タイトル画面: はじめる / メニュー / やめる (Pygame 版の scenes/title.py)
using QteRpg.UI;

namespace QteRpg.Scenes
{
    public sealed class TitleScene : Scene
    {
        const int CENTER_X = Settings.SCREEN_WIDTH / 2;

        readonly SelectList _menu;
        readonly Surface _titleImage;
        Surface _background;

        public TitleScene(Game game) : base(game)
        {
            // はじめる・メニュー・やめる は 黒の太字
            _menu = new SelectList(new[] { "はじめる", "メニュー", "やめる" }, top: 140, centerX: CENTER_X, gap: 24,
                                   color: Settings.BLACK, bold: true);
            // ゲーム名: 黒いふち + 朱色(上)→えんじ色(下) のグラデーション
            _titleImage = Utils.RenderTitleText(Settings.GAME_TITLE, Settings.FONT_TITLE, Settings.SHU, Settings.ENJI,
                                                outlineColor: Settings.BLACK);
        }

        public override void OnEnter()
        {
            _menu.Reset();
            // 背景は assets/images/Background/Title から、タイトルにもどるたびに読みなおす(なければ黒)
            _background = Sprites.LoadBackground("Title");
        }

        public override void HandleEvent(GameEvent e)
        {
            int? choice = _menu.HandleEvent(e);
            if (choice == 0)  // はじめる → プレイヤーを作り直してバトルへ
            {
                Game.NewGame();
                Game.ChangeScene("battle");
            }
            else if (choice == 1)
            {
                Game.ChangeScene("menu");
            }
            else if (choice == 2)
            {
                Game.Quit();
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
            surface.Blit(_titleImage, _titleImage.GetRect(Anchor.Center, CENTER_X, 80));
            _menu.Draw(surface);
        }
    }
}
