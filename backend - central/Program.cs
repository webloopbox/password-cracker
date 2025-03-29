using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Enable CORS
app.UseCors("AllowFrontend");

// Configure the HTTP request pipeline.
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

app.MapPost("/crack-brute-force", (HttpContext context) =>
{
    Console.WriteLine("Received a request to /crack-brute-force");

    return Results.Ok("Endpoint /crack-brute-force is not yet implemented.");
});

app.MapPost("/crack-dictionary", [IgnoreAntiforgeryToken] async (HttpContext context) =>
{
    Console.WriteLine("Received a request to /crack-dictionary");

    if (!context.Request.HasFormContentType)
    {
        Console.WriteLine("Request is not multipart/form-data.");
        return Results.BadRequest("Request must be multipart/form-data.");
    }

    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var hostsString = form["hosts"].ToString();
    var file = form.Files.GetFile("file");

    if (string.IsNullOrEmpty(username))
    {
        Console.WriteLine("Username is missing.");
        return Results.BadRequest("Username is required.");
    }

    if (!int.TryParse(hostsString, out int hosts))
    {
        Console.WriteLine("Hosts is invalid or missing.");
        return Results.BadRequest("Hosts must be a valid number.");
    }

    if (file == null || file.Length == 0)
    {
        Console.WriteLine("No file uploaded or file is empty.");
        return Results.BadRequest("No file uploaded.");
    }

    Console.WriteLine($"Uploaded file name: {file.FileName}");
    Console.WriteLine($"Username: {username}");
    Console.WriteLine($"Hosts: {hosts}");

    if (!file.FileName.EndsWith(".zip"))
    {
        Console.WriteLine("Uploaded file is not a .zip file.");
        return Results.BadRequest("Only .zip files are allowed.");
    }

    // Generate a random hash for the file name
    string hashName;
    using (var sha256 = SHA256.Create())
    {
        hashName = BitConverter.ToString(sha256.ComputeHash(Guid.NewGuid().ToByteArray()))
            .Replace("-", "")
            .Substring(0, 16);
    }

    Console.WriteLine($"Generated hash name for the file: {hashName}");

    var dictionaryPath = Path.Combine("dictionary", $"{hashName}.zip");
    Console.WriteLine($"File will be saved to: {dictionaryPath}");

    // Save the file to the dictionary folder
    Directory.CreateDirectory("dictionary");
    Console.WriteLine("Ensured 'dictionary' folder exists.");

    using (var stream = new FileStream(dictionaryPath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    Console.WriteLine("File has been successfully saved.");

    return Results.Ok(new { FileName = $"{hashName}.zip", Path = dictionaryPath });
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}