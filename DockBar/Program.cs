// Copyright (c) DockBar
// Licensed under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;

namespace DockBar;

public class Program
{
    [MTAThread]
    public static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            using var mutex = new Mutex(true, "Global\\DockBarClaudeUsage_ComServer", out bool createdNew);
            if (!createdNew)
                return;

            await using ComServer server = new();

            ManualResetEvent extensionDisposedEvent = new(false);

            DockBarExtension extensionInstance = new(extensionDisposedEvent);
            server.RegisterClass<DockBarExtension, IExtension>(() => extensionInstance);
            server.Start();

            extensionDisposedEvent.WaitOne();
        }
    }
}
