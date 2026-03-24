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

        try
        {
            UsageData? data = await ClaudeUsageService.FetchAsync(token).ConfigureAwait(false);

            if (data is not null)
            {
                _cached  = data;
                _isStale = false;
            }
            else if (_cached is not null)
            {
                // null means 429 / transient error — keep existing cache, mark stale
                _isStale = true;
            }
            else
            {
                // No cache yet, nothing to show
                _sessionItem.Title    = "Session –";
                _sessionItem.Subtitle = "Unable to fetch usage";
                _weeklyItem.Title     = "Weekly –";
                _weeklyItem.Subtitle  = "Unable to fetch usage";
                return;
            }
        }
        catch
        {
            if (_cached is null)
            {
                _sessionItem.Title    = "Session –";
                _sessionItem.Subtitle = "Unable to fetch usage";
                _weeklyItem.Title     = "Weekly –";
                _weeklyItem.Subtitle  = "Unable to fetch usage";
                return;
            }
            _isStale = true;
        }

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_cached is null)
            return;

        string staleSuffix  = _isStale ? " ·" : string.Empty;
        string sessionReset = FormatTimeSpan(_cached.FiveHourReset.ToUniversalTime() - DateTime.UtcNow);
        string weeklyReset  = FormatTimeSpan(_cached.SevenDayReset.ToUniversalTime()  - DateTime.UtcNow);

        _sessionItem.Title    = $"Session {_cached.FiveHourPct:0}%{staleSuffix}";
        _sessionItem.Subtitle = $"resets in {sessionReset}";

        _weeklyItem.Title    = $"Weekly {_cached.SevenDayPct:0}%{staleSuffix}";
        _weeklyItem.Subtitle = $"resets in {weeklyReset}";
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
