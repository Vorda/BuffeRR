using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SoftwareRR.Demos.Contacts.Models;

public class Contact
{
    public int Id { get; set; }

    // A separate stable user identifier (could be a GUID or external id)
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    // Navigation properties
    public List<PhoneNumber> Phones { get; set; } = new();
    public List<EmailAddress> Emails { get; set; } = new();

    // Free-form note
    public string? Note { get; set; }
}
