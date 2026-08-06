using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ShDicomStudio.Core.Dicom;

namespace ShDicomStudio.App.Services;

public sealed class PacsConfig
{
    public List<PacsNode> Nodes { get; set; } = [];
}

/// <summary>검사 보내기 목적지(PACS) 목록 — %AppData%/sh-dicom-studio/pacs.json.</summary>
public static class PacsConfigStore
{
    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "sh-dicom-studio", "pacs.json");

    public static PacsConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath)
                && JsonSerializer.Deserialize<PacsConfig>(File.ReadAllText(ConfigPath)) is { } config
                && config.Nodes.Count > 0)
                return config;
        }
        catch
        {
            // 손상된 설정은 기본값으로
        }

        return new PacsConfig
        {
            Nodes = [new PacsNode("Orthanc (도커 테스트)", "ORTHANC", "localhost", 4242)],
        };
    }

    public static void Save(PacsConfig config)
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
}
