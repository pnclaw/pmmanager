using Microsoft.EntityFrameworkCore;

namespace Pmm.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Indexer> Indexers => Set<Indexer>();
    public DbSet<IndexerRow> IndexerRows => Set<IndexerRow>();
    public DbSet<DownloadClient> DownloadClients => Set<DownloadClient>();
    public DbSet<IndexerApiRequest> IndexerApiRequests => Set<IndexerApiRequest>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<PrdbNetwork> PrdbNetworks => Set<PrdbNetwork>();
    public DbSet<PrdbSite> PrdbSites => Set<PrdbSite>();
    public DbSet<PrdbVideo> PrdbVideos => Set<PrdbVideo>();
    public DbSet<PrdbPreDbEntry> PrdbPreDbEntries => Set<PrdbPreDbEntry>();
    public DbSet<PrdbVideoImage> PrdbVideoImages => Set<PrdbVideoImage>();
    public DbSet<PrdbVideoPreName> PrdbVideoPreNames => Set<PrdbVideoPreName>();
    public DbSet<PrdbActor> PrdbActors => Set<PrdbActor>();
    public DbSet<PrdbActorImage> PrdbActorImages => Set<PrdbActorImage>();
    public DbSet<PrdbActorAlias> PrdbActorAliases => Set<PrdbActorAlias>();
    public DbSet<PrdbVideoActor> PrdbVideoActors => Set<PrdbVideoActor>();
    public DbSet<PrdbWantedVideo> PrdbWantedVideos => Set<PrdbWantedVideo>();
    public DbSet<IndexerRowMatch> IndexerRowMatches => Set<IndexerRowMatch>();
    public DbSet<FolderMapping> FolderMappings => Set<FolderMapping>();
    public DbSet<DownloadLog> DownloadLogs => Set<DownloadLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppSettings>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasData(new AppSettings { Id = 1 });
        });

        modelBuilder.Entity<PrdbVideoActor>(e =>
        {
            e.HasKey(va => new { va.VideoId, va.ActorId });
        });

        modelBuilder.Entity<PrdbWantedVideo>(e =>
        {
            e.HasKey(w => w.VideoId);
            e.HasOne(w => w.Video)
             .WithMany()
             .HasForeignKey(w => w.VideoId);
        });

        modelBuilder.Entity<IndexerRowMatch>(e =>
        {
            e.HasIndex(m => m.IndexerRowId).IsUnique();
        });

        modelBuilder.Entity<FolderMapping>(e =>
        {
            e.HasIndex(f => f.OriginalFolder).IsUnique();
            e.HasIndex(f => f.MappedToFolder).IsUnique();
        });

        modelBuilder.Entity<PrdbPreDbEntry>(e =>
        {
            e.HasIndex(p => p.CreatedAtUtc);
            e.HasIndex(p => p.Title);
            e.HasIndex(p => p.PrdbVideoId);

            e.HasOne(p => p.Video)
             .WithMany(v => v.PreDbEntries)
             .HasForeignKey(p => p.PrdbVideoId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(p => p.Site)
             .WithMany(s => s.PreDbEntries)
             .HasForeignKey(p => p.PrdbSiteId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
