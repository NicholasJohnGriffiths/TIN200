-- Migration: Create TIN200 table if missing
-- Created: 2026-03-02

SET NOCOUNT ON;

IF OBJECT_ID('dbo.TIN200', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TIN200
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [CEOFirstName] VARCHAR(255) NULL,
        [CEOLastName] VARCHAR(255) NULL,
        [Email] VARCHAR(255) NULL,
        [ExternalID] VARCHAR(50) NULL,
        [CompanyName] VARCHAR(255) NULL,
        [CompanyDescription] VARCHAR(255) NULL,
        [FYE2025] DECIMAL(18,0) NULL,
        [FYE2024] DECIMAL(18,0) NULL,
        [FYE2023] DECIMAL(18,0) NULL,
        [TIN200] VARCHAR(50) NULL,
        [FinancialYear] INT NULL,
        CONSTRAINT [PK_TIN200] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END;

SELECT OBJECT_ID('dbo.TIN200', 'U') AS Tin200ObjectId;
