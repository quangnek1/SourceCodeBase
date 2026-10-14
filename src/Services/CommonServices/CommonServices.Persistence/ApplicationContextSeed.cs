using CommonServices.Domain.Constants;
using CommonServices.Domain.Entities.Identity;
using CommonServices.Domain.Entities.MasterData;
using CommonServices.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shared.Common.Constants.Authorization;
using Action = CommonServices.Domain.Entities.Identity.Action;

namespace CommonServices.Persistence;
public class ApplicationContextSeed
{
    private static readonly Guid AdminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly string[] FunctionIds =
    [
        FunctionCode.DASHBOARD, FunctionCode.SYSTEM, FunctionCode.USER, FunctionCode.ROLE,
        FunctionCode.MASTER_DATA, FunctionCode.LANGUAGE, FunctionCode.REGION, FunctionCode.CURRENCY,
        FunctionCode.COUNTRY, FunctionCode.CITY, FunctionCode.BANK
    ];

    private static readonly string[] ActionIds =
    [
        CommandCode.VIEW, CommandCode.CREATE, CommandCode.UPDATE, CommandCode.DELETE,
        CommandCode.ACTIVATE, CommandCode.INACTIVATE
    ];

    private readonly ApplicationDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly ILogger _logger;

    public ApplicationContextSeed(
        ApplicationDbContext context,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
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
            await SeedRolesAsync();
            await SeedDefaultUserAsync();
            await SeedFunctionsAsync();
            await SeedActionsAsync();
            await SeedActionInFunctionsAsync();
            await SeedPermissionsAsync();
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

    private async Task SeedRolesAsync()
    {
        if (await _roleManager.Roles.AnyAsync()) return;

        var roles = new[]
        {
            new AppRole
            {
                Id = AdminRoleId,
                Name = "Administrator",
                NormalizedName = "ADMINISTRATOR",
                RoleCode = "ADMIN",
                Description = "System administrator with full access"
            },
            new AppRole
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "User",
                NormalizedName = "USER",
                RoleCode = "USER",
                Description = "Standard application user"
            }
        };

        foreach (var role in roles)
        {
            await _roleManager.CreateAsync(role);
        }

        _logger.Information("Seeded {Count} roles.", roles.Length);
    }

    private async Task SeedDefaultUserAsync()
    {
        if (await _userManager.Users.AnyAsync()) return;

        var admin = new AppUser
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@commonservices.local",
            NormalizedEmail = "ADMIN@COMMONSERVICES.LOCAL",
            EmailConfirmed = true,
            FirstName = "System",
            LastName = "Administrator",
            FullName = "System Administrator",
            DayOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsDirector = true,
            IsHeadOfDepartment = true,
            ManagerId = Guid.Empty,
            PositionId = Guid.Empty,
            IsReceipient = 1,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var result = await _userManager.CreateAsync(admin, "Admin@123");
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(admin, "Administrator");
            _logger.Information("Seeded default admin user.");
        }
        else
        {
            _logger.Warning("Failed to seed admin user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedFunctionsAsync()
    {
        if (await _context.Functions.AnyAsync()) return;

        var functions = new[]
        {
            new Function { Id = FunctionCode.DASHBOARD,   Name = "Dashboard",   Url = "/dashboard",              ParrentId = string.Empty,            SortOrder = 1, CssClass = "fa-dashboard", IsActive = true },
            new Function { Id = FunctionCode.SYSTEM,      Name = "System",      Url = "/system",                 ParrentId = string.Empty,            SortOrder = 2, CssClass = "fa-cogs",      IsActive = true },
            new Function { Id = FunctionCode.USER,        Name = "User",        Url = "/system/user",            ParrentId = FunctionCode.SYSTEM,      SortOrder = 1, CssClass = "fa-user",      IsActive = true },
            new Function { Id = FunctionCode.ROLE,        Name = "Role",        Url = "/system/role",            ParrentId = FunctionCode.SYSTEM,      SortOrder = 2, CssClass = "fa-users",     IsActive = true },
            new Function { Id = FunctionCode.MASTER_DATA, Name = "Master Data", Url = "/master-data",            ParrentId = string.Empty,            SortOrder = 3, CssClass = "fa-database",  IsActive = true },
            new Function { Id = FunctionCode.LANGUAGE,    Name = "Language",    Url = "/master-data/language",   ParrentId = FunctionCode.MASTER_DATA, SortOrder = 1, CssClass = "fa-language",  IsActive = true },
            new Function { Id = FunctionCode.REGION,      Name = "Region",      Url = "/master-data/region",     ParrentId = FunctionCode.MASTER_DATA, SortOrder = 2, CssClass = "fa-globe",     IsActive = true },
            new Function { Id = FunctionCode.CURRENCY,    Name = "Currency",    Url = "/master-data/currency",   ParrentId = FunctionCode.MASTER_DATA, SortOrder = 3, CssClass = "fa-money",     IsActive = true },
            new Function { Id = FunctionCode.COUNTRY,     Name = "Country",     Url = "/master-data/country",    ParrentId = FunctionCode.MASTER_DATA, SortOrder = 4, CssClass = "fa-flag",      IsActive = true },
            new Function { Id = FunctionCode.CITY,        Name = "City",        Url = "/master-data/city",       ParrentId = FunctionCode.MASTER_DATA, SortOrder = 5, CssClass = "fa-building",  IsActive = true },
            new Function { Id = FunctionCode.BANK,        Name = "Bank",        Url = "/master-data/bank",       ParrentId = FunctionCode.MASTER_DATA, SortOrder = 6, CssClass = "fa-bank",      IsActive = true }
        };

        await _context.Functions.AddRangeAsync(functions);
        _logger.Information("Seeded {Count} functions.", functions.Length);
    }

    private async Task SeedActionsAsync()
    {
        if (await _context.Actions.AnyAsync()) return;

        var actions = new[]
        {
            new Action { Id = CommandCode.VIEW,       Name = "View",       SortOrder = 1, IsActive = true },
            new Action { Id = CommandCode.CREATE,     Name = "Create",     SortOrder = 2, IsActive = true },
            new Action { Id = CommandCode.UPDATE,     Name = "Update",     SortOrder = 3, IsActive = true },
            new Action { Id = CommandCode.DELETE,     Name = "Delete",     SortOrder = 4, IsActive = true },
            new Action { Id = CommandCode.ACTIVATE,   Name = "Activate",   SortOrder = 5, IsActive = true },
            new Action { Id = CommandCode.INACTIVATE, Name = "Inactivate", SortOrder = 6, IsActive = true }
        };

        await _context.Actions.AddRangeAsync(actions);
        _logger.Information("Seeded {Count} actions.", actions.Length);
    }

    private async Task SeedActionInFunctionsAsync()
    {
        if (await _context.ActionInFunctions.AnyAsync()) return;

        var actionInFunctions = FunctionIds
            .SelectMany(f => ActionIds.Select(a => new ActionInFunction { FunctionId = f, ActionId = a }))
            .ToList();

        await _context.ActionInFunctions.AddRangeAsync(actionInFunctions);
        _logger.Information("Seeded {Count} action-in-functions.", actionInFunctions.Count);
    }

    private async Task SeedPermissionsAsync()
    {
        if (await _context.Permissions.AnyAsync()) return;

        var permissions = FunctionIds
            .SelectMany(f => ActionIds.Select(a => new Permission
            {
                RoleId = AdminRoleId,
                FunctionId = f,
                ActionId = a
            }))
            .ToList();

        await _context.Permissions.AddRangeAsync(permissions);
        _logger.Information("Seeded {Count} permissions for Administrator.", permissions.Count);
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
