using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ScreenToGif.Domain.Interfaces;
using ScreenToGif.Domain.Models.Upload.YandexDisk;
using ScreenToGif.Util;
using ScreenToGif.Util.Settings;
using ScreenToGif.ViewModel.UploadPresets.History;
using ScreenToGif.ViewModel.UploadPresets.Yandex;

namespace ScreenToGif.Cloud;

public class YandexDisk : IUploader
{
    public async Task<IHistory> UploadFileAsync(IUploadPreset preset, string path, CancellationToken cancellationToken, IProgress<double> progressCallback = null)
    {
        if (string.IsNullOrEmpty(path)) 
            throw new ArgumentException(nameof(path));

        var fileName = Path.GetFileName(path);

        var link = await GetAsync<Link>(preset as YandexPreset, "https://cloud-api.yandex.net/v1/disk/resources/upload?path=app:/" + fileName + "&overwrite=true", cancellationToken);
            
        if (string.IsNullOrEmpty(link?.Href)) 
            throw new UploadException("Unknown error");

        await using (var fileSteram = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            await PutAsync(preset as YandexPreset, link.Href, new StreamContent(fileSteram), cancellationToken);
        }

        var downloadLink = await GetAsync<Link>(preset as YandexPreset, "https://cloud-api.yandex.net/v1/disk/resources/download?path=app:/" + fileName, cancellationToken);

        var history = new History
        {
            Type = preset.Type,
            PresetName = preset.Title,
            DateInUtc = DateTime.UtcNow,
            Result = 200,
            Link = downloadLink.Href
        };
            
        return history;
    }

    private async Task<T> GetAsync<T>(YandexPreset preset, string url, CancellationToken cancellationToken)
    {
        var handler = new HttpClientHandler
        {
            Proxy = WebHelper.GetProxy(),
            PreAuthenticate = true,
            UseDefaultCredentials = false,
        };

        using (var client = new HttpClient(handler))
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers =
                {
                    {HttpRequestHeader.Authorization.ToString(), "OAuth " + preset.OAuthToken}
                }
            };

            string responseBody;
            using (var response = await client.SendAsync(request, cancellationToken))
            {
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            }
                
            var errorDescriptor = Serializer.Deserialize<ErrorDescriptor>(responseBody);

            if (errorDescriptor.Error != null)
                throw new UploadException($"{errorDescriptor.Error}, {errorDescriptor.Message}, {errorDescriptor.Description}");

            return Serializer.Deserialize<T>(responseBody);
        }
    }

    private async Task PutAsync(YandexPreset preset, string url, HttpContent content, CancellationToken cancellationToken)
    {
        var handler = new HttpClientHandler
        {
            Proxy = WebHelper.GetProxy(),
            PreAuthenticate = true,
            UseDefaultCredentials = false,
        };

        using (var client = new HttpClient(handler))
        {
            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Headers =
                {
                    {HttpRequestHeader.Authorization.ToString(), "OAuth " + preset.OAuthToken}
                },
                Content = content
            };

            using (await client.SendAsync(request, cancellationToken))
            { }
        }
    }

    public static string GetAuthorizationAdress()
    {
        var args = new Dictionary<string, string>
        {
            {"client_id", Secret.YandexId},
            {"response_type", "token"}
        };

        return WebHelper.AppendQuery($"https://oauth.yandex.{(UserSettings.All.LanguageCode.StartsWith("ru") ? "ru" : "com")}/authorize", args);
    }

    public static bool IsAuthorized(YandexPreset preset)
    {
        return !string.IsNullOrWhiteSpace(preset.OAuthToken);
    }

    /// <summary>
    /// Lists one folder on the app's Yandex Disk directory.
    /// </summary>
    public static async Task<IReadOnlyList<CloudEntry>> ListFolderAsync(YandexPreset preset, string diskPath, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(diskPath) ? "app:/" : diskPath;
        var resource = await GetResourceAsync(preset, "https://cloud-api.yandex.net/v1/disk/resources?limit=200&path=" + Uri.EscapeDataString(path), cancellationToken);
        var items = resource?.Embedded?.Items ?? new List<YandexResource>();

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item?.Name))
            .Select(item => new CloudEntry
            {
                Name = item.Name,
                Key = item.Path,
                IsFolder = string.Equals(item.Type, "dir", StringComparison.OrdinalIgnoreCase),
                Size = item.Size,
                SizeLabel = string.Equals(item.Type, "dir", StringComparison.OrdinalIgnoreCase) ? "" : CloudEntry.FormatSize(item.Size),
                ModifiedLabel = CloudEntry.FormatModified(item.Modified)
            })
            .OrderByDescending(entry => entry.IsFolder)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static async Task<bool> ExistsAsync(YandexPreset preset, string diskPath, CancellationToken cancellationToken)
    {
        var url = "https://cloud-api.yandex.net/v1/disk/resources?path=" + Uri.EscapeDataString(diskPath);
        var handler = new HttpClientHandler
        {
            Proxy = WebHelper.GetProxy(),
            PreAuthenticate = true,
            UseDefaultCredentials = false
        };

        using (var client = new HttpClient(handler))
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers =
                {
                    { HttpRequestHeader.Authorization.ToString(), "OAuth " + preset.OAuthToken }
                }
            };

            using (var response = await client.SendAsync(request, cancellationToken))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return false;

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorDescriptor = Serializer.Deserialize<ErrorDescriptor>(responseBody);

                if (errorDescriptor?.Error != null)
                    throw new UploadException(errorDescriptor.Error + ", " + errorDescriptor.Message + ", " + errorDescriptor.Description);

                return response.IsSuccessStatusCode;
            }
        }
    }

    public static async Task DownloadAsync(YandexPreset preset, string diskPath, string destinationPath, CancellationToken cancellationToken)
    {
        var link = await GetLinkAsync(preset, "https://cloud-api.yandex.net/v1/disk/resources/download?path=" + Uri.EscapeDataString(diskPath), cancellationToken);

        if (string.IsNullOrWhiteSpace(link?.Href))
            throw new UploadException("Yandex Disk did not return a download link.");

        var directory = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var handler = new HttpClientHandler
        {
            Proxy = WebHelper.GetProxy()
        };

        using (var client = new HttpClient(handler))
        {
            client.Timeout = TimeSpan.FromMinutes(30);

            using (var response = await client.GetAsync(link.Href, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    throw new UploadException("Yandex Disk returned " + (int)response.StatusCode + " while downloading.");

                await using (var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    await response.Content.CopyToAsync(file, cancellationToken);
            }
        }
    }

    public static string ParentDiskPath(string diskPath)
    {
        var trimmed = (diskPath ?? "app:/").TrimEnd('/');
        var index = trimmed.LastIndexOf('/');

        if (index <= "app:".Length)
            return "app:/";

        return trimmed.Substring(0, index);
    }

    private static async Task<YandexResource> GetResourceAsync(YandexPreset preset, string url, CancellationToken cancellationToken)
    {
        return Serializer.Deserialize<YandexResource>(await GetAuthorizedAsync(preset, url, cancellationToken));
    }

    private static async Task<Link> GetLinkAsync(YandexPreset preset, string url, CancellationToken cancellationToken)
    {
        return Serializer.Deserialize<Link>(await GetAuthorizedAsync(preset, url, cancellationToken));
    }

    private static async Task<string> GetAuthorizedAsync(YandexPreset preset, string url, CancellationToken cancellationToken)
    {
        var handler = new HttpClientHandler
        {
            Proxy = WebHelper.GetProxy(),
            PreAuthenticate = true,
            UseDefaultCredentials = false
        };

        using (var client = new HttpClient(handler))
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers =
                {
                    { HttpRequestHeader.Authorization.ToString(), "OAuth " + preset.OAuthToken }
                }
            };

            using (var response = await client.SendAsync(request, cancellationToken))
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorDescriptor = Serializer.Deserialize<ErrorDescriptor>(responseBody);

                if (errorDescriptor?.Error != null)
                    throw new UploadException(errorDescriptor.Error + ", " + errorDescriptor.Message + ", " + errorDescriptor.Description);

                return responseBody;
            }
        }
    }
}