using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using ShDicomStudio.Core.Imaging;

namespace ShDicomStudio.Core.Dicom;

/// <summary>PACS 목적지 (PPW "검사 보내기"의 remote host 한 줄).</summary>
public sealed record PacsNode(string Name, string AeTitle, string Host, int Port);

/// <summary>
/// DICOM 네트워크 전송 (Storage SCU) — C-ECHO(연결 테스트)와 C-STORE(검사 보내기).
/// 우리 쪽 AE Title 은 SHDICOM. 압축 없이 원본 그대로 보낸다 (Keep Original).
/// </summary>
public static class DicomSender
{
    public const string CallingAeTitle = "SHDICOM";

    /// <summary>연결 테스트 (C-ECHO) — PACS 가 응답하면 true.</summary>
    public static async Task<bool> EchoAsync(PacsNode node)
    {
        DicomRuntime.EnsureInitialized();
        var success = false;

        var client = DicomClientFactory.Create(node.Host, node.Port, useTls: false,
            CallingAeTitle, node.AeTitle);
        var request = new DicomCEchoRequest();
        request.OnResponseReceived += (_, response) =>
            success = response.Status == DicomStatus.Success;

        await client.AddRequestAsync(request);
        await client.SendAsync();
        return success;
    }

    /// <summary>DICOM 파일들을 C-STORE 로 전송 — 성공한 장수를 반환.</summary>
    public static async Task<int> SendAsync(PacsNode node, IReadOnlyList<string> dcmPaths)
    {
        DicomRuntime.EnsureInitialized();
        var succeeded = 0;

        var client = DicomClientFactory.Create(node.Host, node.Port, useTls: false,
            CallingAeTitle, node.AeTitle);
        foreach (var path in dcmPaths)
        {
            var request = new DicomCStoreRequest(path);
            request.OnResponseReceived += (_, response) =>
            {
                if (response.Status == DicomStatus.Success)
                    Interlocked.Increment(ref succeeded);
            };
            await client.AddRequestAsync(request);
        }

        await client.SendAsync();
        return succeeded;
    }
}
