using Tshin.Core.Models;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Commands;

public interface ICommand
{
    CommandField Field { get; }
    // Pass the EntityManager into the execution context so the command can find the component bag
    void Execute();
}