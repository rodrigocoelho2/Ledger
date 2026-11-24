/*
    BankLinkDelete
    Disconnects a bank account. The entries it brought stay in the ledger.
*/
CREATE PROCEDURE [dbo].[BankLinkDelete]
    @UserUniqueID     UNIQUEIDENTIFIER,
    @BankLinkUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE b
    FROM [dbo].[BankLink] b
    INNER JOIN [dbo].[User] u ON u.[UserID] = b.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND b.[BankLinkUniqueID] = @BankLinkUniqueID;

    SELECT @@ROWCOUNT;
END
