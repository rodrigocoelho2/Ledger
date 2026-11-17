namespace Ledger.Sdk.Models;

/// <summary>A file to hand to the person.</summary>
public sealed class ExportDTO
{
    /// <summary>Gets or sets the name to save it under.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets its media type.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Gets or sets its text.</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>A file to read into the ledger.</summary>
public sealed class ImportRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the text of the file (a CSV or a JSON backup).</summary>
    public string? Content { get; set; }

    /// <summary>Gets or sets a value indicating whether what is in the ledger is erased first.</summary>
    public bool Replace { get; set; }
}

/// <summary>What an import did.</summary>
public sealed class ImportResultDTO
{
    /// <summary>Gets or sets how many entries were added.</summary>
    public int Added { get; set; }

    /// <summary>Gets or sets how many entries were already there and left alone.</summary>
    public int AlreadyThere { get; set; }

    /// <summary>Gets or sets how many rows of the file could not be read.</summary>
    public int Skipped { get; set; }

    /// <summary>Gets or sets how many accounts were created for it.</summary>
    public int AccountsCreated { get; set; }

    /// <summary>Gets or sets how many projects were created for it.</summary>
    public int ProjectsCreated { get; set; }
}
