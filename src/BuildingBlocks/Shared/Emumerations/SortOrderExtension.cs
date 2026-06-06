namespace Shared.Emumerations;
public static class SortOrderExtension
{
    public static SortOrder ConvertStringToSortOrder(string? sortOrder)
             => !string.IsNullOrWhiteSpace(sortOrder)
                 ? sortOrder.ToUpper().Equals("ASC")
                 ? SortOrder.Ascending : SortOrder.Descending : SortOrder.Ascending; // => Descending on CreatedDate by default if sortOrder is not provided or invalid

}
