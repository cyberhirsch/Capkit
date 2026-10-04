# Capkit

Screen capture, recording and sharing for Windows, with macOS in progress.

Capkit is a free, open source desktop tool for taking screenshots, recording your screen, annotating the result and sending it where it needs to go. It has no ads and needs no account.

## How it is organised

The main window is built around what you came to do:

* **Capture**: screenshots and recordings, the shortcuts that trigger them, and the pipeline that runs after each capture (annotate, add effects, save, copy, upload, copy link).
* **Share**: upload files, folders, clipboard content, text and URLs, choose destinations and decide what happens after an upload.
* **Tools**: colour picker, ruler, pin to screen, image editor, beautifier, background remover, OCR, QR codes, video and GIF tools and more, searchable in one grid.
* **History**: everything you captured or uploaded, with quick actions.

Press **Ctrl+K** anywhere to search actions, tools, shortcuts and individual settings.

## Shortcuts that only change what they need

Every hotkey is a shortcut with its own pipeline. A shortcut follows the default pipeline and keeps only the settings you change for it, for example a different save folder or uploading to another destination. Settings you did not change keep following the defaults, so adjusting the default pipeline updates every shortcut at once.

## Capture methods

Region, window, monitor, full screen, last region, custom region, scrolling capture, auto capture, screen recording (MP4) and GIF recording.

## Platforms

| Platform | Status |
| --- | --- |
| Windows 10 and 11 (x64, ARM64) | Main platform |
| macOS 14 or later (Apple Silicon, Intel) | In progress: the app builds for macOS; capture through ScreenCaptureKit and global hotkeys are being implemented and tested |

## Building

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download).

Windows:

```
dotnet build Capkit/Capkit.csproj -c Debug
```

macOS (on a Mac this is the default platform; on Windows it compiles the macOS variant for checking):

```
native/macos/build.sh Capkit/bin/Debug/osx-arm64
dotnet build Capkit/Capkit.csproj -c Debug -p:CapkitPlatform=macos -r osx-arm64
```

The native macOS bridge (`native/macos`) needs Xcode command line tools. GitHub Actions builds both platforms on every push.

## Credits

Capkit is a fork of [ShareX](https://github.com/ShareX/ShareX) by the ShareX Team, modified and renamed. The original copyright notices are kept in the source files.

## License

GNU General Public License, version 2 or (at your option) any later version, as stated in the source file headers. The full text of version 3 is in [LICENSE.txt](LICENSE.txt).
