using System.Text.Json;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalMaterializedCover(int Version,string Scope,Guid ProjectId,Guid CloudProjectId,CoverAssetIdentity Asset,byte[] Bytes);

public sealed partial class LocalCoverStudioStore
{
    private string AssetPath(string scope,Guid project,string reference) {
        _=PathFor(scope,project);
        return Path.Combine(root,scope,"assets",project.ToString("N"),CoverAssetContract.ReferenceHash(reference)+".json");
    }
    private static void ValidateAsset(LocalMaterializedCover value) {
        if(value.Version!=1 || value.ProjectId==Guid.Empty || value.CloudProjectId==Guid.Empty || value.Scope is not {Length:64} || !value.Scope.All(Uri.IsHexDigit))
            throw new InvalidDataException("Unsupported materialized cover cache. Its file is preserved.");
        CoverAssetContract.Validate(value.Asset,value.Bytes,value.CloudProjectId);
    }
    public async Task<LocalMaterializedCover?> ReadAssetAsync(string scope,LocalDocument doc,CancellationToken ct=default) {
        if(doc.Project is not {ServerProjectId:{ } cloud,CoverImageUrl:{ } reference} project)return null;
        string path=AssetPath(scope,project.ProjectId,reference);if(!File.Exists(path))return null;
        if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Materialized cover cache exceeds 4 MB. Its file is preserved.");
        var value=JsonSerializer.Deserialize<LocalMaterializedCover>(await File.ReadAllBytesAsync(path,ct),Json) ?? throw new InvalidDataException("Invalid materialized cover cache.");
        ValidateAsset(value);
        if(value.Scope!=scope || value.ProjectId!=project.ProjectId || value.CloudProjectId!=cloud || value.Asset.RemoteReference!=reference)
            throw new InvalidDataException("Materialized cover belongs to another account, backend or project.");
        return value;
    }
    public async Task WriteAssetAsync(LocalMaterializedCover value,CancellationToken ct=default) {
        ValidateAsset(value);string path=AssetPath(value.Scope,value.ProjectId,value.Asset.RemoteReference);
        await gate.WaitAsync(ct);
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            if(File.Exists(path)) {
                if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Existing cover cache is oversized. Its file is preserved.");
                var previous=JsonSerializer.Deserialize<LocalMaterializedCover>(await File.ReadAllBytesAsync(path,ct),Json);
                if(previous is null)throw new InvalidDataException("Invalid existing cover cache. Its file is preserved.");
                ValidateAsset(previous);
                if(previous.Scope!=value.Scope || previous.ProjectId!=value.ProjectId || previous.CloudProjectId!=value.CloudProjectId
                    || previous.Asset!=value.Asset || !previous.Bytes.SequenceEqual(value.Bytes))
                    throw new InvalidDataException("An immutable cover identity changed. Existing cache is preserved; reopen and refresh the owned source.");
                return;
            }
            await new AtomicDocumentWriter().WriteAsync(path,JsonSerializer.SerializeToUtf8Bytes(value,Json),ct);
        } finally { gate.Release(); }
    }
}
