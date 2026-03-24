-- Migration: Rename FYE columns to semantic names
-- FYE2025 -> FYELastFinancialYear
-- FYE2024 -> [FYEYear-1]
-- FYE2023 -> [FYEYear-2]
-- Created: 2026-03-25

SET NOCOUNT ON;

IF OBJECT_ID('dbo.TIN200', 'U') IS NULL
BEGIN
    RAISERROR('Table dbo.TIN200 was not found.', 16, 1);
    RETURN;
END;

-- Rename FYE2025 -> FYELastFinancialYear
IF COL_LENGTH('dbo.TIN200', 'FYELastFinancialYear') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE2025') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[FYE2025]', 'FYELastFinancialYear', 'COLUMN';
END;

-- Rename FYE2024 -> [FYEYear-1]
IF COL_LENGTH('dbo.TIN200', 'FYEYear-1') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE2024') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[FYE2024]', 'FYEYear-1', 'COLUMN';
END;

-- Rename FYE2023 -> [FYEYear-2]
IF COL_LENGTH('dbo.TIN200', 'FYEYear-2') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE2023') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[FYE2023]', 'FYEYear-2', 'COLUMN';
END;

GO

-- =============================================================================
-- Recreate PowerBI Views with updated column names
-- =============================================================================

-- View 1: Company Financial Analytics
CREATE OR ALTER VIEW vw_CompanyFinancialAnalytics AS
SELECT
    t.Id,
    t.CompanyName,
    t.CompanyDescription,
    CONCAT(t.CEOFirstName, ' ', t.CEOLastName) AS CeoFullName,
    t.CEOFirstName,
    t.CEOLastName,
    t.Email AS ContactEmail,
    t.ExternalID,
    t.FYELastFinancialYear AS Revenue2025,
    t.FYEYear1 AS Revenue2024,
    t.FYEYear2 AS Revenue2023,
    t.TIN200 AS TINNumber,
    CASE
        WHEN t.[FYEYear-1] > 0
        THEN CAST((t.FYELastFinancialYear - t.[FYEYear-1]) AS DECIMAL(18,2))
        ELSE NULL
    END AS GrowthAmount_2025vs2024,
    CASE
        WHEN t.[FYEYear-1] > 0
        THEN CAST(ROUND(((t.FYELastFinancialYear - t.[FYEYear-1]) / t.[FYEYear-1]) * 100, 2) AS DECIMAL(10,2))
        ELSE NULL
    END AS GrowthPercent_2025vs2024,
    CASE
        WHEN t.[FYEYear-2] > 0
        THEN CAST((t.[FYEYear-1] - t.[FYEYear-2]) AS DECIMAL(18,2))
        ELSE NULL
    END AS GrowthAmount_2024vs2023,
    CASE
        WHEN t.[FYEYear-2] > 0
        THEN CAST(ROUND(((t.[FYEYear-1] - t.[FYEYear-2]) / t.[FYEYear-2]) * 100, 2) AS DECIMAL(10,2))
        ELSE NULL
    END AS GrowthPercent_2024vs2023,
    CASE
        WHEN (t.FYELastFinancialYear + t.[FYEYear-1]) > 0
        THEN CAST((t.FYELastFinancialYear + t.[FYEYear-1]) / 2 AS DECIMAL(18,0))
        ELSE NULL
    END AS AverageRevenue_2Year,
    CASE
        WHEN (t.FYELastFinancialYear + t.[FYEYear-1] + t.[FYEYear-2]) > 0
        THEN CAST((t.FYELastFinancialYear + t.[FYEYear-1] + t.[FYEYear-2]) / 3 AS DECIMAL(18,0))
        ELSE NULL
    END AS AverageRevenue_3Year,
    CASE
        WHEN t.FYELastFinancialYear > t.[FYEYear-1] AND t.[FYEYear-1] > t.[FYEYear-2] THEN 'Uptrend'
        WHEN t.FYELastFinancialYear < t.[FYEYear-1] AND t.[FYEYear-1] < t.[FYEYear-2] THEN 'Downtrend'
        WHEN t.FYELastFinancialYear > t.[FYEYear-1] AND t.[FYEYear-1] < t.[FYEYear-2] THEN 'Recovery'
        WHEN t.FYELastFinancialYear < t.[FYEYear-1] AND t.[FYEYear-1] > t.[FYEYear-2] THEN 'Decline'
        ELSE 'Stable'
    END AS RevenueTrend,
    CASE
        WHEN t.FYELastFinancialYear >= 1000000 THEN 'Large'
        WHEN t.FYELastFinancialYear >= 500000 THEN 'Medium'
        WHEN t.FYELastFinancialYear >= 100000 THEN 'Small'
        ELSE 'Micro'
    END AS RevenueSize,
    GETDATE() AS LastRefreshTime
FROM TIN200 t
WHERE t.CompanyName IS NOT NULL AND t.CompanyName != '';

GO

-- View 2: Financial Year Comparison
CREATE OR ALTER VIEW vw_FinancialYearComparison AS
SELECT
    CompanyName,
    CONCAT(CEOFirstName, ' ', CEOLastName) AS CeoFullName,
    2025 AS FiscalYear,
    FYELastFinancialYear AS Revenue,
    ExternalID,
    Email
FROM TIN200
WHERE FYELastFinancialYear IS NOT NULL

UNION ALL

SELECT
    CompanyName,
    CONCAT(CEOFirstName, ' ', CEOLastName) AS CeoFullName,
    2024 AS FiscalYear,
    [FYEYear-1] AS Revenue,
    ExternalID,
    Email
FROM TIN200
WHERE [FYEYear-1] IS NOT NULL

UNION ALL

SELECT
    CompanyName,
    CONCAT(CEOFirstName, ' ', CEOLastName) AS CeoFullName,
    2023 AS FiscalYear,
    [FYEYear-2] AS Revenue,
    ExternalID,
    Email
FROM TIN200
WHERE [FYEYear-2] IS NOT NULL;

GO

-- View 3: Revenue Summary by Company Size
CREATE OR ALTER VIEW vw_RevenueSummaryBySize AS
SELECT
    CASE
        WHEN FYELastFinancialYear >= 1000000 THEN 'Large'
        WHEN FYELastFinancialYear >= 500000 THEN 'Medium'
        WHEN FYELastFinancialYear >= 100000 THEN 'Small'
        ELSE 'Micro'
    END AS RevenueSize,
    COUNT(*) AS CompanyCount,
    CAST(SUM(FYELastFinancialYear) AS DECIMAL(18,0)) AS TotalRevenue2025,
    CAST(AVG(FYELastFinancialYear) AS DECIMAL(18,0)) AS AverageRevenue2025,
    CAST(MIN(FYELastFinancialYear) AS DECIMAL(18,0)) AS MinRevenue2025,
    CAST(MAX(FYELastFinancialYear) AS DECIMAL(18,0)) AS MaxRevenue2025,
    CAST(SUM([FYEYear-1]) AS DECIMAL(18,0)) AS TotalRevenue2024,
    CAST(AVG([FYEYear-1]) AS DECIMAL(18,0)) AS AverageRevenue2024,
    CAST(SUM([FYEYear-2]) AS DECIMAL(18,0)) AS TotalRevenue2023,
    CAST(AVG([FYEYear-2]) AS DECIMAL(18,0)) AS AverageRevenue2023
FROM TIN200
WHERE CompanyName IS NOT NULL AND CompanyName != ''
GROUP BY
    CASE
        WHEN FYELastFinancialYear >= 1000000 THEN 'Large'
        WHEN FYELastFinancialYear >= 500000 THEN 'Medium'
        WHEN FYELastFinancialYear >= 100000 THEN 'Small'
        ELSE 'Micro'
    END;

GO

-- View 4: Top Performers Dashboard
CREATE OR ALTER VIEW vw_TopPerformersAnalytics AS
SELECT TOP 100
    ROW_NUMBER() OVER (ORDER BY t.FYELastFinancialYear DESC) AS RankByRevenue2025,
    t.CompanyName,
    CONCAT(t.CEOFirstName, ' ', t.CEOLastName) AS CeoFullName,
    t.FYELastFinancialYear AS Revenue2025,
    t.[FYEYear-1] AS Revenue2024,
    t.[FYEYear-2] AS Revenue2023,
    CASE
        WHEN t.[FYEYear-1] > 0
        THEN CAST(ROUND(((t.FYELastFinancialYear - t.[FYEYear-1]) / t.[FYEYear-1]) * 100, 2) AS DECIMAL(10,2))
        ELSE NULL
    END AS YoYGrowth2025,
    CASE
        WHEN t.FYELastFinancialYear > t.[FYEYear-1] AND t.[FYEYear-1] > t.[FYEYear-2] THEN 'Strong Uptrend'
        WHEN t.FYELastFinancialYear > t.[FYEYear-1] THEN 'Growing'
        WHEN t.FYELastFinancialYear < t.[FYEYear-1] THEN 'Declining'
        ELSE 'Stable'
    END AS PerformanceTrend,
    t.Email AS ContactEmail
FROM TIN200 t
WHERE t.CompanyName IS NOT NULL
    AND t.CompanyName != ''
    AND t.FYELastFinancialYear IS NOT NULL
    AND t.FYELastFinancialYear > 0
ORDER BY t.FYELastFinancialYear DESC;

GO
