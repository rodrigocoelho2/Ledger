/*
    EntryAttachExternal
    Ties an entry the person wrote by hand to the bank transaction it turned out to be, so the sync doesn't add it again.
*/
CREATE PROCEDURE [dbo].[EntryAttachExternal]
    @UserUniqueID  UNIQUEIDENTIFIER,
    @EntryUniqueID UNIQUEIDENTIFIER,
    @ExternalID    NVARCHAR (100)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE e
    SET e.[ExternalID] = @ExternalID
    FROM [dbo].[Entry] e
    INNER JOIN [dbo].[User] u ON u.[UserID] = e.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND e.[EntryUniqueID] = @EntryUniqueID
      AND e.[ExternalID] IS NULL
      AND NOT EXISTS (SELECT 1 FROM [dbo].[Entry] x WHERE x.[UserID] = e.[UserID] AND x.[ExternalID] = @ExternalID);
END
