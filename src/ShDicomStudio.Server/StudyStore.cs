using System.Globalization;
using Oracle.ManagedDataAccess.Client;

namespace ShDicomStudio.Server;

/// <summary>클라이언트가 올리는 검사 메타데이터 (파일은 아직 로컬 보관 — 3차에서 결정).</summary>
public sealed record StudyDto(
    string StudyUid, string PatientId, string PatientName, string Sex, string Age,
    string Modality, DateTime? StudyDate, DateTime? BirthDate, string StudyDescription,
    string AccessionNumber, string ReferringPhysician, string Comment, bool Anonymous,
    int ImageCount, DateTime CreatedAt, string Username);

/// <summary>Oracle 의 STUDIES 테이블 — 검사 메타 upsert(StudyUid 기준)·검색.</summary>
public sealed class StudyStore(string connectionString)
{
    public async Task InitializeAsync()
    {
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync();

        var exists = await ScalarAsync(conn, "SELECT COUNT(*) FROM user_tables WHERE table_name = 'STUDIES'");
        if (exists == 0)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE STUDIES (
                  ID            NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                  STUDY_UID     VARCHAR2(100 CHAR) NOT NULL UNIQUE,
                  PATIENT_ID    VARCHAR2(100 CHAR) NOT NULL,
                  PATIENT_NAME  VARCHAR2(200 CHAR) NOT NULL,
                  SEX           VARCHAR2(5 CHAR),
                  AGE           VARCHAR2(10 CHAR),
                  MODALITY      VARCHAR2(20 CHAR) NOT NULL,
                  STUDY_DATE    VARCHAR2(10 CHAR),
                  BIRTH_DATE    VARCHAR2(10 CHAR),
                  DESCRIPTION   VARCHAR2(500 CHAR),
                  ACCESSION_NO  VARCHAR2(100 CHAR),
                  REF_PHYSICIAN VARCHAR2(200 CHAR),
                  COMMENTS      VARCHAR2(2000 CHAR),
                  ANONYMOUS     NUMBER(1) NOT NULL,
                  IMAGE_COUNT   NUMBER NOT NULL,
                  CREATED_AT    VARCHAR2(19 CHAR) NOT NULL,
                  USERNAME      VARCHAR2(50 CHAR) NOT NULL
                )
                """;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>StudyUid 기준 upsert — 같은 검사를 다시 올리면(업데이트·InsExam) 덮어쓴다.</summary>
    public async Task UpsertAsync(StudyDto dto)
    {
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            MERGE INTO STUDIES s
            USING (SELECT :suid AS STUDY_UID FROM DUAL) src
            ON (s.STUDY_UID = src.STUDY_UID)
            WHEN MATCHED THEN UPDATE SET
              PATIENT_ID = :pid, PATIENT_NAME = :name, SEX = :sex, AGE = :age, MODALITY = :moda,
              STUDY_DATE = :sdate, BIRTH_DATE = :bdate, DESCRIPTION = :descr, ACCESSION_NO = :acc,
              REF_PHYSICIAN = :refp, COMMENTS = :cmt, ANONYMOUS = :anon, IMAGE_COUNT = :cnt,
              CREATED_AT = :created, USERNAME = :usr
            WHEN NOT MATCHED THEN INSERT
              (STUDY_UID, PATIENT_ID, PATIENT_NAME, SEX, AGE, MODALITY, STUDY_DATE, BIRTH_DATE,
               DESCRIPTION, ACCESSION_NO, REF_PHYSICIAN, COMMENTS, ANONYMOUS, IMAGE_COUNT, CREATED_AT, USERNAME)
            VALUES (:suid, :pid, :name, :sex, :age, :moda, :sdate, :bdate, :descr, :acc, :refp, :cmt,
                    :anon, :cnt, :created, :usr)
            """;
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter("suid", dto.StudyUid));
        cmd.Parameters.Add(new OracleParameter("pid", dto.PatientId));
        cmd.Parameters.Add(new OracleParameter("name", dto.PatientName));
        cmd.Parameters.Add(new OracleParameter("sex", dto.Sex));
        cmd.Parameters.Add(new OracleParameter("age", dto.Age));
        cmd.Parameters.Add(new OracleParameter("moda", dto.Modality));
        cmd.Parameters.Add(new OracleParameter("sdate", (object?)Day(dto.StudyDate) ?? DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("bdate", (object?)Day(dto.BirthDate) ?? DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("descr", dto.StudyDescription));
        cmd.Parameters.Add(new OracleParameter("acc", dto.AccessionNumber));
        cmd.Parameters.Add(new OracleParameter("refp", dto.ReferringPhysician));
        cmd.Parameters.Add(new OracleParameter("cmt", dto.Comment));
        cmd.Parameters.Add(new OracleParameter("anon", dto.Anonymous ? 1 : 0));
        cmd.Parameters.Add(new OracleParameter("cnt", dto.ImageCount));
        cmd.Parameters.Add(new OracleParameter("created", dto.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
        cmd.Parameters.Add(new OracleParameter("usr", dto.Username));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<StudyDto>> SearchAsync(string? patientId, string? patientName,
        string? modality, string? from, string? to)
    {
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(patientId))
        {
            where.Add("PATIENT_ID LIKE :pid");
            cmd.Parameters.Add(new OracleParameter("pid", $"%{patientId.Trim()}%"));
        }
        if (!string.IsNullOrWhiteSpace(patientName))
        {
            where.Add("PATIENT_NAME LIKE :name");
            cmd.Parameters.Add(new OracleParameter("name", $"%{patientName.Trim()}%"));
        }
        if (!string.IsNullOrWhiteSpace(modality))
        {
            where.Add("MODALITY = :moda");
            cmd.Parameters.Add(new OracleParameter("moda", modality));
        }
        if (!string.IsNullOrWhiteSpace(from))
        {
            where.Add("STUDY_DATE >= :fromd");
            cmd.Parameters.Add(new OracleParameter("fromd", from));
        }
        if (!string.IsNullOrWhiteSpace(to))
        {
            where.Add("STUDY_DATE <= :tod");
            cmd.Parameters.Add(new OracleParameter("tod", to));
        }

        cmd.CommandText = "SELECT STUDY_UID, PATIENT_ID, PATIENT_NAME, SEX, AGE, MODALITY, STUDY_DATE, "
            + "BIRTH_DATE, DESCRIPTION, ACCESSION_NO, REF_PHYSICIAN, COMMENTS, ANONYMOUS, IMAGE_COUNT, "
            + "CREATED_AT, USERNAME FROM STUDIES"
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
            + " ORDER BY CREATED_AT DESC FETCH FIRST 200 ROWS ONLY";

        var results = new List<StudyDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new StudyDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? "" : reader.GetString(3),
                reader.IsDBNull(4) ? "" : reader.GetString(4),
                reader.GetString(5),
                ParseDay(reader.IsDBNull(6) ? null : reader.GetString(6)),
                ParseDay(reader.IsDBNull(7) ? null : reader.GetString(7)),
                reader.IsDBNull(8) ? "" : reader.GetString(8),
                reader.IsDBNull(9) ? "" : reader.GetString(9),
                reader.IsDBNull(10) ? "" : reader.GetString(10),
                reader.IsDBNull(11) ? "" : reader.GetString(11),
                reader.GetDecimal(12) != 0,
                (int)reader.GetDecimal(13),
                DateTime.ParseExact(reader.GetString(14), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                reader.GetString(15)));
        }
        return results;
    }

    private static string? Day(DateTime? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime? ParseDay(string? s) =>
        s is null ? null : DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<decimal> ScalarAsync(OracleConnection conn, string sql)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (decimal)(await cmd.ExecuteScalarAsync())!;
    }
}
