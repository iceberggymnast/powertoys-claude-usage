// Copyright (c) DockBar
// Licensed under the MIT license.

using System;
using System.IO;
using System.Text.Json;

namespace DockBar;

/// <summary>
/// Reads the Claude Code OAuth access token from ~/.claude/.credentials.json
/// </summary>
internal static class CredentialHelper
{
    private static readonly string CredentialsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude",
        ".credentials.json");

    /// <summary>
    /// Returns the Claude OAuth access token, or <c>null</c> if not found / parse error.
    /// </summary>
    public static string? GetClaudeAccessToken()
    {
        try
        {
            if (!File.Exists(CredentialsPath))
                return null;

            string json = File.ReadAllText(CredentialsPath);

            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("claudeAiOauth", out JsonElement oauthElem) &&
                oauthElem.TryGetProperty("accessToken", out JsonElement tokenElem))
            {
                return tokenElem.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
