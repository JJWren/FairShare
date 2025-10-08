namespace FairShare.Options;

public sealed class AdminSeedOptions
{
    // Enable/disable seeding entirely
    public bool Enabled { get; set; } = true;

    // Username to seed (default 'admin')
    public string User { get; set; } = "admin";

    // If null/empty a random password is generated
    public string? Password { get; set; }

    // If a random password is generated, should it be logged? (Avoid in prod logs)
    public bool LogGeneratedPassword { get; set; } = true;
}
