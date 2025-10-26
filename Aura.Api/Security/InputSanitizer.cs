using System.Text;
using System.Text.RegularExpressions;

namespace Aura.Api.Security;

/// <summary>
/// Input sanitization helpers to prevent SQL injection, XSS, and path traversal attacks
/// </summary>
public static class InputSanitizer
{
    private static readonly HashSet<string> DangerousSqlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "DROP", "DELETE", "INSERT", "UPDATE", "ALTER", "CREATE", "EXEC", "EXECUTE",
        "SCRIPT", "UNION", "SELECT", "--", "/*", "*/", "xp_", "sp_"
    };

    private static readonly Regex PathTraversalPattern = new(@"\.\.[/\\]", RegexOptions.Compiled);
    private static readonly Regex XssPattern = new(@"<script|javascript:|onerror=|onload=", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates and sanitizes a file path to prevent path traversal attacks
    /// Returns null if the path is potentially malicious
    /// </summary>
    public static string? SanitizeFilePath(string path, string allowedBasePath)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            // Check for path traversal patterns
            if (PathTraversalPattern.IsMatch(path))
            {
                return null;
            }

            // Normalize the path
            var fullPath = Path.GetFullPath(Path.Combine(allowedBasePath, path));

            // Ensure the path is within the allowed base path
            if (!fullPath.StartsWith(Path.GetFullPath(allowedBasePath), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fullPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validates that a filename is safe (no path separators or special characters)
    /// </summary>
    public static bool IsValidFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        // Check for invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (filename.Any(c => invalidChars.Contains(c)))
            return false;

        // Check for path traversal
        if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
            return false;

        return true;
    }

    /// <summary>
    /// Sanitizes text input to prevent XSS attacks
    /// Removes or encodes potentially dangerous HTML/JavaScript
    /// </summary>
    public static string SanitizeForHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove any script tags or javascript: protocols
        if (XssPattern.IsMatch(input))
        {
            input = XssPattern.Replace(input, "");
        }

        // HTML encode special characters
        return System.Net.WebUtility.HtmlEncode(input);
    }

    /// <summary>
    /// Validates that a string doesn't contain SQL injection patterns
    /// Note: This is a defense-in-depth measure. Primary protection is parameterized queries.
    /// </summary>
    public static bool ContainsSqlInjectionPatterns(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        // Check for dangerous SQL keywords
        foreach (var keyword in DangerousSqlKeywords)
        {
            if (input.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that a string is safe for use in database queries
    /// Returns the sanitized string or null if potentially malicious
    /// </summary>
    public static string? SanitizeForDatabase(string input, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Check length
        if (input.Length > maxLength)
            return null;

        // Check for SQL injection patterns
        if (ContainsSqlInjectionPatterns(input))
            return null;

        return input;
    }

    /// <summary>
    /// Validates that a URL is safe and from an allowed domain
    /// </summary>
    public static bool IsValidUrl(string url, string[]? allowedDomains = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Only allow HTTP and HTTPS
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        // Check allowed domains if specified
        if (allowedDomains != null && allowedDomains.Length > 0)
        {
            var host = uri.Host.ToLowerInvariant();
            if (!allowedDomains.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                                               host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Sanitizes a project name or identifier
    /// Allows only alphanumeric, hyphens, underscores, and spaces
    /// </summary>
    public static string? SanitizeIdentifier(string input, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (input.Length > maxLength)
            return null;

        // Only allow safe characters
        var sanitized = Regex.Replace(input, @"[^a-zA-Z0-9\-_ ]", "");

        if (string.IsNullOrWhiteSpace(sanitized))
            return null;

        return sanitized.Trim();
    }

    /// <summary>
    /// Validates that an email address has a valid format
    /// </summary>
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sanitizes JSON string to prevent injection
    /// Validates that the string is valid JSON
    /// </summary>
    public static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a numeric range to prevent overflow or unreasonable values
    /// </summary>
    public static bool IsInRange(double value, double min, double max)
    {
        return value >= min && value <= max && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Validates a string length is within acceptable bounds
    /// </summary>
    public static bool IsValidLength(string? input, int minLength = 0, int maxLength = int.MaxValue)
    {
        if (input == null)
            return minLength == 0;

        return input.Length >= minLength && input.Length <= maxLength;
    }

    /// <summary>
    /// Removes null bytes and other control characters that could cause issues
    /// </summary>
    public static string RemoveControlCharacters(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            // Keep only printable characters and common whitespace
            if (!char.IsControl(c) || c == '\n' || c == '\r' || c == '\t')
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
