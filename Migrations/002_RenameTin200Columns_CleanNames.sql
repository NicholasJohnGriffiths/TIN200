-- Migration: Rename TIN200 columns to clean names (no spaces/trailing spaces)
-- Created: 2026-03-02

SET NOCOUNT ON;

IF COL_LENGTH('dbo.TIN200', 'CEOFirstName') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'CEO First Name ') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO First Name ]', 'CEOFirstName', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'CEO First Name') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO First Name]', 'CEOFirstName', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'CEOLastName') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'CEO Last Name ') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO Last Name ]', 'CEOLastName', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'CEO Last Name') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO Last Name]', 'CEOLastName', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'Email') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'Email ') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Email ]', 'Email', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'ExternalID') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'External ID') IS NOT NULL
    EXEC sp_rename 'dbo.TIN200.[External ID]', 'ExternalID', 'COLUMN';
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

IF COL_LENGTH('dbo.TIN200', 'FYE2025') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE 2025') IS NOT NULL
    EXEC sp_rename 'dbo.TIN200.[FYE 2025]', 'FYE2025', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'FYE2024') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE 2024') IS NOT NULL
    EXEC sp_rename 'dbo.TIN200.[FYE 2024]', 'FYE2024', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'FYE2023') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE 2023') IS NOT NULL
    EXEC sp_rename 'dbo.TIN200.[FYE 2023]', 'FYE2023', 'COLUMN';
END;

-- After running this script, run 001_CreatePowerBIViews.sql to recreate views
-- against the clean column names.
