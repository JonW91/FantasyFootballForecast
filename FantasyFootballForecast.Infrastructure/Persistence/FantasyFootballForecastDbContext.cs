using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FantasyFootballForecast.Infrastructure.Persistence;

public sealed class FantasyFootballForecastDbContext : DbContext, IApplicationDbContext
{
    public FantasyFootballForecastDbContext(DbContextOptions<FantasyFootballForecastDbContext> options)
        : base(options)
    {
    }

    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Gameweek> Gameweeks => Set<Gameweek>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Fixture> Fixtures => Set<Fixture>();
    public DbSet<PlayerMatchStat> PlayerMatchStats => Set<PlayerMatchStat>();
    public DbSet<TeamMatchStat> TeamMatchStats => Set<TeamMatchStat>();
    public DbSet<FantasyPlayerPrice> FantasyPlayerPrices => Set<FantasyPlayerPrice>();
    public DbSet<FantasyPlayerOwnership> FantasyPlayerOwnerships => Set<FantasyPlayerOwnership>();
    public DbSet<PlayerAvailability> PlayerAvailabilities => Set<PlayerAvailability>();
    public DbSet<InjuryReport> InjuryReports => Set<InjuryReport>();
    public DbSet<Suspension> Suspensions => Set<Suspension>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<ModelTrainingRun> ModelTrainingRuns => Set<ModelTrainingRun>();
    public DbSet<DataIngestionRun> DataIngestionRuns => Set<DataIngestionRun>();

    IQueryable<Season> IApplicationDbContext.Seasons => Seasons;
    IQueryable<Gameweek> IApplicationDbContext.Gameweeks => Gameweeks;
    IQueryable<Team> IApplicationDbContext.Teams => Teams;
    IQueryable<Player> IApplicationDbContext.Players => Players;
    IQueryable<Fixture> IApplicationDbContext.Fixtures => Fixtures;
    IQueryable<PlayerMatchStat> IApplicationDbContext.PlayerMatchStats => PlayerMatchStats;
    IQueryable<TeamMatchStat> IApplicationDbContext.TeamMatchStats => TeamMatchStats;
    IQueryable<FantasyPlayerPrice> IApplicationDbContext.FantasyPlayerPrices => FantasyPlayerPrices;
    IQueryable<FantasyPlayerOwnership> IApplicationDbContext.FantasyPlayerOwnerships => FantasyPlayerOwnerships;
    IQueryable<PlayerAvailability> IApplicationDbContext.PlayerAvailabilities => PlayerAvailabilities;
    IQueryable<InjuryReport> IApplicationDbContext.InjuryReports => InjuryReports;
    IQueryable<Suspension> IApplicationDbContext.Suspensions => Suspensions;
    IQueryable<NewsItem> IApplicationDbContext.NewsItems => NewsItems;
    IQueryable<Prediction> IApplicationDbContext.Predictions => Predictions;
    IQueryable<ModelTrainingRun> IApplicationDbContext.ModelTrainingRuns => ModelTrainingRuns;
    IQueryable<DataIngestionRun> IApplicationDbContext.DataIngestionRuns => DataIngestionRuns;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(32);
        });

        modelBuilder.Entity<Gameweek>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasIndex(x => x.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.ShortName).HasMaxLength(32);
            entity.Property(x => x.Code).HasMaxLength(16);
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasIndex(x => x.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
            entity.HasIndex(x => new { x.TeamId, x.Name });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Position).HasMaxLength(32);
        });

        modelBuilder.Entity<Fixture>(entity =>
        {
            entity.HasIndex(x => x.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
            entity.HasIndex(x => new { x.SeasonId, x.GameweekId, x.KickoffUtc });
        });

        modelBuilder.Entity<PlayerMatchStat>(entity =>
        {
            entity.HasIndex(x => new { x.PlayerId, x.FixtureId }).IsUnique();
        });

        modelBuilder.Entity<TeamMatchStat>(entity =>
        {
            entity.HasIndex(x => new { x.TeamId, x.FixtureId }).IsUnique();
        });

        modelBuilder.Entity<FantasyPlayerPrice>(entity =>
        {
            entity.HasIndex(x => new { x.PlayerId, x.GameweekId }).IsUnique();
        });

        modelBuilder.Entity<FantasyPlayerOwnership>(entity =>
        {
            entity.HasIndex(x => new { x.PlayerId, x.GameweekId }).IsUnique();
        });

        modelBuilder.Entity<PlayerAvailability>(entity =>
        {
            entity.HasIndex(x => x.PlayerId);
            entity.Property(x => x.SourceName).HasMaxLength(120);
            entity.Property(x => x.SourceUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<InjuryReport>(entity =>
        {
            entity.HasIndex(x => x.PlayerId);
            entity.Property(x => x.SourceName).HasMaxLength(120);
            entity.Property(x => x.SourceUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Suspension>(entity =>
        {
            entity.HasIndex(x => x.PlayerId);
            entity.Property(x => x.SourceName).HasMaxLength(120);
            entity.Property(x => x.SourceUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<NewsItem>(entity =>
        {
            entity.HasIndex(x => new { x.PublishedUtc, x.SourceName });
            entity.Property(x => x.Title).HasMaxLength(250);
            entity.Property(x => x.SourceName).HasMaxLength(120);
            entity.Property(x => x.SourceUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.HasIndex(x => new { x.PredictionKind, x.CreatedUtc });
        });

        modelBuilder.Entity<ModelTrainingRun>(entity =>
        {
            entity.HasIndex(x => new { x.ModelName, x.StartedUtc });
            entity.Property(x => x.ModelName).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(64);
        });

        modelBuilder.Entity<DataIngestionRun>(entity =>
        {
            entity.HasIndex(x => new { x.SourceName, x.StartedUtc });
            entity.Property(x => x.SourceName).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(64);
        });

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            var underlyingType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

            if (underlyingType == typeof(decimal))
            {
                property.SetPrecision(18);
                property.SetScale(4);
            }
        }
    }
}
