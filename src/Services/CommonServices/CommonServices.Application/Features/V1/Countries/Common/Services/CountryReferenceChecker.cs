using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Repositories;
using Microsoft.EntityFrameworkCore;
using static CommonServices.Domain.Exceptions.CurrencyException;
using static CommonServices.Domain.Exceptions.RegionException;

namespace CommonServices.Application.Features.V1.Countries.Common.Services;

// Region and currency are optional, but when sent they must exist (and not be deleted).
internal static class CountryReferenceChecker
{
    public static async Task EnsureReferencesExistAsync(
        IRepositoryBase<Region, Guid> regionRepository,
        IRepositoryBase<Currency, Guid> currencyRepository,
        Guid? regionId,
        Guid? currencyId,
        CancellationToken cancellationToken)
    {
        if (regionId is { } rId && !await regionRepository.FindAll(x => x.Id == rId).AnyAsync(cancellationToken))
            throw new RegionNotFoundException(rId);

        if (currencyId is { } cId && !await currencyRepository.FindAll(x => x.Id == cId).AnyAsync(cancellationToken))
            throw new CurrencyNotFoundException(cId);
    }
}
