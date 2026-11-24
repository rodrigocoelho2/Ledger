CREATE TABLE [dbo].[AccountValue]
(
    [AccountID] BIGINT          NOT NULL,
    [ValueDate] DATE            NOT NULL,
    [Value]     DECIMAL (14, 2) NOT NULL,
    [UpdatedAt] DATETIME2 (3)   NOT NULL CONSTRAINT [DF_AccountValue_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_AccountValue] PRIMARY KEY CLUSTERED ([AccountID] ASC, [ValueDate] ASC),
    CONSTRAINT [FK_AccountValue_Account] FOREIGN KEY ([AccountID]) REFERENCES [dbo].[Account] ([AccountID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'What an account whose value comes from elsewhere (Obol) was worth, one row per day: the last fetch of the day wins. The latest row is the account''s balance; the last row before a month began gives its change in that month.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'AccountValue';
