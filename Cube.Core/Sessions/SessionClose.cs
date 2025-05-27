using System;
using Cube.Common.Interface;

namespace Cube.Core.Sessions;

public class SessionClose(SocketDisconnect reason, bool isGraceful = false) : ISessionClose
{
    public int Code { get; set; } = (int)reason;
    public bool IsGraceful { get; set; } = isGraceful;
    public string Description { get; set; } = reason.ToString();

    public override string ToString()
    {
        return $"{Code} - {Description} - {IsGraceful}";
    }
}
