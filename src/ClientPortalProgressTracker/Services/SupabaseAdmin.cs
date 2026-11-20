using Supabase;

namespace ClientPortalProgressTracker.Services;

/// <summary>
/// One long-lived client that uses the SERVICE ROLE key. It never signs users in.
/// It bypasses row-level security, so every query that uses it must filter by the
/// logged-in freelancer's id.
/// </summary>
public sealed class SupabaseAdmin
{
    private readonly Client _client;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public SupabaseAdmin(IConfiguration config)
    {
        var url = config["Supabase:Url"]
            ?? throw new InvalidOperationException("Missing 'Supabase:Url' in user secrets.");
        var key = config["Supabase:ServiceRoleKey"]
            ?? throw new InvalidOperationException("Missing 'Supabase:ServiceRoleKey' in user secrets.");

        _client = new Client(url, key, new SupabaseOptions
        {
            AutoRefreshToken = false,
            AutoConnectRealtime = false
        });
    }

    public async Task<Client> GetAsync()
    {
        if (_initialized) return _client;
        await _gate.WaitAsync();
        try
        {
            if (!_initialized)
            {
                await _client.InitializeAsync();
                _initialized = true;
            }
        }
        finally { _gate.Release(); }
        return _client;
    }
}