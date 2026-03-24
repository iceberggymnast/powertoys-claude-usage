// Copyright (c) DockBar
// Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DockBar;

/// <summary>
/// Triggers a manual refresh of Claude usage data.
/// Enforces a 1-minute cooldown to avoid hammering the API.
/// </summary>
internal sealed partial class RefreshCommand : InvokableCommand
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    private readonly ClaudeUsageDockBand _band;
    private DateTime _lastRefresh = DateTime.MinValue;

    internal RefreshCommand(ClaudeUsageDockBand band)
    {
        _band = band;
        Name  = "Refresh";
    }

    public override CommandResult Invoke()
    {
        _ = InvokeAsync();
        return CommandResult.KeepOpen();
    }

    private async Task InvokeAsync()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastRefresh;

        if (elapsed < Cooldown)
        {
            var wait = Cooldown - elapsed;
            _band.SetRefreshStatus($"Wait {(int)wait.TotalMinutes}m {wait.Seconds:D2}s");
            return;
        }

        _lastRefresh = now;
        _band.SetRefreshStatus("Refreshing…");
        await _band.RefreshAsync().ConfigureAwait(false);
        _band.SetRefreshStatus("Refresh");
    }
}
