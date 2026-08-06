using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ShDicomStudio.Server;

var builder = WebApplication.CreateBuilder(args);

// 설정은 전부 환경변수로 (docker compose 에서 주입) — 기본값은 로컬 개발용.
var oracleConn = Environment.GetEnvironmentVariable("ORACLE_CONN")
    ?? "User Id=shdicom;Password=shdicom1234;Data Source=localhost:1521/FREEPDB1";
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "dev-only-secret-change-me-0123456789abcdef"; // 32자 이상 필요 (HS256)

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddSingleton(new UserStore(oracleConn));
builder.Services.AddSingleton(new StudyStore(oracleConn));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = "sh-dicom-studio",
        ValidAudience = "sh-dicom-studio-app",
        IssuerSigningKey = signingKey,
        ValidateIssuerSigningKey = true,
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// Oracle 은 첫 기동이 느리다 — 준비될 때까지 재시도 후 스키마·admin 시드.
var users = app.Services.GetRequiredService<UserStore>();
var studies = app.Services.GetRequiredService<StudyStore>();
await users.InitializeWithRetryAsync(app.Logger, maxAttempts: 60, delaySeconds: 5);
await studies.InitializeAsync();

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

    var token = CreateJwt(user, signingKey);
    return Results.Ok(new LoginResponse(token, user.Username, user.DisplayName));
});

// 검사 메타 업로드 (SaveDB/업데이트/InsExam 시 클라이언트가 호출) — 로그인 필요.
app.MapPost("/api/studies", async (StudyDto dto, ClaimsPrincipal principal) =>
{
    var username = principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "unknown";
    await studies.UpsertAsync(dto with { Username = username });
    return Results.Ok(new { ok = true });
}).RequireAuthorization();

// 검사 메타 검색 — 로그인 필요.
app.MapGet("/api/studies", async (string? patientId, string? patientName, string? modality,
        string? from, string? to) =>
    Results.Ok(await studies.SearchAsync(patientId, patientName, modality, from, to)))
    .RequireAuthorization();

// ── 계정 관리 — admin 전용 (본인 비밀번호 변경만 예외) ──────────────

app.MapGet("/api/users", async (ClaimsPrincipal principal) =>
    IsAdmin(principal) ? Results.Ok(await users.ListAsync()) : Results.Forbid())
    .RequireAuthorization();

app.MapPost("/api/users", async (CreateUserRequest request, ClaimsPrincipal principal) =>
{
    if (!IsAdmin(principal)) return Results.Forbid();
    if (request.Username.Trim().Length == 0 || request.Password.Length < 4)
        return Results.BadRequest(new { message = "아이디를 입력하고 비밀번호는 4자 이상이어야 합니다." });

    var created = await users.CreateAsync(request.Username.Trim(), request.Password,
        string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username.Trim() : request.DisplayName.Trim());
    return created ? Results.Ok(new { ok = true })
                   : Results.Conflict(new { message = "이미 존재하는 아이디입니다." });
}).RequireAuthorization();

app.MapPut("/api/users/{username}/password", async (string username, ChangePasswordRequest request,
    ClaimsPrincipal principal) =>
{
    // admin 은 누구든, 일반 사용자는 본인 것만.
    if (!IsAdmin(principal) && Subject(principal) != username) return Results.Forbid();
    if (request.NewPassword.Length < 4)
        return Results.BadRequest(new { message = "비밀번호는 4자 이상이어야 합니다." });
    await users.ChangePasswordAsync(username, request.NewPassword);
    return Results.Ok(new { ok = true });
}).RequireAuthorization();

app.MapDelete("/api/users/{username}", async (string username, ClaimsPrincipal principal) =>
{
    if (!IsAdmin(principal)) return Results.Forbid();
    if (username == "admin") return Results.BadRequest(new { message = "admin 계정은 삭제할 수 없습니다." });
    await users.DeleteAsync(username);
    return Results.Ok(new { ok = true });
}).RequireAuthorization();

app.Run();

static string Subject(ClaimsPrincipal principal) =>
    principal.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "";

static bool IsAdmin(ClaimsPrincipal principal) => Subject(principal) == "admin";

static string CreateJwt(UserRecord user, SymmetricSecurityKey key)
{
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

public sealed record CreateUserRequest(string Username, string Password, string DisplayName);

public sealed record ChangePasswordRequest(string NewPassword);
