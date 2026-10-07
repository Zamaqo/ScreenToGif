using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using ScreenToGif.Util;

namespace ScreenToGif.Windows.Other;

public partial class CustomRibbonDialog
{
    private readonly ObservableCollection<RibbonAction> _selected = [];
    private readonly ObservableCollection<RibbonAction> _available = [];

    public CustomRibbonDialog()
    {
        InitializeComponent();

        foreach (var action in RibbonActionCatalog.GetSelected())
            _selected.Add(action);

        foreach (var action in RibbonActionCatalog.All)
        {
            if (_selected.All(item => item.Id != action.Id))
                _available.Add(action);
        }

        SelectedList.ItemsSource = _selected;
        AvailableList.ItemsSource = _available;
        UpdateButtons();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (AvailableList.SelectedItem is not RibbonAction action)
            return;

        _available.Remove(action);
        _selected.Add(action);
        SelectedList.SelectedItem = action;
        SelectedList.ScrollIntoView(action);
        UpdateButtons();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedList.SelectedItem is not RibbonAction action)
            return;

        var index = _selected.IndexOf(action);
        _selected.Remove(action);
        InsertAvailable(action);

        if (_selected.Count > 0)
            SelectedList.SelectedIndex = System.Math.Min(index, _selected.Count - 1);

        UpdateButtons();
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        MoveSelected(-1);
    }

    private void DownButton_Click(object sender, RoutedEventArgs e)
    {
        MoveSelected(1);
    }

    private void SelectedList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (IsItemDoubleClick(e) && SelectedList.SelectedItem != null)
            RemoveButton_Click(sender, e);
    }

    private void AvailableList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (IsItemDoubleClick(e) && AvailableList.SelectedItem != null)
            AddButton_Click(sender, e);
    }

    private static bool IsItemDoubleClick(MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;

        while (source != null && source is not ListBoxItem)
            source = VisualTreeHelper.GetParent(source);

        return source is ListBoxItem;
    }

    private void List_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateButtons();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        RibbonActionCatalog.Save(_selected);
        DialogResult = true;
    }

    private void Cancel_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = true;
    }

    private void Cancel_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void MoveSelected(int direction)
    {
        if (SelectedList.SelectedItem is not RibbonAction action)
            return;

        var index = _selected.IndexOf(action);
        var target = index + direction;

        if (target < 0 || target >= _selected.Count)
            return;

        _selected.Move(index, target);
        SelectedList.SelectedIndex = target;
        UpdateButtons();
    }

    private void InsertAvailable(RibbonAction action)
    {
        var order = System.Array.IndexOf(RibbonActionCatalog.All, action);
        var insertAt = 0;

        for (var i = 0; i < _available.Count; i++)
        {
            if (System.Array.IndexOf(RibbonActionCatalog.All, _available[i]) > order)
                break;

            insertAt = i + 1;
        }

        _available.Insert(insertAt, action);
        AvailableList.SelectedItem = action;
    }

    private void UpdateButtons()
    {
        var selectedIndex = SelectedList.SelectedIndex;

        AddButton.IsEnabled = AvailableList.SelectedItem != null;
        RemoveButton.IsEnabled = selectedIndex >= 0;
        UpButton.IsEnabled = selectedIndex > 0;
        DownButton.IsEnabled = selectedIndex >= 0 && selectedIndex < _selected.Count - 1;
    }
}
