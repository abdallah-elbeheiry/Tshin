using System.Text;
using Tshin.Core.Models;
using Tshin.Core.Utils.Commands;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Systems;

/// <summary>
/// Handles the serialization of ECS entities, components, and narrative story nodes into a custom script format.
/// </summary>
public static class FileWriter
{
    public static async Task SaveFileAsync(string filePath, EntityManager entityManager, NodeManager nodeManager)
    {
        await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        await SerializeGlobalEntitiesAsync(writer, entityManager);
        await SerializeStoryNodesAsync(writer, nodeManager);
    }

    #region Entity Serialization

    private static async Task SerializeGlobalEntitiesAsync(StreamWriter writer, EntityManager entityManager)
    {
        var entities = entityManager.GetAllEntities();

        foreach (var entity in entities)
        {
            await writer.WriteLineAsync($"[Entity: \"{entity.Id}\"]");
            
            var xStr = entity.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var yStr = entity.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await writer.WriteLineAsync($"position: {xStr},{yStr}");
            await writer.WriteLineAsync($"name: \"{EscapeText(entity.Name)}\""); // Escaped just in case they use quotes in the name
            await writer.WriteLineAsync($"visible: {entity.Visible.ToString().ToLower()}");
            
            var components = entityManager.GetComponentsForEntity(entity); 
            foreach (var component in components)
            {
                await SerializeComponentAsync(writer, component);
            }
            
            await writer.WriteLineAsync();
        }
    }

    private static async Task SerializeComponentAsync(StreamWriter writer, IComponent component)
    {
        // Cache visibility string
        var visibleStr = component.Visible.ToString().ToLower();

        switch (component)
        {
            case NumberComponent numComp:
            {
                var numVal = numComp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var minVal = numComp.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var maxVal = numComp.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await writer.WriteLineAsync($"number: \"{numComp.Name}\"");
                await writer.WriteLineAsync("{");
                await writer.WriteLineAsync($"  value: {numVal}");
                await writer.WriteLineAsync($"  min: {minVal}");
                await writer.WriteLineAsync($"  max: {maxVal}");
                await writer.WriteLineAsync($"  visible: {visibleStr}"); // Added Component Visibility
                await writer.WriteLineAsync("}");
                break;
            }

            case TextComponent textComp:
                await writer.WriteLineAsync($"text: \"{textComp.Name}\"");
                await writer.WriteLineAsync("{");
                await writer.WriteLineAsync($"  value: \"{EscapeText(textComp.Value)}\"");
                await writer.WriteLineAsync($"  visible: {visibleStr}"); // Added Component Visibility
                await writer.WriteLineAsync("}");
                break;

            case ConditionComponent boolComp:
                await writer.WriteLineAsync($"condition: \"{boolComp.Name}\""); //condition: is also accepted
                await writer.WriteLineAsync("{");
                await writer.WriteLineAsync($"  value: {boolComp.Value.ToString().ToLower()}");
                await writer.WriteLineAsync($"  visible: {visibleStr}"); // Added Component Visibility
                await writer.WriteLineAsync("}");
                break;
        }
    }

    #endregion

    #region Story Node & Choice Serialization

    private static async Task SerializeStoryNodesAsync(StreamWriter writer, NodeManager nodeManager)
    {
        var nodes = nodeManager.GetNodes();

        foreach (var node in nodes)
        {
            await writer.WriteLineAsync($"[{node.NodeType}: \"{node.Id}\"]");

            await writer.WriteLineAsync($"text: \"{EscapeText(node.DisplayText)}\"");
            
            var xStr = node.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var yStr = node.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await writer.WriteLineAsync($"position: {xStr},{yStr}");

            if (node is IBranchingNode branchingNode)
            {
                await SerializeChoicesAsync(writer, branchingNode, nodeManager);
            }

            await writer.WriteLineAsync();
        }
    }

    private static async Task SerializeChoicesAsync(StreamWriter writer, IBranchingNode branchingNode, NodeManager nodeManager)
    {
        var choices = nodeManager.GetChoices(branchingNode);

        foreach (var choice in choices)
        {
            var targetPart = choice.Node is not null ? $"\"{choice.Node.Id}\"" : "null";
            var escapedDisplayText = EscapeText(choice.DisplayText);
            await writer.WriteLineAsync($"choice: \"{escapedDisplayText}\"->{targetPart}");

            var hasBlock = choice.Commands.Count > 0 || choice.Condition is not null;
            if (!hasBlock) continue;

            await writer.WriteLineAsync("{");

            if (choice.Condition is not null)
            {
                await writer.WriteAsync("  require: ");
                await SerializeConditionTreeAsync(writer, choice.Condition, 2);
            }

            foreach (var cmd in choice.Commands)
            {
                var verb = cmd.Field.ToString().ToLower();

                switch (cmd)
                {
                    case ModifyNumberCommand numCmd:
                        var numVal = numCmd.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        await writer.WriteLineAsync($"  {verb}: \"{numCmd.Entity.Id}\" \"{numCmd.TargetComponentName}\" {numVal}");
                        break;

                    case ModifyTextCommand textCmd:
                        var escapedValue = EscapeText(textCmd.Value);
                        await writer.WriteLineAsync($"  {verb}: \"{textCmd.Entity.Id}\" \"{textCmd.TargetComponentName}\" \"{escapedValue}\"");
                        break;

                    case ModifyBooleanCommand boolCmd:
                        await writer.WriteLineAsync($"  {verb}: \"{boolCmd.Entity.Id}\" \"{boolCmd.TargetComponentName}\" {boolCmd.Value.ToString().ToLower()}");
                        break;
                }
            }

            await writer.WriteLineAsync("}");
        }
    }

    /// <summary>
    /// Recursively serializes a condition tree to the output stream.
    /// <c>or(...)</c> and <c>and(...)</c> wrappers are output as multi-line indented blocks.
    /// Atomic conditions are written as single-line quoted tokens with operator and value.
    /// </summary>
    /// <param name="writer">The output stream writer.</param>
    /// <param name="node">The condition node to serialize.</param>
    /// <param name="indent">The current indentation level (number of spaces).</param>
    private static async Task SerializeConditionTreeAsync(StreamWriter writer, IConditionComponentNode node, int indent)
    {
        var pad = new string(' ', indent);

        switch (node)
        {
            case LogicalGroupNode group:
            {
                var keyword = group.IsAnd ? "and" : "or";
                await writer.WriteLineAsync($"{keyword}(");

                foreach (var child in group.Children)
                {
                    await SerializeConditionTreeAsync(writer, child, indent + 2);
                }

                await writer.WriteLineAsync($"{pad})");
                break;
            }

            case AtomicConditionNode atomic:
            {
                var escapedComponentId = EscapeText(atomic.ComponentId);
                await writer.WriteLineAsync($"{pad}\"{atomic.EntityId}\" \"{escapedComponentId}\" {atomic.Operator} {atomic.TargetValue}");
                break;
            }
        }
    }

    #endregion

    #region Escaping

    /// <summary>
    /// Escapes a string for safe embedding inside double-quotes.
    /// </summary>
    public static string EscapeText(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        return value
            .Replace("\r", "")
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n");
    }

    #endregion
}
