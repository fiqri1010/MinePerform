using System.Text;
using System.Text.RegularExpressions;

namespace MinePerform.Core.SchemaStudio;

/// <summary>
/// Enforces SQLite identifier safety for dynamic user-defined tables and columns.
/// Eliminates SQL injection across all No-Code SQLite Studio operations.
/// </summary>
public static partial class IdentifierGuard
{
    [GeneratedRegex("^[a-z][a-z0-9_]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidIdentifierRegex();

    /// <summary>Converts any human display name (e.g. "CONT 40F≥ PERIKSA", "BM-", "FCL/\nLCL") into a safe SQLite slug.</summary>
    public static string ToSafeSlug(string displayName, string fallbackPrefix = "col")
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return $"{fallbackPrefix}_1";
        }

        var sb = new StringBuilder(displayName.Length + 8);
        bool prevUnderscore = false;

        foreach (char ch in displayName.Trim())
        {
            if (ch is >= 'a' and <= 'z')
            {
                sb.Append(ch);
                prevUnderscore = false;
            }
            else if (ch is >= 'A' and <= 'Z')
            {
                sb.Append((char)(ch + 32));
                prevUnderscore = false;
            }
            else if (ch is >= '0' and <= '9')
            {
                sb.Append(ch);
                prevUnderscore = false;
            }
            else if (ch == '-' && displayName.TrimEnd().EndsWith('-'))
            {
                // Preserve distinction between "BM" and "BM-" (PUTUSAN PFPD kurang bayar columns)
                if (!prevUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                }
                sb.Append("kb");
                prevUnderscore = false;
            }
            else
            {
                if (!prevUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    prevUnderscore = true;
                }
            }
        }

        string slug = sb.ToString().Trim('_');
        if (string.IsNullOrEmpty(slug) || slug[0] is < 'a' or > 'z')
        {
            slug = $"{fallbackPrefix}_{slug}".TrimEnd('_');
        }

        if (slug.Length > 60)
        {
            slug = slug[..60].TrimEnd('_');
        }

        return ValidateOrThrow(slug);
    }

    /// <summary>Builds the canonical physical SQLite table name for a workspace year and table slug.</summary>
    public static string BuildPhysicalTableName(int year, string tableSlug)
    {
        if (year is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Workspace year must be between 2000 and 2100.");
        }

        string safeSlug = ValidateOrThrow(tableSlug);
        string physical = $"usr_y{year}_{safeSlug}";
        if (physical.Length > 63)
        {
            physical = physical[..63].TrimEnd('_');
        }
        return ValidateOrThrow(physical);
    }

    /// <summary>Validates that an identifier matches the strict allowlist regex.</summary>
    public static string ValidateOrThrow(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !ValidIdentifierRegex().IsMatch(identifier))
        {
            throw new ArgumentException($"Unsafe SQLite identifier rejected: '{identifier}'", nameof(identifier));
        }

        return identifier;
    }

    /// <summary>Validates and double-quotes a SQLite identifier safely.</summary>
    public static string Quote(string identifier) => $"\"{ValidateOrThrow(identifier)}\"";
}
