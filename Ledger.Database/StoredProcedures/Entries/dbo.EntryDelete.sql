/*
    EntryDelete
    Deletes one entry of a person.
*/
CREATE PROCEDURE [dbo].[EntryDelete]
    @UserUniqueID  UNIQUEIDENTIFIER,
    @EntryUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE e
    FROM [dbo].[Entry] e
    INNER JOIN [dbo].[User] u ON u.[UserID] = e.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND e.[EntryUniqueID] = @EntryUniqueID;
END
