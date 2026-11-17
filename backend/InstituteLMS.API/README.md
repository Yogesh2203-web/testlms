# Institute LMS API

- .NET 10 Web API
- EF Core 10 + SQL Server
- JWT authentication and role claims
- Business Head / Manager / Trainer / Student roles
- Tables: Users, Branches, Students, Trainers, Courses, Admissions, FeePayments, Attendance, Assignments, Exams

## Configure before first run
1. Edit `appsettings.json` and replace `YOUR_SQL_SERVER_INSTANCE` with the exact SQL Server instance shown in SSMS (examples only: `localhost`, `localhost\\SQLEXPRESS`, or `MACHINE\\INSTANCE`).
2. Replace the JWT key with a long random secret (32+ characters).
3. Set an admin bootstrap secret in your terminal as `AdminSetupKey` (environment variable `AdminSetupKey`).
4. Run `dotnet restore`, then `dotnet ef migrations add InitialCreate`, then `dotnet ef database update`, then `dotnet run`.
5. Open Swagger at the URL printed by `dotnet run`.

## Create first Business Head account
POST `/api/auth/seed-admin` with:
`{"fullName":"Institute Admin","email":"admin@example.com","password":"Use-A-Strong-Unique-Password","setupKey":"the-secret-you-set-in-AdminSetupKey"}`
Do not share the setup key or password. Remove `AdminSetupKey` from the environment after the first account is created.

## API
- `GET /api/health`
- `POST /api/auth/login`
- `GET /api/auth/me` (Bearer token)
- `POST /api/auth/seed-admin` (one-time bootstrap)
- `POST /api/admissions/enquiry` (public enquiry)
- Authenticated list/create routes: `/api/branches`, `/api/students`, `/api/trainers`, `/api/courses`, `/api/admissions`, `/api/fees`, `/api/attendance`, `/api/assignments`, `/api/exams`

This is a complete runnable foundation, not a production security audit. Before public deployment add audited branch-level authorization to every write/read endpoint, rate limiting, refresh-token rotation, password reset, audit logs, backups, file upload validation, and automated tests.
