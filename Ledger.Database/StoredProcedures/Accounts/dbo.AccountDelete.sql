/*
    AccountDelete
    Deletes an account. Its entries stay in the ledger with the account emptied, so the months already closed keep
    their totals; they just stop counting towards any balance.
*/
CREATE PROCEDURE [dbo].[AccountDelete]
    @UserUniqueID    UNIQUEIDENTIFIER,
    @AccountUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @AccountID BIGINT = (
        SELECT a.[AccountID]
        FROM [dbo].[Account] a
        INNER JOIN [dbo].[User] u ON u.[UserID] = a.[UserID]
        WHERE u.[UserUniqueID] = @UserUniqueID
          AND a.[AccountUniqueID] = @AccountUniqueID);
    IF @AccountID IS NULL
        RETURN;

    BEGIN TRANSACTION;

    UPDATE [dbo].[Entry]
    SET [AccountID] = NULL
    WHERE [AccountID] = @AccountID;

    UPDATE [dbo].[Entry]
    SET [ToAccountID] = NULL
    WHERE [ToAccountID] = @AccountID;

    DELETE FROM [dbo].[Account]
    WHERE [AccountID] = @AccountID;

    COMMIT TRANSACTION;
END
