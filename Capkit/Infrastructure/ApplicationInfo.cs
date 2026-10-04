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

using Capkit.HelpersLib;
using System;
using System.Text;

namespace Capkit;

internal static class ApplicationInfo
{
    internal const string Name = "Capkit";
    internal const string MutexName = "F597DA05-4959-43D3-90EB-98970E2A1E10";
    // Off Windows the pipe is a Unix socket under $TMPDIR, whose full path must stay below ~104 characters.
    internal static readonly string PipeName = OperatingSystem.IsWindows()
        ? $"{Environment.MachineName}-{Environment.UserName}-{Name}"
        : $"capkit-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Environment.UserName)))[..12].ToLowerInvariant()}";

    internal const CapkitBuild Build =
#if RELEASE
        CapkitBuild.Release;
#elif STEAM
        CapkitBuild.Steam;
#elif MicrosoftStore
        CapkitBuild.MicrosoftStore;
#elif DEBUG
        CapkitBuild.Debug;
#else
        CapkitBuild.Unknown;
#endif

    internal const bool Dev = true;

    internal static string VersionText
    {
        get
        {
            StringBuilder versionText = new();
            Version version = Version.Parse(Helpers.GetApplicationVersion(true));
            versionText.Append(version.Major).Append('.').Append(version.Minor);
            if (version.Build > 0 || version.Revision > 0) versionText.Append('.').Append(version.Build);
            if (version.Revision > 0) versionText.Append('.').Append(version.Revision);
            if (Dev) versionText.Append(" Dev");
            if (StartupOptions.Portable) versionText.Append(" Portable");
            return versionText.ToString();
        }
    }

    internal static string Title
    {
        get
        {
            string title = $"{Name} {VersionText}";
            if (ApplicationState.SettingsOrNull is { DevMode: true })
            {
                string info = Build.ToString();
                if (StartupOptions.IsAdmin)
                {
                    info += ", Admin";
                }

                title += $" ({info})";
            }

            return title;
        }
    }

    internal static string TitleShort => ApplicationState.SettingsOrNull is { DevMode: true } ? Title : Name;
}
