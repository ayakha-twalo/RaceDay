using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Controllers and Swagger.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description =
            "REST API for the RaceDay event management system. " +
            "Log in through POST /api/auth/login first; the session " +
            "cookie then identifies you (and your role) on later calls."
    });

    // Lets the [SwaggerOperation] and [SwaggerResponse] attributes
    // on the controllers show up in the Swagger UI.
    options.EnableAnnotations();
});

// SQL Server connection (Entity Framework Core, Code First).
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Server-side session storage.
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // SameAsRequest lets the cookie work on both the http and https
    // Swagger profiles while developing.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "RaceDay.Api v1");
    });
}

app.UseHttpsRedirection();

// Sessions must be enabled before the controllers run.
app.UseSession();

app.UseAuthorization();

app.MapControllers();

app.Run();
