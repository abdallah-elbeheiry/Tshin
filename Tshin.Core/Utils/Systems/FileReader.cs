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
        string? pendingComponentVisible = null; // Added tracking variable

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var rawLine = lines[lineIndex];
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
                        pendingComponentValue, pendingComponentMin, pendingComponentMax, pendingComponentVisible,
                        currentEntityContext, entityManager);
                    
                    pendingComponentType = pendingComponentName = pendingComponentValue = pendingComponentMin = pendingComponentMax = pendingComponentVisible = null;
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
                pendingComponentType = pendingComponentName = pendingComponentValue = pendingComponentMin = pendingComponentMax = pendingComponentVisible = null;
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
                    if (key == "visible") // Process Entity Visibility
                    {
                        if (bool.TryParse(valuePart, out var isVisible)) currentEntityContext.Visible = isVisible;
                        continue;
                    }
                }

                if (insideComponentBlock)
                {
                    ParseComponentField(line, ref pendingComponentValue, ref pendingComponentMin, ref pendingComponentMax, ref pendingComponentVisible);
                }
                else
                {
                    BeginParseComponent(line, currentEntityContext,
                        ref pendingComponentType, ref pendingComponentName, ref pendingComponentValue,
                        ref pendingComponentMin, ref pendingComponentMax, ref pendingComponentVisible, entityManager);
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

                if (key == "choice")
                {
                    lastCreatedChoice = ParseChoiceFromValue(valuePart, currentNode, temporaryChoicesMap);
                }
                // 3. Process Requirement condition tree inside bracket contexts
                else if (insideChoiceBlock && lastCreatedChoice != null && key == "require")
                {
                    var condition = ParseRequirementExpression(valuePart, lines, ref lineIndex);
                    if (condition is not null)
                        lastCreatedChoice.Condition = condition;
                }
                // 4. Process Action Commands inside localized bracket contexts (set, increase, reduce)
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
        ref string? pendingMin, ref string? pendingMax, ref string? pendingVisible, EntityManager entityManager)
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
                RegisterComponentInline(typeTag, args[0], args[1], null, null, null, entity, entityManager);
            }
            return;
        }

        // Store pending parameters for block capture
        pendingType = typeTag;
        pendingName = name;
        pendingValue = pendingMin = pendingMax = pendingVisible = null;

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
                    RegisterComponentInline(typeTag, args[0], args[1], null, null, null, entity, entityManager);
                    pendingType = pendingName = null;
                }
            }
        }
    }

    private static void ParseComponentField(string line, ref string? value, ref string? min, ref string? max, ref string? visible)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex == -1) return;

        var key = line[..colonIndex].Trim().ToLower();
        var rawVal = line[(colonIndex + 1)..].Trim();

        switch (key)
        {
            case "value":   value = rawVal; break;
            case "min":     min = rawVal; break;
            case "max":     max = rawVal; break;
            case "visible": visible = rawVal; break; // Process localized component visibility
        }
    }

    private static void FinalizeComponent(string? typeTag, string? name, string? value, string? min, string? max, string? visible, Entity entity, EntityManager entityManager)
    {
        if (string.IsNullOrEmpty(typeTag) || string.IsNullOrEmpty(name)) return;
        RegisterComponentInline(typeTag, name, value, min, max, visible, entity, entityManager);
    }

    private static void RegisterComponentInline(string typeTag, string name, string? rawValue, string? rawMin, string? rawMax, string? rawVisible, Entity entity, EntityManager entityManager)
    {
        // Default visibility context to true if field is missing or completely skipped
        var isVisible = true;
        if (rawVisible != null) bool.TryParse(rawVisible, out isVisible);

        switch (typeTag)
        {
            case "number":
            {
                double val = 0, minVal = 0, maxVal = double.MaxValue;
                
                if (rawValue != null) double.TryParse(rawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val);
                if (rawMin != null)   double.TryParse(rawMin, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out minVal);
                if (rawMax != null)   double.TryParse(rawMax, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out maxVal);

                entityManager.SetComponent(entity, new NumberComponent { Name = name, Value = val, MinValue = minVal, MaxValue = maxVal, Visible = isVisible });
                break;
            }
            case "text":
            {
                entityManager.SetComponent(entity, new TextComponent { Name = name, Value = UnescapeValue(rawValue ?? string.Empty), Visible = isVisible });
                break;
            }
            case "boolean":
            case "condition":
            {
                bool.TryParse(rawValue, out var val);
                entityManager.SetComponent(entity, new ConditionComponent { Name = name, Value = val, Visible = isVisible });
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
        var spaceIndex = line.IndexOf(' ');
        if (spaceIndex == -1) return false;

        var potentialVerb = line[..spaceIndex].Trim().ToLower();
        if (potentialVerb is not ("set" or "increase" or "reduce")) return false;
        verb = potentialVerb;
        return true;
    }

    private static void ParseAndAddActionCommand(string line, string verbStr, Choice targetChoice, Dictionary<string, Entity> entityCache, EntityManager entityManager)
    {
        var args = ParseQuoteTokens(line);
        // args[0] is the verb, skip it
        if (args.Count < 4) return;

        var targetEntity = ResolveEntity(args[1], entityCache, entityManager);
        if (!Enum.TryParse<CommandField>(verbStr, true, out var commandFieldContext)) commandFieldContext = CommandField.Set;

        var targetComponentName = args[2];
        var rawValue = args[3];

        if (double.TryParse(rawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numVal))
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

    #region Quote-Driven Tokenizer & Requirement Parsing

    /// <summary>
    /// Tokenizes a line by extracting content between paired double-quote characters
    /// using sequential <see cref="string.IndexOf(char, int)"/> scans.
    /// The remainder after the last closing quote is returned as a final token.
    /// This approach prevents multi-space anomalies from corrupting relative array indices.
    /// </summary>
    /// <param name="line">The input line to tokenize.</param>
    /// <returns>A list of string tokens parsed from the line.</returns>
    private static List<string> ParseQuoteTokens(string line)
    {
        var tokens = new List<string>();
        var searchStart = 0;

        while (searchStart < line.Length)
        {
            var openQuote = line.IndexOf('"', searchStart);
            if (openQuote == -1)
            {
                // No more quotes — split remaining segment by whitespace
                var remainder = line[searchStart..].Trim();
                if (remainder.Length > 0)
                {
                    tokens.AddRange(remainder.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
                }
                break;
            }

            // If there is non-whitespace text before this quote, split it by whitespace
            if (openQuote > searchStart)
            {
                var before = line[searchStart..openQuote].Trim();
                if (before.Length > 0)
                {
                    tokens.AddRange(before.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
                }
            }

            var closeQuote = line.IndexOf('"', openQuote + 1);
            if (closeQuote == -1)
            {
                // Unmatched quote — treat as literal rest of line then split by whitespace
                var unmatched = line[(openQuote + 1)..].Trim();
                if (unmatched.Length > 0)
                {
                    tokens.AddRange(unmatched.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
                }
                break;
            }

            tokens.Add(line[(openQuote + 1)..closeQuote]);
            searchStart = closeQuote + 1;
        }

        return tokens;
    }

    /// <summary>
    /// Normalizes mixed line-ending styles to Unix-style <c>\n</c>.
    /// Replaces <c>\r\n</c> and bare <c>\r</c> with <c>\n</c> so that
    /// downstream newline-based splitters behave consistently across macOS, Linux, and Windows.
    /// </summary>
    /// <param name="content">The raw multi-line string content to normalize.</param>
    /// <returns>A string with all line-endings converted to <c>\n</c>.</returns>
    private static string NormalizeLineEndings(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        return content.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    /// <summary>
    /// Parses a <c>require:</c> definition into a recursive <see cref="IConditionComponentNode"/> tree.
    /// If the initial expression has unbalanced parentheses, additional lines are consumed from
    /// <paramref name="lines"/> (starting at <paramref name="lineIndex"/>) until structural balance
    /// is restored. All line endings are normalized before processing.
    /// </summary>
    /// <param name="initialValue">The value part of the <c>require:</c> line.</param>
    /// <param name="lines">The full array of source lines being processed.</param>
    /// <param name="lineIndex">The current line index. Updated when additional lines are consumed.</param>
    /// <returns>
    /// The root <see cref="IConditionComponentNode"/> of the parsed condition tree,
    /// or <see langword="null"/> if the expression is empty or unparseable.
    /// </returns>
    private static IConditionComponentNode? ParseRequirementExpression(string initialValue, string[] lines, ref int lineIndex)
    {
        var aggregated = new StringBuilder(initialValue);

        // Check for balanced parentheses; aggregate lines if needed
        var depth = 0;
        foreach (var c in initialValue)
        {
            if (c == '(') depth++;
            else if (c == ')') depth--;
        }

        while (depth > 0 && lineIndex + 1 < lines.Length)
        {
            lineIndex++;
            var nextLine = lines[lineIndex].Split('#')[0];
            aggregated.Append('\n').Append(nextLine);

            foreach (var c in nextLine)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
            }
        }

        var fullExpression = NormalizeLineEndings(aggregated.ToString().Trim());
        if (string.IsNullOrEmpty(fullExpression))
            return null;

        return BuildConditionTree(fullExpression);
    }

    /// <summary>
    /// Recursively builds a condition tree from a requirement expression string.
    /// Supports top-level <c>or(...)</c> and <c>and(...)</c> wrappers that can be nested.
    /// Atomic argument lines are separated by newlines at depth 0 of the parent wrapper.
    /// </summary>
    /// <param name="expression">The requirement expression string with normalized line endings.</param>
    /// <returns>
    /// An <see cref="IConditionComponentNode"/> representing the parsed expression,
    /// or <see langword="null"/> if the expression cannot be parsed.
    /// </returns>
    private static IConditionComponentNode? BuildConditionTree(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return null;

        var trimmed = expression.Trim();

        // Detect logical wrapper: or(...) or and(...)
        if (trimmed.StartsWith("or(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            var inner = trimmed[3..^1]; // strip "or(" and ")"
            return new LogicalGroupNode
            {
                IsAnd = false,
                Children = ParseConditionChildren(inner)
            };
        }

        if (trimmed.StartsWith("and(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            var inner = trimmed[4..^1]; // strip "and(" and ")"
            return new LogicalGroupNode
            {
                IsAnd = true,
                Children = ParseConditionChildren(inner)
            };
        }

        // Atomic condition line: "EntityId" "ComponentId" <op> <value>
        return ParseAtomicCondition(trimmed);
    }

    /// <summary>
    /// Splits the inner content of an <c>or(...)</c> or <c>and(...)</c> wrapper into
    /// individual child expressions using depth-based newline splitting.
    /// Child expression strings are then recursively parsed via <see cref="BuildConditionTree"/>.
    /// </summary>
    /// <param name="innerContent">The content between the wrapper parentheses, with normalized newlines.</param>
    /// <returns>A list of child condition nodes.</returns>
    private static List<IConditionComponentNode> ParseConditionChildren(string innerContent)
    {
        var children = new List<IConditionComponentNode>();
        var parts = DepthSplitNewlines(innerContent);

        foreach (var part in parts)
        {
            var child = BuildConditionTree(part);
            if (child is not null)
                children.Add(child);
        }

        return children;
    }

    /// <summary>
    /// Splits a requirement expression into sub-expressions on newline characters,
    /// but only when the tracking parenthesis depth is exactly zero.
    /// Stray whitespace is trimmed and blank entries are discarded.
    /// </summary>
    /// <param name="content">The normalized (Unix newlines) inner content to split.</param>
    /// <returns>A list of trimmed, non-empty sub-expression strings.</returns>
    private static List<string> DepthSplitNewlines(string content)
    {
        var results = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            switch (c)
            {
                case '(':
                    depth++;
                    current.Append(c);
                    break;

                case ')':
                    depth--;
                    current.Append(c);
                    break;

                case '\n' when depth == 0:
                    var segment = current.ToString().Trim();
                    if (segment.Length > 0)
                        results.Add(segment);
                    current.Clear();
                    break;

                default:
                    current.Append(c);
                    break;
            }
        }

        // Flush the last segment
        var lastSegment = current.ToString().Trim();
        if (lastSegment.Length > 0)
            results.Add(lastSegment);

        return results;
    }

    /// <summary>
    /// Parses an atomic condition line into an <see cref="AtomicConditionNode"/>.
    /// Expected format: <c>"EntityId" "ComponentId" &lt;op&gt; &lt;value&gt;</c>
    /// </summary>
    /// <param name="line">The trimmed atomic condition line.</param>
    /// <returns>An <see cref="AtomicConditionNode"/> if parsing succeeds; otherwise, <see langword="null"/>.</returns>
    private static IConditionComponentNode? ParseAtomicCondition(string line)
    {
        var tokens = ParseQuoteTokens(line);
        // tokens: [0]=EntityId, [1]=ComponentId, [2]=operator, [3]=value
        if (tokens.Count < 4)
            return null;

        return new AtomicConditionNode
        {
            EntityId = tokens[0],
            ComponentId = tokens[1],
            Operator = tokens[2],
            TargetValue = tokens[3]
        };
    }

    #endregion

    private sealed record PendingChoiceLink(IChoice ChoiceItem, string TargetId);
}
