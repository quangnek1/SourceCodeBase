namespace CommonServices.Application.Common.Dtos;

public interface ITranslationRequest
{
    string LangCode { get; }
}

// Name of a master data row in one language, sent by create/update commands.
public sealed record TranslationRequest(string LangCode, string Name) : ITranslationRequest;
