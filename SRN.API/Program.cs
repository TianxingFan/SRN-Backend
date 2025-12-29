using Microsoft.EntityFrameworkCore;
using SRN.Infrastructure.Persistence;
using SRN.Infrastructure.Blockchain;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// 1. Retrieve the connection string from configuration (appsettings.json)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. Register the DbContext with PostgreSQL support
builder.Services.AddDbContext<SrnDbContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Register the Blockchain Service for dependency injection
builder.Services.AddScoped<BlockchainService>();

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();