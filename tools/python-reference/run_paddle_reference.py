"""Development-only PaddleOCR reference runner.

This script is intentionally not part of the shipped application. It exists to
generate fixture JSON from PaddleOCR so the C# ONNX pipeline can be matched
fixture-by-fixture.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    try:
        from paddleocr import PaddleOCR
    except ImportError as exc:
        raise SystemExit("Install paddleocr in a development virtualenv first.") from exc

    ocr = PaddleOCR(
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
        text_detection_model_name="PP-OCRv6_small_det",
        text_recognition_model_name="PP-OCRv6_small_rec",
        device="cpu",
        engine="onnxruntime",
    )
    result = ocr.predict(str(args.input))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(compact_result(result), ensure_ascii=False, indent=2), encoding="utf-8")
    return 0


def compact_result(result):
    pages = []
    for page in result:
        pages.append(
            {
                "input_path": str(page.get("input_path", "")),
                "rec_texts": to_jsonable(page.get("rec_texts", [])),
                "rec_scores": to_jsonable(page.get("rec_scores", [])),
                "rec_boxes": to_jsonable(page.get("rec_boxes", [])),
                "rec_polys": to_jsonable(page.get("rec_polys", [])),
                "dt_polys": to_jsonable(page.get("dt_polys", [])),
            }
        )
    return pages


def to_jsonable(value):
    if isinstance(value, np.ndarray):
        return value.tolist()
    if isinstance(value, np.generic):
        return value.item()
    if isinstance(value, dict):
        return {str(k): to_jsonable(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [to_jsonable(v) for v in value]
    try:
        json.dumps(value)
        return value
    except TypeError:
        return str(value)


if __name__ == "__main__":
    raise SystemExit(main())
