using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;

namespace FantasyFootballForecast.Tests;

public sealed class AvailabilityParserTests
{
    [Test]
    public void Enrich_MapsInjuryKeywords()
    {
        var service = new RuleBasedAvailabilityEnrichmentService();

        var result = service.Enrich("Official Club", "https://example.com", "Player is injured and will be ruled out for the weekend.");

        Assert.That(result.Status, Is.EqualTo(AvailabilityStatus.Injured));
        Assert.That(result.InjuryFlag, Is.True);
        Assert.That(result.Confidence, Is.GreaterThan(0.9m));
    }

    [Test]
    public void Enrich_MapsSuspensionKeywords()
    {
        var service = new RuleBasedAvailabilityEnrichmentService();

        var result = service.Enrich("Official Club", "https://example.com", "The player is suspended after a red card.");

        Assert.That(result.Status, Is.EqualTo(AvailabilityStatus.Suspended));
        Assert.That(result.SuspensionFlag, Is.True);
    }
}
