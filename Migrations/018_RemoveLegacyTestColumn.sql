-- Remove legacy [Test] bit column now that TinStatus is canonical.

IF OBJECT_ID(N'dbo.TIN200', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TIN200', N'Test') IS NOT NULL
BEGIN
    ALTER TABLE dbo.TIN200 DROP COLUMN [Test];
END;

IF OBJECT_ID(N'dbo.Company', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Company', N'Test') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Company DROP COLUMN [Test];
END;
