/*
    BankLinkSave
    Keeps a bank account a person just authorised: a new one, or the same account again with a fresh session (after the
    consent was renewed), which keeps its Ledger account and its history. Returns the link's identifier.
*/
CREATE PROCEDURE [dbo].[BankLinkSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @SessionID    NVARCHAR (64),
    @AccountUid   NVARCHAR (64),
    @BankName     NVARCHAR (80),
    @BankCountry  CHAR (2),
    @AccountLabel NVARCHAR (80),
    @IbanTail     VARCHAR (8),
    @ValidUntil   DATETIME2 (3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL
    BEGIN
        SELECT CAST(NULL AS UNIQUEIDENTIFIER);
        RETURN;
    END

    BEGIN TRANSACTION;

    DECLARE @Key UNIQUEIDENTIFIER = (
        SELECT [BankLinkUniqueID]
        FROM [dbo].[BankLink] WITH (UPDLOCK, HOLDLOCK)
        WHERE [UserID] = @UserID
          AND ([AccountUid] = @AccountUid
               OR ([IbanTail] = @IbanTail AND @IbanTail IS NOT NULL AND [BankName] = @BankName)));

    IF @Key IS NULL
    BEGIN
        SET @Key = NEWID();

        INSERT INTO [dbo].[BankLink] ([BankLinkUniqueID], [UserID], [SessionID], [AccountUid], [BankName], [BankCountry], [AccountLabel], [IbanTail], [ValidUntil])
        VALUES (@Key, @UserID, @SessionID, @AccountUid, @BankName, @BankCountry, @AccountLabel, @IbanTail, @ValidUntil);
    END
    ELSE
    BEGIN
        UPDATE [dbo].[BankLink]
        SET [SessionID] = @SessionID,
            [AccountUid] = @AccountUid,
            [AccountLabel] = @AccountLabel,
            [ValidUntil] = @ValidUntil,
            [LastError] = NULL
        WHERE [BankLinkUniqueID] = @Key;
    END

    COMMIT TRANSACTION;

    SELECT @Key;
END
