-- Migration: Rename TIN200 columns to clean names (no spaces/trailing spaces)
-- Created: 2026-03-02

SET NOCOUNT ON;

IF COL_LENGTH('dbo.TIN200', 'CeoFirstName') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'CEO First Name ') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO First Name ]', 'CeoFirstName', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'CEO First Name') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO First Name]', 'CeoFirstName', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'CeoLastName') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'CEO Last Name ') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO Last Name ]', 'CeoLastName', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'CEO Last Name') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO Last Name]', 'CeoLastName', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'Email') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'Email ') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Email ]', 'Email', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'ExternalId') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'External ID') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[External ID]', 'ExternalId', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'CompanyName') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'Company Name') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Company Name]', 'CompanyName', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'CompanyDescription') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'Company Description') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Company Description]', 'CompanyDescription', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'Fye2025') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE 2025') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[FYE 2025]', 'Fye2025', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'Fye2024') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE 2024') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[FYE 2024]', 'Fye2024', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'Fye2023') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE 2023') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[FYE 2023]', 'Fye2023', 'COLUMN';
END;

-- After running this script, run 001_CreatePowerBIViews.sql to recreate views
-- against the clean column names.
