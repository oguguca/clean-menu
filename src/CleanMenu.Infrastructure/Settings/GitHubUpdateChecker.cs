using System.Net;
using System.Text.Json;
using CleanMenu.Core.Settings;

namespace CleanMenu.Infrastructure.Settings;

/// <summary>Reads the latest public release of the project from the GitHub API. Read-only.</summary>
public sealed class GitHubUpdateChecker(HttpClient http) : IUpdateChecker
{
    private static readonly Uri LatestApi = new("https://api.github.com/repos/oguguca/clean-menu/releases/latest");

    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public Uri ReleasesPage { get; } = new("https://github.com/oguguca/clean-menu/releases");

    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestApi);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(UpdateStatus.NoReleasesYet, null);
            }

            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
                var tag = json.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
                return VersionText.IsNewer(tag, currentVersion)
                    ? new UpdateCheckResult(UpdateStatus.NewVersion, tag)
                    : new UpdateCheckResult(UpdateStatus.UpToDate, tag);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            return new UpdateCheckResult(UpdateStatus.Failed, null);
        }
    }
}
