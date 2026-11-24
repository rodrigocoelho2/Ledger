/*
    EntryReviewed
    Marks entries brought from the bank as looked at. @Entries is a JSON array of entry identifiers; an empty array (or
    NULL) marks every entry of the person. Returns how many changed.
*/
CREATE PROCEDURE [dbo].[EntryReviewed]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Entries      NVARCHAR (MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL
    BEGIN
        SELECT 0;
        RETURN;
    END;

    IF @Entries IS NULL OR NOT EXISTS (SELECT 1 FROM OPENJSON(@Entries))
        UPDATE [dbo].[Entry]
        SET [Review] = NULL
        WHERE [UserID] = @UserID AND [Review] = 1;
    ELSE
        UPDATE e
        SET e.[Review] = NULL
        FROM [dbo].[Entry] e
        INNER JOIN OPENJSON(@Entries) o ON TRY_CAST(o.[value] AS UNIQUEIDENTIFIER) = e.[EntryUniqueID]
        WHERE e.[UserID] = @UserID AND e.[Review] = 1;

    SELECT @@ROWCOUNT;
END
