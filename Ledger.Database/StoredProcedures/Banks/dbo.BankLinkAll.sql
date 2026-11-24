/*
    BankLinkAll
    Every connected bank account that has a Ledger account to go into, of every person: what the background sync goes
    through.
*/
CREATE PROCEDURE [dbo].[BankLinkAll]
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
    WHERE b.[AccountID] IS NOT NULL
    ORDER BY b.[BankLinkID];
END
