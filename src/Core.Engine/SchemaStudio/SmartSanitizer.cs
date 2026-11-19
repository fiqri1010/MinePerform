using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MinePerform.Core.SchemaStudio;

/// <summary>
/// Defensive data sanitization & Data Hygiene Radar calibrated to real customs data (Data IP 2026):
/// 1. Excel currency cleaner (" $2,880,771,505.92 ", " $- " -> decimal 0m).
/// 2. Windows NTFS Path Safety Guard (replaces / and \, strips \r\n\t and illegal chars, guards reserved names).
/// 3. Date Anomaly Radar (detects out-of-range year typos like 05/11/2515 without aborting import).
/// 4. No. IP normalizer (preserves leading zeros like 000765 and non-PIB codes like ATA-003/IMPOR/2026).
/// 5. Dynamic Computed Column evaluator (SUM, TK_PERIKSA_RATIO, TEMPLATE).
/// </summary>
public static partial class SmartSanitizer
{
    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd-MM-yyyy",
        "dd.MM.yyyy",
        "MM/dd/yyyy",
        "M/d/yyyy",
        "dd/MM/yyyy HH:mm",
        "dd/MM/yyyy HH:mm:ss",
        "d/M/yyyy H:mm",
        "d/M/yyyy H:mm:ss",
        "dd-MM-yyyy HH:mm:ss",
        "dd.MM.yyyy HH:mm"
    ];

    [GeneratedRegex(@"\{([^}:]+)(?::([^}]+))?\}", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateTokenRegex();

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex MultiSpaceRegex();

    /// <summary>
    /// Cleans messy Excel/CSV currency strings such as " $2,880,771,505.92 ", " $- ", "Rp 144.039.000", or "-".
    /// </summary>
    public static bool TrySanitizeCurrencyOrDecimal(string? raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string trimmed = raw.Trim();
        // Strip currency symbols and spaces
        string cleaned = trimmed
            .Replace("Rp.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Rp", "", StringComparison.OrdinalIgnoreCase)
            .Replace("IDR", "", StringComparison.OrdinalIgnoreCase)
            .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
            .Replace("$", "", StringComparison.Ordinal)
            .Replace("\u00A0", "", StringComparison.Ordinal)
            .Trim();

        if (cleaned is "" or "-" or "—" or "–" or "N/A" or "#N/A")
        {
            value = 0m;
            return true;
        }

        bool isNegative = false;
        if (cleaned.StartsWith('(') && cleaned.EndsWith(')'))
        {
            isNegative = true;
            cleaned = cleaned[1..^1].Trim();
        }
        else if (cleaned.StartsWith('-'))
        {
            isNegative = true;
            cleaned = cleaned[1..].Trim();
        }

        cleaned = cleaned.Replace(" ", "", StringComparison.Ordinal);

        // Determine decimal vs thousands separator intelligently
        int lastComma = cleaned.LastIndexOf(',');
        int lastDot = cleaned.LastIndexOf('.');

        string normalized;
        if (lastComma >= 0 && lastDot >= 0)
        {
            // Both present: whichever appears last is the decimal separator
            if (lastDot > lastComma)
            {
                // US/Excel format: 2,880,771,505.92
                normalized = cleaned.Replace(",", "", StringComparison.Ordinal);
            }
            else
            {
                // ID/EU format: 2.880.771.505,92
                normalized = cleaned.Replace(".", "", StringComparison.Ordinal).Replace(',', '.');
            }
        }
        else if (lastComma >= 0)
        {
            // Only comma: if 1-2 digits after last comma and only one comma, treat as decimal; otherwise thousands
            int digitsAfter = cleaned.Length - lastComma - 1;
            int commaCount = cleaned.Count(c => c == ',');
            normalized = (commaCount == 1 && digitsAfter is >= 1 and <= 2)
                ? cleaned.Replace(',', '.')
                : cleaned.Replace(",", "", StringComparison.Ordinal);
        }
        else if (lastDot >= 0)
        {
            int dotCount = cleaned.Count(c => c == '.');
            int digitsAfter = cleaned.Length - lastDot - 1;
            // Multiple dots like 144.039.000 -> thousands separators
            normalized = (dotCount > 1)
                ? cleaned.Replace(".", "", StringComparison.Ordinal)
                : (dotCount == 1 && digitsAfter == 3 && cleaned.Length > 4 && !trimmed.Contains('$'))
                    ? cleaned.Replace(".", "", StringComparison.Ordinal)
                    : cleaned;
        }
        else
        {
            normalized = cleaned;
        }

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
        {
            value = isNegative ? -parsed : parsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses Excel serial dates (e.g. 46017 -> 2025-12-26) or text dates, flagging out-of-range year typos (e.g. 05/11/2515).
    /// </summary>
    public static (string? IsoValue, DataAnomalyRecord? Anomaly) SanitizeDate(
        string? raw,
        int workspaceYear,
        string tableId,
        long rowId,
        string columnKey,
        bool includeTime)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        string trimmed = raw.Trim();
        DateTime dt;

        // 1. Excel OADate serial number (e.g., 46017 or 46024.3736111)
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double oaSerial)
            && oaSerial is > 1 and < 2958465)
        {
            dt = DateTime.FromOADate(oaSerial);
        }
        else if (!DateTime.TryParseExact(trimmed, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
                 && !DateTime.TryParse(trimmed, CultureInfo.GetCultureInfo("id-ID"), DateTimeStyles.None, out dt)
                 && !DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            var unparseable = new DataAnomalyRecord(
                Guid.NewGuid().ToString("N"),
                tableId,
                rowId,
                columnKey,
                AnomalySeverity.Warning,
                "DATE_UNPARSEABLE",
                trimmed,
                $"Format tanggal tidak dikenali: '{trimmed}'.");
            return (trimmed, unparseable);
        }

        string iso = includeTime
            ? dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Data Hygiene Radar: detect year typo such as 05/11/2515
        if (dt.Year < workspaceYear - 2 || dt.Year > workspaceYear + 1)
        {
            var typoAnomaly = new DataAnomalyRecord(
                Guid.NewGuid().ToString("N"),
                tableId,
                rowId,
                columnKey,
                AnomalySeverity.Warning,
                "DATE_YEAR_TYPO",
                trimmed,
                $"Anomali tahun terdeteksi ({dt.Year}) pada '{trimmed}' untuk workspace tahun {workspaceYear}.");
            return (iso, typoAnomaly);
        }

        return (iso, null);
    }

    /// <summary>
    /// Normalizes No. IP while preserving 6-digit leading zeros (000765, 001081) and alphanumeric codes (ATA-003/IMPOR/2026).
    /// </summary>
    public static string NormalizeNoIp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string cleaned = raw.Trim().Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);

        // If Excel stored a numeric 6-digit IP as e.g. "765" or "765.0", zero-pad to 6 digits
        if (cleaned.EndsWith(".0", StringComparison.Ordinal))
        {
            cleaned = cleaned[..^2];
        }

        if (cleaned.Length is > 0 and < 6 && cleaned.All(char.IsAsciiDigit))
        {
            return cleaned.PadLeft(6, '0');
        }

        return cleaned;
    }

    /// <summary>
    /// Windows NTFS Path Safety Guard for Nama Folder Onedrive and file names.
    /// Replaces / and \ with -, strips \r\n\t and forbidden NTFS chars, guards reserved names, and caps length.
    /// </summary>
    public static string SanitizeWindowsFolderName(string? raw, int maxLength = 160)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "TANPA_NAMA";
        }

        var sb = new StringBuilder(raw.Length);
        foreach (char ch in raw)
        {
            if (ch is '/' or '\\')
            {
                sb.Append('-');
            }
            else if (ch is '\r' or '\n' or '\t')
            {
                sb.Append(' ');
            }
            else if (ch < 0x20 || ch is ':' or '*' or '?' or '"' or '<' or '>' or '|')
            {
                // Strip forbidden NTFS characters
            }
            else
            {
                sb.Append(ch);
            }
        }

        string collapsed = MultiSpaceRegex().Replace(sb.ToString(), " ").Trim();
        collapsed = collapsed.TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(collapsed))
        {
            return "TANPA_NAMA";
        }

        if (ReservedWindowsNames.Contains(collapsed))
        {
            collapsed = $"_{collapsed}";
        }

        if (collapsed.Length > maxLength)
        {
            collapsed = collapsed[..maxLength].TrimEnd('.', ' ');
        }

        return collapsed;
    }

    /// <summary>
    /// Evaluates metadata-driven computed column expressions dynamically (zero hardcoded column names).
    /// Supported expression directives:
    ///  - SUM(col1, col2, ...)
    ///  - TK_PERIKSA_RATIO(qtyCol, tkCol)
    ///  - TEMPLATE({col1}. {col2:000000}_{col3:dd.MM.yyyy}_...)
    /// </summary>
    public static string EvaluateComputedExpression(string expression, IReadOnlyDictionary<string, string?> rowCells)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        string trimmed = expression.Trim();

        if (trimmed.StartsWith("SUM(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            string inner = trimmed[4..^1];
            decimal total = 0m;
            foreach (string part in inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (rowCells.TryGetValue(part, out string? rawVal) && TrySanitizeCurrencyOrDecimal(rawVal, out decimal num))
                {
                    total += num;
                }
            }
            return total.ToString(CultureInfo.InvariantCulture);
        }

        if (trimmed.StartsWith("TK_PERIKSA_RATIO(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            string[] args = trimmed[17..^1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (args.Length >= 2
                && rowCells.TryGetValue(args[0], out string? qtyRaw)
                && TrySanitizeCurrencyOrDecimal(qtyRaw, out decimal qty))
            {
                rowCells.TryGetValue(args[1], out string? tkRaw);
                string tk = (tkRaw ?? string.Empty).Trim().ToUpperInvariant();
                decimal ratio = tk switch
                {
                    "10%" or "0.1" or "0,1" => 0.10m,
                    "30%" or "0.3" or "0,3" => 0.30m,
                    "MENDALAM" or "100%" or "1" => 1.00m,
                    _ => 0m
                };
                if (ratio > 0m)
                {
                    decimal result = Math.Round(qty * ratio, 2, MidpointRounding.AwayFromZero);
                    return result.ToString(CultureInfo.InvariantCulture);
                }
            }
            return string.Empty;
        }

        if (trimmed.StartsWith("TEMPLATE(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            string template = trimmed[9..^1];
            string rendered = TemplateTokenRegex().Replace(template, match =>
            {
                string colKey = match.Groups[1].Value.Trim();
                string? fmt = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;

                rowCells.TryGetValue(colKey, out string? rawVal);
                string val = (rawVal ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(val))
                {
                    return string.Empty;
                }

                if (!string.IsNullOrEmpty(fmt))
                {
                    if (fmt == "000000")
                    {
                        return NormalizeNoIp(val);
                    }
                    if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                    {
                        return dt.ToString(fmt, CultureInfo.InvariantCulture);
                    }
                }

                return val;
            });

            return SanitizeWindowsFolderName(rendered);
        }

        return string.Empty;
    }

    /// <summary>Converts boolean-like Indonesian/English cell text ("TRUE", "YA", "1", "TIDAK", "FALSE") to canonical "1"/"0".</summary>
    public static string? SanitizeBoolean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string upper = raw.Trim().ToUpperInvariant();
        return upper switch
        {
            "1" or "TRUE" or "YA" or "YES" or "Y" or "ADA" => "1",
            "0" or "FALSE" or "TIDAK" or "NO" or "N" or "-" => "0",
            _ => raw.Trim()
        };
    }

    /// <summary>Replaces embedded newlines in single-line text cells and reports an anomaly when present.</summary>
    public static (string CleanValue, DataAnomalyRecord? Anomaly) SanitizeSingleLineText(
        string? raw,
        string tableId,
        long rowId,
        string columnKey)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return (string.Empty, null);
        }

        if (raw.Contains('\n') || raw.Contains('\r'))
        {
            string cleaned = MultiSpaceRegex().Replace(raw.Replace('\r', ' ').Replace('\n', ' '), " ").Trim();
            var anomaly = new DataAnomalyRecord(
                Guid.NewGuid().ToString("N"),
                tableId,
                rowId,
                columnKey,
                AnomalySeverity.Info,
                "MULTILINE_NORMALIZED",
                raw,
                $"Karakter baris baru (\\n) pada '{columnKey}' dinormalisasi otomatis untuk keamanan tabel & path OneDrive.");
            return (cleaned, anomaly);
        }

        return (raw.Trim(), null);
    }
}
