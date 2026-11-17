namespace Ledger.Library.Enums;

/// <summary>
/// Keys of the user-facing messages. Use with <c>nameof(TranslationKeys.X)</c>, never a magic string.
/// </summary>
public enum TranslationKeys
{
    /// <summary>Generic title of a validation failure.</summary>
    ValidationProblems,

    /// <summary>A required field is empty.</summary>
    RequiredField,

    /// <summary>Another record already uses the value.</summary>
    AlreadyExists,

    /// <summary>The record does not exist.</summary>
    NoRecordsFound,

    /// <summary>A user name with characters or a length that is not allowed.</summary>
    UserNameInvalid,

    /// <summary>A password shorter than the minimum.</summary>
    PasswordTooShort,

    /// <summary>A password longer than the maximum.</summary>
    PasswordTooLong,

    /// <summary>Wrong user name or password (never says which).</summary>
    WrongCredentials,

    /// <summary>The current password is wrong.</summary>
    WrongCurrentPassword,

    /// <summary>The last admin cannot be deleted.</summary>
    LastAdmin,

    /// <summary>A value outside its allowed range.</summary>
    OutOfRange,

    /// <summary>A text longer than allowed.</summary>
    TooLong,

    /// <summary>An amount that isn't above zero (or is too big to keep).</summary>
    AmountInvalid,

    /// <summary>A day that isn't one.</summary>
    DateInvalid,

    /// <summary>A category that isn't in the list, or doesn't go with the kind of entry.</summary>
    CategoryUnknown,

    /// <summary>An account that isn't one of the person's.</summary>
    AccountUnknown,

    /// <summary>A transfer from an account to itself.</summary>
    SameAccount,

    /// <summary>A project that isn't one of the person's.</summary>
    ProjectUnknown,

    /// <summary>A language the app doesn't speak.</summary>
    LanguageUnknown,

    /// <summary>A file that isn't a Ledger export.</summary>
    FileInvalid,

    /// <summary>A file with nothing to import.</summary>
    FileEmpty,

    /// <summary>Quick add couldn't find an amount in the text.</summary>
    QuickAddNoAmount,

    /// <summary>No account is linked to that Discord account.</summary>
    DiscordNotLinked,

    /// <summary>That Discord account is linked to another user.</summary>
    DiscordTaken,

    /// <summary>A Discord id that isn't one.</summary>
    DiscordInvalid,

    /// <summary>Discord is the only way in, so it can't be unlinked before a password is set.</summary>
    DiscordNeedsPassword,
}
