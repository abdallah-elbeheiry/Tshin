namespace Tshin.Core.Utils.Commands;

/// <summary>
/// Represents the type of command field.
/// Increase and Reduce are used for numeric fields <see cref="ModifyNumberCommand"/>, Set is used for all fields.
/// </summary>
public enum CommandField
{
    Increase,
    Reduce,
    Set
}