using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Tshin.Behaviors;

public sealed class EntityCompletionEntry
{
    public required string EntityName { get; init; }
    public required string EntityId { get; init; }
    public required List<string> ComponentNames { get; init; }
}

public static class TextBoxCompletionBehavior
{
    public static readonly AttachedProperty<bool> EnableVariableCompletionProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("EnableVariableCompletion", typeof(TextBoxCompletionBehavior));

    public static readonly AttachedProperty<List<EntityCompletionEntry>?> CompletionSourceProperty =
        AvaloniaProperty.RegisterAttached<TextBox, List<EntityCompletionEntry>?>("CompletionSource", typeof(TextBoxCompletionBehavior));

    private static readonly ConditionalWeakTable<TextBox, CompletionState> _states = new();

    static TextBoxCompletionBehavior()
    {
        EnableVariableCompletionProperty.Changed.AddClassHandler<TextBox>(OnEnableChanged);
        CompletionSourceProperty.Changed.AddClassHandler<TextBox>(OnSourceChanged);
    }

    public static void SetEnableVariableCompletion(TextBox element, bool value)
        => element.SetValue(EnableVariableCompletionProperty, value);

    public static bool GetEnableVariableCompletion(TextBox element)
        => element.GetValue(EnableVariableCompletionProperty);

    public static void SetCompletionSource(TextBox element, List<EntityCompletionEntry>? value)
        => element.SetValue(CompletionSourceProperty, value);

    public static List<EntityCompletionEntry>? GetCompletionSource(TextBox element)
        => element.GetValue(CompletionSourceProperty);

    private static void OnEnableChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
            Attach(textBox);
        else
            Detach(textBox);
    }

    private static void OnSourceChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs args)
    {
        if (_states.TryGetValue(textBox, out var state))
            state.Source = args.NewValue as List<EntityCompletionEntry>;
    }

    private static void Attach(TextBox textBox)
    {
        if (_states.TryGetValue(textBox, out _)) return;

        var state = new CompletionState(textBox);
        _states.AddOrUpdate(textBox, state);
        state.Attach();
    }

    private static void Detach(TextBox textBox)
    {
        if (!_states.TryGetValue(textBox, out var state)) return;
        state.Detach();
        _states.Remove(textBox);
    }

    private sealed class CompletionState
    {
        public TextBox TextBox { get; }
        public List<EntityCompletionEntry>? Source { get; set; }
        public Popup Popup { get; }
        public ListBox ListBox { get; }
        public List<string> CurrentMatches { get; private set; } = new();
        private int _openBracePos = -1;
        private bool _inEntityStage = true;
        private string _selectedEntityName = "";
        private bool _attached;

        public CompletionState(TextBox textBox)
        {
            TextBox = textBox;

            ListBox = new ListBox
            {
                MinWidth = 180,
                MaxWidth = 320,
                MaxHeight = 200,
            };
            ListBox.Classes.Add("completion");

            Popup = new Popup
            {
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                PlacementTarget = textBox,
                Child = ListBox,
            };
        }

        public void Attach()
        {
            if (_attached) return;
            _attached = true;

            if (TextBox.IsLoaded)
                ParentPopup();
            else
                TextBox.Loaded += OnTextBoxLoaded;

            TextBox.TextChanged += OnTextChanged;
            TextBox.KeyDown += OnKeyDown;
            TextBox.LostFocus += OnLostFocus;
            ListBox.PointerPressed += OnListBoxSelection;
        }

        public void Detach()
        {
            if (!_attached) return;
            _attached = false;

            Popup.IsOpen = false;
            TextBox.Loaded -= OnTextBoxLoaded;
            TextBox.TextChanged -= OnTextChanged;
            TextBox.KeyDown -= OnKeyDown;
            TextBox.LostFocus -= OnLostFocus;
            ListBox.PointerPressed -= OnListBoxSelection;

            RemovePopupFromParent();
        }

        private void OnTextBoxLoaded(object? sender, RoutedEventArgs e)
        {
            TextBox.Loaded -= OnTextBoxLoaded;
            ParentPopup();
        }

        private void ParentPopup()
        {
            if (Popup.Parent is not null) return;

            var ancestor = TextBox.Parent;
            while (ancestor is not null)
            {
                if (ancestor is Panel panel)
                {
                    panel.Children.Add(Popup);
                    return;
                }
                ancestor = ancestor.Parent;
            }
        }

        private void RemovePopupFromParent()
        {
            if (Popup.Parent is Panel panel)
                panel.Children.Remove(Popup);
        }

        private void PositionPopupAtCaret()
        {
            var text = TextBox.Text ?? string.Empty;
            var caretPos = Math.Min(TextBox.CaretIndex, text.Length);

            // Count newlines before the caret to determine the line index
            var textBeforeCaret = text[..caretPos];
            var lineIndex = 0;
            var col = 0;
            for (var i = textBeforeCaret.Length - 1; i >= 0; i--)
            {
                if (textBeforeCaret[i] == '\n')
                {
                    lineIndex++;
                    col = textBeforeCaret.Length - i - 1;
                    break;
                }
            }
            if (textBeforeCaret.IndexOf('\n') == -1)
                col = textBeforeCaret.Length;

            var fontSize = TextBox.FontSize > 0 ? TextBox.FontSize : 14;
            var lineHeight = fontSize * 1.4;
            var charWidth = fontSize * 0.55;

            Popup.HorizontalOffset = col * charWidth + TextBox.Padding.Left;
            Popup.VerticalOffset = -(TextBox.Bounds.Height - (lineIndex * lineHeight + TextBox.Padding.Top + lineHeight));
        }

        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (Source is null || Source.Count == 0) return;
            UpdatePopup();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (Popup.IsOpen)
            {
                if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    MoveSelection(-1);
                    return;
                }

                if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    MoveSelection(1);
                    return;
                }

                if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    e.Handled = true;
                    CommitSelection();
                    return;
                }

                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Popup.IsOpen = false;
                    return;
                }
            }
        }

        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            Popup.IsOpen = false;
        }

        private void OnListBoxSelection(object? sender, PointerPressedEventArgs e)
        {
            CommitSelection();
        }

        private void UpdatePopup()
        {
            var text = TextBox.Text ?? string.Empty;
            var caretPos = TextBox.CaretIndex;
            if (caretPos > text.Length) return;

            // Walk backwards from the character just before the caret to find
            // an opening brace.  This avoids picking up a brace that's at or
            // past the caret position.
            var searchStart = Math.Max(0, caretPos - 1);
            var openBrace = text.LastIndexOf('{', searchStart);
            if (openBrace == -1 || caretPos <= openBrace)
            {
                Popup.IsOpen = false;
                return;
            }

            // Make sure we're not inside a completed {...} block
            var spanLen = caretPos - openBrace;
            if (spanLen > text.Length - openBrace)
                spanLen = text.Length - openBrace;
            if (spanLen <= 0)
            {
                Popup.IsOpen = false;
                return;
            }

            var closeBrace = text.IndexOf('}', openBrace, spanLen);
            if (closeBrace != -1)
            {
                Popup.IsOpen = false;
                return;
            }

            var partial = text[(openBrace + 1)..caretPos];
            _openBracePos = openBrace;

            var dotIndex = partial.IndexOf('.');
            if (dotIndex >= 0)
            {
                var entityName = partial[..dotIndex];
                var componentPrefix = partial[(dotIndex + 1)..];

                var entity = Source!
                    .FirstOrDefault(e => e.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase));

                if (entity is null)
                {
                    Popup.IsOpen = false;
                    return;
                }

                _inEntityStage = false;
                _selectedEntityName = entity.EntityName;

                var matches = entity.ComponentNames
                    .Where(c => c.StartsWith(componentPrefix, StringComparison.OrdinalIgnoreCase))
                    .Take(20)
                    .ToList();

                if (matches.Count == 0 ||
                    (matches.Count == 1 && matches[0].Equals(componentPrefix, StringComparison.OrdinalIgnoreCase)))
                {
                    Popup.IsOpen = false;
                    return;
                }

                CurrentMatches = matches;
            }
            else
            {
                _inEntityStage = true;

                var matches = Source!
                    .Select(e => e.EntityName)
                    .Where(n => n.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                    .Take(20)
                    .ToList();

                if (matches.Count == 0 ||
                    (matches.Count == 1 && matches[0].Equals(partial, StringComparison.OrdinalIgnoreCase)))
                {
                    Popup.IsOpen = false;
                    return;
                }

                CurrentMatches = matches;
            }

            ListBox.ItemsSource = null;
            ListBox.ItemsSource = CurrentMatches;
            ListBox.SelectedIndex = 0;
            PositionPopupAtCaret();
            Popup.IsOpen = true;
        }

        private void MoveSelection(int direction)
        {
            if (ListBox.ItemCount == 0) return;
            var idx = ListBox.SelectedIndex;
            idx = Math.Clamp(idx + direction, 0, ListBox.ItemCount - 1);
            ListBox.SelectedIndex = idx;
            ListBox.ScrollIntoView(idx);
        }

        private void CommitSelection()
        {
            if (ListBox.SelectedItem is not string selected) return;
            if (Source is null) return;
            if (_openBracePos < 0) return;

            var text = TextBox.Text ?? string.Empty;
            var caretPos = Math.Min(TextBox.CaretIndex, text.Length);
            if (_openBracePos >= text.Length) return;

            if (_inEntityStage)
            {
                var matched = Source
                    .FirstOrDefault(e => e.EntityName.Equals(selected, StringComparison.OrdinalIgnoreCase));
                if (matched is null) return;
                var entityName = matched.EntityName;

                var prefix = text[..(_openBracePos + 1)];
                var suffix = caretPos < text.Length ? text[caretPos..] : string.Empty;

                TextBox.Text = prefix + entityName + "." + suffix;
                var newCaret = prefix.Length + entityName.Length + 1;
                TextBox.CaretIndex = newCaret;
                TextBox.SelectionStart = newCaret;
                TextBox.SelectionEnd = newCaret;

                _selectedEntityName = entityName;
                _inEntityStage = false;

                UpdatePopup();
            }
            else
            {
                var value = "{" + _selectedEntityName + "." + selected + "}";
                var prefix = text[.._openBracePos];
                var suffix = caretPos < text.Length ? text[caretPos..] : string.Empty;

                TextBox.Text = prefix + value + suffix;
                var newCaret = prefix.Length + value.Length;
                TextBox.CaretIndex = newCaret;
                TextBox.SelectionStart = newCaret;
                TextBox.SelectionEnd = newCaret;
                Popup.IsOpen = false;
            }
        }
    }
}
