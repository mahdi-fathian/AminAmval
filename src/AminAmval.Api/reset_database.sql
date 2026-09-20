-- Reset database: delete all data except admin, custodian, departments, categories
-- Run this in SQL Server Object Explorer or SSMS against the AminAmvalFinalJadid database

-- 1. Delete from child tables first (due to foreign key constraints)
DELETE FROM [dbo].[Images];
DELETE FROM [dbo].[Sessions];
DELETE FROM [dbo].[DispositionRequests];
DELETE FROM [dbo].[Assignments];
DELETE FROM [dbo].[Assets];
DELETE FROM [dbo].[Audit] WHERE [EntityType] <> 'System';

-- 2. Delete all users except admin and custodian
DELETE FROM [dbo].[Users]
WHERE [Role] <> 'Admin' AND [Role] <> 'Custodian';

-- 3. Reset identity columns
DBCC CHECKIDENT ('[dbo].[Assets]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[Assignments]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[Audit]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[Users]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[Categories]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[Departments]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[DispositionRequests]', RESEED, 0);

-- Result: Only admin, custodian, departments, and categories remain
