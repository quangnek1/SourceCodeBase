using Contracts.Exceptions;

namespace CommonServices.Domain.Exceptions;
public static class RegionException
{
    public class RegionNotFoundException : NotFoundException
    {
        public RegionNotFoundException(Guid regionId)
            : base($"The region with the id {regionId} was not found.") { }
    }
}
