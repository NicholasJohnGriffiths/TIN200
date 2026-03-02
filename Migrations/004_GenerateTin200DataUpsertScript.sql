-- Migration helper: Generate an UPSERT script for all TIN200 data
-- Run this on LOCAL database (source), then copy output and run it on AZURE database (target).
-- Created: 2026-03-02

SET NOCOUNT ON;

IF OBJECT_ID('dbo.TIN200', 'U') IS NULL
BEGIN
    RAISERROR('Table dbo.TIN200 was not found.', 16, 1);
    RETURN;
END;

SELECT '-- BEGIN GENERATED TIN200 DATA UPSERT SCRIPT';
SELECT 'SET NOCOUNT ON;';
SELECT 'BEGIN TRANSACTION;';
SELECT 'SET IDENTITY_INSERT dbo.TIN200 ON;';

SELECT
    'MERGE dbo.TIN200 AS target ' +
    'USING (SELECT ' +
        CAST(t.Id AS VARCHAR(20)) + ' AS Id, ' +
        CASE WHEN t.CEOFirstName IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(t.CEOFirstName, '''', '''''') + '''' END + ' AS CEOFirstName, ' +
        CASE WHEN t.CEOLastName IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(t.CEOLastName, '''', '''''') + '''' END + ' AS CEOLastName, ' +
        CASE WHEN t.Email IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(t.Email, '''', '''''') + '''' END + ' AS Email, ' +
        CASE WHEN t.ExternalID IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(t.ExternalID, '''', '''''') + '''' END + ' AS ExternalID, ' +
        CASE WHEN t.CompanyName IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(t.CompanyName, '''', '''''') + '''' END + ' AS CompanyName, ' +
        CASE WHEN t.CompanyDescription IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(t.CompanyDescription, '''', '''''') + '''' END + ' AS CompanyDescription, ' +
        CASE WHEN t.FYE2025 IS NULL THEN 'NULL' ELSE CONVERT(VARCHAR(50), t.FYE2025) END + ' AS FYE2025, ' +
        CASE WHEN t.FYE2024 IS NULL THEN 'NULL' ELSE CONVERT(VARCHAR(50), t.FYE2024) END + ' AS FYE2024, ' +
        CASE WHEN t.FYE2023 IS NULL THEN 'NULL' ELSE CONVERT(VARCHAR(50), t.FYE2023) END + ' AS FYE2023, ' +
        CASE WHEN t.TIN200 IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(t.TIN200, '''', '''''') + '''' END + ' AS TIN200, ' +
        CASE WHEN t.FinancialYear IS NULL THEN 'NULL' ELSE CAST(t.FinancialYear AS VARCHAR(20)) END + ' AS FinancialYear' +
    ') AS source ' +
    'ON target.Id = source.Id ' +
    'WHEN MATCHED THEN UPDATE SET ' +
        'target.CEOFirstName = source.CEOFirstName, ' +
        'target.CEOLastName = source.CEOLastName, ' +
        'target.Email = source.Email, ' +
        'target.ExternalID = source.ExternalID, ' +
        'target.CompanyName = source.CompanyName, ' +
        'target.CompanyDescription = source.CompanyDescription, ' +
        'target.FYE2025 = source.FYE2025, ' +
        'target.FYE2024 = source.FYE2024, ' +
        'target.FYE2023 = source.FYE2023, ' +
        'target.TIN200 = source.TIN200, ' +
        'target.FinancialYear = source.FinancialYear ' +
    'WHEN NOT MATCHED BY TARGET THEN INSERT ' +
        '(Id, CEOFirstName, CEOLastName, Email, ExternalID, CompanyName, CompanyDescription, FYE2025, FYE2024, FYE2023, TIN200, FinancialYear) VALUES ' +
        '(source.Id, source.CEOFirstName, source.CEOLastName, source.Email, source.ExternalID, source.CompanyName, source.CompanyDescription, source.FYE2025, source.FYE2024, source.FYE2023, source.TIN200, source.FinancialYear);'
FROM dbo.TIN200 t
ORDER BY t.Id;

SELECT 'SET IDENTITY_INSERT dbo.TIN200 OFF;';
SELECT 'COMMIT TRANSACTION;';
SELECT '-- END GENERATED TIN200 DATA UPSERT SCRIPT';
