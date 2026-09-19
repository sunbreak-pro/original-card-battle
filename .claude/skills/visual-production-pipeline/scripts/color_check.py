"""Check readability of the battle palette: WCAG contrast and colour-vision distance.

Defaults are the A+ tokens (battle_ui_ux_v2.md section 4.5 / 4.8) and the floor
gradients (BattleTheme.cs). Override with --tokens tokens.json
({"roles": {...}, "texts": {...}, "grounds": {...}}).

Contrast: WCAG 2.x relative luminance. Text needs 4.5:1, non-text 3:1.
Colour-vision distance: Machado, Oliveira & Fernandes 2009 matrices (severity 1.0)
applied in linear sRGB, then CIE76 delta E in CIELAB (D65). The delta E threshold
is a project decision, not a standard (default 20).

Usage:
  python color_check.py [--tokens FILE] [--de-threshold 20] [--json]
Exit code 1 when any check fails.
"""

from __future__ import annotations

import argparse
import itertools
import json
import sys

import numpy as np

ROLES = {
    "accent": "#63e0c9",
    "warm": "#ffc46b",
    "omen": "#ff5d47",
    "guard": "#9cc7ff",
    "whiff": "#7d8a8c",
    "amber": "#f0a848",
    "boss": "#a97cf2",
}
TEXTS = {"ink": "#eef3f0", "muted": "#a3b5b3", **ROLES}
GROUNDS = {
    "ground": "#0e1c26",
    "floor1_top": "#1d3340",
    "floor1_bottom": "#0f1a22",
    "floor3_top": "#1a1f3a",
    "floor3_bottom": "#0b0d1a",
    "floor5_top": "#1a0e12",
    "floor5_bottom": "#070507",
}

MACHADO = {
    "protan": np.array([[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]]),
    "deutan": np.array([[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.011820, 0.042940, 0.968881]]),
    "tritan": np.array([[1.255528, -0.076749, -0.178779], [-0.078411, 0.930809, 0.147602], [0.004733, 0.691367, 0.303900]]),
}
SRGB_TO_XYZ = np.array([[0.4124564, 0.3575761, 0.1804375], [0.2126729, 0.7151522, 0.0721750], [0.0193339, 0.1191920, 0.9503041]])
D65 = np.array([0.95047, 1.0, 1.08883])


def hex_to_linear(value: str) -> np.ndarray:
    v = value.lstrip("#")
    srgb = np.array([int(v[i : i + 2], 16) for i in (0, 2, 4)], dtype=float) / 255.0
    return np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)


def luminance(value: str) -> float:
    lin = hex_to_linear(value)
    return float(0.2126 * lin[0] + 0.7152 * lin[1] + 0.0722 * lin[2])


def contrast(a: str, b: str) -> float:
    la, lb = sorted((luminance(a), luminance(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)


def to_lab(lin: np.ndarray) -> np.ndarray:
    xyz = SRGB_TO_XYZ @ np.clip(lin, 0.0, 1.0) / D65
    delta = 6 / 29
    f = np.where(xyz > delta**3, np.cbrt(xyz), xyz / (3 * delta**2) + 4 / 29)
    return np.array([116 * f[1] - 16, 500 * (f[0] - f[1]), 200 * (f[1] - f[2])])


def delta_e(a: str, b: str, vision: str) -> float:
    la, lb = hex_to_linear(a), hex_to_linear(b)
    if vision != "normal":
        m = MACHADO[vision]
        la, lb = m @ la, m @ lb
    return float(np.linalg.norm(to_lab(la) - to_lab(lb)))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--tokens", default=None)
    parser.add_argument("--de-threshold", type=float, default=20.0)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    roles, texts, grounds = ROLES, TEXTS, GROUNDS
    if args.tokens:
        with open(args.tokens, encoding="utf-8") as fh:
            data = json.load(fh)
        roles = data.get("roles", roles)
        texts = data.get("texts", {**texts, **roles})
        grounds = data.get("grounds", grounds)

    contrast_rows = []
    for (tn, tv), (gn, gv) in itertools.product(texts.items(), grounds.items()):
        ratio = contrast(tv, gv)
        contrast_rows.append({"text": tn, "ground": gn, "ratio": round(ratio, 2), "text_ok": ratio >= 4.5, "non_text_ok": ratio >= 3.0})

    de_rows = []
    for (an, av), (bn, bv) in itertools.combinations(roles.items(), 2):
        values = {v: round(delta_e(av, bv, v), 1) for v in ("normal", "protan", "deutan", "tritan")}
        worst = min(values, key=values.get)
        de_rows.append({"a": an, "b": bn, **values, "worst": worst, "ok": values[worst] >= args.de_threshold})

    failed_text = [r for r in contrast_rows if not r["text_ok"]]
    failed_de = [r for r in de_rows if not r["ok"]]

    if args.json:
        print(json.dumps({"contrast": contrast_rows, "delta_e": de_rows, "de_threshold": args.de_threshold}, ensure_ascii=False, indent=1))
    else:
        print("# contrast below 4.5:1 (text). non_text_ok shows the 3:1 check")
        for r in failed_text:
            print(f"{r['text']:>7} on {r['ground']:<14} {r['ratio']:>5}:1  non_text_ok={r['non_text_ok']}")
        print(f"\n# role pairs below delta E {args.de_threshold} (normal / protan / deutan / tritan)")
        for r in failed_de:
            print(f"{r['a']:>7} - {r['b']:<7} {r['normal']:>5} / {r['protan']:>5} / {r['deutan']:>5} / {r['tritan']:>5}  worst={r['worst']}")
        print(f"\ncontrast pairs {len(contrast_rows)}, below 4.5: {len(failed_text)}; role pairs {len(de_rows)}, below threshold: {len(failed_de)}")
    return 1 if failed_text or failed_de else 0


if __name__ == "__main__":
    sys.exit(main())
