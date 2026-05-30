using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;

namespace JamSeshun.Views;

/// <summary>
/// Renders a guitar chord fretboard diagram from a 6-element frets array
/// (E A D G B e order: -1 = muted, 0 = open, 1+ = fret number).
/// </summary>
public class ChordDiagramControl : Control
{
    public static readonly StyledProperty<int[]?> FretsProperty =
        AvaloniaProperty.Register<ChordDiagramControl, int[]?>(nameof(Frets));

    public int[]? Frets
    {
        get => GetValue(FretsProperty);
        set => SetValue(FretsProperty, value);
    }

    static ChordDiagramControl()
    {
        FretsProperty.Changed.AddClassHandler<ChordDiagramControl>((c, _) => c.InvalidateVisual());
        AffectsRender<ChordDiagramControl>(FretsProperty);
    }

    // Layout constants
    private const int StringCount  = 6;
    private const int FretCount    = 4;
    private const int StringGap    = 11;   // px between strings
    private const int FretGap      = 14;   // px between frets
    private const int TopPad       = 18;   // room for X/O markers
    private const int LeftPad      = 10;
    private const int RightPad     = 24;
    private const int BottomPad    = 6;
    private const int DotRadius    = 4;

    private static readonly int DiagramWidth  = LeftPad + (StringCount - 1) * StringGap + RightPad;
    private static readonly int DiagramHeight = TopPad + FretCount * FretGap + BottomPad;

    protected override Size MeasureOverride(Size _) =>
        new(DiagramWidth, DiagramHeight);

    public override void Render(DrawingContext ctx)
    {
        var frets = Frets;
        if (frets == null || frets.Length != StringCount) return;

        // ── Determine which frets to display ─────────────────────────────────
        var frettedValues = frets.Where(f => f > 0).ToArray();
        int startFret = 1;
        if (frettedValues.Length > 0)
        {
            var minFret = frettedValues.Min();
            var maxFret = frettedValues.Max();
            if (maxFret > FretCount)
                startFret = minFret;
        }

        // ── Pens and brushes ──────────────────────────────────────────────────
        var gridBrush   = new SolidColorBrush(Color.Parse("#475569"));
        var nutBrush    = new SolidColorBrush(Color.Parse("#94a3b8"));
        var dotBrush    = new SolidColorBrush(Color.Parse("#7fc0f4"));
        var openBrush   = new SolidColorBrush(Colors.Transparent);
        var openPen     = new Pen(new SolidColorBrush(Color.Parse("#4ade80")), 1.5);
        var mutedBrush  = new SolidColorBrush(Color.Parse("#ef4444"));
        var gridPen     = new Pen(gridBrush, 1);
        var nutPen      = new Pen(nutBrush,  startFret == 1 ? 3 : 1);
        var labelBrush  = new SolidColorBrush(Color.Parse("#94a3b8"));

        double nutY = TopPad;

        // ── Nut / position line ───────────────────────────────────────────────
        ctx.DrawLine(nutPen,
            new Point(LeftPad, nutY),
            new Point(LeftPad + (StringCount - 1) * StringGap, nutY));

        // ── Fret lines ────────────────────────────────────────────────────────
        for (int f = 1; f <= FretCount; f++)
        {
            double y = nutY + f * FretGap;
            ctx.DrawLine(gridPen,
                new Point(LeftPad, y),
                new Point(LeftPad + (StringCount - 1) * StringGap, y));
        }

        // ── String lines ──────────────────────────────────────────────────────
        for (int s = 0; s < StringCount; s++)
        {
            double x = LeftPad + s * StringGap;
            ctx.DrawLine(gridPen,
                new Point(x, nutY),
                new Point(x, nutY + FretCount * FretGap));
        }

        // ── Position label (e.g. "5fr") ───────────────────────────────────────
        if (startFret > 1)
        {
            var ft = new FormattedText(
                $"{startFret}fr",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("sans-serif"),
                9,
                labelBrush);
            ctx.DrawText(ft, new Point(LeftPad + (StringCount - 1) * StringGap + 4, nutY + FretGap / 2.0 - ft.Height / 2));
        }

        // ── Per-string markers ────────────────────────────────────────────────
        for (int s = 0; s < StringCount; s++)
        {
            double x = LeftPad + s * StringGap;
            int    fret = frets[s];

            if (fret == -1)
            {
                // Muted: draw X above nut
                var ft = new FormattedText("✕",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("sans-serif"), 10, mutedBrush);
                ctx.DrawText(ft, new Point(x - ft.Width / 2, nutY - TopPad / 2 - ft.Height / 2));
            }
            else if (fret == 0)
            {
                // Open: draw hollow circle above nut
                ctx.DrawEllipse(openBrush, openPen, new Point(x, nutY - 7), 4, 4);
            }
            else
            {
                // Fretted: filled dot between fret lines
                int relative = fret - startFret + 1;
                if (relative < 1 || relative > FretCount) continue;
                double dotY = nutY + (relative - 0.5) * FretGap;
                ctx.DrawEllipse(dotBrush, null, new Point(x, dotY), DotRadius, DotRadius);
            }
        }
    }
}
