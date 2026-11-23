// 右下のステータス紋章: 丸(HPゲージ) + 五芒星 + 頂点の点(QTEポイント) (Pygame 版の ui/status_emblem.py)
//
//   丸   … HP。満タンで一周、減った割合だけ真上から右まわり(時計回り)に欠ける
//          減ったターンのあいだは、減った部分を赤で表示し、次のターンに欠ける
//   点   … QTEポイント。たまった数だけ、決まった順番で光る
//   線   … 光った点どうしをつなぐ線も光る(5個そろうと星全体が光る)
using System;
using System.Collections.Generic;
using System.Linq;

namespace QteRpg.UI
{
    public sealed class StatusEmblem
    {
        // 点が光る順番。頂点の番号は 0=一番上、そこから時計回りに 1, 2, 3, 4
        // 一番上 → 右へ2つとなり → さらに右へ2つとなり … (星を一筆書きする順番)
        public static readonly int[] LIGHT_ORDER = { 0, 2, 4, 1, 3 };

        const double FLASH_TIME = 0.5;  // 点が新しく光ったときのフラッシュの長さ(秒)

        readonly (int x, int y) _center;
        readonly int _radius;     // 丸の外側の半径
        readonly int _ringWidth;  // 丸の太さ
        readonly List<(int x, int y)> _vertices = new List<(int x, int y)>();

        double _timer;
        (int, int)? _ringKey;    // HPの丸は、HPが変わったときだけ描き直す
        Surface _ringImage;
        int? _turnStartHp;       // このターンが始まったときのHP(赤く表示する部分の基準)
        int? _lastPoints;
        double _flashTimer;
        int? _flashVertex;

        public StatusEmblem((int x, int y) center, int radius = 34, int ringWidth = 4)
        {
            _center = center;
            _radius = radius;
            _ringWidth = ringWidth;

            // 五芒星の頂点(丸の内側に置く)
            int starR = radius - ringWidth - 4;
            for (int k = 0; k < 5; k++)
            {
                double angle = 2 * Math.PI * k / 5;  // 0 = 真上、時計回り
                _vertices.Add((Py.Round(center.x + starR * Math.Sin(angle)), Py.Round(center.y - starR * Math.Cos(angle))));
            }
        }

        // ---- ターン ----
        /// <summary>新しいターンの始めに呼ぶ。前のターンに減った分(赤い部分)はここで消える</summary>
        public void StartTurn(Player player) => _turnStartHp = player.Hp;

        // ---- 更新 ----
        public void Update(double dt, Player player)
        {
            _timer += dt;
            _flashTimer = Math.Max(0.0, _flashTimer - dt);

            // QTEポイントが増えたら、新しく光った点をフラッシュさせる
            int points = player.QtePoints;
            if (_lastPoints != null && points > _lastPoints)
            {
                _flashVertex = LIGHT_ORDER[points - 1];
                _flashTimer = FLASH_TIME;
            }
            else if (_lastPoints != null && points < _lastPoints)
            {
                _flashTimer = 0.0;  // ひっさつなどでポイントが減ったらフラッシュも止める
            }
            _lastPoints = points;
        }

        // ---- 描画 ----
        public void Draw(Surface surface, Player player)
        {
            int maxHp = player.MaxHp;
            int startHp = _turnStartHp ?? player.Hp;
            startHp = Math.Max(startHp, player.Hp);  // ターン中に回復した分は赤くしない
            double hpRatio = maxHp != 0 ? (double)player.Hp / maxHp : 0;
            double startRatio = maxHp != 0 ? (double)startHp / maxHp : 0;
            DrawRing(surface, hpRatio, startRatio);
            DrawStar(surface, player.QtePoints);
        }

        void DrawRing(Surface surface, double ratio, double startRatio)
        {
            var key = (Py.Round(ratio * 360), Py.Round(startRatio * 360));  // 1度きざみで作り直す
            if (_ringKey == null || !_ringKey.Value.Equals(key))
            {
                _ringImage = MakeRing(key.Item1 / 360.0, key.Item2 / 360.0);
                _ringKey = key;
            }
            surface.Blit(_ringImage, _center.x - _radius, _center.y - _radius);
        }

        /// <summary>
        /// HPの丸の画像を作る
        ///   ratio      … 今のHPの割合(0〜1)。この分を白で描く
        ///   startRatio … ターン開始時のHPの割合。ratio との差(このターンに減った分)を赤で描く
        /// 欠ける部分は真上から右まわりに広がる(残りは欠けた所の先から真上まで)
        /// </summary>
        Surface MakeRing(double ratio, double startRatio)
        {
            int size = _radius * 2 + 1;
            var image = new Surface(size, size, SurfaceKind.Alpha);
            double rOut = _radius + 0.5;
            double rIn = _radius - _ringWidth + 0.5;
            double missingNow = (1 - ratio) * 2 * Math.PI;          // ここより先(右まわり)が白
            double missingBefore = (1 - startRatio) * 2 * Math.PI;  // ここから missingNow までが赤
            for (int y = 0; y < size; y++)
            {
                int dy = y - _radius;
                for (int x = 0; x < size; x++)
                {
                    int dx = x - _radius;
                    double dist = Py.Hypot(dx, dy);
                    if (rIn <= dist && dist < rOut)
                    {
                        double angle = Py.Mod(Math.Atan2(dx, -dy), 2 * Math.PI);  // 真上が0、時計回りに増える
                        if (angle >= missingNow)
                        {
                            image.SetAt(x, y, Settings.WHITE);
                        }
                        else if (angle >= missingBefore)
                        {
                            image.SetAt(x, y, Settings.DAMAGE_RED);
                        }
                    }
                }
            }
            return image;
        }

        /// <summary>光らせる線(頂点の組)の一覧</summary>
        List<(int a, int b)> LitEdges(int points)
        {
            var edges = new List<(int a, int b)>();
            for (int i = 0; i < points - 1; i++)
            {
                edges.Add((LIGHT_ORDER[i], LIGHT_ORDER[i + 1]));
            }
            if (points >= GameData.QTE_MAX)  // 5個そろったら最後の点と最初の点もつないで星が完成
            {
                edges.Add((LIGHT_ORDER[LIGHT_ORDER.Length - 1], LIGHT_ORDER[0]));
            }
            return edges;
        }

        void DrawStar(Surface surface, int points)
        {
            var v = _vertices;
            var litVertices = LIGHT_ORDER.Take(points).ToList();
            var litEdges = LitEdges(points);
            double pulse = 0.5 + 0.5 * Math.Sin(_timer * 4);  // 0〜1 でゆっくり明滅

            // 1) 星の線(光っていない状態)
            for (int k = 0; k < 5; k++)
            {
                QteRpg.Draw.Line(surface, Settings.GRAY, v[k], v[(k + 2) % 5], 1);
            }

            // 2) 光っている部分のぼんやりした光(半透明の画像に描いてから重ねる)
            const int pad = 14;
            int ox = _center.x - _radius - pad, oy = _center.y - _radius - pad;
            int size = (_radius + pad) * 2 + 1;
            var glow = new Surface(size, size, SurfaceKind.Alpha);
            var local = v.Select(p => (p.x - ox, p.y - oy)).ToList();  // glow 画像の中での座標
            var glowColor = Settings.GLOW.WithAlpha((int)(50 + 50 * pulse));
            foreach (var (a, b) in litEdges)
            {
                QteRpg.Draw.Line(glow, glowColor, local[a], local[b], 3);
            }
            foreach (int k in litVertices)
            {
                QteRpg.Draw.Circle(glow, glowColor, local[k], 6);
            }
            if (_flashTimer > 0 && _flashVertex != null)
            {
                double t = 1 - _flashTimer / FLASH_TIME;  // 0 → 1 で広がりながら消える
                var flashColor = Settings.GLOW.WithAlpha((int)(255 * (1 - t)));
                QteRpg.Draw.Circle(glow, flashColor, local[_flashVertex.Value], (int)(4 + 10 * t), 1);
            }
            surface.Blit(glow, ox, oy);

            // 3) 光っている線(くっきり)
            foreach (var (a, b) in litEdges)
            {
                QteRpg.Draw.Line(surface, Settings.GLOW, v[a], v[b], 1);
            }

            // 4) 頂点の点
            for (int k = 0; k < 5; k++)
            {
                if (litVertices.Contains(k))
                {
                    QteRpg.Draw.Circle(surface, Settings.GLOW, v[k], 3);
                    surface.SetAt(v[k].x, v[k].y, Settings.WHITE);  // 真ん中を白くして光って見せる
                }
                else
                {
                    QteRpg.Draw.Circle(surface, Settings.BLACK, v[k], 3);
                    QteRpg.Draw.Circle(surface, Settings.GRAY, v[k], 3, 1);
                }
            }
        }
    }
}
