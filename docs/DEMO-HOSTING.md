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
| `App__PublicWebBaseUrl` | `https://kadir-teal.vercel.app` (no trailing slash; invite / verify / reset email links) |
| `Brevo__ApiKey` | Brevo transactional API key (secret) |
| `Brevo__SenderEmail` | Verified sender address in the Brevo account |
| `Brevo__SenderName` | `Kadr` (optional; default Kadr) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

When `Brevo__ApiKey` and `Brevo__SenderEmail` are both set, the API sends real email via Brevo. If either is missing, it falls back to logging only (platform health email = degraded). Preferred names use `__` nesting. Legacy flat names also work: `BREVO_API_KEY`, `BREVO_SENDER_EMAIL`, `BREVO_SENDER_NAME`.

### Invite email not arriving (checklist)

1. On MonsterASP, set **all four**: `Brevo__ApiKey`, `Brevo__SenderEmail`, `Brevo__SenderName`, `App__PublicWebBaseUrl` (or the legacy flat names above). **Most common miss:** SenderEmail/Name set but **`Brevo__ApiKey` missing** → health stays `logging` and nothing is delivered.
2. `Brevo__SenderEmail` must be a **verified** sender (or domain) in the Brevo dashboard — otherwise Brevo rejects the send (API logs include Brevo status + truncated error body; no secrets).
3. **Restart / redeploy** the API after changing settings (DI chooses Brevo only at startup). Deploy must include the branch that has Brevo wiring (prefer `main` after merge).
4. Platform admin → Health: email provider must be **`brevo`**, not `logging` / `messages_logged_not_delivered`. If health shows `logging`, the host is missing ApiKey or SenderEmail — fix env and restart; toast **Open invite link** still works.
5. After Invite/Resend, the toast **Open invite link** is the fallback when mail is delayed or Brevo is misconfigured.
6. Check Brevo → Transactional logs for the recipient; also check spam.

**MonsterASP paste (names only — put real secrets in the panel, never in git):**

```text
App__PublicWebBaseUrl=https://kadir-teal.vercel.app
Brevo__ApiKey=<from Brevo>
Brevo__SenderEmail=<verified sender in Brevo>
Brevo__SenderName=Kadr
```

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
- Node.js Version: **22.x** (set in Project → Settings → Build and Deployment, or via `engines.node` in `web/package.json`)
- Production env: `API_BASE_URL` = `https://kadir.runasp.net` (no trailing slash; build script appends `/api/v1`)
- After setting or changing the variable: Deployments → … on latest → **Redeploy** with **Clear cache**

Build uses `npm run build` → `scripts/write-prod-env.mjs` writes `environment.prod.ts`. If `API_BASE_URL` is missing, the build **fails** (does not ship a relative `/api/v1` base).

Without a successful deploy that baked `API_BASE_URL`, the SPA calls `/api/v1/...` on the Vercel host (not MonsterASP) and industries show **تعذر تحميل القطاعات**.

## 3. Smoke checklist

1. API health OK over HTTPS (JSON, not IIS HTML)
2. Industries endpoint returns a list
3. From the Vercel site: onboarding industry step shows sector cards (not only Retry)
4. Stepper labels show Arabic/English (e.g. الحساب / القطاع), not raw keys like `onboarding.steps.account` — hard-refresh after a web deploy that includes the Transloco stepper fix
5. Register tenant → verify email (Brevo inbox, or Copy invite / verify link from toast when email falls back)
6. Invite user → email link `…/accept-invitation?token=…` or toast **Open invite link**
7. Login → refresh keeps session (cross-site cookie + CSRF header)
8. Optional: attendance punch needs geolocation permission (CSP allows `geolocation=(self)`)

## 4. CI

GitHub Actions `backend-ci.yml` runs restore/build/test on `backend/**` changes. API publish is MonsterASP Git Deploy, not Actions Web Deploy.
