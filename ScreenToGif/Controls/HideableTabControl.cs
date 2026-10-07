using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ScreenToGif.Domain.Enums;
using ScreenToGif.Util;
using ScreenToGif.Util.Settings;

namespace ScreenToGif.Controls;

/// <summary>
/// Basic class of a Hideable TabControl.
/// </summary>
public class HideableTabControl : TabControl
{
    #region Variables

    private const double TabHoldSlop = 6;

    private Button _hideButton;
    private ExtendedMenuItem _extrasMenuItem;
    private TabPanel _tabPanel;
    private Border _border;
    private ExtendedToggleButton _notificationButton;
    private NotificationBox _notificationBox;

    private DispatcherTimer _tabHoldTimer;
    private AwareTabItem _heldTab;
    private Point _holdStart;
    private bool _tabHoldReady;
    private bool _reorderingTab;
    private bool _tabOrderChanged;
    private bool _endingTabHold;

    #endregion

    #region Dependency Properties

    public static DependencyProperty OptionsCommandProperty = DependencyProperty.Register("OptionsCommand", typeof(ICommand), typeof(HideableTabControl), new PropertyMetadata(null));

    public static DependencyProperty FeedbackCommandProperty = DependencyProperty.Register("FeedbackCommand", typeof(ICommand), typeof(HideableTabControl), new PropertyMetadata(null));

    public static DependencyProperty TroubleshootCommandProperty = DependencyProperty.Register("TroubleshootCommand", typeof(ICommand), typeof(HideableTabControl), new PropertyMetadata(null));

    public static DependencyProperty HelpCommandProperty = DependencyProperty.Register("HelpCommand", typeof(ICommand), typeof(HideableTabControl), new PropertyMetadata(null));

    #endregion

    #region Properties

    public ICommand OptionsCommand
    {
        get => (ICommand)GetValue(OptionsCommandProperty);
        set => SetValue(OptionsCommandProperty, value);
    }

    public ICommand FeedbackCommand
    {
        get => (ICommand)GetValue(FeedbackCommandProperty);
        set => SetValue(FeedbackCommandProperty, value);
    }

    public ICommand TroubleshootCommand
    {
        get => (ICommand)GetValue(TroubleshootCommandProperty);
        set => SetValue(TroubleshootCommandProperty, value);
    }

    public ICommand HelpCommand
    {
        get => (ICommand)GetValue(HelpCommandProperty);
        set => SetValue(HelpCommandProperty, value);
    }

    #endregion

    static HideableTabControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HideableTabControl), new FrameworkPropertyMetadata(typeof(HideableTabControl)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _tabPanel = Template.FindName("TabPanel", this) as TabPanel;
        _border = Template.FindName("ContentBorder", this) as Border;

        _notificationButton = Template.FindName("NotificationsButton", this) as ExtendedToggleButton;
        _notificationBox = Template.FindName("NotificationBox", this) as NotificationBox;
        _extrasMenuItem = Template.FindName("ExtrasMenuItem", this) as ExtendedMenuItem;

        _hideButton = Template.FindName("HideGridButton", this) as Button;

        //Hide button.
        if (_hideButton != null)
            _hideButton.Click += HideButton_Clicked;

        //Show tab (if hidden).
        if (_tabPanel != null)
        {
            foreach (TabItem tabItem in _tabPanel.Children)
                tabItem.PreviewMouseDown += TabItem_PreviewMouseDown;

            _tabPanel.PreviewMouseWheel += TabControl_PreviewMouseWheel;
        }

        if (_notificationButton != null)
            _notificationButton.Checked += NotificationButton_Checked;

        UpdateVisual();
        AnimateOrNot();
    }

    /// <summary>
    /// Restores the tab order saved from a previous drag.
    /// </summary>
    public void ApplySavedTabOrder()
    {
        var stored = UserSettings.All.EditorTabOrder;

        if (stored == null || stored.Count == 0)
            return;

        var remaining = Items.OfType<AwareTabItem>().ToList();
        var ordered = new List<AwareTabItem>();

        foreach (var key in stored.OfType<string>())
        {
            var tab = remaining.FirstOrDefault(item => item.TabKey == key);

            if (tab == null)
                continue;

            ordered.Add(tab);
            remaining.Remove(tab);
        }

        ordered.AddRange(remaining);

        var currentKeys = Items.OfType<AwareTabItem>().Select(tab => tab.TabKey).ToList();

        if (currentKeys.SequenceEqual(ordered.Select(tab => tab.TabKey)))
            return;

        var selected = SelectedItem;

        foreach (var tab in ordered)
            Items.Remove(tab);

        foreach (var tab in ordered)
            Items.Add(tab);

        if (selected != null)
            SelectedItem = selected;
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        BeginTabHold(e);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (_heldTab == null || e.LeftButton != MouseButtonState.Pressed || _tabPanel == null)
            return;

        if (!_tabHoldReady)
        {
            var moved = e.GetPosition(this);

            if (Math.Abs(moved.X - _holdStart.X) > TabHoldSlop || Math.Abs(moved.Y - _holdStart.Y) > TabHoldSlop)
                CancelTabHold();

            return;
        }

        _reorderingTab = true;
        Mouse.OverrideCursor = Cursors.SizeAll;
        _heldTab.Opacity = 0.65;

        if (!IsMouseCaptured)
            CaptureMouse();

        if (MoveHeldTab(e.GetPosition(_tabPanel)))
            _tabOrderChanged = true;

        e.Handled = true;
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        FinishTabHold();
        base.OnPreviewMouseLeftButtonUp(e);
    }

    private void BeginTabHold(MouseButtonEventArgs e)
    {
        CancelTabHold();

        if (FindParent<AwareTabItem>(e.OriginalSource as DependencyObject) is not AwareTabItem tab || !Items.Contains(tab))
            return;

        _heldTab = tab;
        _holdStart = e.GetPosition(this);
        _tabHoldReady = false;
        _reorderingTab = false;
        _tabOrderChanged = false;

        _tabHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _tabHoldTimer.Tick += TabHoldTimer_Tick;
        _tabHoldTimer.Start();
    }

    private void TabHoldTimer_Tick(object sender, EventArgs e)
    {
        _tabHoldTimer?.Stop();

        if (_heldTab == null || Mouse.LeftButton != MouseButtonState.Pressed)
        {
            CancelTabHold();
            return;
        }

        _tabHoldReady = true;
    }

    private bool MoveHeldTab(Point positionInPanel)
    {
        var dropIndex = GetDropIndex(positionInPanel);
        var currentIndex = Items.IndexOf(_heldTab);

        if (currentIndex < 0 || dropIndex == currentIndex || dropIndex == currentIndex + 1)
            return false;

        Items.Remove(_heldTab);

        if (dropIndex > currentIndex)
            dropIndex--;

        if (dropIndex < 0)
            dropIndex = 0;

        if (dropIndex > Items.Count)
            dropIndex = Items.Count;

        Items.Insert(dropIndex, _heldTab);
        _heldTab.IsSelected = true;
        return true;
    }

    private int GetDropIndex(Point positionInPanel)
    {
        var tabs = _tabPanel.Children.OfType<TabItem>().ToList();

        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            var left = tab.TranslatePoint(new Point(0, 0), _tabPanel).X;

            if (positionInPanel.X < left + tab.ActualWidth / 2)
                return Items.IndexOf(tab);
        }

        return Items.Count;
    }

    private void FinishTabHold()
    {
        if (_endingTabHold)
            return;

        _endingTabHold = true;

        try
        {
            var changed = _tabOrderChanged;
            CancelTabHold();

            if (changed)
                SaveTabOrder();
        }
        finally
        {
            _endingTabHold = false;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        if (_reorderingTab)
            FinishTabHold();
    }

    private void CancelTabHold()
    {
        if (_tabHoldTimer != null)
        {
            _tabHoldTimer.Stop();
            _tabHoldTimer.Tick -= TabHoldTimer_Tick;
            _tabHoldTimer = null;
        }

        if (_heldTab != null)
            _heldTab.Opacity = 1;

        if (_reorderingTab)
            Mouse.OverrideCursor = null;

        if (IsMouseCaptured)
            ReleaseMouseCapture();

        _heldTab = null;
        _tabHoldReady = false;
        _reorderingTab = false;
        _tabOrderChanged = false;
    }

    private void SaveTabOrder()
    {
        var list = new ArrayList();

        foreach (var tab in Items.OfType<AwareTabItem>())
        {
            if (!string.IsNullOrWhiteSpace(tab.TabKey))
                list.Add(tab.TabKey);
        }

        UserSettings.All.EditorTabOrder = list;
        UserSettings.Save();
    }

    private static T FindParent<T>(DependencyObject source) where T : DependencyObject
    {
        while (source != null && source is not T)
            source = VisualTreeHelper.GetParent(source);

        return source as T;
    }

    #region Events

    private void TabControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
        {
            if (SelectedIndex < Items.Count - 1)
                SelectedIndex++;
            else
                SelectedIndex = 0;
        }
        else
        {

            if (SelectedIndex > 0)
                SelectedIndex--;
            else
                SelectedIndex = Items.Count - 1;
        }

        if (!_tabPanel.Children[SelectedIndex].IsEnabled)
        {
            if (_tabPanel.Children.OfType<TabItem>().All(x => !x.IsEnabled))
            {
                SelectedIndex = -1;
                return;
            }

            TabControl_PreviewMouseWheel(sender, e);
        }

        TabItem_PreviewMouseDown(sender, null);
        ChangeVisibility();
    }

    private void TabItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TabItem selected)
            selected.IsSelected = true;

        if (Math.Abs(_border.ActualHeight - 100) < 0)
            return;

        var animation = new DoubleAnimation(_border.ActualHeight, 100, new Duration(new TimeSpan(0, 0, 0, 1)))
        {
            EasingFunction = new PowerEase { Power = 8 }
        };
        _border.BeginAnimation(HeightProperty, animation);

        var opacityAnimation = new DoubleAnimation(_border.Opacity, 1, new Duration(new TimeSpan(0, 0, 0, 1)))
        {
            EasingFunction = new PowerEase { Power = 8 }
        };
        _border.BeginAnimation(OpacityProperty, opacityAnimation);

        var visibilityAnimation = new ObjectAnimationUsingKeyFrames();
        visibilityAnimation.KeyFrames.Add(new DiscreteObjectKeyFrame(Visibility.Visible, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5))));
        _hideButton.BeginAnimation(VisibilityProperty, visibilityAnimation);

        //Margin = 5,5,0,-1
        var marginAnimation = new ThicknessAnimation(_tabPanel.Margin, new Thickness(5, 5, 0, -1), new Duration(new TimeSpan(0, 0, 0, 0, 1)))
        {
            EasingFunction = new PowerEase { Power = 8 }
        };
        _tabPanel.BeginAnimation(MarginProperty, marginAnimation);
    }

    private void HideButton_Clicked(object sender, RoutedEventArgs routedEventArgs)
    {
        //ActualHeight = 0
        var animation = new DoubleAnimation(_border.ActualHeight, 0, new Duration(new TimeSpan(0, 0, 0, 1)))
        {
            EasingFunction = new PowerEase { Power = 8 }
        };
        _border.BeginAnimation(HeightProperty, animation);

        //Opacity = 0
        var opacityAnimation = new DoubleAnimation(_border.Opacity, 0, new Duration(new TimeSpan(0, 0, 0, 1)))
        {
            EasingFunction = new PowerEase { Power = 8 }
        };
        _border.BeginAnimation(OpacityProperty, opacityAnimation);

        //SelectedItem = null
        var objectAnimation = new ObjectAnimationUsingKeyFrames();
        objectAnimation.KeyFrames.Add(new DiscreteObjectKeyFrame(null, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
        BeginAnimation(SelectedItemProperty, objectAnimation);

        //Visibility = Visibility.Collapsed
        var visibilityAnimation = new ObjectAnimationUsingKeyFrames();
        visibilityAnimation.KeyFrames.Add(new DiscreteObjectKeyFrame(Visibility.Collapsed, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
        _hideButton.BeginAnimation(VisibilityProperty, visibilityAnimation);

        //Margin = 5,5,0,5
        var marginAnimation = new ThicknessAnimation(_tabPanel.Margin, new Thickness(5, 5, 0, 5), new Duration(new TimeSpan(0, 0, 0, 0, 1)))
        {
            EasingFunction = new PowerEase { Power = 8 }
        };
        _tabPanel.BeginAnimation(MarginProperty, marginAnimation);
    }

    private void NotificationButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (_notificationButton.FindResource("NotificationStoryboard") is Storyboard story)
            story.Stop();
    }

    #endregion


    /// <summary>
    /// Changes the visibility of the Content.
    /// </summary>
    /// <param name="visible">True to show the Content.</param>
    public void ChangeVisibility(bool visible = true)
    {
        _border.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _hideButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void UpdateVisual(bool isActivated = true)
    {
        //Shows only a white foreground when:

        //var color = Glass.GlassColor;
        //var ness = Glass.GlassColor.GetBrightness();
        //var aa = color.ConvertRgbToHsv();

        //var darkForeground = !SystemParameters.IsGlassEnabled || !Other.IsGlassSupported() || Glass.GlassColor.GetBrightness() > 973 || !isActivated;
        var darkForeground = !SystemParameters.IsGlassEnabled || !isActivated;
        //var darkForeground = !SystemParameters.IsGlassEnabled || !Other.IsWin8OrHigher() || aa.V > 0.5 || !isActivated;
        var showBackground = true;// !Other.IsGlassSupported();

        //Console.WriteLine("!IsGlassEnabled: " + !SystemParameters.IsGlassEnabled);
        //Console.WriteLine("!UsesColor: " + !Glass.UsesColor);
        //Console.WriteLine("GlassColorBrightness <= 137: " + (Glass.GlassColor.GetBrightness() <= 137));
        //Console.WriteLine("!IsWin8: " + !Other.IsWin8OrHigher());
        //Console.WriteLine("IsActivated: " + isActivated);
        //Console.WriteLine("IsDark: " + isDark);

        //Update each tab.
        if (_tabPanel != null)
            foreach (var tab in _tabPanel.Children.OfType<AwareTabItem>())
            {
                //To force the change.
                if (tab.IsDark == !darkForeground)
                    tab.IsDark = !tab.IsDark;

                if (tab.ShowBackground == showBackground)
                    tab.ShowBackground = !tab.ShowBackground;

                tab.IsDark = !darkForeground;
                tab.ShowBackground = showBackground;
            }

        //Update the buttons.
        if (_notificationButton != null)
        {
            _notificationButton.DarkMode = !darkForeground;
            _notificationButton.IsOverNonClientArea = UserSettings.All.EditorExtendChrome;
        }

        if (_extrasMenuItem != null)
        {
            _extrasMenuItem.DarkMode = !darkForeground;
            _extrasMenuItem.IsOverNonClientArea = UserSettings.All.EditorExtendChrome;
        }
    }


    public void UpdateNotifications(int? id = null)
    {
        _notificationBox?.UpdateNotification(id);

        AnimateOrNot();
    }

    public EncoderListViewItem AddEncoding(int id, bool isActive = false)
    {
        //Display the popup (if the editor is active) and animate the button.
        if (isActive)
            _notificationButton.IsChecked = true;

        AnimateOrNot(true);

        return _notificationBox.AddEncoding(id);
    }

    public void UpdateEncoding(int? id = null, bool onlyStatus = false)
    {
        if (!onlyStatus)
            _notificationBox?.UpdateEncoding(id);

        AnimateOrNot();
    }

    public EncoderListViewItem RemoveEncoding(int id)
    {
        try
        {
            return _notificationBox.RemoveEncoding(id);
        }
        finally
        {
            AnimateOrNot();
        }
    }

    private void AnimateOrNot(bool add = false)
    {
        var story = _notificationButton.FindResource("NotificationStoryboard") as Storyboard;

        if (story != null)
        {
            story.Stop();

            //Blink the button when an encoding is added.
            if (add)
                story.Begin();
        }

        var anyProcessing = EncodingManager.Encodings.Any(s => s.Status == EncodingStatus.Processing);
        var anyCompleted = EncodingManager.Encodings.Any(s => s.Status == EncodingStatus.Completed);
        var anyFaulty = EncodingManager.Encodings.Any(s => s.Status == EncodingStatus.Error);

        _notificationButton.Icon = anyProcessing ? FindResource("Vector.Progress") as Brush :
            anyCompleted ? FindResource("Vector.Ok.Round") as Brush :
            anyFaulty ? FindResource("Vector.Cancel.Round") as Brush : _notificationButton.Icon;
        _notificationButton.IsImportant = anyProcessing;
        _notificationButton.SetResourceReference(ExtendedToggleButton.TextProperty, anyProcessing ? "S.Encoder.Encoding" : anyCompleted ? "S.Encoder.Completed" : anyFaulty? "S.Encoder.Error" : "S.Notifications");

        if (anyProcessing || anyCompleted || anyFaulty)
            return;

        //Animate the button for notifications, when there are no encodings.
        var most = NotificationManager.Notifications.Select(s => s.Kind).OrderByDescending(a => (int)a).FirstOrDefault();

        _notificationButton.Icon = TryFindResource(StatusBand.KindToString(most)) as Brush;
        _notificationButton.IsImportant = most != StatusType.None;
        _notificationButton.SetResourceReference(ExtendedToggleButton.TextProperty, "S.Notifications");

        if(story != null)
        {
            story.Stop();

            if (most != StatusType.None)
                story.Begin();
        }
    }
}