using Shared.DTOs.Excel;

namespace Contracts.Services.Excel;
public interface IExcelReaderService
{
    Task<ExcelSheetData> ReadSheetAsync(
      string filePath,
      int sheetIndex = 1,
      int headerRow = 1,
      int skipRow = 0,
      CancellationToken ct = default);

    Task<ExcelWorkbookData> ReadWorkbookAsync(
      string filePath,
        int headerRow = 1,
        int skipRow = 0,
      CancellationToken ct = default);
}
