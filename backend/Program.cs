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
builder.Services.AddSingleton<IOpenAiChatCompletionClient, OpenAiChatCompletionClient>();
builder.Services.AddScoped<IMarketImpactAnalyzer>(AnalyzerProviderSelector.Select);
builder.Services.AddScoped<IPostAnalysisRunner, PostAnalysisRunner>();
builder.Services.AddScoped<ICollectorProcessRunner, CollectorProcessRunner>();
builder.Services.AddScoped<ICollectorTestRunner, CollectorTestRunner>();
builder.Services.AddScoped<ICollectorRunner, CollectorRunner>();
builder.Services.AddScoped<IFetcherRunService, FetcherRunService>();
builder.Services.Configure<AlertSettings>(builder.Configuration.GetSection(AlertSettings.SectionName));
builder.Services.AddScoped<IAlertEvaluator, AlertEvaluator>();
builder.Services.AddScoped<IEmailSender, LogOnlyEmailSender>();
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
    var origins = configuration["Cors:AllowedOrigins"];

    if (!string.IsNullOrWhiteSpace(origins))
    {
        return origins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    return
    [
        "http://100.92.230.97:5173",
        "http://localhost:5173"
    ];
}

public partial class Program { }
