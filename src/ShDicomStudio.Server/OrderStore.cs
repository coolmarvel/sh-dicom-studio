using System.Globalization;
using Oracle.ManagedDataAccess.Client;

namespace ShDicomStudio.Server;

/// <summary>검사 예약(오더) — Worklist 의 한 줄. 실무의 RIS 처방에 해당.</summary>
public sealed record OrderDto(
    long Id, string PatientId, string PatientName, string Sex, DateTime? BirthDate,
    string Modality, DateTime ScheduledDate, string Description, string AccessionNumber,
    string CreatedBy);

/// <summary>Oracle 의 ORDERS 테이블 — 예약 등록/조회/삭제.</summary>
public sealed class OrderStore(string connectionString)
{
    public async Task InitializeAsync()
    {
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync();

        var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM user_tables WHERE table_name = 'ORDERS'";
        if ((decimal)(await check.ExecuteScalarAsync())! == 0)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE ORDERS (
                  ID             NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                  PATIENT_ID     VARCHAR2(100 CHAR) NOT NULL,
                  PATIENT_NAME   VARCHAR2(200 CHAR) NOT NULL,
                  SEX            VARCHAR2(5 CHAR),
                  BIRTH_DATE     VARCHAR2(10 CHAR),
                  MODALITY       VARCHAR2(20 CHAR) NOT NULL,
                  SCHEDULED_DATE VARCHAR2(10 CHAR) NOT NULL,
                  DESCRIPTION    VARCHAR2(500 CHAR),
                  ACCESSION_NO   VARCHAR2(100 CHAR),
                  CREATED_BY     VARCHAR2(50 CHAR) NOT NULL
                )
                """;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<long> CreateAsync(OrderDto order)
    {
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandText = """
            INSERT INTO ORDERS (PATIENT_ID, PATIENT_NAME, SEX, BIRTH_DATE, MODALITY,
                                SCHEDULED_DATE, DESCRIPTION, ACCESSION_NO, CREATED_BY)
            VALUES (:pid, :name, :sex, :bdate, :moda, :sched, :descr, :acc, :usr)
            RETURNING ID INTO :newid
            """;
        cmd.Parameters.Add(new OracleParameter("pid", order.PatientId));
        cmd.Parameters.Add(new OracleParameter("name", order.PatientName));
        cmd.Parameters.Add(new OracleParameter("sex", order.Sex));
        cmd.Parameters.Add(new OracleParameter("bdate",
            (object?)order.BirthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("moda", order.Modality));
        cmd.Parameters.Add(new OracleParameter("sched",
            order.ScheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        cmd.Parameters.Add(new OracleParameter("descr", order.Description));
        cmd.Parameters.Add(new OracleParameter("acc", order.AccessionNumber));
        cmd.Parameters.Add(new OracleParameter("usr", order.CreatedBy));
        var idParam = new OracleParameter("newid", OracleDbType.Decimal, System.Data.ParameterDirection.Output);
        cmd.Parameters.Add(idParam);

        await cmd.ExecuteNonQueryAsync();
        return (long)((Oracle.ManagedDataAccess.Types.OracleDecimal)idParam.Value!).Value;
    }

    public async Task<List<OrderDto>> SearchAsync(string? date, string? modality)
    {
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(date))
        {
            where.Add("SCHEDULED_DATE = :sched");
            cmd.Parameters.Add(new OracleParameter("sched", date));
        }
        if (!string.IsNullOrWhiteSpace(modality))
        {
            where.Add("MODALITY = :moda");
            cmd.Parameters.Add(new OracleParameter("moda", modality));
        }

        cmd.CommandText = "SELECT ID, PATIENT_ID, PATIENT_NAME, SEX, BIRTH_DATE, MODALITY, "
            + "SCHEDULED_DATE, DESCRIPTION, ACCESSION_NO, CREATED_BY FROM ORDERS"
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
            + " ORDER BY ID DESC FETCH FIRST 200 ROWS ONLY";

        var results = new List<OrderDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new OrderDto(
                (long)reader.GetDecimal(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? "" : reader.GetString(3),
                reader.IsDBNull(4) ? null : DateTime.ParseExact(reader.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.GetString(5),
                DateTime.ParseExact(reader.GetString(6), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.IsDBNull(7) ? "" : reader.GetString(7),
                reader.IsDBNull(8) ? "" : reader.GetString(8),
                reader.GetString(9)));
        }
        return results;
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = new OracleConnection(connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ORDERS WHERE ID = :oid";
        cmd.Parameters.Add(new OracleParameter("oid", id));
        await cmd.ExecuteNonQueryAsync();
    }
}
