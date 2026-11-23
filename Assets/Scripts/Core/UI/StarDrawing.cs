// ひっさつ用: マウスで星(五芒星)をえがくミニゲーム (Pygame 版の ui/star_drawing.py)
//
//   画面の真ん中に星のお手本と 1〜5 の番号が出る
//   ひだりクリックを押したまま 1 → 2 → 3 → 4 → 5 → 1 の順に線をなぞれたら成功
//   (この順番は、右下の紋章で QTEポイントが光る順番と同じ)
//
//   失敗になるのは
//     ・線から大きくはみ出した
//     ・とちゅうでクリックをはなした
//     ・時間切れ
using System;
using System.Collections.Generic;
using System.Linq;

namespace QteRpg.UI
{
    public sealed class StarDrawing
    {
        const int HIT_RADIUS = 9;   // 頂点にこれだけ近づいたら「通った」とみなす(ドット)
        const int TOLERANCE = 14;   // 線からこれ以上はなれたら「はみ出し」(ドット)
        // 1→2→3→4→5→1 (頂点の番号: 0=一番上、時計回り)
        static readonly int[] PATH = StatusEmblem.LIGHT_ORDER.Concat(new[] { StatusEmblem.LIGHT_ORDER[0] }).ToArray();

        /// <summary>点 p と線分 ab のいちばん近いきょり</summary>
        static double DistanceToSegment((int x, int y) p, (int x, int y) a, (int x, int y) b)
        {
            double ax = a.x, ay = a.y, bx = b.x, by = b.y, px = p.x, py = p.y;
            double dx = bx - ax, dy = by - ay;
            double length2 = dx * dx + dy * dy;
            double t = length2 == 0 ? 0.0 : Math.Max(0.0, Math.Min(1.0, ((px - ax) * dx + (py - ay) * dy) / length2));
            return Py.Hypot(px - (ax + t * dx), py - (ay + t * dy));
        }

        readonly (int x, int y) _center;
        readonly List<(int x, int y)> _vertices = new List<(int x, int y)>();
        public readonly double TimeLimit;
        public double TimeLeft;
        bool _drawing;     // クリックを押したまま描いている途中か
        int _reached;      // PATH の何番目の頂点まで通ったか
        List<(int x, int y)> _trail = new List<(int x, int y)>();  // なぞった跡(ドット座標)
        public bool? Result;  // null = 受付中、true = 成功、false = 失敗
        double _timer;

        public StarDrawing((int x, int y) center, int radius, double timeLimit)
        {
            _center = center;
            for (int k = 0; k < 5; k++)
            {
                double angle = 2 * Math.PI * k / 5;  // 0 = 真上、時計回り
                _vertices.Add((Py.Round(center.x + radius * Math.Sin(angle)), Py.Round(center.y - radius * Math.Cos(angle))));
            }
            TimeLimit = timeLimit;
            TimeLeft = timeLimit;
        }

        public (int x, int y) StartPoint => _vertices[PATH[0]];

        (int x, int y) Point(int step) => _vertices[PATH[step]];

        public void HandleEvent(GameEvent e)
        {
            if (Result != null)
            {
                return;
            }
            if (e.Type == GameEventType.MouseButtonDown && e.Button == 1 && !_drawing)
            {
                if (Py.Dist(e.Pos, StartPoint) <= HIT_RADIUS * 1.5)  // 1 の点の近くで押したら開始
                {
                    _drawing = true;
                    _reached = 0;
                    _trail = new List<(int x, int y)> { StartPoint };
                }
            }
            else if (e.Type == GameEventType.MouseMotion && _drawing)
            {
                MoveTo(e.Pos);
            }
            else if (e.Type == GameEventType.MouseButtonUp && e.Button == 1 && _drawing)
            {
                Result = false;  // とちゅうではなした
            }
        }

        void MoveTo((int x, int y) pos)
        {
            var prev = _trail[_trail.Count - 1];
            _trail.Add(pos);
            var target = Point(_reached + 1);
            // 速く動かしても見のがさないよう、前の位置から今の位置までの線で頂点を通ったか調べる
            if (DistanceToSegment(target, prev, pos) <= HIT_RADIUS)
            {
                _reached++;
                if (_reached == PATH.Length - 1)
                {
                    Result = true;  // 1 にもどってきた → 星の完成
                }
                return;
            }
            if (DistanceToSegment(pos, Point(_reached), target) > TOLERANCE)
            {
                Result = false;  // 線からはみ出した
            }
        }

        public void Update(double dt)
        {
            _timer += dt;
            if (Result == null)
            {
                TimeLeft = Math.Max(0.0, TimeLeft - dt);
                if (TimeLeft <= 0)
                {
                    Result = false;  // 時間切れ
                }
            }
        }

        // ---- 表示 ----
        public void Draw(Surface surface)
        {
            var v = _vertices;
            // お手本の線(まだのところは灰色、なぞれたところは金色)
            for (int step = 0; step < PATH.Length - 1; step++)
            {
                var a = Point(step);
                var b = Point(step + 1);
                bool done = step < _reached;
                QteRpg.Draw.Line(surface, done ? Settings.GLOW : Settings.GRAY, a, b, done ? 3 : 1);
            }
            // なぞった跡
            if (_trail.Count >= 2)
            {
                QteRpg.Draw.Lines(surface, Settings.WHITE, false, _trail, 2);
            }
            // 頂点と番号(次に向かう点は点滅)
            bool blink = (int)(_timer * 4) % 2 == 0;
            int nextStep = _drawing ? _reached + 1 : 0;
            for (int order = 0; order < StatusEmblem.LIGHT_ORDER.Length; order++)
            {
                int k = StatusEmblem.LIGHT_ORDER[order];
                var point = v[k];
                bool isNext = Result == null && nextStep < PATH.Length && PATH[nextStep] == k;
                bool passed = Enumerable.Range(1, Math.Max(0, _reached)).Any(s => PATH[s] == k) || (_drawing && k == PATH[0]);
                var color = passed ? Settings.GLOW : Settings.WHITE;
                if (isNext && blink)
                {
                    QteRpg.Draw.Circle(surface, Settings.GLOW, point, HIT_RADIUS, 1);
                }
                QteRpg.Draw.Circle(surface, color, point, 3);
                int dx = point.x - _center.x, dy = point.y - _center.y;
                double dist = Py.Hypot(dx, dy);
                if (dist == 0)
                {
                    dist = 1;
                }
                var labelPos = (Py.Round(point.x + dx / dist * 14), Py.Round(point.y + dy / dist * 14));
                Utils.DrawText(surface, (order + 1).ToString(), Settings.FONT_SMALL, color, Anchor.Center, labelPos);
            }
            // 説明と残り時間
            Utils.DrawText(surface, "クリックしたまま 1から5まで順になぞり、1にもどれ！", Settings.FONT_SMALL, Settings.WHITE,
                           Anchor.Center, _center.x, 18);
            var bar = PgRect.Anchored(160, 4, Anchor.MidTop, _center.x, 32);
            QteRpg.Draw.Rect(surface, Settings.GRAY, bar);
            int filled = TimeLimit != 0 ? Py.Round(bar.Width * TimeLeft / TimeLimit) : 0;
            if (filled > 0)
            {
                QteRpg.Draw.Rect(surface, Settings.WHITE, new PgRect(bar.Left, bar.Top, filled, bar.Height));
            }
        }
    }
}
