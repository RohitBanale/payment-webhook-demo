using Microsoft.EntityFrameworkCore;
using PaymentWebhookDemo.Api.Models;

namespace PaymentWebhookDemo.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Payment>().HasIndex(p => p.PaymentId).IsUnique();
        b.Entity<WebhookEvent>().HasIndex(e => e.EventId).IsUnique();
    }
}
