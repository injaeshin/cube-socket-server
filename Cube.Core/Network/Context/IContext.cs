
namespace Cube.Core.Network;

public interface IContext
{
    string SessionId { get; }
    void Return();
}
