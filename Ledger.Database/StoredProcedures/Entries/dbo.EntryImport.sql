/*
    EntryImport
    Adds many entries at once (a CSV or JSON import), as JSON:
    [{"k":1,"s":1,"n":"Lunch","a":9.80,"d":"2026-10-30","c":"groceries","ac":"6f9c…","to":null,"p":null,"ni":null,"ch":null}, …]
    (k kind, s scope, n name, a amount, d day, c category, ac account, to the account a transfer goes to, p project,
    ni nights, ch channel; pk, pp and pn: the instalment plan, the part and how many parts). Rows without a name, a day or an amount above zero are skipped. Returns how many were added.
*/
CREATE PROCEDURE [dbo].[EntryImport]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Rows         NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL
    BEGIN
        SELECT 0;
        RETURN;
    END

    INSERT INTO [dbo].[Entry] ([UserID], [Kind], [Scope], [Name], [Amount], [EntryDate], [Category], [AccountID], [ToAccountID], [ProjectID], [Nights], [Channel], [PlanKey], [PlanPart], [PlanParts])
    SELECT @UserID, r.[Kind], r.[Scope], r.[Name], r.[Amount], r.[EntryDate], r.[Category], a.[AccountID], t.[AccountID], p.[ProjectID], r.[Nights], r.[Channel],
           TRY_CAST(r.[PlanKey] AS UNIQUEIDENTIFIER), r.[PlanPart], r.[PlanParts]
    FROM OPENJSON(@Rows) WITH (
        [Kind] TINYINT '$.k', [Scope] TINYINT '$.s', [Name] NVARCHAR (120) '$.n', [Amount] DECIMAL (14, 2) '$.a',
        [EntryDate] DATE '$.d', [Category] VARCHAR (32) '$.c', [AccountKey] NVARCHAR (36) '$.ac', [ToAccountKey] NVARCHAR (36) '$.to',
        [ProjectKey] NVARCHAR (36) '$.p', [Nights] SMALLINT '$.ni', [Channel] NVARCHAR (40) '$.ch',
        [PlanKey] NVARCHAR (36) '$.pk', [PlanPart] SMALLINT '$.pp', [PlanParts] SMALLINT '$.pn') AS r
    LEFT JOIN [dbo].[Account] a ON a.[AccountUniqueID] = TRY_CAST(r.[AccountKey] AS UNIQUEIDENTIFIER) AND a.[UserID] = @UserID
    LEFT JOIN [dbo].[Account] t ON t.[AccountUniqueID] = TRY_CAST(r.[ToAccountKey] AS UNIQUEIDENTIFIER) AND t.[UserID] = @UserID
    LEFT JOIN [dbo].[Project] p ON p.[ProjectUniqueID] = TRY_CAST(r.[ProjectKey] AS UNIQUEIDENTIFIER) AND p.[UserID] = @UserID
    WHERE r.[Kind] IN (1, 2, 3)
      AND r.[Scope] IN (1, 2, 3)
      AND r.[Amount] > 0
      AND r.[EntryDate] IS NOT NULL
      AND r.[Name] IS NOT NULL;

    SELECT @@ROWCOUNT;
END
