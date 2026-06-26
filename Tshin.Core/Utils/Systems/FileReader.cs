using System.Text;
using Tshin.Core.Models;
using Tshin.Core.Utils.Factories;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Systems;

public static class FileReader
{
    public static async Task LoadFileAsync(string filePath)
    {
        NodeManager.ClearNodes();

        if (!File.Exists(filePath)) return;

        var temporaryChoicesMap = new List<PendingChoiceLink>();
        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
        IBranchingNode? currentNode = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var headerContent = line.Trim('[', ']'); // Removes bracket wrappers
                var splitIndex = headerContent.IndexOf(':');
                if (splitIndex == -1) continue;

                var nodeTypeName = headerContent[..splitIndex].Trim();
                var rawId = headerContent[(splitIndex + 1)..].Trim();
                var id = ExtractBetweenQuotes(rawId);

                if (string.IsNullOrEmpty(id)) continue;

                if (nodeTypeName == "StoryNode")
                {
                    if (NodeManager.GetNodeIds().Contains(id)) continue;

                    var storyNode = NodeFactory.CreateNode(NodeType.StoryNode, id);
                    currentNode = (IBranchingNode?)storyNode;
                }
                // Extend easily here later: else if (nodeTypeName == "EventNode") { ... }
                continue;
            }

            if (currentNode == null) continue;

            if (line.StartsWith("text:"))
            {
                var textPart = line["text:".Length..].Trim();
                currentNode.DisplayText = ExtractBetweenQuotes(textPart).Replace("\\n", Environment.NewLine);
            }

            else if (line.StartsWith("choice:"))
            {
                var arrowIndex = line.IndexOf("->", StringComparison.Ordinal);
                if (arrowIndex == -1) continue;

                var leftPart = line[..arrowIndex];
                var rightPart = line[(arrowIndex + 2)..];

                var choiceText = ExtractBetweenQuotes(leftPart).Replace("\\n", Environment.NewLine);
                var targetNodeId = ExtractBetweenQuotes(rightPart);

                if (string.IsNullOrEmpty(targetNodeId)) continue;
                var newChoice = new Choice(currentNode, choiceText);
                NodeManager.AddChoice(newChoice, currentNode);
                temporaryChoicesMap.Add(new PendingChoiceLink(newChoice, targetNodeId));
            }
            
            else if (line.StartsWith("position:"))
            {
                var coordinatesPart = line["position:".Length..].Trim();
    
                var coordinates = coordinatesPart.Split(',');

                if (coordinates.Length != 2) continue;
                if (double.TryParse(coordinates[0].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                    double.TryParse(coordinates[1].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var y))
                {
                    currentNode.X = x;
                    currentNode.Y = y;
                }
            }
        }

        foreach (var pendingLink in temporaryChoicesMap)
        {
            if (NodeManager.TryGetNode(pendingLink.TargetId, out var targetNodeInstance))
            {
                NodeManager.ModifyChoicePath(pendingLink.ChoiceItem, targetNodeInstance);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Target node '{pendingLink.TargetId}' not found for a choice.");
            }
        }
    }

    private static string ExtractBetweenQuotes(string input)
    {
        var firstQuote = input.IndexOf('"');
        var lastQuote = input.LastIndexOf('"');

        if (firstQuote != -1 && lastQuote != -1 && firstQuote < lastQuote)
        {
            return input.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        }
        return string.Empty; 
    }

    private sealed record PendingChoiceLink(IChoice ChoiceItem, string TargetId);
}