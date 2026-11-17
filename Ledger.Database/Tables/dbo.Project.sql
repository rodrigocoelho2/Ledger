CREATE TABLE [dbo].[Project]
(
    [ProjectID]       BIGINT           IDENTITY (1, 1) NOT NULL,
    [ProjectUniqueID] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Project_ProjectUniqueID] DEFAULT (NEWID()),
    [UserID]          BIGINT           NOT NULL,
    [Name]            NVARCHAR (80)    NOT NULL,
    [TargetSale]      DECIMAL (14, 2)  NULL,
    [WorksBudget]     DECIMAL (14, 2)  NULL,
    [AgreedPrice]     DECIMAL (14, 2)  NULL,
    [CreatedAt]       DATETIME2 (3)    NOT NULL CONSTRAINT [DF_Project_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Project] PRIMARY KEY CLUSTERED ([ProjectID] ASC),
    CONSTRAINT [UQ_Project_ProjectUniqueID] UNIQUE NONCLUSTERED ([ProjectUniqueID] ASC),
    CONSTRAINT [UQ_Project_UserName] UNIQUE NONCLUSTERED ([UserID] ASC, [Name] ASC),
    CONSTRAINT [FK_Project_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID])
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'A property bought to renovate and sell (the Buy & Sell module). Its entries carry the ProjectID; what was put in, what it sold for and the return are worked out from them. AgreedPrice is the price agreed with the seller (paid in parts: deposit, then the rest at the deed); TargetSale and WorksBudget are what the person is aiming at before it sells.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Project';
