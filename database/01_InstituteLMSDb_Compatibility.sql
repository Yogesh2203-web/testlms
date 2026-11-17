/*
  Institute LMS compatibility update for the existing InstituteLMSDb database.
  BACK UP InstituteLMSDb before running this script.
  This script preserves existing Users rows and PasswordHash values.
*/
USE [InstituteLMSDb];
GO
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    THROW 50001, 'dbo.Users was not found. Select the correct existing database before running this script.', 1;
GO

/* Existing Users table fields observed in the supplied SQL Server screenshot:
   UserId, FullName, Email, Phone, PasswordHash, Role, BranchId, IsActive, CreatedAt.
   Add only the authentication columns required by the current API model.
*/
IF COL_LENGTH('dbo.Users', 'MustChangePassword') IS NULL
    ALTER TABLE dbo.Users ADD MustChangePassword bit NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT (0) WITH VALUES;
IF COL_LENGTH('dbo.Users', 'FailedLoginCount') IS NULL
    ALTER TABLE dbo.Users ADD FailedLoginCount int NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT (0) WITH VALUES;
IF COL_LENGTH('dbo.Users', 'AuthVersion') IS NULL
    ALTER TABLE dbo.Users ADD AuthVersion int NOT NULL CONSTRAINT DF_Users_AuthVersion DEFAULT (1) WITH VALUES;
IF COL_LENGTH('dbo.Users', 'LockoutEndUtc') IS NULL
    ALTER TABLE dbo.Users ADD LockoutEndUtc datetime2 NULL;
IF COL_LENGTH('dbo.Users', 'LastLoginAt') IS NULL
    ALTER TABLE dbo.Users ADD LastLoginAt datetime2 NULL;
GO

/* Create operational tables only when absent. Existing tables/data are not overwritten. */
IF OBJECT_ID(N'dbo.Branches', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.Branches (
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Branches PRIMARY KEY,
  Name nvarchar(200) NOT NULL, Code nvarchar(50) NOT NULL, Address nvarchar(500) NOT NULL DEFAULT(''),
  Phone nvarchar(30) NOT NULL DEFAULT(''), IsActive bit NOT NULL DEFAULT(1));
 CREATE UNIQUE INDEX IX_Branches_Code ON dbo.Branches(Code);
END;
IF OBJECT_ID(N'dbo.Students', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.Students (
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Students PRIMARY KEY,
  AdmissionNo nvarchar(80) NOT NULL, FullName nvarchar(200) NOT NULL, Email nvarchar(256) NOT NULL DEFAULT(''),
  Phone nvarchar(30) NOT NULL DEFAULT(''), Course nvarchar(200) NOT NULL DEFAULT(''), BranchId int NULL,
  AdmissionDate datetime2 NOT NULL DEFAULT(SYSUTCDATETIME()), Status nvarchar(40) NOT NULL DEFAULT('Active'));
 CREATE UNIQUE INDEX IX_Students_AdmissionNo ON dbo.Students(AdmissionNo);
END;
IF OBJECT_ID(N'dbo.Trainers', N'U') IS NULL
 CREATE TABLE dbo.Trainers (
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Trainers PRIMARY KEY, FullName nvarchar(200) NOT NULL,
  Email nvarchar(256) NOT NULL DEFAULT(''), Phone nvarchar(30) NOT NULL DEFAULT(''), Specialization nvarchar(200) NOT NULL DEFAULT(''),
  BranchId int NULL, IsActive bit NOT NULL DEFAULT(1));
IF OBJECT_ID(N'dbo.Courses', N'U') IS NULL
 CREATE TABLE dbo.Courses (
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Courses PRIMARY KEY, Name nvarchar(200) NOT NULL, Code nvarchar(80) NOT NULL DEFAULT(''),
  Description nvarchar(max) NOT NULL DEFAULT(''), Fees decimal(12,2) NOT NULL DEFAULT(0), DurationMonths int NOT NULL DEFAULT(0),
  BranchId int NULL, IsActive bit NOT NULL DEFAULT(1));
IF OBJECT_ID(N'dbo.Admissions', N'U') IS NULL
 CREATE TABLE dbo.Admissions (
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Admissions PRIMARY KEY, ApplicantName nvarchar(200) NOT NULL,
  Phone nvarchar(30) NOT NULL DEFAULT(''), Email nvarchar(256) NOT NULL DEFAULT(''), Course nvarchar(200) NOT NULL DEFAULT(''),
  BranchId int NULL, Status nvarchar(40) NOT NULL DEFAULT('Pending'), CreatedAt datetime2 NOT NULL DEFAULT(SYSUTCDATETIME()));
IF OBJECT_ID(N'dbo.FeePayments', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.FeePayments (
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FeePayments PRIMARY KEY, StudentId int NOT NULL,
  Amount decimal(12,2) NOT NULL, PaymentMode nvarchar(40) NOT NULL DEFAULT('Cash'), ReceiptNo nvarchar(100) NOT NULL,
  PaidAt datetime2 NOT NULL DEFAULT(SYSUTCDATETIME()), Remarks nvarchar(1000) NOT NULL DEFAULT(''));
 CREATE UNIQUE INDEX IX_FeePayments_ReceiptNo ON dbo.FeePayments(ReceiptNo);
END;
IF OBJECT_ID(N'dbo.Attendance', N'U') IS NULL
 CREATE TABLE dbo.Attendance (
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Attendance PRIMARY KEY, StudentId int NOT NULL, TrainerId int NULL,
  Date date NOT NULL, Status nvarchar(40) NOT NULL DEFAULT('Present'), Remarks nvarchar(1000) NOT NULL DEFAULT(''));
IF OBJECT_ID(N'dbo.Assignments', N'U') IS NULL
 CREATE TABLE dbo.Assignments (
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Assignments PRIMARY KEY, Title nvarchar(200) NOT NULL,
  Description nvarchar(max) NOT NULL DEFAULT(''), CourseId int NOT NULL, DueDate datetime2 NOT NULL, TrainerId int NULL);
IF OBJECT_ID(N'dbo.Exams', N'U') IS NULL
 CREATE TABLE dbo.Exams (
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Exams PRIMARY KEY, Title nvarchar(200) NOT NULL,
  CourseId int NOT NULL, ExamDate datetime2 NOT NULL, TotalMarks decimal(8,2) NOT NULL);
GO

/* Check the Business Head record and ensure its original hash was not changed. */
SELECT UserId, FullName, Email, Phone, Role, BranchId, IsActive,
       MustChangePassword, FailedLoginCount, AuthVersion
FROM dbo.Users
ORDER BY UserId;
GO
