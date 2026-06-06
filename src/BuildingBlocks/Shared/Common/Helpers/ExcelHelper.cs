using Shared.DTOs.Excel;

namespace Shared.Common.Helpers;
public static class ExcelHelper
{
    public static string NormalizeHeader(string header)
    {
        return header
            .Trim()
            .Replace("\r", " ").Replace("\n", " ")
            .Replace(" ", "")
            .ToUpperInvariant();
    }

    public static string? GetString(
   ExcelRowData row,
   Dictionary<string, int> headers,
   string columnName)
    {
        var normalized = NormalizeHeader(columnName);

        if (!headers.TryGetValue(normalized, out var colIndex))
            return null;

        return row.Cells.TryGetValue(colIndex, out var value)
            ? value?.Trim()
            : null;
    }

    public static int GetInt(
        ExcelRowData row,
        Dictionary<string, int> headers,
        string columnName)
    {
        var normalized = NormalizeHeader(columnName);

        if (!headers.TryGetValue(normalized, out var colIndex))
            return 0;

        if (!row.Cells.TryGetValue(colIndex, out var value))
            return 0;

        return int.TryParse(value, out var result)
           ? result
           : 0;
    }
    public static int? GetInt(ExcelRowData row, int col)
    {
        if (!row.Cells.TryGetValue(col, out var value))
            return null;

        return int.TryParse(value, out var result)
            ? result
            : null;
    }

    public static double GetDouble(
      ExcelRowData row,
      Dictionary<string, int> headers,
      string columnName)
    {
        var normalized = NormalizeHeader(columnName);

        if (!headers.TryGetValue(normalized, out var colIndex))
            return 0;

        if (!row.Cells.TryGetValue(colIndex, out var value))
            return 0;

        return double.TryParse(value, out var result)
           ? result
           : 0;
    }

    public static DateTime? GetDate(
        ExcelRowData row,
        Dictionary<string, int> headers,
        string columnName)
    {
        var normalized = NormalizeHeader(columnName);

        if (!headers.TryGetValue(normalized, out var colIndex))
            return null;

        if (!row.Cells.TryGetValue(colIndex, out var value))
            return null;

        return DateTime.TryParse(value, out var result)
           ? result
           : null;
    }
    public static DateTime? GetDate(ExcelRowData row, int col)
    {
        if (!row.Cells.TryGetValue(col, out var value))
            return null;

        return DateTime.TryParse(value, out var result)
            ? result
            : null;
    }


}
