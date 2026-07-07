using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Tshin.Core.Models;
using Tshin.Core.Utils.Systems;

namespace Tshin.ViewModels;

/// <summary>
/// Base for the recursive condition-editing view models. A condition tree is either a
/// single <see cref="AtomicConditionViewModel"/> (compare one component to a value) or a
/// <see cref="LogicalGroupViewModel"/> (AND/OR of nested nodes). Each VM can rebuild the
/// matching domain <see cref="IConditionComponentNode"/> via <see cref="BuildModel"/>, and
/// the static <see cref="FromModel"/> reconstructs a VM tree from a domain tree.
/// </summary>
public abstract partial class ConditionNodeViewModel : ViewModelBase
{
    protected readonly Action _onChanged;

    /// <summary>The group this node lives in, or null when it is the root of a choice's tree.</summary>
    public LogicalGroupViewModel? Parent { get; set; }

    protected ConditionNodeViewModel(Action onChanged) => _onChanged = onChanged;

    /// <summary>Builds a fresh domain condition node from the current editor state.</summary>
    public abstract IConditionComponentNode BuildModel();

    // ── Operator symbol ⇄ backend mapping (single source of truth) ─────────

    /// <summary>Operators offered for number components, shown as symbols.</summary>
    public static readonly IReadOnlyList<string> NumberOperators = new[] { "=", "≠", ">", "≥", "<", "≤" };

    /// <summary>Operators offered for text/condition components (equality only).</summary>
    public static readonly IReadOnlyList<string> EqualityOperators = new[] { "=", "≠" };

    private static readonly Dictionary<string, string> SymbolToBackendMap = new()
    {
        ["="] = "==", ["≠"] = "!=", [">"] = ">", ["≥"] = ">=", ["<"] = "<", ["≤"] = "<=",
    };

    private static readonly Dictionary<string, string> BackendToSymbolMap = new()
    {
        ["=="] = "=", ["!="] = "≠", [">"] = ">", [">="] = "≥", ["<"] = "<", ["<="] = "≤",
    };

    public static string SymbolToBackend(string symbol)
        => SymbolToBackendMap.TryGetValue(symbol, out var op) ? op : "==";

    public static string BackendToSymbol(string op)
        => BackendToSymbolMap.TryGetValue(op, out var sym) ? sym : "=";

    /// <summary>Recursively rebuilds an editing VM tree from a domain condition tree.</summary>
    public static ConditionNodeViewModel FromModel(
        IConditionComponentNode node,
        ObservableCollection<EntityViewModel>? entities,
        Action onChanged)
    {
        switch (node)
        {
            case LogicalGroupNode group:
                var groupVm = new LogicalGroupViewModel(onChanged) { AvailableEntities = entities, IsAnd = group.IsAnd };
                foreach (var child in group.Children)
                    groupVm.AddChild(FromModel(child, entities, onChanged));
                return groupVm;

            case AtomicConditionNode atomic:
                return new AtomicConditionViewModel(atomic, entities, onChanged);

            default:
                return new LogicalGroupViewModel(onChanged) { AvailableEntities = entities };
        }
    }
}

/// <summary>
/// A leaf condition: pick an entity, one of its components, a comparison operator, and a
/// value to compare against. Mirrors <see cref="CommandViewModel"/>'s
/// entity→component→value wiring.
/// </summary>
public partial class AtomicConditionViewModel : ConditionNodeViewModel
{
    // Raw identifiers captured on load so BuildModel round-trips even if the picker
    // can't resolve the entity/component (e.g. it was deleted).
    private readonly string _entityIdFallback = string.Empty;
    private readonly string _componentIdFallback = string.Empty;
    private readonly string _targetValueFallback = string.Empty;

    // ── Entity selection ──────────────────────────────────────────────────

    [ObservableProperty]
    private EntityViewModel? _targetEntity;

    public ObservableCollection<EntityViewModel> AvailableEntities { get; }

    // ── Component selection ────────────────────────────────────────────────

    public ObservableCollection<ComponentViewModel> AvailableComponents { get; } = new();

    [ObservableProperty]
    private ComponentViewModel? _selectedComponent;

    /// <summary>"number", "text", "condition", or "" when unknown.</summary>
    [ObservableProperty]
    private string _targetComponentType = "";

    // ── Operator ──────────────────────────────────────────────────────────

    /// <summary>Symbol operators valid for the current component type.</summary>
    public ObservableCollection<string> AvailableOperators { get; } = new();

    [ObservableProperty]
    private string? _selectedOperator;

    // ── Value editors (one per component type) ────────────────────────────

    [ObservableProperty]
    private double _numberValue;

    [ObservableProperty]
    private string _textValue = "";

    [ObservableProperty]
    private bool _boolValue;

    public AtomicConditionViewModel(
        AtomicConditionNode? model,
        ObservableCollection<EntityViewModel>? entities,
        Action onChanged)
        : base(onChanged)
    {
        AvailableEntities = entities ?? new();

        if (model is not null)
        {
            _entityIdFallback = model.EntityId;
            _componentIdFallback = model.ComponentId;
            _targetValueFallback = model.TargetValue;
        }

        // Resolve the entity + component without triggering the reset reactions.
        var entity = model is not null
            ? AvailableEntities.FirstOrDefault(e => e.Id == model.EntityId)
            : null;
        _targetEntity = entity;
        if (entity is not null)
        {
            PopulateAvailableComponents(entity);
            entity.Components.CollectionChanged += OnEntityComponentsChanged;

            if (model is not null && !string.IsNullOrEmpty(model.ComponentId))
            {
                var match = AvailableComponents.FirstOrDefault(c =>
                    c.Name.Equals(model.ComponentId, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    _selectedComponent = match;
                    _targetComponentType = match.ComponentType;
                }
            }
        }

        RebuildOperators();
        var symbol = model is not null ? BackendToSymbol(model.Operator) : "=";
        _selectedOperator = AvailableOperators.Contains(symbol)
            ? symbol
            : AvailableOperators.FirstOrDefault() ?? "=";

        // Seed the type-appropriate value editor from the stored target value.
        if (model is not null)
        {
            switch (_targetComponentType)
            {
                case "number":
                    if (double.TryParse(model.TargetValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        _numberValue = d;
                    break;
                case "text":
                    _textValue = model.TargetValue;
                    break;
                case "condition":
                    _boolValue = bool.TryParse(model.TargetValue, out var b) && b;
                    break;
            }
        }
    }

    // ── Reactions ─────────────────────────────────────────────────────────

    partial void OnTargetEntityChanged(EntityViewModel? oldValue, EntityViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.Components.CollectionChanged -= OnEntityComponentsChanged;
        if (newValue is not null)
            newValue.Components.CollectionChanged += OnEntityComponentsChanged;

        PopulateAvailableComponents(newValue);
        SelectedComponent = null;
        TargetComponentType = "";
        _onChanged();
    }

    private void OnEntityComponentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var previous = SelectedComponent;
        PopulateAvailableComponents(TargetEntity);
        if (previous is not null)
        {
            SelectedComponent = AvailableComponents.FirstOrDefault(c =>
                c.Name.Equals(previous.Name, StringComparison.OrdinalIgnoreCase));
        }
    }

    partial void OnSelectedComponentChanged(ComponentViewModel? value)
    {
        TargetComponentType = value?.ComponentType ?? "";
        RebuildOperators();
        if (SelectedOperator is null || !AvailableOperators.Contains(SelectedOperator))
            SelectedOperator = AvailableOperators.FirstOrDefault() ?? "=";
        _onChanged();
    }

    partial void OnSelectedOperatorChanged(string? value) => _onChanged();
    partial void OnNumberValueChanged(double value) => _onChanged();
    partial void OnTextValueChanged(string value) => _onChanged();
    partial void OnBoolValueChanged(bool value) => _onChanged();

    // ── Helpers ───────────────────────────────────────────────────────────

    private void PopulateAvailableComponents(EntityViewModel? entity)
    {
        AvailableComponents.Clear();
        if (entity is not null)
            foreach (var c in entity.Components)
                AvailableComponents.Add(c);
    }

    private void RebuildOperators()
    {
        var wanted = TargetComponentType == "number" ? NumberOperators : EqualityOperators;
        AvailableOperators.Clear();
        foreach (var op in wanted)
            AvailableOperators.Add(op);
    }

    private string BuildTargetValue() => TargetComponentType switch
    {
        "number" => NumberValue.ToString(CultureInfo.InvariantCulture),
        "text" => TextValue,
        "condition" => BoolValue ? "true" : "false",
        _ => _targetValueFallback,
    };

    public override IConditionComponentNode BuildModel() => new AtomicConditionNode
    {
        EntityId = TargetEntity?.Id ?? _entityIdFallback,
        ComponentId = SelectedComponent?.Name ?? _componentIdFallback,
        Operator = SymbolToBackend(SelectedOperator ?? "="),
        TargetValue = BuildTargetValue(),
    };
}

/// <summary>
/// A composite condition: an AND/OR group of nested condition nodes.
/// </summary>
public partial class LogicalGroupViewModel : ConditionNodeViewModel
{
    /// <summary>True = AND (all children must hold), false = OR (any child holds).</summary>
    [ObservableProperty]
    private bool _isAnd = true;

    public ObservableCollection<ConditionNodeViewModel> Children { get; } = new();

    /// <summary>Shared entity list handed to atomic children's pickers.</summary>
    public ObservableCollection<EntityViewModel>? AvailableEntities { get; set; }

    /// <summary>AND/OR selector items; index 0 = AND, 1 = OR.</summary>
    public ObservableCollection<string> LogicOptions { get; } = new() { "AND", "OR" };

    public int SelectedLogicIndex
    {
        get => IsAnd ? 0 : 1;
        set => IsAnd = value == 0;
    }

    public LogicalGroupViewModel(Action onChanged) : base(onChanged) { }

    public void AddChild(ConditionNodeViewModel child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public void RemoveChild(ConditionNodeViewModel child)
    {
        if (Children.Remove(child))
            child.Parent = null;
    }

    partial void OnIsAndChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectedLogicIndex));
        _onChanged();
    }

    public override IConditionComponentNode BuildModel() => new LogicalGroupNode
    {
        IsAnd = IsAnd,
        Children = Children.Select(c => c.BuildModel()).ToList(),
    };
}
