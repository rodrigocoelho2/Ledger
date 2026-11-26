/*
    UserGet
    One user by its public identifier, with what they chose in Settings.
*/
CREATE PROCEDURE [dbo].[UserGet]
    @UserUniqueID UNIQUEIDENTIFIER
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
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
