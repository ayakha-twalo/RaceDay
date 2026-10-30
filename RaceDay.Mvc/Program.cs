using RaceDay.Mvc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// MVC session: remembers who is logged in between page requests.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Lets RaceDayApiClient read the current user's session.
builder.Services.AddHttpContextAccessor();

// One named HttpClient for the Part 2 API. The address comes from
// configuration so Docker can override it later without code changes.
builder.Services
    .AddHttpClient("RaceDayApi", client =>
    {
        string baseUrl = builder.Configuration["ApiSettings:BaseUrl"]
            ?? throw new InvalidOperationException(
                "ApiSettings:BaseUrl is missing.");

        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

        // Fail fast if the API is unreachable instead of hanging.
        client.Timeout = TimeSpan.FromSeconds(15);
    })
    // IMPORTANT: HttpClient handlers are shared between requests. If
    // they stored cookies themselves, one user's API session could leak
    // to another user. We handle the API cookie manually per user.
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler { UseCookies = false });

builder.Services.AddScoped<RaceDayApiClient>();

var app = builder.Build();

// Users get a friendly page instead of a stack trace.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// No HTTPS redirect: the Docker container serves plain HTTP on 8080.
app.UseStaticFiles();
app.UseRouting();

// Session must come after routing and before the controllers run.
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();