using Ardalis.SmartEnum;
namespace Shared.Emumerations;
public class SortOrder : SmartEnum<SortOrder>
{
    private SortOrder(string name, int value) : base(name, value)
    {
    }

    public static readonly SortOrder Ascending = new(nameof(Ascending), 1);
    public static readonly SortOrder Descending = new(nameof(Descending), 2);

    public static implicit operator SortOrder(string name) => FromName(name, true);

    public static implicit operator SortOrder(int value) => FromValue(value);

    public static implicit operator string(SortOrder sortOrder) => sortOrder.Name;

    public static implicit operator int(SortOrder sortOrder) => sortOrder.Value;
}
