using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ShDicomStudio.App.Services;

/// <summary>로그인 세션 (앱 전역) — 오프라인이면 IsOnline=false 로 그대로 동작한다.</summary>
public static class AppSession
{
    public static bool IsOnline { get; private set; }
    public static string ServerUrl { get; private set; } = "";
    public static string Username { get; private set; } = "";
    public static string DisplayName { get; private set; } = "";
    public static string Token { get; private set; } = "";

    public static void SignIn(string serverUrl, string username, string displayName, string token)
    {
        IsOnline = true;
        ServerUrl = serverUrl;
        Username = username;
        DisplayName = displayName;
        Token = token;
    }
}

/// <summary>서버(ASP.NET Core) HTTP 클라이언트 — 2차 S2 는 로그인만, S3 에서 검사 API 추가.</summary>
public sealed class ServerClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public sealed record LoginResult(bool Success, string Message,
        string Username = "", string DisplayName = "", string Token = "");

    private sealed record LoginResponse(string Token, string Username, string DisplayName);

    public static async Task<LoginResult> LoginAsync(string serverUrl, string username, string password)
    {
        try
        {
            var response = await Http.PostAsJsonAsync(
                $"{serverUrl.TrimEnd('/')}/api/auth/login", new { username, password });

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return new LoginResult(false, "아이디 또는 비밀번호가 올바르지 않습니다.");
            if (!response.IsSuccessStatusCode)
                return new LoginResult(false, $"서버 오류 (HTTP {(int)response.StatusCode})");

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (body is null)
                return new LoginResult(false, "서버 응답을 해석할 수 없습니다.");

            return new LoginResult(true, "로그인 성공", body.Username, body.DisplayName, body.Token);
        }
        catch (TaskCanceledException)
        {
            return new LoginResult(false, "서버 응답이 없습니다 (시간 초과) — 서버가 켜져 있는지 확인하세요.");
        }
        catch (HttpRequestException ex)
        {
            return new LoginResult(false, $"서버에 연결할 수 없습니다 — {ex.Message}");
        }
    }
}
