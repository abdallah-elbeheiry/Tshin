using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Tshin.Views;

namespace Tshin.Behaviors;

public static class TextBoxFormattingBehavior
{
    public static readonly AttachedProperty<bool> EnableFormattingMenuProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("EnableFormattingMenu", typeof(TextBoxFormattingBehavior));

    private static readonly ConditionalWeakTable<TextBox, FormattingState> _states = new();

    static TextBoxFormattingBehavior()
    {
        EnableFormattingMenuProperty.Changed.AddClassHandler<TextBox>(OnEnableChanged);
    }

    public static void SetEnableFormattingMenu(TextBox element, bool value)
        => element.SetValue(EnableFormattingMenuProperty, value);

    public static bool GetEnableFormattingMenu(TextBox element)
        => element.GetValue(EnableFormattingMenuProperty);

    private static void OnEnableChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
            Attach(textBox);
        else
            Detach(textBox);
    }

    private static void Attach(TextBox textBox)
    {
        if (_states.TryGetValue(textBox, out _)) return;

        var state = new FormattingState(textBox);
        _states.AddOrUpdate(textBox, state);
        textBox.ContextMenu = BuildContextMenu(state);
    }

    private static void Detach(TextBox textBox)
    {
        if (!_states.TryGetValue(textBox, out var state)) return;
        textBox.ContextMenu = null;
        _states.Remove(textBox);
    }

    private static ContextMenu BuildContextMenu(FormattingState state)
    {
        var menu = new ContextMenu();

        var boldItem = new MenuItem { Header = "Bold" };
        boldItem.Click += (_, _) => WrapSelection(state.TextBox, "[b]", "[/b]");
        menu.Items.Add(boldItem);

        var italicItem = new MenuItem { Header = "Italic" };
        italicItem.Click += (_, _) => WrapSelection(state.TextBox, "[i]", "[/i]");
        menu.Items.Add(italicItem);

        var underlineItem = new MenuItem { Header = "Underline" };
        underlineItem.Click += (_, _) => WrapSelection(state.TextBox, "[u]", "[/u]");
        menu.Items.Add(underlineItem);

        var sizeItem = new MenuItem { Header = "Large Size" };
        sizeItem.Click += (_, _) => WrapSelection(state.TextBox, "[size=24]", "[/size]");
        menu.Items.Add(sizeItem);

        var colorItem = new MenuItem { Header = "Insert Color..." };
        colorItem.Click += async (_, _) =>
        {
            state.CacheSelection();
            var colorResult = await ShowColorPicker(state.TextBox);
            if (colorResult is null) return;
            state.RestoreSelection();
            WrapSelection(state.TextBox, $"[color={colorResult}]", "[/color]");
        };
        menu.Items.Add(colorItem);

        return menu;
    }

    private static async Task<string?> ShowColorPicker(TextBox textBox)
    {
        var popup = new ColorPickerPopup();
        var window = TopLevel.GetTopLevel(textBox) as Window;
        if (window is not null)
        {
            return await popup.ShowDialog<string?>(window);
        }
        return null;
    }

    private static void WrapSelection(TextBox tb, string prefix, string suffix)
    {
        var start = Math.Min(tb.SelectionStart, tb.SelectionEnd);
        var end = Math.Max(tb.SelectionStart, tb.SelectionEnd);
        var text = tb.Text ?? string.Empty;
        var length = text.Length;

        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, start, length);

        var selected = text[start..end];
        var inserted = prefix + selected + suffix;

        tb.Text = text[..start] + inserted + text[end..];
        tb.SelectionStart = start;
        tb.SelectionEnd = start + inserted.Length;
        tb.Focus();
    }

    private sealed class FormattingState
    {
        public TextBox TextBox { get; }
        public int CachedStart { get; private set; }
        public int CachedEnd { get; private set; }

        public FormattingState(TextBox textBox)
        {
            TextBox = textBox;
        }

        public void CacheSelection()
        {
            CachedStart = TextBox.SelectionStart;
            CachedEnd = TextBox.SelectionEnd;
        }

        public void RestoreSelection()
        {
            TextBox.Focus();
            TextBox.SelectionStart = CachedStart;
            TextBox.SelectionEnd = CachedEnd;
        }
    }
}
