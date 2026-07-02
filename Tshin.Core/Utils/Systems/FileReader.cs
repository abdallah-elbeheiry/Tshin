using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Tshin.Core.Models;
using Tshin.Core.Utils.Commands;
using Tshin.Core.Utils.Factories;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Systems;

/// <summary>
/// Handles the robust deserialization of ECS entities, components, and narrative graphs from custom script files.
/// </summary>
public static class FileReader
{
    public static async Task LoadFileAsync(string filePath, EntityManager entityManager, NodeManager nodeManager)
    {
        nodeManager.ClearNodes();
        entityManager.ClearEntities(); // Reset global ECS state for clean loading context

        if (!File.Exists(filePath)) return;

        var temporaryChoicesMap = new List<PendingChoiceLink>();
        var entityCache = new Dictionary<string, Entity>();
        
        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
        IBranchingNode? currentNode = null;
        Choice? lastCreatedChoice = null;
        Entity? currentEntityContext = null;
        
        var insideChoiceBlock = false;
        var insideComponentBlock = false;

        // Component block parsing state grouped
        string? pendingComponentType = null;
        string? pendingComponentName = null;
        string? pendingComponentValue = null;
        string? pendingComponentMin = null;
        string? pendingComponentMax = null;

        foreach (var rawLine in lines)
        {
            var lineWithoutComments = rawLine.Split('#')[0];
            var line = lineWithoutComments.Trim();
            
            if (string.IsNullOrEmpty(line)) continue;

            // Handle block markers
            if (line == "{")
            {
                insideChoiceBlock = true;
                if (currentEntityContext != null && pendingComponentType != null)
                    insideComponentBlock = true;
                continue;
            }
            if (line == "}")
            {
                // Finalize component if we were in a component block
                if (insideComponentBlock && currentEntityContext != null && pendingComponentType != null)
                {
                    FinalizeComponent(pendingComponentType, pendingComponentName,
                        pendingComponentValue, pendingComponentMin, pendingComponentMax,
                        currentEntityContext, entityManager);
                    
                    pendingComponentType = pendingComponentName = pendingComponentValue = pendingComponentMin = pendingComponentMax = null;
                }

                insideChoiceBlock = false;
                insideComponentBlock = false;
                lastCreatedChoice = null;
                continue;
            }

            // Handle Header Identifiers ([Entity: "..."] or [StoryNode: "..."])
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var (headerType, id) = ParseHeaderLine(line);
                if (string.IsNullOrEmpty(id)) continue;

                // Absolute State Reset: Clear all flags across contexts to handle missing braces gracefully
                pendingComponentType = pendingComponentName = pendingComponentValue = pendingComponentMin = pendingComponentMax = null;
                insideComponentBlock = false;
                insideChoiceBlock = false;
                lastCreatedChoice = null;

                switch (headerType)
                {
                    case "Entity":
                        currentEntityContext = ResolveEntity(id, entityCache, entityManager);
                        currentNode = null; 
                        break;
                    case "StoryNode" when nodeManager.GetNodeIds().Contains(id):
                        continue;
                    case "StoryNode":
                        var newNode = NodeFactory.CreateNode(NodeType.StoryNode, id);
                        nodeManager.AppendNode(newNode);
                        currentNode = newNode as IBranchingNode;
                        currentEntityContext = null; 
                        break;
                }
                continue;
            }

            // Universal Colon Pre-Processing for fields
            var colonIndex = line.IndexOf(':');
            string key = string.Empty;
            string valuePart = string.Empty;
            
            if (colonIndex != -1)
            {
                key = line[..colonIndex].Trim().ToLower();
                valuePart = line[(colonIndex + 1)..].Trim();
            }

            // 1. Process Global Entity Configuration Context
            if (currentEntityContext != null && currentNode == null)
            {
                if (colonIndex != -1 && !insideComponentBlock)
                {
                    if (key == "position")
                    {
                        ParsePositionFromValue(valuePart, currentEntityContext);
                        continue;
                    }
                    if (key == "name")
                    {
                        var entityName = ExtractBetweenQuotes(valuePart);
                        if (!string.IsNullOrEmpty(entityName)) currentEntityContext.Name = entityName;
                        continue;
                    }
                }

                if (insideComponentBlock)
                {
                    ParseComponentField(line, ref pendingComponentValue, ref pendingComponentMin, ref pendingComponentMax);
                }
                else
                {
                    BeginParseComponent(line, currentEntityContext,
                        ref pendingComponentType, ref pendingComponentName, ref pendingComponentValue,
                        ref pendingComponentMin, ref pendingComponentMax, entityManager);
                }
                continue;
            }

            // 2. Process Narrative Topology Context
            if (currentNode != null)
            {
                if (colonIndex != -1 && !insideChoiceBlock)
                {
                    if (key == "text")
                    {
                        currentNode.DisplayText = ExtractBetweenQuotes(valuePart);
                        continue;
                    }
                    if (key == "position")
                    {
                        ParsePositionFromValue(valuePart, currentNode);
                        continue;
                    }
                }

                // Node choices can contain text arrows (choice: "Go" -> "node_1"), so handle via its key check
                if (key == "choice")
                {
                    lastCreatedChoice = ParseChoiceFromValue(valuePart, currentNode, temporaryChoicesMap);
                }
                // 3. Process Action Commands inside localized bracket contexts (set:, increase:, reduce:)
                else if (insideChoiceBlock && lastCreatedChoice != null && ContainsActionVerb(line, out var verbStr))
                {
                    ParseAndAddActionCommand(line, verbStr, lastCreatedChoice, entityCache, entityManager);
                }
            }
        }

        LinkChoicePaths(temporaryChoicesMap, nodeManager);
    }

    #region Component Parsing

    private static void BeginParseComponent(string line, Entity entity,
        ref string? pendingType, ref string? pendingName, ref string? pendingValue,
        ref string? pendingMin, ref string? pendingMax, EntityManager entityManager)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex == -1) return;

        var typeTag = line[..colonIndex].Trim().ToLower();
        var rawArgs = line[(colonIndex + 1)..].Trim();

        var name = ExtractBetweenQuotes(rawArgs);
        if (string.IsNullOrEmpty(name))
        {
            // Fallback: try old inline format: "name", value
            var args = ParseCommandArgs(rawArgs);
            if (args.Count >= 2)
            {
                RegisterComponentInline(typeTag, args[0], args[1], null, null, entity, entityManager);
            }
            return;
        }

        // Store pending parameters for block capture
        pendingType = typeTag;
        pendingName = name;
        pendingValue = pendingMin = pendingMax = null;

        // Inline configuration verification
        var nameEndIndex = rawArgs.IndexOf('"', 1);
        if (nameEndIndex > 0)
        {
            var afterName = rawArgs[(nameEndIndex + 1)..].Trim();
            if (afterName.StartsWith(','))
            {
                var args = ParseCommandArgs(rawArgs);
                if (args.Count >= 2)
                {
                    RegisterComponentInline(typeTag, args[0], args[1], null, null, entity, entityManager);
                    pendingType = pendingName = null;
                }
            }
        }
    }

    private static void ParseComponentField(string line, ref string? value, ref string? min, ref string? max)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex == -1) return;

        var key = line[..colonIndex].Trim().ToLower();
        var rawVal = line[(colonIndex + 1)..].Trim();

        switch (key)
        {
            case "value": value = rawVal; break;
            case "min":   min = rawVal; break;
            case "max":   max = rawVal; break;
        }
    }

    private static void FinalizeComponent(string? typeTag, string? name, string? value, string? min, string? max, Entity entity, EntityManager entityManager)
    {
        if (string.IsNullOrEmpty(typeTag) || string.IsNullOrEmpty(name)) return;
        RegisterComponentInline(typeTag, name, value, min, max, entity, entityManager);
    }

    private static void RegisterComponentInline(string typeTag, string name, string? rawValue, string? rawMin, string? rawMax, Entity entity, EntityManager entityManager)
    {
        switch (typeTag)
        {
            case "number":
            {
                double val = 0, minVal = 0, maxVal = double.MaxValue;
                
                if (rawValue != null) double.TryParse(rawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val);
                if (rawMin != null)   double.TryParse(rawMin, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out minVal);
                if (rawMax != null)   double.TryParse(rawMax, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out maxVal);

                entityManager.SetComponent(entity, new NumberComponent { Name = name, Value = val, MinValue = minVal, MaxValue = maxVal });
                break;
            }
            case "text":
            {
                entityManager.SetComponent(entity, new TextComponent { Name = name, Value = UnescapeValue(rawValue ?? string.Empty) });
                break;
            }
            case "boolean":
            {
                bool.TryParse(rawValue, out var val);
                entityManager.SetComponent(entity, new ConditionComponent { Name = name, Value = val });
                break;
            }
        }
    }

    #endregion

    #region Parsing Helpers

    private static (string Type, string Id) ParseHeaderLine(string line)
    {
        var headerContent = line.Trim('[', ']'); 
        var splitIndex = headerContent.IndexOf(':');
        if (splitIndex == -1) return (string.Empty, string.Empty);

        return (headerContent[..splitIndex].Trim(), ExtractBetweenQuotes(headerContent[(splitIndex + 1)..].Trim()));
    }

    private static Entity ResolveEntity(string entityId, Dictionary<string, Entity> entityCache, EntityManager entityManager)
    {
        if (entityCache.TryGetValue(entityId, out var targetEntity)) return targetEntity;
        var id = Guid.TryParse(entityId, out var parsedGuid) ? parsedGuid : Guid.NewGuid();
        targetEntity = entityManager.CreateEntity(id);
        entityCache[entityId] = targetEntity;
        return targetEntity;
    }

    private static void ParsePositionFromValue(string valuePart, IBranchingNode node)
    {
        var (x, y) = ParseCoordinates(valuePart);
        if (x.HasValue && y.HasValue) { node.X = x.Value; node.Y = y.Value; }
    }

    private static void ParsePositionFromValue(string valuePart, Entity entity)
    {
        var (x, y) = ParseCoordinates(valuePart);
        if (x.HasValue && y.HasValue) { entity.X = x.Value; entity.Y = y.Value; }
    }

    private static (double? X, double? Y) ParseCoordinates(string valuePart)
    {
        var coordinates = valuePart.Split(',');
        if (coordinates.Length != 2) return (null, null);
        
        if (double.TryParse(coordinates[0].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(coordinates[1].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var y))
        {
            return (x, y);
        }
        return (null, null);
    }

    private static Choice? ParseChoiceFromValue(string valuePart, IBranchingNode currentNode, List<PendingChoiceLink> temporaryChoicesMap)
    {
        var arrowIndex = valuePart.IndexOf("->", StringComparison.Ordinal);
        if (arrowIndex == -1) return null;

        var choiceText = ExtractBetweenQuotes(valuePart[..arrowIndex]);
        var rightPart = valuePart[(arrowIndex + 2)..].Trim();

        if (rightPart == "null")
        {
            var newChoice = new Choice(choiceText);
            currentNode.Choices.Add(newChoice);
            return newChoice;
        }

        var targetNodeId = ExtractBetweenQuotes(rightPart);
        if (string.IsNullOrEmpty(targetNodeId)) return null;

        var newChoiceWithTarget = new Choice(choiceText);
        currentNode.Choices.Add(newChoiceWithTarget);
        temporaryChoicesMap.Add(new PendingChoiceLink(newChoiceWithTarget, targetNodeId));
        return newChoiceWithTarget;
    }

    private static bool ContainsActionVerb(string line, out string verb)
    {
        verb = string.Empty;
        var colonIndex = line.IndexOf(':');
        if (colonIndex == -1) return false;

        var potentialVerb = line[..colonIndex].Trim().ToLower();
        if (potentialVerb is not ("set" or "increase" or "reduce")) return false;
        verb = potentialVerb;
        return true;
    }

    private static void ParseAndAddActionCommand(string line, string verbStr, Choice targetChoice, Dictionary<string, Entity> entityCache, EntityManager entityManager)
    {
        var body = line[(line.IndexOf(':') + 1)..].Trim();
        var args = ParseCommandArgs(body);
        if (args.Count < 3) return;

        var targetEntity = ResolveEntity(args[0], entityCache, entityManager);
        if (!Enum.TryParse<CommandField>(verbStr, true, out var commandFieldContext)) commandFieldContext = CommandField.Set;

        var targetComponentName = args[1];
        var rawValue = args[2];

        if (double.TryParse(rawValue, System.Globalization.CultureInfo.InvariantCulture, out var numVal))
        {
            targetChoice.Commands.Add(new ModifyNumberCommand { Entity = targetEntity, TargetComponentName = targetComponentName, Value = numVal, Field = commandFieldContext });
        }
        else if (bool.TryParse(rawValue, out var boolVal))
        {
            targetChoice.Commands.Add(new ModifyBooleanCommand { Entity = targetEntity, TargetComponentName = targetComponentName, Value = boolVal, Field = commandFieldContext });
        }
        else
        {
            targetChoice.Commands.Add(new ModifyTextCommand { Entity = targetEntity, TargetComponentName = targetComponentName, Value = UnescapeValue(rawValue), Field = commandFieldContext });
        }
    }

    private static void LinkChoicePaths(List<PendingChoiceLink> temporaryChoicesMap, NodeManager nodeManager)
    {
        foreach (var pendingLink in temporaryChoicesMap)
        {
            if (nodeManager.TryGetNode(pendingLink.TargetId, out var targetNodeInstance))
            {
                pendingLink.ChoiceItem.Node = targetNodeInstance;
            }
        }
    }

    #endregion

    #region String Utilities

    private static string ExtractBetweenQuotes(string input)
    {
        var firstQuote = -1;
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == '"' && (i == 0 || input[i - 1] != '\\')) { firstQuote = i; break; }
        }
        if (firstQuote == -1) return UnescapeText(input.Trim());

        var lastQuote = -1;
        for (var i = input.Length - 1; i > firstQuote; i--)
        {
            if (input[i] == '"' && input[i - 1] != '\\') { lastQuote = i; break; }
        }
        if (lastQuote == -1) return UnescapeText(input.Trim());

        return UnescapeText(input[(firstQuote + 1)..lastQuote]);
    }

    private static string UnescapeText(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                var next = value[i + 1];
                switch (next)
                {
                    case '"':  sb.Append('"');  i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    case 'n':  sb.Append('\n'); i++; break;
                    default:   sb.Append(value[i]); break;
                }
            }
            else sb.Append(value[i]);
        }
        return sb.ToString();
    }

    private static List<string> ParseCommandArgs(string rawArgs)
    {
        var results = new List<string>();
        var currentToken = new StringBuilder();
        var insideQuotes = false;

        for (var i = 0; i < rawArgs.Length; i++)
        {
            var c = rawArgs[i];
            if (c == '"' && i > 0 && rawArgs[i - 1] == '\\')
            {
                currentToken.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': insideQuotes = !insideQuotes; continue;
                case ',' when !insideQuotes:
                    results.Add(currentToken.ToString().Trim());
                    currentToken.Clear();
                    continue;
                default: currentToken.Append(c); break;
            }
        }
        results.Add(currentToken.ToString().Trim());
        return results;
    }

    private static string UnescapeValue(string rawValue)
    {
        if (string.IsNullOrEmpty(rawValue)) return rawValue;
        if (rawValue.Length >= 2 && rawValue[0] == '"' && rawValue[^1] == '"')
        {
            return ExtractBetweenQuotes(rawValue);
        }
        return UnescapeText(rawValue.Trim());
    }

    #endregion

    private sealed record PendingChoiceLink(IChoice ChoiceItem, string TargetId);
}