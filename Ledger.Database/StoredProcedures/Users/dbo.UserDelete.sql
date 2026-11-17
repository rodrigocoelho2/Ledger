/*
    UserDelete
    Deletes a user and everything they own. The entries go first (they point at the accounts and the projects), then the
    projects, the accounts and the user; the settings and the spending caps follow through their cascading keys.
*/
CREATE PROCEDURE [dbo].[UserDelete]
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

    DELETE FROM [dbo].[Account]
    WHERE [UserID] = @UserID;

    DELETE FROM [dbo].[User]
    WHERE [UserID] = @UserID;

    COMMIT TRANSACTION;
END
