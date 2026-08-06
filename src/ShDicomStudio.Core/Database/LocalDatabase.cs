using System.Globalization;
using Microsoft.Data.Sqlite;
using ShDicomStudio.Core.Dicom;

namespace ShDicomStudio.Core.Database;

/// <summary>로컬 DB 에 저장된 검사 한 건 (VPWinGate Local Database 의 행).</summary>
public sealed record StudyRecord(long Id, string StudyUid, ExamInfo Info, int ImageCount, DateTime CreatedAt);

/// <summary>
/// PACS 전송 전 검사들의 로컬 보관소 (VPWinGate 3.4 Local Database 대응) — SQLite 메타 +
/// `dicom/&lt;StudyUid&gt;/` 폴더의 DICOM 파일. 저장 단위는 검사(Study)다.
/// </summary>
public sealed class LocalDatabase : IDisposable
{
    /// <summary>기본 저장 위치: %AppData%/sh-dicom-studio</summary>
    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "sh-dicom-studio");

    private readonly string _root;
    private readonly SqliteConnection _conn;

    public LocalDatabase(string? root = null)
    {
        _root = root ?? DefaultRoot;
        Directory.CreateDirectory(_root);
        _conn = new SqliteConnection($"Data Source={Path.Combine(_root, "local.db")}");
        _conn.Open();
        Execute("PRAGMA foreign_keys = ON;");
        Execute("""
            CREATE TABLE IF NOT EXISTS Study(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              StudyUid TEXT NOT NULL UNIQUE,
              PatientId TEXT NOT NULL, PatientName TEXT NOT NULL, Sex TEXT NOT NULL,
              Age TEXT NOT NULL, Modality TEXT NOT NULL,
              StudyDate TEXT, BirthDate TEXT,
              StudyDescription TEXT NOT NULL, AccessionNumber TEXT NOT NULL,
              ReferringPhysician TEXT NOT NULL, Comment TEXT NOT NULL,
              Anonymous INTEGER NOT NULL,
              ImageCount INTEGER NOT NULL,
              CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Image(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              StudyId INTEGER NOT NULL REFERENCES Study(Id) ON DELETE CASCADE,
              InstanceNumber INTEGER NOT NULL,
              FilePath TEXT NOT NULL
            );
            """);
    }

    /// <summary>현재 화면의 이미지들을 검사 한 건으로 저장 (DICOM 파일 생성 + 메타 기록).</summary>
    public StudyRecord SaveStudy(ExamInfo info, IReadOnlyList<byte[]> encodedImages)
    {
        var study = new DicomStudy();
        var dir = Path.Combine(_root, "dicom", study.StudyUid);
        Directory.CreateDirectory(dir);

        var paths = new List<string>();
        for (var i = 0; i < encodedImages.Count; i++)
        {
            var path = Path.Combine(dir, $"{i + 1:00000}.dcm");
            study.Create(encodedImages[i], info, i + 1).Save(path);
            paths.Add(path);
        }

        var createdAt = DateTime.Now;
        using var tx = _conn.BeginTransaction();
        var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO Study(StudyUid, PatientId, PatientName, Sex, Age, Modality, StudyDate, BirthDate,
                              StudyDescription, AccessionNumber, ReferringPhysician, Comment, Anonymous,
                              ImageCount, CreatedAt)
            VALUES($uid, $pid, $name, $sex, $age, $mod, $sdate, $bdate, $desc, $acc, $ref, $cmt, $anon, $cnt, $created);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$uid", study.StudyUid);
        cmd.Parameters.AddWithValue("$pid", info.Anonymous ? "ANONYMOUS" : info.PatientId);
        cmd.Parameters.AddWithValue("$name", info.Anonymous ? "ANONYMOUS" : info.PatientName);
        cmd.Parameters.AddWithValue("$sex", info.Sex);
        cmd.Parameters.AddWithValue("$age", info.Age);
        cmd.Parameters.AddWithValue("$mod", info.Modality);
        cmd.Parameters.AddWithValue("$sdate", (object?)ToDateString(info.StudyDate) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$bdate", (object?)ToDateString(info.BirthDate) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$desc", info.StudyDescription);
        cmd.Parameters.AddWithValue("$acc", info.AccessionNumber);
        cmd.Parameters.AddWithValue("$ref", info.ReferringPhysician);
        cmd.Parameters.AddWithValue("$cmt", info.Comment);
        cmd.Parameters.AddWithValue("$anon", info.Anonymous ? 1 : 0);
        cmd.Parameters.AddWithValue("$cnt", encodedImages.Count);
        cmd.Parameters.AddWithValue("$created", createdAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        var studyId = (long)cmd.ExecuteScalar()!;

        for (var i = 0; i < paths.Count; i++)
        {
            var img = _conn.CreateCommand();
            img.Transaction = tx;
            img.CommandText = "INSERT INTO Image(StudyId, InstanceNumber, FilePath) VALUES($sid, $num, $path);";
            img.Parameters.AddWithValue("$sid", studyId);
            img.Parameters.AddWithValue("$num", i + 1);
            img.Parameters.AddWithValue("$path", paths[i]);
            img.ExecuteNonQuery();
        }
        tx.Commit();

        return new StudyRecord(studyId, study.StudyUid, info, encodedImages.Count, createdAt);
    }

    /// <summary>검색 — 비어 있는 조건은 무시된다. 최신 저장이 위.</summary>
    public List<StudyRecord> Search(string? patientId = null, string? patientName = null,
        string? modality = null, DateTime? from = null, DateTime? to = null)
    {
        var cmd = _conn.CreateCommand();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(patientId))
        {
            where.Add("PatientId LIKE $pid");
            cmd.Parameters.AddWithValue("$pid", $"%{patientId.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(patientName))
        {
            where.Add("PatientName LIKE $name");
            cmd.Parameters.AddWithValue("$name", $"%{patientName.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(modality))
        {
            where.Add("Modality = $mod");
            cmd.Parameters.AddWithValue("$mod", modality);
        }
        if (from is { } f)
        {
            where.Add("StudyDate >= $from");
            cmd.Parameters.AddWithValue("$from", ToDateString(f)!);
        }
        if (to is { } t)
        {
            where.Add("StudyDate <= $to");
            cmd.Parameters.AddWithValue("$to", ToDateString(t)!);
        }

        cmd.CommandText = "SELECT Id, StudyUid, PatientId, PatientName, Sex, Age, Modality, StudyDate, BirthDate, "
            + "StudyDescription, AccessionNumber, ReferringPhysician, Comment, Anonymous, ImageCount, CreatedAt FROM Study"
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
            + " ORDER BY CreatedAt DESC";

        var results = new List<StudyRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var info = new ExamInfo
            {
                PatientId = reader.GetString(2),
                PatientName = reader.GetString(3),
                Sex = reader.GetString(4),
                Age = reader.GetString(5),
                Modality = reader.GetString(6),
                StudyDate = FromDateString(reader.IsDBNull(7) ? null : reader.GetString(7)),
                BirthDate = FromDateString(reader.IsDBNull(8) ? null : reader.GetString(8)),
                StudyDescription = reader.GetString(9),
                AccessionNumber = reader.GetString(10),
                ReferringPhysician = reader.GetString(11),
                Comment = reader.GetString(12),
                Anonymous = reader.GetInt64(13) != 0,
            };
            results.Add(new StudyRecord(reader.GetInt64(0), reader.GetString(1), info,
                (int)reader.GetInt64(14),
                DateTime.ParseExact(reader.GetString(15), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
        }
        return results;
    }

    /// <summary>
    /// 기존 검사에 영상 추가 (VPWinGate InsExam) — 같은 Study UID 의 새 Series 로 DICOM 을
    /// 만들고 파일·메타를 이어 붙인다. 추가된 장수를 반환.
    /// </summary>
    public int AppendToStudy(long studyId, IReadOnlyList<byte[]> encodedImages)
    {
        var find = _conn.CreateCommand();
        find.CommandText = "SELECT StudyUid, ImageCount FROM Study WHERE Id = $sid";
        find.Parameters.AddWithValue("$sid", studyId);
        using var found = find.ExecuteReader();
        if (!found.Read()) throw new InvalidOperationException($"검사가 없습니다 (Id={studyId})");
        var studyUid = found.GetString(0);
        var existingCount = (int)found.GetInt64(1);

        // 저장 당시 환자정보를 그대로 헤더에 쓴다 (검사 정보의 SSOT 는 DB 행)
        var info = Search().First(r => r.Id == studyId).Info;

        var study = new DicomStudy(studyUid);
        var dir = Path.Combine(_root, "dicom", studyUid);
        Directory.CreateDirectory(dir);

        using var tx = _conn.BeginTransaction();
        for (var i = 0; i < encodedImages.Count; i++)
        {
            var number = existingCount + i + 1;
            var path = Path.Combine(dir, $"{number:00000}.dcm");
            study.Create(encodedImages[i], info, i + 1).Save(path); // InstanceNumber 는 새 시리즈 내 1..N

            var img = _conn.CreateCommand();
            img.Transaction = tx;
            img.CommandText = "INSERT INTO Image(StudyId, InstanceNumber, FilePath) VALUES($sid, $num, $path);";
            img.Parameters.AddWithValue("$sid", studyId);
            img.Parameters.AddWithValue("$num", number);
            img.Parameters.AddWithValue("$path", path);
            img.ExecuteNonQuery();
        }

        var upd = _conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = "UPDATE Study SET ImageCount = ImageCount + $n WHERE Id = $sid";
        upd.Parameters.AddWithValue("$n", encodedImages.Count);
        upd.Parameters.AddWithValue("$sid", studyId);
        upd.ExecuteNonQuery();
        tx.Commit();

        return encodedImages.Count;
    }

    /// <summary>
    /// 검사를 현재 화면 상태로 덮어쓴다 — 이미지 구성(삭제·순서·편집)과 환자정보가 통째로
    /// 반영된다. Study UID 는 유지(같은 검사), Series 는 새로 발급된다.
    /// </summary>
    public void UpdateStudy(long studyId, ExamInfo info, IReadOnlyList<byte[]> encodedImages)
    {
        var find = _conn.CreateCommand();
        find.CommandText = "SELECT StudyUid FROM Study WHERE Id = $sid";
        find.Parameters.AddWithValue("$sid", studyId);
        if (find.ExecuteScalar() is not string studyUid)
            throw new InvalidOperationException($"검사가 없습니다 (Id={studyId})");

        // 파일 재생성 (같은 Study UID, 새 Series)
        var dir = Path.Combine(_root, "dicom", studyUid);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);

        var study = new DicomStudy(studyUid);
        var paths = new List<string>();
        for (var i = 0; i < encodedImages.Count; i++)
        {
            var path = Path.Combine(dir, $"{i + 1:00000}.dcm");
            study.Create(encodedImages[i], info, i + 1).Save(path);
            paths.Add(path);
        }

        using var tx = _conn.BeginTransaction();
        var clear = _conn.CreateCommand();
        clear.Transaction = tx;
        clear.CommandText = "DELETE FROM Image WHERE StudyId = $sid";
        clear.Parameters.AddWithValue("$sid", studyId);
        clear.ExecuteNonQuery();

        var upd = _conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = """
            UPDATE Study SET PatientId=$pid, PatientName=$name, Sex=$sex, Age=$age, Modality=$mod,
                             StudyDate=$sdate, BirthDate=$bdate, StudyDescription=$desc,
                             AccessionNumber=$acc, ReferringPhysician=$ref, Comment=$cmt,
                             Anonymous=$anon, ImageCount=$cnt
            WHERE Id=$sid
            """;
        upd.Parameters.AddWithValue("$pid", info.Anonymous ? "ANONYMOUS" : info.PatientId);
        upd.Parameters.AddWithValue("$name", info.Anonymous ? "ANONYMOUS" : info.PatientName);
        upd.Parameters.AddWithValue("$sex", info.Sex);
        upd.Parameters.AddWithValue("$age", info.Age);
        upd.Parameters.AddWithValue("$mod", info.Modality);
        upd.Parameters.AddWithValue("$sdate", (object?)ToDateString(info.StudyDate) ?? DBNull.Value);
        upd.Parameters.AddWithValue("$bdate", (object?)ToDateString(info.BirthDate) ?? DBNull.Value);
        upd.Parameters.AddWithValue("$desc", info.StudyDescription);
        upd.Parameters.AddWithValue("$acc", info.AccessionNumber);
        upd.Parameters.AddWithValue("$ref", info.ReferringPhysician);
        upd.Parameters.AddWithValue("$cmt", info.Comment);
        upd.Parameters.AddWithValue("$anon", info.Anonymous ? 1 : 0);
        upd.Parameters.AddWithValue("$cnt", encodedImages.Count);
        upd.Parameters.AddWithValue("$sid", studyId);
        upd.ExecuteNonQuery();

        for (var i = 0; i < paths.Count; i++)
        {
            var img = _conn.CreateCommand();
            img.Transaction = tx;
            img.CommandText = "INSERT INTO Image(StudyId, InstanceNumber, FilePath) VALUES($sid, $num, $path);";
            img.Parameters.AddWithValue("$sid", studyId);
            img.Parameters.AddWithValue("$num", i + 1);
            img.Parameters.AddWithValue("$path", paths[i]);
            img.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>검사의 DICOM 파일 경로들 (InstanceNumber 순).</summary>
    public List<string> GetImagePaths(long studyId)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT FilePath FROM Image WHERE StudyId = $sid ORDER BY InstanceNumber";
        cmd.Parameters.AddWithValue("$sid", studyId);
        var paths = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) paths.Add(reader.GetString(0));
        return paths;
    }

    /// <summary>검사 삭제 — 메타(cascade)와 DICOM 파일 폴더를 함께 지운다. 복원 불가.</summary>
    public void DeleteStudy(long studyId)
    {
        var uidCmd = _conn.CreateCommand();
        uidCmd.CommandText = "SELECT StudyUid FROM Study WHERE Id = $sid";
        uidCmd.Parameters.AddWithValue("$sid", studyId);
        if (uidCmd.ExecuteScalar() is not string uid) return;

        var del = _conn.CreateCommand();
        del.CommandText = "DELETE FROM Study WHERE Id = $sid";
        del.Parameters.AddWithValue("$sid", studyId);
        del.ExecuteNonQuery();

        var dir = Path.Combine(_root, "dicom", uid);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private void Execute(string sql)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string? ToDateString(DateTime? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime? FromDateString(string? s) =>
        s is null ? null : DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public void Dispose() => _conn.Dispose();
}
