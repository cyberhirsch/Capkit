#region License Information (GPL v3)

/*
    Capkit - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

#nullable enable

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Capkit.AvaloniaUI.Theming;
using Capkit.HelpersLib;
using Capkit.ImageEffectsLib;
using Capkit.Localization;
using Capkit.ScreenCaptureLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capkit;

/// <summary>
/// The Capture hub: capture buttons, the shortcuts (workflows) that capture, and the pipeline that runs after a
/// capture. A shortcut follows the Default pipeline except for the single settings it changes.
/// </summary>
internal sealed class CaptureHubView : UserControl
{
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private bool _recording;
    private bool _gif;
    private bool _optionsOpen;
    private HotkeySettings? _workflow;
    private string _stepId = "save";
    private TaskSettings _effective = null!;

    public CaptureHubView()
    {
        Content = _scroll;
        Rebuild();
    }

    public bool IsRecording
    {
        get => _recording;
        set
        {
            if (_recording == value) return;
            _recording = value;
            _workflow = null;
            _stepId = "save";
            Rebuild();
        }
    }

    /// <summary>Shows a step or the capture options for the Default pipeline, e.g. when chosen in the command palette.</summary>
    public void Reveal(bool recording, string? stepId, bool regionOptions = false)
    {
        _recording = recording;
        _workflow = null;
        _stepId = stepId ?? _stepId;
        _optionsOpen |= regionOptions;
        Rebuild();
    }

    private static TaskSettings Defaults => ApplicationState.DefaultTaskSettings;
    private bool IsDefault => _workflow == null;
    private TaskSettings Edited => _workflow?.TaskSettings ?? Defaults;
    private IReadOnlyList<PipelineStep> Steps => _recording ? WorkflowPipeline.RecordingSteps : WorkflowPipeline.ScreenshotSteps;

    public void Rebuild()
    {
        if (ApplicationState.DefaultTaskSettings == null)
        {
            return;
        }

        if (_workflow != null && !Workflows().Contains(_workflow))
        {
            _workflow = null;
        }

        _effective = IsDefault ? Defaults : TaskSettings.GetSafeTaskSettings(_workflow!.TaskSettings);

        Vector offset = _scroll.Offset;
        StackPanel page = new() { Margin = new Thickness(24, 20, 24, 28), Spacing = 16, MaxWidth = 1280 };
        page.Children.Add(BuildHeader());

        Grid top = new() { ColumnDefinitions = new ColumnDefinitions("*,300"), ColumnSpacing = 16 };
        Control now = _recording ? BuildRecordNow() : BuildCaptureNow();
        Control shortcuts = BuildShortcuts();
        Grid.SetColumn(shortcuts, 1);
        top.Children.Add(now);
        top.Children.Add(shortcuts);
        page.Children.Add(top);
        page.Children.Add(BuildPipeline());

        _scroll.Content = page;
        Dispatcher.UIThread.Post(() => _scroll.Offset = offset, DispatcherPriority.Loaded);
    }

    private Control BuildHeader()
    {
        Grid header = new() { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 16 };
        header.Children.Add(HubUi.Text(Strings.MainMenuBuilder_Capture, "hub-title"));

        Control toggle = HubUi.Segmented(
        [
            (Strings.Hub_Screenshot, LucideIcons.camera, !_recording, () => IsRecording = false),
            (Strings.Hub_Recording, LucideIcons.video, _recording, () => IsRecording = true)
        ]);
        Grid.SetColumn(toggle, 1);
        header.Children.Add(toggle);

        Button folder = HubUi.Button(Strings.Hub_OpenScreenshotsFolder, () => MainMenuBuilder.Run(MainFormCommand.ScreenshotsFolder), LucideIcons.folder_open);
        Grid.SetColumn(folder, 3);
        header.Children.Add(folder);
        return header;
    }

    private Control BuildCaptureNow()
    {
        WrapPanel tiles = HubUi.Wrap();
        tiles.Children.Add(AreaTile(LucideIcons.scan, Strings.MainMenuBuilder_Region, HotkeyType.RectangleRegion,
            () => new CaptureRegion().Capture(true)));
        Button window = AreaTile(LucideIcons.app_window, Strings.MainMenuBuilder_Window, HotkeyType.ActiveWindow, null);
        window.Click += (_, _) => ShowWindowMenu(window);
        tiles.Children.Add(window);
        tiles.Children.Add(AreaTile(LucideIcons.monitor, Strings.MainMenuBuilder_Monitor, HotkeyType.ActiveMonitor,
            () => new CaptureActiveMonitor().Capture(true)));
        tiles.Children.Add(AreaTile(LucideIcons.maximize, Strings.MainMenuBuilder_Fullscreen, HotkeyType.PrintScreen,
            () => new CaptureFullscreen().Capture(true)));
        tiles.Children.Add(AreaTile(LucideIcons.layers, Strings.MainMenuBuilder_LastRegion, HotkeyType.LastRegion,
            () => new CaptureLastRegion().Capture(true)));
        Button scrolling = AreaTile(LucideIcons.scroll_text, Strings.MainMenuBuilder_ScrollingCapture, HotkeyType.ScrollingCapture, null);
        scrolling.Click += async (_, _) => await HubUi.RunAsync(() => TaskHelpers.OpenScrollingCapture());
        tiles.Children.Add(scrolling);

        TaskSettingsCapture capture = Defaults.CaptureSettings;
        decimal delay = capture.ScreenshotDelay;
        WrapPanel options = HubUi.Wrap(18);
        options.Children.Add(Labeled(Strings.Hub_Delay, HubUi.Segmented(new[] { 0, 3, 5 }.Select(seconds =>
            (seconds == 0 ? Strings.Hub_None : string.Format(Strings.Hub_SecondsShort, seconds), string.Empty,
             Math.Abs(delay - seconds) < 0.01m, (Action)(() => SetDefault("CaptureSettings.ScreenshotDelay", (decimal)seconds)))))));
        options.Children.Add(Labeled(Strings.MainMenuBuilder_ShowCursor,
            HubUi.Switch(capture.ShowCursor, value => SetDefault("CaptureSettings.ShowCursor", value), Strings.MainMenuBuilder_ShowCursor)));
        options.Children.Add(HubUi.LinkButton(_optionsOpen ? Strings.Hub_HideRegionOptions : Strings.Hub_RegionOptions, () =>
        {
            _optionsOpen = !_optionsOpen;
            Rebuild();
        }));

        Border card = HubUi.Card(HubUi.SectionHeader(Strings.Hub_CaptureNow, Strings.Hub_CaptureNowHint), tiles, options);

        if (_optionsOpen)
        {
            ((StackPanel)card.Child!).Children.Add(DefaultsGrid(WorkflowPipeline.RegionFields));
        }

        return card;
    }

    private Control BuildRecordNow()
    {
        ScreenRecordOutput output = _gif ? ScreenRecordOutput.GIF : ScreenRecordOutput.FFmpeg;
        WrapPanel tiles = HubUi.Wrap();
        tiles.Children.Add(AreaTile(LucideIcons.scan, Strings.MainMenuBuilder_Region,
            _gif ? HotkeyType.ScreenRecorderGIF : HotkeyType.ScreenRecorder,
            () => TaskHelpers.StartScreenRecording(output, ScreenRecordStartMethod.Region)));
        tiles.Children.Add(AreaTile(LucideIcons.app_window, Strings.Hub_ActiveWindow,
            _gif ? HotkeyType.ScreenRecorderGIFActiveWindow : HotkeyType.ScreenRecorderActiveWindow,
            () => TaskHelpers.StartScreenRecording(output, ScreenRecordStartMethod.ActiveWindow)));
        tiles.Children.Add(AreaTile(LucideIcons.square_dashed, Strings.Hub_CustomRegion,
            _gif ? HotkeyType.ScreenRecorderGIFCustomRegion : HotkeyType.ScreenRecorderCustomRegion,
            () => TaskHelpers.StartScreenRecording(output, ScreenRecordStartMethod.CustomRegion)));
        tiles.Children.Add(AreaTile(LucideIcons.layers, Strings.MainMenuBuilder_LastRegion,
            _gif ? HotkeyType.StartScreenRecorderGIF : HotkeyType.StartScreenRecorder,
            () => TaskHelpers.StartScreenRecording(output, ScreenRecordStartMethod.LastRegion)));

        Control format = Labeled(Strings.Hub_Format, HubUi.Segmented(
        [
            (Strings.Hub_VideoMp4, LucideIcons.video, !_gif, () => { _gif = false; Rebuild(); }),
            (Strings.Hub_Gif, LucideIcons.film, _gif, () => { _gif = true; Rebuild(); })
        ]));

        Button more = HubUi.LinkButton(Strings.Hub_AllRecordingSettings, () => MainMenuBuilder.Run(MainFormCommand.TaskSettings));

        return HubUi.Card(HubUi.SectionHeader(Strings.Hub_RecordNow, Strings.Hub_RecordNowHint, more), tiles, format,
            DefaultsGrid(WorkflowPipeline.RecordingFields(_gif)));
    }

    private Button AreaTile(string icon, string title, HotkeyType job, Action? click)
    {
        string? hotkey = FindHotkey(job);
        Button tile = HubUi.Tile(icon, title, hotkey ?? Strings.Hub_NoHotkey, false, click ?? (() => { }));
        tile.Width = 134;
        ToolTip.SetTip(tile, hotkey == null ? title : $"{HubUi.TrimEllipsis(title)} ({hotkey})");
        return tile;
    }

    private static string? FindHotkey(HotkeyType job) =>
        ApplicationState.HotkeysConfigOrNull?.Hotkeys?
            .FirstOrDefault(x => x.TaskSettings?.Job == job && x.HotkeyInfo.IsValidHotkey)?.HotkeyInfo.ToString();

    private void ShowWindowMenu(Control anchor)
    {
        ContextMenu menu = new();

        try
        {
            foreach (DesktopWindow window in DesktopPlatform.Current.GetVisibleWindows())
            {
                DesktopWindow target = window;
                MenuItem item = new() { Header = new TextBlock { Text = target.Title.Truncate(60, "...") } };
                item.Click += (_, _) => HubUi.Run(() => new CaptureWindow(target.Id).Capture(true));
                menu.Items.Add(item);
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }

        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = Strings.MainMenuBuilder_NoWindowsFound, IsEnabled = false });
        }

        menu.Open(anchor);
    }

    private Control BuildShortcuts()
    {
        StackPanel list = new() { Spacing = 6 };
        list.Children.Add(ShortcutRow(null));

        foreach (HotkeySettings workflow in Workflows())
        {
            list.Children.Add(ShortcutRow(workflow));
        }

        Button add = HubUi.Button(Strings.Hub_NewShortcut, () => MainMenuBuilder.Run(MainFormCommand.HotkeySettings), LucideIcons.plus);
        add.HorizontalAlignment = HorizontalAlignment.Stretch;
        add.Classes.Add("dashed");

        return HubUi.Card(HubUi.SectionHeader(Strings.Hub_Shortcuts, Strings.Hub_ShortcutsHint), list, add);
    }

    private IEnumerable<HotkeySettings> Workflows()
    {
        IEnumerable<HotkeySettings> hotkeys = ApplicationState.HotkeysConfigOrNull?.Hotkeys ?? Enumerable.Empty<HotkeySettings>();
        return hotkeys.Where(x => x.TaskSettings != null &&
            (_recording ? WorkflowPipeline.IsRecordingJob(x.TaskSettings.Job) : WorkflowPipeline.IsScreenshotJob(x.TaskSettings.Job)));
    }

    private Control ShortcutRow(HotkeySettings? workflow)
    {
        bool selected = workflow == _workflow;
        string title = workflow == null ? Strings.Hub_Default : workflow.TaskSettings.ToString();
        string subtitle = workflow == null ? Strings.Hub_DefaultHint : workflow.TaskSettings.Job.GetLocalizedDescription();
        int changes = workflow == null ? 0 : WorkflowOverrides.CountChanges(workflow.TaskSettings);

        Grid head = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        head.Children.Add(HubUi.Text(title, "hub-strong"));
        if (changes > 0)
        {
            TextBlock changed = HubUi.Text(changes == 1 ? Strings.Hub_OneChange : string.Format(Strings.Hub_NChanges, changes), "hub-accent-text");
            Grid.SetColumn(changed, 1);
            head.Children.Add(changed);
        }

        Grid sub = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        sub.Children.Add(HubUi.Text(subtitle, "hub-hint"));
        if (workflow != null && workflow.HotkeyInfo.IsValidHotkey)
        {
            Border key = HubUi.Keycap(workflow.HotkeyInfo.ToString());
            Grid.SetColumn(key, 1);
            sub.Children.Add(key);
        }

        StackPanel content = new() { Spacing = 3 };
        content.Children.Add(head);
        content.Children.Add(sub);

        Button select = new() { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch };
        select.Classes.Add("hub-row");
        select.Classes.Set("selected", selected);
        select.Click += (_, _) =>
        {
            _workflow = workflow;
            if (workflow != null && _recording)
            {
                _gif = WorkflowPipeline.IsGifJob(workflow.TaskSettings.Job);
            }
            Rebuild();
        };

        if (workflow == null)
        {
            return select;
        }

        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4 };
        row.Children.Add(select);
        Button run = HubUi.IconButton(LucideIcons.play, Strings.Hub_RunShortcut, () => { });
        run.Click += async (_, _) => await HubUi.RunAsync(() => TaskHelpers.ExecuteJob(workflow.TaskSettings));
        Grid.SetColumn(run, 1);
        row.Children.Add(run);
        return row;
    }

    private Control BuildPipeline()
    {
        Border editing = HubUi.Badge(string.Format(Strings.Hub_Editing, IsDefault ? Strings.Hub_Default : _workflow!.TaskSettings.ToString()), true);
        StackPanel titleRow = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
        titleRow.Children.Add(HubUi.Text(Strings.Hub_WhatHappensNext, "hub-section"));
        titleRow.Children.Add(editing);

        Button addStep = HubUi.Button(Strings.Hub_AllSteps, () => { }, LucideIcons.list_checks);
        addStep.Click += (_, _) => ShowAllStepsMenu(addStep);

        Grid header = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(titleRow);
        Grid.SetColumn(addStep, 1);
        header.Children.Add(addStep);

        WrapPanel chain = new() { ItemSpacing = 6, LineSpacing = 8 };
        chain.Children.Add(TriggerNode());

        foreach (PipelineStep step in Steps)
        {
            TextBlock arrow = HubUi.Icon(LucideIcons.chevron_right, 16);
            arrow.Classes.Add("hub-arrow");
            chain.Children.Add(arrow);
            chain.Children.Add(StepNode(step));
        }

        PipelineStep current = Steps.FirstOrDefault(x => x.Id == _stepId) ?? Steps[0];
        _stepId = current.Id;

        string hint = _recording ? Strings.Hub_PipelineSharedHint : Strings.Hub_PipelineHint;
        return HubUi.Card(header, HubUi.Text(hint, "hub-hint", wrap: true), chain, BuildDetail(current));
    }

    private Control TriggerNode()
    {
        string area = IsDefault
            ? Strings.Hub_AnyButton
            : _workflow!.TaskSettings.Job.GetLocalizedDescription() + (_workflow.HotkeyInfo.IsValidHotkey ? ", " + _workflow.HotkeyInfo : string.Empty);

        StackPanel content = new() { Spacing = 5 };
        TextBlock icon = HubUi.Icon(_recording ? LucideIcons.video : LucideIcons.camera, 18);
        icon.Classes.Add("hub-accent");
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        content.Children.Add(icon);
        content.Children.Add(HubUi.Text(_recording ? Strings.Hub_Recording : Strings.MainMenuBuilder_Capture, "hub-strong"));
        content.Children.Add(HubUi.Text(area, "hub-hint"));

        Border node = new() { Child = content };
        node.Classes.Add("hub-step");
        node.Classes.Add("trigger");
        return node;
    }

    private Control StepNode(PipelineStep step)
    {
        bool on = IsStepOn(step);
        bool selected = step.Id == _stepId;

        StackPanel content = new() { Spacing = 5 };
        TextBlock icon = HubUi.Icon(step.Icon, 18);
        icon.Classes.Add("hub-accent");
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        content.Children.Add(icon);

        StackPanel name = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
        name.Children.Add(HubUi.Text(step.Name, "hub-strong"));
        if (!IsDefault && IsStepChanged(step))
        {
            Border dot = new() { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
            dot.Classes.Add("hub-dot");
            ToolTip.SetTip(dot, Strings.Hub_ChangedForShortcut);
            name.Children.Add(dot);
        }
        content.Children.Add(name);
        content.Children.Add(HubUi.Text(on ? StepSummary(step) : Strings.Hub_Skipped, "hub-hint"));

        Button select = new() { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        select.Classes.Add("hub-step-button");
        select.Click += (_, _) =>
        {
            _stepId = step.Id;
            Rebuild();
        };

        Grid grid = new();
        grid.Children.Add(select);

        if (step.TogglePath != null)
        {
            ToggleSwitch toggle = HubUi.Switch(on, value => ToggleStep(step, value), step.Name);
            toggle.HorizontalAlignment = HorizontalAlignment.Right;
            toggle.VerticalAlignment = VerticalAlignment.Top;
            toggle.Margin = new Thickness(0, 6, 8, 0);
            grid.Children.Add(toggle);
        }

        Border node = new() { Child = grid };
        node.Classes.Add("hub-step");
        node.Classes.Set("selected", selected);
        node.Classes.Set("off", !on);
        return node;
    }

    private bool IsStepOn(PipelineStep step) =>
        step.TogglePath == null || WorkflowOverrides.GetValue(_effective, step.TogglePath) is true;

    private bool IsStepChanged(PipelineStep step)
    {
        TaskSettings settings = Edited;
        IEnumerable<string> paths = step.Fields.Select(x => x.Path);
        if (step.TogglePath != null) paths = paths.Append(step.TogglePath);

        return paths.Any(path => WorkflowOverrides.HasOverride(settings, path) || WorkflowOverrides.IsSectionOwned(settings, path)) ||
            (step.Fields.Any(x => x.Kind == PipelineFieldKind.ScreenshotsFolder) && settings.OverrideScreenshotsFolder);
    }

    private string StepSummary(PipelineStep step)
    {
        PipelineField? field = step.Fields.FirstOrDefault();
        if (field == null || (field.Kind == PipelineFieldKind.Value && WorkflowOverrides.GetValueType(_effective, field.Path) == typeof(bool)))
        {
            return step.Description;
        }

        return field.Kind switch
        {
            PipelineFieldKind.ScreenshotsFolder => ScreenshotsFolder(),
            PipelineFieldKind.ImageEffectPreset => EffectPresetName(),
            _ => Display(WorkflowOverrides.GetValue(_effective, field.Path))
        };
    }

    private void ToggleStep(PipelineStep step, bool value)
    {
        string path = step.TogglePath!;

        if (IsDefault || WorkflowOverrides.IsSectionOwned(Edited, path))
        {
            WorkflowOverrides.SetValue(Edited, path, value);
        }
        else if (WorkflowOverrides.GetValue(Defaults, path) is bool inherited && inherited == value)
        {
            WorkflowOverrides.ClearOverride(Edited, path);
        }
        else
        {
            WorkflowOverrides.SetOverride(Edited, path, value);
        }

        _stepId = step.Id;
        Save();
        Rebuild();
    }

    private Control BuildDetail(PipelineStep step)
    {
        bool on = IsStepOn(step);
        StackPanel title = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
        title.Children.Add(HubUi.Text(step.Name, "hub-section"));
        Border status = HubUi.Badge(on ? Strings.Hub_On : Strings.Hub_OffSkipped, false);
        status.Classes.Add(on ? "on" : "off");
        title.Children.Add(status);

        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(title);
        panel.Children.Add(HubUi.Text(step.Description, "hub-hint", wrap: true));
        panel.Children.Add(HubUi.Text(IsDefault ? Strings.Hub_ScopeDefault : string.Format(Strings.Hub_ScopeShortcut, _workflow!.TaskSettings), "hub-hint", wrap: true));

        foreach (PipelineField field in step.Fields)
        {
            panel.Children.Add(FieldRow(field));
        }

        Border detail = new() { Child = panel };
        detail.Classes.Add("hub-detail");
        return detail;
    }

    private Control FieldRow(PipelineField field)
    {
        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("200,*,Auto,Auto"), ColumnSpacing = 10, MinHeight = 36 };
        row.Classes.Add("hub-field");
        row.Children.Add(HubUi.Text(field.Label, wrap: true));

        bool folder = field.Kind == PipelineFieldKind.ScreenshotsFolder;
        bool owned = !IsDefault && !folder && WorkflowOverrides.IsSectionOwned(Edited, field.Path);
        bool custom = !IsDefault && (folder ? Edited.OverrideScreenshotsFolder : owned || WorkflowOverrides.HasOverride(Edited, field.Path));
        bool editable = IsDefault ? !folder : custom;

        Control editor = folder ? FolderEditor(editable) : Editor(field, editable);
        Grid.SetColumn(editor, 1);
        row.Children.Add(editor);

        if (!IsDefault)
        {
            Border badge = HubUi.Badge(custom ? Strings.Hub_ThisShortcut : Strings.Hub_Default, custom);
            badge.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(badge, 2);
            row.Children.Add(badge);

            Button action = HubUi.Button(custom ? (owned ? Strings.Hub_ResetSection : Strings.Hub_Reset) : Strings.Hub_Change,
                () => ChangeOrReset(field, custom, owned));
            if (owned) ToolTip.SetTip(action, Strings.Hub_ResetSectionHint);
            Grid.SetColumn(action, 3);
            row.Children.Add(action);
        }
        else if (folder)
        {
            Button change = HubUi.Button(Strings.Hub_Change, () => MainMenuBuilder.Run(MainFormCommand.ApplicationSettings));
            ToolTip.SetTip(change, Strings.Hub_FolderInSettingsHint);
            Grid.SetColumn(change, 3);
            row.Children.Add(change);
        }

        return row;
    }

    private void ChangeOrReset(PipelineField field, bool custom, bool owned)
    {
        TaskSettings settings = Edited;

        if (field.Kind == PipelineFieldKind.ScreenshotsFolder)
        {
            if (!custom)
            {
                settings.ScreenshotsFolder = ScreenshotsFolder();
            }

            settings.OverrideScreenshotsFolder = !custom;
        }
        else if (owned)
        {
            WorkflowOverrides.SetSectionToDefault(settings, field.Path);
        }
        else if (custom)
        {
            WorkflowOverrides.ClearOverride(settings, field.Path);
        }
        else
        {
            WorkflowOverrides.SetOverride(settings, field.Path, WorkflowOverrides.GetValue(_effective, field.Path));
        }

        Save();
        Rebuild();
    }

    private Control FolderEditor(bool editable)
    {
        TextBox box = new() { Text = editable ? Edited.ScreenshotsFolder : ScreenshotsFolder(), IsReadOnly = !editable };
        box.Classes.Add("hub-input");
        box.Classes.Add("mono");

        if (editable)
        {
            box.LostFocus += (_, _) =>
            {
                Edited.ScreenshotsFolder = box.Text ?? string.Empty;
                Save();
            };
        }

        return box;
    }

    private Control Editor(PipelineField field, bool editable)
    {
        if (field.Kind == PipelineFieldKind.ImageEffectPreset)
        {
            List<ImageEffectPreset> presets = _effective.ImageSettings.ImageEffectPresets ?? new List<ImageEffectPreset>();
            ComboBox combo = new()
            {
                ItemsSource = presets.Select(x => x?.ToString() ?? string.Empty).ToArray(),
                SelectedIndex = Math.Clamp(_effective.ImageSettings.SelectedImageEffectPreset, -1, presets.Count - 1),
                IsEnabled = editable,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedIndex >= 0) Write(field, combo.SelectedIndex, rebuild: true);
            };
            return combo;
        }

        Type? type = WorkflowOverrides.GetValueType(_effective, field.Path);
        object? value = WorkflowOverrides.GetValue(_effective, field.Path);
        Control editor;

        if (type == typeof(bool))
        {
            ToggleSwitch toggle = HubUi.Switch(value is true, x => Write(field, x, rebuild: true), field.Label);
            toggle.HorizontalAlignment = HorizontalAlignment.Left;
            editor = toggle;
        }
        else if (type != null && type.IsEnum)
        {
            editor = HubUi.EnumCombo(type, value, x => Write(field, x, rebuild: true));
        }
        else if (type == typeof(string))
        {
            TextBox box = new() { Text = value as string ?? string.Empty };
            box.Classes.Add("hub-input");
            if (field.Monospace) box.Classes.Add("mono");
            box.LostFocus += (_, _) => Write(field, box.Text ?? string.Empty, rebuild: false);
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) Write(field, box.Text ?? string.Empty, rebuild: true);
            };
            editor = box;
        }
        else if (type == typeof(int) || type == typeof(decimal) || type == typeof(float) || type == typeof(double))
        {
            NumericUpDown number = new()
            {
                Value = value == null ? 0 : Convert.ToDecimal(value),
                Minimum = 0,
                Maximum = 100000,
                Increment = type == typeof(int) ? 1 : 0.5m,
                FormatString = type == typeof(int) ? "0" : "0.#",
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 140
            };
            number.ValueChanged += (_, e) =>
            {
                if (e.NewValue is decimal newValue) Write(field, Convert.ChangeType(newValue, type), rebuild: false);
            };
            editor = number;
        }
        else
        {
            editor = HubUi.Text(Display(value), "hub-hint");
        }

        editor.IsEnabled = editable;
        return editor;
    }

    private void Write(PipelineField field, object? value, bool rebuild)
    {
        TaskSettings settings = Edited;

        if (IsDefault || WorkflowOverrides.IsSectionOwned(settings, field.Path))
        {
            WorkflowOverrides.SetValue(settings, field.Path, value);
        }
        else
        {
            WorkflowOverrides.SetOverride(settings, field.Path, value);
        }

        Save();

        if (rebuild)
        {
            Rebuild();
        }
        else if (!IsDefault)
        {
            _effective = TaskSettings.GetSafeTaskSettings(settings);
        }
    }

    private void SetDefault(string path, object value)
    {
        WorkflowOverrides.SetValue(Defaults, path, value);
        SettingManager.SaveApplicationConfigAsync();
        Rebuild();
    }

    /// <summary>Rows that edit the Default settings directly (capture options shown next to the capture buttons).</summary>
    private Control DefaultsGrid(IEnumerable<PipelineField> fields)
    {
        StackPanel panel = new() { Spacing = 6 };

        foreach (PipelineField field in fields)
        {
            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("200,*"), ColumnSpacing = 10, MinHeight = 34 };
            row.Children.Add(HubUi.Text(field.Label));
            Type? type = WorkflowOverrides.GetValueType(Defaults, field.Path);
            object? value = WorkflowOverrides.GetValue(Defaults, field.Path);
            Control editor;

            if (type == typeof(bool))
            {
                editor = HubUi.Switch(value is true, x => SetDefault(field.Path, x), field.Label);
            }
            else if (type != null && type.IsEnum)
            {
                editor = HubUi.EnumCombo(type, value, x => SetDefault(field.Path, x));
            }
            else if (type != null)
            {
                NumericUpDown number = new()
                {
                    Value = value == null ? 0 : Convert.ToDecimal(value),
                    Minimum = 0,
                    Maximum = 1000,
                    Increment = type == typeof(int) ? 1 : 0.5m,
                    FormatString = type == typeof(int) ? "0" : "0.#",
                    MinWidth = 140
                };
                Type valueType = type;
                number.ValueChanged += (_, e) =>
                {
                    if (e.NewValue is decimal newValue)
                    {
                        WorkflowOverrides.SetValue(Defaults, field.Path, Convert.ChangeType(newValue, valueType));
                        SettingManager.SaveApplicationConfigAsync();
                    }
                };
                editor = number;
            }
            else
            {
                continue;
            }

            editor.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(editor, 1);
            row.Children.Add(editor);
            panel.Children.Add(row);
        }

        Border box = new() { Child = panel };
        box.Classes.Add("hub-inset");
        return box;
    }

    private void ShowAllStepsMenu(Control anchor)
    {
        ContextMenu menu = new();
        bool uploads = !SystemOptions.DisableUpload;

        foreach ((AfterCaptureTasks task, string header, string _) in MainMenuBuilder.GetAfterCaptureTaskMenuOptions(uploads))
        {
            menu.Items.Add(StepMenuItem(nameof(TaskSettings.AfterCaptureJob) + "." + task, header));
        }

        if (uploads)
        {
            menu.Items.Add(new Separator());

            foreach ((AfterUploadTasks task, string header, string _) in MainMenuBuilder.GetAfterUploadTaskMenuOptions())
            {
                menu.Items.Add(StepMenuItem(nameof(TaskSettings.AfterUploadJob) + "." + task, header));
            }
        }

        menu.Open(anchor);
    }

    private MenuItem StepMenuItem(string path, string header)
    {
        bool on = WorkflowOverrides.GetValue(_effective, path) is true;
        MenuItem item = new()
        {
            Header = new TextBlock { Text = header },
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = on
        };
        item.Click += (_, _) => ToggleStep(new PipelineStep(_stepId, header, string.Empty, path, string.Empty, []), !on);
        return item;
    }

    private string ScreenshotsFolder()
    {
        try
        {
            return TaskHelpers.GetScreenshotsFolder(_effective);
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
            return string.Empty;
        }
    }

    private string EffectPresetName()
    {
        List<ImageEffectPreset>? presets = _effective.ImageSettings.ImageEffectPresets;
        int index = _effective.ImageSettings.SelectedImageEffectPreset;
        return presets != null && index >= 0 && index < presets.Count ? presets[index]?.ToString() ?? string.Empty : string.Empty;
    }

    private static string Display(object? value) => value switch
    {
        null => string.Empty,
        Enum e => e.GetLocalizedDescription(),
        bool b => b ? Strings.Hub_On : Strings.Hub_Off,
        _ => value.ToString() ?? string.Empty
    };

    private static Control Labeled(string label, Control control)
    {
        StackPanel panel = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(HubUi.Text(label, "hub-label"));
        panel.Children.Add(control);
        return panel;
    }

    private void Save()
    {
        if (IsDefault)
        {
            SettingManager.SaveApplicationConfigAsync();
        }
        else
        {
            SettingManager.SaveHotkeysConfigAsync();
        }
    }
}
