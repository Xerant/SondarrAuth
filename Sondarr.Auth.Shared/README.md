# Sondarr.Auth.Shared

Shared authentication library for Sondarr microservices. Validates Supabase-issued JWTs and exposes user context via dependency injection.

## Install

```
dotnet add package Sondarr.Auth.Shared --source https://nuget.pkg.github.com/Xerant/index.json
```

## Setup

`appsettings.json`:

```json
"Supabase": {
  "JwtSecret": "<your-supabase-jwt-secret>",
  "Issuer": "https://<project-ref>.supabase.co/auth/v1",
  "Audience": "authenticated"
}
```

`Program.cs`:

```csharp
builder.Services.AddSupabaseAuthentication(builder.Configuration);
builder.Services.AddSondarrAuthServices();
// ...
app.UseAuthentication();
app.UseAuthorization();
```

## Usage

```csharp
[Authorize]
[RequireRole("admin")]
public class AdminController : ControllerBase
{
    private readonly IUserContextService _userContext;

    public AdminController(IUserContextService userContext) => _userContext = userContext;

    [HttpGet("me")]
    public IActionResult Me() => Ok(_userContext.GetCurrentUserRequired());
}
```

`UserContext.Roles` is populated from the `user_role` claim, which the Supabase project's Custom Access Token Hook injects from `public.profiles.role`. It is not the JWT's standard `role` claim (always `"authenticated"`, the Postgres role).

## Cross-site SSO (shared parent domain)

For sites under a shared parent domain (e.g. `*.sondarr.com`), mount the session endpoints so the site can establish (or clear) a cross-site cookie:

```csharp
app.MapSondarrSessionEndpoints(builder.Configuration);
```

This adds `POST /auth/session` (body: `{ "accessToken": "<supabase-jwt>" }`, sets an HttpOnly cookie scoped to `Supabase:Cookie:Domain`) and `POST /auth/logout` (clears it). `AddSupabaseAuthentication()` already reads that cookie as a fallback when a request has no `Authorization` header, so every other site under the same parent domain picks up the session automatically — no extra wiring needed beyond the usual setup. See `Examples/MicroserviceProgram.cs` for the full config shape and flow.

MVP scope: access-token cookie only, no refresh-token cookie or `/auth/refresh` endpoint yet — the session ends when the JWT expires (~1hr by Supabase default).
