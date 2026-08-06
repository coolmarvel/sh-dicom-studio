using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShDicomStudio.Core.Database;

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

    /// <summary>서버의 검사 메타 한 건 (파일은 로컬에만 있음 — 3차에서 결정).</summary>
    public sealed record ServerStudy(
        string StudyUid, string PatientId, string PatientName, string Sex, string Age,
        string Modality, DateTime? StudyDate, DateTime? BirthDate, string StudyDescription,
        string AccessionNumber, string ReferringPhysician, string Comment, bool Anonymous,
        int ImageCount, DateTime CreatedAt, string Username);

    /// <summary>로컬 DB 검사 메타를 서버에 업로드(upsert). 실패해도 예외 대신 false — 로컬 저장이 우선.</summary>
    public static async Task<bool> UploadStudyAsync(StudyRecord record)
    {
        if (!AppSession.IsOnline) return false;
        try
        {
            var info = record.Info;
            var payload = new ServerStudy(record.StudyUid, info.PatientId, info.PatientName,
                info.Sex, info.Age, info.Modality, info.StudyDate, info.BirthDate,
                info.StudyDescription, info.AccessionNumber, info.ReferringPhysician,
                info.Comment, info.Anonymous, record.ImageCount, record.CreatedAt, AppSession.Username);

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{AppSession.ServerUrl.TrimEnd('/')}/api/studies")
            {
                Content = JsonContent.Create(payload),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSession.Token);

            var response = await Http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Worklist (검사 예약/오더 — 4차) ──────────────────────────────

    public sealed record Order(
        long Id, string PatientId, string PatientName, string Sex, DateTime? BirthDate,
        string Modality, DateTime ScheduledDate, string Description, string AccessionNumber,
        string CreatedBy);

    public static async Task<List<Order>?> GetOrdersAsync(DateTime? date, string? modality)
    {
        if (!AppSession.IsOnline) return null;
        try
        {
            var query = new List<string>();
            if (date is { } d) query.Add($"date={d:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(modality)) query.Add($"modality={Uri.EscapeDataString(modality)}");
            var response = await Http.SendAsync(Authorized(HttpMethod.Get,
                $"/api/orders{(query.Count > 0 ? "?" + string.Join('&', query) : "")}"));
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<List<Order>>()
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static Task<(bool Ok, string Message)> CreateOrderAsync(Order order) =>
        SendSimpleAsync(Authorized(HttpMethod.Post, "/api/orders", order));

    public static Task<(bool Ok, string Message)> DeleteOrderAsync(long id) =>
        SendSimpleAsync(Authorized(HttpMethod.Delete, $"/api/orders/{id}"));

    // ── 계정 관리 (admin 전용 API — S3) ─────────────────────────────

    public sealed record UserEntry(string Username, string DisplayName);

    private static HttpRequestMessage Authorized(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, $"{AppSession.ServerUrl.TrimEnd('/')}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSession.Token);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    public static async Task<List<UserEntry>?> GetUsersAsync()
    {
        try
        {
            var response = await Http.SendAsync(Authorized(HttpMethod.Get, "/api/users"));
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<List<UserEntry>>()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed record ApiMessage(string Message);

    private static async Task<(bool Ok, string Message)> SendSimpleAsync(HttpRequestMessage request)
    {
        try
        {
            var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode) return (true, "완료");
            var body = await response.Content.ReadFromJsonAsync<ApiMessage>();
            return (false, body?.Message ?? $"실패 (HTTP {(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, $"서버 통신 실패 — {ex.Message}");
        }
    }

    public static Task<(bool Ok, string Message)> CreateUserAsync(string username, string password, string displayName) =>
        SendSimpleAsync(Authorized(HttpMethod.Post, "/api/users", new { username, password, displayName }));

    public static Task<(bool Ok, string Message)> ChangePasswordAsync(string username, string newPassword) =>
        SendSimpleAsync(Authorized(HttpMethod.Put, $"/api/users/{Uri.EscapeDataString(username)}/password", new { newPassword }));

    public static Task<(bool Ok, string Message)> DeleteUserAsync(string username) =>
        SendSimpleAsync(Authorized(HttpMethod.Delete, $"/api/users/{Uri.EscapeDataString(username)}"));

    /// <summary>서버 검사 메타 검색 — 실패 시 null (호출부가 안내).</summary>
    public static async Task<List<ServerStudy>?> SearchStudiesAsync(
        string? patientId, string? patientName, string? modality, DateTime? from, DateTime? to)
    {
        if (!AppSession.IsOnline) return null;
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(patientId)) query.Add($"patientId={Uri.EscapeDataString(patientId)}");
            if (!string.IsNullOrWhiteSpace(patientName)) query.Add($"patientName={Uri.EscapeDataString(patientName)}");
            if (!string.IsNullOrWhiteSpace(modality)) query.Add($"modality={Uri.EscapeDataString(modality)}");
            if (from is { } f) query.Add($"from={f:yyyy-MM-dd}");
            if (to is { } t) query.Add($"to={t:yyyy-MM-dd}");

            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{AppSession.ServerUrl.TrimEnd('/')}/api/studies{(query.Count > 0 ? "?" + string.Join('&', query) : "")}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSession.Token);

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<List<ServerStudy>>();
        }
        catch
        {
            return null;
        }
    }
}
