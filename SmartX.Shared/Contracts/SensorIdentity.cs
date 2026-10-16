using System.Text.RegularExpressions;

namespace SmartX.Shared.Contracts;

/// <summary>
/// Single source of truth for the identifier a sensor is registered under. Presence is not
/// enough: the gateway accepts an IEEE MAC or a documented Smart-X unique id, and nothing else.
/// </summary>
public static partial class SensorIdentity
{
    public const string FormatHint =
        "Use an IEEE MAC (AA:BB:CC:DD:EE:FF) or a Smart-X unique id (SX-NODE01).";

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return MacRegex().IsMatch(trimmed) || UniqueIdRegex().IsMatch(trimmed);
    }

    public static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    public static string DescribeViolation(string? value)
    {
        var shown = string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Trim();
        return $"'{shown}' is not a valid sensor identity. {FormatHint}";
    }

    [GeneratedRegex(@"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex MacRegex();

    [GeneratedRegex(@"^SX-[A-Z0-9]{6,}$", RegexOptions.CultureInvariant)]
    private static partial Regex UniqueIdRegex();
}
