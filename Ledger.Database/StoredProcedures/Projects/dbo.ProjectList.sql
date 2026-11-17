/*
    ProjectList
    A person's buy-and-sell projects, oldest first.
*/
CREATE PROCEDURE [dbo].[ProjectList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT p.[ProjectUniqueID],
           p.[Name],
           p.[TargetSale],
           p.[WorksBudget],
           p.[AgreedPrice],
           p.[CreatedAt]
    FROM [dbo].[Project] p
    INNER JOIN [dbo].[User] u ON u.[UserID] = p.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
    ORDER BY p.[ProjectID];
END
