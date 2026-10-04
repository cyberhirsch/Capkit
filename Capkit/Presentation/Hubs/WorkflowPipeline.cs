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

using Capkit.AvaloniaUI.Theming;
using Capkit.HelpersLib;
using Capkit.Localization;
using System;
using System.Collections.Generic;

namespace Capkit;

internal enum PipelineFieldKind
{
    Value,
    ScreenshotsFolder,
    ImageEffectPreset
}

/// <summary>One setting shown in a step's detail panel, addressed by a <see cref="WorkflowOverrides"/> path.</summary>
internal sealed record PipelineField(string Path, string Label, PipelineFieldKind Kind = PipelineFieldKind.Value, bool Monospace = false);

/// <summary>A step of the after-capture pipeline. <see cref="TogglePath"/> is null for steps that always run.</summary>
internal sealed record PipelineStep(string Id, string Name, string Icon, string? TogglePath, string Description, PipelineField[] Fields);

internal static class WorkflowPipeline
{
    private const string AfterCapture = nameof(TaskSettings.AfterCaptureJob) + ".";
    private const string AfterUpload = nameof(TaskSettings.AfterUploadJob) + ".";

    public static IReadOnlyList<PipelineStep> ScreenshotSteps { get; } =
    [
        new("annotate", Strings.Hub_StepAnnotate, LucideIcons.pen_line, AfterCapture + nameof(AfterCaptureTasks.AnnotateImage),
            Strings.Hub_StepAnnotateDescription, []),
        new("effects", Strings.Hub_StepEffects, LucideIcons.wand_sparkles, AfterCapture + nameof(AfterCaptureTasks.AddImageEffects),
            Strings.Hub_StepEffectsDescription,
            [
                new("ImageSettings.SelectedImageEffectPreset", Strings.Hub_FieldEffectPreset, PipelineFieldKind.ImageEffectPreset),
                new(AfterCapture + nameof(AfterCaptureTasks.BeautifyImage), AfterCaptureTasks.BeautifyImage.GetLocalizedDescription())
            ]),
        new("save", Strings.Hub_StepSave, LucideIcons.save, AfterCapture + nameof(AfterCaptureTasks.SaveImageToFile),
            Strings.Hub_StepSaveDescription,
            [
                new(nameof(TaskSettings.ScreenshotsFolder), Strings.Hub_FieldFolder, PipelineFieldKind.ScreenshotsFolder, true),
                new("UploadSettings.NameFormatPattern", Strings.Hub_FieldFileName, Monospace: true),
                new("ImageSettings.ImageFormat", Strings.Hub_FieldFormat),
                new("ImageSettings.ImageJPEGQuality", Strings.Hub_FieldJpegQuality),
                new("ImageSettings.FileExistAction", Strings.Hub_FieldIfFileExists)
            ]),
        new("copy", Strings.Hub_StepCopyImage, LucideIcons.clipboard_copy, AfterCapture + nameof(AfterCaptureTasks.CopyImageToClipboard),
            Strings.Hub_StepCopyImageDescription,
            [
                new(AfterCapture + nameof(AfterCaptureTasks.CopyFilePathToClipboard), AfterCaptureTasks.CopyFilePathToClipboard.GetLocalizedDescription())
            ]),
        UploadStep(AfterCapture + nameof(AfterCaptureTasks.UploadImageToHost), nameof(TaskSettings.ImageDestination)),
        CopyLinkStep()
    ];

    public static IReadOnlyList<PipelineStep> RecordingSteps { get; } =
    [
        new("save", Strings.Hub_StepSaveRecording, LucideIcons.save, null, Strings.Hub_StepSaveRecordingDescription,
            [
                new(nameof(TaskSettings.ScreenshotsFolder), Strings.Hub_FieldFolder, PipelineFieldKind.ScreenshotsFolder, true),
                new("UploadSettings.NameFormatPattern", Strings.Hub_FieldFileName, Monospace: true)
            ]),
        new("copy", Strings.Hub_StepCopyFile, LucideIcons.clipboard_copy, AfterCapture + nameof(AfterCaptureTasks.CopyFileToClipboard),
            Strings.Hub_StepCopyFileDescription,
            [
                new(AfterCapture + nameof(AfterCaptureTasks.CopyFilePathToClipboard), AfterCaptureTasks.CopyFilePathToClipboard.GetLocalizedDescription())
            ]),
        new("folder", Strings.Hub_StepShowInFolder, LucideIcons.folder_open, AfterCapture + nameof(AfterCaptureTasks.ShowInExplorer),
            Strings.Hub_StepShowInFolderDescription, []),
        UploadStep(AfterCapture + nameof(AfterCaptureTasks.UploadImageToHost), nameof(TaskSettings.FileDestination)),
        CopyLinkStep()
    ];

    /// <summary>Recording settings shown under "Record now"; they live in the capture settings of the workflow.</summary>
    public static PipelineField[] RecordingFields(bool gif) => gif
        ?
        [
            new("CaptureSettings.GIFFPS", Strings.Hub_FieldFrameRate),
            new("CaptureSettings.ScreenRecordShowCursor", Strings.Hub_FieldShowCursor),
            new("CaptureSettings.ScreenRecordStartDelay", Strings.Hub_FieldStartDelay)
        ]
        :
        [
            new("CaptureSettings.ScreenRecordFPS", Strings.Hub_FieldFrameRate),
            new("CaptureSettings.FFmpegOptions.VideoCodec", Strings.Hub_FieldVideoCodec),
            new("CaptureSettings.FFmpegOptions.x264_CRF", Strings.Hub_FieldQualityCrf),
            new("CaptureSettings.ScreenRecordShowCursor", Strings.Hub_FieldShowCursor),
            new("CaptureSettings.ScreenRecordStartDelay", Strings.Hub_FieldStartDelay)
        ];

    public static PipelineField[] RegionFields { get; } =
    [
        new("CaptureSettings.RegionCaptureOptions.ShowMagnifier", Strings.Hub_FieldShowMagnifier),
        new("CaptureSettings.RegionCaptureOptions.DetectWindows", Strings.Hub_FieldSnapToWindows),
        new("CaptureSettings.RegionCaptureOptions.ShowScreenCrosshair", Strings.Hub_FieldCrosshair),
        new("CaptureSettings.RegionCaptureOptions.ShowInfo", Strings.Hub_FieldShowInfo)
    ];

    private static PipelineStep UploadStep(string togglePath, string destinationPath) =>
        new("upload", Strings.Hub_StepUpload, LucideIcons.upload_cloud, togglePath, Strings.Hub_StepUploadDescription,
            [
                new(destinationPath, Strings.Hub_FieldDestination),
                new(AfterCapture + nameof(AfterCaptureTasks.ShowBeforeUploadWindow), Strings.Hub_FieldAskBeforeUpload)
            ]);

    private static PipelineStep CopyLinkStep() =>
        new("link", Strings.Hub_StepCopyLink, LucideIcons.link, AfterUpload + nameof(AfterUploadTasks.CopyURLToClipboard),
            Strings.Hub_StepCopyLinkDescription,
            [
                new(AfterUpload + nameof(AfterUploadTasks.UseURLShortener), Strings.Hub_FieldShortenUrl),
                new(nameof(TaskSettings.URLShortenerDestination), Strings.Hub_FieldShortener),
                new(AfterUpload + nameof(AfterUploadTasks.OpenURL), AfterUploadTasks.OpenURL.GetLocalizedDescription())
            ]);

    public static bool IsScreenshotJob(HotkeyType job) => job is HotkeyType.PrintScreen or HotkeyType.ActiveWindow or
        HotkeyType.CustomWindow or HotkeyType.ActiveMonitor or HotkeyType.RectangleRegion or HotkeyType.CustomRegion or
        HotkeyType.LastRegion or HotkeyType.ScrollingCapture or HotkeyType.AutoCapture or HotkeyType.StartAutoCapture;

    public static bool IsRecordingJob(HotkeyType job) => job is HotkeyType.ScreenRecorder or HotkeyType.ScreenRecorderActiveWindow or
        HotkeyType.ScreenRecorderCustomRegion or HotkeyType.StartScreenRecorder or HotkeyType.ScreenRecorderGIF or
        HotkeyType.ScreenRecorderGIFActiveWindow or HotkeyType.ScreenRecorderGIFCustomRegion or HotkeyType.StartScreenRecorderGIF;

    public static bool IsGifJob(HotkeyType job) => job is HotkeyType.ScreenRecorderGIF or HotkeyType.ScreenRecorderGIFActiveWindow or
        HotkeyType.ScreenRecorderGIFCustomRegion or HotkeyType.StartScreenRecorderGIF;
}
