/*
    SettingSave
    Keeps what a person chose in Settings: the language and which optional modules are on.
*/
CREATE PROCEDURE [dbo].[SettingSave]
    @UserUniqueID  UNIQUEIDENTIFIER,
    @Language      CHAR (2),
    @RentalEnabled BIT,
    @FlipsEnabled  BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL
        RETURN;

    BEGIN TRANSACTION;

    UPDATE [dbo].[Setting] WITH (UPDLOCK, HOLDLOCK)
    SET [Language] = @Language,
        [RentalEnabled] = @RentalEnabled,
        [FlipsEnabled] = @FlipsEnabled,
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [UserID] = @UserID;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[Setting] ([UserID], [Language], [RentalEnabled], [FlipsEnabled])
        VALUES (@UserID, @Language, @RentalEnabled, @FlipsEnabled);
    END

    COMMIT TRANSACTION;
END
