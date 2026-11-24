/*
    BankLinkSynced
    Keeps how a sync went: when, the balance the bank reported, and the error if it failed (NULL: it worked).
*/
CREATE PROCEDURE [dbo].[BankLinkSynced]
    @BankLinkUniqueID UNIQUEIDENTIFIER,
    @LastSyncAt       DATETIME2 (3),
    @LastError        NVARCHAR (400),
    @BankBalance      DECIMAL (14, 2)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[BankLink]
    SET [LastSyncAt] = CASE WHEN @LastError IS NULL THEN @LastSyncAt ELSE [LastSyncAt] END,
        [LastError] = @LastError,
        [BankBalance] = ISNULL(@BankBalance, [BankBalance])
    WHERE [BankLinkUniqueID] = @BankLinkUniqueID;
END
