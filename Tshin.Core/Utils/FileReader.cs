using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tshin.Core.Models;

namespace Tshin.Core.Utils;

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
            
            // Skip comments or completely empty buffer lines
            if (string.IsNullOrEmpty(line) || line.StartsWith($"#")) continue;

            //Catch Block Headers: e.g., [StoryNode: "uuid"]
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var headerContent = line.Trim('[', ']'); // Removes brackets
                var splitIndex = headerContent.IndexOf(':');
                if (splitIndex == -1) continue;

                var nodeTypeName = headerContent[..splitIndex].Trim();
                var rawId = headerContent[(splitIndex + 1)..].Trim();
                var id = ExtractBetweenQuotes(rawId);

                if (nodeTypeName == "StoryNode")
                {
                    var storyNode = NodeFactory.CreateNode(NodeType.StoryNode, id);
                    NodeManager.AppendNode(storyNode);
                    currentNode = (IBranchingNode?)storyNode;
                }
                // Extend easily here later: else if (nodeTypeName == "EventNode") { ... }
                continue;
            }

            if (currentNode == null) continue;

            if (line.StartsWith("text:"))
            {
                var textPart = line["text:".Length..].Trim();
                // Restore literal '\n' patterns back into standard UI system line breaks
                currentNode.DisplayText = ExtractBetweenQuotes(textPart).Replace("\\n", Environment.NewLine);
            }

            else if (line.StartsWith("choice:"))
            {
                var arrowIndex = line.IndexOf("->", StringComparison.Ordinal);
                if (arrowIndex == -1) continue;

                var leftPart = line[..arrowIndex];
                var rightPart = line[arrowIndex..];

                var choiceText = ExtractBetweenQuotes(leftPart);
                var targetNodeId = ExtractBetweenQuotes(rightPart);

                var newChoice = new Choice(currentNode, choiceText);
                NodeManager.AddChoice(newChoice, currentNode);

                temporaryChoicesMap.Add(new PendingChoiceLink(newChoice, targetNodeId));
            }
        }

        foreach (var pendingLink in temporaryChoicesMap)
        {
            var targetNodeInstance = NodeManager.GetNode(pendingLink.TargetId);
            NodeManager.ModifyChoicePath(pendingLink.ChoiceItem, targetNodeInstance);
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
        return input;
    }

    // Small interior record structure strictly used for Pass 2 processing bookkeeping
    private sealed record PendingChoiceLink(IChoice ChoiceItem, string TargetId);
}