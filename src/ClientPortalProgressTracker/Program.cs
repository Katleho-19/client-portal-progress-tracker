using ClientPortalProgressTracker.Models.Entities;
using ClientPortalProgressTracker.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Supabase.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Cookie authentication for signed-in freelancers
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

// Per-request client (anon key): used only for sign-up, sign-in and password reset
builder.Services.AddSupabase(
    supabaseUrl: builder.Configuration["Supabase:Url"]
        ?? throw new InvalidOperationException("Missing 'Supabase:Url' in user secrets."),
    supabaseKey: builder.Configuration["Supabase:AnonKey"]
        ?? throw new InvalidOperationException("Missing 'Supabase:AnonKey' in user secrets."),
    configureOptions: options =>
    {
        options.AutoConnectRealtime = false;
        options.AutoRefreshToken = false;
    });

// Server-side database client (service role key)
builder.Services.AddSingleton<SupabaseAdmin>();
builder.Services.AddSingleton<EmailService>();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication(); // Added before UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/supabase-check", async (SupabaseAdmin admin) =>
    {
        var client = await admin.GetAsync();
        var result = await client.From<Freelancer>().Get();
        return Results.Ok(new { ok = true, freelancers = result.Models.Count });
    });
}

app.Run();
