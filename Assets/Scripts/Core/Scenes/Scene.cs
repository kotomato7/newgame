// すべての画面(シーン)の土台になるクラス (Pygame 版の scenes/base.py)
namespace QteRpg.Scenes
{
    public abstract class Scene
    {
        protected readonly Game Game;

        protected Scene(Game game)
        {
            Game = game;
        }

        /// <summary>この画面に切り替わったときに1回呼ばれる</summary>
        public virtual void OnEnter() { }

        /// <summary>キー入力やマウス操作を受け取る</summary>
        public virtual void HandleEvent(GameEvent e) { }

        /// <summary>毎フレームの処理。dt は前のフレームからの経過秒数</summary>
        public virtual void Update(double dt) { }

        /// <summary>毎フレームの描画</summary>
        public virtual void Draw(Surface surface) { }
    }
}
