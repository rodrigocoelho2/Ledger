/*
    AccountList
    A person's accounts in the order they chose, each with its balance: the opening balance, plus the inflows, minus the
    outflows and the transfers out, plus the transfers in, counting only what happened up to @AsOf (today, in the app's
    time zone): instalments still to come don't touch the balance until their day. Movement is that sum without the
    opening balance; for an account valued elsewhere (ValueSource) the service uses the last Value instead. The bank a
    linked account syncs from comes with it.
*/
CREATE PROCEDURE [dbo].[AccountList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @AsOf         DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT a.[AccountUniqueID],
           a.[Name],
           a.[Kind],
           a.[OpeningBalance],
           a.[SortOrder],
           a.[ValueSource],
           a.[ValueRef],
           v.[Value],
           v.[ValueDate],
           b.[BankName],
           b.[LastSyncAt] AS [BankSyncedAt],
           b.[BankBalance],
           a.[OpeningBalance]
               + ISNULL((SELECT SUM(CASE WHEN e.[Kind] = 2 THEN e.[Amount] ELSE -e.[Amount] END) FROM [dbo].[Entry] e WHERE e.[AccountID] = a.[AccountID] AND (@AsOf IS NULL OR e.[EntryDate] <= @AsOf)), 0)
               + ISNULL((SELECT SUM(e.[Amount]) FROM [dbo].[Entry] e WHERE e.[ToAccountID] = a.[AccountID] AND e.[Kind] = 3 AND (@AsOf IS NULL OR e.[EntryDate] <= @AsOf)), 0) AS [Movement]
    FROM [dbo].[Account] a
    INNER JOIN [dbo].[User] u ON u.[UserID] = a.[UserID]
    OUTER APPLY (SELECT TOP (1) av.[Value], av.[ValueDate] FROM [dbo].[AccountValue] av WHERE av.[AccountID] = a.[AccountID] ORDER BY av.[ValueDate] DESC) v
    OUTER APPLY (SELECT TOP (1) bl.[BankName], bl.[LastSyncAt], bl.[BankBalance] FROM [dbo].[BankLink] bl WHERE bl.[AccountID] = a.[AccountID] ORDER BY bl.[BankLinkID]) b
    WHERE u.[UserUniqueID] = @UserUniqueID
    ORDER BY a.[SortOrder], a.[AccountID];
END
