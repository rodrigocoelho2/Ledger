/*
    UserGetByName
    One user by user name (ignoring case).
*/
CREATE PROCEDURE [dbo].[UserGetByName]
    @Username NVARCHAR (32)
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
           s.[FlipsEnabled],
           s.[PayDay],
           s.[Payer]
    FROM [dbo].[User] u
    LEFT JOIN [dbo].[Setting] s ON s.[UserID] = u.[UserID]
    WHERE u.[Username] = @Username;
END
