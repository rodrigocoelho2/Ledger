/*
    EntryList
    A person's entries, newest first. Every filter is optional: the days (both ends included) and the scope
    (1 personal, 2 short-term rental, 3 buy and sell).
*/
CREATE PROCEDURE [dbo].[EntryList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @From         DATE = NULL,
    @To           DATE = NULL,
    @Scope        TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT e.[EntryUniqueID],
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
           e.[Review]
    FROM [dbo].[Entry] e
    INNER JOIN [dbo].[User] u ON u.[UserID] = e.[UserID]
    LEFT JOIN [dbo].[Account] a ON a.[AccountID] = e.[AccountID]
    LEFT JOIN [dbo].[Account] t ON t.[AccountID] = e.[ToAccountID]
    LEFT JOIN [dbo].[Project] p ON p.[ProjectID] = e.[ProjectID]
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND (@From IS NULL OR e.[EntryDate] >= @From)
      AND (@To IS NULL OR e.[EntryDate] <= @To)
      AND (@Scope IS NULL OR e.[Scope] = @Scope)
    ORDER BY e.[EntryDate] DESC, e.[EntryID] DESC;
END
