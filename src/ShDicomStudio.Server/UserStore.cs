using Oracle.ManagedDataAccess.Client;

namespace ShDicomStudio.Server;

public sealed record UserRecord(string Username, string DisplayName);

/// <summary>
/// Oracle 의 USERS 테이블 — 스키마 초기화(없으면 생성)·admin 시드·로그인 검증.
/// 비밀번호는 BCrypt 해시로만 저장한다.
/// </summary>
public sealed class UserStore(string connectionString)
{
    /// <summary>Oracle 이 준비될 때까지 재시도하며 스키마·시드를 만든다 (첫 기동 수십 초 흡수).</summary>
    public async Task InitializeWithRetryAsync(ILogger logger, int maxAttempts, int delaySeconds)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await InitializeAsync();
                logger.LogInformation("Oracle 초기화 완료 (시도 {Attempt}회)", attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning("Oracle 대기 중 ({Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }

    private async Task InitializeAsync()
    {
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync();

        // Oracle 에는 CREATE TABLE IF NOT EXISTS 가 없어 존재 검사 후 생성한다.
        var exists = await ScalarAsync<decimal>(conn,
            "SELECT COUNT(*) FROM user_tables WHERE table_name = 'USERS'");
        if (exists == 0)
        {
            await ExecuteAsync(conn, """
                CREATE TABLE USERS (
                  ID            NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                  USERNAME      VARCHAR2(50 CHAR) NOT NULL UNIQUE,
                  PASSWORD_HASH VARCHAR2(100 CHAR) NOT NULL,
                  DISPLAY_NAME  VARCHAR2(100 CHAR) NOT NULL,
                  CREATED_AT    TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL
                )
                """);
        }

        // 계정이 하나도 없으면 admin 시드 — 초기 비밀번호는 첫 로그인 후 변경 권장(S3 에서 기능화).
        var userCount = await ScalarAsync<decimal>(conn, "SELECT COUNT(*) FROM USERS");
        if (userCount == 0)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO USERS (USERNAME, PASSWORD_HASH, DISPLAY_NAME) VALUES (:u, :h, :d)";
            cmd.Parameters.Add(new OracleParameter("u", "admin"));
            cmd.Parameters.Add(new OracleParameter("h", BCrypt.Net.BCrypt.HashPassword("admin1234")));
            cmd.Parameters.Add(new OracleParameter("d", "관리자"));
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            await using var conn = new OracleConnection(connectionString);
            await conn.OpenAsync();
            _ = await ScalarAsync<decimal>(conn, "SELECT 1 FROM DUAL");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>ID/PW 검증 — 성공 시 사용자, 실패(없음/불일치) 시 null.</summary>
    public async Task<UserRecord?> VerifyAsync(string username, string password)
    {
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PASSWORD_HASH, DISPLAY_NAME FROM USERS WHERE USERNAME = :u";
        cmd.Parameters.Add(new OracleParameter("u", username));

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var hash = reader.GetString(0);
        var displayName = reader.GetString(1);
        return BCrypt.Net.BCrypt.Verify(password, hash)
            ? new UserRecord(username, displayName)
            : null;
    }

    private static async Task<T> ScalarAsync<T>(OracleConnection conn, string sql)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (T)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(OracleConnection conn, string sql)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
