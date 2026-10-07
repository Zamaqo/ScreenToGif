using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

using ScreenToGif.Controls;
using ScreenToGif.Util.Settings;

namespace ScreenToGif.Util;

/// <summary>
/// One editor ribbon command that can be placed on the Custom tab.
/// </summary>
public sealed class RibbonAction
{
    public string Id { get; init; }

    public string TextKey { get; init; }

    /// <summary>
    /// Used when the button caption is not a localized resource, such as "100%".
    /// </summary>
    public string FixedText { get; init; }

    public string IconKey { get; init; }

    public string CommandKey { get; init; }

    public string GroupKey { get; init; }

    public bool IsRepeat { get; init; }

    public string Label => !string.IsNullOrEmpty(FixedText)
        ? FixedText
        : Application.Current?.TryFindResource(TextKey) as string ?? Id;

    public string Group => Application.Current?.TryFindResource(GroupKey) as string ?? "";

    public Brush Icon => Application.Current?.TryFindResource(IconKey) as Brush;
}

/// <summary>
/// The editor ribbon commands a user can add to the Custom tab, in ribbon order.
/// </summary>
public static class RibbonActionCatalog
{
    public static readonly RibbonAction[] All =
    [
        Action("NewRecording", "S.Editor.File.New.Recording", "Vector.Record.New", "Command.NewRecording", "S.Editor.File.New"),
        Action("NewWebcamRecording", "S.Editor.File.New.Webcam", "Vector.Camera.New", "Command.NewWebcamRecording", "S.Editor.File.New"),
        Action("NewBoardRecording", "S.Editor.File.New.Board", "Vector.Board.New", "Command.NewBoardRecording", "S.Editor.File.New"),
        Action("NewAnimation", "S.Editor.File.Blank", "Vector.File.New", "Command.NewAnimation", "S.Editor.File.New"),
        Action("InsertRecording", "S.Editor.File.Insert.Recording", "Vector.Record.Add", "Command.InsertRecording", "S.Editor.File.Insert"),
        Action("InsertWebcamRecording", "S.Editor.File.Insert.Webcam", "Vector.Camera.Add", "Command.InsertWebcamRecording", "S.Editor.File.Insert"),
        Action("InsertBoardRecording", "S.Editor.File.Insert.Board", "Vector.Board.Add", "Command.InsertBoardRecording", "S.Editor.File.Insert"),
        Action("InsertFromMedia", "S.Editor.File.Insert.Media", "Vector.Open", "Command.InsertFromMedia", "S.Editor.File.Insert"),
        Action("Load", "S.Editor.File.Load", "Vector.Open", "Command.Load", "S.Editor.File"),
        Action("SaveAs", "S.Editor.File.Save", "Vector.Save", "Command.SaveAs", "S.Editor.File"),
        Action("LoadRecent", "S.Editor.File.LoadRecent", "Vector.Project", "Command.LoadRecent", "S.Editor.File"),
        Action("DiscardProject", "S.Editor.File.Discard", "Vector.Remove", "Command.DiscardProject", "S.Editor.File"),
        Action("Undo", "S.Editor.Home.Undo", "Vector.Undo", "Command.Undo", "S.Editor.Home.ActionStack"),
        Action("Reset", "S.Editor.Home.Reset", "Vector.Repeat", "Command.Reset", "S.Editor.Home.ActionStack"),
        Action("Redo", "S.Editor.Home.Redo", "Vector.Redo", "Command.Redo", "S.Editor.Home.ActionStack"),
        Action("Paste", "S.Editor.Home.Paste", "Vector.Paste", "Command.Paste", "S.Editor.Home.Clipboard"),
        Action("Copy", "S.Editor.Home.Copy", "Vector.Copy", "Command.Copy", "S.Editor.Home.Clipboard"),
        Action("Cut", "S.Editor.Home.Cut", "Vector.Cut", "Command.Cut", "S.Editor.Home.Clipboard"),
        Action("Zoom100", null, "Vector.Fit", "Command.Zoom100", "S.Editor.Home.Zoom", fixedText: "100%"),
        Action("SizeToContent", "S.Editor.Home.SizeToContent", "Vector.SizeToContent", "Command.SizeToContent", "S.Editor.Home.Zoom"),
        Action("FitImage", "S.Editor.Home.FitImage", "Vector.PictureFit", "Command.FitImage", "S.Editor.Home.Zoom"),
        Action("SelectAll", "S.Editor.Home.SelectAll", "Vector.Cursor", "Command.SelectAll", "S.Editor.Home.Select"),
        Action("GoTo", "S.Editor.Home.GoTo", "Vector.Forward", "Command.GoTo", "S.Editor.Home.Select"),
        Action("InverseSelection", "S.Editor.Home.Inverse", "Vector.InverseSelection", "Command.InverseSelection", "S.Editor.Home.Select"),
        Action("Unselect", "S.Editor.Home.Deselect", "Vector.Unselect", "Command.Unselect", "S.Editor.Home.Select"),
        Action("FirstFrame", "S.Editor.Playback.First", "Vector.First.Green", "Command.FirstFrame", "S.Editor.Playback"),
        Action("PreviousFrame", "S.Editor.Playback.Previous", "Vector.Previous.Green", "Command.PreviousFrame", "S.Editor.Playback", isRepeat: true),
        Action("Play", "S.Editor.Playback.Play", "Vector.Play", "Command.Play", "S.Editor.Playback"),
        Action("NextFrame", "S.Editor.Playback.Next", "Vector.Next.Green", "Command.NextFrame", "S.Editor.Playback", isRepeat: true),
        Action("LastFrame", "S.Editor.Playback.Last", "Vector.Last.Green", "Command.LastFrame", "S.Editor.Playback"),
        Action("Delete", "S.Editor.Edit.Delete", "Vector.RemoveImage", "Command.Delete", "S.Editor.Edit.Frames"),
        Action("RemoveDuplicates", "S.Editor.Edit.Frames.Duplicates", "Vector.RemoveImage", "Command.RemoveDuplicates", "S.Editor.Edit.Frames"),
        Action("Reduce", "S.Editor.Edit.Frames.Reduce", "Vector.RemoveImage", "Command.Reduce", "S.Editor.Edit.Frames"),
        Action("SmoothLoop", "S.Editor.Edit.Frames.SmoothLoop", "Vector.Repeat", "Command.SmoothLoop", "S.Editor.Edit.Frames"),
        Action("DeletePrevious", "S.Editor.Edit.DeletePrevious", "Vector.Delete.Before", "Command.DeletePrevious", "S.Editor.Edit.Frames"),
        Action("DeleteNext", "S.Editor.Edit.DeleteNext", "Vector.Delete.After", "Command.DeleteNext", "S.Editor.Edit.Frames"),
        Action("Reverse", "S.Editor.Edit.Reverse", "Vector.Invert", "Command.Reverse", "S.Editor.Edit.Reordering"),
        Action("Yoyo", "S.Editor.Edit.Yoyo", "Vector.Yoyo", "Command.Yoyo", "S.Editor.Edit.Reordering"),
        Action("MoveLeft", "S.Editor.Edit.MoveLeft", "Vector.MoveLeft", "Command.MoveLeft", "S.Editor.Edit.Reordering"),
        Action("MoveRight", "S.Editor.Edit.MoveRight", "Vector.MoveRight", "Command.MoveRight", "S.Editor.Edit.Reordering"),
        Action("OverrideDelay", "S.Editor.Edit.Delay.Override", "Vector.OverrideDelay", "Command.OverrideDelay", "S.Editor.Edit.Delay"),
        Action("IncreaseDecreaseDelay", "S.Editor.Edit.Delay.IncreaseDecrease", "Vector.IncreaseDecreaseDelay", "Command.IncreaseDecreaseDelay", "S.Editor.Edit.Delay"),
        Action("ScaleDelay", "S.Editor.Edit.Delay.Scale", "Vector.ScaleDelay", "Command.ScaleDelay", "S.Editor.Edit.Delay"),
        Action("Resize", "S.Editor.Image.Resize", "Vector.Resize", "Command.Resize", "S.Editor.Image.SizePosition"),
        Action("Crop", "S.Editor.Image.Crop", "Vector.Crop", "Command.Crop", "S.Editor.Image.SizePosition"),
        Action("FlipRotate", "S.Editor.Image.FlipRotate", "Vector.FlipHorizontal", "Command.FlipRotate", "S.Editor.Image.SizePosition"),
        Action("Caption", "S.Editor.Image.Caption", "Vector.Caption", "Command.Caption", "S.Editor.Image.Text"),
        Action("KeyStrokes", "S.Editor.Image.KeyStrokes", "Vector.Keyboard", "Command.KeyStrokes", "S.Editor.Image.Text"),
        Action("FreeText", "S.Editor.Image.FreeText", "Vector.FreeText", "Command.FreeText", "S.Editor.Image.Text"),
        Action("TitleFrame", "S.Editor.Image.TitleFrame", "Vector.TitleFrame", "Command.TitleFrame", "S.Editor.Image.Text"),
        Action("FreeDrawing", "S.Editor.Image.FreeDrawing", "Vector.FreeDrawing", "Command.FreeDrawing", "S.Editor.Image.Overlay"),
        Action("Shapes", "S.Editor.Image.Shape", "Vector.Ellipse", "Command.Shapes", "S.Editor.Image.Overlay"),
        Action("Progress", "S.Editor.Image.Progress", "Vector.Progress", "Command.Progress", "S.Editor.Image.Overlay"),
        Action("MouseEvents", "S.Editor.Image.MouseEvents", "Vector.Cursor", "Command.MouseEvents", "S.Editor.Image.Overlay"),
        Action("Border", "S.Editor.Image.Border", "Vector.Border", "Command.Border", "S.Editor.Image.Overlay"),
        Action("Shadow", "S.Editor.Image.Shadow", "Vector.Shadow", "Command.Shadow", "S.Editor.Image.Overlay"),
        Action("Obfuscate", "S.Editor.Image.Obfuscate", "Vector.Obfuscate", "Command.Obfuscate", "S.Editor.Image.Overlay"),
        Action("Watermark", "S.Editor.Image.Watermark", "Vector.Watermark", "Command.Watermark", "S.Editor.Image.Overlay"),
        Action("Cinemagraph", "S.Editor.Image.Cinemagraph", "Vector.Cinemagraph", "Command.Cinemagraph", "S.Editor.Image.Overlay"),
        Action("Fade", "S.Editor.Transitions.Fade", "Vector.Fade", "Command.Fade", "S.Editor.Transitions"),
        Action("Slide", "S.Editor.Transitions.Slide", "Vector.Slide", "Command.Slide", "S.Editor.Transitions")
    ];

    public static System.Collections.Generic.IReadOnlyList<RibbonAction> GetSelected()
    {
        var stored = UserSettings.All.CustomRibbonActions;

        if (stored == null)
            return [];

        return stored.OfType<string>()
            .Select(id => All.FirstOrDefault(action => action.Id == id))
            .Where(action => action != null)
            .ToList();
    }

    public static void Save(System.Collections.Generic.IEnumerable<RibbonAction> actions)
    {
        var list = new ArrayList();

        foreach (var action in actions)
            list.Add(action.Id);

        UserSettings.All.CustomRibbonActions = list;
        UserSettings.Save();
    }

    public static FrameworkElement CreateButton(RibbonAction action)
    {
        if (action.IsRepeat)
            return CreateRepeatButton(action);

        var button = new ExtendedButton
        {
            MinWidth = 55,
            ContentHeight = 28,
            ContentWidth = 28,
            Margin = new Thickness(1, 0, 1, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.FindResource("Style.Button.Vertical"),
            Command = (ICommand)Application.Current.FindResource(action.CommandKey)
        };

        ApplyCaption(button, action, ExtendedButton.TextProperty, ExtendedButton.IconProperty);
        AttachTooltip(button);
        return button;
    }

    private static FrameworkElement CreateRepeatButton(RibbonAction action)
    {
        var button = new ExtendedRepeatButton
        {
            ContentHeight = 28,
            ContentWidth = 28,
            Margin = new Thickness(1, 0, 1, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.FindResource("Style.RepeatButton.Vertical"),
            Command = (ICommand)Application.Current.FindResource(action.CommandKey)
        };

        ApplyCaption(button, action, ExtendedRepeatButton.TextProperty, ExtendedRepeatButton.IconProperty);
        AttachTooltip(button);
        return button;
    }

    private static void ApplyCaption(FrameworkElement button, RibbonAction action, DependencyProperty textProperty, DependencyProperty iconProperty)
    {
        if (string.IsNullOrEmpty(action.FixedText))
            button.SetResourceReference(textProperty, action.TextKey);
        else
            button.SetValue(textProperty, action.FixedText);

        button.SetResourceReference(iconProperty, action.IconKey);
    }

    private static void AttachTooltip(FrameworkElement button)
    {
        button.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Command")
        {
            RelativeSource = RelativeSource.Self,
            Converter = Application.Current.FindResource("CommandToInputGestureText") as IValueConverter
        });

        ToolTipService.SetPlacement(button, PlacementMode.Bottom);
        ToolTipService.SetHorizontalOffset(button, -5);
    }

    private static RibbonAction Action(string id, string textKey, string iconKey, string commandKey, string groupKey, string fixedText = null, bool isRepeat = false)
    {
        return new RibbonAction
        {
            Id = id,
            TextKey = textKey,
            FixedText = fixedText,
            IconKey = iconKey,
            CommandKey = commandKey,
            GroupKey = groupKey,
            IsRepeat = isRepeat
        };
    }
}
