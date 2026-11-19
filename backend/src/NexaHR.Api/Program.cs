
using Microsoft.EntityFrameworkCore;
using NexaHR.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Register API services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register SQL Server database context
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is not configured.");

builder.Services.AddDbContext<NexaHRDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
