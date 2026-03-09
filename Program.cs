using Microsoft.SemanticKernel;
using Serilog;
using AffiliatesAPI.Infrastructure;
using AffiliatesAPI.Orchestration;
using AffiliatesAPI.Skills;

// ─── Serilog structured logging ──────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ─── Configuration ────────────────────────────────────────────────────────────
var config         = builder.Configuration;
var aoaiSection    = config.GetSection("AzureOpenAI");
var aoaiEndpoint   = aoaiSection["Endpoint"]       ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured.");
var aoaiDeployment = aoaiSection["DeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is not configured.");
var aoaiApiKey     = aoaiSection["ApiKey"]         ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not configured.");

// ─── Semantic Kernel ─────────────────────────────────────────────────────────
var kernelBuilder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(
        deploymentName: aoaiDeployment,
        endpoint:       aoaiEndpoint,
        apiKey:         aoaiApiKey);

// Register AffiliatesFaqSkill — no database dependency required.
builder.Services.AddSingleton<AffiliatesFaqSkill>();

builder.Services.AddSingleton(sp =>
{
    var skill  = sp.GetRequiredService<AffiliatesFaqSkill>();
    var kernel = kernelBuilder.Build();

    // Register the AffiliatesFaqSkill as a Semantic Kernel plugin.
    // The kernel will auto-invoke its functions when the LLM decides they're needed.
    kernel.Plugins.AddFromObject(skill, "AffiliatesFaqSkill");

    return kernel;
});

// ─── Application Services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IChatOrchestrator, ChatOrchestrator>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()));

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<LoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

