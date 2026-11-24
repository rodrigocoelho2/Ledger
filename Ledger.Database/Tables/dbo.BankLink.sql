CREATE TABLE [dbo].[BankLink]
(
    [BankLinkID]       BIGINT           IDENTITY (1, 1) NOT NULL,
    [BankLinkUniqueID] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_BankLink_BankLinkUniqueID] DEFAULT (NEWID()),
    [UserID]           BIGINT           NOT NULL,
    [AccountID]        BIGINT           NULL,
    [SessionID]        NVARCHAR (64)    NOT NULL,
    [AccountUid]       NVARCHAR (64)    NOT NULL,
    [BankName]         NVARCHAR (80)    NOT NULL,
    [BankCountry]      CHAR (2)         NOT NULL,
    [AccountLabel]     NVARCHAR (80)    NULL,
    [IbanTail]         VARCHAR (8)      NULL,
    [ValidUntil]       DATETIME2 (3)    NULL,
    [ImportFrom]       DATE             NULL,
    [LastSyncAt]       DATETIME2 (3)    NULL,
    [LastError]        NVARCHAR (400)   NULL,
    [BankBalance]      DECIMAL (14, 2)  NULL,
    [CreatedAt]        DATETIME2 (3)    NOT NULL CONSTRAINT [DF_BankLink_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_BankLink] PRIMARY KEY CLUSTERED ([BankLinkID] ASC),
    CONSTRAINT [UQ_BankLink_BankLinkUniqueID] UNIQUE NONCLUSTERED ([BankLinkUniqueID] ASC),
    CONSTRAINT [UQ_BankLink_UserAccountUid] UNIQUE NONCLUSTERED ([UserID] ASC, [AccountUid] ASC),
    CONSTRAINT [FK_BankLink_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]),
    CONSTRAINT [FK_BankLink_Account] FOREIGN KEY ([AccountID]) REFERENCES [dbo].[Account] ([AccountID])
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'A bank account a person connected through Enable Banking (open banking, read only). SessionID and AccountUid are Enable Banking''s; AccountID is the Ledger account its transactions go into (NULL until the person picks one). Only the last four digits of the IBAN are kept. ValidUntil is when the bank''s consent ends and it must be renewed; BankBalance is the balance the bank reported on the last sync.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'BankLink';
