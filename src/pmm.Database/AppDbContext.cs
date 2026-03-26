using Microsoft.EntityFrameworkCore;

namespace Pmm.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Indexer> Indexers => Set<Indexer>();
    public DbSet<IndexerRow> IndexerRows => Set<IndexerRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
