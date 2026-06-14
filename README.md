# Text Snip

Native OCR snipping for Windows with a custom layout formatter. Press `Win+Shift+O`, drag over text, release, and Text Snip copies formatted text to your clipboard.

Windows OCR sucks. Phones made it easy: select text from anything, copy it. Text Snip brings that mobile-native OCR experience to Windows and goes further with local recognition plus a custom formatter that turns OCR boxes into usable clipboard text instead of raw OCR output.

## Why It Exists

Copying text from images, screenshots, videos, PDFs, remote desktops, installers, error dialogs, and locked-down websites should be instant, but such a tool does not exist with speed, ease of use, and accuracy.

Text Snip fixes that with a native shortcut and formatter built for messy real-world captures:

```text id="run-flow"
press one hotkey
drag the area you care about
paste the result
```

Everything needed for OCR ships with the app. Recognition runs locally on CPU using bundled small ONNX OCR models, then Text Snip reconstructs readable clipboard text from the OCR boxes instead of dumping raw model output.

Screenshots are captured in memory and not saved by Text Snip.

## What Makes It Different

* **Native Windows flow**: tray app, global hotkey, desktop overlay, clipboard output.
* **Mobile-style OCR**: select what you see and copy it immediately.
* **Local and private**: OCR runs on the machine with bundled models.
* **Fast install**: one normal Windows installer, no zip workflow, no Python environment, no model download.
* **Real OCR pipeline**: bundled [PP-OCRv6 small detector](https://huggingface.co/PaddlePaddle/PP-OCRv6_small_det_onnx) and [PP-OCRv6 small recognizer](https://huggingface.co/PaddlePaddle/PP-OCRv6_small_rec_onnx) ONNX models through [ONNX Runtime](https://github.com/microsoft/onnxruntime), with [OpenCvSharp](https://github.com/shimat/opencvsharp) / [OpenCV](https://github.com/opencv/opencv) for image preprocessing and [Clipper2](https://github.com/AngusJohnson/Clipper2) for OCR text-box geometry.
* **Custom layout formatter**: Text Snip rebuilds reading order, rows, columns, wrapped lines, list markers, spacing, punctuation, and section breaks.
* **Repairable installer**: reinstalling replaces stale app files and shortcuts; optional reset clears Text Snip settings/logs.
* **No NPU needed**: The only half decent built in OCR requires a Copilot+ PC, this doesn't and performs better.

## How It Works

Text Snip captures the selected screen area, preprocesses the image, runs local OCR, and applies its own layout formatter before copying the result.

The detector finds text regions. The recognizer reads the characters. Text Snip then uses the detected geometry to preserve the original structure. This is custom code in Text Snip, not a default OCR dump.

The formatter handles:

* top-to-bottom reading order, including centered and chat-like captures;
* same-row fragment joining;
* paragraph breaks, section gaps, and wrapped lines;
* bullets, numbered lists, hollow bullets, and OCR bullet encoding cleanup;
* dense navigation text where OCR may pack separate words together;
* two-column cards, menus, price columns, release notes, tables, and app screens;
* punctuation spacing cleanup.

OCR models return text boxes, not polished clipboard text. Text Snip turns those boxes into something you can paste and use.

## Practical Limits

Text Snip is built for real Windows snipping, not perfect document reconstruction. OCR quality still depends on the selected image: blur, tiny text, glare, unusual fonts, handwriting, rotated text, low contrast, and partial selections can reduce accuracy. The formatter improves layout, but it cannot recover words the OCR model did not read or boxes the detector did not find.

For best results, drag slightly outside the text instead of clipping letters at the border.

## Latest Release

Download the latest release:

https://github.com/rollingedit/Text-Snip/releases/latest

## Install

Run:

```text id="installer-name"
Text-Snip-Setup-x64.exe
```

The installer adds:

* Text Snip app files;
* bundled OCR models;
* bundled runtime dependencies;
* third-party license files;
* Start Menu shortcut;
* desktop shortcut by default;
* uninstaller.

If the Microsoft Visual C++ Runtime is missing or outdated, the installer uses the bundled Microsoft Visual C++ Redistributable. If Windows blocks that runtime install without elevation, the installer stops with a clear message instead of leaving a broken app behind.

The installer does not add a driver, service, browser extension, shell extension, background updater, telemetry agent, or network component.

Optional installer choices:

* **Start Text Snip at startup**: off by default, available as an installer option.
* **Reset Text Snip settings and logs**: off by default, useful for repair installs.

## Use

1. Open Text Snip.
2. Press `Win+Shift+O`.
3. Drag over text.
4. Release.
5. Paste.

If the clipboard is busy, Text Snip opens a small result window so the recognized text is still available.

## Diagnostics

Run this from the installed app directory to check model files, native runtime files, the VC++ runtime registration, settings path, and diagnostics log path:

```powershell id="doctor-command"
.\OcrSnip.App.exe --doctor
```

The doctor report does not capture screenshots, run OCR on user content, or log recognized text.

## Settings

* Hotkey
* Launch at startup
* Memory mode
* Small text boost
* Toasts
* Copy mode

## Privacy

Text Snip is designed for local OCR snipping.

Screenshots stay in memory, recognized text goes to the clipboard, and OCR runs on the machine with bundled models.

Diagnostic logs are limited to app failure information and do not include screenshots or OCR output.

## Build

Requirements:

* Windows x64
* .NET SDK matching `global.json`
* Inno Setup 6 for installer builds

Common commands:

```powershell id="build-commands"
dotnet test OcrSnip.slnx -c Release
powershell -ExecutionPolicy Bypass -File tools\publish.ps1
powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1
```

The user-facing installer is created at:

```text id="installer-output"
installer\Output\Text-Snip-Setup-x64.exe
```

## Links

* PP-OCRv6 small detector ONNX model: https://huggingface.co/PaddlePaddle/PP-OCRv6_small_det_onnx
* PP-OCRv6 small recognizer ONNX model: https://huggingface.co/PaddlePaddle/PP-OCRv6_small_rec_onnx
* Installer sources: https://github.com/rollingedit/Text-Snip/tree/main/installer
* Packaged license copies: https://github.com/rollingedit/Text-Snip/tree/main/installer/licenses
* Third-party notices: https://github.com/rollingedit/Text-Snip/blob/main/THIRD_PARTY_NOTICES.md

## License

Text Snip is licensed under the GNU General Public License version 3.0.

See `LICENSE`.

Published builds include third-party license files under `licenses\`, and the repo includes [`THIRD_PARTY_NOTICES.md`](https://github.com/rollingedit/Text-Snip/blob/main/THIRD_PARTY_NOTICES.md).

Bundled/runtime components include:

* [PP-OCRv6 small detector ONNX model](https://huggingface.co/PaddlePaddle/PP-OCRv6_small_det_onnx): Apache License 2.0
* [PP-OCRv6 small recognizer ONNX model](https://huggingface.co/PaddlePaddle/PP-OCRv6_small_rec_onnx): Apache License 2.0
* [ONNX Runtime](https://github.com/microsoft/onnxruntime): MIT
* [OpenCvSharp](https://github.com/shimat/opencvsharp) / [OpenCV](https://github.com/opencv/opencv): Apache License 2.0 for the packages used here; used for image handling and preprocessing before OCR
* [Clipper2](https://github.com/AngusJohnson/Clipper2): Boost Software License 1.0; used for polygon/text-box geometry during OCR post-processing

Text Snip bundles the PP-OCRv6 ONNX model files/configs.

Redistributed builds must follow GPLv3 terms and keep the included third-party license notices.
