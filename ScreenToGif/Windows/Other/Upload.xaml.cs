using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScreenToGif.Domain.Enums;
using ScreenToGif.Domain.Interfaces;
using ScreenToGif.UserControls;
using ScreenToGif.Util;
using ScreenToGif.ViewModel.UploadPresets;
using ScreenToGif.ViewModel.UploadPresets.Cloudflare;
using ScreenToGif.ViewModel.UploadPresets.Imgur;
using ScreenToGif.ViewModel.UploadPresets.Yandex;

namespace ScreenToGif.Windows.Other;

public partial class Upload
{
    public UploadPreset CurrentPreset { get; set; }

    public bool IsEditing { get; set; }

    public ExportFormats? Type { get; set; }

    public Upload()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsEditing)
        {
            MainBorder.Background = TryFindResource("Vector.Pen") as Brush;
            TypeTextBlock.Text = LocalizationHelper.Get("S.Edit");
            SelectService(CurrentPreset?.Type ?? UploadDestinations.NotDefined);
            TypeComboBox.IsEnabled = false;
            EnabledCheckBox.Visibility = Visibility.Visible;

            TypeComboBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
        else
        {
            //If the file type is set, only display services that supports upload for that.
            if (Type.HasValue)
            {
                FilterRun.Text = $"\n(*.{Type.ToString().ToLower()})";

                for (var index = TypeComboBox.Items.Count - 1; index >= 0; index--)
                {
                    if (TypeComboBox.Items[index] is UploadPreset item && item.AllowedTypes is { Count: > 0 } && !item.AllowedTypes.Contains(Type.Value))
                        TypeComboBox.Items.RemoveAt(index);
                }
            }

            TypeComboBox.Focus();
        }
    }

    private void TypeComboBox_Selected(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (TypeComboBox.SelectedItem is not UploadPreset selected || selected.Type == UploadDestinations.NotDefined)
        {
            MainPresenter.Content = null;
            return;
        }

        if (!IsEditing)
        {
            //Create a new model. The combo item is only a template, so edits must not change it.
            switch (selected.Type)
            {
                case UploadDestinations.Imgur:
                    CurrentPreset = new ImgurPreset();
                    break;
                case UploadDestinations.Yandex:
                    CurrentPreset = new YandexPreset();
                    break;
                case UploadDestinations.CloudflareR2:
                    CurrentPreset = new CloudflareR2Preset();
                    break;
            }
        }

        switch (selected.Type)
        {
            case UploadDestinations.Imgur:
                MainPresenter.Content = new ImgurPanel { DataContext = CurrentPreset };
                break;
            case UploadDestinations.Yandex:
                MainPresenter.Content = new YandexPanel { DataContext = CurrentPreset };
                break;
            case UploadDestinations.CloudflareR2:
                MainPresenter.Content = new CloudflareR2Panel { DataContext = CurrentPreset };
                break;
        }
    }

    private void SelectService(UploadDestinations type)
    {
        foreach (var item in TypeComboBox.Items)
        {
            if (item is UploadPreset preset && preset.Type == type)
            {
                TypeComboBox.SelectedItem = item;
                return;
            }
        }
    }

    private void Ok_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = IsLoaded && TypeComboBox.SelectedItem is UploadPreset preset && preset.Type != UploadDestinations.NotDefined;
    }

    private async void Ok_Executed(object sender, RoutedEventArgs e)
    {
        OkButton.Focus();

        if (MainPresenter.Content is not IPanel panel || !await panel.IsValid())
            return;

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}