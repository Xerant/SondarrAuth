# SondarrAuth — Claude Context

## What This Is

A cross-site authentication library for the Sondarr platform. The solution has two parts:

- **`Sondarr.Auth.Shared`** — Class library published as a NuGet package. Installed in each microservice/site to validate Supabase JWTs and expose user context via DI.
- **`Sondarr.Auth.Api`** — ASP.NET Core Web API that hosts auth utility endpoints (validate token, get roles, health check). Not an identity server — Supabase issues all tokens.

## Goal

4 separate websites under the same domain (e.g., `*.sondarr.com`), each in its own container and repo, sharing a persistent login session. Users authenticate once via Supabase; every site validates the same JWT using this shared library.

## Project Structure

```
SondarrAuth/
├── Sondarr.Auth.Shared/          # NuGet package — install this in every site
│   ├── SupabaseAuthenticationExtensions.cs   # AddSupabaseAuthentication() extension
│   ├── ServiceCollectionExtensions.cs        # AddSondarrAuthServices() extension
│   ├── Models/UserContext.cs                 # Hydrated user model from JWT claims
│   ├── Services/IUserContextService.cs       # DI interface
│   ├── Services/UserContextService.cs        # Implementation
│   └── Attributes/RequireRoleAttribute.cs    # [RequireRole] / [RequireAnyRole] filters
├── Sondarr.Auth.Api/             # Utility API (validate, /me, health)
│   ├── Controllers/AuthController.cs
│   ├── Controllers/TestController.cs
│   └── Models/                   # AuthResponse, ValidateTokenRequest DTOs
├── Examples/                     # Reference Program.cs + controller for consumers
└── CLAUDE.md
```

## Stack

- .NET 8.0
- ASP.NET Core JWT Bearer authentication
- Supabase (issues JWTs, PostgreSQL backend)
- NuGet packaging (`GeneratePackageOnBuild=true`)

## How to Add Auth to a New Service (30-second version)

1. Install `Sondarr.Auth.Shared` NuGet package
2. Add to `appsettings.json`:
   ```json
   "Supabase": {
     "JwtSecret": "<your-supabase-jwt-secret>",
     "Issuer": "https://<project-ref>.supabase.co/auth/v1",
     "Audience": "authenticated"
   }
   ```
3. In `Program.cs`:
   ```csharp
   builder.Services.AddSupabaseAuthentication(builder.Configuration);
   builder.Services.AddSondarrAuthServices();
   // ...
   app.UseAuthentication();
   app.UseAuthorization();
   ```
4. Use `[Authorize]`, `[RequireRole("admin")]`, or inject `IUserContextService`

## Key Files to Read First

- `Sondarr.Auth.Shared/SupabaseAuthenticationExtensions.cs` — JWT validation config
- `Sondarr.Auth.Shared/Models/UserContext.cs` — all available user properties
- `Sondarr.Auth.Shared/Services/IUserContextService.cs` — available DI methods
- `Examples/MicroserviceProgram.cs` + `Examples/MicroserviceExample.cs` — usage pattern
- `Sondarr.Auth.Api/Controllers/AuthController.cs` — auth API endpoints

## Known Issues / What's Incomplete

- `POST /api/auth/validate-token` — stub, returns 200 with "not implemented" message
- `CORS` in `Program.cs` uses `AllowAnyOrigin()` — must be locked down before production
- Duplicate service registration in `Program.cs` (lines ~21 and ~60) — harmless but redundant
- No Docker / container configuration files yet
- No NuGet publish pipeline (GitHub Actions or otherwise)
- No test project
- No refresh token handling; no token revocation

## Critical Architecture Note (Multi-Site SSO)

The shared library validates JWTs but does **not** handle cookie sharing across subdomains. For true SSO across `site1.sondarr.com`, `site2.sondarr.com`, etc., the frontend must:
1. Store the Supabase JWT in an `HttpOnly` cookie scoped to `.sondarr.com` (not `localStorage`)
2. Each service then reads the cookie and the shared library validates it

This is a frontend/infra concern — not yet addressed anywhere in this repo.

## Configuration Files

- `appsettings.json` — base config with placeholder secrets
- `appsettings.Development.json` — extended logging, local DB
- `appsettings.Production.json` — restricted logging, production URLs
- Secrets should use `dotnet user-secrets` in development, not be committed
