CREATE TABLE [dbo].[BankIgnore]
(
    [UserID]     BIGINT         NOT NULL,
    [ExternalID] NVARCHAR (100) NOT NULL,
    [CreatedAt]  DATETIME2 (3)  NOT NULL CONSTRAINT [DF_BankIgnore_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_BankIgnore] PRIMARY KEY CLUSTERED ([UserID] ASC, [ExternalID] ASC),
    CONSTRAINT [FK_BankIgnore_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Bank transactions a person deleted after they were imported: the next sync leaves them out instead of bringing them back.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'BankIgnore';
