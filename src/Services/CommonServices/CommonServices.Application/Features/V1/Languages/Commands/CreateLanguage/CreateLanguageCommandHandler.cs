using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Application.Features.V1.Languages.Common.Services;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Languages.Commands.CreateLanguage;
internal sealed class CreateLanguageCommandHandler : ICommandHandler<CreateLanguageCommand, Guid>
{
    private readonly IRepositoryBase<Language, Guid> _languageRepository;
    private readonly ICurrentUser _currentUser;

    public CreateLanguageCommandHandler(IRepositoryBase<Language, Guid> languageRepository, ICurrentUser currentUser)
    {
        _languageRepository = languageRepository ?? throw new ArgumentNullException(nameof(languageRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<Guid>> Handle(CreateLanguageCommand request, CancellationToken cancellationToken)
    {
        if (await _languageRepository.FindAll(x => x.Code == request.Code).AnyAsync(cancellationToken))
            return Result.Failure<Guid>(MasterDataErrors.CodeAlreadyExists(nameof(Language), request.Code));

        var translations = await LanguageTranslationBuilder.BuildAsync(_languageRepository,
            request.Name, request.Description, request.Translations, cancellationToken);

        var languageEntity = Language.Create(request.Code, request.Name, request.Description,
            request.FlagImagePath, request.Platform, _currentUser.UserId.ToString());
        languageEntity.SetTranslations(translations);

        _languageRepository.Add(languageEntity);

        return Result.Success(languageEntity.Id);
    }
}
