/*
    BankLinkList
    The bank accounts a person connected, oldest first.
*/
CREATE PROCEDURE [dbo].[BankLinkList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT b.[BankLinkUniqueID],
           u.[UserUniqueID],
           a.[AccountUniqueID],
           b.[SessionID],
           b.[AccountUid],
           b.[BankName],
           b.[BankCountry],
           b.[AccountLabel],
           b.[IbanTail],
           b.[ValidUntil],
           b.[ImportFrom],
           b.[LastSyncAt],
           b.[LastError],
           b.[BankBalance],
           b.[CreatedAt]
    FROM [dbo].[BankLink] b
    INNER JOIN [dbo].[User] u ON u.[UserID] = b.[UserID]
    LEFT JOIN [dbo].[Account] a ON a.[AccountID] = b.[AccountID]
    WHERE u.[UserUniqueID] = @UserUniqueID
    ORDER BY b.[BankLinkID];
END
