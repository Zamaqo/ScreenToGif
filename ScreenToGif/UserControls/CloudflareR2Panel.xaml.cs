using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ScreenToGif.Cloud;
using ScreenToGif.Domain.Interfaces;
using ScreenToGif.Util;
using ScreenToGif.Util.Settings;
using ScreenToGif.ViewModel.UploadPresets;
using ScreenToGif.ViewModel.UploadPresets.Cloudflare;

namespace ScreenToGif.UserControls;

public partial class CloudflareR2Panel : UserControl, IPanel
{
    private string _originalTitle = "";
    private bool _sampleBusy;

    public CloudflareR2Panel()
    {
        InitializeComponent();
    }

    private void CloudflareR2Panel_Loaded(object sender, RoutedEventArgs e)
    {
        _originalTitle = NameTextBox.Text.Trim();

        if (DataContext is CloudflareR2Preset preset)
            SecretKeyBox.Password = preset.SecretAccessKey ?? "";

        NameTextBox.Focus();
    }

    private void TokenHyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            StatusBand.Hide();
            ProcessHelper.StartWithShell(e.Uri.AbsoluteUri);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            LogWriter.Log(ex, "Opening the Cloudflare R2 API token page.");
            StatusBand.Error(LocalizationHelper.Get("S.Options.Upload.Preset.Warning.GetToken"));
        }
    }

    private async void UploadSample_Click(object sender, RoutedEventArgs e)
    {
        await RunSampleAsync(async (preset, progress) =>
        {
            var url = await CloudflareR2.UploadSampleAsync(preset, progress, CancellationToken.None);
            ShowSampleLink("S.Options.Upload.Preset.CloudflareR2.Test.Upload.Success", preset, url);
        });
    }

    private async void EditSample_Click(object sender, RoutedEventArgs e)
    {
        await RunSampleAsync(async (preset, progress) =>
        {
            var url = await CloudflareR2.ReplaceSampleAsync(preset, progress, CancellationToken.None);
            ShowSampleLink("S.Options.Upload.Preset.CloudflareR2.Test.Edit.Success", preset, url);
        });
    }

    private async void DeleteSample_Click(object sender, RoutedEventArgs e)
    {
        await RunSampleAsync(async (preset, progress) =>
        {
            await CloudflareR2.DeleteSampleAsync(preset, progress, CancellationToken.None);
            StatusBand.Info(string.Format(LocalizationHelper.Get("S.Options.Upload.Preset.CloudflareR2.Test.Delete.Success"), CloudflareR2.SampleKey(preset)));
        });
    }

    private async Task RunSampleAsync(Func<CloudflareR2Preset, IProgress<string>, Task> action)
    {
        if (_sampleBusy || ReadPreset() is not CloudflareR2Preset preset || !ValidateConnection(preset))
            return;

        _sampleBusy = true;
        TestSplitButton.IsEnabled = false;

        try
        {
            await action(preset, new System.Progress<string>(message => StatusBand.Info(message)));
        }
        catch (Exception ex)
        {
            LogWriter.Log(ex, "Testing the Cloudflare R2 preset.");
            StatusBand.Error(ex.Message);
        }
        finally
        {
            _sampleBusy = false;
            TestSplitButton.IsEnabled = true;
        }
    }

    private void ShowSampleLink(string messageKey, CloudflareR2Preset preset, string url)
    {
        StatusBand.Info(string.Format(LocalizationHelper.Get(messageKey), CloudflareR2.SampleKey(preset)), () =>
        {
            try
            {
                ProcessHelper.StartWithShell(url);
            }
            catch (Exception ex)
            {
                LogWriter.Log(ex, "Opening the Cloudflare R2 sample image.");
            }
        });
    }

    public Task<bool> IsValid()
    {
        if (ReadPreset() is not CloudflareR2Preset preset)
            return Task.FromResult(false);

        if (string.IsNullOrWhiteSpace(preset.Title))
        {
            StatusBand.Warning(LocalizationHelper.Get("S.Options.Upload.Preset.Warning.Title"));
            NameTextBox.Focus();
            return Task.FromResult(false);
        }

        if (UserSettings.All.UploadPresets.OfType<UploadPreset>().Any(presetItem => presetItem.Title != _originalTitle && presetItem.Title == preset.Title))
        {
            StatusBand.Warning(LocalizationHelper.Get("S.Options.Upload.Preset.Warning.Repeated"));
            NameTextBox.Focus();
            return Task.FromResult(false);
        }

        return Task.FromResult(ValidateConnection(preset));
    }

    private CloudflareR2Preset ReadPreset()
    {
        if (DataContext is not CloudflareR2Preset preset)
            return null;

        preset.SecretAccessKey = SecretKeyBox.Password?.Trim() ?? "";
        preset.Title = preset.Title?.Trim() ?? "";
        preset.AccountId = preset.AccountId?.Trim() ?? "";
        preset.AccessKeyId = preset.AccessKeyId?.Trim() ?? "";
        preset.Bucket = preset.Bucket?.Trim().ToLowerInvariant() ?? "";
        preset.Prefix = preset.Prefix?.Trim().Trim('/') ?? "";
        preset.PublicBaseUrl = preset.PublicBaseUrl?.Trim().TrimEnd('/') ?? "";
        return preset;
    }

    private bool ValidateConnection(CloudflareR2Preset preset)
    {
        try
        {
            CloudflareR2.BuildHost(preset.AccountId);
        }
        catch (Exception)
        {
            StatusBand.Warning(LocalizationHelper.Get("S.Options.Upload.Preset.Warning.CloudflareR2.Account"));
            AccountTextBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(preset.AccessKeyId))
        {
            StatusBand.Warning(LocalizationHelper.Get("S.Options.Upload.Preset.Warning.CloudflareR2.AccessKey"));
            AccessKeyTextBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(preset.SecretAccessKey))
        {
            StatusBand.Warning(LocalizationHelper.Get("S.Options.Upload.Preset.Warning.CloudflareR2.SecretKey"));
            SecretKeyBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(preset.Bucket) || preset.Bucket.Contains('/') || preset.Bucket.Contains(' '))
        {
            StatusBand.Warning(LocalizationHelper.Get("S.Options.Upload.Preset.Warning.CloudflareR2.Bucket"));
            BucketTextBox.Focus();
            return false;
        }

        try
        {
            CloudflareR2.BuildObjectKey(preset.Prefix, "file.gif");
        }
        catch (Exception)
        {
            StatusBand.Warning(LocalizationHelper.Get("S.Options.Upload.Preset.Warning.CloudflareR2.Folder"));
            return false;
        }

        try
        {
            CloudflareR2.BuildPublicUrl(preset.PublicBaseUrl, "file.gif");
        }
        catch (Exception)
        {
            StatusBand.Warning(LocalizationHelper.Get("S.Options.Upload.Preset.Warning.CloudflareR2.PublicUrl"));
            PublicUrlTextBox.Focus();
            return false;
        }

        return true;
    }

    public UploadPreset GetPreset()
    {
        return DataContext as CloudflareR2Preset;
    }
}
