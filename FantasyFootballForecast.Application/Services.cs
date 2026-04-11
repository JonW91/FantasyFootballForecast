using FantasyFootballForecast.Domain;
using System.Text.RegularExpressions;

namespace FantasyFootballForecast.Application;

public sealed class RuleBasedAvailabilityEnrichmentService : IAvailabilityEnrichmentService
{
    private static readonly (string[] Keywords, AvailabilityStatus Status, bool Injury, bool Suspension, decimal Chance, decimal Confidence)[] Rules =
    [
        (["ruled out", "out for", "broken", "hamstring", "injured", "injury"], AvailabilityStatus.Injured, true, false, 0.05m, 0.96m),
        (["suspended", "ban", "banned", "suspension", "red card"], AvailabilityStatus.Suspended, false, true, 0.00m, 0.97m),
        (["doubtful", "major doubt", "late fitness test", "fitness test"], AvailabilityStatus.Doubtful, true, false, 0.35m, 0.84m),
        (["available", "fit", "returned to training", "back in training"], AvailabilityStatus.Available, false, false, 0.92m, 0.79m),
        (["rested", "rotation risk"], AvailabilityStatus.Rested, false, false, 0.70m, 0.63m)
    ];

    public AvailabilityEnrichmentResult Enrich(string sourceName, string? sourceUrl, string rawText, DateTimeOffset? publishedUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawText);

        var normalized = Regex.Replace(rawText.ToLowerInvariant(), @"\s+", " ");
        foreach (var rule in Rules)
        {
            var hit = rule.Keywords.FirstOrDefault(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                var expectedReturn = rule.Status switch
                {
                    AvailabilityStatus.Injured => "Monitor training updates and official team news.",
                    AvailabilityStatus.Suspended => "Suspension confirmed until eligibility returns.",
                    AvailabilityStatus.Doubtful => "Late fitness call expected before deadline.",
                    AvailabilityStatus.Rested => "May return via rotation or bench role.",
                    _ => null
                };

                return new AvailabilityEnrichmentResult(
                    rule.Status,
                    rule.Injury,
                    rule.Suspension,
                    rule.Chance,
                    expectedReturn,
                    rule.Confidence,
                    hit,
                    rawText);
            }
        }

        return new AvailabilityEnrichmentResult(
            AvailabilityStatus.Unknown,
            false,
            false,
            0.50m,
            null,
            0.35m,
            null,
            rawText);
    }
}

public sealed class FantasyRecommendationService : IFantasyRecommendationService
{
    private readonly IApplicationDbContext _db;

    public FantasyRecommendationService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FantasyPickDto>> GetTopPicksAsync(int count, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 25);

        var rows = _db.Players
            .Join(_db.Teams,
                player => player.TeamId,
                team => team.Id,
                (player, team) => new { player, team })
            .OrderByDescending(joined => joined.player.RecentPoints + joined.player.Goals * 4 + joined.player.Assists * 3 - joined.player.RedCards * 3)
            .Take(count)
            .ToList();

        var players = rows.Select(joined => new FantasyPickDto(
            joined.player.Id,
            joined.player.Name,
            joined.team.Name,
            joined.player.Position,
            Math.Round(joined.player.RecentPoints + joined.player.Goals * 4 + joined.player.Assists * 3, 2),
            joined.player.Price,
            joined.player.Price == 0 ? 0 : Math.Round((joined.player.RecentPoints + 1) / joined.player.Price, 2),
            joined.player.ChanceOfPlayingNextRound,
            joined.player.AvailabilityStatus == AvailabilityStatus.Injured || joined.player.AvailabilityStatus == AvailabilityStatus.RuledOut,
            joined.player.AvailabilityStatus == AvailabilityStatus.Suspended,
            BuildReason(joined.player)))
            .ToList();

        return await Task.FromResult(players);
    }

    public async Task<IReadOnlyList<FantasyPickDto>> GetBestXIAsync(CancellationToken cancellationToken = default)
    {
        var allRows = _db.Players
            .Join(_db.Teams,
                player => player.TeamId,
                team => team.Id,
                (player, team) => new { player, team })
            .OrderByDescending(joined => joined.player.RecentPoints + joined.player.Goals * 4 + joined.player.Assists * 3 - joined.player.RedCards * 3)
            .ToList();

        var selected = new List<FantasyPickDto>(11);

        foreach (var (position, slots) in new[] { ("GK", 1), ("DEF", 4), ("MID", 4), ("FWD", 2) })
        {
            var picks = allRows
                .Where(joined => joined.player.Position.Equals(position, StringComparison.OrdinalIgnoreCase))
                .Take(slots)
                .Select(joined => new FantasyPickDto(
                    joined.player.Id,
                    joined.player.Name,
                    joined.team.Name,
                    joined.player.Position,
                    Math.Round(joined.player.RecentPoints + joined.player.Goals * 4 + joined.player.Assists * 3, 2),
                    joined.player.Price,
                    joined.player.Price == 0 ? 0 : Math.Round((joined.player.RecentPoints + 1) / joined.player.Price, 2),
                    joined.player.ChanceOfPlayingNextRound,
                    joined.player.AvailabilityStatus == AvailabilityStatus.Injured || joined.player.AvailabilityStatus == AvailabilityStatus.RuledOut,
                    joined.player.AvailabilityStatus == AvailabilityStatus.Suspended,
                    BuildReason(joined.player)));
            selected.AddRange(picks);
        }

        return await Task.FromResult(selected);
    }

    private static string BuildReason(Domain.Player player)
    {
        if (player.AvailabilityStatus is AvailabilityStatus.Suspended)
        {
            return "Not available because of a suspension.";
        }

        if (player.AvailabilityStatus is AvailabilityStatus.Injured or AvailabilityStatus.RuledOut)
        {
            return "Availability risk is too high for the next round.";
        }

        var value = player.Price == 0 ? 0 : (player.RecentPoints + 1) / player.Price;
        return value >= 5 ? "Strong value based on recent form." : "Solid recent form and acceptable price.";
    }
}
