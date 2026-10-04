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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Capkit
{
    /// <summary>
    /// Per-setting overrides for workflows. A path such as "ImageSettings.ImageFormat" names a field
    /// or property below <see cref="TaskSettings"/>; a trailing enum member name addresses one flag of a
    /// [Flags] enum, e.g. "AfterCaptureJob.SaveImageToFile". Values are stored as JSON so that the
    /// override survives the settings copy and cleanup that whole sections go through.
    /// </summary>
    public static class WorkflowOverrides
    {
        private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        private static readonly Dictionary<string, string> sectionFlags = new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(TaskSettings.AfterCaptureJob)] = nameof(TaskSettings.UseDefaultAfterCaptureJob),
            [nameof(TaskSettings.AfterUploadJob)] = nameof(TaskSettings.UseDefaultAfterUploadJob),
            [nameof(TaskSettings.ImageDestination)] = nameof(TaskSettings.UseDefaultDestinations),
            [nameof(TaskSettings.ImageFileDestination)] = nameof(TaskSettings.UseDefaultDestinations),
            [nameof(TaskSettings.TextDestination)] = nameof(TaskSettings.UseDefaultDestinations),
            [nameof(TaskSettings.TextFileDestination)] = nameof(TaskSettings.UseDefaultDestinations),
            [nameof(TaskSettings.FileDestination)] = nameof(TaskSettings.UseDefaultDestinations),
            [nameof(TaskSettings.URLShortenerDestination)] = nameof(TaskSettings.UseDefaultDestinations),
            [nameof(TaskSettings.URLSharingServiceDestination)] = nameof(TaskSettings.UseDefaultDestinations),
            [nameof(TaskSettings.GeneralSettings)] = nameof(TaskSettings.UseDefaultGeneralSettings),
            [nameof(TaskSettings.ImageSettings)] = nameof(TaskSettings.UseDefaultImageSettings),
            [nameof(TaskSettings.CaptureSettings)] = nameof(TaskSettings.UseDefaultCaptureSettings),
            [nameof(TaskSettings.UploadSettings)] = nameof(TaskSettings.UseDefaultUploadSettings),
            [nameof(TaskSettings.ToolsSettings)] = nameof(TaskSettings.UseDefaultToolsSettings),
            [nameof(TaskSettings.AdvancedSettings)] = nameof(TaskSettings.UseDefaultAdvancedSettings)
        };

        public static bool HasOverride(TaskSettings settings, string path) =>
            settings.FieldOverrides != null && settings.FieldOverrides.ContainsKey(path);

        public static int CountChanges(TaskSettings settings)
        {
            int count = settings.FieldOverrides?.Count ?? 0;

            foreach (string flag in new HashSet<string>(sectionFlags.Values))
            {
                if (GetValue(settings, flag) is false)
                {
                    count++;
                }
            }

            if (settings.OverrideScreenshotsFolder) count++;

            return count;
        }

        /// <summary>True when the whole section holding this path is owned by the workflow (legacy override).</summary>
        public static bool IsSectionOwned(TaskSettings settings, string path)
        {
            string root = path.Split('.')[0];
            return sectionFlags.TryGetValue(root, out string? flag) && GetValue(settings, flag) is false;
        }

        public static void SetSectionToDefault(TaskSettings settings, string path)
        {
            string root = path.Split('.')[0];
            if (sectionFlags.TryGetValue(root, out string? flag))
            {
                SetValue(settings, flag, true);
            }
        }

        public static void SetOverride(TaskSettings settings, string path, object? value)
        {
            settings.FieldOverrides ??= new Dictionary<string, string>();
            settings.FieldOverrides[path] = JsonConvert.SerializeObject(value);
        }

        public static void ClearOverride(TaskSettings settings, string path)
        {
            settings.FieldOverrides?.Remove(path);
        }

        /// <summary>Applies stored overrides on top of settings whose sections were already merged with the defaults.</summary>
        internal static void Apply(TaskSettings settings)
        {
            if (settings.FieldOverrides == null || settings.FieldOverrides.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in settings.FieldOverrides)
            {
                try
                {
                    Type? type = GetValueType(settings, pair.Key);
                    if (type == null)
                    {
                        continue;
                    }

                    SetValue(settings, pair.Key, JsonConvert.DeserializeObject(pair.Value, type));
                }
                catch (Exception e)
                {
                    DebugHelper.WriteException(e, $"Workflow override \"{pair.Key}\" could not be applied.");
                }
            }
        }

        public static object? GetValue(object root, string path)
        {
            object? current = root;

            foreach (string segment in path.Split('.'))
            {
                if (current == null)
                {
                    return null;
                }

                if (TryGetFlag(current.GetType(), segment, out Enum? flag))
                {
                    return ((Enum)current).HasFlag(flag!);
                }

                MemberInfo? member = FindMember(current.GetType(), segment);
                if (member == null)
                {
                    return null;
                }

                current = GetMemberValue(member, current);
            }

            return current;
        }

        public static Type? GetValueType(object root, string path)
        {
            Type type = root.GetType();
            object? current = root;

            foreach (string segment in path.Split('.'))
            {
                if (TryGetFlag(type, segment, out _))
                {
                    return typeof(bool);
                }

                MemberInfo? member = FindMember(current?.GetType() ?? type, segment);
                if (member == null)
                {
                    return null;
                }

                type = GetMemberType(member);
                current = current == null ? null : GetMemberValue(member, current);
            }

            return type;
        }

        public static void SetValue(object root, string path, object? value)
        {
            SetPath(root, path.Split('.'), 0, value);
        }

        private static object SetPath(object target, string[] segments, int index, object? value)
        {
            string segment = segments[index];
            Type targetType = target.GetType();

            if (TryGetFlag(targetType, segment, out Enum? flag))
            {
                long current = Convert.ToInt64(target);
                long bit = Convert.ToInt64(flag);
                current = value is true ? current | bit : current & ~bit;
                return Enum.ToObject(targetType, current);
            }

            MemberInfo member = FindMember(targetType, segment) ??
                throw new ArgumentException($"\"{segment}\" was not found on {targetType.Name}.", nameof(segments));
            Type memberType = GetMemberType(member);

            if (index == segments.Length - 1)
            {
                SetMemberValue(member, target, ConvertValue(value, memberType));
                return target;
            }

            object child = GetMemberValue(member, target) ??
                throw new InvalidOperationException($"\"{segment}\" is null on {targetType.Name}.");
            object updated = SetPath(child, segments, index + 1, value);

            if (memberType.IsValueType)
            {
                SetMemberValue(member, target, updated);
            }

            return target;
        }

        private static object? ConvertValue(object? value, Type type)
        {
            if (value == null || type.IsInstanceOfType(value))
            {
                return value;
            }

            Type underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying.IsEnum)
            {
                return value is string name ? Enum.Parse(underlying, name) : Enum.ToObject(underlying, value);
            }

            return Convert.ChangeType(value, underlying);
        }

        private static bool TryGetFlag(Type type, string name, out Enum? flag)
        {
            flag = null;

            if (!type.IsEnum || type.GetCustomAttribute<FlagsAttribute>() == null)
            {
                return false;
            }

            if (Enum.TryParse(type, name, true, out object? parsed) && parsed is Enum value)
            {
                flag = value;
                return true;
            }

            return false;
        }

        private static MemberInfo? FindMember(Type type, string name) =>
            (MemberInfo?)type.GetField(name, MemberFlags) ?? type.GetProperty(name, MemberFlags);

        private static Type GetMemberType(MemberInfo member) =>
            member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;

        private static object? GetMemberValue(MemberInfo member, object target) =>
            member is FieldInfo field ? field.GetValue(target) : ((PropertyInfo)member).GetValue(target);

        private static void SetMemberValue(MemberInfo member, object target, object? value)
        {
            if (member is FieldInfo field)
            {
                field.SetValue(target, value);
            }
            else
            {
                ((PropertyInfo)member).SetValue(target, value);
            }
        }
    }
}
