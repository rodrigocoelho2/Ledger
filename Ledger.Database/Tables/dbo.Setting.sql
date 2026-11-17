CREATE TABLE [dbo].[Setting]
(
    [UserID]        BIGINT        NOT NULL,
    [Language]      CHAR (2)      NOT NULL CONSTRAINT [DF_Setting_Language] DEFAULT ('en'),
    [RentalEnabled] BIT           NOT NULL CONSTRAINT [DF_Setting_RentalEnabled] DEFAULT (0),
    [FlipsEnabled]  BIT           NOT NULL CONSTRAINT [DF_Setting_FlipsEnabled] DEFAULT (0),
    [UpdatedAt]     DATETIME2 (3) NOT NULL CONSTRAINT [DF_Setting_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Setting] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [FK_Setting_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_Setting_Language] CHECK ([Language] IN ('en', 'pt'))
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'What a person chose in Settings: the language (en or pt) and which optional modules are on (short-term rental, buy and sell). No row means the defaults: English, both modules off. Switching a module off never deletes what was logged in it.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Setting';
