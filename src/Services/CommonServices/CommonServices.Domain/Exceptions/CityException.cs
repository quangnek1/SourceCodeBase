using Contracts.Exceptions;

namespace CommonServices.Domain.Exceptions;
public static class CityException
{
    public class CityNotFoundException : NotFoundException
    {
        public CityNotFoundException(Guid cityId)
            : base($"The city with the id {cityId} was not found.") { }
    }
}
