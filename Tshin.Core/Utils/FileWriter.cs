using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tshin.Core.Models;

namespace Tshin.Core.Utils;

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