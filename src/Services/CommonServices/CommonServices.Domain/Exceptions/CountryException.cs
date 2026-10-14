using Contracts.Exceptions;

namespace CommonServices.Domain.Exceptions;
public static class CountryException
{
    public class CountryNotFoundException : NotFoundException
    {
        public CountryNotFoundException(Guid countryId)
            : base($"The country with the id {countryId} was not found.") { }
    }
}
