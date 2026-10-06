using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Shared;

namespace WriterApp.Client.Services;

internal sealed partial class CoverApiClient
{
    public string Backend => _http.BaseAddress?.AbsoluteUri ?? throw new InvalidOperationException("Cover backend unavailable.");
    public async Task<T> EditCallAsync<T>(HttpMethod method,string path,object? input,CancellationToken ct,int limit=4*1024*1024) {
        using var request=new HttpRequestMessage(method,path) {Content=input is null ? null : JsonContent.Create(input)};
        using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(!response.IsSuccessStatusCode) {
            var error=await WriterApp.Client.Utilities.ApiErrorDetailsReader.ReadAsync(response);
            throw new InvalidOperationException(error?.UserMessage ?? $"Cover edit failed ({(int)response.StatusCode}). The saved cover is preserved.");
        }
        if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Cover edit response exceeds the safe limit.");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);using var buffer=new MemoryStream();byte[] block=new byte[16384];int read;
        while((read=await stream.ReadAsync(block,ct))>0){if(buffer.Length+read>limit)throw new InvalidDataException("Cover edit response exceeds the safe limit.");await buffer.WriteAsync(block.AsMemory(0,read),ct);}
        ct.ThrowIfCancellationRequested();return JsonSerializer.Deserialize<T>(buffer.ToArray(),new JsonSerializerOptions(JsonSerializerDefaults.Web){MaxDepth=16}) ?? throw new InvalidDataException("Empty cover edit response.");
    }
    public Task<CoverEditCapabilities> EditCapabilitiesAsync(CancellationToken ct)=>EditCallAsync<CoverEditCapabilities>(HttpMethod.Get,"api/covers/edit-capabilities",null,ct,16_384);
    public Task<CoverAssetResponse> EditSourceAsync(CoverEditSourceRequest input,CancellationToken ct)=>EditCallAsync<CoverAssetResponse>(HttpMethod.Post,"api/covers/edit-source",input,ct);
    public async Task<CoverEditResponse> EditAsync(CoverEditRequest input,CancellationToken ct) {
        CoverEdits.Validate(input);var result=await EditCallAsync<CoverEditResponse>(HttpMethod.Post,"api/covers/edit",input,ct);CoverEdits.ValidateResponse(input,result);return result;
    }
    public Task<CoverEditSaveReceipt> SaveEditAsync(CoverEditSaveRequest input,CancellationToken ct)=>EditCallAsync<CoverEditSaveReceipt>(HttpMethod.Post,"api/covers/edit-save",input,ct,6*1024*1024);
    public Task<CoverEditSaveReceipt> ReadEditSaveAsync(Guid operation,CancellationToken ct)=>EditCallAsync<CoverEditSaveReceipt>(HttpMethod.Get,$"api/covers/edit-save/{operation}",null,ct,6*1024*1024);
    public Task<CoverEditSaveReceipt> RestoreEditAsync(Guid operation,long revision,CancellationToken ct)=>EditCallAsync<CoverEditSaveReceipt>(HttpMethod.Post,$"api/covers/edit-save/{operation}/restore",new CoverEditRestoreRequest(revision),ct,6*1024*1024);
}
