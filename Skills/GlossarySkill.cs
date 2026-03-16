using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace AffiliatesAPI.Skills;

/// <summary>
/// Glossary Skill.
/// Provides definitions for common terms used in the affiliate marketing and gambling industry.
/// </summary>
public class GlossarySkill
{
    private readonly ILogger<GlossarySkill> _logger;

    private readonly Dictionary<string, string> _glossary = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Affiliate", "A person or organization officially connected to a larger entity, often for a business or marketing purpose." },
        { "Affiliate Marketer", "A person or business that promotes products or services for another company and earns a commission for sales or leads generated." },
        { "BSB", "BSB (Bank State Branch) A six digit code used in Australia to identify individual bank branches for domestic transactions." },
        { "Bank State Branch", "BSB (Bank State Branch) A six digit code used in Australia to identify individual bank branches for domestic transactions." },
        { "Btag", "Btag (brand tag) Parameter added to affiliate URLs to track traffic source, user behavior and conversions. Affiliate networks and advertisers use it to: Identify the affiliate or campaign that generated the click, Attribute sales or leads accurately, Analyze performance across traffic sources." },
        { "brand tag", "Btag (brand tag) Parameter added to affiliate URLs to track traffic source, user behavior and conversions. Affiliate networks and advertisers use it to: Identify the affiliate or campaign that generated the click, Attribute sales or leads accurately, Analyze performance across traffic sources." },
        { "CPA", "CPA (Cost per affiliate/acquisition) - A commission earning method wherein the affiliate marketer is paid once off per acquisition based on baselines defined." },
        { "Cost per affiliate", "CPA (Cost per affiliate/acquisition) - A commission earning method wherein the affiliate marketer is paid once off per acquisition based on baselines defined." },
        { "Cost per acquisition", "CPA (Cost per affiliate/acquisition) - A commission earning method wherein the affiliate marketer is paid once off per acquisition based on baselines defined." },
        { "Epic", "Epic- As per BET PMO (Project Management Office) this is the container for the project itself. The epic is then broken down into user stories or tasks." },
        { "FTD", "FTD (First Time Deposit) - The first deposit made by a punter. This may not necessarily be the first deposit, but the first deposit that meets a defined baseline." },
        { "First Time Deposit", "FTD (First Time Deposit) - The first deposit made by a punter. This may not necessarily be the first deposit, but the first deposit that meets a defined baseline." },
        { "IBAN", "IBAN (International Bank Account Number) A standardized international format for identifying bank accounts across borders. It helps ensure accurate and efficient processing of international payments." },
        { "International Bank Account Number", "IBAN (International Bank Account Number) A standardized international format for identifying bank accounts across borders. It helps ensure accurate and efficient processing of international payments." },
        { "RevShare", "RevShare - A commission method wherein the affiliate marketer is paid monthly per acquisition based on baselines defined and percentage of revenue." },
        { "Revenue Share", "RevShare - A commission method wherein the affiliate marketer is paid monthly per acquisition based on baselines defined and percentage of revenue." },
        { "Swift Code", "Swift Code - A unique identifier used to specify a particular bank in international financial transactions. AKA BIC- Bank Identifier Code." },
        { "BIC", "Swift Code - A unique identifier used to specify a particular bank in international financial transactions. AKA BIC- Bank Identifier Code." },
        { "Bank Identifier Code", "Swift Code - A unique identifier used to specify a particular bank in international financial transactions. AKA BIC- Bank Identifier Code." },
        { "UTM", "UTM (Urchin Tracking Module) - UTM Parameters are key-value pairs added to the end of a URL. Parameters added to URLs to help track the effectiveness of online marketing campaigns across traffic sources and mediums. They allow analytics tools to capture detailed information about how users arrived at a website." },
        { "Urchin Tracking Module", "UTM (Urchin Tracking Module) - UTM Parameters are key-value pairs added to the end of a URL. Parameters added to URLs to help track the effectiveness of online marketing campaigns across traffic sources and mediums. They allow analytics tools to capture detailed information about how users arrived at a website." }
    };

    public GlossarySkill(ILogger<GlossarySkill> logger)
    {
        _logger = logger;
    }

    [KernelFunction]
    [Description("Gets the definition for a specific affiliate or business term from the glossary. Call this when the user asks what a specific term or acronym means (e.g., CPA, FTD, RevShare, Btag).")]
    public Task<string> GetGlossaryDefinition([Description("The term or acronym to look up.")] string term)
    {
        _logger.LogInformation("GetGlossaryDefinition called | Term={Term}", term);

        if (string.IsNullOrWhiteSpace(term))
        {
            return Task.FromResult("Please provide a term to look up.");
        }

        // Exact match first
        if (_glossary.TryGetValue(term, out var definition))
        {
            return Task.FromResult($"**{term}**: {definition}");
        }

        // Try partial match if exact match fails
        var partialMatch = _glossary.FirstOrDefault(k => 
            k.Key.Contains(term, StringComparison.OrdinalIgnoreCase) || 
            term.Contains(k.Key, StringComparison.OrdinalIgnoreCase));

        if (partialMatch.Key != null)
        {
             return Task.FromResult($"**{partialMatch.Key}**: {partialMatch.Value}");
        }

        return Task.FromResult($"I don't have a specific definition for '{term}' in my glossary.");
    }
}
