namespace CommonServices.Persistence.Constants;
internal class TableNames
{
    // *********** Plural Nouns ***********
    internal const string Actions = nameof(Actions);
    internal const string Functions = nameof(Functions);
    internal const string ActionInFunctions = nameof(ActionInFunctions);
    internal const string Permissions = nameof(Permissions);

    internal const string AppUsers = nameof(AppUsers);
    internal const string AppRoles = nameof(AppRoles);
    internal const string AppUserRoles = nameof(AppUserRoles);

    internal const string AppUserClaims = nameof(AppUserClaims); // IdentityUserClaim
    internal const string AppRoleClaims = nameof(AppRoleClaims); // IdentityRoleClaim
    internal const string AppUserLogins = nameof(AppUserLogins); // IdentityRoleClaim
    internal const string AppUserTokens = nameof(AppUserTokens); // IdentityUserToken

    internal const string UserSessions = nameof(UserSessions);

    // *********** Master Data ***********
    internal const string Languages = nameof(Languages);
    internal const string LanguageTranslations = nameof(LanguageTranslations);
    internal const string Regions = nameof(Regions);
    internal const string RegionTranslations = nameof(RegionTranslations);
    internal const string Currencies = nameof(Currencies);
    internal const string CurrencyTranslations = nameof(CurrencyTranslations);
    internal const string Countries = nameof(Countries);
    internal const string CountryTranslations = nameof(CountryTranslations);
    internal const string Cities = nameof(Cities);
    internal const string CityTranslations = nameof(CityTranslations);
    internal const string Banks = nameof(Banks);
    internal const string BankTranslations = nameof(BankTranslations);
}
