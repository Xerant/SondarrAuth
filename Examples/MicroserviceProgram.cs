using Sondarr.Auth.Shared;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Your Microservice API", 
        Version = "v1",
        Description = "Your microservice using Sondarr.Auth.Shared for authentication."
    });
    
    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add Supabase JWT authentication
// This single line configures all JWT validation using Supabase settings
builder.Services.AddSupabaseAuthentication(builder.Configuration);

// Add Sondarr Auth services
// This adds the UserContextService and other authentication utilities
builder.Services.AddSondarrAuthServices();

// Add CORS for microservices communication
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMicroservices", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add your other services here
// builder.Services.AddScoped<IYourService, YourService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Your Microservice API v1");
    });
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowMicroservices");

// Add authentication and authorization middleware
// IMPORTANT: These must be in this order
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Add health check endpoint
app.MapGet("/health", () => new
{
    status = "healthy",
    service = "Your Microservice",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}).WithName("HealthCheck");

app.Run();

/*
 * Configuration in appsettings.json:
 * 
 * {
 *   "Supabase": {
 *     "JwtSecret": "your-supabase-jwt-secret-here",
 *     "Issuer": "https://your-project-ref.supabase.co/auth/v1",
 *     "Audience": "authenticated"
 *   }
 * }
 * 
 * Usage in controllers:
 * 
 * [ApiController]
 * [Route("api/[controller]")]
 * public class YourController : ControllerBase
 * {
 *     private readonly IUserContextService _userContextService;
 *     
 *     public YourController(IUserContextService userContextService)
 *     {
 *         _userContextService = userContextService;
 *     }
 *     
 *     [HttpGet("protected")]
 *     [Authorize]
 *     public IActionResult GetProtectedData()
 *     {
 *         var user = _userContextService.GetCurrentUserRequired();
 *         return Ok($"Hello {user.Email}!");
 *     }
 *     
 *     [HttpGet("admin-only")]
 *     [Authorize]
 *     [RequireRole("admin")]
 *     public IActionResult GetAdminData()
 *     {
 *         return Ok("Admin data");
 *     }
 * }
 */
