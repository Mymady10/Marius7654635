using SmartAuto.Common.Constants;

namespace SmartAuto.Common.Extensions;

/// <summary>Extension methods for string manipulation in SmartAuto contexts.</summary>
public static class StringExtensions
{
    /// <summary>
    /// Masks a potentially sensitive string for logging.
    /// Returns the original string if it is not sensitive, or <see cref="AppConstants.SensitivePlaceholder"/> otherwise.
    /// </summary>
    public static string MaskIfSensitive(this string value, bool isSensitive)
        => isSensitive ? AppConstants.SensitivePlaceholder : value;

    /// <summary>Truncates a string to the specified maximum length, appending "…" if truncated.</summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
        return string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }

    /// <summary>
    /// Resolves all {{VariableName}} placeholders in a template string
    /// using the provided variable dictionary (case-insensitive lookup).
    /// </summary>
    public static string InterpolateVariables(
        this string template,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var result = template;
        foreach (var (key, value) in variables)
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);

        return result;
    }
}
