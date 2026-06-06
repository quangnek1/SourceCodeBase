namespace Shared.DTOs.Excel;
public sealed class ExcelRowData
{
    public int RowIndex { get; set; }

    public Dictionary<int, string> Cells { get; set; } = [];

    public ExcelRowData(int rowIndex, Dictionary<int, string> cells)
    {
        RowIndex = rowIndex;
        Cells = cells;
    }
    public ExcelRowData()
    {

    }
}
