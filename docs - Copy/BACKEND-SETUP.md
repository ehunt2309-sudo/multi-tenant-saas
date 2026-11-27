# Backend setup

## 1. Install the files
1. Extract the zip **over the repo root** (it overwrites `Platform.API.csproj`, `Program.cs`, appsettings, etc.).
2. From the repo root run `backend\cleanup-old-stubs.ps1` (PowerShell). It deletes the old empty scaffold files this backend replaces.
3. Open the solution in Visual Studio and build. If a NuGet version doesn't exist, bump it to the latest and tell me what broke.

## 2. Local secrets (never committed)
Run inside `backend/Platform.API/Platform.API`:

    dotnet user-secrets set "Jwt:SigningKey" "<64+ random characters>"
    dotnet user-secrets set "Bootstrap:AdminEmail" "you@yourdomain.com"
    dotnet user-secrets set "Bootstrap:AdminPassword" "<12+ char strong password>"

Generate a key in PowerShell: `[Convert]::ToBase64String((1..64 | % { Get-Random -Max 256 }) -as [byte[]])`

The local connection string is in `appsettings.Development.json` (SQL Express). Change it if needed.

## 3. Create the database
Install the EF tool once: `dotnet tool install --global dotnet-ef`, then from the project folder:

    dotnet ef migrations add InitialCreate
    dotnet ef database update

(In Development the app also applies migrations on startup. Commit the generated `Migrations` folder.)

## 4. First run
Run the API. `Bootstrap:*` creates the internal Platform tenant and your PlatformAdmin login (idempotent).

1. `POST /api/auth/login` `{ "email", "password" }` from a tool like Postman (no Origin header => platform admin login).
2. `POST /api/platform/tenants` with the bearer token to onboard a client:
   `{ name, slug, industry, primaryDomain: "localhost:5173", managerEmail, managerFirstName, managerLastName, managerPassword }`
3. A frontend served from `http://localhost:5173` is now recognised as that venue (Development allows http for registered hosts).
   The manager logs in via `/api/auth/login` from that origin.

## 5. How a frontend talks to the API
- Send `Authorization: Bearer <accessToken>` on protected calls. Access tokens last 15 minutes.
- When a call returns 401, call `/api/auth/refresh` with the refresh token. **Refresh tokens rotate**: always store the new one.
  Replaying an old one revokes the whole session on purpose.
- Browsers add `Origin` automatically, which tells the API which venue it is. Server-rendered frontends (Next.js server code)
  should send `X-Tenant-Host: <the venue's domain>` instead.
- Every error is RFC 7807 JSON (`title`, `detail`, `status`). Lists are paged: `?page=1&pageSize=25` (max 100).
- Keep tokens in memory (or the platform's secure store), not in `localStorage`, to limit XSS exposure.

## 6. Azure deployment (summary)
- **App Service** (Linux, .NET 10) with a **system-assigned managed identity**, HTTPS only, minimum TLS 1.2, scale out on CPU/requests.
- **Azure SQL Database**; **Key Vault** with the identity granted `Key Vault Secrets User`.
- Key Vault secrets: `ConnectionStrings--DefaultConnection`, `Jwt--SigningKey`, (temporarily) `Bootstrap--AdminEmail`, `Bootstrap--AdminPassword`.
- App settings: `KeyVault__Uri=https://<vault>.vault.azure.net/`, `ASPNETCORE_ENVIRONMENT=Production`, `APPLICATIONINSIGHTS_CONNECTION_STRING`,
  `Cors__AllowedOrigins__0=https://admin.yourplatform.com` (your own admin portal only).
- Health probes: `/health/live` (process up) and `/health/ready` (database reachable).
- Apply migrations from the pipeline, not on app start with many instances:
  `dotnet ef migrations script --idempotent -o migrate.sql` then run it against Azure SQL.
  For a very first single-instance deploy you may set `Database__MigrateOnStartup=true` once.
- **Remove the `Bootstrap--*` secrets** after your admin account exists.

## 7. Security model
| Concern | Control |
|---|---|
| Tenant isolation | EF global query filter on `TenantId` (fails closed), write guard blocks cross-tenant saves, JWT carries the tenant, mismatched venue origin => 403 |
| Authentication | JWT HS256 only (alg pinned), 15-min access tokens, rotating hashed refresh tokens with reuse detection |
| Passwords | PBKDF2-HMAC-SHA512, 210k iterations, 12+ chars, rehash-on-login, constant-time-ish unknown-user path |
| Brute force | 5 failures => 15 min lockout, 10 auth calls/min/IP, 300 req/min/IP overall |
| Authorization | Fallback policy = authenticated; each action has an explicit role policy; customers only reach their own rows |
| CORS | Only registered venue domains (+ your admin origins), exact-origin echo, no credentials |
| Transport/headers | HTTPS redirect + HSTS, nosniff, frame deny, no-store, no Server header, 1 MB body cap |
| Errors | RFC 7807, no stack traces or internals leaked |
| Secrets | Key Vault + managed identity; none in git |
| Privacy (POPIA) | Consent timestamp, erase endpoint anonymises personal data, PII never placed in tokens or logs |
| Audit | Logins, lockouts, token reuse, staff/tenant changes, manual loyalty changes recorded in `AuditLogs` |

## 8. Known limits / next steps
- Rate limits are per instance. Put Azure Front Door (WAF + global limits) in front when you scale out, and lock the app to its IPs.
- Tenant-host cache is per instance (5 min); a newly added domain can take up to 5 minutes on other instances.
- Not built yet: payments, email/SMS notifications, image uploads (Blob Storage), real-time availability slots, automated tests.
- Customers can book without a staff member (no clash check); add capacity rules per service if needed (e.g. restaurant tables).
