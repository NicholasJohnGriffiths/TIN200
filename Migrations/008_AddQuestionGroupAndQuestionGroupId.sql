IF OBJECT_ID(N'[dbo].[QuestionGroup]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QuestionGroup](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [Title] [nvarchar](max) NULL,
        [Description] [nvarchar](max) NULL,
        [OrderNumber] [int] NULL,
        [ImageId1] [int] NULL,
        [ImageId2] [int] NULL,
        [ImageId3] [int] NULL,
        CONSTRAINT [PK_QuestionGroup] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY];
END;
GO

IF OBJECT_ID(N'[dbo].[Image]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Image](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [EntityType] [varchar](50) NOT NULL,
        [EntityId] [int] NOT NULL,
        [FileName] [varchar](255) NOT NULL,
        [FilePath] [varchar](500) NOT NULL,
        [FileType] [varchar](50) NOT NULL,
        [FileSize] [int] NULL,
        [CreatedDate] [datetime2] NOT NULL CONSTRAINT [DF_Image_CreatedDate] DEFAULT (GETDATE()),
        CONSTRAINT [PK_Image] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY];
END;
GO

IF COL_LENGTH('dbo.QuestionGroup', 'ImageId1') IS NULL
BEGIN
    ALTER TABLE [dbo].[QuestionGroup]
    ADD [ImageId1] [int] NULL;
END;
GO

IF COL_LENGTH('dbo.QuestionGroup', 'ImageId2') IS NULL
BEGIN
    ALTER TABLE [dbo].[QuestionGroup]
    ADD [ImageId2] [int] NULL;
END;
GO

IF COL_LENGTH('dbo.QuestionGroup', 'ImageId3') IS NULL
BEGIN
    ALTER TABLE [dbo].[QuestionGroup]
    ADD [ImageId3] [int] NULL;
END;
GO

IF COL_LENGTH('dbo.Question', 'GroupId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Question]
    ADD [GroupId] [int] NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_Question_QuestionGroup'
)
BEGIN
    ALTER TABLE [dbo].[Question]
    ADD CONSTRAINT [FK_Question_QuestionGroup]
        FOREIGN KEY ([GroupId]) REFERENCES [dbo].[QuestionGroup]([Id]);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_QuestionGroup_Image1'
)
BEGIN
    ALTER TABLE [dbo].[QuestionGroup]
    ADD CONSTRAINT [FK_QuestionGroup_Image1]
        FOREIGN KEY ([ImageId1]) REFERENCES [dbo].[Image]([Id]);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_QuestionGroup_Image2'
)
BEGIN
    ALTER TABLE [dbo].[QuestionGroup]
    ADD CONSTRAINT [FK_QuestionGroup_Image2]
        FOREIGN KEY ([ImageId2]) REFERENCES [dbo].[Image]([Id]);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_QuestionGroup_Image3'
)
BEGIN
    ALTER TABLE [dbo].[QuestionGroup]
    ADD CONSTRAINT [FK_QuestionGroup_Image3]
        FOREIGN KEY ([ImageId3]) REFERENCES [dbo].[Image]([Id]);
END;
GO
