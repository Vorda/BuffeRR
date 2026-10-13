using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SoftwareRR.Demos.Contacts.Data;
using SoftwareRR.Demos.Contacts.Models;

Console.WriteLine("=== Contacts demo (EF Core + SQLite) ===");

using var context = new ContactsContext(new DbContextOptionsBuilder<ContactsContext>().Options);

// Ensure database exists
context.Database.EnsureCreated();

// Seed sample data if empty
if (!context.Contacts.Any())
{
    Console.WriteLine("Seeding sample contacts...");
    var alice = new Contact
    {
        UserId = Guid.NewGuid().ToString(),
        FirstName = "Alice",
        LastName = "Johnson",
        Note = "Met at conference"
    };
    alice.Phones.Add(new PhoneNumber { Number = "+1-555-0100", Type = "Mobile" });
    alice.Emails.Add(new EmailAddress { Address = "alice@example.com", Type = "Personal" });

    var bob = new Contact
    {
        UserId = Guid.NewGuid().ToString(),
        FirstName = "Bob",
        LastName = "Smith",
        Note = "Works remotely"
    };
    bob.Phones.Add(new PhoneNumber { Number = "+1-555-0200", Type = "Home" });
    bob.Emails.Add(new EmailAddress { Address = "bob@work.com", Type = "Work" });

    context.Add(alice);
    context.Add(bob);
    context.SaveChanges();
}

void ListAll()
{
    Console.WriteLine("\nAll contacts:");
    var list = context.Contacts.Include(c => c.Phones).Include(c => c.Emails).ToList();
    foreach (var c in list)
    {
        Console.WriteLine($"[{c.Id}] {c.FirstName} {c.LastName} (UserId: {c.UserId})");
        foreach (var p in c.Phones) Console.WriteLine($"  Phone: {p.Type ?? ""} {p.Number}");
        foreach (var e in c.Emails) Console.WriteLine($"  Email: {e.Type ?? ""} {e.Address}");
        if (!string.IsNullOrEmpty(c.Note)) Console.WriteLine($"  Note: {c.Note}");
    }
}

ListAll();

Console.WriteLine("\nAdd a new contact (press Enter to skip)");
Console.Write("First name: ");
var fn = Console.ReadLine();
if (!string.IsNullOrWhiteSpace(fn))
{
    Console.Write("Last name: ");
    var ln = Console.ReadLine() ?? string.Empty;
    var contact = new Contact { UserId = Guid.NewGuid().ToString(), FirstName = fn, LastName = ln };

    Console.Write("Phone (optional): ");
    var ph = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(ph)) contact.Phones.Add(new PhoneNumber { Number = ph, Type = "Mobile" });

    Console.Write("Email (optional): ");
    var em = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(em)) contact.Emails.Add(new EmailAddress { Address = em, Type = "Personal" });

    context.Contacts.Add(contact);
    context.SaveChanges();
    Console.WriteLine("Contact added.");
    ListAll();
}

Console.WriteLine("\nDemo: Update first contact's note and delete any contact named 'Bob'");
var first = context.Contacts.FirstOrDefault();
if (first != null)
{
    first.Note = (first.Note ?? "") + " (updated)";
    context.SaveChanges();
}

var bobs = context.Contacts.Where(c => c.FirstName == "Bob").ToList();
foreach (var b in bobs)
{
    context.Contacts.Remove(b);
}
if (bobs.Count > 0) context.SaveChanges();

ListAll();

Console.WriteLine("\nDemo complete. Database file 'contacts.db' created in the program folder.");

await Task.CompletedTask;
