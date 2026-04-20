using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SRN.Application.Interfaces;
using SRN.Domain.Entities;
using SRN.Domain.Interfaces;
using SRN.Infrastructure.Blockchain;
using SRN.Infrastructure.Persistence;
using SRN.Infrastructure.Repositories;
using System.Text;
using SRN.Application.Validators;

// Initialize a bootstrap logger to catch and log any errors that occur during application startup
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("--> Starting Web Host...");

    var builder = WebApplication.CreateBuilder(args);

    // Replace the default ASP.NET Core logger with Serilog for structured, highly configurable logging
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // Configure the Entity Framework Core DbContext to use PostgreSQL
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Register Core Infrastructure and Application Services into the Dependency Injection (DI) container
    // Scoped lifetime means a new instance is created per HTTP request
    builder.Services.AddScoped<IBlockchainService, EthereumBlockchainService>();
    Log.Information("--> Using REAL Ethereum Blockchain Service (Sepolia Testnet)");

    builder.Services.AddScoped<IArtifactRepository, ArtifactRepository>();
    builder.Services.AddScoped<IArtifactService, SRN.Application.Services.ArtifactService>();
    builder.Services.AddScoped<INotificationService, SRN.Infrastructure.Services.SignalRNotificationService>();
    builder.Services.AddScoped<IFileStorageService, SRN.Infrastructure.Services.LocalFileStorageService>();

    // Configure ASP.NET Core Identity for user and role management
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // Load JWT configuration settings from appsettings.json
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["Key"] ?? "DefaultSecretKey_MustBeLongerThan32Characters_ForSafety";
    var key = Encoding.ASCII.GetBytes(secretKey);

    // Configure Authentication and define JWT Bearer as the default scheme
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Define the parameters used to validate incoming JWTs from clients
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

    // Configure Cross-Origin Resource Sharing (CORS) to allow the frontend SPA to communicate with this API
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.SetIsOriginAllowed(origin => true) // Highly permissive for development/demo purposes
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // Required for SignalR WebSockets to function across origins
        });
    });

    builder.Services.AddControllers();

    // Automatically register all FluentValidation validators found in the Application assembly
    builder.Services.AddValidatorsFromAssemblyContaining<ArtifactUploadDtoValidator>();

    // Register SignalR for real-time bidirectional communication
    builder.Services.AddSignalR();
    builder.Services.AddEndpointsApiExplorer();

    // Configure Swagger UI and inject JWT Bearer authorization support into the testing interface
    builder.Services.AddSwaggerGen(c =>
    {
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
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
                new string[] {}
            }
        });
    });

    var app = builder.Build();

    // --------------------------------------------------------------------
    // HTTP Request Pipeline Configuration (Middleware Ordering is Critical)
    // --------------------------------------------------------------------

    // 1. Global Exception Handling (Catches all downstream errors)
    app.UseMiddleware<SRN.API.Middleware.ExceptionMiddleware>();

    // 2. Log incoming HTTP requests via Serilog
    app.UseSerilogRequestLogging();

    // 3. Expose Swagger UI in development environments
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // 4. Force HTTPS
    app.UseHttpsRedirection();

    // 5. Serve static frontend files (e.g., index.html, CSS, JS) from wwwroot
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // 6. Apply CORS policy before Auth
    app.UseCors("AllowAll");

    // 7. Identity and Security validation
    app.UseAuthentication();
    app.UseAuthorization();

    // 8. Map route endpoints for REST Controllers and SignalR Hubs
    app.MapControllers();
    app.MapHub<SRN.Infrastructure.Hubs.NotificationHub>("/notificationHub");

    // --------------------------------------------------------------------
    // Database Migration and Initial Data Seeding
    // --------------------------------------------------------------------
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SRN.Infrastructure.Persistence.ApplicationDbContext>();

        // Automatically apply any pending EF Core migrations to the database on startup
        db.Database.Migrate();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Seed system roles if they do not exist
        string[] roleNames = { "Admin", "Member" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName)).ConfigureAwait(false);
            }
        }

        // Seed the default root administrator account to ensure platform access is always possible
        var adminEmail = "admin@srn.ie";
        var adminUser = await userManager.FindByEmailAsync(adminEmail).ConfigureAwait(false);
        if (adminUser == null)
        {
            var newAdmin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, WalletAddress = "0xAdmin" };
            var result = await userManager.CreateAsync(newAdmin, "Admin@123456").ConfigureAwait(false);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newAdmin, "Admin").ConfigureAwait(false);
                Log.Information("--> Default Admin account created successfully.");
            }
        }
    }

    // Start listening for incoming HTTP requests
    app.Run();
}
catch (Exception ex)
{
    // Log fatal crashes that prevent the application from starting
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    // Ensure all log entries are flushed to their sinks before shutting down
    Log.CloseAndFlush();
}