/*
    AccountValueSave
    Keeps what an account valued elsewhere is worth on a day (the last call of the day wins).
*/
CREATE PROCEDURE [dbo].[AccountValueSave]
    @UserUniqueID    UNIQUEIDENTIFIER,
    @AccountUniqueID UNIQUEIDENTIFIER,
    @ValueDate       DATE,
    @Value           DECIMAL (14, 2)
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

    UPDATE [dbo].[AccountValue] WITH (UPDLOCK, HOLDLOCK)
    SET [Value] = @Value,
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [AccountID] = @AccountID
      AND [ValueDate] = @ValueDate;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[AccountValue] ([AccountID], [ValueDate], [Value])
        VALUES (@AccountID, @ValueDate, @Value);
    END

    COMMIT TRANSACTION;
END
