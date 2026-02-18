using Microsoft.EntityFrameworkCore;
using SRN.Infrastructure.Persistence;
using SRN.Infrastructure.Blockchain;
using SRN.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SRN.API.Middleware;
using SRN.API.Hubs;
using Serilog; // [新增]
using FluentValidation; // [新增]
using FluentValidation.AspNetCore; // [新增]

// [新增] 1. 初始化启动日志 (Bootstrap Logger)
// 作用：即使 Appsettings 还没加载或者依赖注入挂了，也能记录下“程序启动失败”的致命错误
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("--> Starting Web Host...");

    var builder = WebApplication.CreateBuilder(args);

    // [新增] 2. 接入 Serilog 接管系统日志
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration) // 读取配置
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()); // 输出到控制台，你也可以加 .WriteTo.File(...)

    // --- 数据库配置 ---
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    // --- 区块链服务配置 ---
    var blockchainProvider = builder.Configuration["Blockchain:Provider"];
    if (blockchainProvider == "Real")
    {
        builder.Services.AddScoped<IBlockchainService, EthereumBlockchainService>();
        Log.Information("--> Using REAL Ethereum Blockchain Service"); // 改用 Log
    }
    else
    {
        builder.Services.AddScoped<IBlockchainService, MockBlockchainService>();
        Log.Information("--> Using MOCK Blockchain Service (No Gas cost)"); // 改用 Log
    }

    // --- Identity 配置 ---
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // --- JWT 配置 ---
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["Key"] ?? "DefaultSecretKey_MustBeLongerThan32Characters_ForSafety";
    var key = Encoding.ASCII.GetBytes(secretKey);

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
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

    // --- CORS 配置 ---
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.SetIsOriginAllowed(origin => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    // --- Controller & FluentValidation ---
    builder.Services.AddControllers();

    // [新增] 3. 自动注册 FluentValidation
    // 这会自动扫描整个项目，找到你写的 Validator 类并注入
    builder.Services.AddFluentValidationAutoValidation()
                    .AddFluentValidationClientsideAdapters();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // --- SignalR & Swagger ---
    builder.Services.AddSignalR();
    builder.Services.AddEndpointsApiExplorer();
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

    // --- 中间件管道 ---

    app.UseMiddleware<SRN.API.Middleware.ExceptionMiddleware>();

    // [新增] 4. Serilog 请求日志 (必须在 StaticFiles 之后，Endpoint 之前)
    // 它可以记录每个 HTTP 请求的耗时、状态码，非常适合排查“为什么接口慢”
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseCors("AllowAll");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<NotificationHub>("/notificationHub");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly"); // 捕获致命错误
}
finally
{
    Log.CloseAndFlush(); // 确保日志写完再退出
}