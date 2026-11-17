/*
    AccountReorder
    Keeps the order a person dragged their accounts into. @Order is a JSON array of account identifiers, first to last:
    ["6f9c…", "0b1e…", …]. Accounts left out keep their place.
*/
CREATE PROCEDURE [dbo].[AccountReorder]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Order        NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL
        RETURN;

    UPDATE a
    SET a.[SortOrder] = CAST(o.[key] AS INT) + 1
    FROM [dbo].[Account] a
    INNER JOIN OPENJSON(@Order) o ON TRY_CAST(o.[value] AS UNIQUEIDENTIFIER) = a.[AccountUniqueID]
    WHERE a.[UserID] = @UserID;
END
