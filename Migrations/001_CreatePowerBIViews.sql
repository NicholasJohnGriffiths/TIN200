-- Migration: Create Power BI Optimized Views for TIN200 Analytics
-- Created: 2026-02-13

-- =============================================================================
-- View 1: Company Financial Analytics
-- Aggregates financial data by company for dashboard visualization
-- =============================================================================
CREATE OR ALTER VIEW vw_CompanyFinancialAnalytics AS
SELECT 
    t.Id,
    t.[Company Name],
    t.[Company Description],
    CONCAT(t.[CEO First Name ], ' ', t.[CEO Last Name ]) AS CeoFullName,
    t.[CEO First Name ] AS CeoFirstName,
    t.[CEO Last Name ] AS CeoLastName,
    t.[Email ] AS ContactEmail,
    t.[External ID] AS ExternalId,
    t.[FYE 2025] AS Revenue2025,
    t.[FYE 2024] AS Revenue2024,
    t.[FYE 2023] AS Revenue2023,
    t.[TIN200] AS TINNumber,
    -- Year-over-Year Growth Calculations
    CASE 
        WHEN t.[FYE 2024] > 0 
        THEN CAST((t.[FYE 2025] - t.[FYE 2024]) AS DECIMAL(18,2))
        ELSE NULL 
    END AS GrowthAmount_2025vs2024,
    CASE 
        WHEN t.[FYE 2024] > 0 
        THEN CAST(ROUND(((t.[FYE 2025] - t.[FYE 2024]) / t.[FYE 2024]) * 100, 2) AS DECIMAL(10,2))
        ELSE NULL 
    END AS GrowthPercent_2025vs2024,
    CASE 
        WHEN t.[FYE 2023] > 0 
        THEN CAST((t.[FYE 2024] - t.[FYE 2023]) AS DECIMAL(18,2))
        ELSE NULL 
    END AS GrowthAmount_2024vs2023,
    CASE 
        WHEN t.[FYE 2023] > 0 
        THEN CAST(ROUND(((t.[FYE 2024] - t.[FYE 2023]) / t.[FYE 2023]) * 100, 2) AS DECIMAL(10,2))
        ELSE NULL 
    END AS GrowthPercent_2024vs2023,
    -- Two-Year Average Revenue
    CASE 
        WHEN (t.[FYE 2025] + t.[FYE 2024]) > 0 
        THEN CAST((t.[FYE 2025] + t.[FYE 2024]) / 2 AS DECIMAL(18,0))
        ELSE NULL 
    END AS AverageRevenue_2Year,
    -- Three-Year Average Revenue
    CASE 
        WHEN (t.[FYE 2025] + t.[FYE 2024] + t.[FYE 2023]) > 0 
        THEN CAST((t.[FYE 2025] + t.[FYE 2024] + t.[FYE 2023]) / 3 AS DECIMAL(18,0))
        ELSE NULL 
    END AS AverageRevenue_3Year,
    -- Revenue Trend Classification
    CASE 
        WHEN t.[FYE 2025] > t.[FYE 2024] AND t.[FYE 2024] > t.[FYE 2023] THEN 'Uptrend'
        WHEN t.[FYE 2025] < t.[FYE 2024] AND t.[FYE 2024] < t.[FYE 2023] THEN 'Downtrend'
        WHEN t.[FYE 2025] > t.[FYE 2024] AND t.[FYE 2024] < t.[FYE 2023] THEN 'Recovery'
        WHEN t.[FYE 2025] < t.[FYE 2024] AND t.[FYE 2024] > t.[FYE 2023] THEN 'Decline'
        ELSE 'Stable'
    END AS RevenueTrend,
    -- Revenue Size Classification
    CASE 
        WHEN t.[FYE 2025] >= 1000000 THEN 'Large'
        WHEN t.[FYE 2025] >= 500000 THEN 'Medium'
        WHEN t.[FYE 2025] >= 100000 THEN 'Small'
        ELSE 'Micro'
    END AS RevenueSize,
    GETDATE() AS LastRefreshTime
FROM TIN200 t
WHERE t.[Company Name] IS NOT NULL AND t.[Company Name] != '';

GO

-- =============================================================================
-- View 2: Financial Year Comparison
-- Normalizes data for easy comparison across years
-- =============================================================================
CREATE OR ALTER VIEW vw_FinancialYearComparison AS
SELECT 
    [Company Name],
    CONCAT([Ceo First Name], ' ', [Ceo Last Name]) AS [Ceo Full Name],
    2025 AS FiscalYear,
    [FYE 2025] AS Revenue,
    [External Id],
    Email
FROM TIN200
WHERE [FYE 2025] IS NOT NULL

UNION ALL

SELECT 
    [Company Name],
    CONCAT([Ceo First Name], ' ', [Ceo Last Name]) AS [Ceo Full Name],
    2024 AS FiscalYear,
    [FYE 2024] AS Revenue,
    [External Id],
    Email
FROM TIN200
WHERE [FYE 2024] IS NOT NULL

UNION ALL

SELECT 
    [Company Name],
    CONCAT([Ceo First Name], ' ', [Ceo Last Name]) AS CeoFullName,
    2023 AS FiscalYear,
    [FYE 2023] AS Revenue,
    [External Id],
    Email
FROM TIN200
WHERE [FYE 2023] IS NOT NULL;

GO

-- =============================================================================
-- View 3: Revenue Summary by Company Size
-- Aggregate metrics grouped by revenue size classification
-- =============================================================================
CREATE OR ALTER VIEW vw_RevenueSummaryBySize AS
SELECT 
    CASE 
        WHEN [FYE 2025] >= 1000000 THEN 'Large'
        WHEN [FYE 2025] >= 500000 THEN 'Medium'
        WHEN [FYE 2025] >= 100000 THEN 'Small'
        ELSE 'Micro'
    END AS RevenueSize,
    COUNT(*) AS CompanyCount,
    CAST(SUM([FYE 2025]) AS DECIMAL(18,0)) AS TotalRevenue2025,
    CAST(AVG([FYE 2025]) AS DECIMAL(18,0)) AS AverageRevenue2025,
    CAST(MIN([FYE 2025]) AS DECIMAL(18,0)) AS MinRevenue2025,
    CAST(MAX([FYE 2025]) AS DECIMAL(18,0)) AS MaxRevenue2025,
    CAST(SUM([FYE 2024]) AS DECIMAL(18,0)) AS TotalRevenue2024,
    CAST(AVG([FYE 2024]) AS DECIMAL(18,0)) AS AverageRevenue2024,
    CAST(SUM([FYE 2023]) AS DECIMAL(18,0)) AS TotalRevenue2023,
    CAST(AVG([FYE 2023]) AS DECIMAL(18,0)) AS AverageRevenue2023
FROM TIN200
WHERE [Company Name] IS NOT NULL AND [Company Name] != ''
GROUP BY 
    CASE 
        WHEN [FYE 2025] >= 1000000 THEN 'Large'
        WHEN [FYE 2025] >= 500000 THEN 'Medium'
        WHEN [FYE 2025] >= 100000 THEN 'Small'
        ELSE 'Micro'
    END;

GO

-- =============================================================================
-- View 4: Top Performers Dashboard
-- Identifies top companies by revenue and growth metrics
-- =============================================================================
CREATE OR ALTER VIEW vw_TopPerformersAnalytics AS
SELECT TOP 100
    ROW_NUMBER() OVER (ORDER BY t.[FYE 2025] DESC) AS RankByRevenue2025,
    t.[Company Name],
    CONCAT(t.[CEO First Name ], ' ', t.[CEO Last Name ]) AS CeoFullName,
    t.[FYE 2025] AS Revenue2025,
    t.[FYE 2024] AS Revenue2024,
    t.[FYE 2023] AS Revenue2023,
    CASE 
        WHEN t.[FYE 2024] > 0 
        THEN CAST(ROUND(((t.[FYE 2025] - t.[FYE 2024]) / t.[FYE 2024]) * 100, 2) AS DECIMAL(10,2))
        ELSE NULL 
    END AS YoYGrowth2025,
    CASE 
        WHEN t.[FYE 2025] > t.[FYE 2024] AND t.[FYE 2024] > t.[FYE 2023] THEN 'Strong Uptrend'
        WHEN t.[FYE 2025] > t.[FYE 2024] THEN 'Growing'
        WHEN t.[FYE 2025] < t.[FYE 2024] THEN 'Declining'
        ELSE 'Stable'
    END AS PerformanceTrend,
    t.[Email ] AS ContactEmail
FROM TIN200 t
WHERE t.[Company Name] IS NOT NULL 
    AND t.[Company Name] != ''
    AND t.[FYE 2025] IS NOT NULL
    AND t.[FYE 2025] > 0
ORDER BY t.[FYE 2025] DESC;

GO
