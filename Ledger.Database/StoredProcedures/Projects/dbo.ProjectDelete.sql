/*
    ProjectDelete
    Deletes a project. Its entries stay in the ledger, without a project.
*/
CREATE PROCEDURE [dbo].[ProjectDelete]
    @UserUniqueID    UNIQUEIDENTIFIER,
    @ProjectUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ProjectID BIGINT = (
        SELECT p.[ProjectID]
        FROM [dbo].[Project] p
        INNER JOIN [dbo].[User] u ON u.[UserID] = p.[UserID]
        WHERE u.[UserUniqueID] = @UserUniqueID
          AND p.[ProjectUniqueID] = @ProjectUniqueID);
    IF @ProjectID IS NULL
        RETURN;

    BEGIN TRANSACTION;

    UPDATE [dbo].[Entry]
    SET [ProjectID] = NULL
    WHERE [ProjectID] = @ProjectID;

    DELETE FROM [dbo].[Project]
    WHERE [ProjectID] = @ProjectID;

    COMMIT TRANSACTION;
END
