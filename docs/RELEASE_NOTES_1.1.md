# Text Snip 1.1

Text Snip 1.1 is focused on clipboard quality. It adds a custom layout formatter on top of local PP-OCRv6 ONNX OCR, so captures paste closer to the layout the user selected instead of looking like raw OCR output.

## What Changed

### Custom Layout Formatter

Text Snip now uses OCR geometry to rebuild useful clipboard structure:

* preserves top-to-bottom reading order for normal, centered, chat-like, and handwriting-style captures;
* joins same-row OCR fragments into readable lines;
* keeps wrapped lines attached to the correct paragraph or numbered step;
* preserves section gaps and paragraph breaks;
* handles real bullets, restored bullets, hollow bullets, numbered lists, and corrupted bullet encodings;
* separates dense navigation text when OCR returns packed words;
* pairs menu item names with nearby prices;
* keeps two-column cards, release notes, app screens, and dense lists readable;
* cleans common punctuation spacing.

### Installer And App UX

* Default shortcut is `Win+Shift+O`.
* The selection overlay can be cancelled with `Esc` or right-click.
* Installer version is `1.1`.
* Installer keeps the one-file Windows setup flow: download, run, use.

## Verification

Built and tested on Windows x64 from commit `6ea1269`:

* Release test suite: 104 passing tests.
* Model hash/config checks passed during installer build.
* Installer build completed successfully.
* Public Windows CI passed for build, tests, OCR fixture gates, validation tooling, publish, and release artifacts.

Latest local installer produced at:

```text
installer\Output\Text-Snip-Setup-x64.exe
```

SHA256:

```text
BDA1C9F9D73848D6B9D27BFE94DC789F4F8D0C29D9373D772416D2A1A257A440
```

## Notes

Text Snip is local OCR. It does not install a driver, service, browser extension, shell extension, background updater, telemetry agent, or network component.

OCR quality still depends on the selected image. Blur, tiny text, glare, unusual fonts, handwriting, rotated text, low contrast, and clipped selections can reduce accuracy. The formatter improves layout from OCR boxes, but it cannot recover text the OCR model did not detect.

## License

Text Snip is GPLv3.

Published builds include third-party license files for PP-OCRv6 ONNX model files, ONNX Runtime, OpenCvSharp / OpenCV, and Clipper2.
