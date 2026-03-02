-- Migration: Create Power BI Optimized Views for TIN200 Analytics
-- Created: 2026-02-13

-- =============================================================================
-- View 1: Company Financial Analytics
-- Aggregates financial data by company for dashboard visualization
-- =============================================================================
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
    t.FYE2025 AS Revenue2025,
    t.FYE2024 AS Revenue2024,
    t.FYE2023 AS Revenue2023,
    t.TIN200 AS TINNumber,
    CASE
        WHEN t.FYE2024 > 0
        THEN CAST((t.FYE2025 - t.FYE2024) AS DECIMAL(18,2))
        ELSE NULL
    END AS GrowthAmount_2025vs2024,
    CASE
        WHEN t.FYE2024 > 0
        THEN CAST(ROUND(((t.FYE2025 - t.FYE2024) / t.FYE2024) * 100, 2) AS DECIMAL(10,2))
        ELSE NULL
    END AS GrowthPercent_2025vs2024,
    CASE
        WHEN t.FYE2023 > 0
        THEN CAST((t.FYE2024 - t.FYE2023) AS DECIMAL(18,2))
        ELSE NULL
    END AS GrowthAmount_2024vs2023,
    CASE
        WHEN t.FYE2023 > 0
        THEN CAST(ROUND(((t.FYE2024 - t.FYE2023) / t.FYE2023) * 100, 2) AS DECIMAL(10,2))
        ELSE NULL
    END AS GrowthPercent_2024vs2023,
    CASE
        WHEN (t.FYE2025 + t.FYE2024) > 0
        THEN CAST((t.FYE2025 + t.FYE2024) / 2 AS DECIMAL(18,0))
        ELSE NULL
    END AS AverageRevenue_2Year,
    CASE
        WHEN (t.FYE2025 + t.FYE2024 + t.FYE2023) > 0
        THEN CAST((t.FYE2025 + t.FYE2024 + t.FYE2023) / 3 AS DECIMAL(18,0))
        ELSE NULL
    END AS AverageRevenue_3Year,
    CASE
        WHEN t.FYE2025 > t.FYE2024 AND t.FYE2024 > t.FYE2023 THEN 'Uptrend'
        WHEN t.FYE2025 < t.FYE2024 AND t.FYE2024 < t.FYE2023 THEN 'Downtrend'
        WHEN t.FYE2025 > t.FYE2024 AND t.FYE2024 < t.FYE2023 THEN 'Recovery'
        WHEN t.FYE2025 < t.FYE2024 AND t.FYE2024 > t.FYE2023 THEN 'Decline'
        ELSE 'Stable'
    END AS RevenueTrend,
    CASE
        WHEN t.FYE2025 >= 1000000 THEN 'Large'
        WHEN t.FYE2025 >= 500000 THEN 'Medium'
        WHEN t.FYE2025 >= 100000 THEN 'Small'
        ELSE 'Micro'
    END AS RevenueSize,
    GETDATE() AS LastRefreshTime
FROM TIN200 t
WHERE t.CompanyName IS NOT NULL AND t.CompanyName != '';

GO

-- =============================================================================
-- View 2: Financial Year Comparison
-- Normalizes data for easy comparison across years
-- =============================================================================
CREATE OR ALTER VIEW vw_FinancialYearComparison AS
SELECT
    CompanyName,
    CONCAT(CEOFirstName, ' ', CEOLastName) AS CeoFullName,
    2025 AS FiscalYear,
    FYE2025 AS Revenue,
    ExternalID,
    Email
FROM TIN200
WHERE FYE2025 IS NOT NULL

UNION ALL

SELECT
    CompanyName,
    CONCAT(CEOFirstName, ' ', CEOLastName) AS CeoFullName,
    2024 AS FiscalYear,
    FYE2024 AS Revenue,
    ExternalID,
    Email
FROM TIN200
WHERE FYE2024 IS NOT NULL

UNION ALL

SELECT
    CompanyName,
    CONCAT(CEOFirstName, ' ', CEOLastName) AS CeoFullName,
    2023 AS FiscalYear,
    FYE2023 AS Revenue,
    ExternalID,
    Email
FROM TIN200
WHERE FYE2023 IS NOT NULL;

GO

-- =============================================================================
-- View 3: Revenue Summary by Company Size
-- Aggregate metrics grouped by revenue size classification
-- =============================================================================
CREATE OR ALTER VIEW vw_RevenueSummaryBySize AS
SELECT
    CASE
        WHEN FYE2025 >= 1000000 THEN 'Large'
        WHEN FYE2025 >= 500000 THEN 'Medium'
        WHEN FYE2025 >= 100000 THEN 'Small'
        ELSE 'Micro'
    END AS RevenueSize,
    COUNT(*) AS CompanyCount,
    CAST(SUM(FYE2025) AS DECIMAL(18,0)) AS TotalRevenue2025,
    CAST(AVG(FYE2025) AS DECIMAL(18,0)) AS AverageRevenue2025,
    CAST(MIN(FYE2025) AS DECIMAL(18,0)) AS MinRevenue2025,
    CAST(MAX(FYE2025) AS DECIMAL(18,0)) AS MaxRevenue2025,
    CAST(SUM(FYE2024) AS DECIMAL(18,0)) AS TotalRevenue2024,
    CAST(AVG(FYE2024) AS DECIMAL(18,0)) AS AverageRevenue2024,
    CAST(SUM(FYE2023) AS DECIMAL(18,0)) AS TotalRevenue2023,
    CAST(AVG(FYE2023) AS DECIMAL(18,0)) AS AverageRevenue2023
FROM TIN200
WHERE CompanyName IS NOT NULL AND CompanyName != ''
GROUP BY
    CASE
        WHEN FYE2025 >= 1000000 THEN 'Large'
        WHEN FYE2025 >= 500000 THEN 'Medium'
        WHEN FYE2025 >= 100000 THEN 'Small'
        ELSE 'Micro'
    END;

GO

-- =============================================================================
-- View 4: Top Performers Dashboard
-- Identifies top companies by revenue and growth metrics
-- =============================================================================
CREATE OR ALTER VIEW vw_TopPerformersAnalytics AS
SELECT TOP 100
    ROW_NUMBER() OVER (ORDER BY t.FYE2025 DESC) AS RankByRevenue2025,
    t.CompanyName,
    CONCAT(t.CEOFirstName, ' ', t.CEOLastName) AS CeoFullName,
    t.FYE2025 AS Revenue2025,
    t.FYE2024 AS Revenue2024,
    t.FYE2023 AS Revenue2023,
    CASE
        WHEN t.FYE2024 > 0
        THEN CAST(ROUND(((t.FYE2025 - t.FYE2024) / t.FYE2024) * 100, 2) AS DECIMAL(10,2))
        ELSE NULL
    END AS YoYGrowth2025,
    CASE
        WHEN t.FYE2025 > t.FYE2024 AND t.FYE2024 > t.FYE2023 THEN 'Strong Uptrend'
        WHEN t.FYE2025 > t.FYE2024 THEN 'Growing'
        WHEN t.FYE2025 < t.FYE2024 THEN 'Declining'
        ELSE 'Stable'
    END AS PerformanceTrend,
    t.Email AS ContactEmail
FROM TIN200 t
WHERE t.CompanyName IS NOT NULL
    AND t.CompanyName != ''
    AND t.FYE2025 IS NOT NULL
    AND t.FYE2025 > 0
ORDER BY t.FYE2025 DESC;

GO
