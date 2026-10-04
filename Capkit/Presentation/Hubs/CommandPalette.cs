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
using Capkit.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capkit;

internal sealed record PaletteCommand(string Title, string Category, string Icon, Func<Task> Execute);

/// <summary>Ctrl+K overlay that finds and runs any action, tool, shortcut or settings page by name.</summary>
internal sealed class CommandPalette : UserControl
{
    private const int MaxResults = 12;

    private readonly TextBox _query;
    private readonly StackPanel _results = new() { Spacing = 2 };
    private readonly Func<IReadOnlyList<PaletteCommand>> _createCommands;
    private IReadOnlyList<PaletteCommand> _commands = Array.Empty<PaletteCommand>();
    private List<PaletteCommand> _matches = new();
    private int _selected;

    public event EventHandler? Dismissed;

    public CommandPalette(Func<IReadOnlyList<PaletteCommand>> createCommands)
    {
        _createCommands = createCommands;
        _query = new TextBox { Watermark = Strings.Hub_PaletteWatermark };
        _query.Classes.Add("hub-input");
        _query.Classes.Add("palette-input");
        _query.TextChanged += (_, _) => Filter();
        _query.KeyDown += OnQueryKeyDown;

        StackPanel panel = new() { Spacing = 10 };
        panel.Children.Add(_query);
        panel.Children.Add(_results);

        Border box = new()
        {
            Child = panel,
            Width = 560,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 70, 0, 0)
        };
        box.Classes.Add("palette");

        Border backdrop = new() { Child = box };
        backdrop.Classes.Add("palette-backdrop");
        backdrop.PointerPressed += (_, e) =>
        {
            if (e.Source == backdrop) Dismiss();
        };

        Content = backdrop;
    }

    public void Open()
    {
        _commands = _createCommands();
        _query.Text = string.Empty;
        Filter();
        IsVisible = true;
        // The first open attaches the control in this pass; focus once it is in the visual tree.
        _query.Focus();
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _query.Focus(), Avalonia.Threading.DispatcherPriority.Loaded);
    }

    public void Dismiss()
    {
        IsVisible = false;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void Filter()
    {
        string query = _query.Text?.Trim() ?? string.Empty;
        string[] words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        _matches = _commands
            .Select(command => (command, score: Score(command, query, words)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.command.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxResults)
            .Select(x => x.command)
            .ToList();

        _selected = 0;
        RenderResults();
    }

    private static int Score(PaletteCommand command, string query, string[] words)
    {
        if (words.Length == 0) return command.Category == Strings.Hub_GoTo ? 2 : 1;

        string title = command.Title;
        string haystack = title + " " + command.Category;

        if (!words.All(word => haystack.Contains(word, StringComparison.CurrentCultureIgnoreCase))) return 0;
        if (title.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)) return 3;
        if (title.Contains(query, StringComparison.CurrentCultureIgnoreCase)) return 2;
        return 1;
    }

    private void RenderResults()
    {
        _results.Children.Clear();

        for (int i = 0; i < _matches.Count; i++)
        {
            PaletteCommand command = _matches[i];
            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("22,*,Auto"), ColumnSpacing = 10 };
            TextBlock icon = HubUi.Icon(command.Icon, 15);
            icon.Classes.Add("hub-accent");
            row.Children.Add(icon);
            TextBlock title = HubUi.Text(command.Title);
            Grid.SetColumn(title, 1);
            row.Children.Add(title);
            TextBlock category = HubUi.Text(command.Category, "hub-hint");
            Grid.SetColumn(category, 2);
            row.Children.Add(category);

            Button item = new() { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch };
            item.Classes.Add("palette-item");
            item.Classes.Set("selected", i == _selected);
            item.Click += async (_, _) => await Execute(command);
            _results.Children.Add(item);
        }

        if (_matches.Count == 0)
        {
            _results.Children.Add(HubUi.Text(Strings.Hub_NoResults, "hub-hint"));
        }
    }

    private async void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Dismiss();
                break;
            case Key.Down:
                if (_matches.Count > 0) _selected = (_selected + 1) % _matches.Count;
                RenderResults();
                break;
            case Key.Up:
                if (_matches.Count > 0) _selected = (_selected - 1 + _matches.Count) % _matches.Count;
                RenderResults();
                break;
            case Key.Enter:
                if (_selected < _matches.Count) await Execute(_matches[_selected]);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private async Task Execute(PaletteCommand command)
    {
        Dismiss();
        await HubUi.RunAsync(command.Execute);
    }
}
