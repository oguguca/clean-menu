using System.Text.RegularExpressions;

namespace CleanMenu.Core.Network;

/// <summary>Progress of a file download.</summary>
public sealed record DownloadProgress(long Received, long? Total);

/// <summary>Downloads one file over HTTPS to a chosen path.</summary>
public interface IFileDownloader
{
    /// <summary>
    /// Downloads to "<paramref name="destination"/>.part" and renames it only when complete, so a
    /// failed or cancelled download never leaves a broken file with the final name.
    /// Returns the number of bytes saved.
    /// </summary>
    Task<long> DownloadAsync(Uri url, string destination, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default);
}

/// <summary>Validation rules for the "Baixar arquivo" feature and the ping host box.</summary>
public static partial class DownloadRules
{
    [GeneratedRegex(@"^[A-Za-z0-9]([A-Za-z0-9\-\.]{0,251}[A-Za-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex HostPattern();

    /// <summary>Accepts only absolute https:// addresses. Returns null and an error message otherwise.</summary>
    public static Uri? ParseHttpsUrl(string? text, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Cole o endereço do arquivo (começando com https://).";
            return null;
        }

        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            error = "Esse endereço não é válido.";
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Por segurança, só são aceitos endereços que começam com https:// (conexão protegida).";
            return null;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "Endereços com usuário ou senha não são aceitos.";
            return null;
        }

        return uri;
    }

    /// <summary>Suggests a safe file name from the address ("arquivo.zip"), or "download" when none.</summary>
    public static string SuggestFileName(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var name = Uri.UnescapeDataString(url.Segments.Length > 0 ? url.Segments[^1] : string.Empty).Trim('/', ' ', '.');
        var invalid = Path.GetInvalidFileNameChars();
        name = new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
        if (name.Length > 120)
        {
            var extension = Path.GetExtension(name);
            name = name[..(120 - extension.Length)] + extension;
        }

        return string.IsNullOrWhiteSpace(name) ? "download" : name;
    }

    private static readonly HashSet<string> RiskyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".scr", ".com", ".jar", ".msix", ".appx", ".reg", ".lnk", ".hta",
    };

    /// <summary>True for files that can run programs when opened.</summary>
    public static bool IsRunnable(string fileName) => RiskyExtensions.Contains(Path.GetExtension(fileName));

    /// <summary>Accepts a host name ("google.com") or an IPv4/IPv6 address for the ping test.</summary>
    public static bool IsValidHost(string? host) =>
        !string.IsNullOrWhiteSpace(host)
        && (System.Net.IPAddress.TryParse(host.Trim(), out _) || HostPattern().IsMatch(host.Trim()));
}
