namespace Tshin.Core.Models;

/// <summary>
/// Defines the behavior of a condition choice when its conditions evaluates to false.
/// Close means to keep the choice visible but not selectable (Logic is for the UI not the backend)
/// Hide means to hide the choice from the UI completely (Logic is for the UI not the backend)
/// </summary>
public enum ConditionFalseBehavior
{
    Close,
    Hide,
}