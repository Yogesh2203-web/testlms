# Institute LMS - Deployment Runbook

This repository is configured for a Vercel static frontend + a separately hosted ASP.NET Core 10 API + a remotely hosted SQL Server database. It is not possible to deploy the API or SQL Server to Vercel as part of the static frontend deployment.

## Important status

- Frontend build config reads `LMS_API_BASE_URL` during build. Vercel must have this environment variable set to the public HTTPS API origin.
- API reads its database, JWT and CORS configuration from host environment variables in Production.
- The existing local database `InstituteLMSDb` is not remotely accessible from Vercel or a cloud API. Create/restore a protected remote SQL Server database and apply `database/01_InstituteLMSDb_Compatibility.sql` to that remote database before switching the API.
- The compatibility SQL is intended to preserve existing `dbo.Users` rows and password hashes. Back up first and inspect any existing operational tables before applying it.
- The API is not certified for production student data until all functional/security checks at the end of this document pass.

## A. Prepare SQL Server

1. Provision Azure SQL Database or another managed SQL Server reachable by your API host. Do not expose your desktop SQL Server to the internet.
2. Create database `InstituteLMSDb` (or update the SQL script and connection string consistently if using another database name).
3. Restore/import the required existing LMS data using a secure migration process. Preserve the Business Head `UserId`, role and `PasswordHash`.
4. Open `database/01_InstituteLMSDb_Compatibility.sql` in SSMS connected to the remote database and review the script before execution. It begins with `USE [InstituteLMSDb]`, checks `dbo.Users`, adds missing authentication columns and creates missing LMS tables only when absent.
5. Verify `dbo.Users` contains `UserId`, `FullName`, `Email`, `Phone`, `PasswordHash`, `Role`, `BranchId`, `IsActive`, `CreatedAt`, `MustChangePassword`, `FailedLoginCount`, `AuthVersion`, `LockoutEndUtc`, `LastLoginAt`.
6. Verify any pre-existing `Branches`, `Students`, `Trainers`, `Courses`, `Admissions`, `FeePayments`, `Attendance`, `Assignments`, `Exams` tables have the columns expected by `Models.cs`. The compatibility script does not rewrite existing versions of these tables.

## B. Deploy API container

Deploy `backend/InstituteLMS.API` using its Dockerfile to a container-capable host (Azure App Service for Containers, Render Docker, etc.). Configure these environment variables in the host secret manager:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__DefaultConnection=Server=tcp:YOUR_SQL_HOST,1433;Initial Catalog=InstituteLMSDb;User ID=YOUR_APP_LOGIN;Password=YOUR_SECRET;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
Jwt__Issuer=InstituteLMS
Jwt__Audience=InstituteLMSUsers
Jwt__Key=REPLACE_WITH_A_RANDOM_SECRET_AT_LEAST_32_BYTES
Cors__AllowedOrigins__0=https://YOUR-VERCEL-PROJECT.vercel.app
AdminSetupKey=ONE_TIME_RANDOM_SETUP_SECRET
```

Use a least-privilege SQL login. The API needs the permissions required by the schema and LMS operations; do not use the SQL Server `sa` account. Use the host's HTTPS URL in the frontend. The Dockerfile listens on port 8080; configure the platform's external HTTPS ingress to forward to it.

Generate a JWT secret locally with PowerShell (do not paste it into chat or commit it):

```powershell
$bytes = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
[Convert]::ToBase64String($bytes)
```

After deployment, test `https://YOUR-API-DOMAIN/api/health`. It should return JSON with `status` equal to `ok`.

### One-time Business Head bootstrap

If the remote database contains the existing Business Head row, do not create another one. If it does not, call `POST /api/auth/seed-admin` once with JSON fields `fullName`, `email`, `phone`, `password`, `setupKey`. Use a unique password of at least 12 characters and the exact `AdminSetupKey` environment value. The endpoint refuses to create a second Business Head. Immediately remove `AdminSetupKey` from the host after successful bootstrap and restart the API. Never publish this key.

The local-only `/api/auth/dev-reset-business-head` route is registered only in Development and is not available in Production.

## C. Deploy frontend to Vercel

1. Push the project root (the folder containing `vercel.json`) to a private GitHub repository.
2. Import that repository into Vercel. Set Root Directory to the repository root, not `frontend`.
3. In Vercel Project Settings -> Environment Variables, add:

```text
LMS_API_BASE_URL=https://YOUR-API-DOMAIN
```

Use the exact API origin only, no trailing slash and no `/api` suffix. Add it to Production and Preview only when the API CORS allowlist also includes the corresponding preview origin. Do not add secrets to the frontend environment.
4. Keep the root `vercel.json` build configuration. It installs dependencies under `frontend`, builds Angular, and publishes `frontend/dist/institute-lms-ui/browser`.
5. Deploy. Confirm `/`, `/#/login`, and `/original/assets/js/auth.js` load. Confirm `lms-config.js` contains only the public HTTPS API origin.

## D. Local Windows test

Backend - PowerShell window 1:

```powershell
cd "<EXTRACTED_FOLDER>\backend\InstituteLMS.API"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet restore
dotnet run --urls http://localhost:5000
```

The development settings file supplies the local `InstituteLMSDb` connection and a development-only JWT key. Health check: `http://localhost:5000/api/health`.

Frontend - PowerShell window 2:

```powershell
cd "<EXTRACTED_FOLDER>\frontend"
npm install
npm start
```

Open `http://localhost:4200/#/login`. The local `frontend/public/lms-config.js` points to `http://localhost:5000`. Do not use that local config for the deployed site; Vercel build overwrites it from `LMS_API_BASE_URL`.

## E. Mandatory acceptance checks before real users

- Test Business Head login and logout; verify role redirects to Business Head dashboard.
- Test Manager with a valid `BranchId`; verify manager cannot access another branch's records or Business Head pages.
- Test Trainer and Student access; verify role mismatch redirects and API returns 401/403 where appropriate.
- Test inactive account, wrong password, temporary lockout and password change flow.
- Test create/update/deactivate/reset-user-password workflows and confirm temporary passwords are shown only once.
- Test every dashboard data view and every create/edit/delete form against the deployed API. Some legacy screens may still be presentation-only or not fully wired.
- Test SQL backup and restore. Configure automated backups, retention and monitoring.
- Configure rate limiting/WAF and abuse protection for public admission enquiry before accepting public traffic.
- Confirm all domains use HTTPS; CORS lists only exact trusted HTTPS frontend origins.
- Confirm no secrets, `.env` files, SQL backups, real student data or local connection strings are committed to Git.
- Perform a security/privacy review before storing real student information.

## Rollback

Keep a verified database backup and the previous successful frontend/API deployment. If schema changes fail, stop the API, restore the backup, and roll back the API image. Never delete or recreate the live database as a troubleshooting shortcut.
