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
using Avalonia.Layout;
using Capkit.AvaloniaUI.Theming;
using Capkit.HelpersLib;
using Capkit.Localization;
using Capkit.UploadersLib;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capkit;

internal enum MainHub
{
    Capture,
    Share,
    Tools,
    History
}

public partial class MainWindow
{
    private readonly Dictionary<MainHub, Button> _hubButtons = new();
    private MainHub _hub = MainHub.Capture;
    private CaptureHubView? _captureHub;
    private ShareHubView? _shareHub;
    private ToolsHubView? _toolsHub;
    private CommandPalette? _palette;

    private void BuildNavigation()
    {
        NavigationPanel.Children.Clear();
        _hubButtons.Clear();

        Button search = new()
        {
            Content = CreateSearchContent(),
            Margin = new Thickness(0, 4, 0, 10)
        };
        search.Classes.Add("nav-button");
        ToolTip.SetTip(search, Strings.Hub_PaletteWatermark);
        search.Click += (_, _) => OpenCommandPalette();
        NavigationPanel.Children.Add(search);

        AddHubButton(MainHub.Capture, Strings.MainMenuBuilder_Capture, LucideIcons.camera);
        if (!SystemOptions.DisableUpload)
        {
            AddHubButton(MainHub.Share, Strings.Hub_Share, LucideIcons.share_2);
        }
        AddHubButton(MainHub.Tools, Strings.MainMenuBuilder_Tools, LucideIcons.wrench);
        AddHubButton(MainHub.History, Strings.MainMenuBuilder_History, LucideIcons.history);

        NavigationPanel.Children.Add(new Separator { Margin = new Thickness(5, 8) });

        foreach (MainNavigationSection section in _navigationMenuBuilder.BuildSecondaryNavigation().Where(x => x.IsVisible))
        {
            Button button = new()
            {
                Content = CreateNavigationContent(section),
                Tag = section
            };
            button.Classes.Add("nav-button");
            button.Click += OnNavigationClick;
            NavigationPanel.Children.Add(button);
        }

        UpdateHubButtons();
    }

    private void AddHubButton(MainHub hub, string header, string icon)
    {
        Button button = new()
        {
            Content = CreateNavigationContent(new MainNavigationSection(HubUi.TrimEllipsis(header), icon, () => ShowHub(hub))),
            Height = 36
        };
        button.Classes.Add("nav-button");
        button.Click += (_, _) => ShowHub(hub);
        _hubButtons[hub] = button;
        NavigationPanel.Children.Add(button);
    }

    private static Control CreateSearchContent()
    {
        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("20,7,*,Auto") };
        grid.Children.Add(CreateAccentMenuIcon(LucideIcons.search, 15));

        TextBlock label = new()
        {
            Text = Strings.Hub_Search,
            FontWeight = Avalonia.Media.FontWeight.Normal,
            Opacity = 0.75,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 2);
        grid.Children.Add(label);

        Border key = HubUi.Keycap("Ctrl K");
        Grid.SetColumn(key, 3);
        grid.Children.Add(key);
        return grid;
    }

    private void ShowHub(MainHub hub)
    {
        if (hub == MainHub.Share && SystemOptions.DisableUpload)
        {
            hub = MainHub.Capture;
        }

        _hub = hub;
        HistoryView.IsVisible = hub == MainHub.History;
        HubHost.IsVisible = hub != MainHub.History;

        switch (hub)
        {
            case MainHub.Capture:
                _captureHub ??= new CaptureHubView();
                _captureHub.Rebuild();
                HubHost.Content = _captureHub;
                break;
            case MainHub.Share:
                _shareHub ??= new ShareHubView();
                _shareHub.Rebuild();
                HubHost.Content = _shareHub;
                break;
            case MainHub.Tools:
                _toolsHub ??= new ToolsHubView();
                HubHost.Content = _toolsHub;
                break;
            default:
                HubHost.Content = null;
                break;
        }

        UpdateHubButtons();
    }

    private void ShowCaptureHub(bool recording)
    {
        ShowHub(MainHub.Capture);
        _captureHub!.IsRecording = recording;
    }

    private void UpdateHubButtons()
    {
        foreach ((MainHub hub, Button button) in _hubButtons)
        {
            button.Classes.Set("active", hub == _hub);
        }
    }

    private void RefreshHubs()
    {
        _hubSignature = HubSignature();
        _captureHub?.Rebuild();
        _shareHub?.Rebuild();
    }

    private string? _hubSignature;

    private void RefreshHubsIfChanged()
    {
        if (HubSignature() != _hubSignature)
        {
            RefreshHubs();
        }
    }

    /// <summary>What the hubs show from settings edited elsewhere; rebuilding without a change would reset text input.</summary>
    private static string HubSignature()
    {
        TaskSettings? defaults = ApplicationState.DefaultTaskSettings;
        IEnumerable<string> hotkeys = (ApplicationState.HotkeysConfigOrNull?.Hotkeys ?? Enumerable.Empty<HotkeySettings>())
            .Select(x => $"{x.TaskSettings?.Job}|{x.TaskSettings?.Description}|{x.HotkeyInfo}|{(x.TaskSettings == null ? 0 : WorkflowOverrides.CountChanges(x.TaskSettings))}");

        return defaults == null ? string.Empty : string.Join(";", hotkeys.Prepend(
            $"{defaults.AfterCaptureJob}|{defaults.AfterUploadJob}|{defaults.ImageDestination}|{defaults.FileDestination}|" +
            $"{defaults.UploadSettings?.NameFormatPattern}|{defaults.ImageSettings?.ImageFormat}|{ApplicationState.Settings?.CustomScreenshotsPath}"));
    }

    private void OpenCommandPalette()
    {
        if (_palette == null)
        {
            _palette = new CommandPalette(BuildPaletteCommands);
            _palette.Dismissed += (_, _) => PaletteHost.IsVisible = false;
            PaletteHost.Content = _palette;
        }

        PaletteHost.IsVisible = true;
        _palette.Open();
    }

    private IReadOnlyList<PaletteCommand> BuildPaletteCommands()
    {
        string go = Strings.Hub_GoTo;
        List<PaletteCommand> commands =
        [
            new(Strings.Hub_Screenshot, go, LucideIcons.camera, () => Done(() => ShowCaptureHub(false))),
            new(Strings.Hub_Recording, go, LucideIcons.video, () => Done(() => ShowCaptureHub(true))),
            new(HubUi.TrimEllipsis(Strings.MainMenuBuilder_Tools), go, LucideIcons.wrench, () => Done(() => ShowHub(MainHub.Tools))),
            new(HubUi.TrimEllipsis(Strings.MainMenuBuilder_History), go, LucideIcons.history, () => Done(() => ShowHub(MainHub.History)))
        ];

        if (!SystemOptions.DisableUpload)
        {
            commands.Add(new(Strings.Hub_Share, go, LucideIcons.share_2, () => Done(() => ShowHub(MainHub.Share))));
        }

        AddSettingCommands(commands);

        foreach ((string category, MainMenuEntry entry) in _navigationMenuBuilder.BuildSearchEntries())
        {
            commands.Add(new(entry.Header, category, string.IsNullOrEmpty(entry.Icon) ? LucideIcons.circle : entry.Icon, entry.ExecuteAsync!));
        }

        return commands;
    }

    /// <summary>Pipeline steps and their settings, so "jpeg" or "file name" lands on the step that holds them.</summary>
    private void AddSettingCommands(List<PaletteCommand> commands)
    {
        string setting = Strings.Hub_Setting;

        void AddSteps(IReadOnlyList<PipelineStep> steps, bool recording)
        {
            string area = recording ? Strings.Hub_Recording : Strings.Hub_Screenshot;

            foreach (PipelineStep step in steps)
            {
                commands.Add(new($"{step.Name} ({area})", Strings.Hub_Step, step.Icon, () => Done(() => RevealCapture(recording, step.Id))));

                foreach (PipelineField field in step.Fields)
                {
                    commands.Add(new($"{field.Label} ({step.Name}, {area})", setting, step.Icon, () => Done(() => RevealCapture(recording, step.Id))));
                }
            }
        }

        AddSteps(WorkflowPipeline.ScreenshotSteps, false);
        AddSteps(WorkflowPipeline.RecordingSteps, true);

        foreach (PipelineField field in WorkflowPipeline.RegionFields)
        {
            commands.Add(new($"{field.Label} ({Strings.Hub_RegionOptions})", setting, LucideIcons.scan, () => Done(() => RevealCapture(false, null, true))));
        }

        foreach (PipelineField field in WorkflowPipeline.RecordingFields(false).Concat(WorkflowPipeline.RecordingFields(true)).DistinctBy(x => x.Label))
        {
            commands.Add(new($"{field.Label} ({Strings.Hub_Recording})", setting, LucideIcons.video, () => Done(() => RevealCapture(true, null))));
        }

        if (!SystemOptions.DisableUpload)
        {
            foreach (string label in new[] { Strings.Hub_ImageUploader, Strings.Hub_TextUploader, Strings.Hub_FileUploader, Strings.Hub_UrlShortener, Strings.Hub_UrlSharing, Strings.Hub_AfterUpload })
            {
                commands.Add(new($"{label} ({Strings.Hub_Share})", setting, LucideIcons.share_2, () => Done(() => ShowHub(MainHub.Share))));
            }
        }
    }

    private void RevealCapture(bool recording, string? stepId, bool regionOptions = false)
    {
        ShowHub(MainHub.Capture);
        _captureHub!.Reveal(recording, stepId, regionOptions);
    }

    private static Task Done(System.Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
