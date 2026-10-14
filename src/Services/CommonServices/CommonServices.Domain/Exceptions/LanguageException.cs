using Contracts.Exceptions;

namespace CommonServices.Domain.Exceptions;
public static class LanguageException
{
    public class LanguageNotFoundException : NotFoundException
    {
        public LanguageNotFoundException(Guid languageId)
            : base($"The language with the id {languageId} was not found.") { }
    }

    public class DefaultLanguageCannotBeChangedException : BadRequestException
    {
        public DefaultLanguageCannotBeChangedException(string langCode)
            : base($"The default language {langCode} cannot be deleted or deactivated.") { }
    }
}
