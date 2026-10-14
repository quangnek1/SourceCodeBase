namespace CommonServices.Domain.Enums;

// Same values as the legacy common-services "Status" column, so imported data keeps its meaning.
public enum ObjectStatus
{
    Deleted = -99,
    Inactive = 0,
    Active = 1
}
