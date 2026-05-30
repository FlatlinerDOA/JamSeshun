using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;

namespace JamSeshun.Behaviors;

public static class InputPaneAssist
{
    public static readonly AttachedProperty<bool> AdjustForKeyboardProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, bool>("AdjustForKeyboard", typeof(InputPaneAssist));

    public static bool GetAdjustForKeyboard(TemplatedControl element) => element.GetValue(AdjustForKeyboardProperty);
    public static void SetAdjustForKeyboard(TemplatedControl element, bool value) => element.SetValue(AdjustForKeyboardProperty, value);

    static InputPaneAssist()
    {
        AdjustForKeyboardProperty.Changed.AddClassHandler<TemplatedControl>(OnAdjustForKeyboardChanged);
    }

    private static void OnAdjustForKeyboardChanged(TemplatedControl control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            control.AttachedToVisualTree += OnAttached;
        else
            control.AttachedToVisualTree -= OnAttached;
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TemplatedControl control) return;
        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel?.InputPane is not { } inputPane) return;

        var basePadding = control.Padding;

        EventHandler<InputPaneStateEventArgs>? handler = null;
        handler = (_, args) =>
        {
            if (args.NewState == InputPaneState.Open)
            {
                var controlBottom = control.TranslatePoint(new Point(0, control.Bounds.Height), topLevel)?.Y ?? 0;
                var overlap = controlBottom - args.EndRect.Top;
                control.Padding = new Thickness(
                    basePadding.Left, basePadding.Top, basePadding.Right,
                    basePadding.Bottom + Math.Max(0, overlap));
            }
            else
            {
                control.Padding = basePadding;
            }
        };

        inputPane.StateChanged += handler;
        control.DetachedFromVisualTree += (_, _) => inputPane.StateChanged -= handler;
    }
}
