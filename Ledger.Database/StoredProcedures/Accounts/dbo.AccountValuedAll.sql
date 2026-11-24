/*
    AccountValuedAll
    Every account, of every person, whose value comes from elsewhere: what the background refresh goes through.
*/
CREATE PROCEDURE [dbo].[AccountValuedAll]
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT u.[UserUniqueID],
           u.[Username],
           a.[AccountUniqueID],
           a.[ValueSource],
           a.[ValueRef]
    FROM [dbo].[Account] a
    INNER JOIN [dbo].[User] u ON u.[UserID] = a.[UserID]
    WHERE a.[ValueSource] IS NOT NULL
    ORDER BY a.[AccountID];
END
