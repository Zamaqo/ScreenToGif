using System.Runtime.Serialization;
using ScreenToGif.Domain.Enums;

namespace ScreenToGif.ViewModel.UploadPresets.Cloudflare;

public class CloudflareR2Preset : UploadPreset
{
    private string _accountId = "";
    private string _accessKeyId = "";
    private string _secretAccessKey = "";
    private string _bucket = "";
    private string _prefix = "";
    private string _publicBaseUrl = "";

    [DataMember(EmitDefaultValue = false)]
    public string AccountId
    {
        get => _accountId;
        set => SetProperty(ref _accountId, value);
    }

    [DataMember(EmitDefaultValue = false)]
    public string AccessKeyId
    {
        get => _accessKeyId;
        set => SetProperty(ref _accessKeyId, value);
    }

    [DataMember(EmitDefaultValue = false)]
    public string SecretAccessKey
    {
        get => _secretAccessKey;
        set => SetProperty(ref _secretAccessKey, value);
    }

    [DataMember(EmitDefaultValue = false)]
    public string Bucket
    {
        get => _bucket;
        set => SetProperty(ref _bucket, value);
    }

    /// <summary>
    /// Optional folder inside the bucket. Stored without leading or trailing slashes.
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public string Prefix
    {
        get => _prefix;
        set => SetProperty(ref _prefix, value);
    }

    /// <summary>
    /// Public address of the bucket (r2.dev subdomain or a custom domain). The copied link is this address plus the object name.
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public string PublicBaseUrl
    {
        get => _publicBaseUrl;
        set => SetProperty(ref _publicBaseUrl, value);
    }

    public CloudflareR2Preset()
    {
        Type = UploadDestinations.CloudflareR2;
        ImageId = "Vector.CloudflareR2";
        AllowedTypes = new List<ExportFormats>();
    }
}
