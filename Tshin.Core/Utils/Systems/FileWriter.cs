using System.Text;
using Tshin.Core.Models;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Systems;

public static class FileWriter
{
    public static async Task SaveFileAsync(string filePath)
    {
        var nodes = NodeManager.GetNodes();

        await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        foreach (var node in nodes)
        {
            await writer.WriteLineAsync($"[{node.NodeType}: \"{node.Id}\"]");

            var escapedText = node.DisplayText.Replace("\r", "").Replace("\n", "\\n");
            await writer.WriteLineAsync($"text: \"{escapedText}\"");
            // Inside FileWriter.cs loop:
            await writer.WriteLineAsync(
                $"position: {node.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},{node.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

            if (node is IBranchingNode branchingNode)
            {
                var choices = NodeManager.GetChoices(branchingNode);
                foreach (var choice in choices)
                {
                    if (choice.Node != null)
                    {
                        await writer.WriteLineAsync($"choice: \"{choice.DisplayText}\"->\"{choice.Node.Id}\"");
                    }
                    else
                    {
                        // Optional: Write something even for null nodes if we want to preserve the choice
                        // For now let's just skip it as it's an invalid state for the story
                    }
                }
            }

            await writer.WriteLineAsync();
        }
    }
}