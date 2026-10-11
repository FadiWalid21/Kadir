# Demo hosting (MonsterASP + Vercel)

Public URLs:

| Role | URL |
|------|-----|
| API | `https://kadir.runasp.net` |
| Web | `https://kadir-teal.vercel.app` |

Deploy model: **MonsterASP Git Deploy** builds `backend/src/Kadr.Api/Kadr.Api.csproj` from GitHub. **Vercel** builds `web/` from GitHub. No secrets in the repo.

## 1. MonsterASP application settings

Set these in the MonsterASP control panel (environment variables / app settings). Values are secrets except origins and flags.

| Env var | Example / notes |
|---------|-----------------|
| `ConnectionStrings__Default` | Postgres connection string (SSL as required by host) |
| `Jwt__SigningKey` | Base64 64+ random bytes (same strength as local user-secrets) |
| `Jwt__Issuer` | `kadr` (optional if default) |
| `Jwt__Audience` | `kadr-api` (optional if default) |
| `Cors__Origins` | `https://kadir-teal.vercel.app` |
| `Auth__CrossSiteCookies` | `true` |
| `Database__MigrateOnStartup` | `true` |
| `Google__ClientId` | Google OAuth client ID (public) |
| `PlatformAdmin__Email` | Seed platform admin (optional) |
| `PlatformAdmin__Password` | Seed password (secret; optional) |
| `PlatformAdmin__FullName` | Seed display name (optional) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

Git Deploy project path:

```text
backend/src/Kadr.Api/Kadr.Api.csproj
```

Branch should track **`main`** (hosting + app code live there).

Local user-secrets do **not** apply on MonsterASP.

### API smoke (must pass before onboarding works)

1. `https://kadir.runasp.net/api/v1/health` → JSON `{ "status": "ok" }`  
   - If you see IIS HTML **404**, the ASP.NET app is not published/running. Check MonsterASP deploy logs, .NET runtime, and project path above; redeploy.
2. `https://kadir.runasp.net/api/v1/templates/industries` → JSON array of sectors (anonymous).  
   - Onboarding calls this for the industry step. Failure shows **تعذر تحميل القطاعات**.

## 2. Vercel

- Root Directory: `web`
- Production env: `API_BASE_URL` = `https://kadir.runasp.net` (no trailing slash; build script appends `/api/v1`)
- Redeploy after setting or changing the variable

Build uses `npm run build` → `scripts/write-prod-env.mjs` writes `environment.prod.ts`.

Without `API_BASE_URL`, the SPA calls `/api/v1/...` on the Vercel host (not MonsterASP) and industries will fail.

## 3. Smoke checklist

1. API health OK over HTTPS (JSON, not IIS HTML)
2. Industries endpoint returns a list
3. From the Vercel site: onboarding industry step shows sector cards (not only Retry)
4. Stepper labels show Arabic/English (e.g. الحساب / القطاع), not raw keys like `onboarding.steps.account` — hard-refresh after a web deploy that includes the Transloco stepper fix
5. Register tenant → verify email (check email provider / logs)
6. Login → refresh keeps session (cross-site cookie + CSRF header)
7. Optional: attendance punch needs geolocation permission (CSP allows `geolocation=(self)`)

## 4. CI

GitHub Actions `backend-ci.yml` runs restore/build/test on `backend/**` changes. API publish is MonsterASP Git Deploy, not Actions Web Deploy.
