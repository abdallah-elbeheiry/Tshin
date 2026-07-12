using System.Collections.Generic;
using Avalonia.Headless.XUnit;
using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>Component value formatting and change notification (drives the player panel).</summary>
public class ComponentViewModelTests
{
    [AvaloniaFact]
    public void Number_display_value_uses_invariant_two_decimals()
    {
        var c = new NumberComponentViewModel("hp", 3.5, 0, 10, NullEditorContext.Instance);
        Assert.Equal("3.5", c.DisplayValue);

        c.Value = 7;
        Assert.Equal("7", c.DisplayValue);
    }

    [AvaloniaFact]
    public void Condition_display_value_is_true_or_false()
    {
        var c = new ConditionComponentViewModel("flag", true, NullEditorContext.Instance);
        Assert.Equal("true", c.DisplayValue);
        c.Value = false;
        Assert.Equal("false", c.DisplayValue);
    }

    [AvaloniaFact]
    public void Text_display_value_is_the_value()
    {
        var c = new TextComponentViewModel("name", "Kai", NullEditorContext.Instance);
        Assert.Equal("Kai", c.DisplayValue);
    }

    [AvaloniaFact]
    public void Changing_value_raises_property_changed_for_display_value()
    {
        var c = new NumberComponentViewModel("hp", 1, 0, 10, NullEditorContext.Instance);
        var changed = new List<string?>();
        c.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        c.Value = 2;

        Assert.Contains(nameof(ComponentViewModel.DisplayValue), changed);
    }

    [AvaloniaFact]
    public void Value_change_marks_the_editor_dirty()
    {
        var ctx = new RecordingEditorContext();
        var c = new TextComponentViewModel("name", "a", ctx);
        c.Value = "b";
        Assert.Equal(1, ctx.MarkDirtyCount);
    }
}
