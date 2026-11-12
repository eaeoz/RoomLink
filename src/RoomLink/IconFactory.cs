using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RoomLink;

/// <summary>
/// Draws the tray icon at runtime: two crossing arrows representing the
/// two-way bridge. Green = bridging, gray = idle.
/// </summary>
internal static class IconFactory
{
    public static Icon Create(bool running)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Color.Transparent);

        using var discBrush = new SolidBrush(running
            ? Color.FromArgb(30, 132, 73)      // green disc
            : Color.FromArgb(90, 90, 90));     // gray disc
        g.FillEllipse(discBrush, 1, 1, 30, 30);

        // Arrow 1: headset mic → speakers (down-right)
        using var penA = new Pen(Color.White, 3.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        DrawArrow(g, penA, from: new Point(8, 9), to: new Point(22, 23));

        // Arrow 2: PC mic → headset (up-left), slightly offset
        using var penB = new Pen(Color.FromArgb(200, 255, 255, 255), 3.2f)
        { StartCap = LineCap.Round, EndCap = LineCap.Round, DashStyle = DashStyle.Dash };
        DrawArrow(g, penB, from: new Point(23, 9), to: new Point(10, 22));

        return Icon.FromHandle(bmp.GetHicon());
    }

    private static void DrawArrow(Graphics g, Pen pen, Point from, Point to)
    {
        g.DrawLine(pen, from, to);
        // arrowhead
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) return;
        var ux = (float)(dx / len); var uy = (float)(dy / len);
        const float s = 7f;                 // head size
        var back = new PointF(to.X - ux * s, to.Y - uy * s);
        var left = new PointF(
            back.X + (float)(-uy) * (float)(s / 2),
            back.Y + (float)(ux) * (float)(s / 2));
        var right = new PointF(
            back.X - (float)(-uy) * (float)(s / 2),
            back.Y - (float)(ux) * (float)(s / 2));
        g.DrawPolygon(pen, new[] { to, left, right });
        g.FillPolygon(pen.Brush, new[] { to, left, right });
    }
}
