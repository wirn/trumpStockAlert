using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.Services;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCors";
var allowedCorsOrigins = GetAllowedCorsOrigins(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type", "X-TrumpStockAlert-Scheduler-Key");
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing. Configure it in appsettings.json for local development or ConnectionStrings__DefaultConnection in Azure.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"));

    options.EnableDetailedErrors();

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});
builder.Services.AddScoped<ITruthPostService, TruthPostService>();
builder.Services.AddScoped<MockMarketImpactAnalyzer>();
builder.Services.AddScoped<OpenAiMarketImpactAnalyzer>();
builder.Services.AddScoped<IMarketImpactAnalyzer>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var provider = configuration["Analyzer:Provider"];

    if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
    {
        return serviceProvider.GetRequiredService<OpenAiMarketImpactAnalyzer>();
    }

    return serviceProvider.GetRequiredService<MockMarketImpactAnalyzer>();
});
builder.Services.AddScoped<IPostAnalysisRunner, PostAnalysisRunner>();
builder.Services.AddScoped<ICollectorProcessRunner, CollectorProcessRunner>();
builder.Services.AddScoped<ICollectorTestRunner, CollectorTestRunner>();
builder.Services.AddScoped<ICollectorRunner, CollectorRunner>();
builder.Services.AddHttpClient<ITruthSocialCollectorClient, TruthSocialCollectorClient>(
    TruthSocialCollectorClient.ConfigureHttpClient);
builder.Services.AddSingleton<MarketImpactPromptBuilder>();
builder.Services.AddSingleton<MarketImpactAiResponseParser>();

var app = builder.Build();

app.Logger.LogInformation(
    "Configured PostgreSQL provider. Apply migrations with 'dotnet ef database update' before running in a new environment.");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(FrontendCorsPolicy);
app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "TrumpStockAlert.Api",
    timestampUtc = DateTimeOffset.UtcNow
}));

app.MapControllers();

app.Run();

static string[] GetAllowedCorsOrigins(IConfiguration configuration)
{
    var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (configuredOrigins is { Length: > 0 })
    {
        return configuredOrigins
            .SelectMany(SplitOrigins)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    var configuredOriginList = configuration["Cors:AllowedOrigins"];
    var origins = SplitOrigins(configuredOriginList).ToArray();
    if (origins.Length > 0)
    {
        return origins;
    }

    return
    [
        "http://100.92.230.97:5173",
        "http://localhost:5173"
    ];
}

static IEnumerable<string> SplitOrigins(string? value)
{
    return (value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(origin => !string.IsNullOrWhiteSpace(origin));
}
