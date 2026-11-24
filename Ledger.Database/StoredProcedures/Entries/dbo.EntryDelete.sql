/*
    EntryDelete
    Deletes one entry of a person. One that came from the bank is remembered in dbo.BankIgnore, so the next sync
    doesn't bring it back.
*/
CREATE PROCEDURE [dbo].[EntryDelete]
    @UserUniqueID  UNIQUEIDENTIFIER,
    @EntryUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    INSERT INTO [dbo].[BankIgnore] ([UserID], [ExternalID])
    SELECT DISTINCT e.[UserID], x.[ExternalID]
    FROM [dbo].[Entry] e
    INNER JOIN [dbo].[User] u ON u.[UserID] = e.[UserID]
    CROSS APPLY (VALUES (e.[ExternalID]), (e.[ToExternalID])) AS x ([ExternalID])
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND e.[EntryUniqueID] = @EntryUniqueID
      AND x.[ExternalID] IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM [dbo].[BankIgnore] i WHERE i.[UserID] = e.[UserID] AND i.[ExternalID] = x.[ExternalID]);

    DELETE e
    FROM [dbo].[Entry] e
    INNER JOIN [dbo].[User] u ON u.[UserID] = e.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND e.[EntryUniqueID] = @EntryUniqueID;

    COMMIT TRANSACTION;
END
