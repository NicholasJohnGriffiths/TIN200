-- Migration 022: Add RevenueForecastMethod default to Config table

IF OBJECT_ID('dbo.Config', 'U') IS NULL
BEGIN
    RAISERROR('Table dbo.Config was not found.', 16, 1);
    RETURN;
END

IF COL_LENGTH('dbo.Config', 'RevenueForecastMethod') IS NULL
BEGIN
    ALTER TABLE dbo.Config
        ADD RevenueForecastMethod VARCHAR(32) NULL;
END

UPDATE dbo.Config
SET RevenueForecastMethod = 'LogLinear'
WHERE RevenueForecastMethod IS NULL;

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.Config')
      AND c.name = 'RevenueForecastMethod'
)
BEGIN
    ALTER TABLE dbo.Config
        ADD CONSTRAINT DF_Config_RevenueForecastMethod
        DEFAULT ('LogLinear') FOR RevenueForecastMethod;
END