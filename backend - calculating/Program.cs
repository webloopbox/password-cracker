using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add OpenAPI
builder.Services.AddOpenApi();

// Configure PostgreSQL database connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)); // Use PostgreSQL instead of InMemory

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/test-db", async ([FromServices] AppDbContext dbContext) =>
{
    await dbContext.Database.EnsureCreatedAsync();

    var testEntity = new TestEntity { Name = "Test Entry" };
    dbContext.TestEntities.Add(testEntity);
    await dbContext.SaveChangesAsync();

    var entries = await dbContext.TestEntities.ToListAsync();
    return Results.Ok(entries);
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TestEntity> TestEntities { get; set; }
}

class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // Ensure Name is initialized
}