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
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Capkit.HelpersLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capkit;

/// <summary>Small control builders shared by the main window hubs. Styling lives in MainWindow.axaml.</summary>
internal static class HubUi
{
    public static TextBlock Icon(string glyph, double size = 16)
    {
        TextBlock icon = new()
        {
            Text = glyph,
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.Classes.Add("icon");
        return icon;
    }

    public static TextBlock Text(string text, string? styleClass = null, bool wrap = false)
    {
        TextBlock block = new()
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis
        };

        if (styleClass != null)
        {
            block.Classes.Add(styleClass);
        }

        return block;
    }

    public static Border Card(params Control[] children)
    {
        StackPanel panel = new() { Spacing = 12 };
        foreach (Control child in children)
        {
            panel.Children.Add(child);
        }

        Border card = new() { Child = panel };
        card.Classes.Add("hub-card");
        return card;
    }

    public static Control SectionHeader(string title, string? hint = null, Control? right = null)
    {
        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        StackPanel text = new() { Spacing = 2 };
        text.Children.Add(Text(title, "hub-section"));

        if (!string.IsNullOrEmpty(hint))
        {
            text.Children.Add(Text(hint, "hub-hint", wrap: true));
        }

        grid.Children.Add(text);

        if (right != null)
        {
            right.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
        }

        return grid;
    }

    public static Button Tile(string icon, string title, string? subtitle, bool selected, Action click, string? tooltip = null)
    {
        StackPanel content = new() { Spacing = 6 };
        TextBlock iconBlock = Icon(icon, 22);
        iconBlock.HorizontalAlignment = HorizontalAlignment.Left;
        iconBlock.Classes.Add("hub-accent");
        content.Children.Add(iconBlock);
        content.Children.Add(Text(TrimEllipsis(title), "hub-strong"));

        if (subtitle != null)
        {
            content.Children.Add(Text(subtitle, "hub-hint"));
        }

        Button button = new() { Content = content };
        button.Classes.Add("hub-tile");
        button.Classes.Set("selected", selected);
        button.Click += (_, _) => Run(click);

        if (tooltip != null)
        {
            ToolTip.SetTip(button, tooltip);
        }

        return button;
    }

    public static Control Segmented(IEnumerable<(string Label, string Icon, bool Active, Action Click)> segments)
    {
        StackPanel panel = new() { Orientation = Orientation.Horizontal, Spacing = 2 };

        foreach ((string label, string icon, bool active, Action click) in segments)
        {
            StackPanel content = new() { Orientation = Orientation.Horizontal, Spacing = 7 };
            if (!string.IsNullOrEmpty(icon))
            {
                content.Children.Add(Icon(icon, 14));
            }

            content.Children.Add(Text(label));
            Button segment = new() { Content = content };
            segment.Classes.Add("hub-segment");
            segment.Classes.Set("active", active);
            segment.Click += (_, _) => Run(click);
            panel.Children.Add(segment);
        }

        Border frame = new() { Child = panel, HorizontalAlignment = HorizontalAlignment.Left };
        frame.Classes.Add("hub-segmented");
        return frame;
    }

    public static Button Button(string text, Action click, string? icon = null)
    {
        Button button = new() { Content = ButtonContent(text, icon) };
        button.Classes.Add("hub-button");
        button.Click += (_, _) => Run(click);
        return button;
    }

    public static Button Button(string text, Func<Task> click, string? icon = null)
    {
        Button button = new() { Content = ButtonContent(text, icon) };
        button.Classes.Add("hub-button");
        button.Click += async (_, _) => await RunAsync(click);
        return button;
    }

    public static Button IconButton(string icon, string tooltip, Action click)
    {
        Button button = new() { Content = Icon(icon, 15) };
        button.Classes.Add("hub-icon-button");
        ToolTip.SetTip(button, tooltip);
        Avalonia.Automation.AutomationProperties.SetName(button, tooltip);
        button.Click += (_, _) => Run(click);
        return button;
    }

    public static Button LinkButton(string text, Action click)
    {
        Button button = new() { Content = Text(text) };
        button.Classes.Add("hub-link");
        button.Click += (_, _) => Run(click);
        return button;
    }

    public static ToggleSwitch Switch(bool value, Action<bool> changed, string? accessibleName = null)
    {
        ToggleSwitch toggle = new()
        {
            IsChecked = value,
            OnContent = null,
            OffContent = null,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center
        };
        toggle.Classes.Add("hub-switch");

        if (accessibleName != null)
        {
            Avalonia.Automation.AutomationProperties.SetName(toggle, accessibleName);
        }

        toggle.IsCheckedChanged += (_, _) => Run(() => changed(toggle.IsChecked == true));
        return toggle;
    }

    public static Border Badge(string text, bool accent)
    {
        Border badge = new() { Child = Text(text) };
        badge.Classes.Add("hub-badge");
        badge.Classes.Set("accent", accent);
        return badge;
    }

    public static Border Keycap(string text)
    {
        Border key = new() { Child = Text(text) };
        key.Classes.Add("hub-keycap");
        return key;
    }

    public static WrapPanel Wrap(double spacing = 8)
    {
        return new WrapPanel { ItemSpacing = spacing, LineSpacing = spacing };
    }

    public static UniformGrid TileGrid(int columns) => new() { Columns = columns };

    public static ComboBox EnumCombo(Type enumType, object? value, Action<object> changed)
    {
        Enum[] values = Enum.GetValues(enumType).Cast<Enum>().ToArray();
        ComboBox combo = new()
        {
            ItemsSource = values.Select(x => x.GetLocalizedDescription()).ToArray(),
            SelectedIndex = Array.FindIndex(values, x => x.Equals(value)),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
            {
                Run(() => changed(values[combo.SelectedIndex]));
            }
        };
        return combo;
    }

    public static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }

    public static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }

    private static Control ButtonContent(string text, string? icon)
    {
        if (string.IsNullOrEmpty(icon))
        {
            return Text(text);
        }

        StackPanel content = new() { Orientation = Orientation.Horizontal, Spacing = 7 };
        content.Children.Add(Icon(icon, 14));
        content.Children.Add(Text(text));
        return content;
    }

    /// <summary>Menu labels end in "..." when they open a dialog; tiles and hub names read better without it.</summary>
    public static string TrimEllipsis(string text) => text.TrimEnd('.', '…').TrimEnd();
}
