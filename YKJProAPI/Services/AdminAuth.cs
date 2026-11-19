using System.Security.Cryptography;
using System.Text;

namespace YKJProAPI.Services;

// Admin login: the admin secret (ADMIN_TOKEN) is exchanged for a short-lived signed
// session token, so the secret itself is sent only once per login.
public class AdminAuth
{
    public const int MinTokenLength = 16;
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    private readonly byte[]? _tokenHash;
    private readonly byte[]? _signingKey;

    public string ActorName { get; }

    public AdminAuth(IConfiguration config)
    {
        var token = config["ADMIN_TOKEN"];
        var name = config["ADMIN_NAME"];
        ActorName = string.IsNullOrWhiteSpace(name) ? "admin" : name.Trim();

        if (string.IsNullOrEmpty(token) || token.Length < MinTokenLength)
            return; // admin disabled

        _tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        // ADMIN_SESSION_KEY is optional; if missing, a key is derived from ADMIN_TOKEN
        // (changing ADMIN_TOKEN then also logs out every session).
        var sessionKey = config["ADMIN_SESSION_KEY"];
        _signingKey = !string.IsNullOrEmpty(sessionKey)
            ? SHA256.HashData(Encoding.UTF8.GetBytes(sessionKey))
            : HMACSHA256.HashData(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes("ykjpro-admin-session-v1"));
    }

    public bool Enabled => _tokenHash != null;

    public bool CheckSecret(string? candidate)
    {
        if (_tokenHash == null || string.IsNullOrEmpty(candidate) || candidate.Length > 512) return false;
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(candidate));
        return CryptographicOperations.FixedTimeEquals(_tokenHash, b);
    }

    public (string token, DateTime expiresAt) IssueSession()
    {
        var expires = DateTime.UtcNow.Add(SessionLifetime);
        var payload = $"{new DateTimeOffset(expires).ToUnixTimeSeconds()}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";
        return ($"{payload}.{Sign(payload)}", expires);
    }

    public bool ValidateSession(string? token)
    {
        if (_signingKey == null || string.IsNullOrEmpty(token) || token.Length > 200) return false;
        var lastDot = token.LastIndexOf('.');
        if (lastDot <= 0) return false;

        var payload = token[..lastDot];
        var sig = token[(lastDot + 1)..];
        var expected = Sign(payload);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(sig)))
            return false;

        var expPart = payload.Split('.')[0];
        return long.TryParse(expPart, out var exp) && DateTimeOffset.UtcNow.ToUnixTimeSeconds() < exp;
    }

    private string Sign(string payload) =>
        Convert.ToBase64String(HMACSHA256.HashData(_signingKey!, Encoding.UTF8.GetBytes(payload)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
