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
using Capkit.Localization;
using System;
using System.Linq;

namespace Capkit;

/// <summary>The Tools hub: every tool as a tile, filtered by a search box.</summary>
internal sealed class ToolsHubView : UserControl
{
    private readonly TextBox _search;
    private readonly StackPanel _categories = new() { Spacing = 16 };

    public ToolsHubView()
    {
        _search = new TextBox { Watermark = Strings.Hub_SearchTools, Width = 420, HorizontalAlignment = HorizontalAlignment.Left };
        _search.Classes.Add("hub-input");
        _search.TextChanged += (_, _) => Filter();

        StackPanel page = new() { Margin = new Thickness(24, 20, 24, 28), Spacing = 16, MaxWidth = 1280 };
        page.Children.Add(HubUi.Text(Strings.MainMenuBuilder_Tools, "hub-title"));
        page.Children.Add(_search);
        page.Children.Add(_categories);

        Content = new ScrollViewer
        {
            Content = page,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        Filter();
    }

    public void FocusSearch()
    {
        _search.Focus();
    }

    private void Filter()
    {
        string query = _search.Text?.Trim() ?? string.Empty;
        _categories.Children.Clear();

        foreach (MainMenuCategory category in MainMenuBuilder.BuildToolCategories())
        {
            MainMenuEntry[] entries = category.Entries
                .Where(x => query.Length == 0 || x.Header.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    category.Header.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToArray();

            if (entries.Length == 0)
            {
                continue;
            }

            WrapPanel tiles = HubUi.Wrap();

            foreach (MainMenuEntry entry in entries)
            {
                MainMenuEntry current = entry;
                Button tile = HubUi.Tile(current.Icon, current.Header, null, false, () => { });
                tile.Width = 160;
                tile.Click += async (_, _) =>
                {
                    if (current.ExecuteAsync != null) await HubUi.RunAsync(current.ExecuteAsync);
                };
                tiles.Children.Add(tile);
            }

            _categories.Children.Add(HubUi.Card(HubUi.SectionHeader(category.Header), tiles));
        }

        if (_categories.Children.Count == 0)
        {
            _categories.Children.Add(HubUi.Text(Strings.Hub_NoResults, "hub-hint"));
        }
    }
}
