# SondarrAuth — Current State

**Last updated:** 2026-07-21 | **Status:** 🟡 In Development
**Suggested vault location:** `02-backend/`

---

## Overall Readiness

| Component                         | Status         | Notes                          |
| --------------------------------- | -------------- | ------------------------------ |
| `Sondarr.Auth.Shared` (NuGet lib) | 🟢 Functional  | v2.0.0 — core complete, role claim wiring fixed, tested |
| Supabase role claim wiring        | 🟢 Enabled     | Custom Access Token Hook live; `user_role` claim populated from `public.profiles.role` |
| `Sondarr.Auth.Api` (utility API)  | 🟢 Functional  | validate-token implemented and tested |
| Cross-site cookie SSO             | 🟢 MVP done    | Shared parent domain (`.sondarr.com`), access-token cookie only, tested |
| NuGet publish pipeline            | 🔴 Not started | Package metadata ready; no CI/CD to push to GitHub Packages yet |
| Docker / container config         | 🔴 Not started | No Dockerfile or compose       |
| Test project                      | 🟢 Done        | `Sondarr.Auth.Shared.Tests` (45) + `Sondarr.Auth.Api.Tests` (6) — 51 xUnit tests, all passing |

---

## Sondarr.Auth.Shared — What Works

- [x] `AddSupabaseAuthentication(IConfiguration)` — configures JWT Bearer with HS256 validation, issuer, audience, expiry, zero clock skew
- [x] `AddSondarrAuthServices()` / `AddUserContextService()` — registers `IUserContextService` in DI
- [x] `UserContext.FromClaims()` — extracts all standard Supabase JWT claims into a typed model
- [x] `UserContext.Roles` — populated from the `user_role` claim (Supabase Custom Access Token Hook, sourced from `public.profiles.role`), not the standard `role` claim (which is always `"authenticated"`, the Postgres role)
- [x] `UserContext.CustomClaims` — `IDictionary<string, object>`; JSON-shaped claim values parse into `JsonElement` instead of collapsing to strings (breaking change, bumped to v2.0.0)
- [x] `UserContext.HasRole()` / `HasAnyRole()` / `IsTokenValid()` — utility methods
- [x] `IUserContextService` — full interface: `GetCurrentUser()`, `GetCurrentUserRequired()`, `GetCurrentUserId()`, `GetCurrentUserRoles()`, `HasRole()`, `HasAnyRole()`, `IsAuthenticated()`
- [x] `RequireRoleAttribute` — `[RequireRole("admin")]` action filter with 401/403 responses
- [x] `RequireAnyRoleAttribute` — `[RequireAnyRole("admin", "mod")]` action filter
- [x] XML documentation on all public APIs
- [x] Nullable reference types enabled
- [x] Example controller + Program.cs in `Examples/`
- [x] NuGet package metadata — `RepositoryUrl`, `PackageProjectUrl`, `PackageReadmeFile` + README.md (no license expression set — internal/private package, not open source)
- [x] `Sondarr.Auth.Shared.Tests` — xUnit coverage for `UserContext`, `UserContextService`, `RequireRoleAttribute`/`RequireAnyRoleAttribute`, DI extensions, cookie SSO
- [x] Cross-site cookie SSO (v2.1.0, MVP) — `SessionEndpointExtensions.MapSondarrSessionEndpoints()` adds `POST /auth/session` (validates a Supabase access token, sets an HttpOnly/Secure/SameSite=Lax cookie scoped to `Supabase:Cookie:Domain`) and `POST /auth/logout` (clears it). `AddSupabaseAuthentication()` reads that cookie as a fallback when a request has no `Authorization` header (header always takes precedence when both are present) — any site under the shared parent domain authenticates automatically once one site has set the cookie. Access-token cookie only; no refresh-token cookie or `/auth/refresh` endpoint yet, so sessions end when the JWT expires (~1hr Supabase default)

---

## Supabase — Auth Schema (project: SondarrFoundation, `xgztnswiiisfmblgrezi`)

- [x] Standard GoTrue `auth` schema (users, sessions, identities, MFA, SSO/SAML, native OAuth server tables)
- [x] `public.profiles.role` — app-level role column (`listener`/`artist`/etc., default `'listener'`)
- [x] `public.custom_access_token_hook(event jsonb)` — migration `add_custom_access_token_hook_for_role`; injects `profiles.role` into JWT as flat `user_role` claim. Grants scoped to `supabase_auth_admin` only (execute + RLS read policy on `profiles`)
- [x] Hook enabled in Supabase Dashboard → Auth → Hooks (confirmed 2026-07-21)
- [ ] Existing sessions won't retroactively have `user_role` — only tokens issued/refreshed after enabling carry the claim
- ⚠ `public.audit_logs` has RLS disabled — fully exposed to `anon`/`authenticated`. Flagged, not fixed. Remediation: `ALTER TABLE public.audit_logs ENABLE ROW LEVEL SECURITY;` plus policies (not yet designed)

---

## Sondarr.Auth.Api — What Works

- [x] `GET /api/auth/me` — returns current user (requires Bearer token)
- [x] `GET /api/auth/roles` — returns user's roles list
- [x] `GET /api/auth/has-role/{role}` — bool role check
- [x] `GET /api/auth/health` — unauthenticated health check
- [x] `GET /api/test/*` — 6 test endpoints covering public, protected, admin, moderator, custom role logic, token info
- [x] Swagger with Bearer auth scheme configured
- [x] CORS middleware registered
- [x] Health endpoint at `GET /health`
- [x] Duplicate `AddSupabaseAuthentication`/`AddSondarrAuthServices` registration in `Program.cs` — fixed
- [x] `POST /api/auth/validate-token` — implemented. Validates an arbitrary token string (not the caller's own bearer token) against the same Supabase JWT rules as the automatic pipeline, via a new shared `SupabaseAuthenticationExtensions.CreateTokenValidationParameters()` helper. Returns `200` with `IsValid: false` for expired/bad-signature/wrong-issuer/malformed tokens; `400` only for a missing token in the request; `500` for genuinely unexpected errors
- [x] `Sondarr.Auth.Api.Tests` — 6 xUnit tests covering validate-token: valid token, expired, wrong signing key, wrong issuer, malformed, missing token

---

## Known Issues

### Bugs / Code Problems

| Severity | Location | Issue |
|---|---|---|
| ~~Low~~ | ~~`Program.cs` lines ~21 and ~60~~ | ~~Duplicate DI registration~~ — fixed |
| ~~Low~~ | ~~`CustomClaims` in `UserContext`~~ | ~~Lost type info for non-string claims~~ — fixed in v2.0.0 |
| ~~Medium~~ | ~~`AuthController.ValidateToken`~~ | ~~`JwtSecurityTokenHandler` remaps short claim types (`sub` → a long XML-namespace URI, etc.) by default via `MapInboundClaims = true` — silently broke `UserContext.FromClaims()` for manually-validated tokens. Fixed by setting `MapInboundClaims = false`. Worth remembering for any future manual JWT validation code — the ASP.NET Core JwtBearer pipeline doesn't have this problem since it uses a different handler by default.~~ |

No open bugs currently tracked.

### Incomplete Implementations

| Priority | Endpoint / Feature | Notes |
|---|---|---|
| Medium | CORS policy | `AllowAnyOrigin()` in `Program.cs` — deliberately deferred until preprod/live testing is done, see below |
| Medium | Token refresh + `/auth/refresh` cookie endpoint | Cross-site cookie SSO MVP is access-token-only; sessions currently end when the JWT expires rather than silently refreshing |
| Medium | Token revocation | No blocklist or revocation check |
| Low | `public.audit_logs` RLS | Disabled, fully exposed — needs policies designed before enabling |

### Missing Infrastructure

- No `Dockerfile` for `Sondarr.Auth.Api`
- No `docker-compose.yml`
- No GitHub Actions workflow (NuGet publish to GitHub Packages, CI build/test) — package metadata is ready for this, pipeline itself not built
- No `dotnet user-secrets` documentation for local development

---

## Security Assessment

| Check | Status | Notes |
|---|---|---|
| JWT signature validation (HS256) | ✅ Pass | Uses `SymmetricSecurityKey` |
| Issuer validation | ✅ Pass | Configured from `Supabase:Issuer` |
| Audience validation | ✅ Pass | `"authenticated"` |
| Token expiry validation | ✅ Pass | `ValidateLifetime=true` |
| Zero clock skew | ✅ Pass | `ClockSkew=TimeSpan.Zero` |
| 401 vs 403 distinction | ✅ Pass | `RequireRoleAttribute` returns correct codes |
| App role claim integrity | ✅ Pass | `user_role` sourced server-side via Custom Access Token Hook (`supabase_auth_admin` only); not client-settable |
| CORS locked to known origins | ⚠ Fail | `AllowAnyOrigin()` currently |
| Secrets not committed | ✅ Pass | Verified — `appsettings*.json` contain only placeholder values |
| SQL Server connection string | ⚠ Review | In `appsettings.Production.json` — should be env var in container |
| Token revocation | ❌ Missing | No blocklist |
| `validate-token` endpoint auth | ⚠ By design | Intentionally unauthenticated (utility endpoint per architecture) — lets a caller check if a captured token is currently valid without needing another protected resource. Doesn't leak anything beyond what possessing the JWT already reveals (JWT payloads are base64, not encrypted), but worth being deliberate about if this API is ever exposed outside the internal network |
| `public.audit_logs` RLS | ❌ Fail | RLS disabled, exposed to `anon`/`authenticated` — not yet remediated |

---

## Next Steps (Priority Order)

- [ ] Lock down CORS to explicit origin list (config-driven, not hardcoded) — **deliberately deferred**: still testing in preprod/live environments, will lock down once those origins are known
- [ ] Add `/auth/refresh` + refresh-token cookie to make cross-site SSO sessions persistent past the access token's ~1hr expiry
- [ ] Add RLS policies and enable RLS on `public.audit_logs`
- [ ] Add `Dockerfile` for `Sondarr.Auth.Api`
- [ ] Set up GitHub Actions: build + push `Sondarr.Auth.Shared` to GitHub Packages on tag
- [ ] Move sensitive config to environment variables / `dotnet user-secrets`

---

## Related Notes

- [[02-backend/auth-overview]] — architecture and multi-site plan
- [[02-backend/auth-nuget-integration]] — how to consume the package
