# Text Snip

Native OCR snipping for Windows.

Press `Win+Shift+O`, drag over text, release, and Text Snip copies it to your clipboard.

Windows OCR sucks. Phones made it easy: select text from anything, copy it. Text Snip brings that mobile-native OCR experience to Windows as a fast shortcut with local recognition and much better accuracy.

## Why It Exists

Copying text from images, screenshots, videos, PDFs, remote desktops, installers, error dialogs, and locked-down websites should be instant, but such a tool does not exist with speed, ease of use, and accuracy.

Text Snip fixes that with a native shortcut:

- press one hotkey;
- drag the area you care about;
- paste the result.

Everything needed for OCR ships with the app. Recognition runs locally on the CPU using bundled ONNX models. Screenshots are captured in memory and not saved by Text Snip.

## What Makes It Different

- **Native Windows flow**: tray app, global hotkey, desktop overlay, clipboard output.
- **Mobile-style OCR**: select what you see and copy it immediately.
- **Local and private**: OCR runs on the machine with bundled models.
- **Fast install**: one normal Windows installer, no zip workflow, no Python environment, no model download.
- **Real OCR pipeline**: bundled PP-OCRv6 detector/recognizer models through ONNX Runtime.
- **Hard-case testing**: dense UI, clipped selections, low contrast, colored text, small text, messy backgrounds, rotated text, multilingual text, symbols, and photo-like scenes.
- **Repairable installer**: reinstalling replaces stale app files and shortcuts; an optional reset can clear Text Snip settings/logs.

## Latest Release

Download the latest release from:

https://github.com/rollingedit/Text-Snip/releases/latest

## Install

Run:

```text
OcrSnip-Setup-x64.exe
```

The installer adds:

- Text Snip app files;
- bundled OCR models;
- bundled runtime dependencies;
- third-party license files;
- Start Menu shortcut;
- desktop shortcut by default;
- uninstaller.

If the Microsoft Visual C++ Runtime is missing or outdated, the installer runs the bundled Microsoft Visual C++ Redistributable.

The installer does not add a driver, service, browser extension, shell extension, background updater, telemetry agent, or network component.

Optional installer choices:

- **Start Text Snip at startup**: off by default.
- **Reset Text Snip settings and logs**: off by default, useful for repair installs.

## Use

1. Open Text Snip.
2. Press `Win+Shift+O`.
3. Drag over text.
4. Release.
5. Paste.

If the clipboard is busy, Text Snip opens a small result window so the recognized text is still available.

## Settings

- Hotkey
- Launch at startup
- Memory mode
- Small text boost
- Toasts
- Copy mode

## Privacy

Text Snip is designed for local OCR snipping. Screenshots stay in memory, recognized text goes to the clipboard, and OCR runs on the machine with bundled models.

Diagnostic logs are limited to app failure information and do not include screenshots or OCR output.

## Build

Requirements:

- Windows x64
- .NET SDK matching `global.json`
- Inno Setup 6 for installer builds

Common commands:

```powershell
.\.dotnet\dotnet.exe test OcrSnip.slnx -c Release
powershell -ExecutionPolicy Bypass -File tools\publish.ps1
powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1
```

The user-facing installer is created at:

```text
installer\Output\OcrSnip-Setup-x64.exe
```

## License

Text Snip is licensed under the GNU General Public License version 3.0. See `LICENSE`.

Repository: https://github.com/rollingedit/Text-Snip

Published builds include third-party license files under `licenses\`, and the repo includes `THIRD_PARTY_NOTICES.md`.

Bundled/runtime components include:

- PaddleOCR / PP-OCRv6 ONNX models: Apache License 2.0  
  https://github.com/PaddlePaddle/PaddleOCR
- ONNX Runtime: MIT  
  https://github.com/microsoft/onnxruntime
- OpenCvSharp / OpenCV: Apache License 2.0 for the packages used here  
  https://github.com/shimat/opencvsharp  
  https://github.com/opencv/opencv
- Clipper2: Boost Software License 1.0  
  https://github.com/AngusJohnson/Clipper2

Redistributed builds must follow GPLv3 terms and keep the included third-party license notices.

## Troubleshooting

- If the hotkey is already in use, OCR Snip opens settings.
- Protected video, secure desktops, and privileged windows may capture as black or unreadable.
- Very incomplete selections can produce partial text.
- Low contrast, tiny text, motion blur, unusual fonts, and rotated text can reduce OCR accuracy.
