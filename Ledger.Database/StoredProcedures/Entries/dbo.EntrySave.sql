/*
    EntrySave
    Adds an entry (NULL @EntryUniqueID) or changes one. The accounts and the project are looked up among this person's
    own; one that isn't theirs is left empty. Returns the entry's identifier, or NULL when the entry to change isn't
    this person's.
*/
CREATE PROCEDURE [dbo].[EntrySave]
    @UserUniqueID      UNIQUEIDENTIFIER,
    @EntryUniqueID     UNIQUEIDENTIFIER,
    @Kind              TINYINT,
    @Scope             TINYINT,
    @Name              NVARCHAR (120),
    @Amount            DECIMAL (14, 2),
    @EntryDate         DATE,
    @Category          VARCHAR (32),
    @AccountUniqueID   UNIQUEIDENTIFIER,
    @ToAccountUniqueID UNIQUEIDENTIFIER,
    @ProjectUniqueID   UNIQUEIDENTIFIER,
    @Nights            SMALLINT,
    @Channel           NVARCHAR (40)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL
    BEGIN
        SELECT CAST(NULL AS UNIQUEIDENTIFIER);
        RETURN;
    END

    DECLARE @AccountID BIGINT = (SELECT [AccountID] FROM [dbo].[Account] WHERE [AccountUniqueID] = @AccountUniqueID AND [UserID] = @UserID);
    DECLARE @ToAccountID BIGINT = (SELECT [AccountID] FROM [dbo].[Account] WHERE [AccountUniqueID] = @ToAccountUniqueID AND [UserID] = @UserID);
    DECLARE @ProjectID BIGINT = (SELECT [ProjectID] FROM [dbo].[Project] WHERE [ProjectUniqueID] = @ProjectUniqueID AND [UserID] = @UserID);

    IF @EntryUniqueID IS NULL
    BEGIN
        SET @EntryUniqueID = NEWID();

        INSERT INTO [dbo].[Entry] ([EntryUniqueID], [UserID], [Kind], [Scope], [Name], [Amount], [EntryDate], [Category], [AccountID], [ToAccountID], [ProjectID], [Nights], [Channel])
        VALUES (@EntryUniqueID, @UserID, @Kind, @Scope, @Name, @Amount, @EntryDate, @Category, @AccountID, @ToAccountID, @ProjectID, @Nights, @Channel);
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Entry]
        SET [Kind] = @Kind,
            [Scope] = @Scope,
            [Name] = @Name,
            [Amount] = @Amount,
            [EntryDate] = @EntryDate,
            [Category] = @Category,
            [AccountID] = @AccountID,
            [ToAccountID] = @ToAccountID,
            [ProjectID] = @ProjectID,
            [Nights] = @Nights,
            [Channel] = @Channel
        WHERE [EntryUniqueID] = @EntryUniqueID
          AND [UserID] = @UserID;

        IF @@ROWCOUNT = 0
            SET @EntryUniqueID = NULL;
    END

    SELECT @EntryUniqueID;
END
