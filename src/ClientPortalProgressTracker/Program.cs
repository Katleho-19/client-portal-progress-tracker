using ClientPortalProgressTracker.Models.Entities;
using ClientPortalProgressTracker.Services;
using Supabase.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

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
