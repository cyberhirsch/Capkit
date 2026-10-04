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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Capkit;

/// <summary>The Share hub: upload actions, where uploads go, and what happens after an upload.</summary>
internal sealed class ShareHubView : UserControl
{
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

    public ShareHubView()
    {
        Content = _scroll;
        Rebuild();
    }

    private static TaskSettings Defaults => ApplicationState.DefaultTaskSettings;

    public void Rebuild()
    {
        if (ApplicationState.DefaultTaskSettings == null)
        {
            return;
        }

        Vector offset = _scroll.Offset;
        StackPanel page = new() { Margin = new Thickness(24, 20, 24, 28), Spacing = 16, MaxWidth = 1280 };
        page.Children.Add(HubUi.Text(Strings.Hub_Share, "hub-title"));

        if (SystemOptions.DisableUpload)
        {
            page.Children.Add(HubUi.Card(HubUi.Text(Strings.YourSystemAdminDisabledTheUploadFeature, wrap: true)));
            _scroll.Content = page;
            return;
        }

        Grid top = new() { ColumnDefinitions = new ColumnDefinitions("*,300"), ColumnSpacing = 16 };
        top.Children.Add(BuildUploadNow());
        Control shortcuts = BuildShortcuts();
        Grid.SetColumn(shortcuts, 1);
        top.Children.Add(shortcuts);
        page.Children.Add(top);
        page.Children.Add(BuildDestinations());
        page.Children.Add(BuildAfterUpload());

        _scroll.Content = page;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _scroll.Offset = offset, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private static Control BuildUploadNow()
    {
        WrapPanel tiles = HubUi.Wrap();

        foreach (MainMenuEntry entry in MainMenuBuilder.BuildUploadMenu().Where(x => !x.IsSeparator && x.IsVisible && x.ExecuteAsync != null))
        {
            MainMenuEntry current = entry;
            Button tile = HubUi.Tile(current.Icon, current.Header, null, false, () => { });
            tile.Width = 150;
            tile.Click += async (_, _) => await HubUi.RunAsync(current.ExecuteAsync!);
            tiles.Children.Add(tile);
        }

        return HubUi.Card(HubUi.SectionHeader(Strings.Hub_UploadNow, Strings.Hub_UploadNowHint), tiles);
    }

    private static Control BuildShortcuts()
    {
        StackPanel list = new() { Spacing = 6 };
        IEnumerable<HotkeySettings> workflows = (ApplicationState.HotkeysConfigOrNull?.Hotkeys ?? Enumerable.Empty<HotkeySettings>())
            .Where(x => x.TaskSettings != null && x.TaskSettings.Job >= HotkeyType.FileUpload && x.TaskSettings.Job <= HotkeyType.ShortenURL);

        foreach (HotkeySettings workflow in workflows)
        {
            HotkeySettings current = workflow;
            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            row.Children.Add(HubUi.Text(current.TaskSettings.ToString(), "hub-strong"));
            if (current.HotkeyInfo.IsValidHotkey)
            {
                Border key = HubUi.Keycap(current.HotkeyInfo.ToString());
                Grid.SetColumn(key, 1);
                row.Children.Add(key);
            }

            Button run = new() { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch };
            run.Classes.Add("hub-row");
            ToolTip.SetTip(run, Strings.Hub_RunShortcut);
            run.Click += async (_, _) => await HubUi.RunAsync(() => TaskHelpers.ExecuteJob(current.TaskSettings));
            list.Children.Add(run);
        }

        if (list.Children.Count == 0)
        {
            list.Children.Add(HubUi.Text(Strings.Hub_NoUploadShortcuts, "hub-hint", wrap: true));
        }

        Button edit = HubUi.Button(Strings.Hub_NewShortcut, () => MainMenuBuilder.Run(MainFormCommand.HotkeySettings), LucideIcons.plus);
        edit.HorizontalAlignment = HorizontalAlignment.Stretch;
        edit.Classes.Add("dashed");
        return HubUi.Card(HubUi.SectionHeader(Strings.Hub_Shortcuts, Strings.Hub_UploadShortcutsHint), list, edit);
    }

    private Control BuildDestinations()
    {
        StackPanel rows = new() { Spacing = 6 };
        rows.Children.Add(EnumRow(Strings.Hub_ImageUploader, nameof(TaskSettings.ImageDestination)));
        if (Defaults.ImageDestination == ImageDestination.FileUploader)
        {
            rows.Children.Add(EnumRow(Strings.Hub_ImageFileUploader, nameof(TaskSettings.ImageFileDestination)));
        }

        rows.Children.Add(EnumRow(Strings.Hub_TextUploader, nameof(TaskSettings.TextDestination)));
        if (Defaults.TextDestination == TextDestination.FileUploader)
        {
            rows.Children.Add(EnumRow(Strings.Hub_TextFileUploader, nameof(TaskSettings.TextFileDestination)));
        }

        rows.Children.Add(EnumRow(Strings.Hub_FileUploader, nameof(TaskSettings.FileDestination)));
        rows.Children.Add(EnumRow(Strings.Hub_UrlShortener, nameof(TaskSettings.URLShortenerDestination)));
        rows.Children.Add(EnumRow(Strings.Hub_UrlSharing, nameof(TaskSettings.URLSharingServiceDestination)));

        StackPanel buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(HubUi.Button(Strings.Hub_Accounts, () => MainMenuBuilder.Run(MainFormCommand.DestinationSettings), LucideIcons.cloud_cog));
        buttons.Children.Add(HubUi.Button(Strings.Hub_CustomUploaders, () => MainMenuBuilder.Run(MainFormCommand.CustomUploaderSettings), LucideIcons.cloud));

        return HubUi.Card(HubUi.SectionHeader(Strings.Hub_Destinations, Strings.Hub_DestinationsHint, buttons), rows);
    }

    private Control EnumRow(string label, string path)
    {
        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("200,320"), ColumnSpacing = 10, MinHeight = 34 };
        row.Children.Add(HubUi.Text(label));
        Type type = WorkflowOverrides.GetValueType(Defaults, path)!;
        ComboBox combo = HubUi.EnumCombo(type, WorkflowOverrides.GetValue(Defaults, path), value =>
        {
            WorkflowOverrides.SetValue(Defaults, path, value);
            SettingManager.SaveApplicationConfigAsync();
            Rebuild();
        });
        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);
        return row;
    }

    private Control BuildAfterUpload()
    {
        WrapPanel toggles = HubUi.Wrap(10);

        foreach ((AfterUploadTasks task, string header, string icon) in MainMenuBuilder.GetAfterUploadTaskMenuOptions())
        {
            string path = nameof(TaskSettings.AfterUploadJob) + "." + task;
            StackPanel item = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
            TextBlock glyph = HubUi.Icon(icon, 15);
            glyph.Classes.Add("hub-accent");
            item.Children.Add(glyph);
            item.Children.Add(HubUi.Text(header));
            item.Children.Add(HubUi.Switch(Defaults.AfterUploadJob.HasFlag(task), value =>
            {
                WorkflowOverrides.SetValue(Defaults, path, value);
                SettingManager.SaveApplicationConfigAsync();
            }, header));

            Border chip = new() { Child = item };
            chip.Classes.Add("hub-inset");
            toggles.Children.Add(chip);
        }

        return HubUi.Card(HubUi.SectionHeader(Strings.Hub_AfterUpload, Strings.Hub_AfterUploadHint), toggles);
    }
}
