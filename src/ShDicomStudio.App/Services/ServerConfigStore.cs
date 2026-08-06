using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShDicomStudio.App.Services;

public sealed record ServerEntry(string Name, string Url);

public sealed class ServerConfig
{
    public List<ServerEntry> Servers { get; set; } = [];
    public string? LastSelected { get; set; }
}

/// <summary>
/// 로그인 창의 서버 목록 설정 — %AppData%/sh-dicom-studio/servers.json 에 저장.
/// (PPW 의 "DB Config" 대응: 서버를 이름으로 등록해 두고 골라 쓴다.)
/// </summary>
public static class ServerConfigStore
{
    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "sh-dicom-studio", "servers.json");

    public static ServerConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath)
                && JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(ConfigPath)) is { } config
                && config.Servers.Count > 0)
                return config;
        }
        catch
        {
            // 손상된 설정은 기본값으로 재생성
        }

        return new ServerConfig
        {
            Servers = [new ServerEntry("로컬 서버", "http://localhost:8080")],
            LastSelected = "로컬 서버",
        };
    }

    public static void Save(ServerConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
        }
    }

    public static ServerEntry? Find(ServerConfig config, string? name) =>
        config.Servers.FirstOrDefault(s => s.Name == name);
}
