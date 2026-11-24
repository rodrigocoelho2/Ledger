CREATE TABLE [dbo].[Account]
(
    [AccountID]       BIGINT           IDENTITY (1, 1) NOT NULL,
    [AccountUniqueID] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Account_AccountUniqueID] DEFAULT (NEWID()),
    [UserID]          BIGINT           NOT NULL,
    [Name]            NVARCHAR (60)    NOT NULL,
    [Kind]            TINYINT          NOT NULL,
    [OpeningBalance]  DECIMAL (14, 2)  NOT NULL CONSTRAINT [DF_Account_OpeningBalance] DEFAULT (0),
    [SortOrder]       INT              NOT NULL CONSTRAINT [DF_Account_SortOrder] DEFAULT (0),
    [CreatedAt]       DATETIME2 (3)    NOT NULL CONSTRAINT [DF_Account_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    [ValueSource]     TINYINT          NULL,
    [ValueRef]        NVARCHAR (64)    NULL,
    CONSTRAINT [PK_Account] PRIMARY KEY CLUSTERED ([AccountID] ASC),
    CONSTRAINT [UQ_Account_AccountUniqueID] UNIQUE NONCLUSTERED ([AccountUniqueID] ASC),
    CONSTRAINT [FK_Account_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]),
    CONSTRAINT [CK_Account_Kind] CHECK ([Kind] IN (1, 2, 3, 4)),
    CONSTRAINT [CK_Account_ValueSource] CHECK ([ValueSource] IS NULL OR [ValueSource] IN (1))
);
GO

CREATE NONCLUSTERED INDEX [IX_Account_UserSortOrder] ON [dbo].[Account] ([UserID] ASC, [SortOrder] ASC);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Where the money sits: Kind 1 current account, 2 savings, 3 investments, 4 meal card. The balance is never stored: it is OpeningBalance plus every entry that touched the account. SortOrder is the order the person dragged them into. An account whose value comes from elsewhere has a ValueSource (1: Obol, with ValueRef the user name there): its balance is then the last value kept in dbo.AccountValue.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Account';
