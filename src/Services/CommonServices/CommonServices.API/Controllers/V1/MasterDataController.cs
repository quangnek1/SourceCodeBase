using Asp.Versioning;
using CommonServices.API.Abstractions;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Application.Features.V1.Banks.Queries.GetBankList;
using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using CommonServices.Application.Features.V1.Cities.Queries.GetCityList;
using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using CommonServices.Application.Features.V1.Countries.Queries.GetCountryList;
using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencyList;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Application.Features.V1.Languages.Queries.GetLanguageList;
using CommonServices.Application.Features.V1.Languages.Queries.GetNativeLanguages;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using CommonServices.Application.Features.V1.Regions.Queries.GetRegionList;
using CommonServices.Domain.Enums;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommonServices.API.Controllers.V1;

// Read-only lists for the portals and the other services (legacy api/common/master/*):
// any signed-in user, active rows only, names in the Accept-Language language.
[ApiVersion(1)]
[Authorize]
public class MasterDataController : ApiController
{
    public MasterDataController(ISender sender) : base(sender)
    {
    }

    [HttpGet("languages")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<LanguageDto>>), statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLanguages(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetLanguageListQuery(RequestLanguage, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("languages/native")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<LanguageDto>>), statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNativeLanguages(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetNativeLanguagesQuery(), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("regions")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<RegionDto>>), statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegions(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetRegionListQuery(RequestLanguage, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("currencies")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<CurrencyDto>>), statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrencies(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCurrencyListQuery(RequestLanguage, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("countries")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<CountryDto>>), statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCountries([FromQuery] Guid[]? regionIds = null, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCountryListQuery(RequestLanguage, ObjectStatus.Active, regionIds), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("cities")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<CityDto>>), statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCities([FromQuery] Guid[]? countryIds = null, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetCityListQuery(RequestLanguage, ObjectStatus.Active, countryIds), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("banks")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<BankDto>>), statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBanks(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetBankListQuery(RequestLanguage, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
