// Copyright (c) DockBar
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DockBar;

/// <summary>
/// A Dock band that displays Claude 5-hour session and 7-day weekly usage.
/// Polls every 5 minutes. Manual refresh button with 1-minute cooldown.
/// Shows stale data (with error suffix) when API call fails.
/// </summary>
internal sealed partial class ClaudeUsageDockBand : WrappedDockItem
{
    private readonly ListItem _sessionItem;
    private readonly ListItem _weeklyItem;
    private readonly ListItem _refreshItem;
    private readonly RefreshCommand _refreshCommand;

    private UsageData? _cached;
    private bool _isStale;
    private string? _lastError;
    private int _failureCount;

    private static readonly TimeSpan BaseInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxInterval  = TimeSpan.FromMinutes(60);

    private readonly Timer _timer;

    public ClaudeUsageDockBand()
        : base([], "com.dockbar.claude.usage", "Claude Usage")
    {
        _sessionItem = new ListItem(new NoOpCommand())
        {
            Title    = "Session –",
            Subtitle = "Loading…",
            Icon     = new IconInfo("⚡"),
        };
        _weeklyItem = new ListItem(new NoOpCommand())
        {
            Title    = "Weekly –",
            Subtitle = "Loading…",
            Icon     = new IconInfo("📅"),
        };

        _refreshCommand = new RefreshCommand(this);
        _refreshItem = new ListItem(_refreshCommand)
        {
            Title    = "↻",
            Subtitle = "Refresh",
            Icon     = new IconInfo("↻"),
        };

        Items = [_sessionItem, _weeklyItem, _refreshItem];

        // Fire immediately; period is Infinite — rescheduled dynamically after each result
        _timer = new Timer(OnTimer, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    private void OnTimer(object? _) => _ = RefreshAsync(isManual: false);

    internal async Task RefreshAsync(bool isManual = false)
    {
        string? token = CredentialHelper.GetClaudeAccessToken();

        if (token is null)
        {
            SetNoToken();
            if (!isManual) ScheduleNext(is429: false);
            return;
        }

        FetchResult result = await ClaudeUsageService.FetchAsync(token).ConfigureAwait(false);

        if (result.Data is not null)
        {
            _cached    = result.Data;
            _isStale   = false;
            _lastError = null;
            ScheduleNext(is429: false);
        }
        else if (_cached is not null)
        {
            _isStale   = true;
            _lastError = result.Error;
            ScheduleNext(is429: result.Is429);
        }
        else
        {
            SetError(result.Error ?? "Unknown error");
            ScheduleNext(is429: result.Is429);
            return;
        }

        UpdateDisplay();
    }

    private void ScheduleNext(bool is429)
    {
        TimeSpan delay;
        if (is429)
        {
            _failureCount++;
            double minutes = Math.Min(5 * Math.Pow(2, _failureCount - 1), MaxInterval.TotalMinutes);
            delay = TimeSpan.FromMinutes(minutes);
            _refreshItem.Subtitle = $"Retry in {(int)delay.TotalMinutes}m";
        }
        else
        {
            _failureCount = 0;
            delay = BaseInterval;
            _refreshItem.Subtitle = "Refresh";
        }
        _timer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    private void SetNoToken()
    {
        const string msg = "Claude Code not logged in";
        _sessionItem.Title    = "Session –";
        _sessionItem.Subtitle = msg;
        _weeklyItem.Title     = "Weekly –";
        _weeklyItem.Subtitle  = msg;
        SetDetails(_sessionItem, "Session –", msg);
        SetDetails(_weeklyItem,  "Weekly –",  msg);
    }

    private void SetError(string error)
    {
        _sessionItem.Title    = "Session –";
        _sessionItem.Subtitle = error;
        _weeklyItem.Title     = "Weekly –";
        _weeklyItem.Subtitle  = error;
        SetDetails(_sessionItem, "Session –", error);
        SetDetails(_weeklyItem,  "Weekly –",  error);
    }

    private void UpdateDisplay()
    {
        if (_cached is null)
            return;

        string sessionReset = FormatTimeSpan(_cached.FiveHourReset.ToUniversalTime() - DateTime.UtcNow);
        string weeklyReset  = FormatTimeSpan(_cached.SevenDayReset.ToUniversalTime()  - DateTime.UtcNow);
        string errSuffix    = _isStale && _lastError is not null ? $" ({_lastError})" : string.Empty;

        string sessionTitle    = $"Session {_cached.FiveHourPct:0}%";
        string sessionSubtitle = $"resets in {sessionReset}{errSuffix}";
        string weeklyTitle     = $"Weekly {_cached.SevenDayPct:0}%";
        string weeklySubtitle  = $"resets in {weeklyReset}{errSuffix}";

        _sessionItem.Title    = sessionTitle;
        _sessionItem.Subtitle = sessionSubtitle;
        _weeklyItem.Title     = weeklyTitle;
        _weeklyItem.Subtitle  = weeklySubtitle;

        // Update hover details with full untruncated text
        SetDetails(_sessionItem, sessionTitle, sessionSubtitle);
        SetDetails(_weeklyItem,  weeklyTitle,  weeklySubtitle);
    }

    internal void SetRefreshStatus(string subtitle)
    {
        _refreshItem.Subtitle = subtitle;
    }

    private static void SetDetails(ListItem item, string title, string body)
    {
        item.Details = new Details { Title = title, Body = body };
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
