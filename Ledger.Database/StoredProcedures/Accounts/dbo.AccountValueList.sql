/*
    AccountValueList
    The values kept for a person's accounts valued elsewhere, from a day on, plus the last one before that day (so a
    month's change has a starting point). Oldest first.
*/
CREATE PROCEDURE [dbo].[AccountValueList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @From         DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT a.[AccountUniqueID],
           av.[ValueDate],
           av.[Value]
    FROM [dbo].[AccountValue] av
    INNER JOIN [dbo].[Account] a ON a.[AccountID] = av.[AccountID]
    INNER JOIN [dbo].[User] u ON u.[UserID] = a.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND (av.[ValueDate] >= @From
           OR av.[ValueDate] = (SELECT MAX(p.[ValueDate]) FROM [dbo].[AccountValue] p WHERE p.[AccountID] = av.[AccountID] AND p.[ValueDate] < @From))
    ORDER BY av.[ValueDate];
END
