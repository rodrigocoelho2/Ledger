/*
    CapSave
    Sets the spending cap of a month (@MonthStart is its first day). NULL @Amount removes it.
*/
CREATE PROCEDURE [dbo].[CapSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @MonthStart   DATE,
    @Amount       DECIMAL (14, 2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL
        RETURN;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[MonthCap] WITH (UPDLOCK, HOLDLOCK)
    WHERE [UserID] = @UserID
      AND [MonthStart] = @MonthStart;

    IF @Amount IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[MonthCap] ([UserID], [MonthStart], [Amount])
        VALUES (@UserID, @MonthStart, @Amount);
    END

    COMMIT TRANSACTION;
END
