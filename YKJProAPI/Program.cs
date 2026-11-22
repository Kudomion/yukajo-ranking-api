using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using YKJProAPI.Data;
using YKJProAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Database: on Railway set DB_PATH to a file on a mounted volume (e.g. /data/yukajo.db),
// otherwise the database lives inside the container and is lost on every deploy.
var dbPath = builder.Configuration["DB_PATH"];
if (string.IsNullOrWhiteSpace(dbPath)) dbPath = "yukajo.db";
var dbDir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
if (!string.IsNullOrEmpty(dbDir)) Directory.CreateDirectory(dbDir);

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSingleton<AdminAuth>();
builder.Services.AddHostedService<DailyBackup>();   // a copy of the database every day (backups/ next to DB_PATH, 7 kept)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Railway terminates TLS at its proxy and appends the client IP to X-Forwarded-For.
// ForwardLimit = 1 uses only the entry added by that proxy, so clients cannot spoof it.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// Admin login rate limit: 5 attempts per 15 minutes per IP, and 30 per 15 minutes overall.
static bool IsAdminLogin(HttpContext ctx) =>
    HttpMethods.IsPost(ctx.Request.Method) &&
    string.Equals(ctx.Request.Path.Value?.TrimEnd('/'), "/api/admin/login", StringComparison.OrdinalIgnoreCase);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(ctx => IsAdminLogin(ctx)
            ? RateLimitPartition.GetFixedWindowLimiter("ip:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 })
            : RateLimitPartition.GetNoLimiter("none")),
        PartitionedRateLimiter.Create<HttpContext, string>(ctx => IsAdminLogin(ctx)
            ? RateLimitPartition.GetFixedWindowLimiter("all",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 })
            : RateLimitPartition.GetNoLimiter("none")));
    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "900";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many login attempts. Try again in 15 minutes." }, token);
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbBootstrap.MigrateDatabase(db, app.Logger);

    if (!scope.ServiceProvider.GetRequiredService<AdminAuth>().Enabled)
        app.Logger.LogWarning("ADMIN_TOKEN is not set (or shorter than {Min} chars): admin endpoints are disabled.", AdminAuth.MinTokenLength);
}

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Security headers for the admin page and admin API.
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;
    if (path.StartsWithSegments("/admin") || path.StartsWithSegments("/api/admin"))
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Cache-Control"] = "no-store";
        h["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
    }
    await next();
});

app.UseDefaultFiles();   // /admin/ -> /admin/index.html (and /admin -> /admin/)
app.UseStaticFiles();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();
