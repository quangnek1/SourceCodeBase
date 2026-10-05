namespace Contracts.Domains.Interfaces;
public interface IDataOwned
{
    Guid? CreatedBy { get; set; }
    Guid? DepartmentId { get; set; }   // phòng của người tạo, lưu lúc tạo
}
