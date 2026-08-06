using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ShDicomStudio.Server;

var builder = WebApplication.CreateBuilder(args);

// 설정은 전부 환경변수로 (docker compose 에서 주입) — 기본값은 로컬 개발용.
var oracleConn = Environment.GetEnvironmentVariable("ORACLE_CONN")
    ?? "User Id=shdicom;Password=shdicom1234;Data Source=localhost:1521/FREEPDB1";
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "dev-only-secret-change-me-0123456789abcdef"; // 32자 이상 필요 (HS256)

builder.Services.AddSingleton(new UserStore(oracleConn));

var app = builder.Build();

// Oracle 은 첫 기동이 느리다 — 준비될 때까지 재시도 후 스키마·admin 시드.
var users = app.Services.GetRequiredService<UserStore>();
await users.InitializeWithRetryAsync(app.Logger, maxAttempts: 60, delaySeconds: 5);

// ── 엔드포인트 ──────────────────────────────────────────────────────

app.MapGet("/health", async () =>
{
    var dbOk = await users.PingAsync();
    var payload = new { status = dbOk ? "ok" : "degraded", db = dbOk ? "ok" : "unreachable" };
    return dbOk ? Results.Ok(payload) : Results.Json(payload, statusCode: 503);
});

app.MapPost("/api/auth/login", async (LoginRequest request) =>
{
    var user = await users.VerifyAsync(request.Username, request.Password);
    if (user is null)
        return Results.Unauthorized();

    var token = CreateJwt(user, jwtSecret);
    return Results.Ok(new LoginResponse(token, user.Username, user.DisplayName));
});

app.Run();

static string CreateJwt(UserRecord user, string secret)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    var token = new JwtSecurityToken(
        issuer: "sh-dicom-studio",
        audience: "sh-dicom-studio-app",
        claims:
        [
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim("displayName", user.DisplayName),
        ],
        expires: DateTime.UtcNow.AddHours(12),
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
    return new JwtSecurityTokenHandler().WriteToken(token);
}

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string Token, string Username, string DisplayName);
