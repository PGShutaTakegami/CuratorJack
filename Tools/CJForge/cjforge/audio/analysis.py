"""Numbers and a waveform/spectrogram PNG so an agent can check audio without listening."""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


def _db(value: float) -> float:
    return round(20.0 * np.log10(max(value, 1e-9)), 1)


def metrics(x: np.ndarray, sr: int, loop: bool) -> dict:
    mono = x.mean(axis=1)
    peak = float(np.max(np.abs(x)))
    level = np.abs(mono)
    onset = np.nonzero(level > peak * 0.1)[0]
    result = {
        "sec": round(len(x) / sr, 3),
        "ch": int(x.shape[1]),
        "peak_db": _db(peak),
        "rms_db": _db(float(np.sqrt(np.mean(x * x)))),
        "onset_ms": round(1000.0 * onset[0] / sr, 1) if len(onset) else None,
    }
    if loop:
        steps = np.abs(np.diff(x, axis=0))
        seam = float(np.max(np.abs(x[0] - x[-1])))
        # seam step relative to the loudest 1% of normal sample steps; <= 1 means no audible click
        result["loop_click"] = round(seam / (float(np.percentile(steps, 99)) + 1e-12), 2)
    return result


def waveform_png(path: Path, x: np.ndarray, sr: int, title: str) -> None:
    width, wave_h, spec_h = 1000, 180, 180
    img = Image.new("RGB", (width, wave_h + spec_h + 20), (18, 18, 22))
    draw = ImageDraw.Draw(img)
    mono = x.mean(axis=1)
    n = len(mono)

    columns = np.array_split(mono, width) if n >= width else [mono[i:i + 1] for i in range(n)]
    mid = 20 + wave_h // 2
    for i, col in enumerate(columns):
        if len(col) == 0:
            continue
        lo, hi = float(col.min()), float(col.max())
        draw.line([(i, mid - hi * (wave_h // 2 - 4)), (i, mid - lo * (wave_h // 2 - 4))], fill=(222, 184, 106))
    draw.line([(0, mid), (width, mid)], fill=(70, 70, 80))

    frame, hop = 2048, max(1, n // width)
    if n > frame:
        starts = np.linspace(0, n - frame, width).astype(int)
        window = np.hanning(frame)
        spectra = np.abs(np.fft.rfft(np.stack([mono[s:s + frame] * window for s in starts]), axis=1))
        freqs = np.fft.rfftfreq(frame, 1.0 / sr)
        rows = np.geomspace(30.0, min(16000.0, sr / 2), spec_h)[::-1]
        idx = np.clip(np.searchsorted(freqs, rows), 0, len(freqs) - 1)
        mag = 20.0 * np.log10(spectra[:, idx].T + 1e-9)
        mag = np.clip((mag - (mag.max() - 80.0)) / 80.0, 0.0, 1.0)
        rgb = np.stack([mag ** 0.7 * 255, mag ** 1.5 * 200, (1 - mag) * mag * 4 * 120], axis=2).astype(np.uint8)
        img.paste(Image.fromarray(rgb, "RGB"), (0, 20 + wave_h))
    seconds_total = n / sr
    step = 1.0 if seconds_total > 3 else 0.1
    t = 0.0
    while t <= seconds_total:
        px = int(t / seconds_total * (width - 1)) if seconds_total > 0 else 0
        draw.line([(px, 20), (px, 26)], fill=(120, 120, 130))
        t += step
    draw.text((4, 3), f"{title}  {seconds_total:.2f}s  (top: waveform / bottom: spectrogram 30Hz-16kHz, log)", fill=(200, 200, 210))
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)
