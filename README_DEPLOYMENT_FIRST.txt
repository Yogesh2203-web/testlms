INSTITUTE LMS - START HERE

Read DEPLOY_NOW.md before deploying.

Architecture: Vercel frontend + separate ASP.NET Core 10 API container + managed SQL Server.
Final database name: InstituteLMSDb.

Do not expose a local desktop SQL Server to the internet. Do not publish until the database is backed up, the remote schema is reviewed, API secrets/CORS are configured, and all four roles pass the acceptance checks.
