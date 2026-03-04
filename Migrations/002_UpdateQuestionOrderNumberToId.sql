BEGIN TRANSACTION;

UPDATE q
SET q.OrderNumber = q.Id
FROM dbo.Question AS q;

COMMIT TRANSACTION;

SELECT q.Id, q.OrderNumber
FROM dbo.Question AS q
ORDER BY q.OrderNumber, q.Id;
