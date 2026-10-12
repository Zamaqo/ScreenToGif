using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ScreenToGif.Domain.Interfaces;
using ScreenToGif.Util;
using ScreenToGif.ViewModel.UploadPresets.Cloudflare;
using ScreenToGif.ViewModel.UploadPresets.History;

namespace ScreenToGif.Cloud;

public class CloudflareR2 : IUploader
{
    public const string SampleFileName = "screentogif-sample.png";

    private const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public async Task<IHistory> UploadFileAsync(IUploadPreset preset, string path, CancellationToken cancellationToken, IProgress<double> progressCallback = null)
    {
        if (preset is not CloudflareR2Preset cloudflare)
            throw new UploadException("Cloudflare R2 preset is missing.");

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new ArgumentException("File to upload was not found.", nameof(path));

        var objectKey = BuildObjectKey(cloudflare.Prefix, Path.GetFileName(path));
        var publicUrl = BuildPublicUrl(cloudflare.PublicBaseUrl, objectKey);
        var payloadHash = await HashFileAsync(path, cancellationToken);

        await using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            var result = await SendSignedAsync(cloudflare, HttpMethod.Put, objectKey, fileStream, payloadHash, GetContentType(path), TimeSpan.FromMinutes(30), cancellationToken);

            if (!IsSuccess(result.StatusCode))
                throw new UploadException("Cloudflare R2 returned " + result.StatusCode + ": " + TrimResponse(result.Body));
        }

        return new History
        {
            Type = preset.Type,
            PresetName = preset.Title,
            DateInUtc = DateTime.UtcNow,
            Result = 200,
            Size = new FileInfo(path).Length,
            Link = publicUrl
        };
    }

    /// <summary>
    /// Uploads a red sample image and leaves it in the bucket so it can be viewed online.
    /// </summary>
    public static async Task<string> UploadSampleAsync(CloudflareR2Preset preset, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var objectKey = SampleKey(preset);
        var image = CreateSolidPng(220, 40, 40);

        progress?.Report(LocalizationHelper.Get("S.Options.Upload.Preset.CloudflareR2.Test.Uploading"));
        await PutBytesAsync(preset, objectKey, image, cancellationToken);

        progress?.Report(LocalizationHelper.Get("S.Options.Upload.Preset.CloudflareR2.Test.Reading"));
        await ExpectStoredBytesAsync(preset, objectKey, image, cancellationToken);

        progress?.Report(LocalizationHelper.Get("S.Options.Upload.Preset.CloudflareR2.Test.Public"));
        await ExpectPublicBytesAsync(preset, objectKey, image, cancellationToken);

        return BuildPublicUrl(preset.PublicBaseUrl, objectKey);
    }

    /// <summary>
    /// Replaces the sample image with a blue one. The previous sample must already be in the bucket.
    /// </summary>
    public static async Task<string> ReplaceSampleAsync(CloudflareR2Preset preset, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var objectKey = SampleKey(preset);
        await RequireSampleAsync(preset, objectKey, cancellationToken);

        var image = CreateSolidPng(40, 90, 220);
        progress?.Report(LocalizationHelper.Get("S.Options.Upload.Preset.CloudflareR2.Test.Replacing"));
        await PutBytesAsync(preset, objectKey, image, cancellationToken);
        await ExpectStoredBytesAsync(preset, objectKey, image, cancellationToken);

        progress?.Report(LocalizationHelper.Get("S.Options.Upload.Preset.CloudflareR2.Test.Public"));
        await ExpectPublicBytesAsync(preset, objectKey, image, cancellationToken);

        return BuildPublicUrl(preset.PublicBaseUrl, objectKey);
    }

    /// <summary>
    /// Removes the sample image from the bucket.
    /// </summary>
    public static async Task DeleteSampleAsync(CloudflareR2Preset preset, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var objectKey = SampleKey(preset);
        await RequireSampleAsync(preset, objectKey, cancellationToken);

        progress?.Report(LocalizationHelper.Get("S.Options.Upload.Preset.CloudflareR2.Test.Deleting"));
        await DeleteAsync(preset, objectKey, cancellationToken);

        var afterDelete = await SendSignedAsync(preset, HttpMethod.Get, objectKey, null, EmptyPayloadHash, null, TimeSpan.FromMinutes(1), cancellationToken);

        if (afterDelete.StatusCode != 404)
            throw new UploadException("The sample image is still in the bucket after delete (HTTP " + afterDelete.StatusCode + ").");
    }

    public static string SampleKey(CloudflareR2Preset preset)
    {
        return BuildObjectKey(preset.Prefix, SampleFileName);
    }

    public static bool IsAuthorized(CloudflareR2Preset preset)
    {
        return preset != null
               && !string.IsNullOrWhiteSpace(preset.AccountId)
               && !string.IsNullOrWhiteSpace(preset.AccessKeyId)
               && !string.IsNullOrWhiteSpace(preset.SecretAccessKey)
               && !string.IsNullOrWhiteSpace(preset.Bucket)
               && !string.IsNullOrWhiteSpace(preset.PublicBaseUrl);
    }

    public static string BuildHost(string accountOrEndpoint)
    {
        var value = (accountOrEndpoint ?? "").Trim();

        if (value.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint))
                throw new UploadException("The S3 endpoint is not a valid address.");

            value = endpoint.Host;
        }

        value = value.Split('/')[0].Trim().Trim('.').ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(value))
            throw new UploadException("The S3 endpoint is not a valid address.");

        if (value.EndsWith(".r2.cloudflarestorage.com", StringComparison.Ordinal))
            return value;

        if (value.Contains('.'))
            throw new UploadException("The S3 endpoint is not a valid address.");

        return value + ".r2.cloudflarestorage.com";
    }

    public static string BuildObjectKey(string prefix, string fileName)
    {
        var name = Path.GetFileName(fileName ?? "");

        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
            throw new UploadException("File name is empty.");

        var folder = (prefix ?? "").Replace('\\', '/').Trim().Trim('/');
        var segments = string.IsNullOrEmpty(folder)
            ? new[] { name }
            : folder.Split('/', StringSplitOptions.RemoveEmptyEntries).Append(name).ToArray();

        if (segments.Any(segment => segment is "." or ".."))
            throw new UploadException("Folder cannot contain '.' or '..'.");

        return string.Join("/", segments);
    }

    public static string EncodePath(string path)
    {
        return string.Join("/", (path ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    public static string BuildPublicUrl(string publicBaseUrl, string objectKey)
    {
        var baseUrl = (publicBaseUrl ?? "").Trim().TrimEnd('/');

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new UploadException("Public URL must be an http or https address.");

        return baseUrl + "/" + EncodePath(objectKey);
    }

    public static string GetContentType(string path)
    {
        switch (Path.GetExtension(path)?.ToLowerInvariant())
        {
            case ".gif":
                return "image/gif";
            case ".png":
            case ".apng":
                return "image/png";
            case ".jpg":
            case ".jpeg":
                return "image/jpeg";
            case ".webp":
                return "image/webp";
            case ".avif":
                return "image/avif";
            case ".bmp":
                return "image/bmp";
            case ".mp4":
                return "video/mp4";
            case ".webm":
                return "video/webm";
            case ".avi":
                return "video/x-msvideo";
            case ".mov":
                return "video/quicktime";
            case ".mkv":
                return "video/x-matroska";
            case ".zip":
            case ".stg":
                return "application/zip";
            case ".psd":
                return "image/vnd.adobe.photoshop";
            default:
                return "application/octet-stream";
        }
    }

    private static async Task PutBytesAsync(CloudflareR2Preset preset, string objectKey, byte[] body, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(body, writable: false);
        var hash = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
        var result = await SendSignedAsync(preset, HttpMethod.Put, objectKey, stream, hash, "image/png", TimeSpan.FromMinutes(1), cancellationToken);

        if (!IsSuccess(result.StatusCode))
            throw new UploadException("Cloudflare R2 returned " + result.StatusCode + ": " + TrimResponse(result.Body));
    }

    private static async Task<byte[]> GetBytesAsync(CloudflareR2Preset preset, string objectKey, CancellationToken cancellationToken)
    {
        var result = await SendSignedAsync(preset, HttpMethod.Get, objectKey, null, EmptyPayloadHash, null, TimeSpan.FromMinutes(1), cancellationToken);

        if (!IsSuccess(result.StatusCode))
            throw new UploadException("Cloudflare R2 returned " + result.StatusCode + " while reading the test image: " + TrimResponse(result.Body));

        return result.Body;
    }

    private static async Task DeleteAsync(CloudflareR2Preset preset, string objectKey, CancellationToken cancellationToken)
    {
        var result = await SendSignedAsync(preset, HttpMethod.Delete, objectKey, null, EmptyPayloadHash, null, TimeSpan.FromMinutes(1), cancellationToken);

        if (!IsSuccess(result.StatusCode))
            throw new UploadException("Cloudflare R2 returned " + result.StatusCode + " while deleting the test image: " + TrimResponse(result.Body));
    }

    private static async Task RequireSampleAsync(CloudflareR2Preset preset, string objectKey, CancellationToken cancellationToken)
    {
        var existing = await SendSignedAsync(preset, HttpMethod.Get, objectKey, null, EmptyPayloadHash, null, TimeSpan.FromMinutes(1), cancellationToken);

        if (existing.StatusCode == 404)
            throw new UploadException("No sample image was found. Upload the sample first. The file name is " + objectKey + ".");

        if (!IsSuccess(existing.StatusCode))
            throw new UploadException("Cloudflare R2 returned " + existing.StatusCode + " while looking for the sample image: " + TrimResponse(existing.Body));
    }

    private static async Task ExpectStoredBytesAsync(CloudflareR2Preset preset, string objectKey, byte[] expected, CancellationToken cancellationToken)
    {
        var downloaded = await GetBytesAsync(preset, objectKey, cancellationToken);

        if (!downloaded.AsSpan().SequenceEqual(expected))
            throw new UploadException("The image read back from the bucket does not match the sample that was sent.");
    }

    private static async Task ExpectPublicBytesAsync(CloudflareR2Preset preset, string objectKey, byte[] expected, CancellationToken cancellationToken)
    {
        var address = BuildPublicUrl(preset.PublicBaseUrl, objectKey);
        var handler = new HttpClientHandler
        {
            Proxy = WebHelper.GetProxy()
        };

        using (var client = new HttpClient(handler))
        {
            client.Timeout = TimeSpan.FromSeconds(20);

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                using (var response = await client.GetAsync(address + "?screentogif=" + DateTime.UtcNow.Ticks, cancellationToken))
                {
                    var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                    if (response.IsSuccessStatusCode && body.AsSpan().SequenceEqual(expected))
                        return;

                    if (attempt == 3)
                        throw new UploadException("The public URL returned HTTP " + (int)response.StatusCode + ". The S3 endpoint accepted the upload, but this link did not serve the test image. Check that the bucket is public, or that the custom domain is attached.");
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private static async Task<(int StatusCode, byte[] Body)> SendSignedAsync(CloudflareR2Preset preset, HttpMethod method, string objectKey, Stream body, string payloadHash, string contentType, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var host = BuildHost(preset.AccountId);
        var bucket = (preset.Bucket ?? "").Trim().ToLowerInvariant();
        var canonicalUri = "/" + EncodePath(bucket + "/" + objectKey);
        var moment = DateTime.UtcNow;
        var dateStamp = moment.ToString("yyyyMMdd");
        var timestamp = moment.ToString("yyyyMMdd'T'HHmmss'Z'");
        var headers = new Dictionary<string, string>
        {
            ["host"] = host,
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = timestamp
        };

        if (!string.IsNullOrEmpty(contentType))
            headers["content-type"] = contentType;

        var authorization = AwsSigV4.BuildAuthorization(method.Method, canonicalUri, "", headers, payloadHash, preset.AccessKeyId.Trim(), preset.SecretAccessKey.Trim(), dateStamp, "auto", "s3");
        var handler = new HttpClientHandler
        {
            Proxy = WebHelper.GetProxy(),
            PreAuthenticate = true,
            UseDefaultCredentials = false
        };

        using (var client = new HttpClient(handler))
        {
            client.Timeout = timeout;
            client.DefaultRequestHeaders.ExpectContinue = false;

            var request = new HttpRequestMessage(method, "https://" + host + canonicalUri);
            request.Headers.Host = host;
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
            request.Headers.TryAddWithoutValidation("x-amz-date", timestamp);
            request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);

            if (body != null)
            {
                request.Content = new StreamContent(body);
                request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }

            using (var response = await client.SendAsync(request, cancellationToken))
            {
                var responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                return ((int)response.StatusCode, responseBody);
            }
        }
    }

    private static bool IsSuccess(int statusCode)
    {
        return statusCode >= 200 && statusCode <= 299;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string TrimResponse(byte[] body)
    {
        if (body == null || body.Length == 0)
            return "No details were returned.";

        var compact = Encoding.UTF8.GetString(body).Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= 500 ? compact : compact.Substring(0, 500);
    }

    private static byte[] CreateSolidPng(byte red, byte green, byte blue)
    {
        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        var header = new byte[] { 0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0 };
        var scanline = new byte[] { 0, red, green, blue };

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(scanline, 0, scanline.Length);

        using var png = new MemoryStream();
        png.Write(signature, 0, signature.Length);
        WritePngChunk(png, "IHDR", header);
        WritePngChunk(png, "IDAT", compressed.ToArray());
        WritePngChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WritePngChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var length = BitConverter.GetBytes(data.Length);

        if (BitConverter.IsLittleEndian)
            Array.Reverse(length);

        stream.Write(length, 0, length.Length);
        stream.Write(typeBytes, 0, typeBytes.Length);
        stream.Write(data, 0, data.Length);

        var crcSource = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcSource, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, crcSource, typeBytes.Length, data.Length);

        var crc = BitConverter.GetBytes(Crc32(crcSource));

        if (BitConverter.IsLittleEndian)
            Array.Reverse(crc);

        stream.Write(crc, 0, crc.Length);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFF;

        foreach (var value in data)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }

        return crc ^ 0xFFFFFFFF;
    }
}
