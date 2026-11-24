/*
    BankLinkAssign
    Says which Ledger account a bank account's transactions go into, and from which day to bring them. A NULL account
    stops the sync. Returns 0 when the link or the account isn't this person's.
*/
CREATE PROCEDURE [dbo].[BankLinkAssign]
    @UserUniqueID     UNIQUEIDENTIFIER,
    @BankLinkUniqueID UNIQUEIDENTIFIER,
    @AccountUniqueID  UNIQUEIDENTIFIER,
    @ImportFrom       DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    DECLARE @AccountID BIGINT = (SELECT [AccountID] FROM [dbo].[Account] WHERE [AccountUniqueID] = @AccountUniqueID AND [UserID] = @UserID);

    IF @AccountUniqueID IS NOT NULL AND @AccountID IS NULL
    BEGIN
        SELECT 0;
        RETURN;
    END

    UPDATE [dbo].[BankLink]
    SET [AccountID] = @AccountID,
        [ImportFrom] = @ImportFrom,
        [LastSyncAt] = NULL,
        [LastError] = NULL
    WHERE [BankLinkUniqueID] = @BankLinkUniqueID
      AND [UserID] = @UserID;

    SELECT @@ROWCOUNT;
END
