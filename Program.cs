using System.Text.RegularExpressions;
using System.ClientModel;
using System.ClientModel.Primitives;
using Azure;
using Azure.AI.OpenAI;
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
// Fallback to gpt5-nano if deployment name isn't explicitly configured for faster responses
var aoaiDeployment = aoaiSection["DeploymentName"] ?? "gpt5-nano";
var aoaiApiKey     = aoaiSection["ApiKey"]         ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not configured.");
var aoaiApiVersion = aoaiSection["ApiVersion"]     ?? "2024-12-01-preview";

// ─── Semantic Kernel ─────────────────────────────────────────────────────────

// Build an AzureOpenAIClient using the custom policy
var azureClientOptions = new AzureOpenAIClientOptions();
azureClientOptions.AddPolicy(new ApiVersionPolicy(aoaiApiVersion), PipelinePosition.PerCall);

var azureClient = new AzureOpenAIClient(
    new Uri(aoaiEndpoint),
    new System.ClientModel.ApiKeyCredential(aoaiApiKey),
    azureClientOptions);

var kernelBuilder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(
        deploymentName:  aoaiDeployment,
        azureOpenAIClient: azureClient);

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


// Custom policy to force a specific API version for Azure OpenAI,
// bypassing the enum limitations of the currently installed Azure SDK.
public class ApiVersionPolicy : PipelinePolicy
{
    private readonly string _apiVersion;
    public ApiVersionPolicy(string apiVersion) => _apiVersion = apiVersion;

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        FixUri(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        FixUri(message);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }

    private void FixUri(PipelineMessage message)
    {
        var uri = message.Request.Uri.ToString();
        var fixedUri = Regex.Replace(uri, @"api-version=[^&]*", "api-version=" + _apiVersion);
        if (!fixedUri.Contains("api-version="))
        {
            fixedUri += (fixedUri.Contains("?") ? "&" : "?") + "api-version=" + _apiVersion;
        }
        message.Request.Uri = new Uri(fixedUri);
    }
}
