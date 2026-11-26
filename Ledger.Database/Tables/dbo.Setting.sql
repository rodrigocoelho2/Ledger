CREATE TABLE [dbo].[Setting]
(
    [UserID]        BIGINT        NOT NULL,
    [Language]      CHAR (2)      NOT NULL CONSTRAINT [DF_Setting_Language] DEFAULT ('en'),
    [RentalEnabled] BIT           NOT NULL CONSTRAINT [DF_Setting_RentalEnabled] DEFAULT (0),
    [FlipsEnabled]  BIT           NOT NULL CONSTRAINT [DF_Setting_FlipsEnabled] DEFAULT (0),
    [PayDay]        TINYINT       NULL,
    [Payer]         NVARCHAR (64) NULL,
    [UpdatedAt]     DATETIME2 (3) NOT NULL CONSTRAINT [DF_Setting_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Setting] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [FK_Setting_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_Setting_Language] CHECK ([Language] IN ('en', 'pt')),
    CONSTRAINT [CK_Setting_PayDay] CHECK ([PayDay] IS NULL OR [PayDay] BETWEEN 2 AND 31)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'What a person chose in Settings: the language (en or pt) and which optional modules are on (short-term rental, buy and sell). PayDay is the day of the month the salary is due (moved back to the business day before on weekends and holidays): months then run from one salary to the next instead of following the calendar; NULL means calendar months. Payer is a word of the salary''s bank description (the employer): a salary with it near the payday opens the month on the day it came in. No row means the defaults: English, both modules off, calendar months. Switching a module off never deletes what was logged in it.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Setting';
