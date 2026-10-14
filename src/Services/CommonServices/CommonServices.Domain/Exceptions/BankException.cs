using Contracts.Exceptions;

namespace CommonServices.Domain.Exceptions;
public static class BankException
{
    public class BankNotFoundException : NotFoundException
    {
        public BankNotFoundException(Guid bankId)
            : base($"The bank with the id {bankId} was not found.") { }
    }
}
