using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace JamSeshun.Views;

public partial class TunerNeedle : Control
{
    // Degrees from vertical the needle sweeps at ±50 cents
    private const double SweepDeg = 55.0;

    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<TunerNeedle, double>(nameof(Angle));

    static TunerNeedle()
    {
        AffectsRender<TunerNeedle>(AngleProperty);
    }

    public TunerNeedle()
    {
        Transitions = new Transitions
        {
            new DoubleTransition
            {
                Duration = TimeSpan.FromMilliseconds(120),
                Property = AngleProperty,
                Easing = new CubicEaseOut()
            }
        };
    }

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var cx = w / 2;
        var cy = h * 0.88;
        var r = Math.Min(w * 0.44, cy * 0.92);

        // ── Colored arc zones ─────────────────────────────────
        // Zones map: ±0–20% → green, ±20–50% → orange, ±50–100% → red
        var z1 = SweepDeg * 0.20;
        var z2 = SweepDeg * 0.50;
        DrawArc(ctx, cx, cy, r, -SweepDeg, -z2, MakeArcPen(Color.Parse("#dc2626"), r));
        DrawArc(ctx, cx, cy, r, -z2, -z1,       MakeArcPen(Color.Parse("#f97316"), r));
        DrawArc(ctx, cx, cy, r, -z1, +z1,        MakeArcPen(Color.Parse("#22c55e"), r));
        DrawArc(ctx, cx, cy, r, +z1, +z2,        MakeArcPen(Color.Parse("#f97316"), r));
        DrawArc(ctx, cx, cy, r, +z2, +SweepDeg,  MakeArcPen(Color.Parse("#dc2626"), r));

        // ── Tick marks ────────────────────────────────────────
        for (var cents = -50; cents <= 50; cents += 10)
        {
            var tickDeg = cents / 50.0 * SweepDeg;
            var tickRad = DegToRad(tickDeg);
            var isCenter = cents == 0;
            var innerR = isCenter ? r * 0.72 : r * 0.83;
            var outerR = r * 0.97;
            var p1 = PolarPoint(cx, cy, innerR, tickRad);
            var p2 = PolarPoint(cx, cy, outerR, tickRad);
            var tickPen = isCenter
                ? new Pen(new SolidColorBrush(Color.Parse("#94a3b8")), 2.5, lineCap: PenLineCap.Round)
                : new Pen(new SolidColorBrush(Color.Parse("#334155")), 1.5, lineCap: PenLineCap.Round);
            ctx.DrawLine(tickPen, p1, p2);
        }

        // ── Needle ────────────────────────────────────────────
        var clampedAngle = Math.Max(-SweepDeg, Math.Min(SweepDeg, Angle));
        var needleRad = DegToRad(clampedAngle);
        var pivot = new Point(cx, cy);
        var tip = PolarPoint(cx, cy, r * 0.82, needleRad);

        // Glow
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 7,
            lineCap: PenLineCap.Round), pivot, tip);
        // (Color.FromArgb byte args are in range 0-255, cast to ensure correct overload)
        // Main needle
        ctx.DrawLine(new Pen(Brushes.White, 2, lineCap: PenLineCap.Round), pivot, tip);

        // Pivot cap
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#1e293b")), null, pivot, r * 0.09, r * 0.09);
        ctx.DrawEllipse(Brushes.White, null, pivot, r * 0.035, r * 0.035);
    }

    private static IPen MakeArcPen(Color color, double radius) =>
        new Pen(new SolidColorBrush(color), radius * 0.07, lineCap: PenLineCap.Flat);

    // Convert degrees-from-vertical to radians in screen coordinate space
    private static double DegToRad(double fromVertical) =>
        (fromVertical - 90.0) * Math.PI / 180.0;

    private static Point PolarPoint(double cx, double cy, double radius, double angleRad) =>
        new(cx + Math.Cos(angleRad) * radius, cy + Math.Sin(angleRad) * radius);

    private static void DrawArc(DrawingContext ctx, double cx, double cy, double r,
        double startDeg, double endDeg, IPen pen)
    {
        var startRad = DegToRad(startDeg);
        var endRad = DegToRad(endDeg);
        var startPt = PolarPoint(cx, cy, r, startRad);
        var endPt = PolarPoint(cx, cy, r, endRad);
        var isLarge = Math.Abs(endDeg - startDeg) > 180;

        var geo = new StreamGeometry();
        using (var geoCtx = geo.Open())
        {
            geoCtx.BeginFigure(startPt, false);
            geoCtx.ArcTo(endPt, new Size(r, r), 0, isLarge, SweepDirection.Clockwise);
        }
        ctx.DrawGeometry(null, pen, geo);
    }
}
