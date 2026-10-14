using CommonServices.Application.Common.Dtos;

namespace CommonServices.Application.Features.V1.Regions.Common.Dtos;
public sealed record RegionTranslationRequest(string LangCode, string Name, string? Note) : ITranslationRequest;
