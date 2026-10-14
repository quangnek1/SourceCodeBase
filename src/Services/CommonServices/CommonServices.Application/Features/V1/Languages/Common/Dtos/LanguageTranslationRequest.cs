using CommonServices.Application.Common.Dtos;

namespace CommonServices.Application.Features.V1.Languages.Common.Dtos;
public sealed record LanguageTranslationRequest(string LangCode, string Name, string? Description) : ITranslationRequest;
