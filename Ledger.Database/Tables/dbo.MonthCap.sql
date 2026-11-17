CREATE TABLE [dbo].[MonthCap]
(
    [UserID]     BIGINT          NOT NULL,
    [MonthStart] DATE            NOT NULL,
    [Amount]     DECIMAL (14, 2) NOT NULL,
    CONSTRAINT [PK_MonthCap] PRIMARY KEY CLUSTERED ([UserID] ASC, [MonthStart] ASC),
    CONSTRAINT [FK_MonthCap_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_MonthCap_Amount] CHECK ([Amount] > 0)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The most a person wants to spend (personal outflows) in a month. MonthStart is the first day of that month. A month without a row has no cap.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'MonthCap';
