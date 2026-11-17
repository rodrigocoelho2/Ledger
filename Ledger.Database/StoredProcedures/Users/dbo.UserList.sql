/*
    UserList
    Every user, oldest first.
*/
CREATE PROCEDURE [dbo].[UserList]
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT u.[UserID],
           u.[UserUniqueID],
           u.[Username],
           u.[PasswordHash],
           u.[IsAdmin],
           u.[SecurityStamp],
           u.[CreatedAt],
           u.[LastLoginAt],
           u.[DiscordID],
           u.[DiscordName],
           s.[Language],
           s.[RentalEnabled],
           s.[FlipsEnabled]
    FROM [dbo].[User] u
    LEFT JOIN [dbo].[Setting] s ON s.[UserID] = u.[UserID]
    ORDER BY u.[UserID];
END
