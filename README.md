# Institute LMS — Angular 20 + ASP.NET Core 10 + SQL Server

This package brings the original Institute LMS interface into an Angular-hosted project and provides a new ASP.NET Core 10 / EF Core / SQL Server API foundation.

## What is included
- Original black-and-white UI preserved from your `lmstest.zip` in `frontend/public/original/` (public pages, admission screens, dashboards, shared CSS and JS).
- Angular 20 application host and route map for all supplied original pages.
- ASP.NET Core 10 API, SQL Server entities, EF Core context, JWT login, one-time Business Head bootstrap, and starter CRUD endpoints.
- Windows start scripts and setup instructions.

## Run on Windows
### 1. Backend
- Install .NET 10 SDK and SQL Server / SSMS.
- Edit `backend/InstituteLMS.API/appsettings.json` and set the correct SQL Server instance.
- Open PowerShell in `backend\InstituteLMS.API`.
- Run:
  `dotnet tool install --global dotnet-ef`
  `dotnet restore`
  `dotnet ef migrations add InitialCreate`
  `dotnet ef database update`
  `$env:AdminSetupKey = "a-long-one-time-setup-secret"`
  `dotnet run`
- Visit Swagger using the URL printed in the terminal.

### 2. Frontend
- Install Node.js 22 LTS.
- Open a second terminal in `frontend`.
- Run `npm install` and `npm start`.
- Open `http://localhost:4200`.

## Important interface note
The supplied original HTML pages are preserved as the visual source of truth and hosted inside the Angular route shell so their existing styling and layouts remain unchanged. This is a migration bridge: the original screens are not yet rewritten as native Angular templates, and their old same-origin Flask fetch calls must be connected to the new API before every form/action is live. The API is a runnable foundation; not every LMS workflow is finished or production-ready.

## Route map
`/#/`, `/#/login`, `/#/courses`, `/#/about`, `/#/career`, `/#/contact`, `/#/social-media`, `/#/admission/enquiry`, `/#/admission/student-admission`, `/#/admission/fee-receipt`, `/#/admission/admission-receipt`, `/#/admission/excel-import`, `/#/dashboard/business-head`, `/#/dashboard/manager`, `/#/dashboard/trainer`, `/#/dashboard/student`, and all supplied dashboard module routes.

## Security
Never deploy with the sample JWT key or an exposed bootstrap key. Use HTTPS, secrets management, backups, authorization tests, and a security review before real student data is entered.
"# lmstest" 
