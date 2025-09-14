# Sondarr.Auth

A centralized authentication service solution for microservices architecture using .NET 8, ASP.NET Core Web API, and Supabase JWT validation.

## Overview

Sondarr.Auth provides a complete authentication solution consisting of:

- **Sondarr.Auth.Shared**: A .NET Class Library packaged as a NuGet package for sharing JWT validation logic across microservices
- **Sondarr.Auth.Api**: A lightweight ASP.NET Core Web API service for centralized authentication operations

## Features

- ✅ Supabase JWT token validation
- ✅ User context extraction from JWT claims
- ✅ Role-based authorization attributes
- ✅ Microservice-ready architecture
- ✅ Comprehensive logging
- ✅ Swagger/OpenAPI documentation
- ✅ Health check endpoints
- ✅ CORS support for microservices communication

## Project Structure

```
Sondarr.Auth/
├── Sondarr.Auth.sln
├── Sondarr.Auth.Shared/
│   ├── Sondarr.Auth.Shared.csproj
│   ├── SupabaseAuthenticationExtensions.cs
│   ├── ServiceCollectionExtensions.cs
│   ├── Models/
│   │   └── UserContext.cs
│   ├── Services/
│   │   ├── IUserContextService.cs
│   │   └── UserContextService.cs
│   └── Attributes/
│       └── RequireRoleAttribute.cs
└── Sondarr.Auth.Api/
    ├── Sondarr.Auth.Api.csproj
    ├── Program.cs
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── appsettings.Production.json
    ├── Controllers/
    │   ├── AuthController.cs
    │   └── TestController.cs
    └── Models/
        ├── AuthResponse.cs
        └── ValidateTokenRequest.cs
```

## Quick Start

### 1. Configure Supabase Settings

Update the `appsettings.json` files with your Supabase configuration:

```json
{
  "Supabase": {
    "JwtSecret": "your-supabase-jwt-secret-here",
    "Issuer": "https://your-project-ref.supabase.co/auth/v1",
    "Audience": "authenticated"
  }
}
```

### 2. Run the API Service

```bash
cd Sondarr.Auth.Api
dotnet run
```

The API will be available at `https://localhost:7000` (or the configured port).

### 3. Access Swagger Documentation

Navigate to `https://localhost:7000` to access the Swagger UI for API documentation and testing.

## Usage in Other Microservices

### 1. Install the Shared Package

```bash
dotnet add package Sondarr.Auth.Shared
```

### 2. Configure Services

In your microservice's `Program.cs`:

```csharp
using Sondarr.Auth.Shared;

var builder = WebApplication.CreateBuilder(args);

// Add Supabase JWT authentication
builder.Services.AddSupabaseAuthentication(builder.Configuration);

// Add Sondarr Auth services
builder.Services.AddSondarrAuthServices();

// Add other services...

var app = builder.Build();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Configure your app...
```

### 3. Use in Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class MyController : ControllerBase
{
    private readonly IUserContextService _userContextService;

    public MyController(IUserContextService userContextService)
    {
        _userContextService = userContextService;
    }

    [HttpGet("protected")]
    [Authorize]
    public IActionResult GetProtectedData()
    {
        var user = _userContextService.GetCurrentUserRequired();
        return Ok($"Hello {user.Email}!");
    }

    [HttpGet("admin-only")]
    [Authorize]
    [RequireRole("admin")]
    public IActionResult GetAdminData()
    {
        return Ok("Admin data");
    }
}
```

## API Endpoints

### Authentication Controller (`/api/auth`)

- `GET /api/auth/me` - Get current user information
- `POST /api/auth/validate-token` - Validate a JWT token
- `GET /api/auth/roles` - Get current user's roles
- `GET /api/auth/has-role/{role}` - Check if user has specific role
- `GET /api/auth/health` - Health check

### Test Controller (`/api/test`)

- `GET /api/test/public` - Public endpoint (no auth required)
- `GET /api/test/protected` - Protected endpoint (auth required)
- `GET /api/test/admin` - Admin-only endpoint
- `GET /api/test/moderator` - Moderator or Admin endpoint
- `GET /api/test/custom-role-check` - Custom role checking
- `GET /api/test/token-info` - Token validation information

## Configuration

### Supabase Configuration

The service reads Supabase configuration from the `Supabase` section of `appsettings.json`:

```json
{
  "Supabase": {
    "JwtSecret": "your-jwt-secret",
    "Issuer": "https://your-project.supabase.co/auth/v1",
    "Audience": "authenticated"
  }
}
```

### Environment-Specific Settings

- `appsettings.Development.json` - Development environment settings
- `appsettings.Production.json` - Production environment settings

## Security Features

- JWT signature validation using Supabase secret
- Issuer and audience validation
- Token expiration validation
- Zero clock skew for enhanced security
- Role-based authorization attributes
- Comprehensive logging for security events

## Development

### Building the Solution

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Creating NuGet Package

The shared library is configured to generate a NuGet package on build:

```bash
cd Sondarr.Auth.Shared
dotnet pack
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For support and questions, please open an issue in the repository or contact the development team.
