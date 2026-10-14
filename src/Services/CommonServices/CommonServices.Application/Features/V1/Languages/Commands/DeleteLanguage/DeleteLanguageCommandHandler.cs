using CommonServices.Application.Common;
using CommonServices.Domain.Constants;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.LanguageException;

namespace CommonServices.Application.Features.V1.Languages.Commands.DeleteLanguage;
internal sealed class DeleteLanguageCommandHandler : ICommandHandler<DeleteLanguageCommand>
{
    private readonly IRepositoryBase<Language, Guid> _languageRepository;
    private readonly ICurrentUser _currentUser;

    public DeleteLanguageCommandHandler(IRepositoryBase<Language, Guid> languageRepository, ICurrentUser currentUser)
    {
        _languageRepository = languageRepository ?? throw new ArgumentNullException(nameof(languageRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(DeleteLanguageCommand request, CancellationToken cancellationToken)
    {
        var languageEntity = await _languageRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new LanguageNotFoundException(request.Id);

        if (string.Equals(languageEntity.Code, LanguageCodes.Default, StringComparison.OrdinalIgnoreCase))
            throw new DefaultLanguageCannotBeChangedException(LanguageCodes.Default);

        // Soft delete: the row is kept with Status = Deleted.
        languageEntity.Delete(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
