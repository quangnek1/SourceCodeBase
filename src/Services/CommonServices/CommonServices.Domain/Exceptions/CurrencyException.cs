using Contracts.Exceptions;

namespace CommonServices.Domain.Exceptions;
public static class CurrencyException
{
    public class CurrencyNotFoundException : NotFoundException
    {
        public CurrencyNotFoundException(Guid currencyId)
            : base($"The currency with the id {currencyId} was not found.") { }
    }
}
