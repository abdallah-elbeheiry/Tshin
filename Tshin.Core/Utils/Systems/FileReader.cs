using System.Text;
using Tshin.Core.Models;
using Tshin.Core.Utils.Commands;
using Tshin.Core.Utils.Factories;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Systems;

/// <summary>
/// Handles the deserialization of ECS entities, components, and narrative graphs from custom script files.
/// </summary>
public static class FileReader
{
    public static async Task LoadFileAsync(string filePath, EntityManager entityManager)
    {
        NodeManager.ClearNodes();
        entityManager.ClearEntities(); // Reset global ECS state for clean loading context

        if (!File.Exists(filePath)) return;

        var temporaryChoicesMap = new List<PendingChoiceLink>();
        var entityCache = new Dictionary<string, Entity>();
        
        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
        IBranchingNode? currentNode = null;
        Choice? lastCreatedChoice = null;
        Entity? currentEntityContext = null;
        var insideChoiceBlock = false;

        foreach (var rawLine in lines)
        {
            var lineWithoutComments = rawLine.Split('#')[0];
            var line = lineWithoutComments.Trim();
            
            if (string.IsNullOrEmpty(line)) continue;

            // Handle block markers
            if (line == "{") { insideChoiceBlock = true; continue; }
            if (line == "}") { insideChoiceBlock = false; lastCreatedChoice = null; continue; }

            // Handle Header Identifiers ([Entity: "..."] or [StoryNode: "..."])
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var (headerType, id) = ParseHeaderLine(line);
                if (string.IsNullOrEmpty(id)) continue;

                switch (headerType)
                {
                    case "Entity":
                        currentEntityContext = ResolveEntity(id, entityCache, entityManager);
                        currentNode = null; // Clear narrative context while processing entities
                        break;
                    case "StoryNode" when NodeManager.GetNodeIds().Contains(id):
                        continue;
                    case "StoryNode":
                        currentNode = NodeFactory.CreateNode(NodeType.StoryNode, id) as IBranchingNode;
                        currentEntityContext = null; // Clear entity context while processing narrative
                        lastCreatedChoice = null;
                        insideChoiceBlock = false;
                        break;
                }
                continue;
            }

            // 1. Process Global Entity Components Configuration
            if (currentEntityContext != null && currentNode == null)
            {
                ParseAndRegisterComponent(line, currentEntityContext, entityManager);
                continue;
            }

            // 2. Process Narrative Topology Block
            if (currentNode != null)
            {
                if (line.StartsWith("text:"))
                {
                    var textPart = line["text:".Length..].Trim();
                    currentNode.DisplayText = ExtractBetweenQuotes(textPart).Replace("\\n", Environment.NewLine);
                }
                else if (line.StartsWith("position:"))
                {
                    ParsePosition(line, currentNode);
                }
                else if (line.StartsWith("choice:"))
                {
                    lastCreatedChoice = ParseChoice(line, currentNode, temporaryChoicesMap);
                }
                // 3. Process Action Commands inside localized bracket contexts (e.g., reduce:, set:, increase:)
                else if (insideChoiceBlock && lastCreatedChoice != null && ContainsActionVerb(line, out var verbStr))
                {
                    ParseAndAddActionCommand(line, verbStr, lastCreatedChoice, entityCache, entityManager);
                }
            }
        }

        LinkChoicePaths(temporaryChoicesMap);
    }

    #region Parsing Helpers

    private static (string Type, string Id) ParseHeaderLine(string line)
    {
        var headerContent = line.Trim('[', ']'); 
        var splitIndex = headerContent.IndexOf(':');
        if (splitIndex == -1) return (string.Empty, string.Empty);

        var type = headerContent[..splitIndex].Trim();
        var rawId = headerContent[(splitIndex + 1)..].Trim();
        return (type, ExtractBetweenQuotes(rawId));
    }

    private static Entity ResolveEntity(string entityId, Dictionary<string, Entity> entityCache, EntityManager entityManager)
    {
        if (entityCache.TryGetValue(entityId, out var targetEntity)) return targetEntity;
        var id = Guid.TryParse(entityId, out var parsedGuid) ? parsedGuid : Guid.NewGuid();
        targetEntity = entityManager.CreateEntity(id);
        entityCache[entityId] = targetEntity;
        return targetEntity;
    }

    private static void ParseAndRegisterComponent(string line, Entity entity, EntityManager entityManager)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex == -1) return;

        var typeTag = line[..colonIndex].Trim();
        var rawArgs = line[(colonIndex + 1)..].Trim();
        var args = ParseCommandArgs(rawArgs);

        if (args.Count < 2) return;

        var compName = args[0];
        var rawVal = args[1];

        switch (typeTag)
        {
            case "number" when double.TryParse(rawVal, System.Globalization.CultureInfo.InvariantCulture, out var n):
                entityManager.SetComponent(entity, new NumberComponent { Name = compName, Value = n });
                break;
            case "text":
                entityManager.SetComponent(entity, new TextComponent { Name = compName, Value = rawVal });
                break;
            case "boolean" when bool.TryParse(rawVal, out var b):
                entityManager.SetComponent(entity, new ConditionComponent { Name = compName, Value = b });
                break;
        }
    }

    private static void ParsePosition(string line, IBranchingNode currentNode)
    {
        var coordinatesPart = line["position:".Length..].Trim();
        var coordinates = coordinatesPart.Split(',');

        if (coordinates.Length != 2) return;
        if (double.TryParse(coordinates[0].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(coordinates[1].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var y))
        {
            currentNode.X = x;
            currentNode.Y = y;
        }
    }

    private static Choice? ParseChoice(string line, IBranchingNode currentNode, List<PendingChoiceLink> temporaryChoicesMap)
    {
        var arrowIndex = line.IndexOf("->", StringComparison.Ordinal);
        if (arrowIndex == -1) return null;

        var leftPart = line[..arrowIndex];
        var rightPart = line[(arrowIndex + 2)..];

        var choiceText = ExtractBetweenQuotes(leftPart).Replace("\\n", Environment.NewLine);
        var targetNodeId = ExtractBetweenQuotes(rightPart);

        if (string.IsNullOrEmpty(targetNodeId)) return null;
        
        var newChoice = new Choice(currentNode, choiceText);
        NodeManager.AddChoice(newChoice, currentNode);
        temporaryChoicesMap.Add(new PendingChoiceLink(newChoice, targetNodeId));
        
        return newChoice;
    }

    private static bool ContainsActionVerb(string line, out string verb)
    {
        verb = string.Empty;
        var colonIndex = line.IndexOf(':');
        if (colonIndex == -1) return false;

        var potentialVerb = line[..colonIndex].Trim();
        if (potentialVerb is not ("set" or "increase" or "reduce")) return false;
        verb = potentialVerb;
        return true;
    }

    private static void ParseAndAddActionCommand(string line, string verbStr, Choice targetChoice, Dictionary<string, Entity> entityCache, EntityManager entityManager)
    {
        var body = line[(line.IndexOf(':') + 1)..].Trim();
        var args = ParseCommandArgs(body);

        if (args.Count < 3) return;

        var entityId = args[0];
        var targetComponentName = args[1];
        var rawValue = args[2];

        var targetEntity = ResolveEntity(entityId, entityCache, entityManager);

        if (!Enum.TryParse<CommandField>(verbStr, true, out var commandFieldContext))
        {
            commandFieldContext = CommandField.Set;
        }

        // Determine assignment destination based on value types
        if (double.TryParse(rawValue, System.Globalization.CultureInfo.InvariantCulture, out var numVal))
        {
            targetChoice.Commands.Add(new ModifyNumberCommand 
            { 
                Entity = targetEntity,
                TargetComponentName = targetComponentName, 
                Value = numVal,
                Field = commandFieldContext
            });
        }
        else if (bool.TryParse(rawValue, out var boolVal))
        {
            targetChoice.Commands.Add(new ModifyBooleanCommand 
            { 
                Entity = targetEntity,
                TargetComponentName = targetComponentName, 
                Value = boolVal,
                Field = commandFieldContext
            });
        }
        else
        {
            targetChoice.Commands.Add(new ModifyTextCommand 
            { 
                Entity = targetEntity,
                TargetComponentName = targetComponentName, 
                Value = rawValue,
                Field = commandFieldContext
            });
        }
    }

    private static void LinkChoicePaths(List<PendingChoiceLink> temporaryChoicesMap)
    {
        foreach (var pendingLink in temporaryChoicesMap)
        {
            if (NodeManager.TryGetNode(pendingLink.TargetId, out var targetNodeInstance))
            {
                NodeManager.ModifyChoicePath(pendingLink.ChoiceItem, targetNodeInstance);
            }
        }
    }

    #endregion

    #region String Utilities

    private static string ExtractBetweenQuotes(string input)
    {
        var firstQuote = input.IndexOf('"');
        var lastQuote = input.LastIndexOf('"');

        if (firstQuote != -1 && lastQuote != -1 && firstQuote < lastQuote)
        {
            return input[(firstQuote + 1)..lastQuote];
        }
        return string.Empty; 
    }

    private static List<string> ParseCommandArgs(string rawArgs)
    {
        var results = new List<string>();
        var currentToken = new StringBuilder();
        bool insideQuotes = false;

        foreach (var c in rawArgs)
        {
            switch (c)
            {
                case '"':
                    insideQuotes = !insideQuotes;
                    continue;
                case ',' when !insideQuotes:
                    results.Add(currentToken.ToString().Trim());
                    currentToken.Clear();
                    continue;
                default:
                    currentToken.Append(c);
                    break;
            }
        }
        results.Add(currentToken.ToString().Trim());
        return results;
    }

    #endregion

    private sealed record PendingChoiceLink(IChoice ChoiceItem, string TargetId);
}
