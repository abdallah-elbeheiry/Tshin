using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Tshin.ViewModels;

/// <summary>
/// Abstract base for all component view models. Mirrors <c>IComponent</c> from the domain layer.
/// </summary>
public abstract partial class ComponentViewModel : ViewModelBase
{
    protected readonly Action _onChanged;

    [ObservableProperty]
    private string _name;

    /// <summary>Whether this component is shown at runtime; toggled from the editor.</summary>
    [ObservableProperty]
    private bool _visible = true;

    public abstract string ComponentType { get; }

    /// <summary>Human-readable current value, shown in the player's entities panel.</summary>
    public abstract string DisplayValue { get; }

    public ComponentViewModel(string name, Action onChanged)
    {
        _name = name;
        _onChanged = onChanged;
    }

    partial void OnNameChanged(string value) => _onChanged();
    partial void OnVisibleChanged(bool value) => _onChanged();
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

    public NumberComponentViewModel(string name, double value, double minValue, double maxValue, Action onChanged)
        : base(name, onChanged)
    {
        _value = value;
        _minValue = minValue;
        _maxValue = maxValue;
    }

    partial void OnValueChanged(double value) { _onChanged(); OnPropertyChanged(nameof(DisplayValue)); }
    partial void OnMinValueChanged(double value) => _onChanged();
    partial void OnMaxValueChanged(double value) => _onChanged();
}

public sealed partial class TextComponentViewModel : ComponentViewModel
{
    [ObservableProperty]
    private string _value;

    public override string ComponentType => "text";
    public override string DisplayValue => Value;

    public TextComponentViewModel(string name, string value, Action onChanged)
        : base(name, onChanged)
    {
        _value = value;
    }

    partial void OnValueChanged(string value) { _onChanged(); OnPropertyChanged(nameof(DisplayValue)); }
}

public sealed partial class ConditionComponentViewModel : ComponentViewModel
{
    [ObservableProperty]
    private bool _value;

    public override string ComponentType => "condition";
    public override string DisplayValue => Value ? "true" : "false";

    public ConditionComponentViewModel(string name, bool value, Action onChanged)
        : base(name, onChanged)
    {
        _value = value;
    }

    partial void OnValueChanged(bool value) { _onChanged(); OnPropertyChanged(nameof(DisplayValue)); }
}