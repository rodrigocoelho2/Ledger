CREATE TABLE [dbo].[Entry]
(
    [EntryID]       BIGINT           IDENTITY (1, 1) NOT NULL,
    [EntryUniqueID] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Entry_EntryUniqueID] DEFAULT (NEWID()),
    [UserID]        BIGINT           NOT NULL,
    [Kind]          TINYINT          NOT NULL,
    [Scope]         TINYINT          NOT NULL CONSTRAINT [DF_Entry_Scope] DEFAULT (1),
    [Name]          NVARCHAR (120)   NOT NULL,
    [Amount]        DECIMAL (14, 2)  NOT NULL,
    [EntryDate]     DATE             NOT NULL,
    [Category]      VARCHAR (32)     NULL,
    [AccountID]     BIGINT           NULL,
    [ToAccountID]   BIGINT           NULL,
    [ProjectID]     BIGINT           NULL,
    [Nights]        SMALLINT         NULL,
    [Channel]       NVARCHAR (40)    NULL,
    [CreatedAt]     DATETIME2 (3)    NOT NULL CONSTRAINT [DF_Entry_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    [PlanKey]       UNIQUEIDENTIFIER NULL,
    [PlanPart]      SMALLINT         NULL,
    [PlanParts]     SMALLINT         NULL,
    [ExternalID]    NVARCHAR (100)   NULL,
    [Review]        BIT              NULL,
    CONSTRAINT [PK_Entry] PRIMARY KEY CLUSTERED ([EntryID] ASC),
    CONSTRAINT [UQ_Entry_EntryUniqueID] UNIQUE NONCLUSTERED ([EntryUniqueID] ASC),
    CONSTRAINT [FK_Entry_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]),
    CONSTRAINT [FK_Entry_Account] FOREIGN KEY ([AccountID]) REFERENCES [dbo].[Account] ([AccountID]),
    CONSTRAINT [FK_Entry_ToAccount] FOREIGN KEY ([ToAccountID]) REFERENCES [dbo].[Account] ([AccountID]),
    CONSTRAINT [FK_Entry_Project] FOREIGN KEY ([ProjectID]) REFERENCES [dbo].[Project] ([ProjectID]),
    CONSTRAINT [CK_Entry_Kind] CHECK ([Kind] IN (1, 2, 3)),
    CONSTRAINT [CK_Entry_Scope] CHECK ([Scope] IN (1, 2, 3)),
    CONSTRAINT [CK_Entry_Amount] CHECK ([Amount] > 0)
);
GO

CREATE NONCLUSTERED INDEX [IX_Entry_UserEntryDate] ON [dbo].[Entry] ([UserID] ASC, [EntryDate] DESC);
GO

CREATE NONCLUSTERED INDEX [IX_Entry_Account] ON [dbo].[Entry] ([AccountID] ASC) INCLUDE ([Kind], [Amount]);
GO

CREATE NONCLUSTERED INDEX [IX_Entry_ToAccount] ON [dbo].[Entry] ([ToAccountID] ASC) INCLUDE ([Kind], [Amount]) WHERE [ToAccountID] IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX [IX_Entry_Plan] ON [dbo].[Entry] ([UserID] ASC, [PlanKey] ASC) WHERE [PlanKey] IS NOT NULL;
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Entry_External] ON [dbo].[Entry] ([UserID] ASC, [ExternalID] ASC) WHERE [ExternalID] IS NOT NULL;
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Every line of the ledger. Kind 1 outflow, 2 inflow, 3 transfer between two accounts (AccountID to ToAccountID). Scope says which part of life it belongs to: 1 personal, 2 short-term rental, 3 buy and sell (then ProjectID is the property). Amount is always positive; the Kind gives the direction. Category is a key of the fixed list in Ledger.Library (NULL on transfers). A rental booking is an inflow with Nights (EntryDate is the check-in) and the Channel it came through. Deleting an account or a project leaves its entries here with the link emptied. A purchase paid in instalments is one entry per instalment sharing a PlanKey, each with its PlanPart (1, 2, 3…) of PlanParts; they are dated on the day each one is paid, so some lie in the future. An entry that came from the bank keeps the bank''s transaction id in ExternalID (so a sync never brings it twice) and Review = 1 until the person opens and saves it.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Entry';
