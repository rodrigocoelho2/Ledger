/*
    AccountList
    A person's accounts in the order they chose, each with its balance: the opening balance, plus the inflows, minus the
    outflows and the transfers out, plus the transfers in.
*/
CREATE PROCEDURE [dbo].[AccountList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT a.[AccountUniqueID],
           a.[Name],
           a.[Kind],
           a.[OpeningBalance],
           a.[SortOrder],
           a.[OpeningBalance]
               + ISNULL((SELECT SUM(CASE WHEN e.[Kind] = 2 THEN e.[Amount] ELSE -e.[Amount] END) FROM [dbo].[Entry] e WHERE e.[AccountID] = a.[AccountID]), 0)
               + ISNULL((SELECT SUM(e.[Amount]) FROM [dbo].[Entry] e WHERE e.[ToAccountID] = a.[AccountID] AND e.[Kind] = 3), 0) AS [Balance]
    FROM [dbo].[Account] a
    INNER JOIN [dbo].[User] u ON u.[UserID] = a.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
    ORDER BY a.[SortOrder], a.[AccountID];
END
