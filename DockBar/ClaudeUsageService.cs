// Copyright (c) DockBar
// Licensed under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DockBar;

internal record UsageData(
    double FiveHourPct,
    DateTime FiveHourReset,
    double SevenDayPct,
    DateTime SevenDayReset);

internal record FetchResult(UsageData? Data, string? Error);

internal static class ClaudeUsageService
{
    private static readonly HttpClient _http = new();
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";

    public static async Task<FetchResult> FetchAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new FetchResult(null, $"Network error: {ex.Message.Split('.')[0]}");
        }

        if (!response.IsSuccessStatusCode)
        {
            return new FetchResult(null, $"HTTP {(int)response.StatusCode}");
        }

        try
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;

            double fivePct = root.GetProperty("five_hour").GetProperty("utilization").GetDouble();
            DateTime fiveReset = DateTime.Parse(
                root.GetProperty("five_hour").GetProperty("resets_at").GetString()!,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind);

            double sevenPct = root.GetProperty("seven_day").GetProperty("utilization").GetDouble();
            DateTime sevenReset = DateTime.Parse(
                root.GetProperty("seven_day").GetProperty("resets_at").GetString()!,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind);

            return new FetchResult(new UsageData(fivePct, fiveReset, sevenPct, sevenReset), null);
        }
        catch
        {
            return new FetchResult(null, "Parse error");
        }
    }
}
