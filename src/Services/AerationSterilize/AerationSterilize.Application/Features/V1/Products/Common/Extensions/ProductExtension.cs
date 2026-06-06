using System.Linq.Expressions;
using AerationSterilize.Domain.Entities;

namespace AerationSterilize.Application.Features.V1.Products.Common.Extensions;
/// <summary>
/// class là một phần mở rộng của lớp Product, cung cấp các phương thức tiện ích để hỗ trợ việc sắp xếp sản phẩm theo các thuộc tính khác nhau như Name, Price, Description.
/// </summary>
public static class ProductExtension
{
    /// <summary>
    /// hàm trả về tên thuộc tính của sản phẩm tương ứng với cột được truyền vào, nếu cột không hợp lệ sẽ trả về "Id"
    /// vào đó sẽ được sử dụng trong câu lệnh OrderBy hoặc OrderByDescending để sắp xếp kết quả truy vấn sản phẩm theo cột tương ứng.
    /// </summary>
    /// <param name="sortColumn"></param>
    /// <returns></returns>
    public static string GetSortProductProperty(string sortColumn)
        => sortColumn.ToLower() switch
        {
            "name" => "Name",
            "price" => "Price",
            "description" => "Description",
            _ => "Id"
        };
    /// <summary>
    /// hàm trả về biểu thức sắp xếp theo cột được truyền vào, nếu cột không hợp lệ sẽ sắp xếp theo Id
    /// vào đó sẽ được sử dụng trong câu lệnh OrderBy hoặc OrderByDescending để sắp xếp kết quả truy vấn sản phẩm theo cột tương ứng.
    /// vào đó sẽ giúp cho việc sắp xếp sản phẩm theo các thuộc tính khác nhau một cách linh hoạt và dễ dàng.
    /// </summary>
    /// <param name="sortColumn"></param>
    /// <returns></returns>
    public static Expression<Func<Product, object>> GetSortExpression(string sortColumn)
    {
        return sortColumn?.ToLower() switch
        {
            "name" => x => x.Name,
            "price" => x => x.Price,
            "description" => x => x.Description,
            _ => x => x.Id
        };
    }
}
