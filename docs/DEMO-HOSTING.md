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

After deploy, open: `https://kadir.runasp.net/api/v1/health` → `{ "status": "ok" }`.

Local user-secrets do **not** apply on MonsterASP.

## 2. Vercel

- Root Directory: `web`
- Production env: `API_BASE_URL` = `https://kadir.runasp.net` (no trailing slash; build script appends `/api/v1`)
- Redeploy after setting the variable

Build uses `npm run build` → `scripts/write-prod-env.mjs` writes `environment.prod.ts`.

## 3. Smoke checklist

1. API health OK over HTTPS
2. From the Vercel site: register tenant → verify email (check email provider / logs)
3. Login → refresh keeps session (cross-site cookie + CSRF header)
4. Optional: attendance punch needs geolocation permission (CSP allows `geolocation=(self)`)

## 4. CI

GitHub Actions `backend-ci.yml` runs restore/build/test on `backend/**` changes. API publish is MonsterASP Git Deploy, not Actions Web Deploy.
