"""Audio file writers. .ogg (Vorbis) keeps the exact sample count in Unity; .mp3 gains ~25 ms of leading silence and padding."""

from pathlib import Path

import numpy as np

AUDIO_EXTENSIONS = ("ogg", "mp3")
_OGG_BLOCK = 4096


def audio_extension(kind: str, loop: bool) -> str:
    """SE and looping BGM need sample-exact timing, so only non-looping BGM is mp3."""
    return "mp3" if kind == "bgm" and not loop else "ogg"


def write_ogg(path: Path, x: np.ndarray, sr: int, quality: float) -> None:
    """x: (n, channels) float in -1..1. quality: Vorbis quality 0..1."""
    import soundfile

    path.parent.mkdir(parents=True, exist_ok=True)
    data = np.clip(x, -1.0, 1.0).astype(np.float32)
    with soundfile.SoundFile(str(path), "w", sr, data.shape[1], subtype="VORBIS", format="OGG",
                             compression_level=1.0 - quality) as out:
        # libsndfile's Vorbis writer overflows the stack on large single writes
        for start in range(0, len(data), _OGG_BLOCK):
            out.write(data[start:start + _OGG_BLOCK])


def write_mp3(path: Path, x: np.ndarray, sr: int, bitrate: int) -> None:
    """x: (n, channels) float in -1..1."""
    import lameenc

    pcm = (np.clip(x, -1.0, 1.0) * 32767.0).astype("<i2")
    encoder = lameenc.Encoder()
    encoder.set_bit_rate(bitrate)
    encoder.set_in_sample_rate(sr)
    encoder.set_channels(x.shape[1])
    encoder.set_quality(2)
    data = encoder.encode(pcm.tobytes()) + encoder.flush()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(bytes(data))
