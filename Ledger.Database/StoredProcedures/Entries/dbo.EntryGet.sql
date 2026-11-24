/*
    EntryGet
    One entry of a person.
*/
CREATE PROCEDURE [dbo].[EntryGet]
    @UserUniqueID  UNIQUEIDENTIFIER,
    @EntryUniqueID UNIQUEIDENTIFIER
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
           e.[PlanParts]
    FROM [dbo].[Entry] e
    INNER JOIN [dbo].[User] u ON u.[UserID] = e.[UserID]
    LEFT JOIN [dbo].[Account] a ON a.[AccountID] = e.[AccountID]
    LEFT JOIN [dbo].[Account] t ON t.[AccountID] = e.[ToAccountID]
    LEFT JOIN [dbo].[Project] p ON p.[ProjectID] = e.[ProjectID]
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND e.[EntryUniqueID] = @EntryUniqueID;
END
