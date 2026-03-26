using Microsoft.EntityFrameworkCore;

namespace Pmm.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Indexer> Indexers => Set<Indexer>();
    public DbSet<IndexerRow> IndexerRows => Set<IndexerRow>();
    public DbSet<DownloadClient> DownloadClients => Set<DownloadClient>();
    public DbSet<IndexerApiRequest> IndexerApiRequests => Set<IndexerApiRequest>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<PrdbNetwork> PrdbNetworks => Set<PrdbNetwork>();
    public DbSet<PrdbSite> PrdbSites => Set<PrdbSite>();
    public DbSet<PrdbVideo> PrdbVideos => Set<PrdbVideo>();
    public DbSet<PrdbVideoImage> PrdbVideoImages => Set<PrdbVideoImage>();
    public DbSet<PrdbVideoPreName> PrdbVideoPreNames => Set<PrdbVideoPreName>();
    public DbSet<PrdbActor> PrdbActors => Set<PrdbActor>();
    public DbSet<PrdbActorImage> PrdbActorImages => Set<PrdbActorImage>();
    public DbSet<PrdbActorAlias> PrdbActorAliases => Set<PrdbActorAlias>();
    public DbSet<PrdbVideoActor> PrdbVideoActors => Set<PrdbVideoActor>();

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
    }
}
