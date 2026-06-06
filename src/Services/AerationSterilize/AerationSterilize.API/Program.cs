using AerationSterilize.API.DependencyInjection.Extensions;
using AerationSterilize.Application.DependencyInjection.Extensions;
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

