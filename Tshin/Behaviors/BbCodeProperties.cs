using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Controls;
using Tshin.Utilities;

namespace Tshin.Behaviors;

public static class BbCodeProperties
{
    public static readonly AttachedProperty<string?> BbCodeTextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("BbCodeText", typeof(BbCodeProperties));

    static BbCodeProperties()
    {
        BbCodeTextProperty.Changed.AddClassHandler<TextBlock>(OnBbCodeTextChanged);
    }

    public static void SetBbCodeText(TextBlock element, string? value)
        => element.SetValue(BbCodeTextProperty, value);

    public static string? GetBbCodeText(TextBlock element)
        => element.GetValue(BbCodeTextProperty);

    private static void OnBbCodeTextChanged(TextBlock? textBlock, AvaloniaPropertyChangedEventArgs args)
    {
        if (textBlock?.Inlines is null) return;
        if (args.NewValue is string text)
        {
            var inlines = BbCodeParser.Parse(text);
            textBlock.Inlines.Clear();
            textBlock.Inlines.AddRange(inlines);
        }
        else
        {
            textBlock.Inlines.Clear();
        }
    }
}
