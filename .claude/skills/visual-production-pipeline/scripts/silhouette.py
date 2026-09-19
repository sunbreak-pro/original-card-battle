"""Make white silhouettes from transparent character sprites and report their footprint.

The battle view tints one sprite for Rim / Flash / Ghost / defeat silhouette
(ArenaView.cs:207-212). A painted sprite breaks those effects, so every
variant needs a companion "<name>_sil.png": RGB = 255, alpha = source alpha.

Usage:
  python silhouette.py <png or directory> [...] [--out DIR] [--frame-height 300] [--alpha-cut 8]

For each sprite it prints the opaque bounding box and the width it would have
when the sprite is scaled so its canvas height equals --frame-height
(the figure frame is 140 x 300 at 1080p; the column hit test is +/-130 px, v2:306).
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

FRAME_WIDTH = 140
COLUMN_HALF_WIDTH = 130


def collect(paths: list[str]) -> list[Path]:
    files: list[Path] = []
    for raw in paths:
        p = Path(raw)
        if p.is_dir():
            files.extend(sorted(x for x in p.glob("*.png") if not x.stem.endswith("_sil")))
        elif p.suffix.lower() == ".png":
            files.append(p)
        else:
            print(f"skip (not png): {p}", file=sys.stderr)
    return files


def make_silhouette(src: Path, out_dir: Path | None, frame_height: int, alpha_cut: int) -> dict[str, object]:
    img = Image.open(src)
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    rgba = np.asarray(img)
    alpha = rgba[:, :, 3]
    if int(alpha.min()) == 255:
        raise ValueError(f"{src.name}: no transparency (remove the background first)")

    sil = np.empty_like(rgba)
    sil[:, :, :3] = 255
    sil[:, :, 3] = alpha
    target_dir = out_dir or src.parent
    target_dir.mkdir(parents=True, exist_ok=True)
    target = target_dir / f"{src.stem}_sil.png"
    Image.fromarray(sil, "RGBA").save(target)

    ys, xs = np.nonzero(alpha > alpha_cut)
    h, w = alpha.shape
    bbox_w = int(xs.max() - xs.min() + 1) if xs.size else 0
    bbox_h = int(ys.max() - ys.min() + 1) if ys.size else 0
    scale = frame_height / h
    shown_w = bbox_w * scale
    return {
        "file": src.name,
        "out": target.name,
        "canvas": f"{w}x{h}",
        "bbox": f"{bbox_w}x{bbox_h}",
        "shown_width_px": round(shown_w, 1),
        "over_frame": shown_w > FRAME_WIDTH,
        "over_column": shown_w / 2 > COLUMN_HALF_WIDTH,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("inputs", nargs="+")
    parser.add_argument("--out", type=Path, default=None)
    parser.add_argument("--frame-height", type=int, default=300)
    parser.add_argument("--alpha-cut", type=int, default=8)
    args = parser.parse_args()

    files = collect(args.inputs)
    if not files:
        print("no png found", file=sys.stderr)
        return 1

    failed = 0
    print("file\tout\tcanvas\tbbox\tshown_width_px\tover_frame(140)\tover_column(+/-130)")
    for f in files:
        try:
            r = make_silhouette(f, args.out, args.frame_height, args.alpha_cut)
        except ValueError as e:
            print(f"ERROR\t{e}", file=sys.stderr)
            failed += 1
            continue
        print(f"{r['file']}\t{r['out']}\t{r['canvas']}\t{r['bbox']}\t{r['shown_width_px']}\t{r['over_frame']}\t{r['over_column']}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
