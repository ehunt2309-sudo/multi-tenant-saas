using System.Text.RegularExpressions;

namespace Platform.API.Shared.Utilities;

public static partial class TextHelper
{
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    public static string NormalizeHost(string host) => host.Trim().ToLowerInvariant();

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public static partial Regex SlugRegex();

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)*(:\d{1,5})?$")]
    public static partial Regex HostRegex();

    [GeneratedRegex(@"^#[0-9a-fA-F]{6}$")]
    public static partial Regex HexColorRegex();

    public static bool IsValidHost(string host) => host.Length is > 0 and <= 253 && HostRegex().IsMatch(host);

    /// <summary>Only absolute https URLs are accepted for user-supplied links/images (blocks javascript: etc).</summary>
    public static bool IsSafeHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;
}
