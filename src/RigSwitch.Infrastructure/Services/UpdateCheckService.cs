namespace RigSwitch.Infrastructure.Services;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using RigSwitch.Core.Interfaces;
using RigSwitch.Core.Models;

/// <summary>
/// Service that checks GitHub / winget releases via REST API to detect newer application updates.
/// </summary>
public sealed class UpdateCheckService : IUpdateCheckService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _releaseApiUrl;
    private readonly bool _disposeClient;
    private bool _disposed;

    /// <summary>
    /// Default GitHub API endpoint for the latest release of RigSwitch.
    /// </summary>
    public const string DefaultGitHubReleaseApiUrl = "https://api.github.com/repos/spelech/RigSwitch/releases/latest";

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCheckService"/> class.
    /// </summary>
    /// <param name="httpClient">Optional <see cref="HttpClient"/> instance to use for network calls.</param>
    /// <param name="releaseApiUrl">Optional custom release API URL endpoint.</param>
    public UpdateCheckService(HttpClient? httpClient = null, string? releaseApiUrl = null)
    {
        _releaseApiUrl = string.IsNullOrWhiteSpace(releaseApiUrl) ? DefaultGitHubReleaseApiUrl : releaseApiUrl;
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
        }
        else
        {
            _httpClient = new HttpClient();
            _disposeClient = true;
        }

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RigSwitch-UpdateChecker", "1.0"));
        }
    }

    /// <inheritdoc/>
    public async Task<UpdateInfo> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        string currentVerString = NormalizeVersionString(currentVersion);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _releaseApiUrl);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateInfo(
                    IsUpdateAvailable: false,
                    CurrentVersion: currentVerString,
                    LatestVersion: currentVerString,
                    ReleaseNotes: string.Empty,
                    DownloadUrl: string.Empty,
                    HtmlUrl: string.Empty,
                    ErrorMessage: $"HTTP request failed with status code {(int)response.StatusCode} ({response.StatusCode}).");
            }

            var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var releaseData = await JsonSerializer.DeserializeAsync<GitHubReleaseData>(
                jsonStream,
                GitHubReleaseJsonContext.Default.GitHubReleaseData,
                cancellationToken).ConfigureAwait(false);

            if (releaseData == null || string.IsNullOrWhiteSpace(releaseData.TagName))
            {
                return new UpdateInfo(
                    IsUpdateAvailable: false,
                    CurrentVersion: currentVerString,
                    LatestVersion: currentVerString,
                    ReleaseNotes: string.Empty,
                    DownloadUrl: string.Empty,
                    HtmlUrl: string.Empty,
                    ErrorMessage: "Release payload was empty or missing version tag.");
            }

            string latestVerString = NormalizeVersionString(releaseData.TagName);
            bool isNewer = IsVersionNewer(latestVerString, currentVerString);

            string htmlUrl = releaseData.HtmlUrl ?? string.Empty;
            string releaseNotes = releaseData.Body ?? string.Empty;
            string downloadUrl = htmlUrl;

            if (releaseData.Assets is { Count: > 0 })
            {
                var exeAsset = releaseData.Assets.FirstOrDefault(a =>
                    !string.IsNullOrWhiteSpace(a.Name) &&
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                if (exeAsset != null && !string.IsNullOrWhiteSpace(exeAsset.BrowserDownloadUrl))
                {
                    downloadUrl = exeAsset.BrowserDownloadUrl;
                }
            }

            return new UpdateInfo(
                IsUpdateAvailable: isNewer,
                CurrentVersion: currentVerString,
                LatestVersion: latestVerString,
                ReleaseNotes: releaseNotes,
                DownloadUrl: downloadUrl,
                HtmlUrl: htmlUrl);
        }
        catch (Exception ex)
        {
            return new UpdateInfo(
                IsUpdateAvailable: false,
                CurrentVersion: currentVerString,
                LatestVersion: currentVerString,
                ReleaseNotes: string.Empty,
                DownloadUrl: string.Empty,
                HtmlUrl: string.Empty,
                ErrorMessage: ex.Message);
        }
    }

    /// <summary>
    /// Normalizes version strings by removing leading 'v' / 'V' prefixes and whitespace.
    /// </summary>
    public static string NormalizeVersionString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "1.0.0";
        }

        string trimmed = input.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        return trimmed;
    }

    /// <summary>
    /// Compares two version strings to determine if latestVersion is strictly newer than currentVersion.
    /// </summary>
    public static bool IsVersionNewer(string latestVersion, string currentVersion)
    {
        if (Version.TryParse(NormalizeVersionString(latestVersion), out var latest) &&
            Version.TryParse(NormalizeVersionString(currentVersion), out var current))
        {
            return latest > current;
        }

        return string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}

internal sealed class GitHubReleaseData
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset>? Assets { get; set; }
}

internal sealed class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
}

[JsonSerializable(typeof(GitHubReleaseData))]
internal partial class GitHubReleaseJsonContext : JsonSerializerContext;
