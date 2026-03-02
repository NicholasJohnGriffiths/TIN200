# Power BI Integration Setup Guide

## Overview
This guide covers setting up the Power BI views and connecting Power BI Desktop to your SQL Server database for real-time financial analytics dashboards.

## Canonical Run Order (Database)

Run these scripts in this exact order against the target database:

1. `Migrations/003_UpdateTin200FieldNames.sql`
2. `Migrations/001_CreatePowerBIViews.sql`

## Database Views Created
4 optimized SQL Server views have been created for Power BI analytics:

1. **vw_CompanyFinancialAnalytics** - Comprehensive company metrics with growth calculations
2. **vw_FinancialYearComparison** - Normalized financial data across years for trending
3. **vw_RevenueSummaryBySize** - Aggregate metrics grouped by company revenue size
4. **vw_TopPerformersAnalytics** - Top 100 companies ranked by revenue with performance trends

## Step 1: Deploy the Database Views

### Option A: Using SQL Server Management Studio
1. Open SQL Server Management Studio
2. Connect to: `VivoTouchMar23\MSSQLSERVER01`
3. Select database: `TIN`
4. Open and execute: `Migrations/003_UpdateTin200FieldNames.sql`
5. Open and execute: `Migrations/001_CreatePowerBIViews.sql`
6. Verify views appear in Object Explorer → Views folder

### Option B: Using Entity Framework (Recommended for .NET Integration)
```powershell
# From your project directory in PowerShell
dotnet ef database update
```

## Step 2: Configure Power BI Desktop

### Prerequisites
- Download [Power BI Desktop](https://powerbi.microsoft.com/en-us/desktop/)
- Your SQL Server connection details:
  - **Server**: VivoTouchMar23\MSSQLSERVER01
  - **Database**: TIN
  - **Username**: tin200
   - **Password**: Use your secure SQL password (do not store it in source control)

### Connection Steps
1. **Open Power BI Desktop**
   - Launch Power BI Desktop application

2. **Connect to SQL Server**
   - Click "Get Data" → "SQL Server"
   - Server: `VivoTouchMar23\MSSQLSERVER01`
   - Database: `TIN`
   - Data Connectivity mode: **Import** (for performance)

3. **Select Your Views**
   - In the Navigator window, select these views:
     - ☑ vw_CompanyFinancialAnalytics
     - ☑ vw_FinancialYearComparison
     - ☑ vw_RevenueSummaryBySize
     - ☑ vw_TopPerformersAnalytics
   - Click "Load"

### Recommended Visualizations

#### Dashboard 1: Executive Financial Overview
- **Card**: Total 2025 Revenue (Sum of Revenue2025)
- **Card**: Average YoY Growth % (Average of GrowthPercent_2025vs2024)
- **Line Chart**: Revenue Trend by Company (Fiscal Year vs Revenue)
- **Column Chart**: Companies by Revenue Size (RevenueSize vs CompanyCount)
- **Clustered Bar Chart**: Top 10 Companies by Revenue

#### Dashboard 2: Growth Analytics
- **Scatter Chart**: Revenue vs Growth (Revenue2025 vs GrowthPercent_2025vs2024)
- **Table**: Company Performance (CompanyName, Revenue2025, GrowthPercent_2025vs2024, PerformanceTrend)
- **Gauge Chart**: Average Revenue Growth
- **Donut Chart**: Companies by Performance Trend

#### Dashboard 3: Company Size Analysis
- **Stacked Column Chart**: Revenue by Size Category (RevenueSize vs Total Revenue)
- **Table**: Size Category Summary (All metrics from vw_RevenueSummaryBySize)
- **KPI Cards**: Count of companies by size

#### Dashboard 4: Detailed Company Analytics
- **Table**: Full company details with all metrics
- **Slicers**: CompanyName, RevenueSize, FiscalYear
- **Cards**: Dynamic metrics based on selections

## Step 3: Configure Real-Time Dashboard Refresh

### For Power BI Desktop (Development)
1. File → Options and settings → Options
2. Data Load → Set background refresh frequency
3. Save your .pbix file

### For Power BI Service (Production - Requires Premium)
1. Publish your dashboard to Power BI Service
2. Settings → Dataset settings → Refresh
3. Set refresh schedule (Premium requires minimum 15-minute intervals)

### For Real-Time Updates (Streaming Dataset)
If you need true real-time updates:
1. Create a Power BI Streaming Dataset
2. Configure your ASP.NET app to push data to the streaming endpoint
3. Reference the included `PowerBIStreamingService.cs` (if implemented)

## Step 4: Integrate with Your ASP.NET App (Optional)

If you want to embed Power BI reports or programmatically query the views from your application:

### Add Service to Access Analytics Data
```csharp
// In your Tin200Service.cs, add:
public async Task<List<CompanyFinancialAnalytics>> GetCompanyFinancialAnalyticsAsync()
{
    return await _context.CompanyFinancialAnalytics
        .OrderByDescending(x => x.Revenue2025)
        .ToListAsync();
}

public async Task<List<TopPerformersAnalytics>> GetTopPerformersAsync()
{
    return await _context.TopPerformersAnalytics
        .ToListAsync();
}
```

### Create Analytics Page (Optional)
Create a new Razor Page to display analytics data alongside Power BI dashboards.

## Key Performance Indicators (KPIs) to Monitor

1. **Total Revenue**: Sum of all FYE 2025 revenues
2. **Average Growth Rate**: Percentage growth from 2024 to 2025
3. **Number of Growing Companies**: Count where GrowthPercent > 0
4. **Revenue by Size**: Distribution across company categories
5. **Top Performer**: Highest revenue company
6. **Trend Analysis**: Companies in Uptrend vs Downtrend

## View Column Reference

### vw_CompanyFinancialAnalytics
- `Revenue2025`, `Revenue2024`, `Revenue2023`: Annual financial figures
- `GrowthAmount_*`: Dollar amount growth
- `GrowthPercent_*`: Percentage growth
- `AverageRevenue_*Year`: 2-year and 3-year averages
- `RevenueTrend`: Uptrend, Downtrend, Recovery, Decline, Stable
- `RevenueSize`: Large, Medium, Small, Micro

### vw_FinancialYearComparison
- Normalized data with one row per year per company
- Ideal for line charts and trend analysis

### vw_RevenueSummaryBySize
- Pre-aggregated metrics by size category
- Count, totals, averages, min/max for each size

### vw_TopPerformersAnalytics
- Top 100 companies by revenue
- Rankings and trend classifications
- Pre-sorted for easy top company identification

## Troubleshooting

**Issue**: Views not appearing in Power BI Navigator
- Solution: Refresh connection, verify SQL permissions, check view creation in SSMS

**Issue**: Slow dashboard refresh
- Solution: Switch to Import mode instead of DirectQuery, enable compression in Power BI

**Issue**: Cannot connect to SQL Server
- Solution: Verify firewall settings, confirm server name, check credentials, ensure TIN database exists

**Issue**: Empty data in Power BI
- Solution: Verify data exists in TIN200 table, check view SQL syntax, ensure company names are populated

## Next Steps

1. ✅ Deploy views to database
2. ✅ Connect Power BI Desktop to SQL Server
3. ✅ Create initial dashboard with recommended visualizations
4. ✅ Test data refresh and drill-down capabilities
5. ☐ Publish to Power BI Service (requires Power BI Premium for auto-refresh)
6. ☐ Embed reports in ASP.NET dashboard (optional)

---
*Last updated: 2026-03-02*
