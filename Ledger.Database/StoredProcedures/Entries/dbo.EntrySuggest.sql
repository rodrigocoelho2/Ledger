/*
    EntrySuggest
    The last entry a person wrote with the same words, for quick add: it lends its kind, category, account and scope.
    An exact name wins over one that only starts with the words; @Like is the "starts with" pattern, already escaped.
*/
CREATE PROCEDURE [dbo].[EntrySuggest]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Name         NVARCHAR (120),
    @Like         NVARCHAR (250)
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT TOP (1) e.[EntryUniqueID],
           e.[Kind],
           e.[Scope],
           e.[Name],
           e.[Amount],
           e.[EntryDate],
           e.[Category],
           a.[AccountUniqueID],
           t.[AccountUniqueID] AS [ToAccountUniqueID],
           p.[ProjectUniqueID],
           e.[Nights],
           e.[Channel],
           e.[CreatedAt],
           e.[PlanKey],
           e.[PlanPart],
           e.[PlanParts],
           e.[ExternalID],
           e.[Review],
           e.[ToExternalID]
    FROM [dbo].[Entry] e
    INNER JOIN [dbo].[User] u ON u.[UserID] = e.[UserID]
    LEFT JOIN [dbo].[Account] a ON a.[AccountID] = e.[AccountID]
    LEFT JOIN [dbo].[Account] t ON t.[AccountID] = e.[ToAccountID]
    LEFT JOIN [dbo].[Project] p ON p.[ProjectID] = e.[ProjectID]
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND e.[Kind] <> 3
      AND (e.[Name] = @Name OR e.[Name] LIKE @Like ESCAPE '\')
    ORDER BY CASE WHEN e.[Name] = @Name THEN 0 ELSE 1 END, e.[EntryDate] DESC, e.[EntryID] DESC;
END
