using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Tshin.ViewModels;

/// <summary>
/// Abstract base for all component view models. Mirrors <c>IComponent</c> from the domain layer.
/// </summary>
public abstract partial class ComponentViewModel : ViewModelBase
{
    protected readonly IEditorContext _context;
    protected readonly Action _onChanged;

    [ObservableProperty]
    private string _name;

    /// <summary>Whether this component is shown at runtime; toggled from the editor.</summary>
    [ObservableProperty]
    private bool _visible = true;

    public abstract string ComponentType { get; }

    /// <summary>Human-readable current value, shown in the player's entities panel.</summary>
    public abstract string DisplayValue { get; }

    public ComponentViewModel(string name, IEditorContext context)
    {
        _name = name;
        _context = context;
        _onChanged = context.MarkDirty;
    }

    partial void OnNameChanged(string value) => _context.NoteContinuousChange(this, "text");
    partial void OnVisibleChanged(bool value) => _onChanged();

    /// <summary>Removes this component from its owning entity.</summary>
    [RelayCommand]
    private void RemoveSelf() => _context.RemoveComponentFromEntity(this);

    /// <summary>Returns the inspector from a component editor to its owning entity.</summary>
    [RelayCommand]
    private void BackToEntity() => _context.BackToEntity();
}

public sealed partial class NumberComponentViewModel : ComponentViewModel
{
    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private double _minValue;

    [ObservableProperty]
    private double _maxValue;

    public override string ComponentType => "number";
    public override string DisplayValue => Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public NumberComponentViewModel(string name, double value, double minValue, double maxValue, IEditorContext context)
        : base(name, context)
    {
        _value = value;
        _minValue = minValue;
        _maxValue = maxValue;
    }

    partial void OnValueChanged(double value) { _context.NoteContinuousChange(this, "value"); OnPropertyChanged(nameof(DisplayValue)); }
    partial void OnMinValueChanged(double value) => _context.NoteContinuousChange(this, "value");
    partial void OnMaxValueChanged(double value) => _context.NoteContinuousChange(this, "value");
}

public sealed partial class TextComponentViewModel : ComponentViewModel
{
    [ObservableProperty]
    private string _value;

    public override string ComponentType => "text";
    public override string DisplayValue => Value;

    public TextComponentViewModel(string name, string value, IEditorContext context)
        : base(name, context)
    {
        _value = value;
    }

    partial void OnValueChanged(string value) { _context.NoteContinuousChange(this, "value"); OnPropertyChanged(nameof(DisplayValue)); }
}

public sealed partial class ConditionComponentViewModel : ComponentViewModel
{
    [ObservableProperty]
    private bool _value;

    public override string ComponentType => "condition";
    public override string DisplayValue => Value ? "true" : "false";

    public ConditionComponentViewModel(string name, bool value, IEditorContext context)
        : base(name, context)
    {
        _value = value;
    }

    partial void OnValueChanged(bool value) { _onChanged(); OnPropertyChanged(nameof(DisplayValue)); }
}