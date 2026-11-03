"""Make white silhouettes from transparent character sprites and measure them for the silhouette gate.

Every sprite gets a companion "<name>_sil.png": RGB = 255, alpha = source alpha.

The gate (style-guide.md §17) is judged at the smallest size a figure is shown: 226 px tall on the
1920x1080 screen (battle-visual-v1.md §4.3, FigureMotion.StandardHeight). The whole canvas height is
scaled to that height, so one source pixel is 226 / 1536 = 0.147 screen pixels for a 1536-tall sprite.

Usage:
  python silhouette.py <png or directory> [...] [--out DIR] [--height 226] [--alpha-cut 128]

For each sprite it prints the width shown on screen, the widest closed holes (condition 3) and the
parts thinner than the condition 4 width, and writes "<name>_gate.png": the silhouette in grey,
the parts thinner than the condition 4 width in red. Condition 4 passes when every protrusion
(horn, spear tip, claw) still sticks out of the outline in grey; a red tip alone is fine.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

# style-guide.md §17, in screen pixels at 1080p. The source values follow from --height.
GAP_PX = 6.0
THICK_PX = 2.0
PROTRUDE_PX = 3.0

# Thin parts smaller than this many source pixels are anti-aliasing crumbs, not shapes.
MIN_AREA = 30

GREY = (60, 60, 60)
RED = (230, 30, 30)
PAPER = (235, 235, 235)


def collect(paths: list[str]) -> list[Path]:
    files: list[Path] = []
    for raw in paths:
        p = Path(raw)
        if p.is_dir():
            files.extend(
                sorted(x for x in p.glob("*.png") if not x.stem.endswith(("_sil", "_gate")))
            )
        elif p.suffix.lower() == ".png":
            files.append(p)
        else:
            print(f"skip (not png): {p}", file=sys.stderr)
    return files


def disk(diameter: float) -> np.ndarray:
    r = diameter / 2.0
    n = int(np.ceil(r))
    y, x = np.mgrid[-n : n + 1, -n : n + 1]
    return (x * x + y * y) <= r * r


def closed_hole_widths(solid: np.ndarray) -> list[float]:
    """Width of each hole the silhouette closes, as the diameter of the widest circle inside it."""
    background, count = ndi.label(~solid)
    edge = set(np.unique(np.r_[background[0], background[-1], background[:, 0], background[:, -1]]))
    distance = ndi.distance_transform_edt(~solid)
    widths = [
        float(2 * distance[background == i].max()) for i in range(1, count + 1) if i not in edge
    ]
    return sorted(widths, reverse=True)


def thin_parts(solid: np.ndarray, thick_src: int) -> tuple[np.ndarray, list[int]]:
    """Parts a disc of the condition 4 width cannot pass through, and the long side of each."""
    thin = solid & ~ndi.binary_opening(solid, structure=disk(thick_src))
    labels, _ = ndi.label(thin, structure=np.ones((3, 3)))
    lengths: list[int] = []
    keep = np.zeros_like(thin)
    for k, box in enumerate(ndi.find_objects(labels), 1):
        part = labels[box] == k
        if int(part.sum()) < MIN_AREA:
            continue
        keep[box] |= part
        lengths.append(max(box[0].stop - box[0].start, box[1].stop - box[1].start))
    return keep, sorted(lengths, reverse=True)


def measure(src: Path, out_dir: Path | None, height: int, alpha_cut: int) -> dict[str, object]:
    img = Image.open(src)
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    rgba = np.asarray(img)
    alpha = rgba[:, :, 3]
    if int(alpha.min()) == 255:
        raise ValueError(f"{src.name}: no transparency (remove the background first)")

    target_dir = out_dir or src.parent
    target_dir.mkdir(parents=True, exist_ok=True)
    sil = np.empty_like(rgba)
    sil[:, :, :3] = 255
    sil[:, :, 3] = alpha
    Image.fromarray(sil, "RGBA").save(target_dir / f"{src.stem}_sil.png")

    solid = alpha >= alpha_cut
    h, w = solid.shape
    scale = height / h
    ys, xs = np.nonzero(solid)
    bbox_w = int(xs.max() - xs.min() + 1) if xs.size else 0

    holes = closed_hole_widths(solid)
    thick_src = math.ceil(THICK_PX / scale)
    thin, lengths = thin_parts(solid, thick_src)

    gate = np.empty((h, w, 3), np.uint8)
    gate[:] = PAPER
    gate[solid] = GREY
    gate[thin] = RED
    Image.fromarray(gate, "RGB").save(target_dir / f"{src.stem}_gate.png")

    return {
        "file": src.name,
        "canvas": f"{w}x{h}",
        "shown_width_px": round(bbox_w * scale, 1),
        "gap_src_needed": math.ceil(GAP_PX / scale),
        "widest_holes_src": [round(x) for x in holes[:3]],
        "gap_ok": bool(holes) and holes[0] * scale >= GAP_PX,
        "thick_src": thick_src,
        "protrude_src": math.ceil(PROTRUDE_PX / scale),
        "thin_parts": len(lengths),
        "longest_thin_src": lengths[:3],
    }


def main() -> int:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("inputs", nargs="+")
    parser.add_argument("--out", type=Path, default=None)
    parser.add_argument("--height", type=int, default=226, help="figure height on the 1080p screen")
    parser.add_argument("--alpha-cut", type=int, default=128)
    args = parser.parse_args()

    files = collect(args.inputs)
    if not files:
        print("no png found", file=sys.stderr)
        return 1

    failed = 0
    print(
        "file\tcanvas\tshown_width_px\tgap_src_needed\twidest_holes_src\tgap_ok"
        "\tthick_src\tprotrude_src\tthin_parts\tlongest_thin_src"
    )
    for f in files:
        try:
            r = measure(f, args.out, args.height, args.alpha_cut)
        except ValueError as e:
            print(f"ERROR\t{e}", file=sys.stderr)
            failed += 1
            continue
        print("\t".join(str(v) for v in r.values()))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
