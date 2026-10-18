using Balenthiran.Snipit.EntityModels;
using Microsoft.EntityFrameworkCore;

namespace Balenthiran.Snipit.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TranscriptionJobEntity> TranscriptionJobs => Set<TranscriptionJobEntity>();
    public DbSet<CutJobEntity> CutJobs => Set<CutJobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TranscriptionJobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TranscriptJson).HasColumnType("text");
        });

        modelBuilder.Entity<CutJobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.KeepRangesJson).HasColumnType("text");
        });
    }
}
