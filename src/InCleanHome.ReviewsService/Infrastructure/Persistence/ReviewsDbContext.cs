using EntityFrameworkCore.CreatedUpdatedDate.Extensions;
using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace InCleanHome.ReviewsService.Infrastructure.Persistence;

public class ReviewsDbContext(DbContextOptions<ReviewsDbContext> options) : DbContext(options)
{
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<SuspensionAppeal> SuspensionAppeals => Set<SuspensionAppeal>();

    protected override void OnConfiguring(DbContextOptionsBuilder builder)
    {
        builder.AddCreatedUpdatedInterceptor();
        base.OnConfiguring(builder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Review>().HasKey(r => r.Id);
        builder.Entity<Review>().Property(r => r.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<Review>().Property(r => r.BookingId).IsRequired();
        builder.Entity<Review>().Property(r => r.ClientId).IsRequired();
        builder.Entity<Review>().Property(r => r.WorkerId).IsRequired();
        builder.Entity<Review>().Property(r => r.Rating).IsRequired();
        builder.Entity<Review>().Property(r => r.Comment).HasMaxLength(2000);
        builder.Entity<Review>().HasIndex(r => r.BookingId).IsUnique();
        builder.Entity<Review>().HasIndex(r => r.WorkerId);

        builder.Entity<Report>().HasKey(r => r.Id);
        builder.Entity<Report>().Property(r => r.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<Report>().Property(r => r.ReporterUserId).IsRequired();
        builder.Entity<Report>().Property(r => r.ReportedUserId).IsRequired();
        builder.Entity<Report>().Property(r => r.ReportedRole).HasMaxLength(20);
        builder.Entity<Report>().Property(r => r.Reason).IsRequired().HasMaxLength(200);
        builder.Entity<Report>().Property(r => r.Details).HasMaxLength(2000);
        builder.Entity<Report>().Property(r => r.Status).IsRequired().HasMaxLength(20);
        builder.Entity<Report>().Property(r => r.AdminNotes).HasMaxLength(1000);
        builder.Entity<Report>().HasIndex(r => r.ReportedUserId);
        builder.Entity<Report>().HasIndex(r => r.Status);

        builder.Entity<SuspensionAppeal>().HasKey(a => a.Id);
        builder.Entity<SuspensionAppeal>().Property(a => a.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<SuspensionAppeal>().Property(a => a.UserId).IsRequired();
        builder.Entity<SuspensionAppeal>().Property(a => a.Reason).IsRequired().HasMaxLength(2000);
        builder.Entity<SuspensionAppeal>().Property(a => a.Status).IsRequired().HasMaxLength(20);
        builder.Entity<SuspensionAppeal>().Property(a => a.AdminResponse).HasMaxLength(2000);
        builder.Entity<SuspensionAppeal>().HasIndex(a => a.UserId);
        builder.Entity<SuspensionAppeal>().HasIndex(a => a.Status);

        builder.UseSnakeCaseNamingConvention();
    }
}
