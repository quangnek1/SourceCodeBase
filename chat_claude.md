# Tổng hợp: gom đăng nhập / phân quyền về AerationSterilize

Ngày: 2026-10-15 — nhánh `add-common-services` (chưa commit)

## 1. Bỏ phần đăng nhập / phân quyền bị lặp ở CommonServices

**Vấn đề:** CommonServices có riêng login, user, role, permission, giống hệt AerationSterilize.

**Cách làm:** AerationSterilize là nơi duy nhất đăng nhập và cấp token (token chứa sẵn quyền). CommonServices chỉ kiểm tra token của Aeration và đọc quyền từ token.

**Đã xoá khỏi CommonServices**
- `AuthController`, toàn bộ `Features/V1/Auth` (login, register, refresh-token, logout).
- Entity `AppUser`, `AppRole`, `Permission`, `Function`, `Action`, `ActionInFunction`, `UserSession`, các cấu hình EF tương ứng, `PermissionService`, `AuthException`.
- `ApplicationDbContext` đổi từ `IdentityDbContext` sang `DbContext`. Bỏ `AddIdentity`, bỏ seed user/role/permission, bỏ package Identity.
- Migration `RemoveIdentity`: drop 12 bảng identity, giữ nguyên bảng master data.

**Gom code dùng chung vào BuildingBlocks/Infrastructure**
- `Authorization/PermissionAttribute`, `PermissionRequirement`, `PermissionAuthorizationHandler`: trước đây copy y hệt ở 2 service.
- `Extensions/AuthenticationServiceCollectionExtensions.cs`: `AddJwtAuthentication()` và `AddPermissionAuthorization()`, cả 2 service đều gọi.

**Sửa để chạy được giữa 2 service**
- Danh sách token đã thu hồi (logout) trên Redis trước đây bị gắn tiền tố khác nhau (`aeration:` và `common:`), nên CommonServices không thấy token đã logout. Giờ key dùng chung, không còn tiền tố (`RedisTokenRevocationService`).
- `AddRedisCache` không còn bắt buộc service phải có `IPermissionService`.
- Seed của Aeration thêm các function master data (MASTER_DATA, LANGUAGE, REGION, CURRENCY, COUNTRY, CITY, BANK) và 2 action ACTIVATE/INACTIVATE. Seed giờ chỉ thêm những gì còn thiếu, nên DB đang chạy cũng được bổ sung.
- `appsettings.json` của CommonServices: `JwtOptions` (SecretKey, Issuer, Audience) phải giống Aeration. File này bị gitignore nên mỗi môi trường phải tự cấu hình.

## 2. Kiểm tra và sửa phân quyền theo dữ liệu (AccessLevel)

**Lỗi tìm thấy ở Aeration và cách sửa**

| # | Lỗi | Sửa |
|---|---|---|
| 1 | API tạo/sửa/xoá Product không có `[Permission]`, ai cũng gọi được | Thêm `[Permission(PRODUCT, CREATE/UPDATE/DELETE)]` |
| 2 | Token không có `department_id`, nên mức Department lọc sai | Login và refresh ghi `department_id` vào token (`UserClaim.DepartmentId`) |
| 3 | `ApplyDataScope` không được gọi ở đâu cả | `DataScope` + `ApplyDataScope` + `Covers` chuyển sang `Contracts/Identity/DataScope.cs` và dùng trong mọi handler Product |
| 4 | `Product.CreatedBy` và `DepartmentId` mặc định là `Guid.NewGuid()` | Bỏ giá trị mặc định; gán theo user hiện tại khi tạo |
| 5 | Quyền mức None vẫn qua được `[Permission]` | Handler chặn mức None |
| 6 | Cache query Product dùng chung giữa các user | Cache key có thêm phạm vi của user (`product:own:{userId}:...`) |
| 7 | Migration cũ thêm cột `AccessLevel` với mặc định 0, nên 20 quyền cũ thành None (phát hiện khi test) | Migration `FixPermissionAccessLevel`: đặt các dòng đó về 3 (All) |

CommonServices không cần lọc theo AccessLevel vì master data là danh mục dùng chung; chỉ cần `[Permission]`.

## 3. Chạy và test toàn bộ

- Bật Docker và Redis (`docker-compose.yml`). Aeration chạy ở cổng 5000, CommonServices ở cổng 5001. Migrate và seed tự động khi khởi động.
- Script: `tests/ApiTests/run_api_tests.py`. Chạy lại được; tự tạo 5 role và 10 user test (mật khẩu `Test@123`, phòng A/B, có cả user không phòng và user 2 role).
- Kết quả: **177 đạt, 0 lỗi, 2 ghi nhận**. Chi tiết từng API ở `API_TEST_REPORT.md`.

**Hai hành vi nên biết (chưa sửa)**
- Đổi quyền rồi refresh token ngay vẫn dùng quyền cũ, vì quyền được cache trên Redis 30 phút (`aeration:perm:{userId}`).
- API refresh-token trả refresh token mới trong body nhưng không set lại cookie, nên client dùng cookie sẽ lỗi ở lần refresh thứ hai.

## 4. Cách phân quyền hoạt động

**Bảng `Permissions`:** `RoleId + FunctionId + ActionId` cho biết được làm gì (quyền chức năng). `AccessLevel` cho biết làm trên những bản ghi nào (quyền dữ liệu).

| AccessLevel | Ý nghĩa | Điều kiện lọc |
|---|---|---|
| 0 None | Không được gì (403) | — |
| 1 Own | Bản ghi mình tạo | `CreatedBy = userId` |
| 2 Department | Bản ghi của phòng mình | `DepartmentId = phòng của user` |
| 3 All | Tất cả | không lọc |

User có nhiều role thì mỗi quyền lấy mức cao nhất.

**Luồng chạy**
1. **Đăng nhập:** Aeration ghép role của user với `Permissions` rồi ghi vào token các claim `permission` (`PRODUCT.VIEW`), `permission_level` (`PRODUCT.VIEW:2`), `department_id`, `sub` (userId).
2. **Gọi API:**
   - Lớp 1, `[Permission(Function, Action)]`: không có quyền hoặc mức None thì trả 403.
   - Lớp 2, trong handler: `_currentUser.GetDataScope(...)`. Danh sách dùng `ApplyDataScope(scope)`; xem/sửa/xoá 1 bản ghi dùng `scope.Covers(entity)`, ngoài phạm vi thì trả 404.
3. Bản ghi phải implement `IDataOwned` (`CreatedBy`, `DepartmentId`), gán lúc tạo từ token của người tạo.

**Áp dụng cho bảng mới (ví dụ Order)**
1. Thêm `FunctionCode.ORDER`, rồi seed vào `Functions` và `ActionInFunctions`, và cấp quyền trong `Permissions`.
2. Entity `Order : IDataOwned`, kèm migration thêm 2 cột.
3. Khi tạo, gán `CreatedBy = _currentUser.UserId`, `DepartmentId = _currentUser.DepartmentId`.
4. Mỗi API gắn đúng 1 `[Permission(FunctionCode.ORDER, ...)]`.
5. Handler lọc bằng `ApplyDataScope` / `Covers`.
6. Query có cache thì truyền `DataScope` vào query và đưa `Scope.CacheKey` vào cache key.

**Lưu ý vận hành:** quyền nằm trong token (60 phút) và cache Redis (30 phút). Sửa `Permissions` hoặc đổi phòng của user thì cần login lại; muốn có hiệu lực ngay thì xoá thêm key `aeration:perm:{userId}`.

## 5. Về việc ghi nhiều `[Permission]`

- Mỗi API chỉ có 1 dòng `[Permission]`; 4 dòng là của 4 API khác nhau, không phải đặt chồng.
- Không nên cài cứng "có DELETE thì có tất cả", vì nghiệp vụ thường không theo bậc thang (ví dụ kế toán được xoá nhưng không được tạo). Muốn role có đủ quyền thì cấp đủ dòng trong `Permissions`.
- Có thể rút gọn bằng cách suy quyền từ HTTP method (`[PermissionFunction(ORDER)]` trên controller). Cách này gọn hơn nhưng dễ cấp nhầm quyền cho các API PUT đặc biệt (activate, approve).
- **Khuyến nghị:** giữ cách ghi rõ từng API, và thêm test kiến trúc (trong `tests/AerationSterilize.Architecture.Tests`) báo lỗi khi có API thiếu `[Permission]`.

## 6. File thay đổi chính

- BuildingBlocks: `Infrastructure/Authorization/*`, `Infrastructure/Extensions/AuthenticationServiceCollectionExtensions.cs`, `Infrastructure/Identity/RedisTokenRevocationService.cs`, `Infrastructure/Identity/TokenService.cs`, `Contracts/Identity/DataScope.cs`, `Shared/DTOs/Identity/TokenRequest.cs`, `Shared/Common/Constants/Authorization/FunctionCode.cs` (`UserClaim`).
- AerationSterilize: `ProductsController`, `CurrentUser`, `ICurrentUser`, các handler/query của Product, `Product.cs`, `ApplicationContextSeed.cs`, migration `FixPermissionAccessLevel`.
- CommonServices: xoá phần identity/auth, `ApplicationDbContext`, seed, DI, migration `RemoveIdentity`.
- Test: `tests/ApiTests/run_api_tests.py`, `API_TEST_REPORT.md`.




/clear
hiện tại ở Aeration đã có logic đăng nhập, phân quyền.
nhưng ở C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices
bị lặp phần login phân quyền permission.
hãy sửa lại phân quyền login, liên quan authen autho của CommonServices
gọi logic từ aeration xuống
Show more
9:05 PM
Mình sẽ xem cấu trúc code trước.



Bash List project structure and last commit
IN
ls src src/Services && find src/Services/CommonServices -type f \( -name "*.cs" -o -name "*.csproj" -o -name "*.json" \) | grep -v -E "/(bin|obj)/" && git show --stat HEAD | head -80

OUT
<persisted-output>
Output too large (31.8KB). Full output saved to: C:\Users\DuyQuang\.claude\projects\c--Users-DuyQuang-Desktop-SourceCodeBase\39023dd3-17a2-4865-b587-db2f3efa5ce2\tool-results\bo9l0icd1.txt

Preview (first 2KB):
src:
BuildingBlocks
Services

src/Services:
AerationSterilize
CommonServices
obj
src/Services/CommonServices/CommonServices.API/Abstractions/ApiController.cs
src/Services/CommonServices/CommonServices.API/Abstractions/ApiEndpoint.cs
src/Services/CommonServices/CommonServices.API/appsettings.Development.json
src/Services/CommonServices/CommonServices.API/appsettings.json
src/Services/CommonServices/CommonServices.API/AssemblyReference.cs
src/Services/CommonServices/CommonServices.API/Authorization/PermissionAttribute.cs
src/Services/CommonServices/CommonServices.API/Authorization/PermissionAuthorizationHandler.cs
src/Services/CommonServices/CommonServices.API/Authorization/PermissionRequirement.cs
src/Services/CommonServices/CommonServices.API/CommonServices.API.csproj
src/Services/CommonServices/CommonServices.API/Controllers/V1/AuthController.cs
src/Services/CommonServices/CommonServices.API/Controllers/V1/BanksController.cs
src/Services/CommonServices/CommonServices.API/Controllers/V1/CitiesController.cs
src/Services/CommonServices/CommonServices.API/Controllers/V1/CountriesController.cs
src/Services/CommonServices/CommonServices.API/Controllers/V1/CurrenciesController.cs
src/Services/CommonServices/CommonServices.API/Controllers/V1/LanguagesController.cs
src/Services/CommonServices/CommonServices.API/Controllers/V1/MasterDataController.cs
src/Services/CommonServices/CommonServices.API/Controllers/V1/RegionsController.cs
src/Services/CommonServices/CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
src/Services/CommonServices/CommonServices.API/DependencyInjection/Extensions/SwaggerExtensions.cs
src/Services/CommonServices/CommonServices.API/DependencyInjection/Options/ConfigureSwaggerOptions.cs
src/Services/CommonServices/CommonServices.API/Program.cs
src/Services/CommonServices/CommonServices.API/Properties/launchSettings.json
src/Services/CommonServices/CommonServices.API/Services/CurrentUser.cs
...
</persisted-output>
Bash Find auth-related files in CommonServices
IN
find src/Services/CommonServices src/BuildingBlocks -type f \( -name "*.cs" -o -name "*.csproj" \) | grep -v -E "/(bin|obj)/" | grep -iE "auth|permission|user|role|token|jwt|login|identity|csproj|Program|ServiceCollection|DependencyInjection|CurrentUser" ; echo ---; find src/Services/AerationSterilize -maxdepth 2 -name "*.csproj" -o -maxdepth 2 -type d | grep -v -E "/(bin|obj)"; ls *.sln* 2>/dev/null

OUT
src/Services/CommonServices/CommonServices.API/Authorization/PermissionAttribute.cs
src/Services/CommonServices/CommonServices.API/Authorization/PermissionAuthorizationHandler.cs
src/Services/CommonServices/CommonServices.API/Authorization/PermissionRequirement.cs
src/Services/CommonServices/CommonServices.API/CommonServices.API.csproj
src/Services/CommonServices/CommonServices.API/Controllers/V1/AuthController.cs
src/Services/CommonServices/CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
src/Services/CommonServices/CommonServices.API/DependencyInjection/Extensions/SwaggerExtensions.cs
src/Services/CommonServices/CommonServices.API/DependencyInjection/Options/ConfigureSwaggerOptions.cs
src/Services/CommonServices/CommonServices.API/Program.cs
src/Services/CommonServices/CommonServices.API/Services/CurrentUser.cs
src/Services/CommonServices/CommonServices.Application/Common/ICurrentUser.cs
src/Services/CommonServices/CommonServices.Application/CommonServices.Application.csproj
src/Services/CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Login/LoginCommand.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Login/LoginCommandHandler.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Login/LoginCommandValidator.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Logout/LogoutCommand.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Logout/LogoutCommandHandler.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommand.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Register/RegisterCommand.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Register/RegisterCommandHandler.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Register/RegisterCommandValidator.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Common/Dtos/LoginDto.cs
src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Common/Dtos/UserDto.cs
src/Services/CommonServices/CommonServices.Domain/CommonServices.Domain.csproj
src/Services/CommonServices/CommonServices.Domain/Entities/Identity/Action.cs
src/Services/CommonServices/CommonServices.Domain/Entities/Identity/ActionInFunction.cs
src/Services/CommonServices/CommonServices.Domain/Entities/Identity/AppRole.cs
src/Services/CommonServices/CommonServices.Domain/Entities/Identity/AppUser.cs
src/Services/CommonServices/CommonServices.Domain/Entities/Identity/Function.cs
src/Services/CommonServices/CommonServices.Domain/Entities/Identity/Permission.cs
src/Services/CommonServices/CommonServices.Domain/Entities/Identity/UserSession.cs
src/Services/CommonServices/CommonServices.Domain/Exceptions/AuthException.cs
src/Services/CommonServices/CommonServices.Infrastructure/CommonServices.Infrastructure.csproj
src/Services/CommonServices/CommonServices.Persistence/CommonServices.Persistence.csproj
src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/ActionConfiguration.cs
src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/ActionInFunctionConfiguration.cs
src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/AppRoleConfiguration.cs
src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/AppUserConfiguration.cs
src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/FunctionConfiguration.cs
src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/IdentityConfiguration.cs
src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/PermissionConfiguration.cs
src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/UserSessionConfiguration.cs
src/Services/CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ApplicationBuilderExtensions.cs
src/Services/CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
src/Services/CommonServices/CommonServices.Persistence/DependencyInjection/Options/SqlServerRetryOptions.cs
src/Services/CommonServices/CommonServices.Persistence/Services/PermissionService.cs
src/BuildingBlocks/Common.Logging/Common.Logging.csproj
src/BuildingBlocks/Contracts/Contracts.csproj
src/BuildingBlocks/Contracts/Domains/Interfaces/IUserTracking.cs
src/BuildingBlocks/Contracts/Identity/IPermissionService.cs
src/BuildingBlocks/Contracts/Identity/ITokenRevocationService.cs
src/BuildingBlocks/Contracts/Identity/ITokenService.cs
src/BuildingBlocks/Infrastructure/Caching/CachedPermissionService.cs
src/BuildingBlocks/Infrastructure/Extensions/CachingServiceCollectionExtensions.cs
src/BuildingBlocks/Infrastructure/Identity/RedisTokenRevocationService.cs
src/BuildingBlocks/Infrastructure/Identity/TokenService.cs
src/BuildingBlocks/Infrastructure/Infrastructure.csproj
src/BuildingBlocks/Shared/Common/Constants/Authorization/AccessLevel.cs
src/BuildingBlocks/Shared/Common/Constants/Authorization/FunctionCode.cs
src/BuildingBlocks/Shared/DTOs/Identity/TokenRequest.cs
src/BuildingBlocks/Shared/DTOs/Identity/TokenResponse.cs
src/BuildingBlocks/Shared/DTOs/Identity/UserPermission.cs
src/BuildingBlocks/Shared/Options/JwtOptions.cs
src/BuildingBlocks/Shared/Shared.csproj
---
src/Services/AerationSterilize
src/Services/AerationSterilize/AerationSterilize.API
src/Services/AerationSterilize/AerationSterilize.API/Abstractions
src/Services/AerationSterilize/AerationSterilize.API/AerationSterilize.API.csproj
src/Services/AerationSterilize/AerationSterilize.API/Authorization
src/Services/AerationSterilize/AerationSterilize.API/Controllers
src/Services/AerationSterilize/AerationSterilize.API/DependencyInjection
src/Services/AerationSterilize/AerationSterilize.API/Properties
src/Services/AerationSterilize/AerationSterilize.API/Services
src/Services/AerationSterilize/AerationSterilize.Application
src/Services/AerationSterilize/AerationSterilize.Application/AerationSterilize.Application.csproj
src/Services/AerationSterilize/AerationSterilize.Application/Behaviors
src/Services/AerationSterilize/AerationSterilize.Application/Common
src/Services/AerationSterilize/AerationSterilize.Application/DependencyInjection
src/Services/AerationSterilize/AerationSterilize.Application/Exceptions
src/Services/AerationSterilize/AerationSterilize.Application/Features
src/Services/AerationSterilize/AerationSterilize.Domain
src/Services/AerationSterilize/AerationSterilize.Domain/AerationSterilize.Domain.csproj
src/Services/AerationSterilize/AerationSterilize.Domain/Entities
src/Services/AerationSterilize/AerationSterilize.Domain/Exceptions
src/Services/AerationSterilize/AerationSterilize.Infrastructure
src/Services/AerationSterilize/AerationSterilize.Infrastructure/AerationSterilize.Infrastructure.csproj
src/Services/AerationSterilize/AerationSterilize.Persistence
src/Services/AerationSterilize/AerationSterilize.Persistence/AerationSterilize.Persistence.csproj
src/Services/AerationSterilize/AerationSterilize.Persistence/Configurations
src/Services/AerationSterilize/AerationSterilize.Persistence/Constants
src/Services/AerationSterilize/AerationSterilize.Persistence/DependencyInjection
src/Services/AerationSterilize/AerationSterilize.Persistence/Migrations
src/Services/AerationSterilize/AerationSterilize.Persistence/Repositories
src/Services/AerationSterilize/AerationSterilize.Persistence/Services
MyApp.sln
Bash Read CommonServices API auth files
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services && for f in CommonServices/CommonServices.API/Program.cs CommonServices/CommonServices.API/CommonServices.API.csproj CommonServices/CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs CommonServices/CommonServices.API/Authorization/*.cs CommonServices/CommonServices.API/Services/CurrentUser.cs CommonServices/CommonServices.API/Controllers/V1/AuthController.cs CommonServices/CommonServices.API/Controllers/V1/BanksController.cs CommonServices/CommonServices.API/appsettings.json CommonServices/CommonServices.Persistence/Services/PermissionService.cs CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs CommonServices/CommonServices.Persistence/*.csproj; do echo "===== $f"; cat "$f"; done

OUT
===== CommonServices/CommonServices.API/Program.cs
using CommonServices.API.DependencyInjection.Extensions;
using CommonServices.Application.DependencyInjection.Extensions;
using Infrastructure.Extensions;
using CommonServices.Persistence.DependencyInjection.Extensions;
using Common.Logging;
using FluentValidation.AspNetCore;
using Infrastructure.Middlewares;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(Serilogger.Configure);
Log.Information(messageTemplate: $"Starting {builder.Environment.EnvironmentName}...");


try
{
    // Add services to the container.

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddTransient<ExceptionHandlingMiddleware>();

    // Add Configuration
    builder.Services.AddConfigureMediaR();
    builder.Services.ConfigureSqlServerRetryOptions(builder.Configuration);
    builder.Services.AddSqlConfiguration(builder.Configuration);
    builder.Services.AddRepositoryBaseConfiguration();
    builder.Services.AddConfigurationOptions(builder.Configuration);
    builder.Services.ConfigureCors(builder.Configuration);
    builder.Services.AddAuthorizationConfigurationAPI();
    builder.Services.AddRedisCache(builder.Configuration); // after AddAuthorizationConfigurationAPI: decorates IPermissionService

    builder.Services.AddFluentValidationAutoValidation();

    builder.Services
       .AddSwaggerGenNewtonsoftSupport()
       .AddFluentValidationRulesToSwagger()
       .AddEndpointsApiExplorer()
       .AddSwagger();

    builder.Services.AddApiVersioningConfiguration();

    var app = builder.Build();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    await app.SeedDatabaseAsync();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment() || builder.Environment.IsStaging())
    {
        app.ConfigureSwagger();
    }
    app.UseCors("CorsPolicy");

    //app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();

}
catch (Exception ex) when (ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shutdown complete");
    Log.CloseAndFlush();
}

===== CommonServices/CommonServices.API/CommonServices.API.csproj
﻿<Project Sdk="Microsoft.NET.Sdk.Web">

	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<Nullable>enable</Nullable>
		<ImplicitUsings>enable</ImplicitUsings>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Swashbuckle.AspNetCore" />
		<PackageReference Include="Swashbuckle.AspNetCore.Newtonsoft" />
		<PackageReference Include="Asp.Versioning.Mvc.ApiExplorer" />
		<PackageReference Include="FluentValidation.AspNetCore" />
		<PackageReference Include="MicroElements.Swashbuckle.FluentValidation" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
	</ItemGroup>

	<ItemGroup>
		<Folder Include="Controllers\V2\" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\..\BuildingBlocks\Common.Logging\Common.Logging.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Contracts\Contracts.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Infrastructure\Infrastructure.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Shared\Shared.csproj" />
		<ProjectReference Include="..\CommonServices.Application\CommonServices.Application.csproj" />
		<ProjectReference Include="..\CommonServices.Domain\CommonServices.Domain.csproj" />
		<ProjectReference Include="..\CommonServices.Infrastructure\CommonServices.Infrastructure.csproj" />
		<ProjectReference Include="..\CommonServices.Persistence\CommonServices.Persistence.csproj" />
	</ItemGroup>

</Project>
===== CommonServices/CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
﻿using CommonServices.API.Authorization;
using CommonServices.API.Services;
using CommonServices.Application.Common;
using CommonServices.Application.DependencyInjection.Extensions;
using CommonServices.Persistence.Services;
using Contracts.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace CommonServices.API.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddApiVersioningConfiguration(this IServiceCollection services)
       => services.AddApiVersioning(options => options.ReportApiVersions = true)
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

    public static void ConfigureCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration["AllowOrigins"];

        services.AddTransient<ITokenService, TokenService>();
        services.AddConfigureJWTAuthentication(configuration);
        services.AddCors(option =>
        {
            option.AddPolicy("CorsPolicy", buider =>
            {
                buider.WithOrigins(origins: origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            });
        });
    }

    public static void AddAuthorizationConfigurationAPI(this IServiceCollection services)
    {
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
    }

}

===== CommonServices/CommonServices.API/Authorization/PermissionAttribute.cs
﻿using Microsoft.AspNetCore.Authorization;

namespace CommonServices.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public PermissionAttribute(string functionCode, string commandCode)
    {
        FunctionCode = functionCode;
        CommandCode = commandCode;
    }
    public string FunctionCode { get; }
    public string CommandCode { get; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(FunctionCode, CommandCode);
    }
}
===== CommonServices/CommonServices.API/Authorization/PermissionAuthorizationHandler.cs
﻿using Microsoft.AspNetCore.Authorization;
using Shared.Common.Constants.Authorization;

namespace CommonServices.API.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(PermissionClaim.Type, requirement.Value))
            context.Succeed(requirement);

        // Không Succeed => ASP.NET tự trả 403 (đã login) hoặc 401 (chưa login)
        return Task.CompletedTask;
    }
}
===== CommonServices/CommonServices.API/Authorization/PermissionRequirement.cs
﻿using Microsoft.AspNetCore.Authorization;
using Shared.Common.Constants.Authorization;

namespace CommonServices.API.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string functionCode, string commandCode)
    {
        FunctionCode = functionCode;
        CommandCode = commandCode;
    }
    public string FunctionCode { get; }
    public string CommandCode { get; }
    public string Value => PermissionClaim.Build(FunctionCode, CommandCode);
}
===== CommonServices/CommonServices.API/Services/CurrentUser.cs
﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CommonServices.Application.Common;
using Shared.Common.Constants.Authorization;

namespace CommonServices.API.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _user;

    public CurrentUser(IHttpContextAccessor accessor)
        => _user = accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public Guid UserId => Guid.Parse(_user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public Guid? DepartmentId =>
        Guid.TryParse(_user.FindFirstValue("department_id"), out var id) ? id : null;

    public string? TokenId => _user.FindFirstValue(JwtRegisteredClaimNames.Jti);

    public DateTimeOffset? TokenExpiresAt =>
        long.TryParse(_user.FindFirstValue(JwtRegisteredClaimNames.Exp), out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp)
            : null;

    public AccessLevel GetAccessLevel(string functionCode, string commandCode)
    {
        var prefix = PermissionClaim.Build(functionCode, commandCode) + ":";
        var claim = _user.FindAll(PermissionClaim.LevelType)
                         .FirstOrDefault(c => c.Value.StartsWith(prefix));

        return claim is not null && int.TryParse(claim.Value[prefix.Length..], out var level)
            ? (AccessLevel)level
            : AccessLevel.None;
    }
}
===== CommonServices/CommonServices.API/Controllers/V1/AuthController.cs
﻿using System.Threading;
using CommonServices.API.Abstractions;
using CommonServices.Application.Features.V1.Auth.Commands.Login;
using CommonServices.Application.Features.V1.Auth.Commands.Logout;
using CommonServices.Application.Features.V1.Auth.Commands.RefreshToken;
using CommonServices.Application.Features.V1.Auth.Commands.Register;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommonServices.API.Controllers.V1;

[ApiVersion(1)]
public class AuthController : ApiController
{
    public AuthController(ISender sender) : base(sender)
    {
    }

    [HttpPost]
    [Route("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Login([FromForm] LoginCommand command)
    {
        var result = await Sender.Send(command);
        if (result.IsFailure)
            return HandleFailure(result);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(30)
        };

        Response.Cookies.Append("refreshToken", result.Value.RefreshToken, cookieOptions);

        return Ok(new
        {
            accessToken = result.Value.AccessToken,
            expiresIn = result.Value.ExpiresIn
        });
    }

    [HttpPost]
    [Route("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost]
    [Route("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (refreshToken == null)
            return Unauthorized();

        var command = new RefreshTokenCommand(refreshToken);
        var result = await Sender.Send(command, cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost]
    [Route("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (refreshToken == null)
            return Unauthorized();

        var command = new LogoutCommand(refreshToken);
        var result = await Sender.Send(command, cancellationToken);

        Response.Cookies.Delete("refreshToken");

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet]
    [Route("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me()
    {
        return Ok();
    }
}
===== CommonServices/CommonServices.API/Controllers/V1/BanksController.cs
using Asp.Versioning;
using CommonServices.API.Abstractions;
using CommonServices.API.Authorization;
using CommonServices.Application.Features.V1.Banks.Commands.CreateBank;
using CommonServices.Application.Features.V1.Banks.Commands.DeleteBank;
using CommonServices.Application.Features.V1.Banks.Commands.SetBankStatus;
using CommonServices.Application.Features.V1.Banks.Commands.UpdateBank;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Application.Features.V1.Banks.Queries.GetBankById;
using CommonServices.Application.Features.V1.Banks.Queries.GetBankList;
using CommonServices.Application.Features.V1.Banks.Queries.GetBanks;
using CommonServices.Domain.Enums;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.API.Controllers.V1;

[ApiVersion(1)]
public class BanksController : ApiController
{
    public BanksController(ISender sender) : base(sender)
    {
    }

    [HttpGet(Name = "GetBanks")]
    [ProducesResponseType(type: typeof(Result<PagedResult<BankDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.BANK, CommandCode.VIEW)]
    public async Task<IActionResult> GetBanks(string? searchTerm = null,
        ObjectStatus? status = null,
        string? sortColumn = null,
        string? sortOrder = null,
        int pageIndex = PagedResult<BankDto>.DefaultPageIndex,
        int pageSize = PagedResult<BankDto>.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetBanksQuery(searchTerm, status, RequestLanguage, sortColumn,
            SortOrderExtension.ConvertStringToSortOrder(sortOrder),
            pageIndex,
            pageSize), cancellationToken);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("list")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<BankDto>>), statusCode: StatusCodes.Status200OK)]
    [Permission(FunctionCode.BANK, CommandCode.VIEW)]
    public async Task<IActionResult> GetBankList(ObjectStatus? status = null, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetBankListQuery(RequestLanguage, status), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("{bankId:guid}")]
    [ProducesResponseType(type: typeof(Result<BankDetailDto>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.BANK, CommandCode.VIEW)]
    public async Task<IActionResult> GetBankById(Guid bankId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetBankByIdQuery(bankId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost(Name = "CreateBank")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Permission(FunctionCode.BANK, CommandCode.CREATE)]
    public async Task<IActionResult> CreateBank([FromBody] CreateBankCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{bankId:guid}")]
    [ProducesResponseType(typeof(Result<BankDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.BANK, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateBank(Guid bankId, [FromBody] UpdateBankCommand command, CancellationToken cancellationToken)
    {
        command.SetId(bankId);
        var result = await Sender.Send(command, cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{bankId:guid}/activate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.BANK, CommandCode.ACTIVATE)]
    public async Task<IActionResult> ActivateBank(Guid bankId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetBankStatusCommand(bankId, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{bankId:guid}/deactivate")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.BANK, CommandCode.INACTIVATE)]
    public async Task<IActionResult> DeactivateBank(Guid bankId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetBankStatusCommand(bankId, ObjectStatus.Inactive), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpDelete("{bankId:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.BANK, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteBank(Guid bankId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteBankCommand(bankId), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
===== CommonServices/CommonServices.API/appsettings.json
{
    "Serilog": {
        "MinimumLevel": {
            "Default": "Debug",
            "Override": {
                "Microsoft": "Warning",
                "System": "Warning",
                "Microsoft.Hosting.Lifetime": "Information",
                "Microsoft.AspNetCore.Authentication": "Debug"
            }
        }
    },
    "SqlServerRetryOptions": {
        "MaxRetryCount": 5,
        "MaxRetryDelay": "00:00:05",
        "ErrorNumbersToAdd": []
    },
  "DatabaseOptions": {
    "ConnectionString": "Server=QUANG;Database=CommonServices;User Id=sa;Password=123456;Persist Security Info=True;MultipleActiveResultSets=True;TrustServerCertificate=True;"
  },
    "JwtOptions": {
        "SecretKey": "CommonServices_SuperSecretKey_PhaiDaiHon_32KyTu_Nhe_123456!",
        "Issuer": "CommonServices",
        "Audience": "CommonServices",
        "ExpiryMinutes": 60
    },
    "RedisOptions": {
        "ConnectionString": "localhost:6379",
        "InstanceName": "common:",
        "DefaultExpirationMinutes": 10
    },
    "AllowOrigins": "*",
    "AllowedHosts": "*"
}
===== CommonServices/CommonServices.Persistence/Services/PermissionService.cs
﻿using Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Constants.Authorization;
using Shared.DTOs.Identity;

namespace CommonServices.Persistence.Services;
public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;

    public PermissionService(ApplicationDbContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<IReadOnlyList<UserPermission>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // AspNetUserRoles (UserId, RoleId)  JOIN  Permissions (RoleId, FunctionId, ActionId)
        var rows = await (
            from ur in _context.UserRoles
            join p in _context.Permissions on ur.RoleId equals p.RoleId
            join f in _context.Functions on p.FunctionId equals f.Id
            join a in _context.Actions on p.ActionId equals a.Id
            where ur.UserId == userId && f.IsActive == true && a.IsActive == true
            select new { p.FunctionId, p.ActionId, p.AccessLevel })
        .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.FunctionId, x.ActionId })
            .Select(g => new UserPermission(
                PermissionClaim.Build(g.Key.FunctionId, g.Key.ActionId),
                g.Max(x => x.AccessLevel)))
            .ToList();
    }
}

===== CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
﻿using CommonServices.Domain.Entities.Identity;
using CommonServices.Persistence.DependencyInjection.Options;
using CommonServices.Persistence.Repositories;
using Contracts.Common.Repositories;
using Infrastructure.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Options;

namespace CommonServices.Persistence.DependencyInjection.Extensions;
public static class ServiceCollectionExtensions
{
    public static void AddRepositoryBaseConfiguration(this IServiceCollection services)
    => services
        .AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork<ApplicationDbContext>))
        .AddScoped(typeof(IRepositoryBase<,>), typeof(ApplicationRepository<,>))
        ;

    public static void AddSqlConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseOptions = configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>();
        if (databaseOptions == null || string.IsNullOrEmpty(databaseOptions.ConnectionString))
            throw new ArgumentNullException("Connection string is not configured.");

        services.AddDbContextPool<DbContext, ApplicationDbContext>((provider, builder) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var options = provider.GetRequiredService<IOptionsMonitor<SqlServerRetryOptions>>();

            builder
         .EnableDetailedErrors(true)
         .EnableSensitiveDataLogging(true)
         //   .UseLazyLoadingProxies(true) // => If UseLazyLoadingProxies, all of the navigation fields should be VIRTUAL
         .UseSqlServer(
                 connectionString: databaseOptions.ConnectionString,
                 sqlServerOptionsAction: optionsBuilder
                     => optionsBuilder
                     .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));


        });

        services.AddIdentity<AppUser, AppRole>(opt =>
        {
            opt.Lockout.AllowedForNewUsers = true;
            opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(2);
            opt.Lockout.MaxFailedAccessAttempts = 3;

            opt.Password.RequireDigit = false;
            opt.Password.RequireLowercase = false;
            opt.Password.RequireNonAlphanumeric = false;
            opt.Password.RequireUppercase = false;
            opt.Password.RequiredLength = 6;
            opt.Password.RequiredUniqueChars = 1;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();
    }

    public static OptionsBuilder<SqlServerRetryOptions> ConfigureSqlServerRetryOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlServerRetryOptions = configuration.GetSection(nameof(SqlServerRetryOptions));
        var result = services
              .AddOptions<SqlServerRetryOptions>()
              .Bind(sqlServerRetryOptions)
              .ValidateDataAnnotations()
              .ValidateOnStart();

        return result;
    }
}
===== CommonServices/CommonServices.Persistence/CommonServices.Persistence.csproj
﻿<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Microsoft.AspNetCore.Identity" />
		<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
		<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\..\BuildingBlocks\Common.Logging\Common.Logging.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Contracts\Contracts.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Infrastructure\Infrastructure.csproj" />
		<ProjectReference Include="..\CommonServices.Domain\CommonServices.Domain.csproj" />
	</ItemGroup>

</Project>
Bash Read BuildingBlocks identity code
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/BuildingBlocks && for f in Infrastructure/Identity/*.cs Infrastructure/Caching/CachedPermissionService.cs Infrastructure/Extensions/CachingServiceCollectionExtensions.cs Contracts/Identity/*.cs Shared/Common/Constants/Authorization/*.cs Shared/DTOs/Identity/*.cs Shared/Options/JwtOptions.cs; do echo "===== $f"; cat "$f"; done; grep -rn "AddConfigureJWTAuthentication" --include=*.cs . | head

OUT
===== Infrastructure/Identity/RedisTokenRevocationService.cs
using Contracts.Caching;
using Contracts.Identity;

namespace Infrastructure.Identity;

// Deny-list of access token ids (jti). Each entry lives only until the token would have expired anyway.
internal sealed class RedisTokenRevocationService : ITokenRevocationService
{
    private readonly ICacheService _cacheService;

    public RedisTokenRevocationService(ICacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return;

        await _cacheService.SetAsync(CacheKeys.RevokedToken(jti), true, remaining, cancellationToken);
    }

    public Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
        => _cacheService.ExistsAsync(CacheKeys.RevokedToken(jti), cancellationToken);
}
===== Infrastructure/Identity/TokenService.cs
﻿using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.Common.Constants.Authorization;
using Shared.DTOs.Identity;
using Shared.Options;

namespace Infrastructure.Identity;
public class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly UserManager<AppUser> _userManager;
    //private readonly RoleManager<AppRole> _roleManager;

    public TokenService(JwtOptions jwtOptions)
    {
        _jwtOptions = jwtOptions ?? throw new ArgumentNullException(nameof(jwtOptions));
    }
    public TokenResponse GetToken(TokenRequest request)
    {
        var accessToken = GenerateAccessToken(request);
        var freshToken = GenerateRefreshToken();
        var ExpiresIn = _jwtOptions.ExpiryMinutes * 60; // Convert minutes to seconds for the response

        var result = new TokenResponse(accessToken, freshToken, ExpiresIn);

        return result;
    }

    private string GenerateAccessToken(TokenRequest request)
        => GenerateEncryptedToken(request);

    private string GenerateEncryptedToken(TokenRequest request)
    {
        var credentials = GetSigningCredentials();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, request.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(request.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        foreach (var p in request.Permissions)
        {
            claims.Add(new Claim(PermissionClaim.Type, p.Code));                       // "ORDER.VIEW"
            claims.Add(new Claim(PermissionClaim.LevelType, $"{p.Code}:{(int)p.Level}")); // "ORDER.VIEW:1"
        }

        var accessTokenExpiry = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        var token = new JwtSecurityToken(
           issuer: _jwtOptions.Issuer,
           audience: _jwtOptions.Audience,
           claims: claims,
           expires: accessTokenExpiry,
           signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];

        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);

        return Convert.ToBase64String(randomNumber);
    }

    private SigningCredentials GetSigningCredentials()
    {
        byte[] secret = Encoding.UTF8.GetBytes(_jwtOptions.SecretKey);
        return new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256);
    }

}
===== Infrastructure/Caching/CachedPermissionService.cs
using Contracts.Caching;
using Contracts.Identity;
using Shared.DTOs.Identity;

namespace Infrastructure.Caching;

// Decorates the database-backed IPermissionService. The entry is removed on logout.
internal sealed class CachedPermissionService : IPermissionService
{
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(30);

    private readonly IPermissionService _inner;
    private readonly ICacheService _cacheService;

    public CachedPermissionService(IPermissionService inner, ICacheService cacheService)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task<IReadOnlyList<UserPermission>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var key = CacheKeys.Permissions(userId);

        var cached = await _cacheService.GetAsync<List<UserPermission>>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var permissions = await _inner.GetPermissionsAsync(userId, cancellationToken);
        await _cacheService.SetAsync(key, permissions, Expiration, cancellationToken);

        return permissions;
    }
}
===== Infrastructure/Extensions/CachingServiceCollectionExtensions.cs
using Contracts.Caching;
using Contracts.Identity;
using Infrastructure.Caching;
using Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Options;
using StackExchange.Redis;

namespace Infrastructure.Extensions;
public static class CachingServiceCollectionExtensions
{
    // Registers the Redis cache shared by all services.
    // Must be called after IPermissionService has been registered, because it decorates that registration.
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(nameof(RedisOptions)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var redisOptions = provider.GetRequiredService<IOptions<RedisOptions>>().Value;
            var configurationOptions = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            configurationOptions.AbortOnConnectFail = false; // keep retrying in the background instead of crashing on startup

            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<ITokenRevocationService, RedisTokenRevocationService>();
        services.Decorate<IPermissionService, CachedPermissionService>();

        return services;
    }

    private static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException($"{typeof(TService).Name} must be registered before it can be decorated.");

        services.Remove(descriptor);
        services.Add(ServiceDescriptor.Describe(
            typeof(TService),
            provider => ActivatorUtilities.CreateInstance<TDecorator>(provider, CreateInner(provider, descriptor)),
            descriptor.Lifetime));

        return services;
    }

    private static object CreateInner(IServiceProvider provider, ServiceDescriptor descriptor)
        => descriptor.ImplementationInstance
           ?? descriptor.ImplementationFactory?.Invoke(provider)
           ?? ActivatorUtilities.GetServiceOrCreateInstance(provider, descriptor.ImplementationType!);
}
===== Contracts/Identity/IPermissionService.cs
﻿using Shared.DTOs.Identity;

namespace Contracts.Identity;
public interface IPermissionService
{
    Task<IReadOnlyList<UserPermission>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
===== Contracts/Identity/ITokenRevocationService.cs
namespace Contracts.Identity;
public interface ITokenRevocationService
{
    Task RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default);
}
===== Contracts/Identity/ITokenService.cs
﻿
using Shared.DTOs.Identity;

namespace Contracts.Identity;
public interface ITokenService
{
    TokenResponse GetToken(TokenRequest request);
}

===== Shared/Common/Constants/Authorization/AccessLevel.cs
﻿namespace Shared.Common.Constants.Authorization;
public enum AccessLevel
{
    None = 0,
    Own = 1,
    Department = 2,
    All = 3
}
===== Shared/Common/Constants/Authorization/FunctionCode.cs
﻿namespace Shared.Common.Constants.Authorization;
public static class FunctionCode
{
    public const string DASHBOARD = "DASHBOARD";
    public const string SYSTEM = "SYSTEM";
    public const string USER = "USER";
    public const string ROLE = "ROLE";
    public const string PRODUCT = "PRODUCT";

    // Master data (CommonServices)
    public const string MASTER_DATA = "MASTER_DATA";
    public const string LANGUAGE = "LANGUAGE";
    public const string REGION = "REGION";
    public const string CURRENCY = "CURRENCY";
    public const string COUNTRY = "COUNTRY";
    public const string CITY = "CITY";
    public const string BANK = "BANK";
}

public static class CommandCode
{
    public const string VIEW = "VIEW";
    public const string CREATE = "CREATE";
    public const string UPDATE = "UPDATE";
    public const string DELETE = "DELETE";
    public const string ACTIVATE = "ACTIVATE";
    public const string INACTIVATE = "INACTIVATE";
}

public static class PermissionClaim
{
    public const string Type = "permission";
    public const string LevelType = "permission_level";

    // "PRODUCT" + "VIEW" => "PRODUCT.VIEW"
    public static string Build(string functionId, string actionId) => $"{functionId}.{actionId}";
}
===== Shared/DTOs/Identity/TokenRequest.cs
﻿namespace Shared.DTOs.Identity;
public sealed record TokenRequest(
    Guid UserId,
    string UserName,
    IList<string> Roles,
    IReadOnlyList<UserPermission> Permissions);
===== Shared/DTOs/Identity/TokenResponse.cs
﻿namespace Shared.DTOs.Identity;
/// <summary>
/// represents the response returned after a successful authentication, containing the access token, refresh token, and expiration information.
/// </summary>
/// <param name="AccessToken"></param>
/// <param name="RefreshToken"></param>
/// <param name="ExpiresIn">(ExpiresIn = Access Token)</param>
public record TokenResponse(string AccessToken, string RefreshToken, double ExpiresIn);

===== Shared/DTOs/Identity/UserPermission.cs
﻿using Shared.Common.Constants.Authorization;

namespace Shared.DTOs.Identity;
public sealed record UserPermission(string Code, AccessLevel Level);
===== Shared/Options/JwtOptions.cs
﻿namespace Shared.Options;

public class JwtOptions
{
    public string SecretKey { get; set; }
    public string Issuer { get; set; }
    public string Audience { get; set; }
    public double ExpiryMinutes { get; set; } 
}
Bash Find JWT config and Aeration auth files
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src && grep -rn "AddConfigureJWTAuthentication\|IsRevokedAsync\|OnTokenValidated" --include=*.cs . | grep -v /obj/; cat BuildingBlocks/Infrastructure/Infrastructure.csproj; find Services/AerationSterilize -type f -name "*.cs" | grep -v -E "/(bin|obj|Migrations)/" | grep -iE "auth|permission|user|role|token|login|logout|session|identity|ServiceCollection|Program"

OUT
./BuildingBlocks/Contracts/Identity/ITokenRevocationService.cs:6:    Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default);
./BuildingBlocks/Infrastructure/Identity/RedisTokenRevocationService.cs:25:    public Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
./Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:27:        services.AddConfigureJWTAuthentication(configuration);
./Services/AerationSterilize/AerationSterilize.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:44:    public static IServiceCollection AddConfigureJWTAuthentication(this IServiceCollection services, IConfiguration configuration)
./Services/AerationSterilize/AerationSterilize.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:82:                OnTokenValidated = async context =>
./Services/AerationSterilize/AerationSterilize.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:92:                    if (await tokenRevocationService.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
./Services/CommonServices/CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:27:        services.AddConfigureJWTAuthentication(configuration);
./Services/CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:44:    public static IServiceCollection AddConfigureJWTAuthentication(this IServiceCollection services, IConfiguration configuration)
./Services/CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:82:                OnTokenValidated = async context =>
./Services/CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:92:                    if (await tokenRevocationService.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
﻿<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" />
		<PackageReference Include="Microsoft.EntityFrameworkCore" />
		<PackageReference Include="Microsoft.IdentityModel.Tokens" />
		<PackageReference Include="System.IdentityModel.Tokens.Jwt" />
		<PackageReference Include="EPPlus" />
		<PackageReference Include="StackExchange.Redis" />
		<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
		<PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\Services\AerationSterilize\AerationSterilize.Domain\AerationSterilize.Domain.csproj" />
		<ProjectReference Include="..\Common.Logging\Common.Logging.csproj" />
		<ProjectReference Include="..\Contracts\Contracts.csproj" />
		<ProjectReference Include="..\Shared\Shared.csproj" />
	</ItemGroup>

</Project>
Services/AerationSterilize/AerationSterilize.API/Authorization/PermissionAttribute.cs
Services/AerationSterilize/AerationSterilize.API/Authorization/PermissionAuthorizationHandler.cs
Services/AerationSterilize/AerationSterilize.API/Authorization/PermissionRequirement.cs
Services/AerationSterilize/AerationSterilize.API/Controllers/V1/AuthController.cs
Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
Services/AerationSterilize/AerationSterilize.API/Program.cs
Services/AerationSterilize/AerationSterilize.API/Services/CurrentUser.cs
Services/AerationSterilize/AerationSterilize.Application/Common/ICurrentUser.cs
Services/AerationSterilize/AerationSterilize.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Login/LoginCommand.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Login/LoginCommandHandler.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Login/LoginCommandValidator.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Logout/LogoutCommand.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Logout/LogoutCommandHandler.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommand.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Register/RegisterCommand.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Register/RegisterCommandHandler.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Register/RegisterCommandValidator.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Common/Dtos/LoginDto.cs
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Common/Dtos/UserDto.cs
Services/AerationSterilize/AerationSterilize.Domain/Entities/Identity/Action.cs
Services/AerationSterilize/AerationSterilize.Domain/Entities/Identity/ActionInFunction.cs
Services/AerationSterilize/AerationSterilize.Domain/Entities/Identity/AppRole.cs
Services/AerationSterilize/AerationSterilize.Domain/Entities/Identity/AppUser.cs
Services/AerationSterilize/AerationSterilize.Domain/Entities/Identity/Function.cs
Services/AerationSterilize/AerationSterilize.Domain/Entities/Identity/Permission.cs
Services/AerationSterilize/AerationSterilize.Domain/Entities/Identity/UserSession.cs
Services/AerationSterilize/AerationSterilize.Domain/Exceptions/AuthException.cs
Services/AerationSterilize/AerationSterilize.Persistence/Configurations/Identiy/AppRoleConfiguration.cs
Services/AerationSterilize/AerationSterilize.Persistence/Configurations/Identiy/AppUserConfiguration.cs
Services/AerationSterilize/AerationSterilize.Persistence/Configurations/Identiy/IdentityConfiguration.cs
Services/AerationSterilize/AerationSterilize.Persistence/Configurations/Identiy/PermissionConfiguration.cs
Services/AerationSterilize/AerationSterilize.Persistence/Configurations/Identiy/UserSessionConfiguration.cs
Services/AerationSterilize/AerationSterilize.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
Services/AerationSterilize/AerationSterilize.Persistence/Services/PermissionService.cs
Bash Compare JWT setup and read Aeration auth config
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services && diff AerationSterilize/AerationSterilize.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs; echo =====; cat CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs; echo ====; cat AerationSterilize/AerationSterilize.API/appsettings.json AerationSterilize/AerationSterilize.API/Properties/launchSettings.json CommonServices/CommonServices.API/appsettings.Development.json; cat AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Logout/LogoutCommandHandler.cs AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Login/LoginCommandHandler.cs

OUT
1,100c1,100
< ﻿using System.Text;
< using AerationSterilize.Application.Behaviors;
< using Infrastructure.Behaviors;
< using Contracts.Identity;
< using FluentValidation;
< using MediatR;
< using Microsoft.AspNetCore.Authentication.JwtBearer;
< using Microsoft.Extensions.Configuration;
< using Microsoft.Extensions.DependencyInjection;
< using Microsoft.IdentityModel.Tokens;
< using Shared.Options;
< 
< namespace AerationSterilize.Application.DependencyInjection.Extensions;
< public static class ServiceCollectionExtensions
< {
<     public static void AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
<     {
<         var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
<         services.AddSingleton(jwtOptions);
<     }
<     public static IServiceCollection AddConfigureMediaR(this IServiceCollection services)
<     {
<         services
<             .AddConfigureAutoMapper()
<             .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly))
<             .AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true)
<             .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>))
<             .AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>))
<             .AddTransient(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationPipelineBehavior<,>))
<             .AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingPipelineBehavior<,>))
<             .AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionPipelineBehavior<,>))
<             ;
<         return services;
<     }
< 
<     public static IServiceCollection AddConfigureAutoMapper(this IServiceCollection services)
<     {
<         services
<             .AddAutoMapper(_ => { }, AssemblyReference.Assembly)
<             ;
<         return services;
<     }
< 
<     public static IServiceCollection AddConfigureJWTAuthentication(this IServiceCollection services, IConfiguration configuration)
<     {
<         var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
<         if (jwtOptions == null || string.IsNullOrEmpty(jwtOptions.SecretKey))
<         {
<             throw new ArgumentNullException($"{nameof(JwtOptions)} is not configured properly");
<         }
< 
<         var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));
< 
<         var tokenValidationParameters = new TokenValidationParameters
<         {
<             ValidateIssuerSigningKey = true,
<             IssuerSigningKey = signingKey,
< 
<             ValidateIssuer = true,
<             ValidIssuer = jwtOptions.Issuer,
< 
<             ValidateAudience = true,
<             ValidAudience = jwtOptions.Audience,
< 
<             ValidateLifetime = true,
<             RequireExpirationTime = true,
< 
<             ClockSkew = TimeSpan.Zero
<         };
<         services.AddAuthentication(o =>
<         {
<             o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
<             o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
<         }).AddJwtBearer(x =>
<         {
<             x.SaveToken = true;
<             x.RequireHttpsMetadata = false;
<             x.TokenValidationParameters = tokenValidationParameters;
<             x.Events = new JwtBearerEvents
<             {
<                 // Reject access tokens that were revoked on logout, even though their signature and lifetime are still valid.
<                 OnTokenValidated = async context =>
<                 {
<                     var jti = context.SecurityToken?.Id;
<                     if (string.IsNullOrEmpty(jti))
<                     {
<                         context.Fail("Token has no jti.");
<                         return;
<                     }
< 
<                     var tokenRevocationService = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationService>();
<                     if (await tokenRevocationService.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
<                         context.Fail("Token has been revoked.");
<                 }
<             };
<         });
< 
<         return services;
<     }
< }
---
> ﻿using System.Text;
> using CommonServices.Application.Behaviors;
> using Infrastructure.Behaviors;
> using Contracts.Identity;
> using FluentValidation;
> using MediatR;
> using Microsoft.AspNetCore.Authentication.JwtBearer;
> using Microsoft.Extensions.Configuration;
> using Microsoft.Extensions.DependencyInjection;
> using Microsoft.IdentityModel.Tokens;
> using Shared.Options;
> 
> namespace CommonServices.Application.DependencyInjection.Extensions;
> public static class ServiceCollectionExtensions
> {
>     public static void AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
>     {
>         var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
>         services.AddSingleton(jwtOptions);
>     }
>     public static IServiceCollection AddConfigureMediaR(this IServiceCollection services)
>     {
>         services
>             .AddConfigureAutoMapper()
>             .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly))
>             .AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true)
>             .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>))
>             .AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>))
>             .AddTransient(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationPipelineBehavior<,>))
>             .AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingPipelineBehavior<,>))
>             .AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionPipelineBehavior<,>))
>             ;
>         return services;
>     }
> 
>     public static IServiceCollection AddConfigureAutoMapper(this IServiceCollection services)
>     {
>         services
>             .AddAutoMapper(_ => { }, AssemblyReference.Assembly)
>             ;
>         return services;
>     }
> 
>     public static IServiceCollection AddConfigureJWTAuthentication(this IServiceCollection services, IConfiguration configuration)
>     {
>         var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
>         if (jwtOptions == null || string.IsNullOrEmpty(jwtOptions.SecretKey))
>         {
>             throw new ArgumentNullException($"{nameof(JwtOptions)} is not configured properly");
>         }
> 
>         var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));
> 
>         var tokenValidationParameters = new TokenValidationParameters
>         {
>             ValidateIssuerSigningKey = true,
>             IssuerSigningKey = signingKey,
> 
>             ValidateIssuer = true,
>             ValidIssuer = jwtOptions.Issuer,
> 
>             ValidateAudience = true,
>             ValidAudience = jwtOptions.Audience,
> 
>             ValidateLifetime = true,
>             RequireExpirationTime = true,
> 
>             ClockSkew = TimeSpan.Zero
>         };
>         services.AddAuthentication(o =>
>         {
>             o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
>             o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
>         }).AddJwtBearer(x =>
>         {
>             x.SaveToken = true;
>             x.RequireHttpsMetadata = false;
>             x.TokenValidationParameters = tokenValidationParameters;
>             x.Events = new JwtBearerEvents
>             {
>                 // Reject access tokens that were revoked on logout, even though their signature and lifetime are still valid.
>                 OnTokenValidated = async context =>
>                 {
>                     var jti = context.SecurityToken?.Id;
>                     if (string.IsNullOrEmpty(jti))
>                     {
>                         context.Fail("Token has no jti.");
>                         return;
>                     }
> 
>                     var tokenRevocationService = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationService>();
>                     if (await tokenRevocationService.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
>                         context.Fail("Token has been revoked.");
>                 }
>             };
>         });
> 
>         return services;
>     }
> }
=====
﻿using System.Text;
using CommonServices.Application.Behaviors;
using Infrastructure.Behaviors;
using Contracts.Identity;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.Options;

namespace CommonServices.Application.DependencyInjection.Extensions;
public static class ServiceCollectionExtensions
{
    public static void AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
        services.AddSingleton(jwtOptions);
    }
    public static IServiceCollection AddConfigureMediaR(this IServiceCollection services)
    {
        services
            .AddConfigureAutoMapper()
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly))
            .AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true)
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationPipelineBehavior<,>))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingPipelineBehavior<,>))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionPipelineBehavior<,>))
            ;
        return services;
    }

    public static IServiceCollection AddConfigureAutoMapper(this IServiceCollection services)
    {
        services
            .AddAutoMapper(_ => { }, AssemblyReference.Assembly)
            ;
        return services;
    }

    public static IServiceCollection AddConfigureJWTAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
        if (jwtOptions == null || string.IsNullOrEmpty(jwtOptions.SecretKey))
        {
            throw new ArgumentNullException($"{nameof(JwtOptions)} is not configured properly");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,

            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateLifetime = true,
            RequireExpirationTime = true,

            ClockSkew = TimeSpan.Zero
        };
        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(x =>
        {
            x.SaveToken = true;
            x.RequireHttpsMetadata = false;
            x.TokenValidationParameters = tokenValidationParameters;
            x.Events = new JwtBearerEvents
            {
                // Reject access tokens that were revoked on logout, even though their signature and lifetime are still valid.
                OnTokenValidated = async context =>
                {
                    var jti = context.SecurityToken?.Id;
                    if (string.IsNullOrEmpty(jti))
                    {
                        context.Fail("Token has no jti.");
                        return;
                    }

                    var tokenRevocationService = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationService>();
                    if (await tokenRevocationService.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
                        context.Fail("Token has been revoked.");
                }
            };
        });

        return services;
    }
}
====
{
    "Serilog": {
        "MinimumLevel": {
            "Default": "Debug",
            "Override": {
                "Microsoft": "Warning",
                "System": "Warning",
                "Microsoft.Hosting.Lifetime": "Information",
                "Microsoft.AspNetCore.Authentication": "Debug"
            }
        }
    },
    "SqlServerRetryOptions": {
        "MaxRetryCount": 5,
        "MaxRetryDelay": "00:00:05",
        "ErrorNumbersToAdd": []
    },
  "DatabaseOptions": {
    "ConnectionString": "Server=QUANG;Database=SourceBase;User Id=sa;Password=123456;Persist Security Info=True;MultipleActiveResultSets=True;TrustServerCertificate=True;"
  },
    "JwtOptions": {
        "SecretKey": "AeartionSterilize_SuperSecretKey_PhaiDaiHon_32KyTu_Nhe_123456!",
        "Issuer": "AerationSterilize",
        "Audience": "AerationSterilize",
        "ExpiryMinutes": 60
    },
    "RedisOptions": {
        "ConnectionString": "localhost:6379",
        "InstanceName": "aeration:",
        "DefaultExpirationMinutes": 10
    },
    "AllowOrigins": "*",
    "AllowedHosts": "*"
}
{
  "profiles": {
    "AerationSterilize.API": {
      "commandName": "Project",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "applicationUrl": "http://localhost:5000"
    }
  }
}{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "RedisOptions": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "common:",
    "DefaultExpirationMinutes": 10
  }
}
using AerationSterilize.Application.Common;
using static AerationSterilize.Domain.Exceptions.AuthException;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Caching;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Identity;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Logout;
public class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly IRepositoryBase<UserSession, int> _userSessionRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly ICacheService _cacheService;

    public LogoutCommandHandler(IRepositoryBase<UserSession, int> userSessionRepository,
        ICurrentUser currentUser,
        ITokenRevocationService tokenRevocationService,
        ICacheService cacheService)
    {
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _tokenRevocationService = tokenRevocationService ?? throw new ArgumentNullException(nameof(tokenRevocationService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenEntity = await _userSessionRepository
            .FindSingleAsync(us => us.RefreshToken == request.RefreshToken, cancellationToken)
         ?? throw new InvalidRefreshTokenException();

        tokenEntity.IsRevoked = true;

        // Revoke the access token used for this request, so it stops working now instead of at expiry.
        if (_currentUser.TokenId is { } jti && _currentUser.TokenExpiresAt is { } expiresAt)
            await _tokenRevocationService.RevokeAsync(jti, expiresAt, cancellationToken);

        await _cacheService.RemoveAsync(CacheKeys.Permissions(tokenEntity.UserId), cancellationToken);

        return Result.Success();
    }
}
﻿using System.Security.Claims;
using static AerationSterilize.Domain.Exceptions.AuthException;
using AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Identity;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Identity;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Login;
internal class LoginCommandHandler : ICommandHandler<LoginCommand, LoginDto>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IPermissionService _permissionService;

    private readonly ITokenService _tokenService;
    private readonly IRepositoryBase<UserSession, int> _userSessionRepository;

    public LoginCommandHandler(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, ITokenService tokenService,
          IRepositoryBase<UserSession, int> userSessionRepository, IPermissionService permissionService)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    public async Task<Result<LoginDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(request.UserName) ?? throw new InvalidCredentialsException();

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!isValidPassword)
            throw new InvalidCredentialsException();


        var results = new Dictionary<string, int>();

        var roleNames = await _userManager.GetRolesAsync(user);
        var permissions = await _permissionService.GetPermissionsAsync(user.Id, cancellationToken);

        //var roles = await _roleManager.Roles.Where(p => roleNames.Contains(p.Name)).ToListAsync();

        //var roleClaims = new List<Claim>();
        //foreach (var role in roles)
        //{
        //    var resultClaims = await _roleManager.GetClaimsAsync(role);
        //    if (resultClaims.Any())
        //    {
        //        roleClaims.AddRange(resultClaims);
        //    }
        //}

        //var roleClaimNames = roleClaims.Select(x => x.Type).Distinct();
        //foreach (var name in roleClaimNames)
        //{
        //    if (!results.ContainsKey(name))
        //    {
        //        var claims = roleClaims.Where(p => p.Type == name);
        //        if (!claims.Any(p => p.Value == "-1"))
        //        {
        //            var value = 0;
        //            foreach (var claim in claims)
        //            {
        //                if (int.TryParse(claim.Value, out int claimValue))
        //                    value |= claimValue;
        //            }
        //            results.Add(name, value);
        //        }
        //    }
        //}


        var tokenRequest = new TokenRequest(user.Id, user.UserName!, roleNames, permissions);
        var token = _tokenService.GetToken(tokenRequest);

        var userSession = new UserSession
        {
            UserId = user.Id,
            RefreshToken = token.RefreshToken,
            ExpiredAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn),
        };
        _userSessionRepository.Add(userSession);

        var result = new LoginDto(
           token.AccessToken,
           token.RefreshToken,
           token.ExpiresIn
        );

        return Result.Success(result);
    }
}
9:05 PM
Mình đang đọc code để xác định phần auth bị lặp ở CommonServices; tiếp theo kiểm tra DbContext, seed, Redis keys.



Bash Inspect CommonServices persistence and identity usages
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src && ls Services/CommonServices/CommonServices.Persistence Services/CommonServices/CommonServices.Persistence/Migrations 2>&1 | head -30; cat Services/CommonServices/CommonServices.Persistence/ApplicationDbContext.cs Services/CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ApplicationBuilderExtensions.cs; grep -rln "AppUser\|AppRole\|UserSession\|Identity\.\|AuthException\|ICurrentUser\|IPermissionService\|ITokenService" --include=*.cs Services/CommonServices | grep -v -E "/(bin|obj)/"

OUT
Services/CommonServices/CommonServices.Persistence:
ApplicationContextSeed.cs
ApplicationDbContext.cs
AssemblyReference.cs
CommonServices.Persistence.csproj
Configurations
Constants
DependencyInjection
Migrations
Repositories
Services
bin
obj

Services/CommonServices/CommonServices.Persistence/Migrations:
20261014145811_Initial.Designer.cs
20261014145811_Initial.cs
ApplicationDbContextModelSnapshot.cs
using CommonServices.Domain.Entities.Identity;
using CommonServices.Domain.Entities.MasterData;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Persistence;

public class ApplicationDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
        => builder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

    public DbSet<AppUser> AppUses { get; set; }
    public DbSet<Domain.Entities.Identity.Action> Actions { get; set; }
    public DbSet<Function> Functions { get; set; }
    public DbSet<ActionInFunction> ActionInFunctions { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }

    public DbSet<Language> Languages { get; set; }
    public DbSet<Region> Regions { get; set; }
    public DbSet<Currency> Currencies { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<Bank> Banks { get; set; }
}
﻿using CommonServices.Domain.Entities.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CommonServices.Persistence.DependencyInjection.Extensions;
public static class ApplicationBuilderExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
        var logger = services.GetRequiredService<Serilog.ILogger>();
        var seeder = new ApplicationContextSeed(context, userManager, roleManager, logger);

        await seeder.InitializeAsync();
        await seeder.SeedAsync();
    }
}
Services/CommonServices/CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
Services/CommonServices/CommonServices.API/Program.cs
Services/CommonServices/CommonServices.API/Services/CurrentUser.cs
Services/CommonServices/CommonServices.Application/Common/ICurrentUser.cs
Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Login/LoginCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Logout/LogoutCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Register/RegisterCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Banks/Commands/CreateBank/CreateBankCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Banks/Commands/DeleteBank/DeleteBankCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Banks/Commands/SetBankStatus/SetBankStatusCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Banks/Commands/UpdateBank/UpdateBankCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Cities/Commands/CreateCity/CreateCityCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Cities/Commands/DeleteCity/DeleteCityCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Cities/Commands/SetCityStatus/SetCityStatusCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Cities/Commands/UpdateCity/UpdateCityCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Countries/Commands/CreateCountry/CreateCountryCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Countries/Commands/DeleteCountry/DeleteCountryCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Countries/Commands/SetCountryStatus/SetCountryStatusCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Countries/Commands/UpdateCountry/UpdateCountryCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Currencies/Commands/CreateCurrency/CreateCurrencyCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Currencies/Commands/DeleteCurrency/DeleteCurrencyCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Currencies/Commands/SetCurrencyStatus/SetCurrencyStatusCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Currencies/Commands/UpdateCurrency/UpdateCurrencyCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Languages/Commands/CreateLanguage/CreateLanguageCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Languages/Commands/DeleteLanguage/DeleteLanguageCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Languages/Commands/SetLanguageStatus/SetLanguageStatusCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Languages/Commands/UpdateLanguage/UpdateLanguageCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Regions/Commands/CreateRegion/CreateRegionCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Regions/Commands/DeleteRegion/DeleteRegionCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Regions/Commands/SetRegionStatus/SetRegionStatusCommandHandler.cs
Services/CommonServices/CommonServices.Application/Features/V1/Regions/Commands/UpdateRegion/UpdateRegionCommandHandler.cs
Services/CommonServices/CommonServices.Domain/Entities/Identity/AppRole.cs
Services/CommonServices/CommonServices.Domain/Entities/Identity/AppUser.cs
Services/CommonServices/CommonServices.Domain/Entities/Identity/UserSession.cs
Services/CommonServices/CommonServices.Domain/Exceptions/AuthException.cs
Services/CommonServices/CommonServices.Persistence/ApplicationContextSeed.cs
Services/CommonServices/CommonServices.Persistence/ApplicationDbContext.cs
Services/CommonServices/CommonServices.Persistence/Configurations/Identity/ActionConfiguration.cs
Services/CommonServices/CommonServices.Persistence/Configurations/Identity/AppRoleConfiguration.cs
Services/CommonServices/CommonServices.Persistence/Configurations/Identity/AppUserConfiguration.cs
Services/CommonServices/CommonServices.Persistence/Configurations/Identity/IdentityConfiguration.cs
Services/CommonServices/CommonServices.Persistence/Configurations/Identity/UserSessionConfiguration.cs
Services/CommonServices/CommonServices.Persistence/Constants/TableNames.cs
Services/CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ApplicationBuilderExtensions.cs
Services/CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
Services/CommonServices/CommonServices.Persistence/Migrations/20261014145811_Initial.cs
Services/CommonServices/CommonServices.Persistence/Migrations/20261014145811_Initial.Designer.cs
Services/CommonServices/CommonServices.Persistence/Migrations/ApplicationDbContextModelSnapshot.cs
Services/CommonServices/CommonServices.Persistence/Services/PermissionService.cs
Bash Read seed, ICurrentUser, cache keys, package props
IN
cat Services/CommonServices/CommonServices.Persistence/ApplicationContextSeed.cs Services/CommonServices/CommonServices.Application/Common/ICurrentUser.cs Services/CommonServices/CommonServices.Persistence/Constants/TableNames.cs; grep -n "ICurrentUser\|_currentUser" Services/CommonServices/CommonServices.Application/Features/V1/Banks/Commands/CreateBank/CreateBankCommandHandler.cs; find BuildingBlocks -name "RedisCacheService.cs" -o -name "CacheKeys.cs" | grep -v obj | xargs cat; cat ../Directory.Packages.props 2>/dev/null | head -80; ls ..

OUT
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
﻿using Shared.Common.Constants.Authorization;

namespace CommonServices.Application.Common;
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid? DepartmentId { get; }
    string? TokenId { get; }
    DateTimeOffset? TokenExpiresAt { get; }
    AccessLevel GetAccessLevel(string functionCode, string commandCode);
}
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
13:    private readonly ICurrentUser _currentUser;
15:    public CreateBankCommandHandler(IRepositoryBase<Bank, Guid> bankRepository, ICurrentUser currentUser)
18:        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
26:        var bankEntity = Bank.Create(request.Code, request.Name, request.VAPrefix, request.IsVAByBank, _currentUser.UserId.ToString());
namespace Contracts.Caching;

// Keys shared by every service. Service-specific keys live in the service itself.
public static class CacheKeys
{
    public static string Permissions(Guid userId) => $"perm:{userId}";

    public static string RevokedToken(string jti) => $"revoked:jti:{jti}";
}
using System.Text.Json;
using Contracts.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Options;
using StackExchange.Redis;

namespace Infrastructure.Caching;

// Redis failures are logged and treated as a cache miss so the API keeps working without Redis.
internal sealed class RedisCacheService : ICacheService
{
    private const int DeleteBatchSize = 500;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connection;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connection, IOptions<RedisOptions> options, ILogger<RedisCacheService> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private IDatabase Database => _connection.GetDatabase();

    // While disconnected, commands would wait for the full timeout (5s by default) on every request, so skip Redis instead.
    private bool IsAvailable => _connection.IsConnected;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return default;

        try
        {
            var value = await Database.StringGetAsync(BuildKey(key));
            return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>((string)value!, SerializerOptions);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis GET failed for key {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("Redis is unavailable, skipped SET for key {CacheKey}", key);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(value, SerializerOptions);
            await Database.StringSetAsync(BuildKey(key), json, expiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes));
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis SET failed for key {CacheKey}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return false;

        try
        {
            return await Database.KeyExistsAsync(BuildKey(key));
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis EXISTS failed for key {CacheKey}", key);
            return false;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("Redis is unavailable, skipped DEL for key {CacheKey}", key);
            return;
        }

        try
        {
            await Database.KeyDeleteAsync(BuildKey(key));
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis DEL failed for key {CacheKey}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("Redis is unavailable, skipped remove by prefix {CachePrefix}", prefix);
            return;
        }

        try
        {
            var database = Database;
            var pattern = BuildKey(prefix) + "*";

            foreach (var endpoint in _connection.GetEndPoints())
            {
                var server = _connection.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica)
                    continue;

                // KeysAsync uses SCAN, so it does not block the server like KEYS does.
                var batch = new List<RedisKey>(DeleteBatchSize);
                await foreach (var key in server.KeysAsync(database.Database, pattern, DeleteBatchSize).WithCancellation(cancellationToken))
                {
                    batch.Add(key);
                    if (batch.Count == DeleteBatchSize)
                    {
                        await database.KeyDeleteAsync(batch.ToArray());
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                    await database.KeyDeleteAsync(batch.ToArray());
            }
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis remove by prefix failed for {CachePrefix}", prefix);
        }
    }

    private string BuildKey(string key) => _options.InstanceName + key;

    private static bool IsRedisFailure(Exception ex) => ex is RedisException or RedisTimeoutException;
}
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- AspNetCore -->
    <PackageVersion Include="FluentValidation.AspNetCore" Version="11.3.1" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.27" />
    <PackageVersion Include="Microsoft.AspNetCore.Http.Abstractions" Version="2.3.10" />
    <PackageVersion Include="Microsoft.AspNetCore.Http.Features" Version="2.3.9" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity" Version="2.3.10" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageVersion Include="Microsoft.Extensions.Identity.Core" Version="8.0.27" />
    <PackageVersion Include="Microsoft.Extensions.Identity.Stores" Version="8.0.27" />
    <PackageVersion Include="Microsoft.IdentityModel.Tokens" Version="8.19.0" />
    <!-- Logging -->
    <PackageVersion Include="Serilog.AspNetCore" Version="5.0.0" />
    <PackageVersion Include="Serilog.Enrichers.Environment" Version="2.2.0" />
    <PackageVersion Include="Serilog.Formatting.Compact" Version="3.0.0" />
    <!-- Analyzer -->
    <PackageVersion Include="StyleCop.Analyzers" Version="1.1.118" />
    <PackageVersion Include="SonarAnalyzer.CSharp" Version="9.7.0.75501" />
    <!-- OpenAPI -->
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageVersion Include="Swashbuckle.AspNetCore.Newtonsoft" Version="6.5.0" />
    <PackageVersion Include="MicroElements.Swashbuckle.FluentValidation" Version="7.1.6" />
    <!-- Mapping -->
    <PackageVersion Include="AutoMapper" Version="16.1.1" />
    <PackageVersion Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.0.1" />
    <!-- Validation -->
    <PackageVersion Include="FluentValidation" Version="12.1.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.12.0" />
    <!-- MediatR -->
    <PackageVersion Include="MediatR" Version="12.5.0" />
    <PackageVersion Include="MediatR.Extensions.Microsoft.DependencyInjection" Version="12.1.1" />
    <!-- Enum -->
    <PackageVersion Include="Ardalis.SmartEnum" Version="8.2.0" />
    <!-- EF Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.27" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.27" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Proxies" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.2" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Options.DataAnnotations" Version="8.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
    <!-- Messaging -->
    <PackageVersion Include="MassTransit" Version="8.3.2" />
    <PackageVersion Include="MassTransit.RabbitMQ" Version="8.3.2" />
    <!-- Testing -->
    <PackageVersion Include="coverlet.collector" Version="6.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageVersion Include="System.IdentityModel.Tokens.Jwt" Version="8.18.0" />
    <PackageVersion Include="xunit" Version="2.5.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.5.3" />
    <!-- SmartEnum -->
    <PackageVersion Include="Ardalis.SmartEnum" Version="7.0.0" />
    <!-- Excel File -->
    <PackageVersion Include="EPPlus" Version="8.0.8" />
    <!-- Background Job -->
    <PackageVersion Include="Hangfire" Version="1.8.21" />
    <PackageVersion Include="Hangfire.SqlServer" Version="1.8.21" />
    <!-- Caching -->
    <PackageVersion Include="StackExchange.Redis" Version="2.8.16" />
    <!-- API Versioning -->
    <PackageVersion Include="Asp.Versioning.Mvc.ApiExplorer" Version="7.1.0" />
  </ItemGroup>
</Project>Directory.Build.props
Directory.Packages.props
MyApp.sln
README.md
docker-compose.yml
global.json
src
tests
Bash Compare auth files and check Aeration seed
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services && for f in Authorization/PermissionAttribute.cs Authorization/PermissionAuthorizationHandler.cs Authorization/PermissionRequirement.cs Services/CurrentUser.cs; do diff -q <(sed 's/AerationSterilize/X/g' AerationSterilize/AerationSterilize.API/$f) <(sed 's/CommonServices/X/g' CommonServices/CommonServices.API/$f) && echo "same $f"; done; cat AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs AerationSterilize/AerationSterilize.API/Program.cs; ls AerationSterilize/AerationSterilize.Persistence AerationSterilize/AerationSterilize.Persistence/Migrations; grep -n "FunctionCode\.\|Function {" AerationSterilize/AerationSterilize.Persistence/*.cs | head -40; dotnet --version; dotnet ef --version 2>&1 | tail -1; cat ../../global.json

OUT
same Authorization/PermissionAttribute.cs
same Authorization/PermissionAuthorizationHandler.cs
same Authorization/PermissionRequirement.cs
same Services/CurrentUser.cs
﻿using AerationSterilize.API.Authorization;
using AerationSterilize.API.Services;
using AerationSterilize.Application.Common;
using AerationSterilize.Application.DependencyInjection.Extensions;
using AerationSterilize.Persistence.Services;
using Contracts.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace AerationSterilize.API.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddApiVersioningConfiguration(this IServiceCollection services)
       => services.AddApiVersioning(options => options.ReportApiVersions = true)
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

    public static void ConfigureCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration["AllowOrigins"];

        services.AddTransient<ITokenService, TokenService>();
        services.AddConfigureJWTAuthentication(configuration);
        services.AddCors(option =>
        {
            option.AddPolicy("CorsPolicy", buider =>
            {
                buider.WithOrigins(origins: origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            });
        });
    }

    public static void AddAuthorizationConfigurationAPI(this IServiceCollection services)
    {
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
    }

}

using AerationSterilize.API.DependencyInjection.Extensions;
using AerationSterilize.Application.DependencyInjection.Extensions;
using Infrastructure.Extensions;
using AerationSterilize.Persistence.DependencyInjection.Extensions;
using Common.Logging;
using FluentValidation.AspNetCore;
using Infrastructure.Middlewares;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(Serilogger.Configure);
Log.Information(messageTemplate: $"Starting {builder.Environment.EnvironmentName}...");


try
{
    // Add services to the container.

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddTransient<ExceptionHandlingMiddleware>();

    // Add Configuration
    builder.Services.AddConfigureMediaR();
    builder.Services.ConfigureSqlServerRetryOptions(builder.Configuration);
    builder.Services.AddSqlConfiguration(builder.Configuration);
    builder.Services.AddRepositoryBaseConfiguration();
    builder.Services.AddConfigurationOptions(builder.Configuration);
    builder.Services.ConfigureCors(builder.Configuration);
    builder.Services.AddAuthorizationConfigurationAPI();
    builder.Services.AddRedisCache(builder.Configuration); // after AddAuthorizationConfigurationAPI: decorates IPermissionService

    builder.Services.AddFluentValidationAutoValidation();

    builder.Services
       .AddSwaggerGenNewtonsoftSupport()
       .AddFluentValidationRulesToSwagger()
       .AddEndpointsApiExplorer()
       .AddSwagger();

    builder.Services.AddApiVersioningConfiguration();

    var app = builder.Build();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    await app.SeedDatabaseAsync();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment() || builder.Environment.IsStaging())
    {
        app.ConfigureSwagger();
    }
    app.UseCors("CorsPolicy");

    //app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();

}
catch (Exception ex) when (ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shutdown complete");
    Log.CloseAndFlush();
}

AerationSterilize/AerationSterilize.Persistence:
AerationSterilize.Persistence.csproj
ApplicationContextSeed.cs
ApplicationDbContext.cs
AssemblyReference.cs
Configurations
Constants
DependencyInjection
Migrations
Repositories
Services
bin
obj

AerationSterilize/AerationSterilize.Persistence/Migrations:
20260605075521_Initial.Designer.cs
20260605075521_Initial.cs
20261014135852_AddDataScopeColumns.Designer.cs
20261014135852_AddDataScopeColumns.cs
ApplicationDbContextModelSnapshot.cs
AerationSterilize/AerationSterilize.Persistence/ApplicationContextSeed.cs:139:            new Function { Id = "DASHBOARD", Name = "Dashboard", Url = "/dashboard", ParrentId = string.Empty, SortOrder = 1, CssClass = "fa-dashboard", IsActive = true },
AerationSterilize/AerationSterilize.Persistence/ApplicationContextSeed.cs:140:            new Function { Id = "SYSTEM",    Name = "System",    Url = "/system",    ParrentId = string.Empty, SortOrder = 2, CssClass = "fa-cogs",      IsActive = true },
AerationSterilize/AerationSterilize.Persistence/ApplicationContextSeed.cs:141:            new Function { Id = "USER",      Name = "User",      Url = "/system/user", ParrentId = "SYSTEM", SortOrder = 1, CssClass = "fa-user",   IsActive = true },
AerationSterilize/AerationSterilize.Persistence/ApplicationContextSeed.cs:142:            new Function { Id = "ROLE",      Name = "Role",      Url = "/system/role", ParrentId = "SYSTEM", SortOrder = 2, CssClass = "fa-users",  IsActive = true },
AerationSterilize/AerationSterilize.Persistence/ApplicationContextSeed.cs:143:            new Function { Id = "PRODUCT",   Name = "Product",   Url = "/product",   ParrentId = string.Empty, SortOrder = 3, CssClass = "fa-cube",       IsActive = true }
AerationSterilize/AerationSterilize.Persistence/ApplicationContextSeed.cs:174:            .SelectMany(f => actionIds.Select(a => new ActionInFunction { FunctionId = f, ActionId = a }))
10.0.401
9.0.5
{
    "sdk": {
        "version": "8.0",
        "allowPrerelease": false,
        "rollForward": "latestMajor"
    }
}
Bash Read Aeration seed
IN
cat AerationSterilize/AerationSterilize.Persistence/ApplicationContextSeed.cs | sed -n 1,60p; sed -n 120,240p AerationSterilize/AerationSterilize.Persistence/ApplicationContextSeed.cs; cat AerationSterilize/AerationSterilize.Domain/Entities/Identity/Permission.cs

OUT
﻿using AerationSterilize.Domain.Entities;
using AerationSterilize.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Action = AerationSterilize.Domain.Entities.Identity.Action;

namespace AerationSterilize.Persistence;
public class ApplicationContextSeed
{
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
            new Function { Id = "DASHBOARD", Name = "Dashboard", Url = "/dashboard", ParrentId = string.Empty, SortOrder = 1, CssClass = "fa-dashboard", IsActive = true },
            new Function { Id = "SYSTEM",    Name = "System",    Url = "/system",    ParrentId = string.Empty, SortOrder = 2, CssClass = "fa-cogs",      IsActive = true },
            new Function { Id = "USER",      Name = "User",      Url = "/system/user", ParrentId = "SYSTEM", SortOrder = 1, CssClass = "fa-user",   IsActive = true },
            new Function { Id = "ROLE",      Name = "Role",      Url = "/system/role", ParrentId = "SYSTEM", SortOrder = 2, CssClass = "fa-users",  IsActive = true },
            new Function { Id = "PRODUCT",   Name = "Product",   Url = "/product",   ParrentId = string.Empty, SortOrder = 3, CssClass = "fa-cube",       IsActive = true }
        };

        await _context.Functions.AddRangeAsync(functions);
        _logger.Information("Seeded {Count} functions.", functions.Length);
    }

    private async Task SeedActionsAsync()
    {
        if (await _context.Actions.AnyAsync()) return;

        var actions = new[]
        {
            new Action { Id = "VIEW",   Name = "View",   SortOrder = 1, IsActive = true },
            new Action { Id = "CREATE", Name = "Create", SortOrder = 2, IsActive = true },
            new Action { Id = "UPDATE", Name = "Update", SortOrder = 3, IsActive = true },
            new Action { Id = "DELETE", Name = "Delete", SortOrder = 4, IsActive = true }
        };

        await _context.Actions.AddRangeAsync(actions);
        _logger.Information("Seeded {Count} actions.", actions.Length);
    }

    private async Task SeedActionInFunctionsAsync()
    {
        if (await _context.ActionInFunctions.AnyAsync()) return;

        var functionIds = new[] { "DASHBOARD", "SYSTEM", "USER", "ROLE", "PRODUCT" };
        var actionIds = new[] { "VIEW", "CREATE", "UPDATE", "DELETE" };

        var actionInFunctions = functionIds
            .SelectMany(f => actionIds.Select(a => new ActionInFunction { FunctionId = f, ActionId = a }))
            .ToList();

        await _context.ActionInFunctions.AddRangeAsync(actionInFunctions);
        _logger.Information("Seeded {Count} action-in-functions.", actionInFunctions.Count);
    }

    private async Task SeedPermissionsAsync()
    {
        if (await _context.Permissions.AnyAsync()) return;

        var adminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var functionIds = new[] { "DASHBOARD", "SYSTEM", "USER", "ROLE", "PRODUCT" };
        var actionIds = new[] { "VIEW", "CREATE", "UPDATE", "DELETE" };

        var permissions = functionIds
            .SelectMany(f => actionIds.Select(a => new Permission
            {
                RoleId = adminRoleId,
                FunctionId = f,
                ActionId = a
            }))
            .ToList();

        await _context.Permissions.AddRangeAsync(permissions);
        _logger.Information("Seeded {Count} permissions for Administrator.", permissions.Count);
    }

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
﻿using Shared.Common.Constants.Authorization;

namespace AerationSterilize.Domain.Entities.Identity;
public class Permission
{
    public Guid RoleId { get; set; }
    public string FunctionId { get; set; }
    public string ActionId { get; set; }
    /// <summary>
    /// Phân quyền theo dữ liệu
    /// </summary>
    public AccessLevel AccessLevel { get; set; } = AccessLevel.All;
}
Tôi sẽ thiết kế theo hướng Aeration là nơi duy nhất đăng nhập và cấp token (permission nhúng sẵn trong JWT), còn CommonServices chỉ validate token và kiểm tra claim permission. Tiếp theo tôi sẽ gom phần JWT + [Permission] dùng chung vào BuildingBlocks và build baseline trước.
Bash Baseline build of solution
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase && dotnet build MyApp.sln -nologo -v q 2>&1 | grep -E "error|Build succeeded|Warn|Error" | sort -u | head -20

OUT
    0 Error(s)
    396 Warning(s)
Build succeeded.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Contracts\Responses\Error.cs(19,33): warning S3875: Remove this overload of 'operator =='. (https://rules.sonarsource.com/csharp/RSPEC-3875) [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Contracts\Contracts.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Contracts\Responses\Error.cs(2,14): warning S3897: Implement 'IEquatable<Error>'. (https://rules.sonarsource.com/csharp/RSPEC-3897) [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Contracts\Contracts.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\Behaviors\PerformanceBehaviour.cs(30,28): warning CA2254: The logging message template should not vary between calls to 'LoggerExtensions.LogWarning(ILogger, string?, params object?[])' (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2254) [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\AerationSterilize.Application.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.Application\Behaviors\PerformanceBehaviour.cs(30,28): warning CA2254: The logging message template should not vary between calls to 'LoggerExtensions.LogWarning(ILogger, string?, params object?[])' (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2254) [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.Application\CommonServices.Application.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.Application\Common\Errors\MasterDataErrors.cs(7,71): warning CA1308: In method 'CodeAlreadyExists', replace the call to 'ToLowerInvariant' with 'ToUpperInvariant' (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1308) [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.Application\CommonServices.Application.csproj]
Bash Check csproj and remaining identity references
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src && cat Services/CommonServices/CommonServices.Application/CommonServices.Application.csproj Services/CommonServices/CommonServices.Domain/CommonServices.Domain.csproj Services/AerationSterilize/AerationSterilize.Application/AerationSterilize.Application.csproj; grep -rn "AuthException\|Entities.Identity" --include=*.cs Services/CommonServices | grep -v -E "/(bin|obj|Migrations)/|Features/V1/Auth|Entities/Identity/|Configurations/Identity"; ls Services/CommonServices/CommonServices.Persistence/Configurations Services/CommonServices/CommonServices.Domain/Exceptions; grep -rn "Middlewares\|class " BuildingBlocks/Infrastructure --include=*.cs -l | grep -v obj | head -30

OUT
﻿<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
	</PropertyGroup>

	<ItemGroup>
		<Folder Include="Abstractions\" />
		<Folder Include="Features\V2\" />
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="FluentValidation" />
		<PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
		<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\..\BuildingBlocks\Common.Logging\Common.Logging.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Contracts\Contracts.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Infrastructure\Infrastructure.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Shared\Shared.csproj" />
		<ProjectReference Include="..\CommonServices.Domain\CommonServices.Domain.csproj" />
	</ItemGroup>

</Project>
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Identity" />
    <PackageReference Include="Microsoft.Extensions.Identity.Core" />
    <PackageReference Include="Microsoft.Extensions.Identity.Stores" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\BuildingBlocks\Contracts\Contracts.csproj" />
  </ItemGroup>

</Project>
﻿<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
	</PropertyGroup>

	<ItemGroup>
		<Folder Include="Abstractions\" />
		<Folder Include="Features\V1\Products\Common\Validator\" />
		<Folder Include="Features\V1\Products\Events\" />
		<Folder Include="Features\V2\" />
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="FluentValidation" />
		<PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
		<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\..\BuildingBlocks\Common.Logging\Common.Logging.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Contracts\Contracts.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Infrastructure\Infrastructure.csproj" />
		<ProjectReference Include="..\..\..\BuildingBlocks\Shared\Shared.csproj" />
		<ProjectReference Include="..\AerationSterilize.Domain\AerationSterilize.Domain.csproj" />
	</ItemGroup>

</Project>
Services/CommonServices/CommonServices.Domain/Exceptions/AuthException.cs:4:public static class AuthException
Services/CommonServices/CommonServices.Persistence/ApplicationContextSeed.cs:2:using CommonServices.Domain.Entities.Identity;
Services/CommonServices/CommonServices.Persistence/ApplicationContextSeed.cs:9:using Action = CommonServices.Domain.Entities.Identity.Action;
Services/CommonServices/CommonServices.Persistence/ApplicationDbContext.cs:1:using CommonServices.Domain.Entities.Identity;
Services/CommonServices/CommonServices.Persistence/ApplicationDbContext.cs:18:    public DbSet<Domain.Entities.Identity.Action> Actions { get; set; }
Services/CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ApplicationBuilderExtensions.cs:1:﻿using CommonServices.Domain.Entities.Identity;
Services/CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:1:﻿using CommonServices.Domain.Entities.Identity;
Services/CommonServices/CommonServices.Domain/Exceptions:
AuthException.cs
BankException.cs
CityException.cs
CountryException.cs
CurrencyException.cs
LanguageException.cs
RegionException.cs

Services/CommonServices/CommonServices.Persistence/Configurations:
Identity
MasterData
BuildingBlocks/Infrastructure/Behaviors/CacheInvalidationPipelineBehavior.cs
BuildingBlocks/Infrastructure/Behaviors/QueryCachingPipelineBehavior.cs
BuildingBlocks/Infrastructure/Caching/CachedPermissionService.cs
BuildingBlocks/Infrastructure/Caching/RedisCacheService.cs
BuildingBlocks/Infrastructure/Common/Repositories/RepositoryBase.cs
BuildingBlocks/Infrastructure/Common/UnitOfWork.cs
BuildingBlocks/Infrastructure/Extensions/CachingServiceCollectionExtensions.cs
BuildingBlocks/Infrastructure/Extensions/ConfigurationExtensions.cs
BuildingBlocks/Infrastructure/Identity/RedisTokenRevocationService.cs
BuildingBlocks/Infrastructure/Identity/TokenService.cs
BuildingBlocks/Infrastructure/Middlewares/ExceptionHandlingMiddleware.cs
BuildingBlocks/Infrastructure/Services/Email/EmailService.cs
BuildingBlocks/Infrastructure/Services/Excel/EpplusExcelReaderService.cs
9:07 PM
Đang triển khai: tạo phần auth dùng chung ở BuildingBlocks trước.



Bash Find usages of Authorization namespace and options
IN
grep -rln "API.Authorization" --include=*.cs Services | grep -v -E "/(bin|obj)/"; find BuildingBlocks -name "RedisOptions.cs" -o -name "ICacheService.cs" | grep -v obj | xargs cat; grep -rn "AddConfigurationOptions\|JwtOptions" --include=*.cs Services | grep -v -E "/(bin|obj)/"

OUT
Services/AerationSterilize/AerationSterilize.API/Authorization/PermissionAttribute.cs
Services/AerationSterilize/AerationSterilize.API/Authorization/PermissionAuthorizationHandler.cs
Services/AerationSterilize/AerationSterilize.API/Authorization/PermissionRequirement.cs
Services/AerationSterilize/AerationSterilize.API/Controllers/V1/ProductsController.cs
Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
Services/CommonServices/CommonServices.API/Authorization/PermissionAttribute.cs
Services/CommonServices/CommonServices.API/Authorization/PermissionAuthorizationHandler.cs
Services/CommonServices/CommonServices.API/Authorization/PermissionRequirement.cs
Services/CommonServices/CommonServices.API/Controllers/V1/BanksController.cs
Services/CommonServices/CommonServices.API/Controllers/V1/CitiesController.cs
Services/CommonServices/CommonServices.API/Controllers/V1/CountriesController.cs
Services/CommonServices/CommonServices.API/Controllers/V1/CurrenciesController.cs
Services/CommonServices/CommonServices.API/Controllers/V1/LanguagesController.cs
Services/CommonServices/CommonServices.API/Controllers/V1/RegionsController.cs
Services/CommonServices/CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
namespace Contracts.Caching;
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
using System.ComponentModel.DataAnnotations;

namespace Shared.Options;

public class RedisOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    // Prefix added to every key, so several apps can share one Redis instance.
    public string InstanceName { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int DefaultExpirationMinutes { get; set; } = 10;
}
Services/AerationSterilize/AerationSterilize.API/Program.cs:29:    builder.Services.AddConfigurationOptions(builder.Configuration);
Services/AerationSterilize/AerationSterilize.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:16:    public static void AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
Services/AerationSterilize/AerationSterilize.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:18:        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
Services/AerationSterilize/AerationSterilize.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:46:        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
Services/AerationSterilize/AerationSterilize.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:49:            throw new ArgumentNullException($"{nameof(JwtOptions)} is not configured properly");
Services/CommonServices/CommonServices.API/Program.cs:29:    builder.Services.AddConfigurationOptions(builder.Configuration);
Services/CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:16:    public static void AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
Services/CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:18:        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
Services/CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:46:        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
Services/CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs:49:            throw new ArgumentNullException($"{nameof(JwtOptions)} is not configured properly");
Bash Move permission authorization classes to BuildingBlocks
IN
mkdir -p BuildingBlocks/Infrastructure/Authorization && for f in PermissionAttribute PermissionAuthorizationHandler PermissionRequirement; do git mv Services/AerationSterilize/AerationSterilize.API/Authorization/$f.cs BuildingBlocks/Infrastructure/Authorization/$f.cs; git rm -q Services/CommonServices/CommonServices.API/Authorization/$f.cs; done && sed -i 's/namespace AerationSterilize.API.Authorization;/namespace Infrastructure.Authorization;/' BuildingBlocks/Infrastructure/Authorization/*.cs && grep -rl "API.Authorization" --include=*.cs Services | grep -v -E "/(bin|obj)/" | xargs sed -i -E 's/using (AerationSterilize|CommonServices)\.API\.Authorization;/using Infrastructure.Authorization;/' && head -5 BuildingBlocks/Infrastructure/Authorization/*.cs

OUT
==> BuildingBlocks/Infrastructure/Authorization/PermissionAttribute.cs <==
﻿using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]

==> BuildingBlocks/Infrastructure/Authorization/PermissionAuthorizationHandler.cs <==
﻿using Microsoft.AspNetCore.Authorization;
using Shared.Common.Constants.Authorization;

namespace Infrastructure.Authorization;


==> BuildingBlocks/Infrastructure/Authorization/PermissionRequirement.cs <==
﻿using Microsoft.AspNetCore.Authorization;
using Shared.Common.Constants.Authorization;

namespace Infrastructure.Authorization;
9:07 PM
Now the shared JWT/authorization extension and csproj update.



Write C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Extensions\AuthenticationServiceCollectionExtensions.cs
83 lines
using System.Text;
using Contracts.Identity;
using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.Options;

namespace Infrastructure.Extensions;
public static class AuthenticationServiceCollectionExtensions
{
    // Validates access tokens issued by AerationSterilize (the identity service).
    // Every service must use the same JwtOptions (SecretKey, Issuer, Audience) as AerationSterilize.
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
        if (jwtOptions == null || string.IsNullOrEmpty(jwtOptions.SecretKey))
        {
            throw new ArgumentNullException($"{nameof(JwtOptions)} is not configured properly");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,

            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateLifetime = true,
            RequireExpirationTime = true,

            ClockSkew = TimeSpan.Zero
        };
        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(x =>
        {
            x.SaveToken = true;
            x.RequireHttpsMetadata = false;
            x.TokenValidationParameters = tokenValidationParameters;
            x.Events = new JwtBearerEvents
            {
                // Reject access tokens that were revoked on logout, even though their signature and lifetime are still valid.
                OnTokenValidated = async context =>
                {
                    var jti = context.SecurityToken?.Id;
                    if (string.IsNullOrEmpty(jti))
                    {
                        context.Fail("Token has no jti.");
                        return;
                    }

                    var tokenRevocationService = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationService>();
                    if (await tokenRevocationService.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
                        context.Fail("Token has been revoked.");
                }
            };
        });

        return services;
    }

    // [Permission(FunctionCode, CommandCode)] is checked against the permission claims inside the access token.
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        services.AddHttpContextAccessor();

        return services;
    }
}
Bash Add ASP.NET Core framework and JwtBearer to Infrastructure
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/BuildingBlocks/Infrastructure && sed -i 's#\t\t<PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" />#\t\t<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />#' Infrastructure.csproj && sed -i 's#\t<ItemGroup>\r\{0,1\}$#&#' Infrastructure.csproj && python - <<'EOF'
p='Infrastructure.csproj'
s=open(p,encoding='utf-8-sig').read()
nl='\r\n' if '\r\n' in s else '\n'
s=s.replace('\t</PropertyGroup>'+nl, '\t</PropertyGroup>'+nl+nl+'\t<ItemGroup>'+nl+'\t\t<FrameworkReference Include="Microsoft.AspNetCore.App" />'+nl+'\t</ItemGroup>'+nl,1)
open(p,'w',encoding='utf-8-sig').write(s)
EOF
cat Infrastructure.csproj

OUT
﻿<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>enable</Nullable>
	</PropertyGroup>

	<ItemGroup>
		<FrameworkReference Include="Microsoft.AspNetCore.App" />
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
		<PackageReference Include="Microsoft.EntityFrameworkCore" />
		<PackageReference Include="Microsoft.IdentityModel.Tokens" />
		<PackageReference Include="System.IdentityModel.Tokens.Jwt" />
		<PackageReference Include="EPPlus" />
		<PackageReference Include="StackExchange.Redis" />
		<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
		<PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\Services\AerationSterilize\AerationSterilize.Domain\AerationSterilize.Domain.csproj" />
		<ProjectReference Include="..\Common.Logging\Common.Logging.csproj" />
		<ProjectReference Include="..\Contracts\Contracts.csproj" />
		<ProjectReference Include="..\Shared\Shared.csproj" />
	</ItemGroup>

</Project>
9:07 PM
Now make the revoked-token deny-list shared across services (currently keys are prefixed aeration: vs common:, so a logout in Aeration wouldn't be seen by CommonServices):



Write C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Identity\RedisTokenRevocationService.cs
60 lines
using Contracts.Caching;
using Contracts.Identity;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Identity;

// Deny-list of access token ids (jti). Each entry lives only until the token would have expired anyway.
// Keys are written without RedisOptions.InstanceName: a token revoked by AerationSterilize (logout)
// must also be rejected by every other service that accepts it.
internal sealed class RedisTokenRevocationService : ITokenRevocationService
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger<RedisTokenRevocationService> _logger;

    public RedisTokenRevocationService(IConnectionMultiplexer connection, ILogger<RedisTokenRevocationService> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return;

        if (!_connection.IsConnected)
        {
            _logger.LogWarning("Redis is unavailable, could not revoke token {Jti}", jti);
            return;
        }

        try
        {
            await _connection.GetDatabase().StringSetAsync(CacheKeys.RevokedToken(jti), true, remaining);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis SET failed while revoking token {Jti}", jti);
        }
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (!_connection.IsConnected)
            return false;

        try
        {
            return await _connection.GetDatabase().KeyExistsAsync(CacheKeys.RevokedToken(jti));
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis EXISTS failed for revoked token {Jti}", jti);
            return false;
        }
    }
}
Edit C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Extensions\CachingServiceCollectionExtensions.cs
Added 1 line
Edit C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Extensions\CachingServiceCollectionExtensions.cs
Added 1 line
9:08 PM
Now update Aeration to use the shared code:



Bash Remove duplicated JWT config from both Application layers
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services && python - <<'EOF'
import re
for svc in ['AerationSterilize','CommonServices']:
    p=f'{svc}/{svc}.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs'
    s=open(p,encoding='utf-8-sig').read()
    i=s.index('    public static IServiceCollection AddConfigureJWTAuthentication')
    j=s.rindex('}')
    s=s[:i].rstrip()+'\n}\n'
    for u in ['using System.Text;\n','using Contracts.Identity;\n','using Microsoft.AspNetCore.Authentication.JwtBearer;\n','using Microsoft.IdentityModel.Tokens;\n']:
        s=s.replace(u,'')
    open(p,'w',encoding='utf-8-sig').write(s)
EOF
cat CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs | head -20; tail -5 CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs

OUT
﻿using CommonServices.Application.Behaviors;
using Infrastructure.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;

namespace CommonServices.Application.DependencyInjection.Extensions;
public static class ServiceCollectionExtensions
{
    public static void AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
        services.AddSingleton(jwtOptions);
    }
    public static IServiceCollection AddConfigureMediaR(this IServiceCollection services)
    {
        services
            .AddConfigureAutoMapper()
            .AddAutoMapper(_ => { }, AssemblyReference.Assembly)
            ;
        return services;
    }
}
Bash Wire both APIs to shared auth extensions
IN
python - <<'EOF'
# Aeration API: use shared JWT + permission authorization
p='AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs'
s=open(p,encoding='utf-8-sig').read()
s=s.replace('using Infrastructure.Authorization;\n','')
s=s.replace('using Infrastructure.Identity;\nusing Microsoft.AspNetCore.Authorization;\n','using Infrastructure.Extensions;\nusing Infrastructure.Identity;\n')
s=s.replace('        services.AddConfigureJWTAuthentication(configuration);','        services.AddJwtAuthentication(configuration);')
s=s.replace('''        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        services.AddHttpContextAccessor();
''','''        services.AddPermissionAuthorization();
''')
open(p,'w',encoding='utf-8-sig').write(s)

# CommonServices API: no login, no token issuing, no permission DB - only validate AerationSterilize tokens
p='CommonServices/CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs'
s=open(p,encoding='utf-8-sig').read()
s=s.replace('''using Infrastructure.Authorization;
using CommonServices.API.Services;
using CommonServices.Application.Common;
using CommonServices.Application.DependencyInjection.Extensions;
using CommonServices.Persistence.Services;
using Contracts.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
''','''using CommonServices.API.Services;
using CommonServices.Application.Common;
using Infrastructure.Extensions;
''')
s=s.replace('''        services.AddTransient<ITokenService, TokenService>();
        services.AddConfigureJWTAuthentication(configuration);''','''        // Login / token issuing lives in AerationSterilize; this service only validates its access tokens.
        services.AddJwtAuthentication(configuration);''')
s=s.replace('''        services.AddScoped<IPermissionService, PermissionService>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        services.AddHttpContextAccessor();
''','''        // Permissions are read from the claims of the AerationSterilize access token.
        services.AddPermissionAuthorization();
''')
open(p,'w',encoding='utf-8-sig').write(s)

p='CommonServices/CommonServices.API/Program.cs'
s=open(p,encoding='utf-8-sig').read()
s=s.replace('    builder.Services.AddConfigurationOptions(builder.Configuration);\n','')
s=s.replace('    builder.Services.AddRedisCache(builder.Configuration); // after AddAuthorizationConfigurationAPI: decorates IPermissionService','    builder.Services.AddRedisCache(builder.Configuration); // token revocation list shared with AerationSterilize')
open(p,'w',encoding='utf-8-sig').write(s)

p='CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs'
s=open(p,encoding='utf-8-sig').read()
s=s.replace('''    public static void AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
        services.AddSingleton(jwtOptions);
    }
''','')
s=s.replace('using Microsoft.Extensions.Configuration;\n','').replace('using Shared.Options;\n','')
open(p,'w',encoding='utf-8-sig').write(s)

for svc in ['AerationSterilize','CommonServices']:
    p=f'{svc}/{svc}.Application/{svc}.Application.csproj'
    s=open(p,encoding='utf-8-sig').read()
    s=s.replace('\t\t<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />\r\n','').replace('\t\t<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />\n','')
    open(p,'w',encoding='utf-8-sig').write(s)
EOF
cat CommonServices/CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs; git diff --stat

OUT
﻿using CommonServices.API.Services;
using CommonServices.Application.Common;
using Infrastructure.Extensions;

namespace CommonServices.API.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddApiVersioningConfiguration(this IServiceCollection services)
       => services.AddApiVersioning(options => options.ReportApiVersions = true)
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

    public static void ConfigureCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration["AllowOrigins"];

        // Login / token issuing lives in AerationSterilize; this service only validates its access tokens.
        services.AddJwtAuthentication(configuration);
        services.AddCors(option =>
        {
            option.AddPolicy("CorsPolicy", buider =>
            {
                buider.WithOrigins(origins: origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            });
        });
    }

    public static void AddAuthorizationConfigurationAPI(this IServiceCollection services)
    {
        // Permissions are read from the claims of the AerationSterilize access token.
        services.AddPermissionAuthorization();
        services.AddScoped<ICurrentUser, CurrentUser>();
    }

}

warning: in the working copy of 'src/BuildingBlocks/Infrastructure/Authorization/PermissionAttribute.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/BuildingBlocks/Infrastructure/Authorization/PermissionAuthorizationHandler.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/BuildingBlocks/Infrastructure/Authorization/PermissionRequirement.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/BuildingBlocks/Infrastructure/Extensions/CachingServiceCollectionExtensions.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/BuildingBlocks/Infrastructure/Identity/RedisTokenRevocationService.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/Services/AerationSterilize/AerationSterilize.API/Controllers/V1/ProductsController.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/Services/CommonServices/CommonServices.API/Controllers/V1/BanksController.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/Services/CommonServices/CommonServices.API/Controllers/V1/CitiesController.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/Services/CommonServices/CommonServices.API/Controllers/V1/CountriesController.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/Services/CommonServices/CommonServices.API/Controllers/V1/CurrenciesController.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/Services/CommonServices/CommonServices.API/Controllers/V1/LanguagesController.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/Services/CommonServices/CommonServices.API/Controllers/V1/RegionsController.cs', LF will be replaced by CRLF the next time Git touches it
 .../Authorization/PermissionAttribute.cs           |  2 +-
 .../PermissionAuthorizationHandler.cs              |  2 +-
 .../Authorization/PermissionRequirement.cs         |  2 +-
 .../CachingServiceCollectionExtensions.cs          |  6 +-
 .../Identity/RedisTokenRevocationService.cs        | 44 ++++++++++++--
 .../Infrastructure/Infrastructure.csproj           |  6 +-
 .../Controllers/V1/ProductsController.cs           |  2 +-
 .../Extensions/ServiceCollectionExtensions.cs      | 11 ++--
 .../AerationSterilize.Application.csproj           |  1 -
 .../Extensions/ServiceCollectionExtensions.cs      | 63 +------------------
 .../Controllers/V1/BanksController.cs              |  2 +-
 .../Controllers/V1/CitiesController.cs             |  2 +-
 .../Controllers/V1/CountriesController.cs          |  2 +-
 .../Controllers/V1/CurrenciesController.cs         |  2 +-
 .../Controllers/V1/LanguagesController.cs          |  2 +-
 .../Controllers/V1/RegionsController.cs            |  2 +-
 .../Extensions/ServiceCollectionExtensions.cs      | 19 ++----
 .../CommonServices/CommonServices.API/Program.cs   |  5 +-
 .../CommonServices.Application.csproj              |  1 -
 .../Extensions/ServiceCollectionExtensions.cs      | 70 +---------------------
 20 files changed, 71 insertions(+), 175 deletions(-)
9:08 PM
Now remove the duplicated identity/login code from CommonServices:



Bash Delete CommonServices auth and identity files
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/CommonServices && git rm -rq CommonServices.API/Controllers/V1/AuthController.cs CommonServices.Application/Features/V1/Auth CommonServices.Domain/Entities/Identity CommonServices.Domain/Exceptions/AuthException.cs CommonServices.Persistence/Configurations/Identity CommonServices.Persistence/Services/PermissionService.cs && ls CommonServices.Persistence/Services CommonServices.API/Authorization 2>&1; git status --short . | head -50

OUT
ls: cannot access 'CommonServices.Persistence/Services': No such file or directory
ls: cannot access 'CommonServices.API/Authorization': No such file or directory
D  CommonServices.API/Authorization/PermissionAttribute.cs
D  CommonServices.API/Authorization/PermissionAuthorizationHandler.cs
D  CommonServices.API/Authorization/PermissionRequirement.cs
D  CommonServices.API/Controllers/V1/AuthController.cs
 M CommonServices.API/Controllers/V1/BanksController.cs
 M CommonServices.API/Controllers/V1/CitiesController.cs
 M CommonServices.API/Controllers/V1/CountriesController.cs
 M CommonServices.API/Controllers/V1/CurrenciesController.cs
 M CommonServices.API/Controllers/V1/LanguagesController.cs
 M CommonServices.API/Controllers/V1/RegionsController.cs
 M CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
 M CommonServices.API/Program.cs
 M CommonServices.Application/CommonServices.Application.csproj
 M CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
D  CommonServices.Application/Features/V1/Auth/Commands/Login/LoginCommand.cs
D  CommonServices.Application/Features/V1/Auth/Commands/Login/LoginCommandHandler.cs
D  CommonServices.Application/Features/V1/Auth/Commands/Login/LoginCommandValidator.cs
D  CommonServices.Application/Features/V1/Auth/Commands/Logout/LogoutCommand.cs
D  CommonServices.Application/Features/V1/Auth/Commands/Logout/LogoutCommandHandler.cs
D  CommonServices.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommand.cs
D  CommonServices.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs
D  CommonServices.Application/Features/V1/Auth/Commands/Register/RegisterCommand.cs
D  CommonServices.Application/Features/V1/Auth/Commands/Register/RegisterCommandHandler.cs
D  CommonServices.Application/Features/V1/Auth/Commands/Register/RegisterCommandValidator.cs
D  CommonServices.Application/Features/V1/Auth/Common/Dtos/LoginDto.cs
D  CommonServices.Application/Features/V1/Auth/Common/Dtos/UserDto.cs
D  CommonServices.Domain/Entities/Identity/Action.cs
D  CommonServices.Domain/Entities/Identity/ActionInFunction.cs
D  CommonServices.Domain/Entities/Identity/AppRole.cs
D  CommonServices.Domain/Entities/Identity/AppUser.cs
D  CommonServices.Domain/Entities/Identity/Function.cs
D  CommonServices.Domain/Entities/Identity/Permission.cs
D  CommonServices.Domain/Entities/Identity/UserSession.cs
D  CommonServices.Domain/Exceptions/AuthException.cs
D  CommonServices.Persistence/Configurations/Identity/ActionConfiguration.cs
D  CommonServices.Persistence/Configurations/Identity/ActionInFunctionConfiguration.cs
D  CommonServices.Persistence/Configurations/Identity/AppRoleConfiguration.cs
D  CommonServices.Persistence/Configurations/Identity/AppUserConfiguration.cs
D  CommonServices.Persistence/Configurations/Identity/FunctionConfiguration.cs
D  CommonServices.Persistence/Configurations/Identity/IdentityConfiguration.cs
D  CommonServices.Persistence/Configurations/Identity/PermissionConfiguration.cs
D  CommonServices.Persistence/Configurations/Identity/UserSessionConfiguration.cs
D  CommonServices.Persistence/Services/PermissionService.cs
9:08 PM
Now DbContext, seed, DI, TableNames, csproj packages:



Bash Strip identity from CommonServices persistence and seed
IN
python - <<'EOF'
import re
def rw(p,f):
    s=open(p,encoding='utf-8-sig').read(); n=f(s); assert n!=s,p; open(p,'w',encoding='utf-8-sig').write(n)

rw('CommonServices.Persistence/ApplicationDbContext.cs', lambda s: s
   .replace('using CommonServices.Domain.Entities.Identity;\n','')
   .replace('using Microsoft.AspNetCore.Identity.EntityFrameworkCore;\n','')
   .replace('public class ApplicationDbContext : IdentityDbContext<AppUser, AppRole, Guid>','// Users, roles and permissions live in AerationSterilize (identity service), not here.\npublic class ApplicationDbContext : DbContext')
   .replace('''    public DbSet<AppUser> AppUses { get; set; }
    public DbSet<Domain.Entities.Identity.Action> Actions { get; set; }
    public DbSet<Function> Functions { get; set; }
    public DbSet<ActionInFunction> ActionInFunctions { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }

''',''))

rw('CommonServices.Persistence/DependencyInjection/Extensions/ApplicationBuilderExtensions.cs', lambda s: s
   .replace('using CommonServices.Domain.Entities.Identity;\n','')
   .replace('using Microsoft.AspNetCore.Identity;\n','')
   .replace('''        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
''','')
   .replace('new ApplicationContextSeed(context, userManager, roleManager, logger)','new ApplicationContextSeed(context, logger)'))

def ds(s):
    s=s.replace('using CommonServices.Domain.Entities.Identity;\n','').replace('using Microsoft.AspNetCore.Identity;\n','')
    i=s.index('        services.AddIdentity<AppUser, AppRole>')
    j=s.index('.AddDefaultTokenProviders();')+len('.AddDefaultTokenProviders();\n')
    s=s[:i].rstrip()+'\n'+s[j:]
    return s
rw('CommonServices.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs', ds)

def seed(s):
    s=s.replace('using CommonServices.Domain.Entities.Identity;\n','').replace('using Microsoft.AspNetCore.Identity;\n','')
    s=s.replace('using Shared.Common.Constants.Authorization;\n','').replace('using Action = CommonServices.Domain.Entities.Identity.Action;\n','')
    i=s.index('    private static readonly Guid AdminRoleId'); j=s.index('    private readonly ApplicationDbContext _context;')
    s=s[:i]+s[j:]
    s=s.replace('''    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
''','')
    s=s.replace('''        ApplicationDbContext context,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ILogger logger)''','''        ApplicationDbContext context,
        ILogger logger)''')
    s=s.replace('''        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
''','')
    s=s.replace('''            await SeedRolesAsync();
            await SeedDefaultUserAsync();
            await SeedFunctionsAsync();
            await SeedActionsAsync();
            await SeedActionInFunctionsAsync();
            await SeedPermissionsAsync();
''','')
    i=s.index('    private async Task SeedRolesAsync()'); j=s.index('    private async Task SeedLanguagesAsync()')
    s=s[:i]+s[j:]
    return s
rw('CommonServices.Persistence/ApplicationContextSeed.cs', seed)

def tn(s):
    i=s.index('    internal const string Actions'); j=s.index('    // *********** Master Data')
    return s[:i]+s[j:]
rw('CommonServices.Persistence/Constants/TableNames.cs', tn)

def nopkg(names):
    def f(s):
        for n in names:
            s=re.sub(r'[ \t]*<PackageReference Include="%s" />\r?\n'%re.escape(n),'',s)
        return s
    return f
rw('CommonServices.Domain/CommonServices.Domain.csproj', nopkg(['Microsoft.AspNetCore.Identity','Microsoft.Extensions.Identity.Core','Microsoft.Extensions.Identity.Stores']))
rw('CommonServices.Persistence/CommonServices.Persistence.csproj', nopkg(['Microsoft.AspNetCore.Identity','Microsoft.AspNetCore.Identity.EntityFrameworkCore']))
EOF
cat CommonServices.Domain/CommonServices.Domain.csproj CommonServices.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs | sed -n 1,60p; sed -n 1,60p CommonServices.Persistence/ApplicationContextSeed.cs

OUT
﻿<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\BuildingBlocks\Contracts\Contracts.csproj" />
  </ItemGroup>

</Project>
﻿using CommonServices.Persistence.DependencyInjection.Options;
using CommonServices.Persistence.Repositories;
using Contracts.Common.Repositories;
using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Options;

namespace CommonServices.Persistence.DependencyInjection.Extensions;
public static class ServiceCollectionExtensions
{
    public static void AddRepositoryBaseConfiguration(this IServiceCollection services)
    => services
        .AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork<ApplicationDbContext>))
        .AddScoped(typeof(IRepositoryBase<,>), typeof(ApplicationRepository<,>))
        ;

    public static void AddSqlConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseOptions = configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>();
        if (databaseOptions == null || string.IsNullOrEmpty(databaseOptions.ConnectionString))
            throw new ArgumentNullException("Connection string is not configured.");

        services.AddDbContextPool<DbContext, ApplicationDbContext>((provider, builder) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var options = provider.GetRequiredService<IOptionsMonitor<SqlServerRetryOptions>>();

            builder
         .EnableDetailedErrors(true)
         .EnableSensitiveDataLogging(true)
         //   .UseLazyLoadingProxies(true) // => If UseLazyLoadingProxies, all of the navigation fields should be VIRTUAL
         .UseSqlServer(
                 connectionString: databaseOptions.ConnectionString,
                 sqlServerOptionsAction: optionsBuilder
                     => optionsBuilder
                     .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));


        });
    }

﻿using CommonServices.Domain.Constants;
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
Bash Point CommonServices JWT settings at Aeration issuer
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services && python - <<'EOF'
import re
p='CommonServices/CommonServices.Domain/CommonServices.Domain.csproj'
s=open(p,encoding='utf-8-sig').read()
s=re.sub(r'  <ItemGroup>\r?\n  </ItemGroup>\r?\n\r?\n','',s)
open(p,'w',encoding='utf-8-sig').write(s)
p='CommonServices/CommonServices.API/appsettings.json'
s=open(p,encoding='utf-8').read()
old='''    "JwtOptions": {
        "SecretKey": "CommonServices_SuperSecretKey_PhaiDaiHon_32KyTu_Nhe_123456!",
        "Issuer": "CommonServices",
        "Audience": "CommonServices",
        "ExpiryMinutes": 60
    },'''
new='''    "JwtOptions": {
        "SecretKey": "AeartionSterilize_SuperSecretKey_PhaiDaiHon_32KyTu_Nhe_123456!",
        "Issuer": "AerationSterilize",
        "Audience": "AerationSterilize"
    },'''
s2=s.replace(old.replace('\n','\r\n'),new.replace('\n','\r\n')) if '\r\n' in s else s.replace(old,new)
assert s2!=s
open(p,'w',encoding='utf-8').write(s2)
EOF
git diff CommonServices/CommonServices.API/appsettings.json

OUT
(Bash completed with no output)
Bash Check appsettings change and tracking
IN
git diff --stat -- CommonServices/CommonServices.API/appsettings.json; grep -n -A4 JwtOptions CommonServices/CommonServices.API/appsettings.json; git check-ignore -v CommonServices/CommonServices.API/appsettings.json; git ls-files CommonServices/CommonServices.API | grep appsettings

OUT
21:    "JwtOptions": {
22-        "SecretKey": "AeartionSterilize_SuperSecretKey_PhaiDaiHon_32KyTu_Nhe_123456!",
23-        "Issuer": "AerationSterilize",
24-        "Audience": "AerationSterilize"
25-    },
.gitignore:116:appsettings.json	CommonServices/CommonServices.API/appsettings.json
appsettings.json bị gitignore nên thay đổi chỉ áp dụng ở local. Giờ tôi sẽ sửa seed Aeration để thêm các function master data (BANK, CITY…) cùng action ACTIVATE/INACTIVATE, tránh việc admin thiếu quyền gọi CommonServices.
Read C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Persistence\ApplicationContextSeed.cs (lines 2-11)
Bash Make Aeration seed include master-data permissions
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize/AerationSterilize.Persistence && python - <<'EOF'
p='ApplicationContextSeed.cs'
s=open(p,encoding='utf-8-sig').read()
crlf='\r\n' in s
s=s.replace('\r\n','\n')
i=s.index('    private async Task SeedFunctionsAsync()'); j=s.index('    private async Task SeedProductsAsync()')
new='''    // Functions, actions and permissions are additive: entries missing from an existing database are inserted,
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

'''
s=s[:i]+new+s[j:]
s=s.replace('''public class ApplicationContextSeed
{
''','''public class ApplicationContextSeed
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

''',1)
s=s.replace('using Serilog;\n','using Serilog;\nusing Shared.Common.Constants.Authorization;\n',1)
if crlf: s=s.replace('\n','\r\n')
open(p,'w',encoding='utf-8-sig').write(s)
EOF
grep -n "11111111\|AdminRoleId" ApplicationContextSeed.cs

OUT
12:    private static readonly Guid AdminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
88:                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
218:                .Where(p => p.RoleId == AdminRoleId)
228:                RoleId = AdminRoleId,
Bash Use constant in role seed and build solution
IN
sed -i 's/                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),/                Id = AdminRoleId,/' ApplicationContextSeed.cs && cd /c/Users/DuyQuang/Desktop/SourceCodeBase && dotnet build MyApp.sln -nologo -v q 2>&1 | grep -E " error |Build succeeded|Error\(s\)|warning (CS|NU|MSB)" | sort -u | head -30

OUT
    0 Error(s)
Build succeeded.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Common.Logging\Common.Logging.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Contracts\Contracts.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Common\Repositories\RepositoryBase.cs(33,9): warning CS8603: Possible null reference return. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Infrastructure.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Common\Repositories\RepositoryBase.cs(34,36): warning CS8602: Dereference of a possibly null reference. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Infrastructure.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Common\Repositories\RepositoryBase.cs(37,10): warning CS8603: Possible null reference return. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Infrastructure.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Identity\TokenService.cs(19,43): warning CS0169: The field 'TokenService._userManager' is never used [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Infrastructure.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Identity\TokenService.cs(22,12): warning CS8618: Non-nullable field '_userManager' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the field as nullable. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Infrastructure.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Infrastructure.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Services\Email\EmailService.cs(10,12): warning CS8618: Non-nullable field '_emailOptions' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the field as nullable. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Infrastructure.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Services\Email\EmailService.cs(8,35): warning CS0169: The field 'EmailService._emailOptions' is never used [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Infrastructure\Infrastructure.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Shared\Shared.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.API\AerationSterilize.API.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.API\DependencyInjection\Extensions\ServiceCollectionExtensions.cs(31,45): warning CS8604: Possible null reference argument for parameter 'origins' in 'CorsPolicyBuilder CorsPolicyBuilder.WithOrigins(params string[] origins)'. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.API\AerationSterilize.API.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\AerationSterilize.Application.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\DependencyInjection\Extensions\ServiceCollectionExtensions.cs(15,9): warning CS8634: The type 'Shared.Options.JwtOptions?' cannot be used as type parameter 'TService' in the generic type or method 'ServiceCollectionServiceExtensions.AddSingleton<TService>(IServiceCollection, TService)'. Nullability of type argument 'Shared.Options.JwtOptions?' doesn't match 'class' constraint. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\AerationSterilize.Application.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\Features\V1\Products\Commands\UpdateProduct\UpdateProductCommand.cs(10,19): warning CS8618: Non-nullable property 'Name' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\AerationSterilize.Application.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\Features\V1\Products\Commands\UpdateProduct\UpdateProductCommand.cs(12,19): warning CS8618: Non-nullable property 'Description' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\AerationSterilize.Application.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\Features\V1\Products\Queries\GetProducts\GetProductsQueryHandler.cs(29,65): warning CS8604: Possible null reference argument for parameter 'sortColumn' in 'Expression<Func<Product, object>> ProductExtension.GetSortExpression(string sortColumn)'. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Application\AerationSterilize.Application.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Domain\AerationSterilize.Domain.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Infrastructure\AerationSterilize.Infrastructure.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\AerationSterilize\AerationSterilize.Persistence\AerationSterilize.Persistence.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.API\CommonServices.API.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.API\DependencyInjection\Extensions\ServiceCollectionExtensions.cs(27,45): warning CS8604: Possible null reference argument for parameter 'origins' in 'CorsPolicyBuilder CorsPolicyBuilder.WithOrigins(params string[] origins)'. [C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.API\CommonServices.API.csproj]
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.Application\CommonServices.Application.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.Domain\CommonServices.Domain.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.Infrastructure\CommonServices.Infrastructure.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\src\Services\CommonServices\CommonServices.Persistence\CommonServices.Persistence.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
C:\Users\DuyQuang\Desktop\SourceCodeBase\tests\AerationSterilize.Architecture.Tests\AerationSterilize.Architecture.Tests.csproj : warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. The duplicate 'PackageVersion' items are: Ardalis.SmartEnum 8.2.0, Ardalis.SmartEnum 7.0.0.
9:09 PM
Build OK. Giờ tạo migration để xoá các bảng identity khỏi DB CommonServices:



Bash Generate EF migration removing identity tables
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/CommonServices && dotnet ef migrations add RemoveIdentity --project CommonServices.Persistence --startup-project CommonServices.API --no-build 2>&1 | tail -5; ls CommonServices.Persistence/Migrations

OUT
[21:09:48 Information] 
Shutdown complete

An operation was scaffolded that may result in the loss of data. Please review the migration for accuracy.
Done. To undo this action, use 'ef migrations remove'
20261014145811_Initial.Designer.cs
20261014145811_Initial.cs
20261015140950_RemoveIdentity.Designer.cs
20261015140950_RemoveIdentity.cs
ApplicationDbContextModelSnapshot.cs
Bash Inspect generated migration
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/CommonServices/CommonServices.Persistence/Migrations && grep -nE "migrationBuilder\.\w+|name: \"" 20261015140950_RemoveIdentity.cs | sed -n 1,40p; grep -c "Identity\|AppUser" ApplicationDbContextModelSnapshot.cs

OUT
14:            migrationBuilder.DropTable(
15:                name: "ActionInFunctions");
17:            migrationBuilder.DropTable(
18:                name: "AppRoleClaims");
20:            migrationBuilder.DropTable(
21:                name: "AppUserClaims");
23:            migrationBuilder.DropTable(
24:                name: "AppUserLogins");
26:            migrationBuilder.DropTable(
27:                name: "AppUserRoles");
29:            migrationBuilder.DropTable(
30:                name: "AppUserTokens");
32:            migrationBuilder.DropTable(
33:                name: "Permissions");
35:            migrationBuilder.DropTable(
36:                name: "UserSessions");
38:            migrationBuilder.DropTable(
39:                name: "Actions");
41:            migrationBuilder.DropTable(
42:                name: "AppRoles");
44:            migrationBuilder.DropTable(
45:                name: "Functions");
47:            migrationBuilder.DropTable(
48:                name: "AppUsers");
54:            migrationBuilder.CreateTable(
55:                name: "Actions",
68:            migrationBuilder.CreateTable(
69:                name: "AppRoles",
84:            migrationBuilder.CreateTable(
85:                name: "AppUsers",
119:            migrationBuilder.CreateTable(
120:                name: "Functions",
136:            migrationBuilder.CreateTable(
137:                name: "AppRoleClaims",
149:                        name: "FK_AppRoleClaims_AppRoles_RoleId",
156:            migrationBuilder.CreateTable(
157:                name: "AppUserClaims",
169:                        name: "FK_AppUserClaims_AppUsers_UserId",
176:            migrationBuilder.CreateTable(
177:                name: "AppUserLogins",
1
9:09 PM
Migration chỉ drop 12 bảng identity, không đụng master data. Build lại + chạy test:



Bash Rebuild and run tests
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase && dotnet build MyApp.sln -nologo -v q 2>&1 | grep -E " error |Error\(s\)" | sort -u; dotnet test MyApp.sln --no-build -nologo 2>&1 | grep -E "Passed!|Failed!|error|Total" | head

OUT
    0 Error(s)
Bash Show test output
IN
dotnet test MyApp.sln --no-build 2>&1 | tail -8

OUT
Test run for C:\Users\DuyQuang\Desktop\SourceCodeBase\tests\AerationSterilize.Architecture.Tests\bin\Debug\net8.0\AerationSterilize.Architecture.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
No test is available in C:\Users\DuyQuang\Desktop\SourceCodeBase\tests\AerationSterilize.Architecture.Tests\bin\Debug\net8.0\AerationSterilize.Architecture.Tests.dll. Make sure that test discoverer & executors are registered and platform & framework version settings are appropriate and try again.
9:10 PM
CommonServices giờ không còn login và bảng phân quyền riêng nữa. Aeration là nơi duy nhất đăng nhập và cấp token (token đã chứa sẵn danh sách quyền), còn CommonServices chỉ kiểm tra token của Aeration rồi đọc quyền từ token. Cả solution build không lỗi. Mình chưa chạy thử: chưa áp migration vào DB, chưa gọi API thật, và dự án không có test nào để chạy. Mình cũng chưa commit.

Đã xoá khỏi CommonServices

AuthController và toàn bộ phần login / register / refresh-token / logout.
Các entity user, role, permission, session cùng cấu hình EF của chúng, PermissionService, AuthException.
ApplicationDbContext chuyển từ IdentityDbContext sang DbContext thường. Bỏ AddIdentity, bỏ seed user/role/permission, bỏ package Identity.
Thêm migration RemoveIdentity: chỉ drop 12 bảng identity, không đụng bảng master data.
Phần dùng chung, chuyển vào BuildingBlocks/Infrastructure

[Permission] cùng handler và requirement của nó, trước đây bị copy y hệt ở cả 2 service, giờ nằm ở Infrastructure/Authorization/.
Cấu hình JWT, trước đây cũng copy y hệt ở 2 service, giờ gom vào AuthenticationServiceCollectionExtensions.cs (AddJwtAuthentication, AddPermissionAuthorization). Cả hai service đều gọi từ đây.
Hai lỗi đã sửa, nếu không thì cách này không chạy được

Logout không có tác dụng ở CommonServices. Danh sách token đã thu hồi trên Redis bị gắn tiền tố khác nhau theo service (aeration: và common:). Vì vậy logout ở Aeration thì CommonServices vẫn nhận token đó. Giờ key này không còn tiền tố nên các service dùng chung (RedisTokenRevocationService.cs).
Admin không có quyền gì ở CommonServices. Seed của Aeration thiếu các function master data (BANK, CITY, LANGUAGE…) và 2 action ACTIVATE/INACTIVATE. Mình đã thêm vào ApplicationContextSeed.cs. Seed giờ chỉ thêm những gì còn thiếu, nên DB đang chạy cũng được bổ sung.
Ngoài ra AddRedisCache không còn bắt buộc service phải có IPermissionService. Trước đây nó báo lỗi nếu thiếu, mà CommonServices giờ không còn dùng service này.

Bạn cần tự làm

appsettings.json bị gitignore, nên mình chỉ sửa được ở máy bạn. Phần JwtOptions của CommonServices phải giống Aeration (SecretKey, Issuer = Audience = AerationSterilize). Môi trường khác cũng cần cấu hình như vậy.
Bảng user/role/permission ở DB CommonServices sẽ bị drop khi chạy migration. Nếu trong đó có dữ liệu cần giữ thì chuyển sang DB Aeration trước.
Client từ giờ đăng nhập qua Aeration, lấy accessToken rồi gọi CommonServices.
Code lấy user hiện tại (CurrentUser) mình để nguyên ở từng service, vì nó dùng để ghi audit (ai tạo/sửa bản ghi), không phải phần đăng nhập.



AuthenticationServiceCollectionExtensions.cs
Ở thằng commonservice nó có phần phân quyền theo accesslevel.
chỗ ý tao không hiểu.
thằng aeration có chưa.
có đủ hết phân quyền chưa. kiểm tra lại \
Show more
Bash Find AccessLevel usage across services
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src && grep -rn "AccessLevel\|GetAccessLevel\|DepartmentId\|department_id\|DataScope\|OwnerId\|CreatedBy" --include=*.cs Services BuildingBlocks | grep -v -E "/(bin|obj)/|Migrations/" | head -60

OUT
Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs:7:public static class DataScopeExtensions
Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs:9:    public static IQueryable<T> ApplyDataScope<T>(
Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs:13:        return currentUser.GetAccessLevel(functionCode, commandCode) switch
Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs:15:            AccessLevel.All => query,
Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs:16:            AccessLevel.Department => query.Where(x => x.DepartmentId == currentUser.DepartmentId),
Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs:17:            AccessLevel.Own => query.Where(x => x.CreatedBy == currentUser.UserId),
Services/AerationSterilize/AerationSterilize.API/Services/CurrentUser.cs:17:    public Guid? DepartmentId =>
Services/AerationSterilize/AerationSterilize.API/Services/CurrentUser.cs:18:        Guid.TryParse(_user.FindFirstValue("department_id"), out var id) ? id : null;
Services/AerationSterilize/AerationSterilize.API/Services/CurrentUser.cs:27:    public AccessLevel GetAccessLevel(string functionCode, string commandCode)
Services/AerationSterilize/AerationSterilize.API/Services/CurrentUser.cs:34:            ? (AccessLevel)level
Services/AerationSterilize/AerationSterilize.API/Services/CurrentUser.cs:35:            : AccessLevel.None;
Services/AerationSterilize/AerationSterilize.Application/Common/ICurrentUser.cs:7:    Guid? DepartmentId { get; }
Services/AerationSterilize/AerationSterilize.Application/Common/ICurrentUser.cs:10:    AccessLevel GetAccessLevel(string functionCode, string commandCode);
Services/AerationSterilize/AerationSterilize.Domain/Entities/Identity/AppUser.cs:22:    public Guid? DepartmentId { get; set; }
Services/AerationSterilize/AerationSterilize.Domain/Entities/Identity/Permission.cs:12:    public AccessLevel AccessLevel { get; set; } = AccessLevel.All;
Services/AerationSterilize/AerationSterilize.Domain/Entities/Product.cs:10:    public Guid? CreatedBy { get; set; } = Guid.NewGuid();
Services/AerationSterilize/AerationSterilize.Domain/Entities/Product.cs:11:    public Guid? DepartmentId { get; set; } = Guid.NewGuid();
Services/AerationSterilize/AerationSterilize.Persistence/Services/PermissionService.cs:23:            select new { p.FunctionId, p.ActionId, p.AccessLevel })
Services/AerationSterilize/AerationSterilize.Persistence/Services/PermissionService.cs:30:                g.Max(x => x.AccessLevel)))
Services/CommonServices/CommonServices.API/Services/CurrentUser.cs:17:    public Guid? DepartmentId =>
Services/CommonServices/CommonServices.API/Services/CurrentUser.cs:18:        Guid.TryParse(_user.FindFirstValue("department_id"), out var id) ? id : null;
Services/CommonServices/CommonServices.API/Services/CurrentUser.cs:27:    public AccessLevel GetAccessLevel(string functionCode, string commandCode)
Services/CommonServices/CommonServices.API/Services/CurrentUser.cs:34:            ? (AccessLevel)level
Services/CommonServices/CommonServices.API/Services/CurrentUser.cs:35:            : AccessLevel.None;
Services/CommonServices/CommonServices.Application/Common/ICurrentUser.cs:7:    Guid? DepartmentId { get; }
Services/CommonServices/CommonServices.Application/Common/ICurrentUser.cs:10:    AccessLevel GetAccessLevel(string functionCode, string commandCode);
Services/CommonServices/CommonServices.Domain/Abstractions/MasterDataEntity.cs:20:    public string? CreatedBy { get; set; }
Services/CommonServices/CommonServices.Domain/Abstractions/MasterDataEntity.cs:36:        CreatedBy = userId;
Services/CommonServices/CommonServices.Persistence/Configurations/MasterData/MasterDataEntityConfiguration.cs:29:        builder.Property(x => x.CreatedBy).HasMaxLength(50);
BuildingBlocks/Contracts/Domains/Interfaces/IDataOwned.cs:4:    Guid? CreatedBy { get; set; }
BuildingBlocks/Contracts/Domains/Interfaces/IDataOwned.cs:5:    Guid? DepartmentId { get; set; }   // phòng của người tạo, lưu lúc tạo
BuildingBlocks/Contracts/Domains/Interfaces/IUserTracking.cs:5:    string? CreatedBy { get; set; }
BuildingBlocks/Shared/Common/Constants/Authorization/AccessLevel.cs:2:public enum AccessLevel
BuildingBlocks/Shared/DTOs/Identity/UserPermission.cs:4:public sealed record UserPermission(string Code, AccessLevel Level);
Bash Inspect data scope implementation and usage
IN
cat Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs BuildingBlocks/Contracts/Domains/Interfaces/IDataOwned.cs; grep -rn "ApplyDataScope\|IDataOwned" --include=*.cs Services | grep -v -E "/(bin|obj)/"; sed -n 1,40p Services/AerationSterilize/AerationSterilize.Domain/Entities/Identity/AppUser.cs; grep -rn "Permission(" Services/AerationSterilize/AerationSterilize.API/Controllers; git log --oneline -3 -- Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs

OUT
﻿using AerationSterilize.Application.Common;
using Contracts.Domains.Interfaces;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.API.DependencyInjection.Extensions;

public static class DataScopeExtensions
{
    public static IQueryable<T> ApplyDataScope<T>(
    this IQueryable<T> query, ICurrentUser currentUser, string functionCode, string commandCode)
    where T : IDataOwned
    {
        return currentUser.GetAccessLevel(functionCode, commandCode) switch
        {
            AccessLevel.All => query,
            AccessLevel.Department => query.Where(x => x.DepartmentId == currentUser.DepartmentId),
            AccessLevel.Own => query.Where(x => x.CreatedBy == currentUser.UserId),
            _ => query.Where(x => false)
        };
    }
}
﻿namespace Contracts.Domains.Interfaces;
public interface IDataOwned
{
    Guid? CreatedBy { get; set; }
    Guid? DepartmentId { get; set; }   // phòng của người tạo, lưu lúc tạo
}
Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs:9:    public static IQueryable<T> ApplyDataScope<T>(
Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs:11:    where T : IDataOwned
Services/AerationSterilize/AerationSterilize.Domain/Entities/Product.cs:5:public class Product : EntityAuditBase<Guid>, IDataOwned
﻿using Microsoft.AspNetCore.Identity;

namespace AerationSterilize.Domain.Entities.Identity;
public class AppUser : IdentityUser<Guid>
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string FullName { get; set; }

    public DateTime? DayOfBirth { get; set; }

    public bool? IsDirector { get; set; }

    public bool? IsHeadOfDepartment { get; set; }

    public Guid ManagerId { get; set; }

    public Guid PositionId { get; set; }

    public Guid? DepartmentId { get; set; }

    public int IsReceipient { get; set; }

    public virtual ICollection<IdentityUserClaim<Guid>> Claims { get; set; }

    public virtual ICollection<IdentityUserLogin<Guid>> Logins { get; set; }

    public virtual ICollection<IdentityUserToken<Guid>> Tokens { get; set; }

    public virtual ICollection<IdentityUserRole<Guid>> UserRoles { get; set; }

}
Services/AerationSterilize/AerationSterilize.API/Controllers/V1/ProductsController.cs:28:    [Permission(FunctionCode.PRODUCT, CommandCode.VIEW)]
Services/AerationSterilize/AerationSterilize.API/Controllers/V1/ProductsController.cs:46:    [Permission(FunctionCode.PRODUCT, CommandCode.VIEW)]
571a284 feat: implement data ownership and permission data check
Bash Check product endpoints and refresh token permissions
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize && grep -n "Http\|Permission\|Authorize\|public async" AerationSterilize.API/Controllers/V1/ProductsController.cs; grep -n "Permission\|TokenRequest\|GetToken" AerationSterilize.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs; grep -rn "CreatedBy\|DepartmentId" --include=*.cs AerationSterilize.Application AerationSterilize.Persistence/ApplicationDbContext.cs ../../BuildingBlocks/Infrastructure/Common | grep -v Migrations | head

OUT
25:    [HttpGet(Name = "GetProducts")]
28:    [Permission(FunctionCode.PRODUCT, CommandCode.VIEW)]
29:    public async Task<IActionResult> GetProducts(string? searchTerm = null,
43:    [HttpGet("{productId}")]
46:    [Permission(FunctionCode.PRODUCT, CommandCode.VIEW)]
47:    public async Task<IActionResult> GetProductById(Guid productId)
53:    [HttpPost(Name = "CreateProducts")]
56:    public async Task<IActionResult> CreateProducts([FromBody] CreateProductCommand command)
63:    [HttpDelete("{productId}")]
66:    public async Task<IActionResult> DeleteProduct(Guid productId)
72:    [HttpPut("{productId}")]
75:    public async Task<IActionResult> UpdateProductById(Guid productId, [FromBody] UpdateProductCommand command)
17:    private readonly IPermissionService _permissionService;
22:       IPermissionService permissionService)
43:        var permissions = await _permissionService.GetPermissionsAsync(user.Id, cancellationToken);
45:        var tokenRequest = new TokenRequest(user.Id, user.UserName!, roles, permissions);
47:        var token = _tokenService.GetToken(tokenRequest);
AerationSterilize.Application/Common/ICurrentUser.cs:7:    Guid? DepartmentId { get; }
Bash Check base controller authorization
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services && grep -n "Authorize\|\[" AerationSterilize/AerationSterilize.API/Abstractions/ApiController.cs | head; grep -rn "FallbackPolicy" --include=*.cs .. | grep -v obj

OUT
8:[ApiController]
9:[Route("api/v{version:apiVersion}/[controller]")]
28:    private static ProblemDetails CreateProblemDetails(string title, int status, Error error, Error[]? errors = null)
9:13 PM
Mình đã kiểm tra lại. Aeration có đủ khung cho AccessLevel, nhưng phần lọc dữ liệu theo cấp chưa chạy được, và có một lỗ hổng nặng hơn: 3 API sửa Product đang mở cho cả người chưa đăng nhập.

AccessLevel là gì
Hệ thống phân quyền có 2 tầng:

Quyền chức năng: "có được gọi API này không". Ví dụ PRODUCT.VIEW. Tầng này do [Permission(...)] kiểm tra; không có quyền thì trả 403.
AccessLevel (quyền theo dữ liệu): đã được gọi API rồi thì "được thấy những bản ghi nào".
All: thấy tất cả.
Department: chỉ thấy bản ghi của phòng mình (DepartmentId).
Own: chỉ thấy bản ghi do mình tạo (CreatedBy).
None: không thấy gì.
Cách chạy: bảng Permissions (Role + Function + Action) có cột AccessLevel. Khi login, Aeration lấy mức cao nhất của user rồi ghi vào token dưới dạng claim, ví dụ permission_level = "PRODUCT.VIEW:2". CurrentUser.GetAccessLevel() đọc claim này. ApplyDataScope() dùng kết quả đó để thêm điều kiện WHERE vào câu query.

Ở CommonServices: chỉ có hàm GetAccessLevel trong CurrentUser và không chỗ nào gọi đến. Master data (ngân hàng, tỉnh thành, tiền tệ…) là danh mục dùng chung, không thuộc về phòng hay người nào, nên không cần lọc theo AccessLevel. Ở đây chỉ cần [Permission], và tầng này đã đủ: mọi API trong các controller master data đều đã có attribute.

Aeration: có khung, nhưng chưa hoạt động đúng
Đã có:

Cột Permissions.AccessLevel.
PermissionService lấy mức cao nhất khi user có nhiều role.
Token có claim permission_level.
GetAccessLevel và ApplyDataScope.
Refresh token có lấy lại quyền mới.
Lỗi tìm thấy:

#	Vấn đề	Hậu quả
1	3 API CreateProducts, UpdateProductById, DeleteProduct không có [Permission], và controller gốc cũng không có [Authorize]	Ai cũng tạo/sửa/xoá Product được, kể cả không đăng nhập
2	Token không có claim department_id: TokenService chỉ ghi sub, unique_name, jti, role và permission	CurrentUser.DepartmentId luôn null, nên mức Department lọc sai: chỉ ra những bản ghi không có DepartmentId
3	ApplyDataScope không được gọi ở đâu cả	Query Product không lọc gì, mọi user đều thấy hết như All
4	Product.CreatedBy và DepartmentId mặc định là Guid.NewGuid() (giá trị ngẫu nhiên) và lúc tạo không gán theo user hiện tại	Kể cả khi đã lọc, mức Own và Department cũng không khớp được bản ghi nào
5	PermissionAuthorizationHandler chỉ xem có claim hay không, bỏ qua mức	Quyền đặt AccessLevel = None vẫn qua được [Permission]
6	ApplyDataScope nằm ở tầng API, trong khi query nằm ở Application handler	Handler không gọi được hàm này; cần chuyển xuống Application hoặc BuildingBlocks
Ngoài ra cần biết: quyền nằm sẵn trong token (hạn 60 phút). Đổi quyền của role thì user phải login lại hoặc refresh token mới có tác dụng.

Đề xuất sửa
Thêm [Permission(PRODUCT, CREATE / UPDATE / DELETE)] cho 3 API Product. Việc này gấp nhất.
Ghi department_id vào token khi login và khi refresh token.
Chuyển ApplyDataScope xuống Application hoặc BuildingBlocks, rồi dùng trong GetProducts và GetProductById.
Khi tạo Product, gán CreatedBy và DepartmentId theo user hiện tại, và bỏ giá trị mặc định Guid.NewGuid().
Handler chặn quyền có mức None.
Bạn muốn mình sửa hết 5 mục này luôn không?



hãy sửa đi
Bash Read product controller, entity, features, refresh handler
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src && cat Services/AerationSterilize/AerationSterilize.API/Controllers/V1/ProductsController.cs Services/AerationSterilize/AerationSterilize.Domain/Entities/Product.cs; ls -R Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products | head -40; cat Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs

OUT
﻿using AerationSterilize.API.Abstractions;
using Infrastructure.Authorization;
using AerationSterilize.Application.Features.V1.Products.Commands.CreateProduct;
using AerationSterilize.Application.Features.V1.Products.Commands.DeleteProduct;
using AerationSterilize.Application.Features.V1.Products.Commands.UpdateProduct;
using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Queries.GetProductById;
using AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
using Asp.Versioning;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Common.Constants.Authorization;
using Shared.Emumerations;

namespace AerationSterilize.API.Controllers.V1;

[ApiVersion(1)]
public class ProductsController : ApiController
{
    public ProductsController(ISender sender) : base(sender)
    {
    }

    [HttpGet(Name = "GetProducts")]
    [ProducesResponseType(type: typeof(Result<IEnumerable<ProductDto>>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.VIEW)]
    public async Task<IActionResult> GetProducts(string? searchTerm = null,
       string? sortColumn = null,
       string? sortOrder = null,
       int pageIndex = 1,
       int pageSize = 10)
    {
        var result = await Sender.Send(new GetProductsQuery(searchTerm, sortColumn,
            SortOrderExtension.ConvertStringToSortOrder(sortOrder),
            pageIndex,
            pageSize));

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpGet("{productId}")]
    [ProducesResponseType(type: typeof(Result<ProductDto>), statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.VIEW)]
    public async Task<IActionResult> GetProductById(Guid productId)
    {
        var result = await Sender.Send(new GetProductByIdQuery(productId));
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPost(Name = "CreateProducts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateProducts([FromBody] CreateProductCommand command)
    {
        var result = await Sender.Send(command);

        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpDelete("{productId}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(Guid productId)
    {
        var result = await Sender.Send(new DeleteProductCommand(productId));
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }

    [HttpPut("{productId}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProductById(Guid productId, [FromBody] UpdateProductCommand command)
    {
        command.SetId(productId);
        var result = await Sender.Send(command);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
}
﻿using Contracts.Abstractions.Entities.Domains;
using Contracts.Domains.Interfaces;

namespace AerationSterilize.Domain.Entities;
public class Product : EntityAuditBase<Guid>, IDataOwned
{
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public string Description { get; private set; }
    public Guid? CreatedBy { get; set; } = Guid.NewGuid();
    public Guid? DepartmentId { get; set; } = Guid.NewGuid();

    private Product() { }

    public Product(Guid id, string name, decimal price, string description)
    {
        Id = id;
        Name = name;
        Price = price;
        Description = description;

    }
}
Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products:
Commands
Common
Queries

Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands:
CreateProduct
DeleteProduct
UpdateProduct

Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/CreateProduct:
CreateProductCommand.cs
CreateProductCommandHandler.cs
CreateProductCommandValidator.cs

Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/DeleteProduct:
DeleteProductCommand.cs
DeleteProductCommandHandler.cs

Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/UpdateProduct:
UpdateProductCommand.cs
UpdateProductCommandHandler.cs
UpdateProductCommandValidator.cs

Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Common:
Caching
Dtos
Extensions
Mapping

Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Common/Caching:
ProductCacheKeys.cs

Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Common/Dtos:
ProductDto.cs

Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Common/Extensions:
ProductExtension.cs

Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Common/Mapping:
﻿using AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
using static AerationSterilize.Domain.Exceptions.AuthException;
using AerationSterilize.Domain.Entities.Identity;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Identity;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Shared.DTOs.Identity;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.RefreshToken;
public class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, LoginDto>
{
    private readonly IRepositoryBase<UserSession, int> _userSessionRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IPermissionService _permissionService;

    public RefreshTokenCommandHandler(IRepositoryBase<UserSession, int> userSessionRepository,
       ITokenService tokenService,
       UserManager<AppUser> userManager,
       IPermissionService permissionService)
    {
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    public async Task<Result<LoginDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenEntity = await _userSessionRepository.FindSingleAsync(us => us.RefreshToken == request.RefreshToken, cancellationToken);
        if (tokenEntity is null || tokenEntity.IsRevoked || tokenEntity.ExpiredAt <= DateTime.UtcNow)
            throw new InvalidRefreshTokenException();

        var user = await _userManager.FindByIdAsync(tokenEntity.UserId.ToString())
            ?? throw new InvalidRefreshTokenException();

        // revoke token cũ
        tokenEntity.IsRevoked = true;

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await _permissionService.GetPermissionsAsync(user.Id, cancellationToken);

        var tokenRequest = new TokenRequest(user.Id, user.UserName!, roles, permissions);

        var token = _tokenService.GetToken(tokenRequest);

        var userSession = new UserSession
        {
            UserId = user.Id,
            RefreshToken = token.RefreshToken,
            ExpiredAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn),
        };
        _userSessionRepository.Add(userSession);

        var result = new LoginDto(
           token.AccessToken,
           token.RefreshToken,
           token.ExpiresIn
        );

        return Result.Success(result);
    }
}
Bash Read product handlers and queries
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize/AerationSterilize.Application && for f in Features/V1/Products/Commands/*/*Handler.cs Features/V1/Products/Queries/*/*.cs Features/V1/Products/Common/Caching/ProductCacheKeys.cs Common/ICurrentUser.cs; do echo "===== $f"; cat $f; done; grep -rn "class ProductConfiguration" -A25 ../AerationSterilize.Persistence/Configurations | grep -n "CreatedBy\|DepartmentId"; grep -rln "IQueryCachable\|ICacheable" ../../../BuildingBlocks --include=*.cs | grep -v obj

OUT
===== Features/V1/Products/Commands/CreateProduct/CreateProductCommandHandler.cs
﻿using AerationSterilize.Domain.Entities;
using AutoMapper;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Products.Commands.CreateProduct;
internal sealed class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;
    private readonly IMapper _mapper;

    public CreateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }
    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var productEntity = _mapper.Map<Product>(request);

        _productRepository.Add(productEntity);

        return Result.Success(productEntity.Id);
    }
}

===== Features/V1/Products/Commands/DeleteProduct/DeleteProductCommandHandler.cs
﻿using AerationSterilize.Domain.Entities;
using AutoMapper;
using static AerationSterilize.Domain.Exceptions.ProductException;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Products.Commands.DeleteProduct;
internal sealed class DeleteProductCommandHandler : ICommandHandler<DeleteProductCommand>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;

    public DeleteProductCommandHandler(IRepositoryBase<Product, Guid> productRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new ProductNotFoundException(request.Id);

        _productRepository.Remove(productEntity);

        return Result.Success();
    }
}

===== Features/V1/Products/Commands/UpdateProduct/UpdateProductCommandHandler.cs
﻿using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Domain.Entities;
using static AerationSterilize.Domain.Exceptions.ProductException;
using AutoMapper;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Products.Commands.UpdateProduct;

internal sealed class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand, ProductDto>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;
    private readonly IMapper _mapper;

    public UpdateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new ProductNotFoundException(request.Id);

        productEntity = _mapper.Map(request, productEntity);

        _productRepository.Update(productEntity);

        var result = _mapper.Map<ProductDto>(productEntity);

        return Result.Success(result);
    }
}
===== Features/V1/Products/Queries/GetProductById/GetProductByIdQuery.cs
using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProductById;
public sealed record GetProductByIdQuery(Guid Id) : ICachedQuery<ProductDto>
{
    string ICachedQuery.CacheKey => ProductCacheKeys.ById(Id);

    TimeSpan? ICachedQuery.Expiration => null;
}
===== Features/V1/Products/Queries/GetProductById/GetProductByIdQueryHandler.cs
﻿using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Domain.Entities;
using static AerationSterilize.Domain.Exceptions.ProductException;
using AutoMapper;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProductById;
public class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDto>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;
    private readonly IMapper _mapper;

    public GetProductByIdQueryHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new ProductNotFoundException(request.Id);

        var result = _mapper.Map<ProductDto>(product);

        return Result.Success(result);
    }
}
===== Features/V1/Products/Queries/GetProducts/GetProductsQuery.cs
using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;
using Shared.Emumerations;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
public sealed record GetProductsQuery(
    string? searchTerm,
    string? sortColumn,
    SortOrder? SortOrder,
    int PageIndex, int PageSize)
     : ICachedQuery<PagedResult<ProductDto>>
{
    string ICachedQuery.CacheKey => ProductCacheKeys.List(searchTerm, sortColumn, SortOrder?.Name, PageIndex, PageSize);

    TimeSpan? ICachedQuery.Expiration => TimeSpan.FromMinutes(5);
}
===== Features/V1/Products/Queries/GetProducts/GetProductsQueryHandler.cs
﻿using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Common.Extensions;
using AerationSterilize.Domain.Entities;
using AutoMapper;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Shared.Emumerations;
using Shared.Paging;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
public class GetProductsQueryHandler : IQueryHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    private readonly IRepositoryBase<Product, Guid> _productRepository;
    private readonly IMapper _mapper;

    public GetProductsQueryHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<PagedResult<ProductDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var productsQuery = string.IsNullOrWhiteSpace(request.searchTerm)
          ? _productRepository.FindAll()
          : _productRepository.FindAll(x => x.Name.Contains(request.searchTerm) || x.Description.Contains(request.searchTerm));

        var sortExpression = ProductExtension.GetSortExpression(request.sortColumn);

        productsQuery = request.SortOrder == SortOrder.Descending
          ? productsQuery.OrderByDescending(sortExpression)
          : productsQuery.OrderBy(sortExpression);

        var pagedResult = await PagingExtensions.ToPagedResultAsync(
                        productsQuery,
                        request.PageIndex,
                        request.PageSize);

        var result = _mapper.Map<PagedResult<ProductDto>>(pagedResult);

        return Result.Success(result);
    }
}
===== Features/V1/Products/Queries/GetProducts/GetProductsQueryValidator.cs
﻿using FluentValidation;

namespace AerationSterilize.Application.Features.V1.Products.Queries.GetProducts;
public class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(x => x.sortColumn)
           .NotEmpty()
           .WithMessage("Sort column is required.");
    }
}
===== Features/V1/Products/Common/Caching/ProductCacheKeys.cs
namespace AerationSterilize.Application.Features.V1.Products.Common.Caching;
public static class ProductCacheKeys
{
    public const string Prefix = "product:";

    public static string ById(Guid id) => $"{Prefix}{id}";

    public static string List(string? searchTerm, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}list:{searchTerm?.Trim().ToLowerInvariant()}:{sortColumn?.ToLowerInvariant()}:{sortOrder}:{pageIndex}:{pageSize}";
}
===== Common/ICurrentUser.cs
﻿using Shared.Common.Constants.Authorization;

namespace AerationSterilize.Application.Common;
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid? DepartmentId { get; }
    string? TokenId { get; }
    DateTimeOffset? TokenExpiresAt { get; }
    AccessLevel GetAccessLevel(string functionCode, string commandCode);
}
Bash Read caching behaviors and command definitions
IN
cat BuildingBlocks/Infrastructure/Behaviors/CacheInvalidationPipelineBehavior.cs BuildingBlocks/Infrastructure/Behaviors/QueryCachingPipelineBehavior.cs; find BuildingBlocks/Contracts -name "ICachedQuery*" -o -name "ICacheInvalidat*" -o -name "IRepositoryBase*" | grep -v obj | xargs cat; cat Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/*/*Command.cs; cat Services/AerationSterilize/AerationSterilize.Persistence/Configurations/ProductConfiguration.cs 2>/dev/null || ls Services/AerationSterilize/AerationSterilize.Persistence/Configurations

OUT
using Contracts.Caching;
using Contracts.Responses;
using MediatR;

namespace Infrastructure.Behaviors;

// Registered outside TransactionPipelineBehavior so the cache is cleared only after the transaction has committed.
public sealed class CacheInvalidationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheInvalidator
    where TResponse : Result
{
    private readonly ICacheService _cacheService;

    public CacheInvalidationPipelineBehavior(ICacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        if (response.IsSuccess)
        {
            foreach (var prefix in request.CacheKeyPrefixes)
                await _cacheService.RemoveByPrefixAsync(prefix, cancellationToken);
        }

        return response;
    }
}
using System.Reflection;
using Contracts.Caching;
using Contracts.Responses;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Behaviors;

// Caches the Value of a successful Result<T> for queries implementing ICachedQuery.
// Result<T> cannot be deserialized directly, so only the value is stored and the Result is rebuilt on a hit.
public sealed class QueryCachingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICachedQuery
    where TResponse : Result
{
    private static readonly MethodInfo HandleCachedMethod =
        typeof(QueryCachingPipelineBehavior<TRequest, TResponse>)
            .GetMethod(nameof(HandleCachedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ICacheService _cacheService;
    private readonly ILogger<QueryCachingPipelineBehavior<TRequest, TResponse>> _logger;

    public QueryCachingPipelineBehavior(ICacheService cacheService, ILogger<QueryCachingPipelineBehavior<TRequest, TResponse>> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Result<>))
            return next(cancellationToken);

        var valueType = responseType.GetGenericArguments()[0];
        return (Task<TResponse>)HandleCachedMethod
            .MakeGenericMethod(valueType)
            .Invoke(this, [request, next, cancellationToken])!;
    }

    private async Task<TResponse> HandleCachedAsync<TValue>(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var cached = await _cacheService.GetAsync<TValue>(request.CacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for {CacheKey}", request.CacheKey);
            return (TResponse)(object)Result.Success(cached);
        }

        _logger.LogDebug("Cache miss for {CacheKey}", request.CacheKey);
        var response = await next(cancellationToken);

        // A null value (e.g. "not found") is not cached, otherwise it would hide a record created later.
        if (response is Result<TValue> { IsSuccess: true, Value: not null } result)
            await _cacheService.SetAsync(request.CacheKey, result.Value, request.Expiration, cancellationToken);

        return response;
    }
}
using Contracts.Common.Messages;

namespace Contracts.Caching;
public interface ICachedQuery
{
    string CacheKey { get; }

    // null => RedisOptions.DefaultExpirationMinutes
    TimeSpan? Expiration { get; }
}

public interface ICachedQuery<TResponse> : IQuery<TResponse>, ICachedQuery;
namespace Contracts.Caching;

// Commands implementing this clear every cache key starting with one of the prefixes after they succeed.
public interface ICacheInvalidator
{
    IEnumerable<string> CacheKeyPrefixes { get; }
}
﻿using System.Linq.Expressions;

namespace Contracts.Common.Repositories;
public interface IRepositoryBase<T, in K> where T : class
{
    IQueryable<T> FindAll(Expression<Func<T, bool>>? predicate = null,
        bool tracking = false,
        params Expression<Func<T, object>>[] includeProperties);

    Task<T> FindByIdAsync(K id,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[] includeProperties);

    Task<T> FindSingleAsync(Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[] includeProperties);

    void Add(T entity);

    void Update(T entity);

    void Remove(T entity);

    void RemoveMultiple(IEnumerable<T> entities);
}
using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Products.Commands.CreateProduct;
public sealed record CreateProductCommand(
    string Name,
    decimal Price,
    string Description
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [ProductCacheKeys.Prefix];
}
using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Products.Commands.DeleteProduct;
public sealed record DeleteProductCommand(Guid Id) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [ProductCacheKeys.Prefix];
}
using AerationSterilize.Application.Features.V1.Products.Common.Dtos;
using AerationSterilize.Application.Features.V1.Products.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Products.Commands.UpdateProduct;
public sealed class UpdateProductCommand : ICommand<ProductDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }

    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [ProductCacheKeys.Prefix];

    public void SetId(Guid id)
    {
        Id = id;
    }
}
﻿using AerationSterilize.Domain.Entities;
using AerationSterilize.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AerationSterilize.Persistence.Configurations;
internal class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(TableNames.Product);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired(true);
        builder.Property(x => x.Description)
            .HasMaxLength(250).IsRequired(true);
    }
}
Bash Check project references and mapping
IN
grep -n "Include" BuildingBlocks/Contracts/Contracts.csproj BuildingBlocks/Shared/Shared.csproj; grep -rn "Product(" Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Common/Mapping/*.cs; ls BuildingBlocks/Contracts BuildingBlocks/Contracts/Identity

OUT
BuildingBlocks/Contracts/Contracts.csproj:10:		<PackageReference Include="AutoMapper" />
BuildingBlocks/Contracts/Contracts.csproj:11:		<PackageReference Include="MediatR" />
BuildingBlocks/Contracts/Contracts.csproj:12:		<PackageReference Include="Microsoft.EntityFrameworkCore" />
BuildingBlocks/Contracts/Contracts.csproj:16:    <ProjectReference Include="..\Common.Logging\Common.Logging.csproj" />
BuildingBlocks/Contracts/Contracts.csproj:17:    <ProjectReference Include="..\Shared\Shared.csproj" />
BuildingBlocks/Shared/Shared.csproj:10:    <PackageReference Include="Microsoft.AspNetCore.Http.Features" />
BuildingBlocks/Shared/Shared.csproj:11:	  <PackageReference Include="Ardalis.SmartEnum" />
BuildingBlocks/Shared/Shared.csproj:12:	  <PackageReference Include="Microsoft.EntityFrameworkCore" />
BuildingBlocks/Shared/Shared.csproj:16:    <Folder Include="DTOs\Email\templates\" />
BuildingBlocks/Shared/Shared.csproj:17:    <Folder Include="Services\" />
BuildingBlocks/Contracts:
AssemblyReference.cs
BackgroundJobs
Caching
Common
Contracts.csproj
Domains
Exceptions
Identity
Mapping
Responses
Services
bin
obj

BuildingBlocks/Contracts/Identity:
IPermissionService.cs
ITokenRevocationService.cs
ITokenService.cs
9:16 PM
Bắt đầu sửa. Đầu tiên là phần dùng chung: DataScope + claim department_id.



Write C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Contracts\Identity\DataScope.cs
45 lines
using Contracts.Domains.Interfaces;
using Shared.Common.Constants.Authorization;

namespace Contracts.Identity;

// The rows of an IDataOwned entity the current user may access for one permission (FunctionCode + CommandCode).
public sealed record DataScope(AccessLevel Level, Guid UserId, Guid? DepartmentId)
{
    // Added to the cache key of scoped queries, so users with a different scope never share a cached result.
    public string CacheKey => Level switch
    {
        AccessLevel.All => "all",
        AccessLevel.Department => $"dept:{DepartmentId}",
        AccessLevel.Own => $"own:{UserId}",
        _ => "none"
    };

    public bool Covers(IDataOwned entity) => Level switch
    {
        AccessLevel.All => true,
        AccessLevel.Department => DepartmentId is not null && entity.DepartmentId == DepartmentId,
        AccessLevel.Own => entity.CreatedBy == UserId,
        _ => false
    };
}

public static class DataScopeExtensions
{
    public static IQueryable<T> ApplyDataScope<T>(this IQueryable<T> query, DataScope scope)
        where T : IDataOwned
    {
        var userId = scope.UserId;
        var departmentId = scope.DepartmentId;

        return scope.Level switch
        {
            AccessLevel.All => query,
            // A user without a department sees nothing, instead of every row whose DepartmentId is null.
            AccessLevel.Department when departmentId is not null => query.Where(x => x.DepartmentId == departmentId),
            AccessLevel.Own => query.Where(x => x.CreatedBy == userId),
            _ => query.Where(x => false)
        };
    }
}
Edit C:\Users\DuyQuang\Desktop\SourceCodeBase\src\BuildingBlocks\Shared\Common\Constants\Authorization\FunctionCode.cs
Added 5 lines
Bash Read handler and TokenRequest usages
IN
cat BuildingBlocks/Infrastructure/Authorization/PermissionAuthorizationHandler.cs; grep -rn "new TokenRequest" --include=*.cs . | grep -v obj

OUT
﻿using Microsoft.AspNetCore.Authorization;
using Shared.Common.Constants.Authorization;

namespace Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(PermissionClaim.Type, requirement.Value))
            context.Succeed(requirement);

        // Không Succeed => ASP.NET tự trả 403 (đã login) hoặc 401 (chưa login)
        return Task.CompletedTask;
    }
}
./Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Login/LoginCommandHandler.cs:80:        var tokenRequest = new TokenRequest(user.Id, user.UserName!, roleNames, permissions);
./Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs:45:        var tokenRequest = new TokenRequest(user.Id, user.UserName!, roles, permissions);
Bash Add department claim to token and check AccessLevel in handler
IN
python - <<'EOF'
def rw(p,pairs):
    s=open(p,encoding='utf-8-sig').read()
    crlf='\r\n' in s; s=s.replace('\r\n','\n')
    for a,b in pairs:
        assert a in s,(p,a); s=s.replace(a,b,1)
    if crlf: s=s.replace('\n','\r\n')
    open(p,'w',encoding='utf-8-sig').write(s)

rw('BuildingBlocks/Infrastructure/Authorization/PermissionAuthorizationHandler.cs',[(
'''        if (context.User.HasClaim(PermissionClaim.Type, requirement.Value))
            context.Succeed(requirement);
''','''        // A permission granted with AccessLevel.None gives no access to any data, so it does not open the endpoint either.
        var levelPrefix = requirement.Value + ":";
        var hasAccess = context.User.FindAll(PermissionClaim.LevelType)
            .Any(c => c.Value.StartsWith(levelPrefix, StringComparison.Ordinal)
                      && int.TryParse(c.Value[levelPrefix.Length..], out var level)
                      && level > (int)AccessLevel.None);

        if (context.User.HasClaim(PermissionClaim.Type, requirement.Value) && hasAccess)
            context.Succeed(requirement);
''')])

rw('BuildingBlocks/Shared/DTOs/Identity/TokenRequest.cs',[(
'''    string UserName,
''','''    string UserName,
    Guid? DepartmentId,
''')])

rw('BuildingBlocks/Infrastructure/Identity/TokenService.cs',[(
'''        claims.AddRange(request.Roles''','''        if (request.DepartmentId is { } departmentId)
            claims.Add(new Claim(UserClaim.DepartmentId, departmentId.ToString()));

        claims.AddRange(request.Roles''')])

for f in ['Login/LoginCommandHandler.cs','RefreshToken/RefreshTokenCommandHandler.cs']:
    p='Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/'+f
    s=open(p,encoding='utf-8-sig').read()
    old='new TokenRequest(user.Id, user.UserName!, '
    assert old in s
    open(p,'w',encoding='utf-8-sig').write(s.replace(old,'new TokenRequest(user.Id, user.UserName!, user.DepartmentId, '))

# CurrentUser (both services) use the shared claim name
for svc in ['AerationSterilize','CommonServices']:
    rw(f'Services/{svc}/{svc}.API/Services/CurrentUser.cs',[('_user.FindFirstValue("department_id")','_user.FindFirstValue(UserClaim.DepartmentId)')])
EOF
cat Services/AerationSterilize/AerationSterilize.API/Services/CurrentUser.cs | head -12

OUT
﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AerationSterilize.Application.Common;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.API.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _user;

    public CurrentUser(IHttpContextAccessor accessor)
9:16 PM
Giờ ICurrentUser.GetDataScope + bỏ DataScopeExtensions ở tầng API:



Bash Add GetDataScope to Aeration current user
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize && git rm -q AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs && python - <<'EOF'
def rw(p,pairs):
    s=open(p,encoding='utf-8-sig').read()
    crlf='\r\n' in s; s=s.replace('\r\n','\n')
    for a,b in pairs:
        assert a in s,(p,a); s=s.replace(a,b,1)
    if crlf: s=s.replace('\n','\r\n')
    open(p,'w',encoding='utf-8-sig').write(s)

rw('AerationSterilize.Application/Common/ICurrentUser.cs',[
('using Shared.Common.Constants.Authorization;\n','using Contracts.Identity;\nusing Shared.Common.Constants.Authorization;\n'),
('    AccessLevel GetAccessLevel(string functionCode, string commandCode);\n','''    AccessLevel GetAccessLevel(string functionCode, string commandCode);
    DataScope GetDataScope(string functionCode, string commandCode);
''')])

rw('AerationSterilize.API/Services/CurrentUser.cs',[
('using AerationSterilize.Application.Common;\n','using AerationSterilize.Application.Common;\nusing Contracts.Identity;\n'),
('''            : AccessLevel.None;
    }
''','''            : AccessLevel.None;
    }

    public DataScope GetDataScope(string functionCode, string commandCode)
        => new(GetAccessLevel(functionCode, commandCode), UserId, DepartmentId);
''')])
EOF
tail -12 AerationSterilize.API/Services/CurrentUser.cs

OUT
        var prefix = PermissionClaim.Build(functionCode, commandCode) + ":";
        var claim = _user.FindAll(PermissionClaim.LevelType)
                         .FirstOrDefault(c => c.Value.StartsWith(prefix));

        return claim is not null && int.TryParse(claim.Value[prefix.Length..], out var level)
            ? (AccessLevel)level
            : AccessLevel.None;
    }

    public DataScope GetDataScope(string functionCode, string commandCode)
        => new(GetAccessLevel(functionCode, commandCode), UserId, DepartmentId);
}
9:16 PM
Giờ sửa Product: entity, controller, query/command handlers.



Bash Apply data scope and permissions to Product feature
IN
python - <<'EOF'
def rw(p,pairs):
    s=open(p,encoding='utf-8-sig').read()
    crlf='\r\n' in s; s=s.replace('\r\n','\n')
    for a,b in pairs:
        assert a in s,(p,a); s=s.replace(a,b,1)
    if crlf: s=s.replace('\n','\r\n')
    open(p,'w',encoding='utf-8-sig').write(s)
F='AerationSterilize.Application/Features/V1/Products/'

rw('AerationSterilize.Domain/Entities/Product.cs',[(
'''    public Guid? CreatedBy { get; set; } = Guid.NewGuid();
    public Guid? DepartmentId { get; set; } = Guid.NewGuid();
''','''    // Owner of the row, used by the data scope (AccessLevel.Own / AccessLevel.Department). Set on create.
    public Guid? CreatedBy { get; set; }
    public Guid? DepartmentId { get; set; }
''')])

rw(F+'Common/Caching/ProductCacheKeys.cs',[
('''    public static string ById(Guid id) => $"{Prefix}{id}";''','''    // scope = DataScope.CacheKey: the same product/page is cached separately per data scope.
    public static string ById(Guid id, string scope) => $"{Prefix}{scope}:{id}";'''),
('''    public static string List(string? searchTerm, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}list:{searchTerm''','''    public static string List(string scope, string? searchTerm, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}{scope}:list:{searchTerm''')])

rw(F+'Queries/GetProductById/GetProductByIdQuery.cs',[
('using Contracts.Caching;\n','using Contracts.Caching;\nusing Contracts.Identity;\n'),
('public sealed record GetProductByIdQuery(Guid Id) : ICachedQuery<ProductDto>','public sealed record GetProductByIdQuery(Guid Id, DataScope Scope) : ICachedQuery<ProductDto>'),
('ProductCacheKeys.ById(Id)','ProductCacheKeys.ById(Id, Scope.CacheKey)')])

rw(F+'Queries/GetProductById/GetProductByIdQueryHandler.cs',[
('''        var product = await _productRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new ProductNotFoundException(request.Id);
''','''        var product = await _productRepository.FindByIdAsync(request.Id, cancellationToken);

        // Outside the user's data scope is reported as not found, so the product's existence is not revealed.
        if (product is null || !request.Scope.Covers(product))
            throw new ProductNotFoundException(request.Id);
''')])

rw(F+'Queries/GetProducts/GetProductsQuery.cs',[
('using Contracts.Caching;\n','using Contracts.Caching;\nusing Contracts.Identity;\n'),
('''    int PageIndex, int PageSize)''','''    int PageIndex, int PageSize,
    DataScope Scope)'''),
('ProductCacheKeys.List(searchTerm,','ProductCacheKeys.List(Scope.CacheKey, searchTerm,')])

rw(F+'Queries/GetProducts/GetProductsQueryHandler.cs',[
('using Contracts.Common.Repositories;\n','using Contracts.Common.Repositories;\nusing Contracts.Identity;\n'),
('''        var sortExpression''','''        productsQuery = productsQuery.ApplyDataScope(request.Scope);

        var sortExpression''')])

rw(F+'Commands/CreateProduct/CreateProductCommandHandler.cs',[
('using AerationSterilize.Domain.Entities;\n','using AerationSterilize.Application.Common;\nusing AerationSterilize.Domain.Entities;\n'),
('''    private readonly IMapper _mapper;

    public CreateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper)
    {
''','''    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;

    public CreateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper, ICurrentUser currentUser)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
'''),
('''        var productEntity = _mapper.Map<Product>(request);
''','''        var productEntity = _mapper.Map<Product>(request);
        productEntity.CreatedBy = _currentUser.UserId;
        productEntity.DepartmentId = _currentUser.DepartmentId;
''')])

for cmd,code in [('UpdateProduct','UPDATE'),('DeleteProduct','DELETE')]:
    p=F+f'Commands/{cmd}/{cmd}CommandHandler.cs'
    s=open(p,encoding='utf-8-sig').read()
    crlf='\r\n' in s; s=s.replace('\r\n','\n')
    s=s.replace('using AerationSterilize.Domain.Entities;\n','using AerationSterilize.Domain.Entities;\nusing AerationSterilize.Application.Common;\n',1)
    s=s.replace('using Contracts.Responses;\n','using Contracts.Responses;\nusing Shared.Common.Constants.Authorization;\n',1)
    s=s.replace('    private readonly IRepositoryBase<Product, Guid> _productRepository;\n','    private readonly IRepositoryBase<Product, Guid> _productRepository;\n    private readonly ICurrentUser _currentUser;\n',1)
    import re
    s=re.sub(r'(public '+cmd+r'CommandHandler\(IRepositoryBase<Product, Guid> productRepository)(, IMapper mapper)?\)\n    \{\n',
             lambda m: m.group(1)+(m.group(2) or '')+', ICurrentUser currentUser)\n    {\n        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));\n', s)
    old='''        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new ProductNotFoundException(request.Id);
'''
    new=f'''        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken);

        // Outside the user's data scope is reported as not found, so the product's existence is not revealed.
        var scope = _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.{code});
        if (productEntity is null || !scope.Covers(productEntity))
            throw new ProductNotFoundException(request.Id);
'''
    assert old in s and '_currentUser = currentUser' in s, p
    s=s.replace(old,new,1)
    if crlf: s=s.replace('\n','\r\n')
    open(p,'w',encoding='utf-8-sig').write(s)

rw('AerationSterilize.API/Controllers/V1/ProductsController.cs',[
('using AerationSterilize.API.Abstractions;\n','using AerationSterilize.API.Abstractions;\nusing AerationSterilize.Application.Common;\n'),
('''    public ProductsController(ISender sender) : base(sender)
    {
    }
''','''    private readonly ICurrentUser _currentUser;

    public ProductsController(ISender sender, ICurrentUser currentUser) : base(sender)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }
'''),
('''            pageIndex,
            pageSize));''','''            pageIndex,
            pageSize,
            _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.VIEW)));'''),
('new GetProductByIdQuery(productId)','new GetProductByIdQuery(productId, _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.VIEW))'),
('''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateProducts''','''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.CREATE)]
    public async Task<IActionResult> CreateProducts'''),
('''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct''','''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteProduct'''),
('''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProductById''','''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateProductById''')])
EOF
git diff AerationSterilize.Application/Features/V1/Products/Commands/DeleteProduct/

OUT
Exit code 2
/usr/bin/bash: -c: line 115: unexpected EOF while looking for matching `''
9:17 PM
The heredoc tripped the shell; mình chuyển script ra file tạm:



Bash Check scratchpad directory
IN
echo $TEMP; ls "C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/" 2>&1

OUT
C:\Users\DuyQuang\AppData\Local\Temp
scratchpad
tasks
Write C:\Users\DuyQuang\AppData\Local\Temp\claude\c--Users-DuyQuang-Desktop-SourceCodeBase\39023dd3-17a2-4865-b587-db2f3efa5ce2\scratchpad\product_scope.py
145 lines
import os, re, sys

os.chdir(sys.argv[1])


def rw(p, pairs):
    s = open(p, encoding='utf-8-sig').read()
    crlf = '\r\n' in s
    s = s.replace('\r\n', '\n')
    for a, b in pairs:
        assert a in s, (p, a)
        s = s.replace(a, b, 1)
    if crlf:
        s = s.replace('\n', '\r\n')
    open(p, 'w', encoding='utf-8-sig').write(s)


F = 'AerationSterilize.Application/Features/V1/Products/'

rw('AerationSterilize.Domain/Entities/Product.cs', [(
'''    public Guid? CreatedBy { get; set; } = Guid.NewGuid();
    public Guid? DepartmentId { get; set; } = Guid.NewGuid();
''', '''    // Owner of the row, used by the data scope (AccessLevel.Own / AccessLevel.Department). Set on create.
    public Guid? CreatedBy { get; set; }
    public Guid? DepartmentId { get; set; }
''')])

rw(F + 'Common/Caching/ProductCacheKeys.cs', [
('''    public static string ById(Guid id) => $"{Prefix}{id}";''',
 '''    // scope = DataScope.CacheKey: the same product/page is cached separately per data scope.
    public static string ById(Guid id, string scope) => $"{Prefix}{scope}:{id}";'''),
('''    public static string List(string? searchTerm, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}list:{searchTerm''',
 '''    public static string List(string scope, string? searchTerm, string? sortColumn, string? sortOrder, int pageIndex, int pageSize)
        => $"{Prefix}{scope}:list:{searchTerm''')])

rw(F + 'Queries/GetProductById/GetProductByIdQuery.cs', [
('using Contracts.Caching;\n', 'using Contracts.Caching;\nusing Contracts.Identity;\n'),
('public sealed record GetProductByIdQuery(Guid Id) : ICachedQuery<ProductDto>',
 'public sealed record GetProductByIdQuery(Guid Id, DataScope Scope) : ICachedQuery<ProductDto>'),
('ProductCacheKeys.ById(Id)', 'ProductCacheKeys.ById(Id, Scope.CacheKey)')])

rw(F + 'Queries/GetProductById/GetProductByIdQueryHandler.cs', [(
'''        var product = await _productRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new ProductNotFoundException(request.Id);
''', '''        var product = await _productRepository.FindByIdAsync(request.Id, cancellationToken);

        // Outside the user's data scope is reported as not found, so the product's existence is not revealed.
        if (product is null || !request.Scope.Covers(product))
            throw new ProductNotFoundException(request.Id);
''')])

rw(F + 'Queries/GetProducts/GetProductsQuery.cs', [
('using Contracts.Caching;\n', 'using Contracts.Caching;\nusing Contracts.Identity;\n'),
('''    int PageIndex, int PageSize)''', '''    int PageIndex, int PageSize,
    DataScope Scope)'''),
('ProductCacheKeys.List(searchTerm,', 'ProductCacheKeys.List(Scope.CacheKey, searchTerm,')])

rw(F + 'Queries/GetProducts/GetProductsQueryHandler.cs', [
('using Contracts.Common.Repositories;\n', 'using Contracts.Common.Repositories;\nusing Contracts.Identity;\n'),
('''        var sortExpression''', '''        productsQuery = productsQuery.ApplyDataScope(request.Scope);

        var sortExpression''')])

rw(F + 'Commands/CreateProduct/CreateProductCommandHandler.cs', [
('using AerationSterilize.Domain.Entities;\n', 'using AerationSterilize.Application.Common;\nusing AerationSterilize.Domain.Entities;\n'),
('''    private readonly IMapper _mapper;

    public CreateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper)
    {
''', '''    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;

    public CreateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper, ICurrentUser currentUser)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
'''),
('''        var productEntity = _mapper.Map<Product>(request);
''', '''        var productEntity = _mapper.Map<Product>(request);
        productEntity.CreatedBy = _currentUser.UserId;
        productEntity.DepartmentId = _currentUser.DepartmentId;
''')])

for cmd, code in [('UpdateProduct', 'UPDATE'), ('DeleteProduct', 'DELETE')]:
    p = F + f'Commands/{cmd}/{cmd}CommandHandler.cs'
    s = open(p, encoding='utf-8-sig').read()
    crlf = '\r\n' in s
    s = s.replace('\r\n', '\n')
    s = s.replace('using AerationSterilize.Domain.Entities;\n',
                  'using AerationSterilize.Application.Common;\nusing AerationSterilize.Domain.Entities;\n', 1)
    s = s.replace('using Contracts.Responses;\n',
                  'using Contracts.Responses;\nusing Shared.Common.Constants.Authorization;\n', 1)
    s = s.replace('    private readonly IRepositoryBase<Product, Guid> _productRepository;\n',
                  '    private readonly IRepositoryBase<Product, Guid> _productRepository;\n    private readonly ICurrentUser _currentUser;\n', 1)
    s = re.sub(r'(public ' + cmd + r'CommandHandler\(IRepositoryBase<Product, Guid> productRepository)(, IMapper mapper)?\)\n    \{\n',
               lambda m: m.group(1) + (m.group(2) or '') + ', ICurrentUser currentUser)\n    {\n'
               + '        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));\n', s)
    old = '''        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new ProductNotFoundException(request.Id);
'''
    new = f'''        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken);

        // Outside the user's data scope is reported as not found, so the product's existence is not revealed.
        var scope = _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.{code});
        if (productEntity is null || !scope.Covers(productEntity))
            throw new ProductNotFoundException(request.Id);
'''
    assert old in s and '_currentUser = currentUser' in s, p
    s = s.replace(old, new, 1)
    if crlf:
        s = s.replace('\n', '\r\n')
    open(p, 'w', encoding='utf-8-sig').write(s)

rw('AerationSterilize.API/Controllers/V1/ProductsController.cs', [
('using AerationSterilize.API.Abstractions;\n', 'using AerationSterilize.API.Abstractions;\nusing AerationSterilize.Application.Common;\n'),
('''    public ProductsController(ISender sender) : base(sender)
    {
    }
''', '''    private readonly ICurrentUser _currentUser;

    public ProductsController(ISender sender, ICurrentUser currentUser) : base(sender)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }
'''),
('''            pageIndex,
            pageSize));''', '''            pageIndex,
            pageSize,
            _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.VIEW)));'''),
('new GetProductByIdQuery(productId)',
 'new GetProductByIdQuery(productId, _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.VIEW))'),
('''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateProducts''', '''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.CREATE)]
    public async Task<IActionResult> CreateProducts'''),
('''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct''', '''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.DELETE)]
    public async Task<IActionResult> DeleteProduct'''),
('''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProductById''', '''    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permission(FunctionCode.PRODUCT, CommandCode.UPDATE)]
    public async Task<IActionResult> UpdateProductById''')])
print("ok")
Bash Run product scope edit script
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase && python "C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/product_scope.py" src/Services/AerationSterilize && git diff src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/DeleteProduct/

OUT
ok
diff --git a/src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/DeleteProduct/DeleteProductCommandHandler.cs b/src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/DeleteProduct/DeleteProductCommandHandler.cs
index 285d529..dde3dfd 100644
--- a/src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/DeleteProduct/DeleteProductCommandHandler.cs
+++ b/src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/DeleteProduct/DeleteProductCommandHandler.cs
@@ -1,24 +1,32 @@
-﻿using AerationSterilize.Domain.Entities;
+﻿using AerationSterilize.Application.Common;
+using AerationSterilize.Domain.Entities;
 using AutoMapper;
 using static AerationSterilize.Domain.Exceptions.ProductException;
 using Contracts.Common.Messages;
 using Contracts.Common.Repositories;
 using Contracts.Responses;
+using Shared.Common.Constants.Authorization;
 
 namespace AerationSterilize.Application.Features.V1.Products.Commands.DeleteProduct;
 internal sealed class DeleteProductCommandHandler : ICommandHandler<DeleteProductCommand>
 {
     private readonly IRepositoryBase<Product, Guid> _productRepository;
+    private readonly ICurrentUser _currentUser;
 
-    public DeleteProductCommandHandler(IRepositoryBase<Product, Guid> productRepository)
+    public DeleteProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, ICurrentUser currentUser)
     {
+        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
         _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
     }
 
     public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
     {
-        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken)
-            ?? throw new ProductNotFoundException(request.Id);
+        var productEntity = await _productRepository.FindByIdAsync(request.Id, cancellationToken);
+
+        // Outside the user's data scope is reported as not found, so the product's existence is not revealed.
+        var scope = _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.DELETE);
+        if (productEntity is null || !scope.Covers(productEntity))
+            throw new ProductNotFoundException(request.Id);
 
         _productRepository.Remove(productEntity);
9:17 PM
Sắp lại thứ tự gán trong constructor cho giống code xung quanh, rồi build:



Bash Reorder constructor assignments and build
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands && python -c "
import re,glob
for p in glob.glob('*/*CommandHandler.cs'):
    s=open(p,encoding='utf-8-sig').read()
    line='        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));'
    nl='\r\n' if '\r\n' in s else '\n'
    if line not in s: continue
    s=s.replace(line+nl,'',1)
    s=re.sub(r'(_mapper = mapper \?\? throw new ArgumentNullException\(nameof\(mapper\)\);|_productRepository = productRepository \?\? throw new ArgumentNullException\(nameof\(productRepository\)\);)(\r?\n    \})', lambda m: m.group(1)+nl+line+m.group(2), s, count=1)
    open(p,'w',encoding='utf-8-sig').write(s); print(p)
" && grep -n -A5 "CommandHandler(IRepo" */*CommandHandler.cs && cd /c/Users/DuyQuang/Desktop/SourceCodeBase && dotnet build MyApp.sln -nologo -v q 2>&1 | grep -E " error |Error\(s\)" | sort -u

OUT
CreateProduct\CreateProductCommandHandler.cs
DeleteProduct\DeleteProductCommandHandler.cs
UpdateProduct\UpdateProductCommandHandler.cs
CreateProduct/CreateProductCommandHandler.cs:15:    public CreateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper, ICurrentUser currentUser)
CreateProduct/CreateProductCommandHandler.cs-16-    {
CreateProduct/CreateProductCommandHandler.cs-17-        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
CreateProduct/CreateProductCommandHandler.cs-18-        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
CreateProduct/CreateProductCommandHandler.cs-19-        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
CreateProduct/CreateProductCommandHandler.cs-20-    }
--
DeleteProduct/DeleteProductCommandHandler.cs:16:    public DeleteProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, ICurrentUser currentUser)
DeleteProduct/DeleteProductCommandHandler.cs-17-    {
DeleteProduct/DeleteProductCommandHandler.cs-18-        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
DeleteProduct/DeleteProductCommandHandler.cs-19-        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
DeleteProduct/DeleteProductCommandHandler.cs-20-    }
DeleteProduct/DeleteProductCommandHandler.cs-21-
--
UpdateProduct/UpdateProductCommandHandler.cs:19:    public UpdateProductCommandHandler(IRepositoryBase<Product, Guid> productRepository, IMapper mapper, ICurrentUser currentUser)
UpdateProduct/UpdateProductCommandHandler.cs-20-    {
UpdateProduct/UpdateProductCommandHandler.cs-21-        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
UpdateProduct/UpdateProductCommandHandler.cs-22-        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
UpdateProduct/UpdateProductCommandHandler.cs-23-        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
UpdateProduct/UpdateProductCommandHandler.cs-24-    }
    0 Error(s)
9:18 PM
Build OK. Kiểm tra xem Aeration có cần migration mới không:



Bash Check pending EF model changes and status
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize && dotnet ef migrations has-pending-model-changes --project AerationSterilize.Persistence --startup-project AerationSterilize.API --no-build 2>&1 | tail -2; cd ../CommonServices && dotnet ef migrations has-pending-model-changes --project CommonServices.Persistence --startup-project CommonServices.API --no-build 2>&1 | tail -1; cd /c/Users/DuyQuang/Desktop/SourceCodeBase && git status --short

OUT
No changes have been made to the model since the last migration.
No changes have been made to the model since the last migration.
RM src/Services/AerationSterilize/AerationSterilize.API/Authorization/PermissionAttribute.cs -> src/BuildingBlocks/Infrastructure/Authorization/PermissionAttribute.cs
RM src/Services/AerationSterilize/AerationSterilize.API/Authorization/PermissionAuthorizationHandler.cs -> src/BuildingBlocks/Infrastructure/Authorization/PermissionAuthorizationHandler.cs
RM src/Services/AerationSterilize/AerationSterilize.API/Authorization/PermissionRequirement.cs -> src/BuildingBlocks/Infrastructure/Authorization/PermissionRequirement.cs
 M src/BuildingBlocks/Infrastructure/Extensions/CachingServiceCollectionExtensions.cs
 M src/BuildingBlocks/Infrastructure/Identity/RedisTokenRevocationService.cs
 M src/BuildingBlocks/Infrastructure/Identity/TokenService.cs
 M src/BuildingBlocks/Infrastructure/Infrastructure.csproj
 M src/BuildingBlocks/Shared/Common/Constants/Authorization/FunctionCode.cs
 M src/BuildingBlocks/Shared/DTOs/Identity/TokenRequest.cs
 M src/Services/AerationSterilize/AerationSterilize.API/Controllers/V1/ProductsController.cs
D  src/Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/DataScopeExtensions.cs
 M src/Services/AerationSterilize/AerationSterilize.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
 M src/Services/AerationSterilize/AerationSterilize.API/Services/CurrentUser.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/AerationSterilize.Application.csproj
 M src/Services/AerationSterilize/AerationSterilize.Application/Common/ICurrentUser.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Login/LoginCommandHandler.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/CreateProduct/CreateProductCommandHandler.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/DeleteProduct/DeleteProductCommandHandler.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/UpdateProduct/UpdateProductCommandHandler.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Common/Caching/ProductCacheKeys.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Queries/GetProductById/GetProductByIdQuery.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Queries/GetProductById/GetProductByIdQueryHandler.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Queries/GetProducts/GetProductsQuery.cs
 M src/Services/AerationSterilize/AerationSterilize.Application/Features/V1/Products/Queries/GetProducts/GetProductsQueryHandler.cs
 M src/Services/AerationSterilize/AerationSterilize.Domain/Entities/Product.cs
 M src/Services/AerationSterilize/AerationSterilize.Persistence/ApplicationContextSeed.cs
D  src/Services/CommonServices/CommonServices.API/Authorization/PermissionAttribute.cs
D  src/Services/CommonServices/CommonServices.API/Authorization/PermissionAuthorizationHandler.cs
D  src/Services/CommonServices/CommonServices.API/Authorization/PermissionRequirement.cs
D  src/Services/CommonServices/CommonServices.API/Controllers/V1/AuthController.cs
 M src/Services/CommonServices/CommonServices.API/Controllers/V1/BanksController.cs
 M src/Services/CommonServices/CommonServices.API/Controllers/V1/CitiesController.cs
 M src/Services/CommonServices/CommonServices.API/Controllers/V1/CountriesController.cs
 M src/Services/CommonServices/CommonServices.API/Controllers/V1/CurrenciesController.cs
 M src/Services/CommonServices/CommonServices.API/Controllers/V1/LanguagesController.cs
 M src/Services/CommonServices/CommonServices.API/Controllers/V1/RegionsController.cs
 M src/Services/CommonServices/CommonServices.API/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
 M src/Services/CommonServices/CommonServices.API/Program.cs
 M src/Services/CommonServices/CommonServices.API/Services/CurrentUser.cs
 M src/Services/CommonServices/CommonServices.Application/CommonServices.Application.csproj
 M src/Services/CommonServices/CommonServices.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Login/LoginCommand.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Login/LoginCommandHandler.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Login/LoginCommandValidator.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Logout/LogoutCommand.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Logout/LogoutCommandHandler.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommand.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Register/RegisterCommand.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Register/RegisterCommandHandler.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Commands/Register/RegisterCommandValidator.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Common/Dtos/LoginDto.cs
D  src/Services/CommonServices/CommonServices.Application/Features/V1/Auth/Common/Dtos/UserDto.cs
 M src/Services/CommonServices/CommonServices.Domain/CommonServices.Domain.csproj
D  src/Services/CommonServices/CommonServices.Domain/Entities/Identity/Action.cs
D  src/Services/CommonServices/CommonServices.Domain/Entities/Identity/ActionInFunction.cs
D  src/Services/CommonServices/CommonServices.Domain/Entities/Identity/AppRole.cs
D  src/Services/CommonServices/CommonServices.Domain/Entities/Identity/AppUser.cs
D  src/Services/CommonServices/CommonServices.Domain/Entities/Identity/Function.cs
D  src/Services/CommonServices/CommonServices.Domain/Entities/Identity/Permission.cs
D  src/Services/CommonServices/CommonServices.Domain/Entities/Identity/UserSession.cs
D  src/Services/CommonServices/CommonServices.Domain/Exceptions/AuthException.cs
 M src/Services/CommonServices/CommonServices.Persistence/ApplicationContextSeed.cs
 M src/Services/CommonServices/CommonServices.Persistence/ApplicationDbContext.cs
 M src/Services/CommonServices/CommonServices.Persistence/CommonServices.Persistence.csproj
D  src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/ActionConfiguration.cs
D  src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/ActionInFunctionConfiguration.cs
D  src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/AppRoleConfiguration.cs
D  src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/AppUserConfiguration.cs
D  src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/FunctionConfiguration.cs
D  src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/IdentityConfiguration.cs
D  src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/PermissionConfiguration.cs
D  src/Services/CommonServices/CommonServices.Persistence/Configurations/Identity/UserSessionConfiguration.cs
 M src/Services/CommonServices/CommonServices.Persistence/Constants/TableNames.cs
 M src/Services/CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ApplicationBuilderExtensions.cs
 M src/Services/CommonServices/CommonServices.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs
 M src/Services/CommonServices/CommonServices.Persistence/Migrations/ApplicationDbContextModelSnapshot.cs
D  src/Services/CommonServices/CommonServices.Persistence/Services/PermissionService.cs
?? src/BuildingBlocks/Contracts/Identity/DataScope.cs
?? src/BuildingBlocks/Infrastructure/Extensions/AuthenticationServiceCollectionExtensions.cs
?? src/Services/CommonServices/CommonServices.Persistence/Migrations/20261015140950_RemoveIdentity.Designer.cs
?? src/Services/CommonServices/CommonServices.Persistence/Migrations/20261015140950_RemoveIdentity.cs
9:18 PM
Mình đã sửa xong cả 5 mục. Solution build không lỗi và không cần migration mới (EF xác nhận model không đổi). Mình chưa chạy thử API thật và chưa commit.

1. API Product không còn mở cho người chưa đăng nhập

CreateProducts, UpdateProductById, DeleteProduct giờ có [Permission(PRODUCT, CREATE / UPDATE / DELETE)].
2. Token có thêm department_id

Login và refresh token đều ghi phòng ban của user vào token. Mức Department giờ lọc đúng theo phòng.
Tên claim là hằng UserClaim.DepartmentId, dùng chung cho cả 2 service.
3. Đã áp dụng lọc theo AccessLevel cho Product

Thêm DataScope ở Contracts/Identity/DataScope.cs, thay cho DataScopeExtensions ở tầng API (đã xoá). Hàm ApplyDataScope lọc câu query; hàm Covers kiểm tra một bản ghi cụ thể có nằm trong phạm vi của user không.
User có mức Department nhưng không thuộc phòng nào thì không thấy gì. Trước đây họ thấy mọi bản ghi không gán phòng.
GetProducts chỉ trả về bản ghi trong phạm vi của user.
GetProductById, Update, Delete: bản ghi ngoài phạm vi trả về "không tìm thấy", để không lộ là bản ghi đó có tồn tại.
Cache: hai query Product đang được cache trên Redis, nên mình thêm phạm vi của user vào cache key, ví dụ product:own:{userId}:.... Nếu không, user chỉ có quyền Own có thể nhận kết quả đã cache của người có quyền All. Khi tạo/sửa/xoá Product, cache vẫn bị xoá hết theo tiền tố product: như trước.
4. Gán người tạo khi tạo Product

Tạo Product giờ gán CreatedBy và DepartmentId theo user hiện tại.
Đã bỏ giá trị mặc định Guid.NewGuid() trong entity.
5. Quyền mức None bị chặn

PermissionAuthorizationHandler giờ kiểm tra cả mức quyền. Quyền có AccessLevel = None sẽ bị trả 403.
Bạn cần biết khi chạy

Product cũ trong DB: CreatedBy và DepartmentId đang là Guid ngẫu nhiên do lỗi mặc định cũ, nên user mức Own hoặc Department sẽ không thấy chúng. Nếu cần, cập nhật lại 2 cột này bằng SQL.
Product seed: giờ không có chủ, nên chỉ user mức All thấy.
Token cũ: chưa có department_id, nên user phải login lại hoặc refresh token.
CommonServices không đổi gì về AccessLevel: master data là danh mục dùng chung, chỉ cần [Permission] là đủ.


Hãy chạy hết và tạo bảng lên đi.
mày hãy test, hãy nhập dữ liệu mẫu vào. tạo thêm vài user, các kiểu.
test hết tất cả đi.
test đến đâu ghi ra 1 file.
ghi 1 file .md ở solution đi. ghi test từng api và kết quả.
Show more
9:20 PM
Mình kiểm tra trước SQL Server và Redis có đang chạy không.



Bash Check tooling, docker-compose, and Redis reachability
IN
which sqlcmd redis-cli docker curl 2>&1; cat docker-compose.yml | head -40; (echo PING; sleep 1) | timeout 3 bash -c 'exec 3<>/dev/tcp/localhost/6379; echo -e "PING\r" >&3; timeout 1 cat <&3' 2>&1 | head -2

OUT
which: no redis-cli in (/c/Users/DuyQuang/bin:/mingw64/bin:/usr/local/bin:/usr/bin:/bin:/mingw64/bin:/usr/bin:/c/Users/DuyQuang/bin:/c/Program Files/dotnet:/c/Program Files (x86)/VMware/VMware Workstation/bin:/c/Windows/system32:/c/Windows:/c/Windows/System32/Wbem:/c/Windows/System32/WindowsPowerShell/v1.0:/c/Windows/System32/OpenSSH:/c/Program Files/Microsoft VS Code/bin:/c/Program Files/Microsoft SQL Server/150/Tools/Binn:/c/Program Files/Microsoft SQL Server/Client SDK/ODBC/170/Tools/Binn:/c/Program Files (x86)/Microsoft SQL Server/160/Tools/Binn:/c/Program Files/Microsoft SQL Server/160/Tools/Binn:/c/Program Files/Microsoft SQL Server/160/DTS/Binn:/c/Program Files (x86)/Microsoft SQL Server/160/DTS/Binn:/cmd:/c/Program Files/Cloudflare/Cloudflare WARP:/c/Program Files/Docker/Docker/resources/bin:/c/Users/DuyQuang/AppData/Local/Programs/Python/Python311/Scripts:/c/Users/DuyQuang/AppData/Local/Programs/Python/Python311:/c/Users/DuyQuang/AppData/Local/Microsoft/WindowsApps:/c/Users/DuyQuang/.dotnet/tools:/c/Users/DuyQuang/AppData/Local/Programs/Antigravity IDE/bin:/usr/bin/vendor_perl:/usr/bin/core_perl)
/c/Program Files/Microsoft SQL Server/Client SDK/ODBC/170/Tools/Binn/sqlcmd
/c/Program Files/Docker/Docker/resources/bin/docker
/mingw64/bin/curl
services:
  redis:
    image: redis:7-alpine
    container_name: aeration-redis
    restart: unless-stopped
    command: ["redis-server", "--appendonly", "yes"]
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5

volumes:
  redis-data:
Bash Check docker containers and SQL Server databases
IN
docker ps --format "{{.Names}} {{.Status}} {{.Ports}}" 2>&1 | head; sqlcmd -S QUANG -U sa -P 123456 -C -Q "select name from sys.databases" 2>&1 | head -20

OUT
error during connect: Get "http://%2F%2F.%2Fpipe%2FdockerDesktopLinuxEngine/v1.49/containers/json": open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified.
name                                                                                                                            
--------------------------------------------------------------------------------------------------------------------------------
master                                                                                                                          
tempdb                                                                                                                          
model                                                                                                                           
msdb                                                                                                                            
FABudgetDB                                                                                                                      
sonarqube                                                                                                                       
SourceBase                                                                                                                      
SourceBase_RedisTest                                                                                                            
CommonServices                                                                                                                  

(9 rows affected)
9:20 PM
SQL Server chạy được, nhưng Docker (Redis) đang tắt. Mình khởi động Docker Desktop rồi bật Redis:



PowerShell Start Docker Desktop and wait until ready
IN
Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"; $ok=$false; for($i=0;$i -lt 40;$i++){ Start-Sleep 5; docker info *> $null; if($LASTEXITCODE -eq 0){$ok=$true;break} }; "docker ready: $ok"

OUT
docker ready: True
Bash Start Redis container
IN
docker compose up -d 2>&1 | tail -3; sleep 3; docker exec aeration-redis redis-cli ping

OUT
 Container aeration-redis  Running
PONG
9:20 PM
Redis đã chạy. Giờ mình đọc các API và kiểu dữ liệu đầu vào để viết test.



Bash Read Aeration API shapes
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services && cat */*.API/Properties/launchSettings.json | grep -i url; cat AerationSterilize/AerationSterilize.API/Controllers/V1/AuthController.cs | grep -n "Http\|Route\|From\|Authorize\|Cookies"; cat AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Register/RegisterCommand*.cs AerationSterilize/AerationSterilize.Application/Features/V1/Auth/Commands/Login/LoginCommand.cs AerationSterilize/AerationSterilize.Application/Features/V1/Products/Commands/CreateProduct/CreateProductCommandValidator.cs; grep -n "sortColumn\|case\|\"" AerationSterilize/AerationSterilize.Application/Features/V1/Products/Common/Extensions/ProductExtension.cs

OUT
      "applicationUrl": "http://localhost:5000"
      "applicationUrl": "http://localhost:5001"
21:    [HttpPost]
22:    [Route("login")]
26:    public async Task<IActionResult> Login([FromForm] LoginCommand command)
34:            HttpOnly = true,
40:        Response.Cookies.Append("refreshToken", result.Value.RefreshToken, cookieOptions);
49:    [HttpPost]
50:    [Route("register")]
54:    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
61:    [HttpPost]
62:    [Route("refresh-token")]
68:        var refreshToken = Request.Cookies["refreshToken"];
78:    [HttpPost]
79:    [Route("logout")]
80:    [Authorize]
85:        var refreshToken = Request.Cookies["refreshToken"];
92:        Response.Cookies.Delete("refreshToken");
97:    [HttpGet]
98:    [Route("me")]
99:    [Authorize]
﻿using Contracts.Common.Messages;
using Shared.DTOs.Identity;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Register;
public sealed record RegisterCommand(
        string UserName,
        string Password,
        string Email,
        string FirstName,
        string LastName,
        string FullName) : ICommand;
﻿using AerationSterilize.Domain.Entities.Identity;
using static AerationSterilize.Domain.Exceptions.AuthException;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Identity;
using Contracts.Responses;
using Microsoft.AspNetCore.Identity;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Register;
internal class RegisterCommandHandler : ICommandHandler<RegisterCommand>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRepositoryBase<UserSession, int> _userSessionRepository;

    public RegisterCommandHandler(UserManager<AppUser> userManager, ITokenService tokenService,
          IRepositoryBase<UserSession, int> userSessionRepository)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
    }

    public async Task<Result> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await _userManager.FindByNameAsync(request.UserName) is not null)
        {
            return Result.Failure(new Error("USERNAME_EXISTS", "Username already exists"));
        }

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Result.Failure(new Error("EMAIL_EXISTS", "Email already exists"));
        }

        var userEntity = new AppUser
        {
            UserName = request.UserName,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            FullName = request.FullName
        };

        var result = await _userManager.CreateAsync(userEntity, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(x => x.Description));

            throw new RegisterFailedException(errors);
        }

        return Result.Success();
    }
}
﻿namespace AerationSterilize.Application.Features.V1.Auth.Commands.Register;
internal class RegisterCommandValidator
{
}
﻿using AerationSterilize.Application.Features.V1.Auth.Common.Dtos;
using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Auth.Commands.Login;
public sealed record LoginCommand(string UserName, string Password) : ICommand<LoginDto>;
using FluentValidation;

namespace AerationSterilize.Application.Features.V1.Products.Commands.CreateProduct;
internal sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0);
    }
}
11:    /// hàm trả về tên thuộc tính của sản phẩm tương ứng với cột được truyền vào, nếu cột không hợp lệ sẽ trả về "Id"
14:    /// <param name="sortColumn"></param>
16:    public static string GetSortProductProperty(string sortColumn)
17:        => sortColumn.ToLower() switch
19:            "name" => "Name",
20:            "price" => "Price",
21:            "description" => "Description",
22:            _ => "Id"
29:    /// <param name="sortColumn"></param>
31:    public static Expression<Func<Product, object>> GetSortExpression(string sortColumn)
33:        return sortColumn?.ToLower() switch
35:            "name" => x => x.Name,
36:            "price" => x => x.Price,
37:            "description" => x => x.Description,
Bash List CommonServices endpoints
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/CommonServices && grep -n "\[Http\|\[Permission\|public async\|Route\|AllowAnonymous" CommonServices.API/Controllers/V1/*.cs | sed 's/CommonServices.API\/Controllers\/V1\///' | grep -v "^Banks" ; cat CommonServices.API/Abstractions/ApiController.cs | grep -n "Language\|Header" 

OUT
CitiesController.cs:29:    [HttpGet(Name = "GetCities")]
CitiesController.cs:31:    [Permission(FunctionCode.CITY, CommandCode.VIEW)]
CitiesController.cs:32:    public async Task<IActionResult> GetCities(string? searchTerm = null,
CitiesController.cs:49:    [HttpGet("list")]
CitiesController.cs:51:    [Permission(FunctionCode.CITY, CommandCode.VIEW)]
CitiesController.cs:52:    public async Task<IActionResult> GetCityList(ObjectStatus? status = null,
CitiesController.cs:60:    [HttpGet("{cityId:guid}")]
CitiesController.cs:63:    [Permission(FunctionCode.CITY, CommandCode.VIEW)]
CitiesController.cs:64:    public async Task<IActionResult> GetCityById(Guid cityId, CancellationToken cancellationToken)
CitiesController.cs:70:    [HttpPost(Name = "CreateCity")]
CitiesController.cs:74:    [Permission(FunctionCode.CITY, CommandCode.CREATE)]
CitiesController.cs:75:    public async Task<IActionResult> CreateCity([FromBody] CreateCityCommand command, CancellationToken cancellationToken)
CitiesController.cs:81:    [HttpPut("{cityId:guid}")]
CitiesController.cs:84:    [Permission(FunctionCode.CITY, CommandCode.UPDATE)]
CitiesController.cs:85:    public async Task<IActionResult> UpdateCity(Guid cityId, [FromBody] UpdateCityCommand command, CancellationToken cancellationToken)
CitiesController.cs:92:    [HttpPut("{cityId:guid}/activate")]
CitiesController.cs:95:    [Permission(FunctionCode.CITY, CommandCode.ACTIVATE)]
CitiesController.cs:96:    public async Task<IActionResult> ActivateCity(Guid cityId, CancellationToken cancellationToken)
CitiesController.cs:102:    [HttpPut("{cityId:guid}/deactivate")]
CitiesController.cs:105:    [Permission(FunctionCode.CITY, CommandCode.INACTIVATE)]
CitiesController.cs:106:    public async Task<IActionResult> DeactivateCity(Guid cityId, CancellationToken cancellationToken)
CitiesController.cs:112:    [HttpDelete("{cityId:guid}")]
CitiesController.cs:115:    [Permission(FunctionCode.CITY, CommandCode.DELETE)]
CitiesController.cs:116:    public async Task<IActionResult> DeleteCity(Guid cityId, CancellationToken cancellationToken)
CountriesController.cs:29:    [HttpGet(Name = "GetCountries")]
CountriesController.cs:31:    [Permission(FunctionCode.COUNTRY, CommandCode.VIEW)]
CountriesController.cs:32:    public async Task<IActionResult> GetCountries(string? searchTerm = null,
CountriesController.cs:49:    [HttpGet("list")]
CountriesController.cs:51:    [Permission(FunctionCode.COUNTRY, CommandCode.VIEW)]
CountriesController.cs:52:    public async Task<IActionResult> GetCountryList(ObjectStatus? status = null,
CountriesController.cs:60:    [HttpGet("{countryId:guid}")]
CountriesController.cs:63:    [Permission(FunctionCode.COUNTRY, CommandCode.VIEW)]
CountriesController.cs:64:    public async Task<IActionResult> GetCountryById(Guid countryId, CancellationToken cancellationToken)
CountriesController.cs:70:    [HttpPost(Name = "CreateCountry")]
CountriesController.cs:74:    [Permission(FunctionCode.COUNTRY, CommandCode.CREATE)]
CountriesController.cs:75:    public async Task<IActionResult> CreateCountry([FromBody] CreateCountryCommand command, CancellationToken cancellationToken)
CountriesController.cs:81:    [HttpPut("{countryId:guid}")]
CountriesController.cs:84:    [Permission(FunctionCode.COUNTRY, CommandCode.UPDATE)]
CountriesController.cs:85:    public async Task<IActionResult> UpdateCountry(Guid countryId, [FromBody] UpdateCountryCommand command, CancellationToken cancellationToken)
CountriesController.cs:92:    [HttpPut("{countryId:guid}/activate")]
CountriesController.cs:95:    [Permission(FunctionCode.COUNTRY, CommandCode.ACTIVATE)]
CountriesController.cs:96:    public async Task<IActionResult> ActivateCountry(Guid countryId, CancellationToken cancellationToken)
CountriesController.cs:102:    [HttpPut("{countryId:guid}/deactivate")]
CountriesController.cs:105:    [Permission(FunctionCode.COUNTRY, CommandCode.INACTIVATE)]
CountriesController.cs:106:    public async Task<IActionResult> DeactivateCountry(Guid countryId, CancellationToken cancellationToken)
CountriesController.cs:112:    [HttpDelete("{countryId:guid}")]
CountriesController.cs:115:    [Permission(FunctionCode.COUNTRY, CommandCode.DELETE)]
CountriesController.cs:116:    public async Task<IActionResult> DeleteCountry(Guid countryId, CancellationToken cancellationToken)
CurrenciesController.cs:29:    [HttpGet(Name = "GetCurrencies")]
CurrenciesController.cs:31:    [Permission(FunctionCode.CURRENCY, CommandCode.VIEW)]
CurrenciesController.cs:32:    public async Task<IActionResult> GetCurrencies(string? searchTerm = null,
CurrenciesController.cs:48:    [HttpGet("list")]
CurrenciesController.cs:50:    [Permission(FunctionCode.CURRENCY, CommandCode.VIEW)]
CurrenciesController.cs:51:    public async Task<IActionResult> GetCurrencyList(ObjectStatus? status = null, CancellationToken cancellationToken = default)
CurrenciesController.cs:57:    [HttpGet("{currencyId:guid}")]
CurrenciesController.cs:60:    [Permission(FunctionCode.CURRENCY, CommandCode.VIEW)]
CurrenciesController.cs:61:    public async Task<IActionResult> GetCurrencyById(Guid currencyId, CancellationToken cancellationToken)
CurrenciesController.cs:67:    [HttpPost(Name = "CreateCurrency")]
CurrenciesController.cs:70:    [Permission(FunctionCode.CURRENCY, CommandCode.CREATE)]
CurrenciesController.cs:71:    public async Task<IActionResult> CreateCurrency([FromBody] CreateCurrencyCommand command, CancellationToken cancellationToken)
CurrenciesController.cs:77:    [HttpPut("{currencyId:guid}")]
CurrenciesController.cs:80:    [Permission(FunctionCode.CURRENCY, CommandCode.UPDATE)]
CurrenciesController.cs:81:    public async Task<IActionResult> UpdateCurrency(Guid currencyId, [FromBody] UpdateCurrencyCommand command, CancellationToken cancellationToken)
CurrenciesController.cs:88:    [HttpPut("{currencyId:guid}/activate")]
CurrenciesController.cs:91:    [Permission(FunctionCode.CURRENCY, CommandCode.ACTIVATE)]
CurrenciesController.cs:92:    public async Task<IActionResult> ActivateCurrency(Guid currencyId, CancellationToken cancellationToken)
CurrenciesController.cs:98:    [HttpPut("{currencyId:guid}/deactivate")]
CurrenciesController.cs:101:    [Permission(FunctionCode.CURRENCY, CommandCode.INACTIVATE)]
CurrenciesController.cs:102:    public async Task<IActionResult> DeactivateCurrency(Guid currencyId, CancellationToken cancellationToken)
CurrenciesController.cs:108:    [HttpDelete("{currencyId:guid}")]
CurrenciesController.cs:111:    [Permission(FunctionCode.CURRENCY, CommandCode.DELETE)]
CurrenciesController.cs:112:    public async Task<IActionResult> DeleteCurrency(Guid currencyId, CancellationToken cancellationToken)
LanguagesController.cs:29:    [HttpGet(Name = "GetLanguages")]
LanguagesController.cs:31:    [Permission(FunctionCode.LANGUAGE, CommandCode.VIEW)]
LanguagesController.cs:32:    public async Task<IActionResult> GetLanguages(string? searchTerm = null,
LanguagesController.cs:48:    [HttpGet("list")]
LanguagesController.cs:50:    [Permission(FunctionCode.LANGUAGE, CommandCode.VIEW)]
LanguagesController.cs:51:    public async Task<IActionResult> GetLanguageList(ObjectStatus? status = null, CancellationToken cancellationToken = default)
LanguagesController.cs:57:    [HttpGet("{languageId:guid}")]
LanguagesController.cs:60:    [Permission(FunctionCode.LANGUAGE, CommandCode.VIEW)]
LanguagesController.cs:61:    public async Task<IActionResult> GetLanguageById(Guid languageId, CancellationToken cancellationToken)
LanguagesController.cs:67:    [HttpPost(Name = "CreateLanguage")]
LanguagesController.cs:70:    [Permission(FunctionCode.LANGUAGE, CommandCode.CREATE)]
LanguagesController.cs:71:    public async Task<IActionResult> CreateLanguage([FromBody] CreateLanguageCommand command, CancellationToken cancellationToken)
LanguagesController.cs:77:    [HttpPut("{languageId:guid}")]
LanguagesController.cs:81:    [Permission(FunctionCode.LANGUAGE, CommandCode.UPDATE)]
LanguagesController.cs:82:    public async Task<IActionResult> UpdateLanguage(Guid languageId, [FromBody] UpdateLanguageCommand command, CancellationToken cancellationToken)
LanguagesController.cs:89:    [HttpPut("{languageId:guid}/activate")]
LanguagesController.cs:92:    [Permission(FunctionCode.LANGUAGE, CommandCode.ACTIVATE)]
LanguagesController.cs:93:    public async Task<IActionResult> ActivateLanguage(Guid languageId, CancellationToken cancellationToken)
LanguagesController.cs:99:    [HttpPut("{languageId:guid}/deactivate")]
LanguagesController.cs:103:    [Permission(FunctionCode.LANGUAGE, CommandCode.INACTIVATE)]
LanguagesController.cs:104:    public async Task<IActionResult> DeactivateLanguage(Guid languageId, CancellationToken cancellationToken)
LanguagesController.cs:110:    [HttpDelete("{languageId:guid}")]
LanguagesController.cs:114:    [Permission(FunctionCode.LANGUAGE, CommandCode.DELETE)]
LanguagesController.cs:115:    public async Task<IActionResult> DeleteLanguage(Guid languageId, CancellationToken cancellationToken)
MasterDataController.cs:34:    [HttpGet("languages")]
MasterDataController.cs:36:    public async Task<IActionResult> GetLanguages(CancellationToken cancellationToken)
MasterDataController.cs:42:    [HttpGet("languages/native")]
MasterDataController.cs:44:    public async Task<IActionResult> GetNativeLanguages(CancellationToken cancellationToken)
MasterDataController.cs:50:    [HttpGet("regions")]
MasterDataController.cs:52:    public async Task<IActionResult> GetRegions(CancellationToken cancellationToken)
MasterDataController.cs:58:    [HttpGet("currencies")]
MasterDataController.cs:60:    public async Task<IActionResult> GetCurrencies(CancellationToken cancellationToken)
MasterDataController.cs:66:    [HttpGet("countries")]
MasterDataController.cs:68:    public async Task<IActionResult> GetCountries([FromQuery] Guid[]? regionIds = null, CancellationToken cancellationToken = default)
MasterDataController.cs:74:    [HttpGet("cities")]
MasterDataController.cs:76:    public async Task<IActionResult> GetCities([FromQuery] Guid[]? countryIds = null, CancellationToken cancellationToken = default)
MasterDataController.cs:82:    [HttpGet("banks")]
MasterDataController.cs:84:    public async Task<IActionResult> GetBanks(CancellationToken cancellationToken)
RegionsController.cs:29:    [HttpGet(Name = "GetRegions")]
RegionsController.cs:31:    [Permission(FunctionCode.REGION, CommandCode.VIEW)]
RegionsController.cs:32:    public async Task<IActionResult> GetRegions(string? searchTerm = null,
RegionsController.cs:48:    [HttpGet("list")]
RegionsController.cs:50:    [Permission(FunctionCode.REGION, CommandCode.VIEW)]
RegionsController.cs:51:    public async Task<IActionResult> GetRegionList(ObjectStatus? status = null, CancellationToken cancellationToken = default)
RegionsController.cs:57:    [HttpGet("{regionId:guid}")]
RegionsController.cs:60:    [Permission(FunctionCode.REGION, CommandCode.VIEW)]
RegionsController.cs:61:    public async Task<IActionResult> GetRegionById(Guid regionId, CancellationToken cancellationToken)
RegionsController.cs:67:    [HttpPost(Name = "CreateRegion")]
RegionsController.cs:70:    [Permission(FunctionCode.REGION, CommandCode.CREATE)]
RegionsController.cs:71:    public async Task<IActionResult> CreateRegion([FromBody] CreateRegionCommand command, CancellationToken cancellationToken)
RegionsController.cs:77:    [HttpPut("{regionId:guid}")]
RegionsController.cs:80:    [Permission(FunctionCode.REGION, CommandCode.UPDATE)]
RegionsController.cs:81:    public async Task<IActionResult> UpdateRegion(Guid regionId, [FromBody] UpdateRegionCommand command, CancellationToken cancellationToken)
RegionsController.cs:88:    [HttpPut("{regionId:guid}/activate")]
RegionsController.cs:91:    [Permission(FunctionCode.REGION, CommandCode.ACTIVATE)]
RegionsController.cs:92:    public async Task<IActionResult> ActivateRegion(Guid regionId, CancellationToken cancellationToken)
RegionsController.cs:98:    [HttpPut("{regionId:guid}/deactivate")]
RegionsController.cs:101:    [Permission(FunctionCode.REGION, CommandCode.INACTIVATE)]
RegionsController.cs:102:    public async Task<IActionResult> DeactivateRegion(Guid regionId, CancellationToken cancellationToken)
RegionsController.cs:108:    [HttpDelete("{regionId:guid}")]
RegionsController.cs:111:    [Permission(FunctionCode.REGION, CommandCode.DELETE)]
RegionsController.cs:112:    public async Task<IActionResult> DeleteRegion(Guid regionId, CancellationToken cancellationToken)
18:    // Language of the translated names, taken from Accept-Language like the legacy common-services ("en-US,en;q=0.9" => "en-US").
19:    protected string RequestLanguage
23:            var header = Request.Headers.AcceptLanguage.ToString();
26:            return string.IsNullOrEmpty(language) ? LanguageCodes.Default : language;
Bash Read master data commands
IN
sed -n 15,40p CommonServices.API/Controllers/V1/MasterDataController.cs; for e in Language Region Currency Country City Bank; do for k in Create Update; do f=$(ls CommonServices.Application/Features/V1/*/Commands/$k$e/$k${e}Command.cs); sed -n '/record\|class/,$p' $f | grep -v "^\s*$" ; done; done; cat CommonServices.Application/Features/V1/Languages/Commands/CreateLanguage/*Validator.cs | grep -n Rule -A3 | head -40

OUT
using CommonServices.Application.Features.V1.Regions.Queries.GetRegionList;
using CommonServices.Domain.Enums;
using Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommonServices.API.Controllers.V1;

// Read-only lists for the portals and the other services (legacy api/common/master/*):
// any signed-in user, active rows only, names in the Accept-Language language.
[ApiVersion(1)]
[Authorize]
public class MasterDataController : ApiController
{
    public MasterDataController(ISender sender) : base(sender)
    {
    }

    [HttpGet("languages")]
    [ProducesResponseType(type: typeof(Result<IReadOnlyList<LanguageDto>>), statusCode: StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLanguages(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetLanguageListQuery(RequestLanguage, ObjectStatus.Active), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result);
    }
public sealed record CreateLanguageCommand(
    string Code,
    string Name,
    string? Description,
    string? FlagImagePath,
    LanguagePlatform? Platform,
    IReadOnlyList<LanguageTranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [LanguageCacheKeys.Prefix];
}
public sealed class UpdateLanguageCommand : ICommand<LanguageDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? FlagImagePath { get; set; }
    public LanguagePlatform? Platform { get; set; }
    public IReadOnlyList<LanguageTranslationRequest>? Translations { get; set; }
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [LanguageCacheKeys.Prefix];
    public void SetId(Guid id)
    {
        Id = id;
    }
}
public sealed record CreateRegionCommand(
    string? Code,
    string Name,
    string? Note,
    int SortOrder,
    IReadOnlyList<RegionTranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [RegionCacheKeys.Prefix];
}
public sealed class UpdateRegionCommand : ICommand<RegionDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string? Note { get; set; }
    public int SortOrder { get; set; }
    public IReadOnlyList<RegionTranslationRequest>? Translations { get; set; }
    // Countries show the region name, so their cached entries are cleared too.
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [RegionCacheKeys.Prefix, CountryCacheKeys.Prefix];
    public void SetId(Guid id)
    {
        Id = id;
    }
}
public sealed record CreateCurrencyCommand(
    string Code,
    string Name,
    string? Symbol,
    bool IsCurrencyOptionEnabled,
    IReadOnlyList<TranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CurrencyCacheKeys.Prefix];
}
public sealed class UpdateCurrencyCommand : ICommand<CurrencyDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Symbol { get; set; }
    public bool IsCurrencyOptionEnabled { get; set; }
    public IReadOnlyList<TranslationRequest>? Translations { get; set; }
    // Countries show the currency code, so their cached entries are cleared too.
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CurrencyCacheKeys.Prefix, CountryCacheKeys.Prefix];
    public void SetId(Guid id)
    {
        Id = id;
    }
}
public sealed record CreateCountryCommand(
    string Code,
    string Name,
    Guid? RegionId,
    Guid? CurrencyId,
    IReadOnlyList<TranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CountryCacheKeys.Prefix];
}
public sealed class UpdateCountryCommand : ICommand<CountryDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid? RegionId { get; set; }
    public Guid? CurrencyId { get; set; }
    public IReadOnlyList<TranslationRequest>? Translations { get; set; }
    // Cities show the country name, so their cached entries are cleared too.
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CountryCacheKeys.Prefix, CityCacheKeys.Prefix];
    public void SetId(Guid id)
    {
        Id = id;
    }
}
public sealed record CreateCityCommand(
    string Code,
    string Name,
    Guid CountryId,
    IReadOnlyList<TranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CityCacheKeys.Prefix];
}
public sealed class UpdateCityCommand : ICommand<CityDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid CountryId { get; set; }
    public IReadOnlyList<TranslationRequest>? Translations { get; set; }
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [CityCacheKeys.Prefix];
    public void SetId(Guid id)
    {
        Id = id;
    }
}
public sealed record CreateBankCommand(
    string Code,
    string Name,
    string? VAPrefix,
    bool IsVAByBank,
    IReadOnlyList<TranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [BankCacheKeys.Prefix];
}
public sealed class UpdateBankCommand : ICommand<BankDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsVAByBank { get; set; }
    public IReadOnlyList<TranslationRequest>? Translations { get; set; }
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [BankCacheKeys.Prefix];
    public void SetId(Guid id)
    {
        Id = id;
    }
}
10:        RuleFor(x => x.Code)
11-            .NotEmpty()
12-            .MaximumLength(ValidatorExtensions.LangCodeMaxLength); // the code is stored as LangCode in every translation table
13-
14:        RuleFor(x => x.Name)
15-            .NotEmpty()
16-            .MaximumLength(128);
17-
18:        RuleFor(x => x.Description)
19-            .MaximumLength(256);
20-
21:        RuleFor(x => x.FlagImagePath)
22-            .MaximumLength(1000);
23-
24:        RuleFor(x => x.Platform)
25-            .IsInEnum();
26-
27:        RuleFor(x => x.Translations)
28-            .MustHaveUniqueLangCodes();
29-
30:        RuleForEach(x => x.Translations)
31-            .SetValidator(new LanguageTranslationRequestValidator());
32-    }
33-}
Bash Check translation DTOs and enums
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src && grep -rn "record TranslationRequest\|record LanguageTranslationRequest\|record RegionTranslationRequest" --include=*.cs Services/CommonServices | grep -v obj; grep -rn "enum ObjectStatus\|enum LanguagePlatform" -A6 --include=*.cs . | grep -v obj | head -20; grep -rln "sortColumn" Services/CommonServices --include=*Validator.cs | head; grep -rn "JsonStringEnumConverter\|AddNewtonsoft" --include=*.cs Services | grep -v obj

OUT
Services/CommonServices/CommonServices.Application/Common/Dtos/TranslationRequest.cs:9:public sealed record TranslationRequest(string LangCode, string Name) : ITranslationRequest;
Services/CommonServices/CommonServices.Application/Features/V1/Languages/Common/Dtos/LanguageTranslationRequest.cs:4:public sealed record LanguageTranslationRequest(string LangCode, string Name, string? Description) : ITranslationRequest;
Services/CommonServices/CommonServices.Application/Features/V1/Regions/Common/Dtos/RegionTranslationRequest.cs:4:public sealed record RegionTranslationRequest(string LangCode, string Name, string? Note) : ITranslationRequest;
./Services/CommonServices/CommonServices.Domain/Enums/LanguagePlatform.cs:6:public enum LanguagePlatform
./Services/CommonServices/CommonServices.Domain/Enums/LanguagePlatform.cs-7-{
./Services/CommonServices/CommonServices.Domain/Enums/LanguagePlatform.cs-8-    None = 0,
./Services/CommonServices/CommonServices.Domain/Enums/LanguagePlatform.cs-9-    AdminPortal = 1,
./Services/CommonServices/CommonServices.Domain/Enums/LanguagePlatform.cs-10-    SunPortal = 2,
./Services/CommonServices/CommonServices.Domain/Enums/LanguagePlatform.cs-11-    All = 99
./Services/CommonServices/CommonServices.Domain/Enums/LanguagePlatform.cs-12-}
--
./Services/CommonServices/CommonServices.Domain/Enums/ObjectStatus.cs:4:public enum ObjectStatus
./Services/CommonServices/CommonServices.Domain/Enums/ObjectStatus.cs-5-{
./Services/CommonServices/CommonServices.Domain/Enums/ObjectStatus.cs-6-    Deleted = -99,
./Services/CommonServices/CommonServices.Domain/Enums/ObjectStatus.cs-7-    Inactive = 0,
./Services/CommonServices/CommonServices.Domain/Enums/ObjectStatus.cs-8-    Active = 1
./Services/CommonServices/CommonServices.Domain/Enums/ObjectStatus.cs-9-}
9:21 PM
Đủ thông tin. Giờ chạy 2 service (Aeration chạy migrate và seed, CommonServices áp migration RemoveIdentity):



Bash Check ports and build
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase && netstat -ano | grep -E ":500[01] .*LISTEN"; dotnet build MyApp.sln -nologo -v q 2>&1 | grep -E "Error\(s\)"

OUT
    0 Error(s)
Bash Run AerationSterilize API on port 5000
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize/AerationSterilize.API && dotnet run --no-build > "C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/aeration.log" 2>&1

OUT
Command running in background with ID: buj4tuksx. Output is being written to: C:\Users\DuyQuang\AppData\Local\Temp\claude\c--Users-DuyQuang-Desktop-SourceCodeBase\39023dd3-17a2-4865-b587-db2f3efa5ce2\tasks\buj4tuksx.output. You will be notified when it completes. To check interim output, use Read on that file path.
Session cwd remains C:\Users\DuyQuang\Desktop\SourceCodeBase; directory changes made by the backgrounded command do not apply to subsequent commands.
Bash Run CommonServices API on port 5001
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/CommonServices/CommonServices.API && dotnet run --no-build > "C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/common.log" 2>&1

OUT
Command running in background with ID: b0vqhgxjc. Output is being written to: C:\Users\DuyQuang\AppData\Local\Temp\claude\c--Users-DuyQuang-Desktop-SourceCodeBase\39023dd3-17a2-4865-b587-db2f3efa5ce2\tasks\b0vqhgxjc.output. You will be notified when it completes. To check interim output, use Read on that file path.
Session cwd remains C:\Users\DuyQuang\Desktop\SourceCodeBase; directory changes made by the backgrounded command do not apply to subsequent commands.
Bash Wait for services to start and check logs
IN
S="C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad"; for i in $(seq 1 30); do grep -q "Now listening" $S/aeration.log && grep -q "Now listening" $S/common.log && break; grep -qiE "fatal|Unhandled" $S/aeration.log $S/common.log && break; sleep 2; done; grep -iE "listening|Seeded|error|fatal|exception|migrat" $S/aeration.log $S/common.log | cut -c1-300 | head -30

OUT
C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/aeration.log:Sensitive data logging is enabled. Log entries and exception messages may include sensitive application data; this mode should only be enabled during deve
C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/aeration.log:Seeded 7 functions.
C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/aeration.log:Seeded 2 actions.
C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/aeration.log:Seeded 52 action-in-functions.
C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/aeration.log:Seeded 52 permissions for Administrator.
C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/aeration.log:Now listening on: http://localhost:5000
C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/common.log:Sensitive data logging is enabled. Log entries and exception messages may include sensitive application data; this mode should only be enabled during develo
C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/common.log:Now listening on: http://localhost:5001
9:21 PM
Cả 2 service đã chạy, seed thêm 52 quyền mới cho admin. Kiểm tra DB:



Bash Inspect database state for both services
IN
q(){ sqlcmd -S QUANG -U sa -P 123456 -C -d $1 -W -s"|" -Q "SET NOCOUNT ON; $2" 2>&1; }; q CommonServices "select name from sys.tables order by name"; q CommonServices "select MigrationId from __EFMigrationsHistory"; q SourceBase "select name from sys.tables order by name"; q SourceBase "select Id,UserName,DepartmentId from AppUsers"; q SourceBase "select Id,Name from AppRoles"; q SourceBase "select Id,Name,CreatedBy,DepartmentId from Products"; q SourceBase "select top 3 * from Permissions"

OUT
name
----
__EFMigrationsHistory
Banks
BankTranslations
Cities
CityTranslations
Countries
CountryTranslations
Currencies
CurrencyTranslations
Languages
LanguageTranslations
Regions
RegionTranslations
MigrationId
-----------
20261014145811_Initial
20261015140950_RemoveIdentity
name
----
__EFMigrationsHistory
ActionInFunctions
Actions
AppRoleClaims
AppRoles
AppUserClaims
AppUserLogins
AppUserRoles
AppUsers
AppUserTokens
Functions
Permissions
Product
sysdiagrams
UserSessions
Id|UserName|DepartmentId
--|--------|------------
33333333-3333-3333-3333-333333333333|admin|NULL
Id|Name
--|----
11111111-1111-1111-1111-111111111111|Administrator
22222222-2222-2222-2222-222222222222|User
Msg 208, Level 16, State 1, Server Quang, Line 1
Invalid object name 'Products'.
RoleId|FunctionId|ActionId|AccessLevel
------|----------|--------|-----------
11111111-1111-1111-1111-111111111111|BANK|ACTIVATE|3
11111111-1111-1111-1111-111111111111|BANK|CREATE|3
11111111-1111-1111-1111-111111111111|BANK|DELETE|3
Bash Inspect Product table and permissions
IN
sqlcmd -S QUANG -U sa -P 123456 -C -d SourceBase -W -s"|" -Q "SET NOCOUNT ON; select Id,Name,Price,CreatedBy,DepartmentId from Product; select FunctionId, count(*) c from Permissions group by FunctionId; select COLUMN_NAME from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME='Product'" 2>&1

OUT
Id|Name|Price|CreatedBy|DepartmentId
--|----|-----|---------|------------
AAAAAAAA-0001-0000-0000-000000000001|Aeration Module A1|1500.00|NULL|NULL
AAAAAAAA-0001-0000-0000-000000000002|Sterilizer Unit S2|2750.50|NULL|NULL
AAAAAAAA-0001-0000-0000-000000000003|Ozone Generator O3|3200.00|NULL|NULL
AAAAAAAA-0001-0000-0000-000000000004|Diffuser Plate D4|450.75|NULL|NULL
FunctionId|c
----------|-
BANK|6
CITY|6
COUNTRY|6
CURRENCY|6
DASHBOARD|6
LANGUAGE|6
MASTER_DATA|6
PRODUCT|6
REGION|6
ROLE|6
SYSTEM|6
USER|6
COLUMN_NAME
-----------
Id
Name
Price
Description
CreatedDate
LastModifiedDate
CreatedBy
DepartmentId
9:22 PM
DB đã đúng: CommonServices chỉ còn các bảng master data, và Aeration có quyền cho cả 12 function. Xem định dạng response và cách map lỗi trước khi viết test:



Bash Check error mapping and try admin login
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src && sed -n 1,80p BuildingBlocks/Infrastructure/Middlewares/ExceptionHandlingMiddleware.cs | grep -n "Status\|=>\|Exception" | head -20; curl -s -X POST localhost:5000/api/v1/auth/login -F UserName=admin -F Password=Admin@123 -i | head -20; sqlcmd -S QUANG -U sa -P 123456 -C -d SourceBase -W -Q "SET NOCOUNT ON; select COLUMN_NAME, IS_NULLABLE from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME in ('AppRoles') " 

OUT
2:using Contracts.Exceptions;
8:public sealed class ExceptionHandlingMiddleware : IMiddleware
12:    public ExceptionHandlingMiddleware(ILogger logger)
23:        catch (Exception ex)
27:            await HandleExceptionAsync(context, ex);
31:    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
36:            Status = GetStatusCode(exception),
43:        context.Response.StatusCode = response.Status;
48:    private static int GetStatusCode(Exception exception)
52:            ValidationException => StatusCodes.Status400BadRequest,
54:            BadRequestException => StatusCodes.Status400BadRequest,
56:            NotFoundException => StatusCodes.Status404NotFound,
58:            FormatException => StatusCodes.Status422UnprocessableEntity,
60:            _ => StatusCodes.Status500InternalServerError
65:        Exception exception)
69:            DomainException domainException => domainException.Title,
70:            _ => "Internal Server Error"
75:        GetErrors(Exception exception)
79:            ValidationException validationException => validationException.Errors,
80:            _ => null
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Thu, 15 Oct 2026 14:22:05 GMT
Server: Kestrel
Set-Cookie: refreshToken=dHwfdLkA9YqjpbEiwHOcn8SvW2V09yoIrvXF%2BxAuUJE%3D; expires=Sat, 14 Nov 2026 14:22:06 GMT; path=/; secure; samesite=strict; httponly
Transfer-Encoding: chunked
api-supported-versions: 1.0

{"accessToken":"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIzMzMzMzMzMy0zMzMzLTMzMzMtMzMzMy0zMzMzMzMzMzMzMzMiLCJ1bmlxdWVfbmFtZSI6ImFkbWluIiwianRpIjoiZmE1MjllNWEtMGI1MC00YzI3LTkxM2QtYmQ4OGRhNjcwODdjIiwiaHR0cDovL3NjaGVtYXMubWljcm9zb2Z0LmNvbS93cy8yMDA4LzA2L2lkZW50aXR5L2NsYWltcy9yb2xlIjoiQWRtaW5pc3RyYXRvciIsInBlcm1pc3Npb24iOlsiQkFOSy5BQ1RJVkFURSIsIkJBTksuQ1JFQVRFIiwiQkFOSy5ERUxFVEUiLCJCQU5LLklOQUNUSVZBVEUiLCJCQU5LLlVQREFURSIsIkJBTksuVklFVyIsIkNJVFkuQUNUSVZBVEUiLCJDSVRZLkNSRUFURSIsIkNJVFkuREVMRVRFIiwiQ0lUWS5JTkFDVElWQVRFIiwiQ0lUWS5VUERBVEUiLCJDSVRZLlZJRVciLCJDT1VOVFJZLkFDVElWQVRFIiwiQ09VTlRSWS5DUkVBVEUiLCJDT1VOVFJZLkRFTEVURSIsIkNPVU5UUlkuSU5BQ1RJVkFURSIsIkNPVU5UUlkuVVBEQVRFIiwiQ09VTlRSWS5WSUVXIiwiQ1VSUkVOQ1kuQUNUSVZBVEUiLCJDVVJSRU5DWS5DUkVBVEUiLCJDVVJSRU5DWS5ERUxFVEUiLCJDVVJSRU5DWS5JTkFDVElWQVRFIiwiQ1VSUkVOQ1kuVVBEQVRFIiwiQ1VSUkVOQ1kuVklFVyIsIkRBU0hCT0FSRC5BQ1RJVkFURSIsIkRBU0hCT0FSRC5DUkVBVEUiLCJEQVNIQk9BUkQuREVMRVRFIiwiREFTSEJPQVJELklOQUNUSVZBVEUiLCJEQVNIQk9BUkQuVVBEQVRFIiwiREFTSEJPQVJELlZJRVciLCJMQU5HVUFHRS5BQ1RJVkFURSIsIkxBTkdVQUdFLkNSRUFURSIsIkxBTkdVQUdFLkRFTEVURSIsIkxBTkdVQUdFLklOQUNUSVZBVEUiLCJMQU5HVUFHRS5VUERBVEUiLCJMQU5HVUFHRS5WSUVXIiwiTUFTVEVSX0RBVEEuQUNUSVZBVEUiLCJNQVNURVJfREFUQS5DUkVBVEUiLCJNQVNURVJfREFUQS5ERUxFVEUiLCJNQVNURVJfREFUQS5JTkFDVElWQVRFIiwiTUFTVEVSX0RBVEEuVVBEQVRFIiwiTUFTVEVSX0RBVEEuVklFVyIsIlBST0RVQ1QuQUNUSVZBVEUiLCJQUk9EVUNULkNSRUFURSIsIlBST0RVQ1QuREVMRVRFIiwiUFJPRFVDVC5JTkFDVElWQVRFIiwiUFJPRFVDVC5VUERBVEUiLCJQUk9EVUNULlZJRVciLCJSRUdJT04uQUNUSVZBVEUiLCJSRUdJT04uQ1JFQVRFIiwiUkVHSU9OLkRFTEVURSIsIlJFR0lPTi5JTkFDVElWQVRFIiwiUkVHSU9OLlVQREFURSIsIlJFR0lPTi5WSUVXIiwiUk9MRS5BQ1RJVkFURSIsIlJPTEUuQ1JFQVRFIiwiUk9MRS5ERUxFVEUiLCJST0xFLklOQUNUSVZBVEUiLCJST0xFLlVQREFURSIsIlJPTEUuVklFVyIsIlNZU1RFTS5BQ1RJVkFURSIsIlNZU1RFTS5DUkVBVEUiLCJTWVNURU0uREVMRVRFIiwiU1lTVEVNLklOQUNUSVZBVEUiLCJTWVNURU0uVVBEQVRFIiwiU1lTVEVNLlZJRVciLCJVU0VSLkFDVElWQVRFIiwiVVNFUi5DUkVBVEUiLCJVU0VSLkRFTEVURSIsIlVTRVIuSU5BQ1RJVkFURSIsIlVTRVIuVVBEQVRFIiwiVVNFUi5WSUVXIl0sInBlcm1pc3Npb25fbGV2ZWwiOlsiQkFOSy5BQ1RJVkFURTozIiwiQkFOSy5DUkVBVEU6MyIsIkJBTksuREVMRVRFOjMiLCJCQU5LLklOQUNUSVZBVEU6MyIsIkJBTksuVVBEQVRFOjMiLCJCQU5LLlZJRVc6MyIsIkNJVFkuQUNUSVZBVEU6MyIsIkNJVFkuQ1JFQVRFOjMiLCJDSVRZLkRFTEVURTozIiwiQ0lUWS5JTkFDVElWQVRFOjMiLCJDSVRZLlVQREFURTozIiwiQ0lUWS5WSUVXOjMiLCJDT1VOVFJZLkFDVElWQVRFOjMiLCJDT1VOVFJZLkNSRUFURTozIiwiQ09VTlRSWS5ERUxFVEU6MyIsIkNPVU5UUlkuSU5BQ1RJVkFURTozIiwiQ09VTlRSWS5VUERBVEU6MyIsIkNPVU5UUlkuVklFVzozIiwiQ1VSUkVOQ1kuQUNUSVZBVEU6MyIsIkNVUlJFTkNZLkNSRUFURTozIiwiQ1VSUkVOQ1kuREVMRVRFOjMiLCJDVVJSRU5DWS5JTkFDVElWQVRFOjMiLCJDVVJSRU5DWS5VUERBVEU6MyIsIkNVUlJFTkNZLlZJRVc6MyIsIkRBU0hCT0FSRC5BQ1RJVkFURTozIiwiREFTSEJPQVJELkNSRUFURTowIiwiREFTSEJPQVJELkRFTEVURTowIiwiREFTSEJPQVJELklOQUNUSVZBVEU6MyIsIkRBU0hCT0FSRC5VUERBVEU6MCIsIkRBU0hCT0FSRC5WSUVXOjAiLCJMQU5HVUFHRS5BQ1RJVkFURTozIiwiTEFOR1VBR0UuQ1JFQVRFOjMiLCJMQU5HVUFHRS5ERUxFVEU6MyIsIkxBTkdVQUdFLklOQUNUSVZBVEU6MyIsIkxBTkdVQUdFLlVQREFURTozIiwiTEFOR1VBR0UuVklFVzozIiwiTUFTVEVSX0RBVEEuQUNUSVZBVEU6MyIsIk1BU1RFUl9EQVRBLkNSRUFURTozIiwiTUFTVEVSX0RBVEEuREVMRVRFOjMiLCJNQVNURVJfREFUQS5JTkFDVElWQVRFOjMiLCJNQVNURVJfREFUQS5VUERBVEU6MyIsIk1BU1RFUl9EQVRBLlZJRVc6MyIsIlBST0RVQ1QuQUNUSVZBVEU6MyIsIlBST0RVQ1QuQ1JFQVRFOjAiLCJQUk9EVUNULkRFTEVURTowIiwiUFJPRFVDVC5JTkFDVElWQVRFOjMiLCJQUk9EVUNULlVQREFURTowIiwiUFJPRFVDVC5WSUVXOjAiLCJSRUdJT04uQUNUSVZBVEU6MyIsIlJFR0lPTi5DUkVBVEU6MyIsIlJFR0lPTi5ERUxFVEU6MyIsIlJFR0lPTi5JTkFDVElWQVRFOjMiLCJSRUdJT04uVVBEQVRFOjMiLCJSRUdJT04uVklFVzozIiwiUk9MRS5BQ1RJVkFURTozIiwiUk9MRS5DUkVBVEU6MCIsIlJPTEUuREVMRVRFOjAiLCJST0xFLklOQUNUSVZBVEU6MyIsIlJPTEUuVVBEQVRFOjAiLCJST0xFLlZJRVc6MCIsIlNZU1RFTS5BQ1RJVkFURTozIiwiU1lTVEVNLkNSRUFURTowIiwiU1lTVEVNLkRFTEVURTowIiwiU1lTVEVNLklOQUNUSVZBVEU6MyIsIlNZU1RFTS5VUERBVEU6MCIsIlNZU1RFTS5WSUVXOjAiLCJVU0VSLkFDVElWQVRFOjMiLCJVU0VSLkNSRUFURTowIiwiVVNFUi5ERUxFVEU6MCIsIlVTRVIuSU5BQ1RJVkFURTozIiwiVVNFUi5VUERBVEU6MCIsIlVTRVIuVklFVzowIl0sImV4cCI6MTc5MjA3NzcyNSwiaXNzIjoiQWVyYXRpb25TdGVyaWxpemUiLCJhdWQiOiJBZXJhdGlvblN0ZXJpbGl6ZSJ9.cojaC3198bz9ywdnUQAEYIFr34oco6oqAo4nnXZBebc","expiresIn":3600}COLUMN_NAME IS_NULLABLE
----------- -----------
Id NO
Description NO
RoleCode NO
Name YES
NormalizedName YES
ConcurrencyStamp YES
9:22 PM
Phát hiện quan trọng: token của admin có PRODUCT.VIEW:0 (mức None). Các quyền cũ có trước migration AddDataScopeColumns đang ở mức 0. Mình kiểm tra migration đó:



Bash Inspect AccessLevel migration and data
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize/AerationSterilize.Persistence && sed -n '/Up(/,/Down(/p' Migrations/20261014135852_AddDataScopeColumns.cs; cat Configurations/Identiy/PermissionConfiguration.cs; sqlcmd -S QUANG -U sa -P 123456 -C -d SourceBase -W -Q "SET NOCOUNT ON; select AccessLevel, count(*) from Permissions group by AccessLevel"

OUT
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Product",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Product",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccessLevel",
                table: "Permissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "AppUsers",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
﻿using AerationSterilize.Domain.Entities.Identity;
using AerationSterilize.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AerationSterilize.Persistence.Configurations.Identiy;
internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable(TableNames.Permissions);

        builder.HasKey(x => new { x.RoleId, x.FunctionId, x.ActionId });
    }
}
AccessLevel  
----------- -
0 20
3 52
Tôi phát hiện lỗi dữ liệu từ migration AddDataScopeColumns: 20 quyền cũ của admin bị gán nhầm giá trị mặc định None thay vì All. Giờ tôi sẽ tạo migration mới để sửa lại dữ liệu đó về giá trị All.
PowerShell Stop running Aeration API
IN
$p = (Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue).OwningProcess; if ($p) { Stop-Process -Id $p -Force -Confirm:$false; "stopped $p" }

OUT
stopped 16812 16812
Bash Scaffold empty data-fix migration
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize && dotnet ef migrations add FixPermissionAccessLevel --project AerationSterilize.Persistence --startup-project AerationSterilize.API 2>&1 | tail -2; ls AerationSterilize.Persistence/Migrations/*Fix*; cat AerationSterilize.Persistence/Migrations/*FixPermissionAccessLevel.cs

OUT
Done. To undo this action, use 'ef migrations remove'
AerationSterilize.Persistence/Migrations/20261015142238_FixPermissionAccessLevel.Designer.cs
AerationSterilize.Persistence/Migrations/20261015142238_FixPermissionAccessLevel.cs
﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AerationSterilize.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPermissionAccessLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
Background command "Run AerationSterilize API on port 5000" failed with exit code 127
9:22 PM
(Thông báo "failed" ở trên chỉ là do mình vừa tắt tiến trình Aeration cũ.)



Bash Write data-fix migration and rebuild
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize/AerationSterilize.Persistence/Migrations && python -c "
import glob
p=glob.glob('*_FixPermissionAccessLevel.cs')[0]
s=open(p,encoding='utf-8-sig').read()
nl='\r\n' if '\r\n' in s else '\n'
up='''        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddDataScopeColumns added AccessLevel with default 0 (None), so every permission granted before it
            // silently lost its access. Those rows predate data scopes and meant full access.
            migrationBuilder.Sql(\"UPDATE [Permissions] SET [AccessLevel] = 3 WHERE [AccessLevel] = 0\");
        }'''.replace('\n',nl)
s=s.replace('''        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }'''.replace('\n',nl), up,1)
open(p,'w',encoding='utf-8-sig').write(s)
" && sed -n 8,18p *_FixPermissionAccessLevel.cs && cd /c/Users/DuyQuang/Desktop/SourceCodeBase && dotnet build MyApp.sln -nologo -v q 2>&1 | grep "Error(s)"

OUT
    public partial class FixPermissionAccessLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddDataScopeColumns added AccessLevel with default 0 (None), so every permission granted before it
            // silently lost its access. Those rows predate data scopes and meant full access.
            migrationBuilder.Sql("UPDATE [Permissions] SET [AccessLevel] = 3 WHERE [AccessLevel] = 0");
        }

        /// <inheritdoc />
    0 Error(s)
Bash Restart AerationSterilize API
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/AerationSterilize/AerationSterilize.API && dotnet run --no-build > "C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad/aeration.log" 2>&1

OUT
Command running in background with ID: bn1z2hhcd. Output is being written to: C:\Users\DuyQuang\AppData\Local\Temp\claude\c--Users-DuyQuang-Desktop-SourceCodeBase\39023dd3-17a2-4865-b587-db2f3efa5ce2\tasks\bn1z2hhcd.output. You will be notified when it completes. To check interim output, use Read on that file path.
Session cwd remains C:\Users\DuyQuang\Desktop\SourceCodeBase; directory changes made by the backgrounded command do not apply to subsequent commands.
Bash Verify migration applied and check Redis perm cache
IN
S="C:/Users/DuyQuang/AppData/Local/Temp/claude/c--Users-DuyQuang-Desktop-SourceCodeBase/39023dd3-17a2-4865-b587-db2f3efa5ce2/scratchpad"; for i in $(seq 1 30); do grep -q "Now listening" $S/aeration.log && break; sleep 2; done; grep -iE "listening|fatal" $S/aeration.log; sqlcmd -S QUANG -U sa -P 123456 -C -d SourceBase -W -Q "SET NOCOUNT ON; select AccessLevel, count(*) n from Permissions group by AccessLevel; select MigrationId from __EFMigrationsHistory"; docker exec aeration-redis redis-cli --scan --pattern "*perm*"

OUT
Now listening on: http://localhost:5000
AccessLevel n
----------- -
3 72
MigrationId
-----------
20260605075521_Initial
20261014135852_AddDataScopeColumns
20261015142238_FixPermissionAccessLevel
aeration:perm:33333333-3333-3333-3333-333333333333
Migration đã chạy xong, cả 72 quyền đều ở mức All, nhưng Redis vẫn còn cache quyền admin cũ (30 phút) nên script test sẽ xoá cache này khi setup. Giờ tôi sẽ kiểm tra giới hạn độ dài mã để sinh dữ liệu test hợp lệ.
Bash Check code length rules and delete behavior
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/src/Services/CommonServices && grep -rn "Code)" -A3 --include=Create*Validator.cs CommonServices.Application | grep -E "Maximum|Matches|Length"; grep -rn "LangCodeMaxLength =" --include=*.cs CommonServices.Application; grep -n "Remove\|Status\|Delete" CommonServices.Application/Features/V1/Banks/Commands/DeleteBank/DeleteBankCommandHandler.cs | head; grep -rn "HasIndex.*Code" -A1 --include=*.cs CommonServices.Persistence/Configurations | head

OUT
CommonServices.Application/Features/V1/Banks/Commands/CreateBank/CreateBankCommandValidator.cs-11-            .MaximumLength(ValidatorExtensions.CodeMaxLength);
CommonServices.Application/Features/V1/Cities/Commands/CreateCity/CreateCityCommandValidator.cs-11-            .MaximumLength(ValidatorExtensions.CodeMaxLength);
CommonServices.Application/Features/V1/Countries/Commands/CreateCountry/CreateCountryCommandValidator.cs-11-            .MaximumLength(ValidatorExtensions.CodeMaxLength);
CommonServices.Application/Features/V1/Currencies/Commands/CreateCurrency/CreateCurrencyCommandValidator.cs-11-            .MaximumLength(ValidatorExtensions.CodeMaxLength);
CommonServices.Application/Features/V1/Languages/Commands/CreateLanguage/CreateLanguageCommandValidator.cs-12-            .MaximumLength(ValidatorExtensions.LangCodeMaxLength); // the code is stored as LangCode in every translation table
CommonServices.Application/Features/V1/Regions/Commands/CreateRegion/CreateRegionCommandValidator.cs-11-            .MaximumLength(ValidatorExtensions.CodeMaxLength);
CommonServices.Application/Common/Validators/ValidatorExtensions.cs:11:    public const int LangCodeMaxLength = 10;
8:namespace CommonServices.Application.Features.V1.Banks.Commands.DeleteBank;
9:internal sealed class DeleteBankCommandHandler : ICommandHandler<DeleteBankCommand>
14:    public DeleteBankCommandHandler(IRepositoryBase<Bank, Guid> bankRepository, ICurrentUser currentUser)
20:    public async Task<Result> Handle(DeleteBankCommand request, CancellationToken cancellationToken)
25:        // Soft delete: the row is kept with Status = Deleted.
26:        bankEntity.Delete(_currentUser.UserId.ToString());
CommonServices.Persistence/Configurations/MasterData/MasterDataEntityConfiguration.cs:36:        builder.HasIndex(x => x.Code)
CommonServices.Persistence/Configurations/MasterData/MasterDataEntityConfiguration.cs-37-            .IsUnique()
Bash Probe response shapes with admin token
IN
docker exec aeration-redis redis-cli del aeration:perm:33333333-3333-3333-3333-333333333333 >/dev/null; T=$(curl -s -X POST localhost:5000/api/v1/auth/login -F UserName=admin -F Password=Admin@123 | python -c "import sys,json;print(json.load(sys.stdin)['accessToken'])"); curl -s "localhost:5001/api/v1/banks?pageSize=2" -H "Authorization: Bearer $T"; echo; curl -s "localhost:5000/api/v1/products?sortColumn=name&pageSize=2" -H "Authorization: Bearer $T"; echo; curl -s "localhost:5001/api/v1/regions/list" -H "Authorization: Bearer $T" -H "Accept-Language: en-US"; echo; curl -s -X POST localhost:5001/api/v1/banks -H "Authorization: Bearer $T" -H "Content-Type: application/json" -d '{"code":"","name":""}'; echo; curl -s -o /dev/null -w "%{http_code}\n" -X POST localhost:5001/api/v1/auth/login -F UserName=admin -F Password=Admin@123

OUT
{"value":{"items":[{"id":"6bd39443-596f-49d1-9ddd-8e3024536bf1","code":"VCB","name":"VCB","vaPrefix":null,"isVAByBank":true,"status":1}],"pageIndex":1,"pageSize":2,"totalCount":1,"totalPages":1,"hasNextPage":false,"hasPreviousPage":false},"isSuccess":true,"isFailure":false,"error":{"code":"","message":""}}
{"value":{"items":[{"id":"aaaaaaaa-0001-0000-0000-000000000001","name":"Aeration Module A1","price":1500.00,"description":"High-efficiency aeration module for medium-sized tanks."},{"id":"aaaaaaaa-0001-0000-0000-000000000004","name":"Diffuser Plate D4","price":450.75,"description":"Ceramic diffuser plate producing fine bubbles for aeration."}],"pageIndex":1,"pageSize":2,"totalCount":4,"totalPages":2,"hasNextPage":true,"hasPreviousPage":false},"isSuccess":true,"isFailure":false,"error":{"code":"","message":""}}
{"value":[{"id":"0146236a-6911-4631-9e72-743cd5f6a898","code":"ASIA","name":"Asia","note":null,"sortOrder":1,"status":1}],"isSuccess":true,"isFailure":false,"error":{"code":"","message":""}}
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Code":["'Code' must not be empty."],"Name":["'Name' must not be empty."]},"traceId":"00-dbee37f1b32c875aaec917dd0968e8ef-f2c9df0df5df744e-00"}
404
Tất cả API đều trả đúng định dạng, và /auth/login ở CommonServices đã trả 404 như mong đợi. Giờ tôi sẽ viết script test tự tạo dữ liệu mẫu (role, user, phòng ban), chạy từng API và ghi kết quả vào file .md sau mỗi lần test.
Write C:\Users\DuyQuang\Desktop\SourceCodeBase\tests\ApiTests\run_api_tests.py
631 lines
"""
End-to-end API test: AerationSterilize (identity, products) + CommonServices (master data).

Requires both APIs running (http://localhost:5000, http://localhost:5001), SQL Server (sqlcmd)
and the Redis container "aeration-redis". Every test result is appended to API_TEST_REPORT.md
(solution root) as soon as it runs.

    python tests/ApiTests/run_api_tests.py
"""
import base64
import datetime
import hashlib
import hmac
import json
import os
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

AER = "http://localhost:5000/api/v1"
COM = "http://localhost:5001/api/v1"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REPORT = os.path.join(ROOT, "API_TEST_REPORT.md")
AER_SETTINGS = os.path.join(ROOT, "src", "Services", "AerationSterilize", "AerationSterilize.API", "appsettings.json")

SQL_SERVER, SQL_USER, SQL_PASSWORD = "QUANG", "sa", "123456"
AER_DB, COM_DB = "SourceBase", "CommonServices"
REDIS_CONTAINER = "aeration-redis"

PASSWORD = "Test@123"
DEPT_A = "d0000000-0000-0000-0000-00000000000a"
DEPT_B = "d0000000-0000-0000-0000-00000000000b"
ADMIN_ID = "33333333-3333-3333-3333-333333333333"
SEEDED_PRODUCT = "aaaaaaaa-0001-0000-0000-000000000001"

ROLES = {
    "R_MANAGER": ("a0000000-0000-0000-0000-000000000001", "T Product Manager"),
    "R_DEPT": ("a0000000-0000-0000-0000-000000000002", "T Department Staff"),
    "R_OWN": ("a0000000-0000-0000-0000-000000000003", "T Own Staff"),
    "R_VIEWER": ("a0000000-0000-0000-0000-000000000004", "T Master Data Viewer"),
    "R_NONE": ("a0000000-0000-0000-0000-000000000005", "T No Access"),
}
MASTER = ["MASTER_DATA", "LANGUAGE", "REGION", "CURRENCY", "COUNTRY", "CITY", "BANK"]
CRUD = ["VIEW", "CREATE", "UPDATE", "DELETE"]
# role -> [(function, action, AccessLevel)]   AccessLevel: 0 None, 1 Own, 2 Department, 3 All
ROLE_PERMISSIONS = {
    "R_MANAGER": [("PRODUCT", a, 3) for a in CRUD] + [(f, "VIEW", 3) for f in MASTER],
    "R_DEPT": [("PRODUCT", "VIEW", 2), ("PRODUCT", "CREATE", 2), ("PRODUCT", "UPDATE", 2), ("PRODUCT", "DELETE", 1)],
    "R_OWN": [("PRODUCT", a, 1) for a in CRUD],
    "R_VIEWER": [(f, "VIEW", 3) for f in MASTER],
    "R_NONE": [("PRODUCT", "VIEW", 0), ("BANK", "VIEW", 0)],
}
# user -> (department, roles, description)
USERS = {
    "t_manager": (DEPT_A, ["R_MANAGER"], "Phòng A, PRODUCT mức All + xem master data"),
    "t_staff_a1": (DEPT_A, ["R_DEPT"], "Phòng A, PRODUCT VIEW/CREATE/UPDATE mức Department, DELETE mức Own"),
    "t_staff_a2": (DEPT_A, ["R_DEPT"], "Phòng A, giống t_staff_a1"),
    "t_staff_b1": (DEPT_B, ["R_DEPT"], "Phòng B, giống t_staff_a1"),
    "t_own_a1": (DEPT_A, ["R_OWN"], "Phòng A, PRODUCT mức Own"),
    "t_own_a2": (DEPT_A, ["R_OWN"], "Phòng A, PRODUCT mức Own"),
    "t_viewer": (DEPT_B, ["R_VIEWER"], "Phòng B, chỉ xem master data, không có quyền PRODUCT"),
    "t_noaccess": (DEPT_A, ["R_NONE"], "PRODUCT.VIEW và BANK.VIEW nhưng AccessLevel = None"),
    "t_nodept": (None, ["R_DEPT"], "Không thuộc phòng nào, PRODUCT mức Department"),
    "t_multi": (DEPT_B, ["R_OWN", "R_DEPT"], "Phòng B, 2 role Own + Department (lấy mức cao nhất)"),
}

RUN = datetime.datetime.now().strftime("%H%M%S")
stats = {"pass": 0, "fail": 0, "info": 0}
counter = [0]


# ---------------------------------------------------------------- report

def write(text):
    with open(REPORT, "a", encoding="utf-8") as f:
        f.write(text)


def section(title, note=None):
    write(f"\n## {title}\n\n")
    if note:
        write(note + "\n\n")
    write("| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |\n|---|---|---|---|---|---|---|\n")


def cell(value):
    return str(value).replace("|", "\\|").replace("\n", " ")


def record(api, user, scenario, expected, actual, ok, info=False):
    counter[0] += 1
    mark = "ℹ️" if info else ("✅" if ok else "❌")
    stats["info" if info else ("pass" if ok else "fail")] += 1
    write(f"| {counter[0]} | `{cell(api)}` | {cell(user)} | {cell(scenario)} | {cell(expected)} | {cell(actual)} | {mark} |\n")
    print(f"{mark} {counter[0]:3} {api} [{user}] {scenario} -> {actual}")
    return ok


# ---------------------------------------------------------------- http

class Resp:
    def __init__(self, status, body, headers):
        self.status, self.body, self.headers = status, body, headers

    @property
    def json(self):
        try:
            return json.loads(self.body) if self.body else None
        except ValueError:
            return None

    @property
    def value(self):
        j = self.json
        return j.get("value") if isinstance(j, dict) else None

    def cookie(self, name):
        for k, v in self.headers:
            if k.lower() == "set-cookie" and v.startswith(name + "="):
                return v.split(";", 1)[0].split("=", 1)[1]
        return None

    def short(self):
        j = self.json
        if isinstance(j, dict) and j.get("isFailure"):
            return f"{self.status} ({j['error'].get('code')})"
        if isinstance(j, dict) and "title" in j and self.status >= 400:
            return f"{self.status} ({j.get('title')})"
        return str(self.status)


def http(method, url, token=None, body=None, form=None, cookie=None, lang=None):
    headers = {}
    data = None
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    if form is not None:
        data = urllib.parse.urlencode(form).encode()
        headers["Content-Type"] = "application/x-www-form-urlencoded"
    if cookie:
        headers["Cookie"] = f"refreshToken={cookie}"
    if lang:
        headers["Accept-Language"] = lang
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return Resp(r.status, r.read().decode(), r.getheaders())
    except urllib.error.HTTPError as e:
        return Resp(e.code, e.read().decode(), e.headers.items())


def expect(method, url, user, scenario, expected_status, token=None, check=None, check_desc=None, **kw):
    """Calls the API, records the row and returns the response."""
    r = http(method, url, token=token, **kw)
    ok = r.status == expected_status
    actual = r.short()
    if ok and check is not None:
        good, detail = check(r)
        ok = ok and good
        actual = f"{actual}; {detail}"
    expected = f"{expected_status}" + (f"; {check_desc}" if check_desc else "")
    record(f"{method} {url.replace(AER, ':5000').replace(COM, ':5001')}", user, scenario, expected, actual, ok)
    return r


# ---------------------------------------------------------------- sql / redis / jwt

def sql(db, query):
    out = subprocess.run(
        ["sqlcmd", "-S", SQL_SERVER, "-U", SQL_USER, "-P", SQL_PASSWORD, "-C", "-d", db, "-b",
         "-h", "-1", "-W", "-s", "|", "-Q", "SET NOCOUNT ON; " + query],
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    if out.returncode != 0:
        raise RuntimeError(f"SQL failed: {out.stdout}{out.stderr}")
    return [line for line in out.stdout.splitlines() if line.strip()]


def redis_delete(pattern):
    keys = subprocess.run(["docker", "exec", REDIS_CONTAINER, "redis-cli", "--scan", "--pattern", pattern],
                          capture_output=True, text=True).stdout.split()
    for k in keys:
        subprocess.run(["docker", "exec", REDIS_CONTAINER, "redis-cli", "del", k], capture_output=True)
    return len(keys)


def b64url(data):
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def jwt_claims(token):
    payload = token.split(".")[1]
    return json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))


def jwt_sign(claims, secret):
    header = b64url(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    payload = b64url(json.dumps(claims).encode())
    sig = hmac.new(secret.encode(), f"{header}.{payload}".encode(), hashlib.sha256).digest()
    return f"{header}.{payload}.{b64url(sig)}"


def as_list(v):
    return v if isinstance(v, list) else ([] if v is None else [v])


# ---------------------------------------------------------------- setup

def setup():
    write(f"# Báo cáo test API\n\n"
          f"- Thời gian chạy: {datetime.datetime.now():%Y-%m-%d %H:%M:%S}\n"
          f"- AerationSterilize: `{AER}` (DB `{AER_DB}`) — đăng nhập, cấp token, phân quyền, Product\n"
          f"- CommonServices: `{COM}` (DB `{COM_DB}`) — master data, chỉ kiểm tra token của Aeration\n"
          f"- Redis: container `{REDIS_CONTAINER}` (cache + danh sách token đã thu hồi)\n"
          f"- Script: `tests/ApiTests/run_api_tests.py` (chạy lại được, tự dọn dữ liệu test cũ)\n\n"
          f"<!-- SUMMARY -->\n")

    # Clean data from a previous run (test users/roles start with "t_"/"T ", test products with "TEST-").
    role_ids = ",".join(f"'{rid}'" for rid, _ in ROLES.values())
    sql(AER_DB, f"""
        DELETE FROM UserSessions WHERE UserId IN (SELECT Id FROM AppUsers WHERE UserName LIKE 't[_]%');
        DELETE FROM AppUserRoles WHERE UserId IN (SELECT Id FROM AppUsers WHERE UserName LIKE 't[_]%') OR RoleId IN ({role_ids});
        DELETE FROM AppUsers WHERE UserName LIKE 't[_]%';
        DELETE FROM Permissions WHERE RoleId IN ({role_ids});
        DELETE FROM AppRoles WHERE Id IN ({role_ids});
        DELETE FROM Product WHERE Name LIKE 'TEST-%';""")

    # Roles + permissions
    values = []
    for key, (rid, name) in ROLES.items():
        sql(AER_DB, f"INSERT INTO AppRoles (Id, Name, NormalizedName, RoleCode, Description, ConcurrencyStamp) "
                    f"VALUES ('{rid}', N'{name}', N'{name.upper()}', '{key}', N'Role test {key}', NEWID())")
        values += [f"('{rid}','{f}','{a}',{lvl})" for f, a, lvl in ROLE_PERMISSIONS[key]]
    sql(AER_DB, "INSERT INTO Permissions (RoleId, FunctionId, ActionId, AccessLevel) VALUES " + ",".join(values))

    section("0. Chuẩn bị dữ liệu mẫu",
            "Role test (bảng `AppRoles` + `Permissions`, AccessLevel: 0 None, 1 Own, 2 Department, 3 All):\n\n"
            + "\n".join(f"- **{ROLES[k][1]}** (`{k}`): "
                        + ", ".join(f"{f}.{a}:{lvl}" for f, a, lvl in ROLE_PERMISSIONS[k]) for k in ROLES)
            + f"\n\nPhòng ban: A = `{DEPT_A}`, B = `{DEPT_B}`. Mật khẩu mọi user test: `{PASSWORD}`.\n\n"
            + "User test (tạo qua API register, sau đó gán phòng + role bằng SQL):\n\n"
            + "\n".join(f"- `{u}`: {d[2]}" for u, d in USERS.items()))

    ids = {}
    for user, (dept, roles, _) in USERS.items():
        expect("POST", f"{AER}/auth/register", "(anonymous)", f"Đăng ký {user}", 200,
               body={"userName": user, "password": PASSWORD, "email": f"{user}@test.local",
                     "firstName": user, "lastName": "Test", "fullName": f"{user} Test"})
        uid = sql(AER_DB, f"SELECT Id FROM AppUsers WHERE UserName = '{user}'")[0].strip()
        ids[user] = uid
        dept_sql = f"'{dept}'" if dept else "NULL"
        sql(AER_DB, f"UPDATE AppUsers SET DepartmentId = {dept_sql} WHERE Id = '{uid}'; "
                    + "".join(f"INSERT INTO AppUserRoles (UserId, RoleId) VALUES ('{uid}', '{ROLES[r][0]}');" for r in roles))

    record("SQL", "-", "Gán phòng ban + role cho 10 user test", "OK", "OK", True)

    # Permissions are cached in Redis for 30 minutes; drop them so the new roles are used.
    n = redis_delete("aeration:perm:*") + redis_delete("aeration:product:*")
    record("Redis", "-", "Xoá cache quyền + cache product cũ", "OK", f"xoá {n} key", True)
    return ids


# ---------------------------------------------------------------- tests

def login(user, password=PASSWORD):
    r = http("POST", f"{AER}/auth/login", form={"UserName": user, "Password": password})
    return r


def test_auth(ids):
    section("1. Đăng nhập / token (AerationSterilize)")
    tokens, cookies = {}, {}

    expect("POST", f"{AER}/auth/register", "(anonymous)", "Đăng ký trùng username", 400,
           body={"userName": "t_manager", "password": PASSWORD, "email": "other@test.local",
                 "firstName": "x", "lastName": "x", "fullName": "x"})
    r = http("POST", f"{AER}/auth/login", form={"UserName": "admin", "Password": "sai-mat-khau"})
    record("POST :5000/auth/login", "admin", "Sai mật khẩu", "4xx", r.short(), 400 <= r.status < 500)
    r = http("POST", f"{AER}/auth/login", form={"UserName": "khong_ton_tai", "Password": "x"})
    record("POST :5000/auth/login", "khong_ton_tai", "User không tồn tại", "4xx", r.short(), 400 <= r.status < 500)

    for user in ["admin"] + list(USERS):
        r = login(user, "Admin@123" if user == "admin" else PASSWORD)
        ok = r.status == 200 and r.json and r.json.get("accessToken")
        detail = r.short()
        if ok:
            tokens[user] = r.json["accessToken"]
            cookies[user] = r.cookie("refreshToken")
            c = jwt_claims(tokens[user])
            levels = [x for x in as_list(c.get("permission_level")) if x.startswith("PRODUCT.") or x.startswith("BANK.VIEW")]
            detail += f"; department_id={c.get('department_id')}; {', '.join(sorted(levels)) or '(không có quyền PRODUCT/BANK)'}"
        record("POST :5000/auth/login", user, "Đăng nhập, xem claim trong token", "200 + token", detail, bool(ok))

    # Claim checks
    c = jwt_claims(tokens["admin"])
    record("JWT", "admin", "Quyền cũ của admin sau migration FixPermissionAccessLevel", "PRODUCT.VIEW:3",
           "có" if "PRODUCT.VIEW:3" in as_list(c.get("permission_level")) else "không có",
           "PRODUCT.VIEW:3" in as_list(c.get("permission_level")))
    c = jwt_claims(tokens["t_staff_a1"])
    record("JWT", "t_staff_a1", "Token có department_id của phòng A", DEPT_A, c.get("department_id"),
           c.get("department_id") == DEPT_A)
    c = jwt_claims(tokens["t_multi"])
    record("JWT", "t_multi", "2 role Own + Department → lấy mức cao nhất", "PRODUCT.VIEW:2",
           [x for x in as_list(c.get("permission_level")) if x.startswith("PRODUCT.VIEW")],
           "PRODUCT.VIEW:2" in as_list(c.get("permission_level")))
    c = jwt_claims(tokens["t_nodept"])
    record("JWT", "t_nodept", "User không có phòng → không có claim department_id", "không có",
           c.get("department_id") or "không có", "department_id" not in c)
    c = jwt_claims(tokens["admin"])
    record("JWT", "admin", "Issuer / Audience", "AerationSterilize", f"{c.get('iss')} / {c.get('aud')}",
           c.get("iss") == "AerationSterilize" and c.get("aud") == "AerationSterilize")

    expect("GET", f"{AER}/auth/me", "(anonymous)", "Không có token", 401)
    expect("GET", f"{AER}/auth/me", "t_staff_a1", "Có token", 200, token=tokens["t_staff_a1"])

    expect("POST", f"{AER}/auth/refresh-token", "(anonymous)", "Không có cookie refreshToken", 401)
    r = expect("POST", f"{AER}/auth/refresh-token", "t_own_a1", "Refresh bằng cookie", 200, cookie=cookies["t_own_a1"],
               check=lambda r: (bool(r.value and r.value.get("accessToken")), "nhận access token mới"),
               check_desc="access token mới")
    if r.status == 200 and r.value:
        new = r.value["accessToken"]
        c = jwt_claims(new)
        record("JWT", "t_own_a1", "Token sau refresh vẫn có department_id + quyền", f"{DEPT_A}; PRODUCT.VIEW:1",
               f"{c.get('department_id')}; {[x for x in as_list(c.get('permission_level')) if x.startswith('PRODUCT.VIEW')]}",
               c.get("department_id") == DEPT_A and "PRODUCT.VIEW:1" in as_list(c.get("permission_level")))
        tokens["t_own_a1"] = new
        cookies["t_own_a1"] = urllib.parse.quote(r.value["refreshToken"], safe="")
        record("POST :5000/auth/refresh-token", "t_own_a1", "Refresh token mới trả trong body, KHÔNG set lại cookie",
               "-", "cookie mới: " + ("có" if r.cookie("refreshToken") else "không"), True, info=True)
    r = http("POST", f"{AER}/auth/refresh-token", cookie=http_cookie_old(cookies))
    record("POST :5000/auth/refresh-token", "t_own_a1", "Dùng lại refresh token cũ (đã bị thu hồi)", "4xx", r.short(),
           400 <= r.status < 500)
    return tokens, cookies


_old_cookie = {}


def http_cookie_old(cookies):
    return _old_cookie.get("t_own_a1")


def test_product_authorization(tokens):
    section("2. Product — chặn theo quyền chức năng ([Permission])")
    body = {"name": "TEST-hack", "price": 1, "description": "x"}
    expect("GET", f"{AER}/products?sortColumn=name", "(anonymous)", "Không có token", 401)
    expect("POST", f"{AER}/products", "(anonymous)", "Tạo product không token (trước đây KHÔNG bị chặn)", 401, body=body)
    expect("PUT", f"{AER}/products/{SEEDED_PRODUCT}", "(anonymous)", "Sửa product không token (trước đây KHÔNG bị chặn)", 401, body=body)
    expect("DELETE", f"{AER}/products/{SEEDED_PRODUCT}", "(anonymous)", "Xoá product không token (trước đây KHÔNG bị chặn)", 401)
    expect("GET", f"{AER}/products?sortColumn=name", "(token rác)", "Token không hợp lệ", 401, token="abc.def.ghi")
    expect("GET", f"{AER}/products?sortColumn=name", "t_viewer", "Không có quyền PRODUCT.VIEW", 403, token=tokens["t_viewer"])
    expect("POST", f"{AER}/products", "t_viewer", "Không có quyền PRODUCT.CREATE", 403, token=tokens["t_viewer"], body=body)
    expect("GET", f"{AER}/products?sortColumn=name", "t_noaccess", "Có PRODUCT.VIEW nhưng AccessLevel = None", 403, token=tokens["t_noaccess"])
    expect("POST", f"{AER}/products", "t_manager", "Validation: tên rỗng", 400, token=tokens["t_manager"],
           body={"name": "", "price": 1, "description": "x"})
    expect("POST", f"{AER}/products", "t_manager", "Validation: giá âm", 400, token=tokens["t_manager"],
           body={"name": "TEST-x", "price": -1, "description": "x"})


def test_product_scope(tokens, ids):
    section("3. Product — phân quyền theo dữ liệu (AccessLevel)",
            "Mỗi user tạo 1 product. Sau đó từng user xem / sửa / xoá để kiểm tra phạm vi All / Department / Own.")
    creators = {"TEST-M": "t_manager", "TEST-A1": "t_staff_a1", "TEST-A2": "t_staff_a2", "TEST-B1": "t_staff_b1",
                "TEST-O1": "t_own_a1", "TEST-O2": "t_own_a2", "TEST-MULTI": "t_multi"}
    pid = {}
    for name, user in creators.items():
        r = expect("POST", f"{AER}/products", user, f"Tạo {name}", 200, token=tokens[user],
                   body={"name": name, "price": 100, "description": f"Product test của {user}"})
        pid[name] = r.value

    # Owner columns filled from the token
    rows = sql(AER_DB, "SELECT Name, CreatedBy, ISNULL(CAST(DepartmentId AS varchar(36)),'NULL') FROM Product WHERE Name LIKE 'TEST-%'")
    bad = []
    for row in rows:
        name, created_by, dept = [x.strip() for x in row.split("|")]
        user = creators[name]
        exp_dept = USERS[user][0] or "NULL"
        if created_by.lower() != ids[user].lower() or dept.lower() != exp_dept.lower():
            bad.append(name)
    record("SQL Product", "-", "CreatedBy / DepartmentId được gán theo user tạo", "đúng cho 7 product",
           "sai: " + ", ".join(bad) if bad else f"đúng cho {len(rows)} product", not bad and len(rows) == 7)

    def names(r):
        return sorted(x["name"] for x in (r.value or {}).get("items", []))

    expected_lists = {
        "admin": ["TEST-A1", "TEST-A2", "TEST-B1", "TEST-M", "TEST-MULTI", "TEST-O1", "TEST-O2"],
        "t_manager": ["TEST-A1", "TEST-A2", "TEST-B1", "TEST-M", "TEST-MULTI", "TEST-O1", "TEST-O2"],
        "t_staff_a1": ["TEST-A1", "TEST-A2", "TEST-M", "TEST-O1", "TEST-O2"],
        "t_staff_b1": ["TEST-B1", "TEST-MULTI"],
        "t_own_a1": ["TEST-O1"],
        "t_own_a2": ["TEST-O2"],
        "t_multi": ["TEST-B1", "TEST-MULTI"],
        "t_nodept": [],
    }
    scope_desc = {"admin": "All", "t_manager": "All", "t_staff_a1": "Department A", "t_staff_b1": "Department B",
                  "t_own_a1": "Own", "t_own_a2": "Own", "t_multi": "Department B (max Own/Dept)",
                  "t_nodept": "Department nhưng không có phòng"}
    url = f"{AER}/products?searchTerm=TEST-&sortColumn=name&pageSize=100"
    for user, exp in expected_lists.items():
        expect("GET", url, user, f"Danh sách, phạm vi {scope_desc[user]} (cùng query → cache key tách theo phạm vi)", 200,
               token=tokens[user], check=lambda r, e=exp: (names(r) == e, ", ".join(names(r)) or "(rỗng)"),
               check_desc=", ".join(exp) or "(rỗng)")

    all_url = f"{AER}/products?sortColumn=name&pageSize=100"
    expect("GET", all_url, "admin", "Danh sách không lọc tên: thấy cả product seed (không có chủ)", 200, token=tokens["admin"],
           check=lambda r: (SEEDED_PRODUCT in [x["id"] for x in r.value["items"]], f"{r.value['totalCount']} product"),
           check_desc="có product seed")
    expect("GET", all_url, "t_staff_a1", "Danh sách không lọc tên: KHÔNG thấy product seed (không thuộc phòng A)", 200,
           token=tokens["t_staff_a1"],
           check=lambda r: (SEEDED_PRODUCT not in [x["id"] for x in r.value["items"]], f"{r.value['totalCount']} product"),
           check_desc="không có product seed")

    def by_id(user, name, status, scenario):
        target = pid.get(name, name)
        expect("GET", f"{AER}/products/{target}", user, scenario, status, token=tokens[user])

    by_id("t_staff_a2", "TEST-A1", 200, "Xem product của đồng nghiệp cùng phòng A")
    by_id("t_staff_b1", "TEST-A1", 404, "Xem product phòng A từ phòng B")
    by_id("t_own_a1", "TEST-O1", 200, "Xem product của chính mình")
    by_id("t_own_a1", "TEST-O2", 404, "Xem product của người khác (mức Own)")
    by_id("t_nodept", "TEST-A1", 404, "User không có phòng xem product phòng A")
    by_id("t_manager", SEEDED_PRODUCT, 200, "Mức All xem product seed")
    by_id("t_staff_a1", SEEDED_PRODUCT, 404, "Mức Department xem product seed (không có phòng)")
    by_id("admin", str(uuid.uuid4()), 404, "Id không tồn tại")

    def update(user, name, status, scenario, new_name=None):
        return expect("PUT", f"{AER}/products/{pid[name]}", user, scenario, status, token=tokens[user],
                      body={"name": new_name or name, "price": 200, "description": f"Sửa bởi {user}"})

    update("t_own_a2", "TEST-O1", 404, "Sửa product của người khác (mức Own)")
    update("t_own_a1", "TEST-O1", 200, "Sửa product của chính mình")
    update("t_staff_b1", "TEST-A1", 404, "Sửa product phòng A từ phòng B")
    update("t_staff_a2", "TEST-A1", 200, "Sửa product của đồng nghiệp cùng phòng (UPDATE mức Department)", "TEST-A1-UPD")
    expect("GET", f"{AER}/products/{pid['TEST-A1']}", "t_staff_a1", "Xem lại sau khi sửa (cache đã bị xoá)", 200,
           token=tokens["t_staff_a1"], check=lambda r: (r.value["name"] == "TEST-A1-UPD", r.value["name"]),
           check_desc="TEST-A1-UPD")

    def delete(user, name, status, scenario):
        expect("DELETE", f"{AER}/products/{pid[name]}", user, scenario, status, token=tokens[user])

    delete("t_staff_a2", "TEST-A1", 404, "Xoá product của đồng nghiệp (DELETE chỉ mức Own)")
    delete("t_staff_b1", "TEST-A1", 404, "Xoá product phòng khác")
    delete("t_staff_a1", "TEST-A1", 200, "Xoá product của chính mình")
    delete("t_own_a2", "TEST-O1", 404, "Xoá product của người khác (mức Own)")
    delete("t_manager", "TEST-O1", 200, "Mức All xoá product của người khác")
    expect("GET", url, "t_staff_a1", "Danh sách phòng A sau khi xoá", 200, token=tokens["t_staff_a1"],
           check=lambda r: (names(r) == ["TEST-A2", "TEST-M", "TEST-O2"], ", ".join(names(r))),
           check_desc="TEST-A2, TEST-M, TEST-O2")


def test_permission_change(tokens, cookies, ids):
    section("4. Đổi quyền khi user đang đăng nhập",
            "Quyền nằm trong token (60 phút) và danh sách quyền được cache trên Redis 30 phút (`aeration:perm:{userId}`).")
    viewer = "t_viewer"
    sql(AER_DB, f"INSERT INTO Permissions (RoleId, FunctionId, ActionId, AccessLevel) VALUES ('{ROLES['R_VIEWER'][0]}','PRODUCT','VIEW',3)")
    record("SQL", "-", "Cấp thêm PRODUCT.VIEW mức All cho role Master Data Viewer", "OK", "OK", True)
    expect("GET", f"{AER}/products?sortColumn=name", viewer, "Token cũ (chưa có quyền mới)", 403, token=tokens[viewer])

    r = http("POST", f"{AER}/auth/refresh-token", cookie=cookies[viewer])
    new = r.value["accessToken"] if r.status == 200 else None
    status = http("GET", f"{AER}/products?sortColumn=name", token=new).status if new else None
    record("POST :5000/auth/refresh-token → GET :5000/products", viewer,
           "Refresh ngay sau khi đổi quyền (quyền cũ còn trong cache Redis)", "-",
           f"refresh {r.status}, products {status} " + ("(vẫn dùng quyền cũ trong cache)" if status == 403 else ""),
           True, info=True)

    n = redis_delete(f"aeration:perm:{ids[viewer].lower()}") + redis_delete(f"aeration:perm:{ids[viewer].upper()}")
    record("Redis", "-", "Xoá cache quyền của t_viewer", "OK", f"xoá {n} key", True)
    cookie = urllib.parse.quote(r.value["refreshToken"], safe="") if r.status == 200 else cookies[viewer]
    r = http("POST", f"{AER}/auth/refresh-token", cookie=cookie)
    new = r.value["accessToken"] if r.status == 200 else None
    expect("GET", f"{AER}/products?sortColumn=name", viewer, "Refresh lại sau khi xoá cache → có quyền mới", 200, token=new)


def test_common_auth(tokens):
    section("5. CommonServices — xác thực bằng token của Aeration")
    expect("GET", f"{COM}/banks", "(anonymous)", "Không có token", 401)
    expect("POST", f"{COM}/auth/login", "admin", "Login cũ của CommonServices đã bị bỏ", 404,
           form={"UserName": "admin", "Password": "Admin@123"})

    claims = jwt_claims(tokens["admin"])
    old = dict(claims, iss="CommonServices", aud="CommonServices", jti=str(uuid.uuid4()))
    forged = jwt_sign(old, "CommonServices_SuperSecretKey_PhaiDaiHon_32KyTu_Nhe_123456!")
    expect("GET", f"{COM}/banks", "admin (token kiểu cũ)", "Token ký bằng secret/issuer cũ của CommonServices", 401, token=forged)

    secret = json.load(open(AER_SETTINGS, encoding="utf-8-sig"))["JwtOptions"]["SecretKey"]
    expired = jwt_sign(dict(claims, exp=int(time.time()) - 60, jti=str(uuid.uuid4())), secret)
    expect("GET", f"{COM}/banks", "admin", "Token hết hạn", 401, token=expired)
    head, payload, sig = tokens["admin"].split(".")
    tampered_claims = dict(claims, permission=as_list(claims["permission"]) + ["X.Y"])
    tampered = f"{head}.{b64url(json.dumps(tampered_claims).encode())}.{sig}"
    expect("GET", f"{COM}/banks", "admin", "Token bị sửa payload (sai chữ ký)", 401, token=tampered)
    expect("GET", f"{COM}/banks", "admin", "Token của Aeration", 200, token=tokens["admin"])

    section("6. CommonServices — quyền chức năng")
    for res in ["languages", "regions", "currencies", "countries", "cities", "banks"]:
        expect("GET", f"{COM}/{res}", "t_viewer", f"Xem {res} (có quyền VIEW)", 200, token=tokens["t_viewer"])
    expect("POST", f"{COM}/banks", "t_viewer", "Tạo bank khi chỉ có quyền VIEW", 403, token=tokens["t_viewer"],
           body={"code": "X", "name": "X", "isVAByBank": False})
    expect("GET", f"{COM}/banks", "t_staff_a1", "Không có quyền BANK.VIEW", 403, token=tokens["t_staff_a1"])
    expect("GET", f"{COM}/banks", "t_noaccess", "BANK.VIEW nhưng AccessLevel = None", 403, token=tokens["t_noaccess"])
    expect("GET", f"{COM}/masterdata/banks", "(anonymous)", "MasterData không token", 401)
    for res in ["languages", "languages/native", "regions", "currencies", "countries", "cities", "banks"]:
        expect("GET", f"{COM}/masterdata/{res}", "t_staff_a1", f"MasterData/{res}: mọi user đã đăng nhập", 200,
               token=tokens["t_staff_a1"])


def test_common_crud(tokens):
    section("7. CommonServices — CRUD master data (admin)",
            f"Mã dữ liệu test có hậu tố `{RUN}` để chạy lại không bị trùng (xoá là xoá mềm, mã vẫn giữ unique).")
    t = tokens["admin"]
    ids = {}

    def crud(res, create_body, update_body, label):
        r = expect("POST", f"{COM}/{res}", "admin", f"Tạo {label}", 200, token=t, body=create_body)
        rid = r.value
        ids[res] = rid
        if not rid:
            return None
        expect("GET", f"{COM}/{res}/{rid}", "admin", f"Xem {label} theo id", 200, token=t)
        expect("PUT", f"{COM}/{res}/{rid}", "admin", f"Sửa {label}", 200, token=t, body=update_body,
               check=lambda r: (r.value and r.value.get("name") == update_body["name"], r.value and r.value.get("name")),
               check_desc=update_body["name"])
        expect("PUT", f"{COM}/{res}/{rid}/deactivate", "admin", f"Ngừng kích hoạt {label}", 200, token=t)
        expect("GET", f"{COM}/{res}/list?status=0", "admin", f"Danh sách {label} trạng thái Inactive", 200, token=t,
               check=lambda r: (rid in [x["id"] for x in r.value], "có" if rid in [x["id"] for x in r.value] else "không có"),
               check_desc="có bản ghi vừa ngừng")
        expect("PUT", f"{COM}/{res}/{rid}/activate", "admin", f"Kích hoạt lại {label}", 200, token=t)
        expect("GET", f"{COM}/{res}?pageSize=100", "admin", f"Danh sách phân trang {label}", 200, token=t,
               check=lambda r: (rid in [x["id"] for x in r.value["items"]], f"{r.value['totalCount']} bản ghi"),
               check_desc="có bản ghi mới")
        return rid

    tr = lambda vi, en: [{"langCode": "vi-VN", "name": vi}, {"langCode": "en-US", "name": en}]
    crud("regions", {"code": f"R{RUN}", "name": f"Vùng test {RUN}", "note": "test", "sortOrder": 9},
         {"code": f"R{RUN}", "name": f"Vùng test {RUN} (sửa)", "note": "test", "sortOrder": 9}, "region")
    crud("currencies", {"code": f"C{RUN}", "name": f"Tiền test {RUN}", "symbol": "T", "isCurrencyOptionEnabled": True},
         {"code": f"C{RUN}", "name": f"Tiền test {RUN} (sửa)", "symbol": "T", "isCurrencyOptionEnabled": False}, "currency")
    crud("countries", {"code": f"N{RUN}", "name": f"Nước test {RUN}", "regionId": ids.get("regions"), "currencyId": ids.get("currencies")},
         {"code": f"N{RUN}", "name": f"Nước test {RUN} (sửa)", "regionId": ids.get("regions"), "currencyId": ids.get("currencies")}, "country")
    crud("cities", {"code": f"T{RUN}", "name": f"Thành phố test {RUN}", "countryId": ids.get("countries")},
         {"code": f"T{RUN}", "name": f"Thành phố test {RUN} (sửa)", "countryId": ids.get("countries")}, "city")
    crud("languages", {"code": f"t-{RUN}", "name": f"Ngôn ngữ test {RUN}", "platform": 99},
         {"code": f"t-{RUN}", "name": f"Ngôn ngữ test {RUN} (sửa)", "platform": 1}, "language")
    crud("banks", {"code": f"B{RUN}", "name": f"Ngân hàng test {RUN}", "vaPrefix": "99", "isVAByBank": True,
                   "translations": tr(f"Ngân hàng test {RUN}", f"Test bank {RUN}")},
         {"code": f"B{RUN}", "name": f"Ngân hàng test {RUN} (sửa)", "isVAByBank": False,
          "translations": tr(f"Ngân hàng test {RUN} (sửa)", f"Test bank {RUN} (edited)")}, "bank")

    if ids.get("banks"):
        expect("GET", f"{COM}/banks/list", "admin", "Accept-Language: en-US → tên tiếng Anh", 200, token=t, lang="en-US",
               check=lambda r: (any(x["name"] == f"Test bank {RUN} (edited)" for x in r.value),
                                next((x["name"] for x in r.value if x["id"] == ids["banks"]), "?")),
               check_desc=f"Test bank {RUN} (edited)")
        created_by = sql(COM_DB, f"SELECT CreatedBy FROM Banks WHERE Id = '{ids['banks']}'")[0].strip()
        record("SQL Banks", "admin", "CreatedBy lấy từ token Aeration (user id của admin)", ADMIN_ID, created_by,
               created_by.lower() == ADMIN_ID)
    expect("POST", f"{COM}/banks", "admin", "Tạo bank trùng mã", 409 if False else 400, token=t,
           body={"code": f"B{RUN}", "name": "trùng", "isVAByBank": False})
    expect("POST", f"{COM}/banks", "admin", "Validation: mã + tên rỗng", 400, token=t, body={"code": "", "name": ""})
    expect("POST", f"{COM}/cities", "admin", "Tạo city với country không tồn tại", 404, token=t,
           body={"code": f"X{RUN}", "name": "x", "countryId": str(uuid.uuid4())})

    for res, label in [("cities", "city"), ("countries", "country"), ("currencies", "currency"), ("regions", "region"),
                       ("languages", "language"), ("banks", "bank")]:
        if ids.get(res):
            expect("DELETE", f"{COM}/{res}/{ids[res]}", "admin", f"Xoá {label}", 200, token=t)
            expect("GET", f"{COM}/{res}/{ids[res]}", "admin", f"Xem {label} sau khi xoá", 404, token=t)


def test_logout():
    section("8. Logout — thu hồi token trên cả 2 service")
    r = login("t_manager")
    token, cookie = r.json["accessToken"], r.cookie("refreshToken")
    expect("GET", f"{AER}/products?sortColumn=name", "t_manager", "Trước logout", 200, token=token)
    expect("GET", f"{COM}/banks", "t_manager", "Trước logout (CommonServices)", 200, token=token)
    expect("POST", f"{AER}/auth/logout", "(anonymous)", "Logout không có token", 401, cookie=cookie)
    expect("POST", f"{AER}/auth/logout", "t_manager", "Logout", 200, token=token, cookie=cookie)
    expect("GET", f"{AER}/products?sortColumn=name", "t_manager", "Token đã logout gọi Aeration", 401, token=token)
    expect("GET", f"{COM}/banks", "t_manager", "Token đã logout gọi CommonServices (danh sách thu hồi dùng chung)", 401, token=token)
    r = http("POST", f"{AER}/auth/refresh-token", cookie=cookie)
    record("POST :5000/auth/refresh-token", "t_manager", "Refresh bằng cookie đã logout", "4xx", r.short(), 400 <= r.status < 500)


def finish():
    total = stats["pass"] + stats["fail"]
    summary = (f"## Tổng kết\n\n- Tổng số test: **{total}** — ✅ đạt **{stats['pass']}**, ❌ lỗi **{stats['fail']}**"
               f"{', ℹ️ ghi nhận ' + str(stats['info']) if stats['info'] else ''}\n"
               f"- Ký hiệu: ✅ đúng như mong đợi, ❌ sai, ℹ️ ghi nhận hành vi (không tính đạt/lỗi)\n")
    content = open(REPORT, encoding="utf-8").read().replace("<!-- SUMMARY -->\n", summary)
    open(REPORT, "w", encoding="utf-8").write(content)
    print(f"\nPASS {stats['pass']}  FAIL {stats['fail']}  INFO {stats['info']}  -> {REPORT}")


def main():
    open(REPORT, "w", encoding="utf-8").close()
    ids = setup()
    tokens, cookies = None, None
    try:
        # Keep the original refresh cookie of t_own_a1 to check that a used refresh token is rejected.
        tokens, cookies = test_auth_with_old_cookie(ids)
        test_product_authorization(tokens)
        test_product_scope(tokens, ids)
        test_permission_change(tokens, cookies, ids)
        test_common_auth(tokens)
        test_common_crud(tokens)
        test_logout()
    except Exception as ex:  # keep what was written so far and show where it stopped
        record("-", "-", "Script dừng do lỗi", "-", f"{type(ex).__name__}: {ex}", False)
        raise
    finally:
        finish()


def test_auth_with_old_cookie(ids):
    original = test_auth.__code__  # noqa: F841 (documentation only)
    r = login("t_own_a1")
    _old_cookie["t_own_a1"] = r.cookie("refreshToken")
    tokens, cookies = test_auth(ids)
    return tokens, cookies


if __name__ == "__main__":
    main()
9:26 PM
Mình đang dọn lại vài chỗ rối trong script trước khi chạy:



Bash Simplify refresh-token reuse test and fix expectations
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/tests/ApiTests && python - <<'EOF'
p='run_api_tests.py'
s=open(p,encoding='utf-8').read()
def rep(a,b):
    global s
    assert a in s,a; s=s.replace(a,b,1)
rep('''    expect("POST", f"{AER}/auth/refresh-token", "(anonymous)", "Không có cookie refreshToken", 401)
    r = expect(''','''    expect("POST", f"{AER}/auth/refresh-token", "(anonymous)", "Không có cookie refreshToken", 401)
    used_cookie = cookies["t_own_a1"]
    r = expect(''')
rep('''    r = http("POST", f"{AER}/auth/refresh-token", cookie=http_cookie_old(cookies))''','''    r = http("POST", f"{AER}/auth/refresh-token", cookie=used_cookie)''')
rep('''_old_cookie = {}


def http_cookie_old(cookies):
    return _old_cookie.get("t_own_a1")


''','')
rep('''        # Keep the original refresh cookie of t_own_a1 to check that a used refresh token is rejected.
        tokens, cookies = test_auth_with_old_cookie(ids)''','''        tokens, cookies = test_auth(ids)''')
rep('''def test_auth_with_old_cookie(ids):
    original = test_auth.__code__  # noqa: F841 (documentation only)
    r = login("t_own_a1")
    _old_cookie["t_own_a1"] = r.cookie("refreshToken")
    tokens, cookies = test_auth(ids)
    return tokens, cookies


''','')
rep('"Tạo bank trùng mã", 409 if False else 400,','"Tạo bank trùng mã", 400,')
open(p,'w',encoding='utf-8').write(s)
EOF
python -c "import ast;ast.parse(open('run_api_tests.py',encoding='utf-8').read());print('syntax ok')"

OUT
syntax ok
9:26 PM
Chạy test:



Bash Run API test suite, show non-passing lines
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase && PYTHONIOENCODING=utf-8 python -I tests/ApiTests/run_api_tests.py 2>&1 | grep -vE "^✅" | tail -40

OUT
Traceback (most recent call last):
  File "C:\Users\DuyQuang\Desktop\SourceCodeBase\tests\ApiTests\run_api_tests.py", line 615, in <module>
    main()
  File "C:\Users\DuyQuang\Desktop\SourceCodeBase\tests\ApiTests\run_api_tests.py", line 597, in main
    ids = setup()
          ^^^^^^^
  File "C:\Users\DuyQuang\Desktop\SourceCodeBase\tests\ApiTests\run_api_tests.py", line 251, in setup
    expect("POST", f"{AER}/auth/register", "(anonymous)", f"\u0110\u0103ng k� {user}", 200,
  File "C:\Users\DuyQuang\Desktop\SourceCodeBase\tests\ApiTests\run_api_tests.py", line 168, in expect
    record(f"{method} {url.replace(AER, ':5000').replace(COM, ':5001')}", user, scenario, expected, actual, ok)
  File "C:\Users\DuyQuang\Desktop\SourceCodeBase\tests\ApiTests\run_api_tests.py", line 98, in record
    print(f"{mark} {counter[0]:3} {api} [{user}] {scenario} -> {actual}")
  File "C:\Users\DuyQuang\AppData\Local\Programs\Python\Python311\Lib\encodings\cp1252.py", line 19, in encode
    return codecs.charmap_encode(input,self.errors,encoding_table)[0]
           ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
UnicodeEncodeError: 'charmap' codec can't encode character '\u2705' in position 0: character maps to <undefined>
Bash Fix console encoding and rerun tests
IN
cd /c/Users/DuyQuang/Desktop/SourceCodeBase/tests/ApiTests && python - <<'EOF'
p='run_api_tests.py'
s=open(p,encoding='utf-8').read()
s=s.replace('import subprocess\n','import subprocess\nimport sys\n',1)
s=s.replace('def main():\n','def main():\n    sys.stdout.reconfigure(encoding="utf-8")\n',1)
open(p,'w',encoding='utf-8').write(s)
EOF
wc -l run_api_tests.py; cd /c/Users/DuyQuang/Desktop/SourceCodeBase && python -I tests/ApiTests/run_api_tests.py 2>&1 | grep -vE "^✅" | tail -40

OUT
617 run_api_tests.py
ℹ️  37 POST :5000/auth/refresh-token [t_own_a1] Refresh token mới trả trong body, KHÔNG set lại cookie -> cookie mới: không
ℹ️  88 POST :5000/auth/refresh-token → GET :5000/products [t_viewer] Refresh ngay sau khi đổi quyền (quyền cũ còn trong cache Redis) -> refresh 200, products 403 (vẫn dùng quyền cũ trong cache)

PASS 177  FAIL 0  INFO 2  -> C:\Users\DuyQuang\Desktop\SourceCodeBase\API_TEST_REPORT.md
9:26 PM
Cả 177 test đều đạt. Mình xem lại file script và báo cáo để chắc kết quả không bị "đạt giả":



Bash Spot-check script formatting and report rows
IN
sed -n 1,12p tests/ApiTests/run_api_tests.py | cat -A | cut -c1-60 | head -12; head -12 API_TEST_REPORT.md; grep -E "trùng|không tồn tại|en-US|CreatedBy|seed|hết hạn|cũ của|logout|None" API_TEST_REPORT.md | cut -c1-260

OUT
"""$
End-to-end API test: AerationSterilize (identity, products) 
$
Requires both APIs running (http://localhost:5000, http://lo
and the Redis container "aeration-redis". Every test result 
(solution root) as soon as it runs.$
$
    python tests/ApiTests/run_api_tests.py$
"""$
import base64$
import datetime$
import hashlib$
# Báo cáo test API

- Thời gian chạy: 2026-10-15 21:26:40
- AerationSterilize: `http://localhost:5000/api/v1` (DB `SourceBase`) — đăng nhập, cấp token, phân quyền, Product
- CommonServices: `http://localhost:5001/api/v1` (DB `CommonServices`) — master data, chỉ kiểm tra token của Aeration
- Redis: container `aeration-redis` (cache + danh sách token đã thu hồi)
- Script: `tests/ApiTests/run_api_tests.py` (chạy lại được, tự dọn dữ liệu test cũ)

## Tổng kết

- Tổng số test: **177** — ✅ đạt **177**, ❌ lỗi **0**, ℹ️ ghi nhận 2
- Ký hiệu: ✅ đúng như mong đợi, ❌ sai, ℹ️ ghi nhận hành vi (không tính đạt/lỗi)
Role test (bảng `AppRoles` + `Permissions`, AccessLevel: 0 None, 1 Own, 2 Department, 3 All):
- `t_noaccess`: PRODUCT.VIEW và BANK.VIEW nhưng AccessLevel = None
| 13 | `POST :5000/auth/register` | (anonymous) | Đăng ký trùng username | 400 | 400 (Bad Request) | ✅ |
| 15 | `POST :5000/auth/login` | khong_ton_tai | User không tồn tại | 4xx | 400 | ✅ |
| 16 | `POST :5000/auth/login` | admin | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=None; BANK.VIEW:3, PRODUCT.ACTIVATE:3, PRODUCT.CREATE:3, PRODUCT.DELETE:3, PRODUCT.INACTIVATE:3, PRODUCT.UPDATE:3, PRODUCT.VIEW:3 | ✅ |
| 25 | `POST :5000/auth/login` | t_nodept | Đăng nhập, xem claim trong token | 200 + token | 200; department_id=None; PRODUCT.CREATE:2, PRODUCT.DELETE:1, PRODUCT.UPDATE:2, PRODUCT.VIEW:2 | ✅ |
| 27 | `JWT` | admin | Quyền cũ của admin sau migration FixPermissionAccessLevel | PRODUCT.VIEW:3 | có | ✅ |
| 46 | `GET :5000/products?sortColumn=name` | t_noaccess | Có PRODUCT.VIEW nhưng AccessLevel = None | 403 | 403 | ✅ |
| 56 | `SQL Product` | - | CreatedBy / DepartmentId được gán theo user tạo | đúng cho 7 product | đúng cho 7 product | ✅ |
| 65 | `GET :5000/products?sortColumn=name&pageSize=100` | admin | Danh sách không lọc tên: thấy cả product seed (không có chủ) | 200; có product seed | 200; 11 product | ✅ |
| 66 | `GET :5000/products?sortColumn=name&pageSize=100` | t_staff_a1 | Danh sách không lọc tên: KHÔNG thấy product seed (không thuộc phòng A) | 200; không có product seed | 200; 5 product | ✅ |
| 72 | `GET :5000/products/aaaaaaaa-0001-0000-0000-000000000001` | t_manager | Mức All xem product seed | 200 | 200 | ✅ |
| 73 | `GET :5000/products/aaaaaaaa-0001-0000-0000-000000000001` | t_staff_a1 | Mức Department xem product seed (không có phòng) | 404 | 404 | ✅ |
| 74 | `GET :5000/products/50e31718-76aa-472e-9777-ff338d8e483a` | admin | Id không tồn tại | 404 | 404 | ✅ |
| 92 | `POST :5001/auth/login` | admin | Login cũ của CommonServices đã bị bỏ | 404 | 404 | ✅ |
| 93 | `GET :5001/banks` | admin (token kiểu cũ) | Token ký bằng secret/issuer cũ của CommonServices | 401 | 401 | ✅ |
| 94 | `GET :5001/banks` | admin | Token hết hạn | 401 | 401 | ✅ |
| 105 | `GET :5001/banks` | t_noaccess | BANK.VIEW nhưng AccessLevel = None | 403 | 403 | ✅ |
Mã dữ liệu test có hậu tố `212640` để chạy lại không bị trùng (xoá là xoá mềm, mã vẫn giữ unique).
| 156 | `GET :5001/banks/list` | admin | Accept-Language: en-US → tên tiếng Anh | 200; Test bank 212640 (edited) | 200; Test bank 212640 (edited) | ✅ |
| 157 | `SQL Banks` | admin | CreatedBy lấy từ token Aeration (user id của admin) | 33333333-3333-3333-3333-333333333333 | 33333333-3333-3333-3333-333333333333 | ✅ |
| 158 | `POST :5001/banks` | admin | Tạo bank trùng mã | 400 | 400 (Bad Request) | ✅ |
| 160 | `POST :5001/cities` | admin | Tạo city với country không tồn tại | 404 | 404 | ✅ |
| 173 | `GET :5000/products?sortColumn=name` | t_manager | Trước logout | 200 | 200 | ✅ |
| 174 | `GET :5001/banks` | t_manager | Trước logout (CommonServices) | 200 | 200 | ✅ |
| 175 | `POST :5000/auth/logout` | (anonymous) | Logout không có token | 401 | 401 | ✅ |
| 176 | `POST :5000/auth/logout` | t_manager | Logout | 200 | 200 | ✅ |
| 177 | `GET :5000/products?sortColumn=name` | t_manager | Token đã logout gọi Aeration | 401 | 401 | ✅ |
| 178 | `GET :5001/banks` | t_manager | Token đã logout gọi CommonServices (danh sách thu hồi dùng chung) | 401 | 401 | ✅ |
| 179 | `POST :5000/auth/refresh-token` | t_manager | Refresh bằng cookie đã logout | 4xx | 400 | ✅ |
9:26 PM
Chạy lại lần 2 để chắc script chạy lặp được (tự dọn dữ liệu cũ):



Bash Re-run tests to confirm repeatability
IN
python -I tests/ApiTests/run_api_tests.py 2>&1 | grep -E "^❌|PASS|Traceback"; git status --short | grep -vE "^ M|^D |^RM|^M " 

OUT
PASS 177  FAIL 0  INFO 2  -> C:\Users\DuyQuang\Desktop\SourceCodeBase\API_TEST_REPORT.md
?? API_TEST_REPORT.md
?? src/BuildingBlocks/Contracts/Identity/DataScope.cs
?? src/BuildingBlocks/Infrastructure/Extensions/AuthenticationServiceCollectionExtensions.cs
?? src/Services/AerationSterilize/AerationSterilize.Persistence/Migrations/20261015142238_FixPermissionAccessLevel.Designer.cs
?? src/Services/AerationSterilize/AerationSterilize.Persistence/Migrations/20261015142238_FixPermissionAccessLevel.cs
?? src/Services/CommonServices/CommonServices.Persistence/Migrations/20261015140950_RemoveIdentity.Designer.cs
?? src/Services/CommonServices/CommonServices.Persistence/Migrations/20261015140950_RemoveIdentity.cs
?? tests/ApiTests/
9:27 PM
Mình đã chạy cả 2 service, tạo bảng, nhập dữ liệu mẫu và test hết các API. Kết quả: 177 test đạt, 0 lỗi, cộng 2 dòng chỉ ghi nhận hành vi (không tính đạt/lỗi). Chạy lại lần 2 cũng ra y hệt. Báo cáo từng API nằm ở API_TEST_REPORT.md ở thư mục solution; mỗi test chạy xong là ghi ngay một dòng vào file.

Chạy và tạo bảng

Mình bật Docker Desktop và container Redis aeration-redis từ docker-compose.yml.
Aeration chạy ở cổng 5000, CommonServices ở cổng 5001.
DB CommonServices đã áp migration RemoveIdentity, giờ chỉ còn bảng master data.
Aeration seed thêm các function master data và quyền tương ứng cho admin.
Lỗi tìm ra khi test, đã sửa: token của admin có PRODUCT.VIEW:0, tức 20 quyền cũ đều ở mức None. Nguyên nhân là migration AddDataScopeColumns thêm cột AccessLevel với giá trị mặc định 0. Lỗi này có từ trước nhưng bị che vì handler cũ không kiểm tra mức quyền, nên khi handler mới chặn mức None thì admin bị 403 ở Product. Mình thêm migration FixPermissionAccessLevel, đặt các dòng đó về mức All (3); migration đã chạy trên DB SourceBase.

Dữ liệu mẫu

5 role:
Product Manager: PRODUCT mức All, xem được master data.
Department Staff: PRODUCT mức Department, riêng xoá chỉ mức Own.
Own Staff: PRODUCT mức Own.
Master Data Viewer: chỉ xem master data.
No Access: có quyền nhưng mức None.
10 user test ở 2 phòng A và B, gồm cả user không có phòng và user có 2 role. Mật khẩu chung là Test@123.
Các nhóm test trong báo cáo

Đăng nhập / token: register, login đúng và sai, kiểm tra claim trong token (department_id, mức quyền, user 2 role lấy mức cao nhất), /me, refresh token, dùng lại refresh token cũ.
Chặn theo quyền chức năng ở Product: tạo/sửa/xoá không có token giờ trả 401; không có quyền hoặc mức None trả 403; validation.
Lọc theo AccessLevel: 7 user cùng gọi một query danh sách, mỗi người nhận đúng phạm vi All / Department / Own. Có kiểm tra xem/sửa/xoá chéo phòng, và CreatedBy/DepartmentId được gán đúng trong DB.
Đổi quyền khi user đang đăng nhập: xem phần lưu ý bên dưới.
CommonServices xác thực bằng token Aeration: từ chối token ký bằng key cũ của CommonServices, token hết hạn, token bị sửa; /auth/login cũ trả 404.
CommonServices phân quyền: quyền VIEW và CREATE; các API MasterData chỉ cần đăng nhập.
CRUD đủ 6 loại master data (region, currency, country, city, language, bank): tạo, xem, sửa, ngừng/kích hoạt, xoá. Có thêm test tên tiếng Anh theo Accept-Language, mã trùng, và CreatedBy lấy đúng user id từ token Aeration.
Logout: token đã logout bị từ chối ở cả 2 service.
Hai hành vi nên biết (dòng ℹ️ trong báo cáo)

Đổi quyền của user rồi refresh token ngay thì vẫn dùng quyền cũ, vì quyền được cache trên Redis 30 phút. Phải xoá key aeration:perm:{userId} thì refresh mới nhận quyền mới.
API refresh-token trả refresh token mới trong body nhưng không set lại cookie. Client dùng cookie sẽ bị lỗi ở lần refresh thứ hai.
Bạn muốn mình sửa 2 điểm này luôn không?

Script test nằm ở tests/ApiTests/run_api_tests.py; chạy lại bằng python tests/ApiTests/run_api_tests.py. Hiện 2 service và Docker vẫn đang chạy, và mình chưa commit gì.



API_TEST_REPORT.md
phân quyền bảng permission tao thấy có level rồi.
SELECT TOP (1000) [RoleId]
      ,[FunctionId]
      ,[ActionId]
      ,[AccessLevel]
  FROM [SourceBase].[dbo].[Permissions]
tao hỏi.
giờ mày giải thích cho tao cách hoạt động.
phân quyền dữ liệu là thế nào.
giờ nếu tao 1 product, hay 1 order thì thế nào.
phân kiểu gì,
Show more
9:32 PM
Mỗi dòng trong bảng Permissions trả lời 2 câu hỏi:

RoleId + FunctionId + ActionId: role nào được làm hành động nào trên chức năng nào. Ví dụ: "Nhân viên" được "VIEW" "PRODUCT". Đây là quyền chức năng.
AccessLevel: làm hành động đó trên những bản ghi nào. Đây là quyền dữ liệu.
AccessLevel	Ý nghĩa	Điều kiện lọc
0 None	Có dòng quyền nhưng không được gì (bị 403)	—
1 Own	Chỉ bản ghi mình tạo	CreatedBy = userId
2 Department	Bản ghi của phòng mình	DepartmentId = phòng của user
3 All	Tất cả	không lọc
Ví dụ với role "Nhân viên kinh doanh":


RoleId  FunctionId  ActionId  AccessLevel
NVKD    PRODUCT     VIEW      2   → xem product của cả phòng
NVKD    PRODUCT     UPDATE    2   → sửa product của cả phòng
NVKD    PRODUCT     DELETE    1   → chỉ xoá product do chính mình tạo
Một user có nhiều role thì mỗi quyền lấy mức cao nhất (ví dụ Own + Department → Department).

Cách hoạt động: 2 thời điểm
1. Lúc đăng nhập
Aeration đọc các role của user, ghép với bảng Permissions rồi ghi kết quả vào token:


permission       = "PRODUCT.VIEW"       ← có quyền hay không
permission_level = "PRODUCT.VIEW:2"     ← mức dữ liệu
department_id    = "d000...0a"          ← phòng của user (bảng AppUsers)
sub              = userId
Từ đó không service nào phải đọc lại DB quyền, kể cả CommonServices; tất cả đọc từ token.

2. Lúc gọi API: 2 lớp kiểm tra
Lớp 1, quyền chức năng. Attribute đặt trên controller:


[Permission(FunctionCode.PRODUCT, CommandCode.VIEW)]
Token không có PRODUCT.VIEW, hoặc có nhưng mức None, thì trả 403 và không vào được handler.

Lớp 2, quyền dữ liệu. Trong handler, lấy phạm vi của user:


var scope = _currentUser.GetDataScope(FunctionCode.PRODUCT, CommandCode.VIEW);
// scope = { Level = Department, UserId = ..., DepartmentId = phòng A }
Sau đó dùng phạm vi này theo 2 cách:

Lấy danh sách: query.ApplyDataScope(scope) tự thêm WHERE theo bảng ở trên.
Thao tác 1 bản ghi (xem/sửa/xoá theo id): scope.Covers(entity). Nếu ngoài phạm vi thì trả 404, không trả 403, để user không biết bản ghi đó có tồn tại.
Điều kiện để lọc được: bản ghi phải biết "chủ" của nó
Entity cần 2 cột qua interface IDataOwned:


public Guid? CreatedBy { get; set; }     // ai tạo
public Guid? DepartmentId { get; set; }  // phòng của người tạo, lưu lúc tạo
Hai cột này gán lúc tạo từ token của người tạo và giữ nguyên về sau. Nhân viên chuyển phòng thì các bản ghi cũ vẫn thuộc phòng cũ.

Ví dụ thực tế (đúng với dữ liệu đã test)
Phòng A có staff_a1, own_a1; phòng B có staff_b1. Sản phẩm:

Product	CreatedBy	Dept
P1	staff_a1	A
P2	own_a1	A
P3	staff_b1	B
User	Mức VIEW	Thấy
manager	All	P1, P2, P3
staff_a1	Department	P1, P2
staff_b1	Department	P3
own_a1	Own	P2
Áp dụng cho Order (hoặc bất kỳ bảng nào mới)
Bước 1: Thêm mã chức năng. Trong FunctionCode.cs:


public const string ORDER = "ORDER";
Sau đó thêm ORDER vào seed của Aeration (bảng Functions, ActionInFunctions) và cấp quyền cho các role trong Permissions, kèm AccessLevel mong muốn.

Bước 2: Entity implement IDataOwned.


public class Order : EntityAuditBase<Guid>, IDataOwned
{
    ...
    public Guid? CreatedBy { get; set; }
    public Guid? DepartmentId { get; set; }
}
Tạo migration để thêm 2 cột.

Bước 3: Gán chủ khi tạo (CreateOrderCommandHandler):


order.CreatedBy = _currentUser.UserId;
order.DepartmentId = _currentUser.DepartmentId;
Bước 4: Controller gắn [Permission] cho mọi API.


[Permission(FunctionCode.ORDER, CommandCode.VIEW)]   // GET
[Permission(FunctionCode.ORDER, CommandCode.CREATE)] // POST
[Permission(FunctionCode.ORDER, CommandCode.UPDATE)] // PUT
[Permission(FunctionCode.ORDER, CommandCode.DELETE)] // DELETE
Bước 5: Lọc trong handler.


// Danh sách
var query = _orderRepository.FindAll().ApplyDataScope(request.Scope);

// Sửa / xoá / xem 1 bản ghi
var order = await _orderRepository.FindByIdAsync(id);
var scope = _currentUser.GetDataScope(FunctionCode.ORDER, CommandCode.UPDATE);
if (order is null || !scope.Covers(order))
    throw new OrderNotFoundException(id);
Bước 6: Nếu query có cache, cho phạm vi vào cache key. Đây là bước dễ quên nhất. Truyền DataScope vào query và đưa Scope.CacheKey vào key:


public sealed record GetOrdersQuery(..., DataScope Scope) : ICachedQuery<...>
{
    string ICachedQuery.CacheKey => OrderCacheKeys.List(Scope.CacheKey, ...);
}
Nếu quên, user mức Own có thể nhận kết quả đã được cache cho người mức All. Xem cách làm mẫu ở Product: GetProductsQuery.cs và ProductsController.cs.

Khi nào không cần quyền dữ liệu
Dữ liệu dùng chung, không thuộc về ai, như master data ở CommonServices (ngân hàng, tỉnh thành…), thì chỉ cần lớp 1 ([Permission]). Lúc đó AccessLevel chỉ cần khác None; mức 1, 2 hay 3 cho kết quả như nhau.

Lưu ý vận hành
Quyền nằm trong token (60 phút) và được cache trên Redis 30 phút. Sửa bảng Permissions thì user phải login lại, và cần xoá key aeration:perm:{userId} thì quyền mới có hiệu lực ngay.
Đổi phòng của user (AppUsers.DepartmentId) cũng chỉ có hiệu lực từ token sau.
Nếu bạn muốn, mình có thể làm luôn module Order theo đúng 6 bước trên làm mẫu.



[Permission(FunctionCode.ORDER, CommandCode.VIEW)]   // GET
[Permission(FunctionCode.ORDER, CommandCode.CREATE)] // POST
[Permission(FunctionCode.ORDER, CommandCode.UPDATE)] // PUT
[Permission(FunctionCode.ORDER, CommandCode.DELETE)] // DELETE
tại sao ghi lắm thế này,
đã có quyền delete thì phải có các quyền khác. ngắn hơn dược khong
Show more
9:37 PM
4 dòng đó không đặt chồng lên một API. Mỗi dòng nằm trên một API khác nhau, nên mỗi API thật ra chỉ có 1 dòng:


[HttpGet]    [Permission(ORDER, VIEW)]   public GetOrders()
[HttpPost]   [Permission(ORDER, CREATE)] public CreateOrder()
[HttpPut]    [Permission(ORDER, UPDATE)] public UpdateOrder()
[HttpDelete] [Permission(ORDER, DELETE)] public DeleteOrder()
Mỗi API cần đúng 1 quyền, nên số dòng đã là tối thiểu.

"Có DELETE thì phải có các quyền khác"
Không nên cài cứng điều này trong code, vì nghiệp vụ thường không xếp quyền theo bậc thang:

Role	Cần quyền
Nhân viên nhập liệu	VIEW + CREATE + UPDATE, không DELETE
Kế toán	VIEW + DELETE (huỷ đơn sai), không CREATE
Quản lý	VIEW + ACTIVATE (duyệt đơn), không sửa
Nếu code quy định "DELETE kéo theo tất cả" thì không cấu hình được role Kế toán ở trên. Muốn một role có đủ quyền thì cứ cấp đủ các dòng trong bảng Permissions. Đó là dữ liệu, đổi lúc nào cũng được mà không phải sửa code.

Có một quy tắc hợp lý mà bạn có thể muốn: có CREATE/UPDATE/DELETE thì tự có VIEW, vì sửa được thứ mình không xem được là vô lý. Cách làm đúng là cấp đủ dòng trong Permissions, hoặc cho seed/màn hình phân quyền tự thêm dòng VIEW, không phải sửa code kiểm tra.

Nếu muốn controller ngắn hơn
Có thể đặt 1 dòng trên cả controller rồi suy ra quyền từ HTTP method:


[PermissionFunction(FunctionCode.ORDER)]   // GET→VIEW, POST→CREATE, PUT→UPDATE, DELETE→DELETE
public class OrdersController : ApiController
{
    [HttpGet]    public GetOrders() ...
    [HttpPost]   public CreateOrder() ...
    [HttpPut]    public UpdateOrder() ...
    [HttpDelete] public DeleteOrder() ...

    [HttpPut("{id}/activate")]
    [Permission(FunctionCode.ORDER, CommandCode.ACTIVATE)]   // trường hợp đặc biệt vẫn ghi rõ
    public Activate() ...
}
Lợi: gọn, không quên gắn quyền cho API mới (lỗ hổng Product lúc trước chính là do quên).
Hại: nhìn vào API không thấy ngay nó cần quyền gì. Các API PUT kiểu activate, deactivate, approve sẽ bị suy ra thành UPDATE nếu quên ghi đè, tức là dễ cấp nhầm quyền.
Khuyến nghị của mình: giữ cách ghi rõ từng API như hiện tại. Mỗi API chỉ 1 dòng, đọc vào là biết. Để không còn chuyện quên gắn quyền, nên thêm một test kiến trúc quét mọi controller và báo lỗi nếu có API nào thiếu [Permission] (hoặc thiếu [AllowAnonymous] một cách cố ý). Project đã có sẵn tests/AerationSterilize.Architecture.Tests nhưng đang trống, có thể viết test đó vào đây.

Bạn muốn làm cách nào: viết test kiến trúc, hay chuyển sang cách suy quyền từ HTTP method?



ok. 
giờ tổng hợp lại nội dung đoạn chat này vào 1 file chat_claude.md đi
Write C:\Users\DuyQuang\Desktop\SourceCodeBase\chat_claude.md
96 lines
# Tổng hợp: gom đăng nhập / phân quyền về AerationSterilize

Ngày: 2026-10-15 — nhánh `add-common-services` (chưa commit)

## 1. Bỏ phần đăng nhập / phân quyền bị lặp ở CommonServices

**Vấn đề:** CommonServices có riêng login, user, role, permission, giống hệt AerationSterilize.

**Cách làm:** AerationSterilize là nơi duy nhất đăng nhập và cấp token (token chứa sẵn quyền). CommonServices chỉ kiểm tra token của Aeration và đọc quyền từ token.

**Đã xoá khỏi CommonServices**
- `AuthController`, toàn bộ `Features/V1/Auth` (login, register, refresh-token, logout).
- Entity `AppUser`, `AppRole`, `Permission`, `Function`, `Action`, `ActionInFunction`, `UserSession`, các cấu hình EF tương ứng, `PermissionService`, `AuthException`.
- `ApplicationDbContext` đổi từ `IdentityDbContext` sang `DbContext`. Bỏ `AddIdentity`, bỏ seed user/role/permission, bỏ package Identity.
- Migration `RemoveIdentity`: drop 12 bảng identity, giữ nguyên bảng master data.

**Gom code dùng chung vào BuildingBlocks/Infrastructure**
- `Authorization/PermissionAttribute`, `PermissionRequirement`, `PermissionAuthorizationHandler`: trước đây copy y hệt ở 2 service.
- `Extensions/AuthenticationServiceCollectionExtensions.cs`: `AddJwtAuthentication()` và `AddPermissionAuthorization()`, cả 2 service đều gọi.

**Sửa để chạy được giữa 2 service**
- Danh sách token đã thu hồi (logout) trên Redis trước đây bị gắn tiền tố khác nhau (`aeration:` và `common:`), nên CommonServices không thấy token đã logout. Giờ key dùng chung, không còn tiền tố (`RedisTokenRevocationService`).
- `AddRedisCache` không còn bắt buộc service phải có `IPermissionService`.
- Seed của Aeration thêm các function master data (MASTER_DATA, LANGUAGE, REGION, CURRENCY, COUNTRY, CITY, BANK) và 2 action ACTIVATE/INACTIVATE. Seed giờ chỉ thêm những gì còn thiếu, nên DB đang chạy cũng được bổ sung.
- `appsettings.json` của CommonServices: `JwtOptions` (SecretKey, Issuer, Audience) phải giống Aeration. File này bị gitignore nên mỗi môi trường phải tự cấu hình.

## 2. Kiểm tra và sửa phân quyền theo dữ liệu (AccessLevel)

**Lỗi tìm thấy ở Aeration và cách sửa**

| # | Lỗi | Sửa |
|---|---|---|
| 1 | API tạo/sửa/xoá Product không có `[Permission]`, ai cũng gọi được | Thêm `[Permission(PRODUCT, CREATE/UPDATE/DELETE)]` |
| 2 | Token không có `department_id`, nên mức Department lọc sai | Login và refresh ghi `department_id` vào token (`UserClaim.DepartmentId`) |
| 3 | `ApplyDataScope` không được gọi ở đâu cả | `DataScope` + `ApplyDataScope` + `Covers` chuyển sang `Contracts/Identity/DataScope.cs` và dùng trong mọi handler Product |
| 4 | `Product.CreatedBy` và `DepartmentId` mặc định là `Guid.NewGuid()` | Bỏ giá trị mặc định; gán theo user hiện tại khi tạo |
| 5 | Quyền mức None vẫn qua được `[Permission]` | Handler chặn mức None |
| 6 | Cache query Product dùng chung giữa các user | Cache key có thêm phạm vi của user (`product:own:{userId}:...`) |
| 7 | Migration cũ thêm cột `AccessLevel` với mặc định 0, nên 20 quyền cũ thành None (phát hiện khi test) | Migration `FixPermissionAccessLevel`: đặt các dòng đó về 3 (All) |

CommonServices không cần lọc theo AccessLevel vì master data là danh mục dùng chung; chỉ cần `[Permission]`.

## 3. Chạy và test toàn bộ

- Bật Docker và Redis (`docker-compose.yml`). Aeration chạy ở cổng 5000, CommonServices ở cổng 5001. Migrate và seed tự động khi khởi động.
- Script: `tests/ApiTests/run_api_tests.py`. Chạy lại được; tự tạo 5 role và 10 user test (mật khẩu `Test@123`, phòng A/B, có cả user không phòng và user 2 role).
- Kết quả: **177 đạt, 0 lỗi, 2 ghi nhận**. Chi tiết từng API ở `API_TEST_REPORT.md`.

**Hai hành vi nên biết (chưa sửa)**
- Đổi quyền rồi refresh token ngay vẫn dùng quyền cũ, vì quyền được cache trên Redis 30 phút (`aeration:perm:{userId}`).
- API refresh-token trả refresh token mới trong body nhưng không set lại cookie, nên client dùng cookie sẽ lỗi ở lần refresh thứ hai.

## 4. Cách phân quyền hoạt động

**Bảng `Permissions`:** `RoleId + FunctionId + ActionId` cho biết được làm gì (quyền chức năng). `AccessLevel` cho biết làm trên những bản ghi nào (quyền dữ liệu).

| AccessLevel | Ý nghĩa | Điều kiện lọc |
|---|---|---|
| 0 None | Không được gì (403) | — |
| 1 Own | Bản ghi mình tạo | `CreatedBy = userId` |
| 2 Department | Bản ghi của phòng mình | `DepartmentId = phòng của user` |
| 3 All | Tất cả | không lọc |

User có nhiều role thì mỗi quyền lấy mức cao nhất.

**Luồng chạy**
1. **Đăng nhập:** Aeration ghép role của user với `Permissions` rồi ghi vào token các claim `permission` (`PRODUCT.VIEW`), `permission_level` (`PRODUCT.VIEW:2`), `department_id`, `sub` (userId).
2. **Gọi API:**
   - Lớp 1, `[Permission(Function, Action)]`: không có quyền hoặc mức None thì trả 403.
   - Lớp 2, trong handler: `_currentUser.GetDataScope(...)`. Danh sách dùng `ApplyDataScope(scope)`; xem/sửa/xoá 1 bản ghi dùng `scope.Covers(entity)`, ngoài phạm vi thì trả 404.
3. Bản ghi phải implement `IDataOwned` (`CreatedBy`, `DepartmentId`), gán lúc tạo từ token của người tạo.

**Áp dụng cho bảng mới (ví dụ Order)**
1. Thêm `FunctionCode.ORDER`, rồi seed vào `Functions` và `ActionInFunctions`, và cấp quyền trong `Permissions`.
2. Entity `Order : IDataOwned`, kèm migration thêm 2 cột.
3. Khi tạo, gán `CreatedBy = _currentUser.UserId`, `DepartmentId = _currentUser.DepartmentId`.
4. Mỗi API gắn đúng 1 `[Permission(FunctionCode.ORDER, ...)]`.
5. Handler lọc bằng `ApplyDataScope` / `Covers`.
6. Query có cache thì truyền `DataScope` vào query và đưa `Scope.CacheKey` vào cache key.

**Lưu ý vận hành:** quyền nằm trong token (60 phút) và cache Redis (30 phút). Sửa `Permissions` hoặc đổi phòng của user thì cần login lại; muốn có hiệu lực ngay thì xoá thêm key `aeration:perm:{userId}`.

## 5. Về việc ghi nhiều `[Permission]`

- Mỗi API chỉ có 1 dòng `[Permission]`; 4 dòng là của 4 API khác nhau, không phải đặt chồng.
- Không nên cài cứng "có DELETE thì có tất cả", vì nghiệp vụ thường không theo bậc thang (ví dụ kế toán được xoá nhưng không được tạo). Muốn role có đủ quyền thì cấp đủ dòng trong `Permissions`.
- Có thể rút gọn bằng cách suy quyền từ HTTP method (`[PermissionFunction(ORDER)]` trên controller). Cách này gọn hơn nhưng dễ cấp nhầm quyền cho các API PUT đặc biệt (activate, approve).
- **Khuyến nghị:** giữ cách ghi rõ từng API, và thêm test kiến trúc (trong `tests/AerationSterilize.Architecture.Tests`) báo lỗi khi có API thiếu `[Permission]`.

## 6. File thay đổi chính

- BuildingBlocks: `Infrastructure/Authorization/*`, `Infrastructure/Extensions/AuthenticationServiceCollectionExtensions.cs`, `Infrastructure/Identity/RedisTokenRevocationService.cs`, `Infrastructure/Identity/TokenService.cs`, `Contracts/Identity/DataScope.cs`, `Shared/DTOs/Identity/TokenRequest.cs`, `Shared/Common/Constants/Authorization/FunctionCode.cs` (`UserClaim`).
- AerationSterilize: `ProductsController`, `CurrentUser`, `ICurrentUser`, các handler/query của Product, `Product.cs`, `ApplicationContextSeed.cs`, migration `FixPermissionAccessLevel`.
- CommonServices: xoá phần identity/auth, `ApplicationDbContext`, seed, DI, migration `RemoveIdentity`.
- Test: `tests/ApiTests/run_api_tests.py`, `API_TEST_REPORT.md`.
9:38 PM
Mình hiểu là file này để bạn (và team) đọc lại sau. Đã ghi vào chat_claude.md ở thư mục solution, gồm 6 phần:

Bỏ login/phân quyền bị lặp ở CommonServices và gom code dùng chung vào BuildingBlocks.
Các lỗi về AccessLevel tìm thấy ở Aeration và cách đã sửa, kể cả lỗi migration làm quyền cũ thành mức None.
Kết quả chạy và test: 177 đạt, 0 lỗi, kèm 2 hành vi chưa sửa.
Cách phân quyền hoạt động và 6 bước áp dụng cho bảng mới như Order.
Lý do mỗi API ghi một dòng [Permission] và khuyến nghị thêm test kiến trúc.
Danh sách các file đã thay đổi.
Toàn bộ thay đổi vẫn chưa commit.



