/*
    SettingSave
    Keeps what a person chose in Settings: the language, which optional modules are on and where their months begin
    (@PayDay NULL: calendar months).
*/
CREATE PROCEDURE [dbo].[SettingSave]
    @UserUniqueID  UNIQUEIDENTIFIER,
    @Language      CHAR (2),
    @RentalEnabled BIT,
    @FlipsEnabled  BIT,
    @PayDay        TINYINT       = NULL,
    @Payer         NVARCHAR (64) = NULL
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
        [PayDay] = @PayDay,
        [Payer] = @Payer,
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [UserID] = @UserID;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[Setting] ([UserID], [Language], [RentalEnabled], [FlipsEnabled], [PayDay], [Payer])
        VALUES (@UserID, @Language, @RentalEnabled, @FlipsEnabled, @PayDay, @Payer);
    END

    COMMIT TRANSACTION;
END
