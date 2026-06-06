using Contracts.Services.Excel;
using OfficeOpenXml;
using Serilog;
using Shared.Common.Helpers;
using Shared.DTOs.Excel;
using Shared.Options;

namespace Infrastructure.Services.Excel;
internal class EpplusExcelReaderService : IExcelReaderService
{
    private readonly ILogger _logger;
    public EpplusExcelReaderService(ILogger logger, EpplusOptions epplusOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ExcelPackage.License.SetNonCommercialPersonal(epplusOptions.LicenseName);

    }
    public async Task<ExcelSheetData> ReadSheetAsync(string filePath, int sheetIndex = 1, int headerRow = 1, int skipRow = 0, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"File Not exist: {filePath}");

            using var package = new ExcelPackage(
                new FileInfo(filePath));

            var worksheet =
                package.Workbook.Worksheets[sheetIndex - 1];

            if (worksheet.Dimension == null)
                return new ExcelSheetData();

            var totalRows = worksheet.Dimension.Rows;
            var totalCols = worksheet.Dimension.Columns;

            // data bắt đầu sau header + skip
            var startRow = headerRow + skipRow + 1;

            // build headers
            var headers = new Dictionary<string, int>();

            for (int colIdx = 1; colIdx <= totalCols; colIdx++)
            {
                var headerText = worksheet.Cells[headerRow, colIdx].Text?.Trim();

                if (string.IsNullOrWhiteSpace(headerText))
                    continue;

                //    headers[ExcelHelper.NormalizeHeader(headerText)] = colIdx;

                var header = ExcelHelper.NormalizeHeader(headerText);
                // chỉ add lần đầu
                if (!headers.ContainsKey(header))
                {
                    headers[header] = colIdx;
                }
            }

            var rows = new List<ExcelRowData>();

            for (int rowIdx = startRow; rowIdx <= totalRows; rowIdx++)
            {
                var cells = new Dictionary<int, string>();

                for (int colIdx = 1; colIdx <= totalCols; colIdx++)
                {
                    var value = worksheet.Cells[rowIdx, colIdx].Text;

                    cells[colIdx] = value ?? string.Empty;
                }

                rows.Add(new ExcelRowData(rowIdx, cells));
            }

            var result = new ExcelSheetData
            {
                SheetIndex = sheetIndex,
                SheetName = worksheet.Name,
                Headers = headers,
                Rows = rows
            };

            _logger.Information("Readed {File}: {Count} row", filePath, rows.Count);

            return result;

        }, ct);
    }

    public async Task<ExcelWorkbookData> ReadWorkbookAsync(string filePath, int headerRow = 1, int skipRow = 0, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"File không tồn tại: {filePath}");

            using var package = new ExcelPackage(
                new FileInfo(filePath));

            var workbook = new ExcelWorkbookData();

            for (int sheetIdx = 0; sheetIdx < package.Workbook.Worksheets.Count; sheetIdx++)
            {
                ct.ThrowIfCancellationRequested();

                var worksheet = package.Workbook.Worksheets[sheetIdx];

                if (worksheet.Dimension == null)
                    continue;

                var totalRows = worksheet.Dimension.Rows;
                var totalCols = worksheet.Dimension.Columns;

                // build headers
                var headers = new Dictionary<string, int>();
                string? lastTopHeader = null;

                for (int colIdx = 1; colIdx <= totalCols; colIdx++)
                {
                    var topHeader = worksheet.Cells[headerRow, colIdx].Text?.Trim();

                    var subHeader = worksheet.Cells[headerRow + 1, colIdx].Text?.Trim();

                    //       var headerText = worksheet.Cells[headerRow, colIdx].Text?.Trim();

                    // merged cell -> dùng header trước đó
                    if (!string.IsNullOrWhiteSpace(topHeader))
                    {
                        lastTopHeader = topHeader;
                    }
                    else
                    {
                        topHeader = lastTopHeader;
                    }

                    string finalHeader;

                    finalHeader =
                        !string.IsNullOrWhiteSpace(topHeader) &&
                        !string.IsNullOrWhiteSpace(subHeader)
                            ? $"{topHeader}_{subHeader}"
                            : topHeader ?? subHeader ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(finalHeader))
                        continue;

                    var normalized = ExcelHelper.NormalizeHeader(finalHeader);

                    // lấy cột đầu tiên nếu duplicate
                    if (!headers.ContainsKey(normalized))
                    {
                        headers[normalized] = colIdx;
                    }
                }

                // data row start
                var startRow = headerRow + skipRow + 1;

                var rows = new List<ExcelRowData>();

                for (int rowIdx = startRow; rowIdx <= totalRows; rowIdx++)
                {
                    var cells = new Dictionary<int, string>();

                    for (int colIdx = 1; colIdx <= totalCols; colIdx++)
                    {
                        var value = worksheet.Cells[rowIdx, colIdx].Text;

                        cells[colIdx] = value ?? string.Empty;
                    }

                    rows.Add(new ExcelRowData(rowIdx, cells));
                }

                workbook.Sheets.Add(new ExcelSheetData
                {
                    SheetIndex = sheetIdx + 1,
                    SheetName = worksheet.Name,
                    Headers = headers,
                    Rows = rows
                });

                _logger.Information("Read sheet {SheetName}: {RowCount} row", worksheet.Name, rows.Count);
            }

            _logger.Information("Readed successfully: {FilePath}", filePath);

            return workbook;

        }, ct);
    }
}
