using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tshin.ViewModels;

/// <summary>
/// ViewModel for a command attached to a choice.
/// Encapsulates the entity→component→value-editor flow:
/// 1. Pick an entity from AvailableEntities
/// 2. Pick a component from that entity's AvailableComponents
/// 3. Based on component type, show the appropriate value editor
/// </summary>
public partial class CommandViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    // ── Entity selection ──────────────────────────────────────────────────

    [ObservableProperty]
    private EntityViewModel? _targetEntity;

    /// <summary>All entities the user can pick from (shared from the editor).</summary>
    public ObservableCollection<EntityViewModel> AvailableEntities { get; }

    // ── Component selection ────────────────────────────────────────────────

    /// <summary>Components on the selected entity — populated automatically.</summary>
    public ObservableCollection<ComponentViewModel> AvailableComponents { get; } = new();

    [ObservableProperty]
    private ComponentViewModel? _selectedComponent;

    /// <summary>Detected type: "number", "text", "condition", or "" when unknown.</summary>
    [ObservableProperty]
    private string _targetComponentType = "";

    // ── Value editors (only one is relevant at a time per component type) ──

    // Number fields
    public ObservableCollection<string> AvailableFields { get; } = new() { "Set", "Increase", "Reduce" };

    [ObservableProperty]
    private int _selectedFieldIndex;

    [ObservableProperty]
    private double _numberValue;

    // Text
    [ObservableProperty]
    private string _textValue = "";

    // Condition
    public ObservableCollection<string> AvailableBoolValues { get; } = new() { "true", "false" };

    [ObservableProperty]
    private string _boolValueText = "true";

    [ObservableProperty]
    private bool _boolValue;

    // ── Derived helpers ───────────────────────────────────────────────────

    public string DisplayFieldValue
    {
        get
        {
            if (SelectedFieldIndex >= 0 && SelectedFieldIndex < AvailableFields.Count)
                return AvailableFields[SelectedFieldIndex];
            return "Set";
        }
    }

    /// <summary>Name of the target component (written to snapshot).</summary>
    public string TargetComponentName => SelectedComponent?.Name ?? "";

    // ── Constructor ───────────────────────────────────────────────────────

    public CommandViewModel(
        EntityViewModel? targetEntity,
        string targetComponentName,
        string field,
        string valueText,
        double numberValue,
        bool boolValue,
        ObservableCollection<EntityViewModel> availableEntities,
        Action onChanged)
    {
        _targetEntity = targetEntity;
        _numberValue = numberValue;
        _boolValue = boolValue;
        _boolValueText = boolValue ? "true" : "false";
        _textValue = valueText;
        AvailableEntities = availableEntities;
        _onChanged = onChanged;

        // Set field index
        var idx = AvailableFields.IndexOf(field);
        _selectedFieldIndex = idx >= 0 ? idx : 0;

        // If a target entity is already set, populate its components
        if (targetEntity is not null)
            PopulateAvailableComponents(targetEntity);

        // Try to find and select the matching component by name
        if (targetEntity is not null && !string.IsNullOrEmpty(targetComponentName))
        {
            var match = AvailableComponents.FirstOrDefault(c =>
                c.Name.Equals(targetComponentName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                _selectedComponent = match;
                _targetComponentType = match.ComponentType;
            }
        }
    }

    // ── Reactions ─────────────────────────────────────────────────────────

    partial void OnTargetEntityChanged(EntityViewModel? value)
    {
        PopulateAvailableComponents(value);
        SelectedComponent = null;
        TargetComponentType = "";
        _onChanged();
    }

    partial void OnSelectedComponentChanged(ComponentViewModel? value)
    {
        if (value is not null)
        {
            TargetComponentType = value.ComponentType;
            // Reset field to "Set" when component type changes
            SelectedFieldIndex = 0;
        }
        else
        {
            TargetComponentType = "";
        }
        _onChanged();
    }

    partial void OnNumberValueChanged(double value) => _onChanged();
    partial void OnTextValueChanged(string value) => _onChanged();
    partial void OnBoolValueChanged(bool value) => _onChanged();
    partial void OnBoolValueTextChanged(string value)
    {
        if (bool.TryParse(value, out var b))
            BoolValue = b;
        _onChanged();
    }
    partial void OnSelectedFieldIndexChanged(int value) => _onChanged();

    // ── Helpers ───────────────────────────────────────────────────────────

    private void PopulateAvailableComponents(EntityViewModel? entity)
    {
        AvailableComponents.Clear();
        if (entity is not null)
        {
            foreach (var c in entity.Components)
                AvailableComponents.Add(c);
        }
    }
}