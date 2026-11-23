// 白わくのウィンドウ(コマンド欄・メッセージ欄など)を描く (Pygame 版の ui/window.py)
namespace QteRpg.UI
{
    public static class Window
    {
        public static void DrawWindow(Surface surface, PgRect rect)
        {
            Draw.Rect(surface, Settings.BLACK, rect, 0, 3);
            Draw.Rect(surface, Settings.WHITE, rect, 1, 3);
        }
    }
}
