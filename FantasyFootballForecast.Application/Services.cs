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

        var fdrByTeam = BuildTeamFixtureDifficulty();

        var rows = _db.Players
            .Join(_db.Teams,
                player => player.TeamId,
                team => team.Id,
                (player, team) => new { player, team })
            .OrderByDescending(joined => joined.player.RecentPoints + joined.player.Goals * 4 + joined.player.Assists * 3 - joined.player.RedCards * 3)
            .Take(Math.Min(count * 5, 50))
            .ToList();

        var players = rows
            .Select(joined =>
            {
                var fdr = fdrByTeam.TryGetValue(joined.player.TeamId, out var d) ? (int?)d : null;
                var rawScore = joined.player.RecentPoints + joined.player.Goals * 4 + joined.player.Assists * 3;
                var adjustedScore = Math.Round(ApplyFdrMultiplier(rawScore, fdr), 2);
                return new FantasyPickDto(
                    joined.player.Id,
                    joined.player.Name,
                    joined.team.Name,
                    joined.player.Position,
                    adjustedScore,
                    joined.player.Price,
                    joined.player.Price == 0 ? 0 : Math.Round((adjustedScore + 1) / joined.player.Price, 2),
                    joined.player.ChanceOfPlayingNextRound,
                    joined.player.AvailabilityStatus == AvailabilityStatus.Injured || joined.player.AvailabilityStatus == AvailabilityStatus.RuledOut,
                    joined.player.AvailabilityStatus == AvailabilityStatus.Suspended,
                    BuildReason(joined.player, fdr));
            })
            .OrderByDescending(pick => pick.PredictedPoints)
            .Take(count)
            .ToList();

        return await Task.FromResult(players);
    }

    public async Task<IReadOnlyList<FantasyPickDto>> GetBestXIAsync(CancellationToken cancellationToken = default)
    {
        var fdrByTeam = BuildTeamFixtureDifficulty();

        var allRows = _db.Players
            .Join(_db.Teams,
                player => player.TeamId,
                team => team.Id,
                (player, team) => new { player, team })
            .ToList()
            .Select(joined =>
            {
                var fdr = fdrByTeam.TryGetValue(joined.player.TeamId, out var d) ? (int?)d : null;
                var rawScore = joined.player.RecentPoints + joined.player.Goals * 4 + joined.player.Assists * 3;
                var adjustedScore = Math.Round(ApplyFdrMultiplier(rawScore, fdr), 2);
                return new
                {
                    joined.player,
                    joined.team,
                    fdr,
                    adjustedScore
                };
            })
            .OrderByDescending(item => item.adjustedScore - item.player.RedCards * 3)
            .ToList();

        var selected = new List<FantasyPickDto>(11);

        foreach (var (position, slots) in new[] { ("GK", 1), ("DEF", 4), ("MID", 4), ("FWD", 2) })
        {
            var picks = allRows
                .Where(item => item.player.Position.Equals(position, StringComparison.OrdinalIgnoreCase))
                .Take(slots)
                .Select(item => new FantasyPickDto(
                    item.player.Id,
                    item.player.Name,
                    item.team.Name,
                    item.player.Position,
                    item.adjustedScore,
                    item.player.Price,
                    item.player.Price == 0 ? 0 : Math.Round((item.adjustedScore + 1) / item.player.Price, 2),
                    item.player.ChanceOfPlayingNextRound,
                    item.player.AvailabilityStatus == AvailabilityStatus.Injured || item.player.AvailabilityStatus == AvailabilityStatus.RuledOut,
                    item.player.AvailabilityStatus == AvailabilityStatus.Suspended,
                    BuildReason(item.player, item.fdr)));
            selected.AddRange(picks);
        }

        return await Task.FromResult(selected);
    }

    private Dictionary<int, int> BuildTeamFixtureDifficulty()
    {
        var upcomingFixtures = _db.Fixtures
            .Where(fixture => !fixture.IsFinished)
            .Join(_db.Teams, fixture => fixture.HomeTeamId, team => team.Id, (fixture, home) => new { fixture, home })
            .Join(_db.Teams, joined => joined.fixture.AwayTeamId, team => team.Id, (joined, away) => new
            {
                HomeTeamId = joined.home.Id,
                AwayTeamId = away.Id,
                HomeOpponentStrength = away.StrengthRating,
                AwayOpponentStrength = joined.home.StrengthRating,
                joined.fixture.KickoffUtc
            })
            .OrderBy(item => item.KickoffUtc)
            .ToList();

        var result = new Dictionary<int, int>();
        foreach (var item in upcomingFixtures)
        {
            if (!result.ContainsKey(item.HomeTeamId))
            {
                result[item.HomeTeamId] = CalculateDifficulty(item.HomeOpponentStrength, isHome: true);
            }

            if (!result.ContainsKey(item.AwayTeamId))
            {
                result[item.AwayTeamId] = CalculateDifficulty(item.AwayOpponentStrength, isHome: false);
            }
        }

        return result;
    }

    private static int CalculateDifficulty(decimal opponentStrength, bool isHome)
    {
        var baseDifficulty = opponentStrength switch
        {
            < 60 => 1,
            < 70 => 2,
            < 80 => 3,
            < 90 => 4,
            _ => 5
        };
        var modifier = isHome ? -1 : 1;
        return Math.Clamp(baseDifficulty + modifier, 1, 5);
    }

    private static decimal ApplyFdrMultiplier(decimal baseScore, int? fdr) => fdr switch
    {
        1 or 2 => baseScore * 1.10m,
        4 => baseScore * 0.90m,
        5 => baseScore * 0.80m,
        _ => baseScore
    };

    private static string BuildReason(Domain.Player player, int? fdr = null)
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

        return fdr switch
        {
            1 or 2 => value >= 5 ? "Excellent fixture and strong value." : "Easy fixture ahead, good pick.",
            4 or 5 => value >= 5 ? "Good value despite a tough fixture." : "Tough fixture ahead, consider alternatives.",
            _ => value >= 5 ? "Strong value based on recent form." : "Solid recent form and acceptable price."
        };
    }
}
