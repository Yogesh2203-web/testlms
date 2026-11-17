INSTITUTE LMS - USER ACCOUNT MANAGEMENT UPDATE

Included:
- Business Head-only user list, create, edit, activate/deactivate and password reset endpoints.
- Strong temporary passwords generated server-side for new Manager, Trainer and Student accounts.
- Temporary passwords are returned only once by create/reset operations; they are not stored in plaintext.
- New and reset accounts must change their password at first sign-in.
- Password change endpoint and forced password-change page.
- Five failed logins trigger a 15-minute lockout.
- Last-login tracking and account lockout status in the user list.
- Auth-version checks invalidate existing tokens after password reset, password change, or account status change.
- Manager account creation requires an active branch assignment.
- Business Head creation remains a separate one-time seed operation; Business Head accounts cannot be created through the ordinary user form.

LOCAL SETUP (run from backend/InstituteLMS.API):
1. Confirm appsettings.json points to your local SQL Server. This package is preconfigured for Server=DESKTOP-COHQC1K; Database=InstituteLMS_LocalDb using Windows Authentication. Change the Server value if your SQL Server host differs.
2. Set a private local setup key in the terminal: set AdminSetupKey=YOUR_LOCAL_SETUP_KEY
3. Restore: dotnet restore
4. If this project has no Migrations folder yet: dotnet ef migrations add InitialCreate
5. Apply: dotnet ef database update
6. Set Development mode and run: set ASPNETCORE_ENVIRONMENT=Development then dotnet run
7. Swagger is available at http://localhost:5000/swagger only in Development.

FRONTEND:
- Set frontend/public/lms-config.js API_BASE_URL to http://localhost:5000 for local use.
- Add/confirm the Angular wrapper route /dashboard/users for the user-management page.
- Sign in as BusinessHead and open http://localhost:4200/#/dashboard/users.

CREATE BUSINESS HEAD:
- Use the existing POST /api/auth/seed-admin endpoint once, with FullName, Email, Password (at least 12 characters), and SetupKey.
- The setup key must be configured as an environment variable. Do not publish it or commit it.
- Seeded Business Head accounts must also change their password at first sign-in.

SECURITY / DEPLOYMENT:
- appsettings.json contains a LOCAL-ONLY JWT signing key. Replace it with a new cryptographically random secret via the hosting provider's environment variables before deployment. Never reuse the local key.
- Set production ConnectionStrings__DefaultConnection, Jwt__Key, Jwt__Issuer, Jwt__Audience, and Cors__AllowedOrigins in the production host's secret/environment settings.
- Do not expose AdminSetupKey in frontend code. Remove/disable the seed-admin setup key after creating the initial Business Head account.
- Serve production over HTTPS and use a managed SQL Server database with backups.
- Temporary passwords should be delivered privately and must be changed immediately. The LMS does not email them automatically.

LIMITATIONS:
- This update implements login account administration and branch assignment using the models already present in this project. It does not yet implement trainer-to-course assignment, bulk import, email delivery, or a self-service email/OTP password-recovery workflow.
- Test the migration on a backup/new development database before applying to any database containing important records.
