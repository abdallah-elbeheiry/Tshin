using System.IO;
using System.Linq;
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
        switch (component)
        {
            case NumberComponent numComp:
                var numVal = numComp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await writer.WriteLineAsync($"number: \"{numComp.Name}\", {numVal}");
                break;

            case TextComponent textComp:
                await writer.WriteLineAsync($"text: \"{textComp.Name}\", \"{textComp.Value}\"");
                break;

            case ConditionComponent boolComp:
                await writer.WriteLineAsync($"boolean: \"{boolComp.Name}\", {boolComp.Value.ToString().ToLower()}");
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

            var escapedText = node.DisplayText.Replace("\r", "").Replace("\n", "\\n");
            await writer.WriteLineAsync($"text: \"{escapedText}\"");
            
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
            await writer.WriteLineAsync($"choice: \"{choice.DisplayText}\"->{targetPart}");

            var commands = choice.Commands.ToList();
            if (commands.Count > 0)
            {
                await SerializeCommandBlockAsync(writer, commands);
            }
        }
    }

    private static async Task SerializeCommandBlockAsync(StreamWriter writer, List<ICommand> commands)
    {
        await writer.WriteLineAsync("{");

        foreach (var cmd in commands)
        {
            var verb = cmd.Field.ToString().ToLower();
            
            switch (cmd)
            {
                case ModifyNumberCommand numCmd:
                    var numVal = numCmd.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    await writer.WriteLineAsync($"  {verb}: \"{numCmd.Entity.Id}\", \"{numCmd.TargetComponentName}\", {numVal}");
                    break;

                case ModifyTextCommand textCmd:
                    await writer.WriteLineAsync($"  {verb}: \"{textCmd.Entity.Id}\", \"{textCmd.TargetComponentName}\", \"{textCmd.Value}\"");
                    break;

                case ModifyBooleanCommand boolCmd:
                    await writer.WriteLineAsync($"  {verb}: \"{boolCmd.Entity.Id}\", \"{boolCmd.TargetComponentName}\", {boolCmd.Value.ToString().ToLower()}");
                    break;
            }
        }

        await writer.WriteLineAsync("}");
    }

    #endregion
}
