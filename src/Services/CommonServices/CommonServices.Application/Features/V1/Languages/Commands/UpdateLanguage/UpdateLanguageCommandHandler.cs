using AutoMapper;
using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Application.Features.V1.Languages.Common.Services;
using CommonServices.Domain.Constants;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using static CommonServices.Domain.Exceptions.LanguageException;

namespace CommonServices.Application.Features.V1.Languages.Commands.UpdateLanguage;
internal sealed class UpdateLanguageCommandHandler : ICommandHandler<UpdateLanguageCommand, LanguageDetailDto>
{
    private readonly IRepositoryBase<Language, Guid> _languageRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateLanguageCommandHandler(IRepositoryBase<Language, Guid> languageRepository, ICurrentUser currentUser, IMapper mapper)
    {
        _languageRepository = languageRepository ?? throw new ArgumentNullException(nameof(languageRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<LanguageDetailDto>> Handle(UpdateLanguageCommand request, CancellationToken cancellationToken)
    {
        var languageEntity = await _languageRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new LanguageNotFoundException(request.Id);

        // The default-language code is what the main Name columns of every master data table refer to.
        if (string.Equals(languageEntity.Code, LanguageCodes.Default, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Code, LanguageCodes.Default, StringComparison.OrdinalIgnoreCase))
            throw new DefaultLanguageCannotBeChangedException(LanguageCodes.Default);

        if (await _languageRepository.FindAll(x => x.Code == request.Code && x.Id != request.Id).AnyAsync(cancellationToken))
            return Result.Failure<LanguageDetailDto>(MasterDataErrors.CodeAlreadyExists(nameof(Language), request.Code));

        var translations = await LanguageTranslationBuilder.BuildAsync(_languageRepository,
            request.Name, request.Description, request.Translations, cancellationToken);

        // The entity is tracked, so the changes (translations included) are saved by the transaction behavior without calling Update().
        languageEntity.Update(request.Code, request.Name, request.Description, request.FlagImagePath, request.Platform, _currentUser.UserId.ToString());
        languageEntity.SetTranslations(translations);

        var result = _mapper.Map<LanguageDetailDto>(languageEntity);

        return Result.Success(result);
    }
}
