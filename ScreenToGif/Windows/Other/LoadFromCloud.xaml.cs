using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ScreenToGif.Cloud;
using ScreenToGif.Domain.Enums;
using ScreenToGif.Util;
using ScreenToGif.Util.Settings;
using ScreenToGif.ViewModel.UploadPresets;
using ScreenToGif.ViewModel.UploadPresets.Cloudflare;
using ScreenToGif.ViewModel.UploadPresets.Yandex;

namespace ScreenToGif.Windows.Other;

public partial class LoadFromCloud
{
    private readonly List<string> _locations = new();
    private CancellationTokenSource _work;
    private bool _loadingServices;

    public bool UseComputer { get; private set; }

    public List<string> LocalPaths { get; } = new();

    public LoadFromCloud()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var presets = UserSettings.All.UploadPresets?.OfType<UploadPreset>().Where(preset => preset.IsEnabled).OrderBy(preset => preset.Title).ToList() ?? new List<UploadPreset>();

        _loadingServices = true;
        ServiceComboBox.ItemsSource = presets;
        ServiceComboBox.SelectedIndex = presets.Count > 0 ? 0 : -1;
        _loadingServices = false;

        ResetLocation();
        _ = LoadFolderAsync();
    }

    private void ServiceComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loadingServices || !IsLoaded)
            return;

        ResetLocation();
        _ = LoadFolderAsync();
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_locations.Count <= 1)
            return;

        _locations.RemoveAt(_locations.Count - 1);
        _ = LoadFolderAsync();
    }

    private void ComputerButton_Click(object sender, RoutedEventArgs e)
    {
        UseComputer = true;
        DialogResult = true;
    }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem is not CloudEntry entry)
            return;

        if (entry.IsFolder)
        {
            _locations.Add(entry.Key);
            _ = LoadFolderAsync();
            return;
        }

        _ = OpenSelectionAsync();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = FileList.SelectedItems.OfType<CloudEntry>().ToList();
        var folder = selected.FirstOrDefault(entry => entry.IsFolder);

        if (selected.Count == 1 && folder != null)
        {
            _locations.Add(folder.Key);
            _ = LoadFolderAsync();
            return;
        }

        _ = OpenSelectionAsync();
    }

    private void ResetLocation()
    {
        _locations.Clear();

        switch (ServiceComboBox.SelectedItem)
        {
            case CloudflareR2Preset cloudflare:
                _locations.Add(CloudflareR2.NormalizePrefix(cloudflare.Prefix));
                break;
            case YandexPreset:
                _locations.Add("app:/");
                break;
        }
    }

    private async Task LoadFolderAsync()
    {
        _work?.Cancel();
        _work = new CancellationTokenSource();
        var token = _work.Token;

        FileList.ItemsSource = null;
        OpenButton.IsEnabled = false;
        UpButton.IsEnabled = _locations.Count > 1;
        PathText.Text = _locations.LastOrDefault() ?? "";
        StatusText.Text = LocalizationHelper.Get("S.Editor.File.Cloud.Loading");

        try
        {
            IReadOnlyList<CloudEntry> entries;

            switch (ServiceComboBox.SelectedItem)
            {
                case CloudflareR2Preset cloudflare:
                    entries = await CloudflareR2.ListFolderAsync(cloudflare, _locations.LastOrDefault(), token);
                    break;
                case YandexPreset yandex:
                    entries = await YandexDisk.ListFolderAsync(yandex, _locations.LastOrDefault(), token);
                    break;
                default:
                    StatusText.Text = LocalizationHelper.Get("S.Editor.File.Cloud.Unsupported");
                    return;
            }

            if (token.IsCancellationRequested)
                return;

            FileList.ItemsSource = entries;
            OpenButton.IsEnabled = true;
            StatusText.Text = entries.Count == 0 ? LocalizationHelper.Get("S.Editor.File.Cloud.Empty") : "";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (token.IsCancellationRequested)
                return;

            LogWriter.Log(ex, "Listing a cloud folder.");
            StatusText.Text = ex.Message;
        }
    }

    private async Task OpenSelectionAsync()
    {
        var files = FileList.SelectedItems.OfType<CloudEntry>().Where(entry => !entry.IsFolder).ToList();

        if (files.Count == 0)
        {
            StatusText.Text = LocalizationHelper.Get("S.Editor.File.Cloud.Select");
            return;
        }

        _work?.Cancel();
        _work = new CancellationTokenSource();
        var token = _work.Token;

        OpenButton.IsEnabled = false;
        ComputerButton.IsEnabled = false;
        StatusText.Text = LocalizationHelper.Get("S.Editor.File.Cloud.Downloading");

        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "ScreenToGif", "Cloud", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            foreach (var file in files)
            {
                var destination = Path.Combine(folder, Path.GetFileName(file.Name));

                switch (ServiceComboBox.SelectedItem)
                {
                    case CloudflareR2Preset cloudflare:
                        await CloudflareR2.DownloadAsync(cloudflare, file.Key, destination, token);
                        break;
                    case YandexPreset yandex:
                        await YandexDisk.DownloadAsync(yandex, file.Key, destination, token);
                        break;
                    default:
                        throw new UploadException(LocalizationHelper.Get("S.Editor.File.Cloud.Unsupported"));
                }

                LocalPaths.Add(destination);
            }

            DialogResult = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (token.IsCancellationRequested)
                return;

            LogWriter.Log(ex, "Downloading a cloud file.");
            StatusText.Text = ex.Message;
            LocalPaths.Clear();
            OpenButton.IsEnabled = true;
            ComputerButton.IsEnabled = true;
        }
    }
}
