namespace Tshin.Core.Models;

public interface IChoice
{
    INode Node { get; set; }
    string DisplayText { get; set; }
}