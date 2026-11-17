/*
    ProjectSave
    Adds a project (NULL @ProjectUniqueID) or changes one. Adding a name the person already has returns that project
    as it is, so an entry or an import can name a project without checking first. Returns the project's identifier,
    or NULL when the project to change isn't this person's.
*/
CREATE PROCEDURE [dbo].[ProjectSave]
    @UserUniqueID    UNIQUEIDENTIFIER,
    @ProjectUniqueID UNIQUEIDENTIFIER,
    @Name            NVARCHAR (80),
    @TargetSale      DECIMAL (14, 2),
    @WorksBudget     DECIMAL (14, 2),
    @AgreedPrice     DECIMAL (14, 2)
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

    IF @ProjectUniqueID IS NULL
    BEGIN
        BEGIN TRANSACTION;

        SET @ProjectUniqueID = (SELECT [ProjectUniqueID] FROM [dbo].[Project] WITH (UPDLOCK, HOLDLOCK) WHERE [UserID] = @UserID AND [Name] = @Name);
        IF @ProjectUniqueID IS NULL
        BEGIN
            SET @ProjectUniqueID = NEWID();

            INSERT INTO [dbo].[Project] ([ProjectUniqueID], [UserID], [Name], [TargetSale], [WorksBudget], [AgreedPrice])
            VALUES (@ProjectUniqueID, @UserID, @Name, @TargetSale, @WorksBudget, @AgreedPrice);
        END

        COMMIT TRANSACTION;
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Project]
        SET [Name] = @Name,
            [TargetSale] = @TargetSale,
            [WorksBudget] = @WorksBudget,
            [AgreedPrice] = @AgreedPrice
        WHERE [ProjectUniqueID] = @ProjectUniqueID
          AND [UserID] = @UserID;

        IF @@ROWCOUNT = 0
            SET @ProjectUniqueID = NULL;
    END

    SELECT @ProjectUniqueID;
END
