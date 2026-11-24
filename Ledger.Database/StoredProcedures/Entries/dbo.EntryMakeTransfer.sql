/*
    EntryMakeTransfer
    Turns money that came from one connected bank account into the transfer it was: the entry (an outflow or inflow brought
    from one bank account) becomes a transfer from @AccountUniqueID to @ToAccountUniqueID, carrying the bank's ids of both
    sides, so neither side counts as spending or income. Returns how many changed (0 or 1).
*/
CREATE PROCEDURE [dbo].[EntryMakeTransfer]
    @UserUniqueID      UNIQUEIDENTIFIER,
    @EntryUniqueID     UNIQUEIDENTIFIER,
    @AccountUniqueID   UNIQUEIDENTIFIER,
    @ToAccountUniqueID UNIQUEIDENTIFIER,
    @ExternalID        NVARCHAR (100),
    @ToExternalID      NVARCHAR (100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    DECLARE @AccountID BIGINT = (SELECT [AccountID] FROM [dbo].[Account] WHERE [UserID] = @UserID AND [AccountUniqueID] = @AccountUniqueID);
    DECLARE @ToAccountID BIGINT = (SELECT [AccountID] FROM [dbo].[Account] WHERE [UserID] = @UserID AND [AccountUniqueID] = @ToAccountUniqueID);
    IF @AccountID IS NULL OR @ToAccountID IS NULL OR @AccountID = @ToAccountID
    BEGIN
        SELECT 0;
        RETURN;
    END;

    UPDATE e
    SET e.[Kind] = 3,
        e.[Scope] = 1,
        e.[Category] = NULL,
        e.[ProjectID] = NULL,
        e.[Nights] = NULL,
        e.[Channel] = NULL,
        e.[AccountID] = @AccountID,
        e.[ToAccountID] = @ToAccountID,
        e.[ExternalID] = @ExternalID,
        e.[ToExternalID] = @ToExternalID
    FROM [dbo].[Entry] e
    WHERE e.[UserID] = @UserID
      AND e.[EntryUniqueID] = @EntryUniqueID
      AND e.[Kind] IN (1, 2)
      AND e.[ExternalID] IN (@ExternalID, @ToExternalID)
      AND NOT EXISTS (SELECT 1 FROM [dbo].[Entry] x
                      WHERE x.[UserID] = @UserID AND x.[EntryID] <> e.[EntryID]
                        AND (x.[ExternalID] IN (@ExternalID, @ToExternalID) OR x.[ToExternalID] IN (@ExternalID, @ToExternalID)));

    SELECT @@ROWCOUNT;
END
