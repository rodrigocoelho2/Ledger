/*
    EntryAttachExternal
    Ties an entry the person wrote by hand to the bank transaction it turned out to be, so the sync doesn't add it again.
    @To = 1 ties the arriving side of a transfer (ToExternalID); otherwise the leaving side, or the entry itself.
*/
CREATE PROCEDURE [dbo].[EntryAttachExternal]
    @UserUniqueID  UNIQUEIDENTIFIER,
    @EntryUniqueID UNIQUEIDENTIFIER,
    @ExternalID    NVARCHAR (100),
    @To            BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE e
    SET e.[ExternalID] = CASE WHEN @To = 1 THEN e.[ExternalID] ELSE @ExternalID END,
        e.[ToExternalID] = CASE WHEN @To = 1 THEN @ExternalID ELSE e.[ToExternalID] END
    FROM [dbo].[Entry] e
    INNER JOIN [dbo].[User] u ON u.[UserID] = e.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND e.[EntryUniqueID] = @EntryUniqueID
      AND ((@To = 1 AND e.[ToExternalID] IS NULL) OR (@To = 0 AND e.[ExternalID] IS NULL))
      AND NOT EXISTS (SELECT 1 FROM [dbo].[Entry] x WHERE x.[UserID] = e.[UserID] AND (x.[ExternalID] = @ExternalID OR x.[ToExternalID] = @ExternalID));
END
