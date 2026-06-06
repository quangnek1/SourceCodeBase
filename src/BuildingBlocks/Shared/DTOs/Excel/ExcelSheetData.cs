namespace Shared.DTOs.Excel;
public class ExcelSheetData
{
    public string SheetName { get; set; } = string.Empty;
    public int SheetIndex { get; set; }

    public Dictionary<string, int> Headers { get; set; } = [];
    public IReadOnlyList<ExcelRowData> Rows { get; set; } = [];
}
