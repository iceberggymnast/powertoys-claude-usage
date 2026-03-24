// Copyright (c) DockBar
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DockBar;

/// <summary>
/// A Dock band that displays Claude 5-hour session and 7-day weekly usage.
/// Updates every 60 seconds. Shows stale data (with a trailing "·") on HTTP 429.
/// </summary>
internal sealed partial class ClaudeUsageDockBand : WrappedDockItem
{
    private readonly ListItem _sessionItem;
    private readonly ListItem _weeklyItem;

    private UsageData? _cached;
    private bool _isStale;
    private string? _lastError;

    private readonly Timer _timer;

    public ClaudeUsageDockBand()
        : base([], "com.dockbar.claude.usage", "Claude Usage")
    {
        _sessionItem = new ListItem(new NoOpCommand()) { Title = "Session –", Subtitle = "Loading…", Icon = new IconInfo("⚡") };
        _weeklyItem  = new ListItem(new NoOpCommand()) { Title = "Weekly –",  Subtitle = "Loading…", Icon = new IconInfo("📅") };

        Items = [_sessionItem, _weeklyItem];

        // Fire immediately (dueTime=0), then repeat every 60 s
        _timer = new Timer(OnTimer, null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
    }

    private void OnTimer(object? _)
    {
        // Fire-and-forget; exceptions are swallowed to keep the timer alive
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        string? token = CredentialHelper.GetClaudeAccessToken();

        if (token is null)
        {
            _sessionItem.Title    = "Session –";
            _sessionItem.Subtitle = "Claude Code not logged in";
            _weeklyItem.Title     = "Weekly –";
            _weeklyItem.Subtitle  = "Claude Code not logged in";
            return;
        }

        FetchResult result = await ClaudeUsageService.FetchAsync(token).ConfigureAwait(false);

        if (result.Data is not null)
        {
            _cached     = result.Data;
            _isStale    = false;
            _lastError  = null;
        }
        else if (_cached is not null)
        {
            _isStale   = true;
            _lastError = result.Error;
        }
        else
        {
            // No cache yet — show specific error
            string err = result.Error ?? "Unknown error";
            _sessionItem.Title    = "Session";
            _sessionItem.Subtitle = err;
            _weeklyItem.Title     = "Weekly";
            _weeklyItem.Subtitle  = err;
            return;
        }

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_cached is null)
            return;

        string sessionReset = FormatTimeSpan(_cached.FiveHourReset.ToUniversalTime() - DateTime.UtcNow);
        string weeklyReset  = FormatTimeSpan(_cached.SevenDayReset.ToUniversalTime()  - DateTime.UtcNow);
        string staleSuffix  = _isStale && _lastError is not null ? $" ({_lastError})" : string.Empty;

        _sessionItem.Title    = $"Session {_cached.FiveHourPct:0}%";
        _sessionItem.Subtitle = $"resets in {sessionReset}{staleSuffix}";

        _weeklyItem.Title    = $"Weekly {_cached.SevenDayPct:0}%";
        _weeklyItem.Subtitle = $"resets in {weeklyReset}{staleSuffix}";
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalSeconds <= 0)
            return "now";

        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h";

        return $"{ts.Hours}h {ts.Minutes}m";
    }
}
