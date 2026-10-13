using Microsoft.EntityFrameworkCore;
using SoftwareRR.Demos.Contacts.Models;

namespace SoftwareRR.Demos.Contacts.Data;

public class ContactsContext : DbContext
{
    public DbSet<Contact> Contacts { get; set; } = null!;
    public DbSet<PhoneNumber> PhoneNumbers { get; set; } = null!;
    public DbSet<EmailAddress> EmailAddresses { get; set; } = null!;

    public ContactsContext(DbContextOptions<ContactsContext> options) : base(options)
    {
    }

    // Simple OnConfiguring fallback for developer convenience when not using DI
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=contacts.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Contact>(eb =>
        {
            eb.HasKey(c => c.Id);
            eb.HasIndex(c => c.UserId).IsUnique(false);
            eb.Property(c => c.FirstName).IsRequired();
            eb.Property(c => c.LastName).IsRequired();
            eb.HasMany(c => c.Phones).WithOne(p => p.Contact).HasForeignKey(p => p.ContactId).OnDelete(DeleteBehavior.Cascade);
            eb.HasMany(c => c.Emails).WithOne(e => e.Contact).HasForeignKey(e => e.ContactId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PhoneNumber>(eb =>
        {
            eb.HasKey(p => p.Id);
            eb.Property(p => p.Number).IsRequired();
        });

        modelBuilder.Entity<EmailAddress>(eb =>
        {
            eb.HasKey(e => e.Id);
            eb.Property(e => e.Address).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
