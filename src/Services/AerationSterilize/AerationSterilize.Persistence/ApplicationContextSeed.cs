using AerationSterilize.Domain.Entities;
using AerationSterilize.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shared.Common.Constants.Authorization;
using Action = AerationSterilize.Domain.Entities.Identity.Action;

namespace AerationSterilize.Persistence;
public class ApplicationContextSeed
{
    private static readonly Guid AdminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly string[] FunctionIds =
    [
        FunctionCode.DASHBOARD, FunctionCode.SYSTEM, FunctionCode.USER, FunctionCode.ROLE, FunctionCode.PRODUCT,
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
            await SeedProductsAsync();
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
            Email = "admin@aerationsterilize.local",
            NormalizedEmail = "ADMIN@AERATIONSTERILIZE.LOCAL",
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

    // Functions, actions and permissions are additive: entries missing from an existing database are inserted,
    // so new functions (e.g. the CommonServices master data) reach databases that were seeded before them.
    private async Task SeedFunctionsAsync()
    {
        var functions = new[]
        {
            new Function { Id = FunctionCode.DASHBOARD,   Name = "Dashboard",   Url = "/dashboard",            ParrentId = string.Empty,            SortOrder = 1, CssClass = "fa-dashboard", IsActive = true },
            new Function { Id = FunctionCode.SYSTEM,      Name = "System",      Url = "/system",               ParrentId = string.Empty,            SortOrder = 2, CssClass = "fa-cogs",      IsActive = true },
            new Function { Id = FunctionCode.USER,        Name = "User",        Url = "/system/user",          ParrentId = FunctionCode.SYSTEM,      SortOrder = 1, CssClass = "fa-user",      IsActive = true },
            new Function { Id = FunctionCode.ROLE,        Name = "Role",        Url = "/system/role",          ParrentId = FunctionCode.SYSTEM,      SortOrder = 2, CssClass = "fa-users",     IsActive = true },
            new Function { Id = FunctionCode.PRODUCT,     Name = "Product",     Url = "/product",              ParrentId = string.Empty,            SortOrder = 3, CssClass = "fa-cube",      IsActive = true },

            // Master data (CommonServices)
            new Function { Id = FunctionCode.MASTER_DATA, Name = "Master Data", Url = "/master-data",          ParrentId = string.Empty,            SortOrder = 4, CssClass = "fa-database",  IsActive = true },
            new Function { Id = FunctionCode.LANGUAGE,    Name = "Language",    Url = "/master-data/language", ParrentId = FunctionCode.MASTER_DATA, SortOrder = 1, CssClass = "fa-language",  IsActive = true },
            new Function { Id = FunctionCode.REGION,      Name = "Region",      Url = "/master-data/region",   ParrentId = FunctionCode.MASTER_DATA, SortOrder = 2, CssClass = "fa-globe",     IsActive = true },
            new Function { Id = FunctionCode.CURRENCY,    Name = "Currency",    Url = "/master-data/currency", ParrentId = FunctionCode.MASTER_DATA, SortOrder = 3, CssClass = "fa-money",     IsActive = true },
            new Function { Id = FunctionCode.COUNTRY,     Name = "Country",     Url = "/master-data/country",  ParrentId = FunctionCode.MASTER_DATA, SortOrder = 4, CssClass = "fa-flag",      IsActive = true },
            new Function { Id = FunctionCode.CITY,        Name = "City",        Url = "/master-data/city",     ParrentId = FunctionCode.MASTER_DATA, SortOrder = 5, CssClass = "fa-building",  IsActive = true },
            new Function { Id = FunctionCode.BANK,        Name = "Bank",        Url = "/master-data/bank",     ParrentId = FunctionCode.MASTER_DATA, SortOrder = 6, CssClass = "fa-bank",      IsActive = true }
        };

        var existingIds = await _context.Functions.Select(f => f.Id).ToListAsync();
        var missing = functions.Where(f => !existingIds.Contains(f.Id)).ToList();
        if (missing.Count == 0) return;

        await _context.Functions.AddRangeAsync(missing);
        _logger.Information("Seeded {Count} functions.", missing.Count);
    }

    private async Task SeedActionsAsync()
    {
        var actions = new[]
        {
            new Action { Id = CommandCode.VIEW,       Name = "View",       SortOrder = 1, IsActive = true },
            new Action { Id = CommandCode.CREATE,     Name = "Create",     SortOrder = 2, IsActive = true },
            new Action { Id = CommandCode.UPDATE,     Name = "Update",     SortOrder = 3, IsActive = true },
            new Action { Id = CommandCode.DELETE,     Name = "Delete",     SortOrder = 4, IsActive = true },
            new Action { Id = CommandCode.ACTIVATE,   Name = "Activate",   SortOrder = 5, IsActive = true },
            new Action { Id = CommandCode.INACTIVATE, Name = "Inactivate", SortOrder = 6, IsActive = true }
        };

        var existingIds = await _context.Actions.Select(a => a.Id).ToListAsync();
        var missing = actions.Where(a => !existingIds.Contains(a.Id)).ToList();
        if (missing.Count == 0) return;

        await _context.Actions.AddRangeAsync(missing);
        _logger.Information("Seeded {Count} actions.", missing.Count);
    }

    private async Task SeedActionInFunctionsAsync()
    {
        var existing = (await _context.ActionInFunctions.Select(x => new { x.FunctionId, x.ActionId }).ToListAsync())
            .Select(x => (x.FunctionId, x.ActionId))
            .ToHashSet();

        var missing = FunctionActionPairs()
            .Where(p => !existing.Contains(p))
            .Select(p => new ActionInFunction { FunctionId = p.FunctionId, ActionId = p.ActionId })
            .ToList();
        if (missing.Count == 0) return;

        await _context.ActionInFunctions.AddRangeAsync(missing);
        _logger.Information("Seeded {Count} action-in-functions.", missing.Count);
    }

    private async Task SeedPermissionsAsync()
    {
        var existing = (await _context.Permissions
                .Where(p => p.RoleId == AdminRoleId)
                .Select(p => new { p.FunctionId, p.ActionId })
                .ToListAsync())
            .Select(x => (x.FunctionId, x.ActionId))
            .ToHashSet();

        var missing = FunctionActionPairs()
            .Where(p => !existing.Contains(p))
            .Select(p => new Permission
            {
                RoleId = AdminRoleId,
                FunctionId = p.FunctionId,
                ActionId = p.ActionId
            })
            .ToList();
        if (missing.Count == 0) return;

        await _context.Permissions.AddRangeAsync(missing);
        _logger.Information("Seeded {Count} permissions for Administrator.", missing.Count);
    }

    private static IEnumerable<(string FunctionId, string ActionId)> FunctionActionPairs()
        => FunctionIds.SelectMany(f => ActionIds.Select(a => (f, a)));

    private async Task SeedProductsAsync()
    {
        if (await _context.Products.AnyAsync()) return;

        var products = new[]
        {
            new Product(
                Guid.Parse("aaaaaaaa-0001-0000-0000-000000000001"),
                "Aeration Module A1",
                1500.00m,
                "High-efficiency aeration module for medium-sized tanks."),
            new Product(
                Guid.Parse("aaaaaaaa-0001-0000-0000-000000000002"),
                "Sterilizer Unit S2",
                2750.50m,
                "UV-based sterilizer unit with auto-cleaning support."),
            new Product(
                Guid.Parse("aaaaaaaa-0001-0000-0000-000000000003"),
                "Ozone Generator O3",
                3200.00m,
                "Industrial ozone generator for disinfection processes."),
            new Product(
                Guid.Parse("aaaaaaaa-0001-0000-0000-000000000004"),
                "Diffuser Plate D4",
                450.75m,
                "Ceramic diffuser plate producing fine bubbles for aeration.")
        };

        var now = DateTimeOffset.UtcNow;
        foreach (var p in products)
        {
            p.CreatedDate = now;
        }

        await _context.Products.AddRangeAsync(products);
        _logger.Information("Seeded {Count} products.", products.Length);
    }
}
