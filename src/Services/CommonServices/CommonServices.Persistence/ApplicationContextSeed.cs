using CommonServices.Domain.Constants;
using CommonServices.Domain.Entities.MasterData;
using CommonServices.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace CommonServices.Persistence;
public class ApplicationContextSeed
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger _logger;

    public ApplicationContextSeed(
        ApplicationDbContext context,
        ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync()
    {
        try
        {
            if (_context.Database.IsSqlServer())
            {
                await _context.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred while initialzing the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await SeedLanguagesAsync();
            await SeedCurrenciesAsync();
            await SeedGeographyAsync();
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task SeedLanguagesAsync()
    {
        if (await _context.Languages.IgnoreQueryFilters().AnyAsync()) return;

        var vietnamese = Language.Create(LanguageCodes.Default, "Tiếng Việt", "Tiếng Việt", null, LanguagePlatform.All, null);
        vietnamese.SetTranslations(
        [
            (LanguageCodes.Default, "Tiếng Việt", "Tiếng Việt"),
            ("en-US", "Vietnamese", "Vietnamese")
        ]);

        var english = Language.Create("en-US", "Tiếng Anh", "Tiếng Anh", null, LanguagePlatform.All, null);
        english.SetTranslations(
        [
            (LanguageCodes.Default, "Tiếng Anh", "Tiếng Anh"),
            ("en-US", "English", "English")
        ]);

        await _context.Languages.AddRangeAsync(vietnamese, english);
        _logger.Information("Seeded {Count} languages.", 2);
    }

    private async Task SeedCurrenciesAsync()
    {
        if (await _context.Currencies.IgnoreQueryFilters().AnyAsync()) return;

        var vnd = Currency.Create("VND", "Việt Nam Đồng", "₫", true, null);
        vnd.SetTranslations([(LanguageCodes.Default, "Việt Nam Đồng"), ("en-US", "Vietnamese Dong")]);

        var usd = Currency.Create("USD", "Đô la Mỹ", "$", true, null);
        usd.SetTranslations([(LanguageCodes.Default, "Đô la Mỹ"), ("en-US", "US Dollar")]);

        await _context.Currencies.AddRangeAsync(vnd, usd);
        _logger.Information("Seeded {Count} currencies.", 2);
    }

    private async Task SeedGeographyAsync()
    {
        if (await _context.Regions.IgnoreQueryFilters().AnyAsync()) return;

        var asia = Region.Create("ASIA", "Châu Á", null, 1, null);
        asia.SetTranslations([(LanguageCodes.Default, "Châu Á", null), ("en-US", "Asia", null)]);

        // Currencies are added in the same SaveChanges, so link through the tracked entity rather than a database lookup.
        var vnd = _context.Currencies.Local.FirstOrDefault(c => c.Code == "VND");
        var vietnam = Country.Create("VN", "Việt Nam", asia.Id, vnd?.Id, null);
        vietnam.SetTranslations([(LanguageCodes.Default, "Việt Nam"), ("en-US", "Vietnam")]);

        var hanoi = City.Create("HAN", "Hà Nội", vietnam.Id, null);
        hanoi.SetTranslations([(LanguageCodes.Default, "Hà Nội"), ("en-US", "Hanoi")]);

        var hoChiMinh = City.Create("SGN", "Hồ Chí Minh", vietnam.Id, null);
        hoChiMinh.SetTranslations([(LanguageCodes.Default, "Hồ Chí Minh"), ("en-US", "Ho Chi Minh City")]);

        await _context.Regions.AddAsync(asia);
        await _context.Countries.AddAsync(vietnam);
        await _context.Cities.AddRangeAsync(hanoi, hoChiMinh);
        _logger.Information("Seeded sample region, country and cities.");
    }
}
