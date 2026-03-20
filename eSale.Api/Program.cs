using eSale.Api.Middleware;
using eSale.Application;
using eSale.Application.Common.Interfaces;
using eSale.Infrastructure;
using eSale.Infrastructure.Persistence;
using Hangfire;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var runDbInitialization = builder.Configuration.GetValue("Infrastructure:RunDbInitialization", true);
var seqServerUrl = builder.Configuration["Seq:ServerUrl"];

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/esale-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            shared: true);

    if (!string.IsNullOrWhiteSpace(seqServerUrl))
    {
        configuration.WriteTo.Seq(serverUrl: seqServerUrl);
    }
});

builder.Services.AddApplication();
builder.Services.AddInfrastructureProduction(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();

var app = builder.Build();

if (runDbInitialization)
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbInitializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await dbInitializer.ApplyMigrationsAsync();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<TenantMiddleware>();
app.UseHttpsRedirection();
app.UseHangfireDashboard("/hangfire");
app.MapControllers();

app.Run();
