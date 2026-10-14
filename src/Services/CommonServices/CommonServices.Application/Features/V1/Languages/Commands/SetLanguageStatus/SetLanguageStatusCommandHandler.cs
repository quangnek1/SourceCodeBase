using CommonServices.Application.Common;
using CommonServices.Domain.Constants;
using CommonServices.Domain.Entities.MasterData;
using CommonServices.Domain.Enums;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.LanguageException;

namespace CommonServices.Application.Features.V1.Languages.Commands.SetLanguageStatus;
internal sealed class SetLanguageStatusCommandHandler : ICommandHandler<SetLanguageStatusCommand>
{
    private readonly IRepositoryBase<Language, Guid> _languageRepository;
    private readonly ICurrentUser _currentUser;

    public SetLanguageStatusCommandHandler(IRepositoryBase<Language, Guid> languageRepository, ICurrentUser currentUser)
    {
        _languageRepository = languageRepository ?? throw new ArgumentNullException(nameof(languageRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(SetLanguageStatusCommand request, CancellationToken cancellationToken)
    {
        var languageEntity = await _languageRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new LanguageNotFoundException(request.Id);

        if (request.Status == ObjectStatus.Active)
        {
            languageEntity.Activate(_currentUser.UserId.ToString());
            return Result.Success();
        }

        if (string.Equals(languageEntity.Code, LanguageCodes.Default, StringComparison.OrdinalIgnoreCase))
            throw new DefaultLanguageCannotBeChangedException(LanguageCodes.Default);

        languageEntity.Deactivate(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
