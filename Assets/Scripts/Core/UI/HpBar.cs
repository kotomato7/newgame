// 敵のHPバー(数字なし) (Pygame 版の ui/hp_bar.py)
//
// 減るときはなめらかに縮み、縮んでいる途中の部分は赤で見せる
using System;

namespace QteRpg.UI
{
    public sealed class HpBar
    {
        const double DRAIN_SPEED = 1.2;  // 1秒で最大HPの何倍ぶん縮むか

        readonly Character _unit;
        readonly PgRect _rect;
        double _shownHp;  // 表示しているHP(実際のHPに少し遅れてついていく)

        public HpBar(Character unit, int centerX, int top, int width = 52, int height = 6)
        {
            _unit = unit;
            _rect = PgRect.Anchored(width, height, Anchor.MidTop, centerX, top);
            _shownHp = unit.Hp;
        }

        public void Update(double dt)
        {
            if (_shownHp > _unit.Hp)
            {
                _shownHp = Math.Max(_unit.Hp, _shownHp - _unit.MaxHp * DRAIN_SPEED * dt);
            }
            else
            {
                _shownHp = _unit.Hp;
            }
        }

        public void Draw(Surface surface)
        {
            var inner = _rect.Inflate(-2, -2);
            QteRpg.Draw.Rect(surface, Settings.BLACK, _rect);
            int maxHp = _unit.MaxHp != 0 ? _unit.MaxHp : 1;
            int hpW = Py.Round((double)inner.Width * _unit.Hp / maxHp);
            int shownW = Py.Round(inner.Width * _shownHp / maxHp);
            if (shownW > hpW)  // 減っている途中の部分
            {
                QteRpg.Draw.Rect(surface, Settings.DAMAGE_RED, new PgRect(inner.X + hpW, inner.Y, shownW - hpW, inner.Height));
            }
            if (hpW > 0)
            {
                QteRpg.Draw.Rect(surface, Settings.WHITE, new PgRect(inner.X, inner.Y, hpW, inner.Height));
            }
            QteRpg.Draw.Rect(surface, Settings.LIGHT_GRAY, _rect, 1);
        }
    }
}
