namespace Ledger.WebApi.Features.Users.Models.Entities;

/// <summary>
/// A user (dbo.User). The password is only kept as a salted PBKDF2 hash.
/// </summary>
public sealed class UserAccount
{
    /// <summary>Gets or sets the internal key (never leaves the WebApi).</summary>
    public long UserID { get; set; }

    /// <summary>Gets or sets the public identifier (the Website's cookie holds it).</summary>
    public Guid UserUniqueID { get; set; }

    /// <summary>Gets or sets the user name (unique, ignoring case).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the password hash (<see cref="Services.PasswordHasher"/> format).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the account is an admin (the first account is).</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Gets or sets a random value that changes with the password, so other sessions end.</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>Gets or sets when the account was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the last successful sign-in (UTC).</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Gets or sets the linked Discord account's id (null: none).</summary>
    public string? DiscordID { get; set; }

    /// <summary>Gets or sets the linked Discord account's name when it was linked.</summary>
    public string? DiscordName { get; set; }

    /// <summary>Gets or sets the language chosen in Settings (null: the default, English; dbo.Setting).</summary>
    public string? Language { get; set; }

    /// <summary>Gets or sets whether the short-term rental module is on (null: off; dbo.Setting).</summary>
    public bool? RentalEnabled { get; set; }

    /// <summary>Gets or sets whether the buy-and-sell module is on (null: off; dbo.Setting).</summary>
    public bool? FlipsEnabled { get; set; }

    /// <summary>Gets or sets the day of the month the salary is due (null: calendar months; dbo.Setting).</summary>
    public byte? PayDay { get; set; }

    /// <summary>Gets or sets a word of the salary's bank description, the employer (null: none; dbo.Setting).</summary>
    public string? Payer { get; set; }
}
