/*
    UserCount
    How many users there are (none: the first one to be created becomes the admin).
*/
CREATE PROCEDURE [dbo].[UserCount]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) FROM [dbo].[User];
END
