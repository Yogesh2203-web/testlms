# Institute LMS deployment guide (Vercel frontend + separate API + SQL Server)

## Architecture
- Vercel hosts the Angular-built static frontend and the original HTML/CSS/JS screens under `/original/`.
- ASP.NET Core 10 API must be deployed separately as a Docker web service (for example Azure App Service/container hosting or Render Docker).
- SQL Server must be hosted separately (Azure SQL Database or another internet-reachable SQL Server). Vercel does not host SQL Server or run this ASP.NET Core API as a normal Vercel deployment.

## 1. Deploy the API first
1. Create a remote SQL Server database. Do not expose a local `localhost` SQL Server to the internet.
2. Deploy `backend/InstituteLMS.API` using its Dockerfile.
3. Set these environment variables in the API host (never commit real values):
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ASPNETCORE_URLS=http://+:8080` (Dockerfile already sets it)
   - `ConnectionStrings__DefaultConnection=Server=tcp:YOUR_SERVER,1433;Initial Catalog=InstituteLMSDb;Persist Security Info=False;User ID=YOUR_USER;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`
   - `Jwt__Issuer=InstituteLMS`
   - `Jwt__Audience=InstituteLMSUsers`
   - `Jwt__Key=GENERATE_A_RANDOM_SECRET_OF_32_BYTES_OR_MORE`
   - `AdminSetupKey=GENERATE_A_SEPARATE_ONE_TIME_RANDOM_SECRET`
   - `Cors__AllowedOrigins__0=https://YOUR-VERCEL-DOMAIN.vercel.app`
4. Run EF migrations against the remote DB from a trusted deployment environment: `dotnet tool install --global dotnet-ef` then `dotnet ef database update` in the API project. The uploaded project does not contain migrations for the `AppDbContext` model in `backend/`; generate and review an initial migration against the intended schema before production. Do not point it at your existing local DB without a backup and schema review.
5. Verify `https://YOUR-API-DOMAIN/api/health` returns `{"status":"ok"...}`.

## 2. Configure and deploy Vercel frontend
1. Set `frontend/public/lms-config.js` to:
   `window.LMS_CONFIG = { API_BASE_URL: "https://YOUR-API-DOMAIN" };`
   Keep no trailing slash. This value is public and must not contain secrets.
2. Push the repository to GitHub.
3. In Vercel, import the repository and keep **Root Directory** as the repository root.
4. Vercel uses `vercel.json`: build command `cd frontend && npm install && npm run build`; output directory `frontend/dist/institute-lms-ui/browser`.
5. Deploy, then verify the site opens and that `/original/` assets load. Test login and API calls using the deployed HTTPS API.

## 3. First Business Head
Use the one-time bootstrap endpoint only after remote database migrations and API configuration are complete:
`POST https://YOUR-API-DOMAIN/api/auth/seed-admin`
JSON body: `{ "fullName":"Institute Admin", "email":"admin@example.com", "password":"Use-A-Unique-Long-Password", "setupKey":"YOUR_ONE_TIME_SETUP_KEY" }`
The endpoint refuses to create a second Business Head. Remove `AdminSetupKey` from the API environment after setup (or rotate it immediately).

## 4. Local build checks
Frontend: `cd frontend && npm install && npm run build`
Backend: `cd backend/InstituteLMS.API && dotnet restore && dotnet build`
This package was not built with the .NET SDK in the preparation environment. Run both checks on your machine or CI before production.

## Important functional and security status
- Existing original dashboards are preserved. The Manager dashboard and Business Head user-management page are connected to the API paths included in this package.
- This is a deployment scaffold and functional foundation, not a certification that every legacy LMS form, payment workflow, import/export, notification, or all edit/delete flows are production complete. The original project README documents that some screens are still presentation-only.
- The API context uses the existing `AppDbContext` model. Create and review migrations before deploying to a fresh remote SQL Server.
- Perform authorization tests for each role, backup/restore tests, rate limiting, password reset, audit logging, HTTPS, privacy review, and a security review before storing real student data.
- Never commit connection strings, passwords, JWT keys, setup keys, or student personal data.
