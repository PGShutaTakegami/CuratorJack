"""カード画像の外枠（ふち）の色だけを塗り替える。

外周の帯（--band ピクセル）の中で、赤みの強い画素を指定した色相に回す。
絵の部分には触れないので、同じ構図の赤枠カードを緑枠などに変えられる。

例: python recolor_frame.py reversal.png out.png --hue 130
"""

import argparse
import colorsys

import numpy as np
from PIL import Image


def recolor(src: str, dst: str, hue_deg: float, value_scale: float, band: int) -> int:
    image = Image.open(src).convert("RGBA")
    pixels = np.asarray(image).astype(np.float32) / 255.0
    height, width = pixels.shape[:2]

    ys, xs = np.mgrid[0:height, 0:width]
    in_band = (xs < band) | (xs >= width - band) | (ys < band) | (ys >= height - band)
    r, g, b, a = pixels[..., 0], pixels[..., 1], pixels[..., 2], pixels[..., 3]
    reddish = (r - np.maximum(g, b) > 0.08) & (a > 0)
    mask = in_band & reddish

    hue = hue_deg / 360.0
    out = pixels.copy()
    for y, x in zip(*np.nonzero(mask)):
        _, s, v = colorsys.rgb_to_hsv(r[y, x], g[y, x], b[y, x])
        out[y, x, :3] = colorsys.hsv_to_rgb(hue, s, min(1.0, v * value_scale))

    Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), "RGBA").save(dst)
    return int(mask.sum())


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("src")
    parser.add_argument("dst")
    parser.add_argument("--hue", type=float, default=130.0, help="塗り替え後の色相（度）。緑は 130 前後")
    parser.add_argument("--value", type=float, default=0.85, help="明るさの倍率")
    parser.add_argument("--band", type=int, default=40, help="外周から何ピクセルまでを枠とみなすか")
    args = parser.parse_args()
    count = recolor(args.src, args.dst, args.hue, args.value, args.band)
    print(f"{count} pixels recolored -> {args.dst}")


if __name__ == "__main__":
    main()
