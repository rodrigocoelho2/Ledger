/*
    AccountSave
    Adds an account (NULL @AccountUniqueID; it goes to the end of the list) or changes one, with where its value comes
    from (@ValueSource NULL: its entries). Returns the account's identifier, or NULL when the account to change isn't
    this person's.
*/
CREATE PROCEDURE [dbo].[AccountSave]
    @UserUniqueID    UNIQUEIDENTIFIER,
    @AccountUniqueID UNIQUEIDENTIFIER,
    @Name            NVARCHAR (60),
    @Kind            TINYINT,
    @OpeningBalance  DECIMAL (14, 2),
    @ValueSource     TINYINT = NULL,
    @ValueRef        NVARCHAR (64) = NULL
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

    IF @AccountUniqueID IS NULL
    BEGIN
        SET @AccountUniqueID = NEWID();

        INSERT INTO [dbo].[Account] ([AccountUniqueID], [UserID], [Name], [Kind], [OpeningBalance], [SortOrder], [ValueSource], [ValueRef])
        SELECT @AccountUniqueID, @UserID, @Name, @Kind, @OpeningBalance, ISNULL(MAX(a.[SortOrder]), 0) + 1, @ValueSource, @ValueRef
        FROM [dbo].[Account] a
        WHERE a.[UserID] = @UserID;
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Account]
        SET [Name] = @Name,
            [Kind] = @Kind,
            [OpeningBalance] = @OpeningBalance,
            [ValueSource] = @ValueSource,
            [ValueRef] = @ValueRef
        WHERE [AccountUniqueID] = @AccountUniqueID
          AND [UserID] = @UserID;

        IF @@ROWCOUNT = 0
            SET @AccountUniqueID = NULL;
    END

    SELECT @AccountUniqueID;
END
