// Copyright (c) DockBar
// Licensed under the MIT license.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DockBar;

public partial class DockBarCommandProvider : CommandProvider
{
    private readonly ClaudeUsageDockBand _band = new();

    public DockBarCommandProvider()
    {
        DisplayName = "Claude Usage";
        Icon = new IconInfo("\uE899"); // People / person icon
    }

    public override ICommandItem[] TopLevelCommands() => [];

    public override ICommandItem[] GetDockBands() => [_band];
}
