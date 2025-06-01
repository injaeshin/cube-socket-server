using System.Text;
using Cube.Packet;

namespace Cube.Core;

public class CoreHelper
{
    public static string CreateSessionId()
    {
        // 세션의 전체 크기는 shortChar 1byte + randomPart 3byte + shortTime 4byte = 8byte
        string shortChar = ((char)('a' + DateTime.Now.Hour)).ToString();
        string randomPart = Guid.NewGuid().ToString("N")[..3];
        string shortTime = DateTime.Now.ToString("HHmm");

        return $"{shortChar}{randomPart}{shortTime}";
    }
}