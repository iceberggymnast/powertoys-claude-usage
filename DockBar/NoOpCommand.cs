// Copyright (c) DockBar
// Licensed under the MIT license.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DockBar;

/// <summary>
/// A command that does nothing. Used as a placeholder for display-only ListItems.
/// </summary>
internal sealed partial class NoOpCommand : InvokableCommand
{
    internal NoOpCommand()
    {
        Name = string.Empty;
    }

    public override CommandResult Invoke() => CommandResult.KeepOpen();
}
