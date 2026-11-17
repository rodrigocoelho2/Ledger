/*
    CapList
    Every month a person set a spending cap for.
*/
CREATE PROCEDURE [dbo].[CapList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT c.[MonthStart],
           c.[Amount]
    FROM [dbo].[MonthCap] c
    INNER JOIN [dbo].[User] u ON u.[UserID] = c.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
    ORDER BY c.[MonthStart];
END
