/*
    LedgerReset
    Start over: deletes every entry, project, account and spending cap of a person. The user and what they chose in
    Settings stay, and so do the connected banks, waiting for an account again.
*/
CREATE PROCEDURE [dbo].[LedgerReset]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL
        RETURN;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[Entry]
    WHERE [UserID] = @UserID;

    DELETE FROM [dbo].[Project]
    WHERE [UserID] = @UserID;

    UPDATE [dbo].[BankLink]
    SET [AccountID] = NULL, [LastSyncAt] = NULL, [BankBalance] = NULL
    WHERE [UserID] = @UserID;

    DELETE FROM [dbo].[BankIgnore]
    WHERE [UserID] = @UserID;

    DELETE FROM [dbo].[Account]
    WHERE [UserID] = @UserID;

    DELETE FROM [dbo].[MonthCap]
    WHERE [UserID] = @UserID;

    COMMIT TRANSACTION;
END
