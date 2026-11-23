// pygame.draw のかわり: 四角・線・円・楕円・多角形を描く
//
// pygame 2.6 の draw.c と同じ手順でドットを置くので、Pygame 版と同じ形になる。
// pygame と同じく、色は「混ぜずにそのまま置く」(透明度つきの色なら、その透明度のまま置く)。
using System;
using System.Collections.Generic;

namespace QteRpg
{
    public static class Draw
    {
        // ================= 公開する関数(pygame.draw.xxx と同じ引数) =================

        /// <summary>pygame.draw.rect(surface, color, rect, width=0, border_radius=0)</summary>
        public static void Rect(Surface surf, Rgba color, PgRect rect, int width = 0, int borderRadius = 0)
        {
            if (width < 0)
            {
                return;
            }
            if (borderRadius <= 0 || Math.Abs(rect.W) < 2 || Math.Abs(rect.H) < 2)
            {
                int cx0 = Math.Max(rect.X, 0), cy0 = Math.Max(rect.Y, 0);
                int cx1 = Math.Min(rect.X + rect.W, surf.Width), cy1 = Math.Min(rect.Y + rect.H, surf.Height);
                if (cx1 <= cx0 || cy1 <= cy0)
                {
                    return;
                }
                int clippedW = cx1 - cx0, clippedH = cy1 - cy0;
                if (width > 0 && width * 2 < clippedW && width * 2 < clippedH)
                {
                    DrawRectOutline(surf, rect.X, rect.Y, rect.X + rect.W - 1, rect.Y + rect.H - 1, width, color);
                }
                else
                {
                    surf.FillRect(new PgRect(cx0, cy0, clippedW, clippedH), color);
                }
                return;
            }
            int x = rect.X, y = rect.Y, w = rect.W, h = rect.H;
            if (w < 0)
            {
                x += w;
                w = -w;
            }
            if (h < 0)
            {
                y += h;
                h = -h;
            }
            if (width > w / 2 || width > h / 2)
            {
                width = Math.Max(w / 2, h / 2);
            }
            DrawRoundRect(surf, x, y, x + w - 1, y + h - 1, borderRadius, width, color, -1, -1, -1, -1);
        }

        /// <summary>pygame.draw.line(surface, color, start, end, width=1)</summary>
        public static void Line(Surface surf, Rgba color, (int x, int y) start, (int x, int y) end, int width = 1)
        {
            if (width < 1)
            {
                return;
            }
            DrawLineWidth(surf, color, start.x, start.y, end.x, end.y, width);
        }

        /// <summary>pygame.draw.lines(surface, color, closed, points, width=1)</summary>
        public static void Lines(Surface surf, Rgba color, bool closed, IList<(int x, int y)> points, int width = 1)
        {
            if (points.Count < 2)
            {
                throw new ArgumentException("points argument must contain 2 or more points");
            }
            if (width < 1)
            {
                return;
            }
            for (int i = 1; i < points.Count; i++)
            {
                DrawLineWidth(surf, color, points[i - 1].x, points[i - 1].y, points[i].x, points[i].y, width);
            }
            if (closed && points.Count > 2)
            {
                var last = points[points.Count - 1];
                DrawLineWidth(surf, color, last.x, last.y, points[0].x, points[0].y, width);
            }
        }

        /// <summary>pygame.draw.circle(surface, color, center, radius, width=0)</summary>
        public static void Circle(Surface surf, Rgba color, (int x, int y) center, int radius, int width = 0)
        {
            if (radius < 1 || width < 0)
            {
                return;
            }
            if (width > radius)
            {
                width = radius;
            }
            int px = center.x, py = center.y;
            if (px > surf.Width + radius || px < -radius || py > surf.Height + radius || py < -radius)
            {
                return;
            }
            if (width == 0 || width == radius)
            {
                DrawCircleFilled(surf, px, py, radius, color);
            }
            else if (width == 1)
            {
                DrawCircleBresenhamThin(surf, px, py, radius, color);
            }
            else
            {
                DrawCircleBresenham(surf, px, py, radius, width, color);
            }
        }

        /// <summary>pygame.draw.ellipse(surface, color, rect, width=0) (ぬりつぶしのみ)</summary>
        public static void Ellipse(Surface surf, Rgba color, PgRect rect, int width = 0)
        {
            if (width < 0)
            {
                return;
            }
            if (width == 0 || width >= Math.Min(rect.W / 2 + rect.W % 2, rect.H / 2 + rect.H % 2))
            {
                DrawEllipseFilled(surf, rect.X, rect.Y, rect.W, rect.H, color);
            }
            else
            {
                throw new NotImplementedException("ふちだけの楕円(width > 0)はまだ移植していません");
            }
        }

        /// <summary>pygame.draw.polygon(surface, color, points, width=0)</summary>
        public static void Polygon(Surface surf, Rgba color, IList<(int x, int y)> points, int width = 0)
        {
            if (width != 0)
            {
                Lines(surf, color, true, points, width);
                return;
            }
            if (points.Count < 3)
            {
                throw new ArgumentException("points argument must contain more than 2 points");
            }
            var xs = new int[points.Count];
            var ys = new int[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                xs[i] = points[i].x;
                ys[i] = points[i].y;
            }
            DrawFillPoly(surf, xs, ys, points.Count, color);
        }

        // ================= ここから下は pygame の draw.c をそのまま移したもの =================

        static int Sign(int x, int y) => x > 0 ? 1 : (x < 0 ? -1 : y);

        static bool InsideClip(Surface surf, int x, int y) => x >= 0 && x < surf.Width && y >= 0 && y < surf.Height;

        static void SetAt(Surface surf, int x, int y, Rgba color) => surf.SetAt(x, y, color);

        static void DrawHorzLine(Surface surf, Rgba color, int x1, int y1, int x2)
        {
            color = surf.Store(color);
            int row = y1 * surf.Width;
            for (int x = x1; x <= x2; x++)
            {
                surf.Pixels[row + x] = color;
            }
        }

        static void DrawHorzLineClip(Surface surf, Rgba color, int x1, int y1, int x2)
        {
            if (y1 < 0 || y1 >= surf.Height)
            {
                return;
            }
            if (x2 < x1)
            {
                int temp = x1;
                x1 = x2;
                x2 = temp;
            }
            x1 = Math.Max(x1, 0);
            x2 = Math.Min(x2, surf.Width - 1);
            if (x2 < 0 || x1 >= surf.Width)
            {
                return;
            }
            if (x1 == x2)
            {
                SetAt(surf, x1, y1, color);
                return;
            }
            DrawHorzLine(surf, color, x1, y1, x2);
        }

        static bool ClipLine(Surface surf, ref int x1, ref int y1, ref int x2, ref int y2)
        {
            int p1 = x1 - x2;
            int p2 = -p1;
            int p3 = y1 - y2;
            int p4 = -p3;
            int q1 = x1;
            int q2 = surf.Width - x1;
            int q3 = y1;
            int q4 = surf.Height - y1;
            int oldX1 = x1;
            int oldY1 = y1;
            double nmax = 0;
            double pmin = 1;
            double r1, r2;
            if ((p1 == 0 && q1 < 0) || (p2 == 0 && q2 < 0) || (p3 == 0 && q3 < 0) || (p4 == 0 && q4 < 0))
            {
                return false;
            }
            if (p1 != 0)
            {
                r1 = (double)q1 / p1;
                r2 = (double)q2 / p2;
                if (p1 < 0)
                {
                    if (r1 > nmax) nmax = r1;
                    if (r2 < pmin) pmin = r2;
                }
                else
                {
                    if (r2 > nmax) nmax = r2;
                    if (r1 < pmin) pmin = r1;
                }
            }
            if (p3 != 0)
            {
                r1 = (double)q3 / p3;
                r2 = (double)q4 / p4;
                if (p3 < 0)
                {
                    if (r1 > nmax) nmax = r1;
                    if (r2 < pmin) pmin = r2;
                }
                else
                {
                    if (r2 > nmax) nmax = r2;
                    if (r1 < pmin) pmin = r1;
                }
            }
            if (nmax > pmin)
            {
                return false;
            }
            x1 = oldX1 + (int)(p2 * nmax < 0 ? (p2 * nmax - 0.5) : (p2 * nmax + 0.5));
            y1 = oldY1 + (int)(p4 * nmax < 0 ? (p4 * nmax - 0.5) : (p4 * nmax + 0.5));
            x2 = oldX1 + (int)(p2 * pmin < 0 ? (p2 * pmin - 0.5) : (p2 * pmin + 0.5));
            y2 = oldY1 + (int)(p4 * pmin < 0 ? (p4 * pmin - 0.5) : (p4 * pmin + 0.5));
            return true;
        }

        static void DrawLineWidth(Surface surf, Rgba color, int x1, int y1, int x2, int y2, int width)
        {
            int dx, dy, err, e2, sx, sy, y;
            int leftTop, rightBottom;
            int endX = x2;
            int endY = y2;
            bool xinc = false;
            if (Math.Abs(x1 - x2) <= Math.Abs(y1 - y2))
            {
                xinc = true;
            }
            dx = Math.Abs(x2 - x1);
            sx = x1 < x2 ? 1 : -1;
            dy = Math.Abs(y2 - y1);
            sy = y1 < y2 ? 1 : -1;
            err = (dx > dy ? dx : -dy) / 2;
            if (!ClipLine(surf, ref x1, ref y1, ref x2, ref y2))
            {
                return;
            }
            if (width == 1)
            {
                DrawLine(surf, x1, y1, x2, y2, color);
                return;
            }
            if (xinc)
            {
                leftTop = x1 - (width - 1) / 2;
                rightBottom = x1 + width / 2;
            }
            else
            {
                leftTop = y1 - (width - 1) / 2;
                rightBottom = y1 + width / 2;
            }
            while ((Sign(x1 - x2, sx) != sx) || (Sign(y1 - y2, sy) != sy))
            {
                if (xinc)
                {
                    DrawHorzLineClip(surf, color, leftTop, y1, rightBottom);
                }
                else
                {
                    for (y = leftTop; y <= rightBottom; y++)
                    {
                        SetAt(surf, x1, y, color);
                    }
                }
                e2 = err;
                if (e2 > -dx)
                {
                    err -= dy;
                    x1 += sx;
                    if (xinc)
                    {
                        leftTop += sx;
                        rightBottom += sx;
                    }
                }
                if (e2 < dy)
                {
                    err += dx;
                    y1 += sy;
                    if (!xinc)
                    {
                        leftTop += sy;
                        rightBottom += sy;
                    }
                }
            }
            if (xinc)
            {
                while (y1 != endY && (InsideClip(surf, leftTop, y1) || InsideClip(surf, rightBottom, y1)))
                {
                    DrawHorzLineClip(surf, color, leftTop, y1, rightBottom);
                    e2 = err;
                    if (e2 > -dx)
                    {
                        err -= dy;
                        x1 += sx;
                        leftTop += sx;
                        rightBottom += sx;
                    }
                    if (e2 < dy)
                    {
                        err += dx;
                        y1 += sy;
                    }
                }
                DrawHorzLineClip(surf, color, leftTop, y1, rightBottom);
            }
            else
            {
                while (x1 != endX && (InsideClip(surf, x1, leftTop) || InsideClip(surf, x1, rightBottom)))
                {
                    for (y = leftTop; y <= rightBottom; y++)
                    {
                        SetAt(surf, x1, y, color);
                    }
                    e2 = err;
                    if (e2 > -dx)
                    {
                        err -= dy;
                        x1 += sx;
                    }
                    if (e2 < dy)
                    {
                        err += dx;
                        y1 += sy;
                        leftTop += sy;
                        rightBottom += sy;
                    }
                }
                for (y = leftTop; y <= rightBottom; y++)
                {
                    SetAt(surf, x1, y, color);
                }
            }
        }

        static void DrawLine(Surface surf, int x1, int y1, int x2, int y2, Rgba color)
        {
            int dx, dy, err, e2, sx, sy;
            if (x1 == x2 && y1 == y2)
            {
                SetAt(surf, x1, y1, color);
                return;
            }
            if (y1 == y2)
            {
                dx = (x1 < x2) ? 1 : -1;
                for (sx = 0; sx <= Math.Abs(x1 - x2); sx++)
                {
                    SetAt(surf, x1 + dx * sx, y1, color);
                }
                return;
            }
            if (x1 == x2)
            {
                dy = (y1 < y2) ? 1 : -1;
                for (sy = 0; sy <= Math.Abs(y1 - y2); sy++)
                {
                    SetAt(surf, x1, y1 + dy * sy, color);
                }
                return;
            }
            dx = Math.Abs(x2 - x1);
            sx = x1 < x2 ? 1 : -1;
            dy = Math.Abs(y2 - y1);
            sy = y1 < y2 ? 1 : -1;
            err = (dx > dy ? dx : -dy) / 2;
            while (x1 != x2 || y1 != y2)
            {
                SetAt(surf, x1, y1, color);
                e2 = err;
                if (e2 > -dx)
                {
                    err -= dy;
                    x1 += sx;
                }
                if (e2 < dy)
                {
                    err += dx;
                    y1 += sy;
                }
            }
            SetAt(surf, x2, y2, color);
        }

        static void DrawCircleBresenham(Surface surf, int x0, int y0, int radius, int thickness, Rgba color)
        {
            long x = 0;
            long y = radius;
            long radiusSquared = radius * radius;
            long doubleRadiusSquared = 2 * radiusSquared;
            double d1 = radiusSquared * (1.25 - radius);
            long dx = 0;
            long dy = doubleRadiusSquared * y;

            bool line = true;
            long radiusInner = radius - thickness + 1;
            long xInner = 0;
            long yInner = radiusInner;
            long radiusInnerSquared = radiusInner * radiusInner;
            long doubleRadiusInnerSquared = 2 * radiusInnerSquared;
            double d1Inner = radiusInnerSquared * (1.25 - radiusInner);
            double d2Inner = 0;
            long dxInner = 0;
            long dyInner = doubleRadiusInnerSquared * yInner;

            while (dx < dy)
            {
                while (d1 < 0)
                {
                    x++;
                    dx += doubleRadiusSquared;
                    d1 += dx + radiusSquared;
                }
                if (line)
                {
                    DrawHorzLineClip(surf, color, x0 - (int)x, y0 - (int)y, x0 + (int)x - 1);
                    DrawHorzLineClip(surf, color, x0 - (int)x, y0 + (int)y - 1, x0 + (int)x - 1);
                }
                else
                {
                    DrawHorzLineClip(surf, color, x0 - (int)x, y0 - (int)y, x0 - (int)xInner);
                    DrawHorzLineClip(surf, color, x0 - (int)x, y0 + (int)y - 1, x0 - (int)xInner);
                    DrawHorzLineClip(surf, color, x0 + (int)xInner - 1, y0 - (int)y, x0 + (int)x - 1);
                    DrawHorzLineClip(surf, color, x0 + (int)xInner - 1, y0 + (int)y - 1, x0 + (int)x - 1);
                }
                x++;
                y--;
                dx += doubleRadiusSquared;
                dy -= doubleRadiusSquared;
                d1 += dx - dy + radiusSquared;
                if (line && y < radiusInner)
                {
                    line = false;
                }
                if (!line)
                {
                    while (d1Inner < 0)
                    {
                        xInner += 1;
                        dxInner += doubleRadiusInnerSquared;
                        d1Inner += dxInner + radiusInnerSquared;
                    }
                    xInner++;
                    yInner--;
                    dxInner += doubleRadiusInnerSquared;
                    dyInner -= doubleRadiusInnerSquared;
                    d1Inner += dxInner - dyInner + radiusInnerSquared;
                }
            }
            d1 = radiusSquared * ((x + 0.5) * (x + 0.5) + (y - 1) * (y - 1) - radiusSquared);
            while (y >= 0)
            {
                if (line)
                {
                    DrawHorzLineClip(surf, color, x0 - (int)x, y0 - (int)y, x0 + (int)x - 1);
                    DrawHorzLineClip(surf, color, x0 - (int)x, y0 + (int)y - 1, x0 + (int)x - 1);
                }
                else
                {
                    DrawHorzLineClip(surf, color, x0 - (int)x, y0 - (int)y, x0 - (int)xInner);
                    DrawHorzLineClip(surf, color, x0 - (int)x, y0 + (int)y - 1, x0 - (int)xInner);
                    DrawHorzLineClip(surf, color, x0 + (int)xInner - 1, y0 - (int)y, x0 + (int)x - 1);
                    DrawHorzLineClip(surf, color, x0 + (int)xInner - 1, y0 + (int)y - 1, x0 + (int)x - 1);
                }
                if (d1 > 0)
                {
                    y--;
                    dy -= doubleRadiusSquared;
                    d1 += radiusSquared - dy;
                }
                else
                {
                    y--;
                    x++;
                    dx += doubleRadiusSquared;
                    dy -= doubleRadiusSquared;
                    d1 += dx - dy + radiusSquared;
                }
                if (line && y < radiusInner)
                {
                    line = false;
                }
                if (!line)
                {
                    if (dxInner < dyInner)
                    {
                        while (d1Inner < 0)
                        {
                            xInner += 1;
                            dxInner += doubleRadiusInnerSquared;
                            d1Inner += dxInner + radiusInnerSquared;
                        }
                        xInner++;
                        yInner--;
                        dxInner += doubleRadiusInnerSquared;
                        dyInner -= doubleRadiusInnerSquared;
                        d1Inner += dxInner - dyInner + radiusInnerSquared;
                    }
                    else
                    {
                        if (d2Inner == 0)
                        {
                            d2Inner = radiusInnerSquared * ((xInner + 0.5) * (xInner + 0.5) +
                                                            (yInner - 1) * (yInner - 1) - radiusInnerSquared);
                        }
                        if (d2Inner > 0)
                        {
                            yInner--;
                            dyInner -= doubleRadiusInnerSquared;
                            d2Inner += radiusInnerSquared - dyInner;
                        }
                        else
                        {
                            yInner--;
                            xInner++;
                            dxInner += doubleRadiusInnerSquared;
                            dyInner -= doubleRadiusInnerSquared;
                            d2Inner += dxInner - dyInner + radiusInnerSquared;
                        }
                    }
                }
            }
        }

        static void DrawCircleBresenhamThin(Surface surf, int x0, int y0, int radius, Rgba color)
        {
            int f = 1 - radius;
            int ddFx = 0;
            int ddFy = -2 * radius;
            int x = 0;
            int y = radius;
            while (x < y)
            {
                if (f >= 0)
                {
                    y--;
                    ddFy += 2;
                    f += ddFy;
                }
                x++;
                ddFx += 2;
                f += ddFx + 1;
                SetAt(surf, x0 + x - 1, y0 + y - 1, color);
                SetAt(surf, x0 - x, y0 + y - 1, color);
                SetAt(surf, x0 + x - 1, y0 - y, color);
                SetAt(surf, x0 - x, y0 - y, color);
                SetAt(surf, x0 + y - 1, y0 + x - 1, color);
                SetAt(surf, x0 + y - 1, y0 - x, color);
                SetAt(surf, x0 - y, y0 + x - 1, color);
                SetAt(surf, x0 - y, y0 - x, color);
            }
        }

        static void DrawCircleQuadrant(Surface surf, int x0, int y0, int radius, int thickness, Rgba color,
                                       bool topRight, bool topLeft, bool bottomLeft, bool bottomRight)
        {
            int f = 1 - radius;
            int ddFx = 0;
            int ddFy = -2 * radius;
            int x = 0;
            int y = radius;
            int y1;
            int iY = radius - thickness;
            int iF = 1 - iY;
            int iDdFx = 0;
            int iDdFy = -2 * iY;
            int i;
            if (radius == 1)
            {
                if (topRight) SetAt(surf, x0, y0 - 1, color);
                if (topLeft) SetAt(surf, x0 - 1, y0 - 1, color);
                if (bottomLeft) SetAt(surf, x0 - 1, y0, color);
                if (bottomRight) SetAt(surf, x0, y0, color);
                return;
            }

            if (thickness != 0)
            {
                while (x < y)
                {
                    if (f >= 0)
                    {
                        y--;
                        ddFy += 2;
                        f += ddFy;
                    }
                    if (iF >= 0)
                    {
                        iY--;
                        iDdFy += 2;
                        iF += iDdFy;
                    }
                    x++;
                    ddFx += 2;
                    f += ddFx + 1;

                    iDdFx += 2;
                    iF += iDdFx + 1;

                    if (thickness > 1)
                    {
                        thickness = y - iY;
                    }

                    if (topRight)
                    {
                        for (i = 0; i < thickness; i++)
                        {
                            y1 = y - i;
                            if ((y0 - y1) < (y0 - x)) SetAt(surf, x0 + x - 1, y0 - y1, color);
                            if ((x0 + y1 - 1) >= (x0 + x - 1)) SetAt(surf, x0 + y1 - 1, y0 - x, color);
                        }
                    }
                    if (topLeft)
                    {
                        for (i = 0; i < thickness; i++)
                        {
                            y1 = y - i;
                            if ((y0 - y1) <= (y0 - x)) SetAt(surf, x0 - x, y0 - y1, color);
                            if ((x0 - y1) < (x0 - x)) SetAt(surf, x0 - y1, y0 - x, color);
                        }
                    }
                    if (bottomLeft)
                    {
                        for (i = 0; i < thickness; i++)
                        {
                            y1 = y - i;
                            if ((x0 - y1) <= (x0 - x)) SetAt(surf, x0 - y1, y0 + x - 1, color);
                            if ((y0 + y1 - 1) > (y0 + x - 1)) SetAt(surf, x0 - x, y0 + y1 - 1, color);
                        }
                    }
                    if (bottomRight)
                    {
                        for (i = 0; i < thickness; i++)
                        {
                            y1 = y - i;
                            if ((y0 + y1 - 1) >= (y0 + x - 1)) SetAt(surf, x0 + x - 1, y0 + y1 - 1, color);
                            if ((x0 + y1 - 1) > (x0 + x - 1)) SetAt(surf, x0 + y1 - 1, y0 + x - 1, color);
                        }
                    }
                }
            }
            else
            {
                while (x < y)
                {
                    if (f >= 0)
                    {
                        y--;
                        ddFy += 2;
                        f += ddFy;
                    }
                    x++;
                    ddFx += 2;
                    f += ddFx + 1;
                    if (topRight)
                    {
                        for (y1 = y0 - x; y1 <= y0; y1++) SetAt(surf, x0 + y - 1, y1, color);
                        for (y1 = y0 - y; y1 <= y0; y1++) SetAt(surf, x0 + x - 1, y1, color);
                    }
                    if (topLeft)
                    {
                        for (y1 = y0 - x; y1 <= y0; y1++) SetAt(surf, x0 - y, y1, color);
                        for (y1 = y0 - y; y1 <= y0; y1++) SetAt(surf, x0 - x, y1, color);
                    }
                    if (bottomLeft)
                    {
                        for (y1 = y0; y1 < y0 + x; y1++) SetAt(surf, x0 - y, y1, color);
                        for (y1 = y0; y1 < y0 + y; y1++) SetAt(surf, x0 - x, y1, color);
                    }
                    if (bottomRight)
                    {
                        for (y1 = y0; y1 < y0 + x; y1++) SetAt(surf, x0 + y - 1, y1, color);
                        for (y1 = y0; y1 < y0 + y; y1++) SetAt(surf, x0 + x - 1, y1, color);
                    }
                }
            }
        }

        static void DrawCircleFilled(Surface surf, int x0, int y0, int radius, Rgba color)
        {
            int f = 1 - radius;
            int ddFx = 0;
            int ddFy = -2 * radius;
            int x = 0;
            int y = radius;
            while (x < y)
            {
                if (f >= 0)
                {
                    y--;
                    ddFy += 2;
                    f += ddFy;
                }
                x++;
                ddFx += 2;
                f += ddFx + 1;
                if (f >= 0)
                {
                    DrawHorzLineClip(surf, color, x0 - x, y0 + y - 1, x0 + x - 1);
                    DrawHorzLineClip(surf, color, x0 - x, y0 - y, x0 + x - 1);
                }
                DrawHorzLineClip(surf, color, x0 - y, y0 + x - 1, x0 + y - 1);
                DrawHorzLineClip(surf, color, x0 - y, y0 - x, x0 + y - 1);
            }
        }

        static void DrawEllipseFilled(Surface surf, int x0, int y0, int width, int height, Rgba color)
        {
            long dx, dy, x, y;
            int xOffset, yOffset;
            double d1, d2;
            if (width == 1)
            {
                DrawLine(surf, x0, y0, x0, y0 + height - 1, color);
                return;
            }
            if (height == 1)
            {
                DrawHorzLineClip(surf, color, x0, y0, x0 + width - 1);
                return;
            }
            x0 = x0 + width / 2;
            y0 = y0 + height / 2;
            xOffset = (width + 1) % 2;
            yOffset = (height + 1) % 2;
            width = width / 2;
            height = height / 2;
            x = 0;
            y = height;
            d1 = (height * height) - (width * width * height) + (0.25 * width * width);
            dx = 2 * height * height * x;
            dy = 2 * width * width * y;
            while (dx < dy)
            {
                DrawHorzLineClip(surf, color, x0 - (int)x, y0 - (int)y, x0 + (int)x - xOffset);
                DrawHorzLineClip(surf, color, x0 - (int)x, y0 + (int)y - yOffset, x0 + (int)x - xOffset);
                if (d1 < 0)
                {
                    x++;
                    dx = dx + (2 * height * height);
                    d1 = d1 + dx + (height * height);
                }
                else
                {
                    x++;
                    y--;
                    dx = dx + (2 * height * height);
                    dy = dy - (2 * width * width);
                    d1 = d1 + dx - dy + (height * height);
                }
            }
            d2 = (((double)height * height) * ((x + 0.5) * (x + 0.5))) +
                 (((double)width * width) * ((y - 1) * (y - 1))) -
                 ((double)width * width * height * height);
            while (y >= 0)
            {
                DrawHorzLineClip(surf, color, x0 - (int)x, y0 - (int)y, x0 + (int)x - xOffset);
                DrawHorzLineClip(surf, color, x0 - (int)x, y0 + (int)y - yOffset, x0 + (int)x - xOffset);
                if (d2 > 0)
                {
                    y--;
                    dy = dy - (2 * width * width);
                    d2 = d2 + (width * width) - dy;
                }
                else
                {
                    y--;
                    x++;
                    dx = dx + (2 * height * height);
                    dy = dy - (2 * width * width);
                    d2 = d2 + dx - dy + (width * width);
                }
            }
        }

        static void DrawFillPoly(Surface surf, int[] pointX, int[] pointY, int numPoints, Rgba color)
        {
            int i, iPrevious;
            double y, miny, maxy;
            double x1, y1, x2, y2;
            var xIntersect = new int[numPoints];

            miny = pointY[0];
            maxy = pointY[0];
            for (i = 1; i < numPoints; i++)
            {
                miny = Math.Min(miny, pointY[i]);
                maxy = Math.Max(maxy, pointY[i]);
            }

            if (miny == maxy)
            {
                int minx = pointX[0];
                int maxx = pointX[0];
                for (i = 1; i < numPoints; i++)
                {
                    minx = Math.Min(minx, pointX[i]);
                    maxx = Math.Max(maxx, pointX[i]);
                }
                DrawHorzLineClip(surf, color, minx, (int)miny, maxx);
                return;
            }

            for (y = miny; y <= maxy; y++)
            {
                int nIntersections = 0;
                for (i = 0; i < numPoints; i++)
                {
                    iPrevious = i != 0 ? i - 1 : numPoints - 1;
                    y1 = pointY[iPrevious];
                    y2 = pointY[i];
                    if (y1 < y2)
                    {
                        x1 = pointX[iPrevious];
                        x2 = pointX[i];
                    }
                    else if (y1 > y2)
                    {
                        y2 = pointY[iPrevious];
                        y1 = pointY[i];
                        x2 = pointX[iPrevious];
                        x1 = pointX[i];
                    }
                    else
                    {
                        continue;
                    }
                    if ((y >= y1 && y < y2) || (y == maxy && y2 == maxy))
                    {
                        xIntersect[nIntersections++] = (int)((y - y1) * (x2 - x1) / (y2 - y1) + x1);
                    }
                }
                Array.Sort(xIntersect, 0, nIntersections);
                for (i = 0; i + 1 < nIntersections; i += 2)
                {
                    DrawHorzLineClip(surf, color, xIntersect[i], (int)y, xIntersect[i + 1]);
                }
            }

            for (i = 0; i < numPoints; i++)
            {
                iPrevious = i != 0 ? i - 1 : numPoints - 1;
                y = pointY[i];
                if (miny < y && pointY[iPrevious] == y && y < maxy)
                {
                    DrawHorzLineClip(surf, color, pointX[i], (int)y, pointX[iPrevious]);
                }
            }
        }

        static void DrawRectOutline(Surface surf, int x1, int y1, int x2, int y2, int width, Rgba color)
        {
            int i;
            for (i = 0; i < width; i++)
            {
                DrawHorzLineClip(surf, color, x1, y1 + i, x2);
                DrawHorzLineClip(surf, color, x1, y2 - i, x2);
            }
            for (i = 0; i < (y2 - y1) - 2 * width + 1; i++)
            {
                DrawHorzLineClip(surf, color, x1, y1 + width + i, x1 + width - 1);
                DrawHorzLineClip(surf, color, x2 - width + 1, y1 + width + i, x2);
            }
        }

        static void DrawRoundRect(Surface surf, int x1, int y1, int x2, int y2, int radius, int width, Rgba color,
                                  int topLeft, int topRight, int bottomLeft, int bottomRight)
        {
            int i;
            if (topLeft < 0) topLeft = radius;
            if (topRight < 0) topRight = radius;
            if (bottomLeft < 0) bottomLeft = radius;
            if (bottomRight < 0) bottomRight = radius;
            if ((topLeft + topRight) > (x2 - x1 + 1) || (bottomLeft + bottomRight) > (x2 - x1 + 1) ||
                (topLeft + bottomLeft) > (y2 - y1 + 1) || (topRight + bottomRight) > (y2 - y1 + 1))
            {
                float qTop = (x2 - x1 + 1) / (float)(topLeft + topRight);
                float qLeft = (y2 - y1 + 1) / (float)(topLeft + bottomLeft);
                float qBottom = (x2 - x1 + 1) / (float)(bottomLeft + bottomRight);
                float qRight = (y2 - y1 + 1) / (float)(topRight + bottomRight);
                float f = Math.Min(Math.Min(Math.Min(qTop, qLeft), qBottom), qRight);
                topLeft = (int)(topLeft * f);
                topRight = (int)(topRight * f);
                bottomLeft = (int)(bottomLeft * f);
                bottomRight = (int)(bottomRight * f);
            }
            if (width == 0)
            {
                var px = new int[] { x1, x1 + topLeft, x2 - topRight, x2, x2, x2 - bottomRight, x1 + bottomLeft, x1 };
                var py = new int[] { y1 + topLeft, y1, y1, y1 + topRight, y2 - bottomRight, y2, y2, y2 - bottomLeft };
                DrawFillPoly(surf, px, py, 8, color);
                DrawCircleQuadrant(surf, x2 - topRight + 1, y1 + topRight, topRight, 0, color, true, false, false, false);
                DrawCircleQuadrant(surf, x1 + topLeft, y1 + topLeft, topLeft, 0, color, false, true, false, false);
                DrawCircleQuadrant(surf, x1 + bottomLeft, y2 - bottomLeft + 1, bottomLeft, 0, color, false, false, true, false);
                DrawCircleQuadrant(surf, x2 - bottomRight + 1, y2 - bottomRight + 1, bottomRight, 0, color, false, false, false, true);
            }
            else
            {
                if (x2 - topRight == x1 + topLeft)
                {
                    for (i = 0; i < width; i++) SetAt(surf, x1 + topLeft, y1 + i, color);
                }
                else
                {
                    DrawLineWidth(surf, color, x1 + topLeft, y1 + width / 2 - 1 + width % 2,
                                  x2 - topRight, y1 + width / 2 - 1 + width % 2, width);
                }
                if (y2 - bottomLeft == y1 + topLeft)
                {
                    for (i = 0; i < width; i++) SetAt(surf, x1 + i, y1 + topLeft, color);
                }
                else
                {
                    DrawLineWidth(surf, color, x1 + width / 2 - 1 + width % 2, y1 + topLeft,
                                  x1 + width / 2 - 1 + width % 2, y2 - bottomLeft, width);
                }
                if (x2 - bottomRight == x1 + bottomLeft)
                {
                    for (i = 0; i < width; i++) SetAt(surf, x1 + bottomLeft, y2 - i, color);
                }
                else
                {
                    DrawLineWidth(surf, color, x1 + bottomLeft, y2 - width / 2, x2 - bottomRight, y2 - width / 2, width);
                }
                if (y2 - bottomRight == y1 + topRight)
                {
                    for (i = 0; i < width; i++) SetAt(surf, x2 - i, y1 + topRight, color);
                }
                else
                {
                    DrawLineWidth(surf, color, x2 - width / 2, y1 + topRight, x2 - width / 2, y2 - bottomRight, width);
                }
                DrawCircleQuadrant(surf, x2 - topRight + 1, y1 + topRight, topRight, width, color, true, false, false, false);
                DrawCircleQuadrant(surf, x1 + topLeft, y1 + topLeft, topLeft, width, color, false, true, false, false);
                DrawCircleQuadrant(surf, x1 + bottomLeft, y2 - bottomLeft + 1, bottomLeft, width, color, false, false, true, false);
                DrawCircleQuadrant(surf, x2 - bottomRight + 1, y2 - bottomRight + 1, bottomRight, width, color, false, false, false, true);
            }
        }
    }
}
