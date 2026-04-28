using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing. Configure it in appsettings.json for local development or ConnectionStrings__DefaultConnection in Azure.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        connectionString,
        sqlServerOptions =>
        {
            sqlServerOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sqlServerOptions.CommandTimeout(30);
        });

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
builder.Services.AddSingleton<MarketImpactPromptBuilder>();
builder.Services.AddSingleton<MarketImpactAiResponseParser>();

var app = builder.Build();

app.Logger.LogInformation(
    "Configured SQL Server provider. Apply migrations with 'dotnet ef database update' before running in a new environment.");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("FrontendDev");
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
