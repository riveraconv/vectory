# Texturas procedurales sin Blender: mismo algoritmo y mismas semillas que el prototipo (mkRng, valueNoise, fbm,
# fromArrays, carbonSet, brushedSet, fabricSet, smudgeTex). Los Float32Array y Uint8ClampedArray de JavaScript
# se repiten con float32 y con redondeo a par (np.rint), así salen los mismos bytes. PNG escrito a mano, sin fecha.
import os
import struct
import zlib

import numpy as np

N = 512
_MASK = 0xFFFFFFFF


# Xorshift de 32 bits: se enmascara en cada paso, como hace >>>= 0 en JavaScript
class Rng:
    def __init__(self, seed):
        self.s = (seed * 2654435761) & _MASK or 1

    def next(self):
        s = self.s
        s ^= (s << 13) & _MASK
        s ^= s >> 17
        s ^= (s << 5) & _MASK
        self.s = s
        return s / 4294967296


def _value_noise(cx, cy, rng):
    g = np.array([rng.next() for _ in range(cx * cy)], dtype=np.float32).astype(np.float64)
    ax = np.arange(N, dtype=np.float64)
    fy = ax / N * cy
    fx = ax / N * cx
    iy = np.floor(fy).astype(np.int64)
    ix = np.floor(fx).astype(np.int64)
    ty = fy - iy
    tx = fx - ix
    sy = (ty * ty * (3 - 2 * ty))[:, None]
    sx = (tx * tx * (3 - 2 * tx))[None, :]
    y0 = (iy % cy)[:, None]
    y1 = ((iy + 1) % cy)[:, None]
    x0 = (ix % cx)[None, :]
    x1 = ((ix + 1) % cx)[None, :]
    a = g[y0 * cx + x0]
    b = g[y0 * cx + x1]
    c = g[y1 * cx + x0]
    d = g[y1 * cx + x1]
    return (a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy).astype(np.float32)


def _fbm(rng, c0=4, octaves=5, sx=1, sy=1):
    out = np.zeros((N, N), dtype=np.float32)
    amp = 1.0
    total = 0.0
    for o in range(octaves):
        n = _value_noise(max(1, (c0 << o) * sx), max(1, (c0 << o) * sy), rng)
        out = (out.astype(np.float64) + n.astype(np.float64) * amp).astype(np.float32)
        total += amp
        amp *= 0.5
    return (out.astype(np.float64) / total).astype(np.float32)


def _clamped(a):
    return np.rint(np.clip(a, 0.0, 255.0)).astype(np.uint8)


def _from_arrays(height, color, rough, strength):
    h = height.astype(np.float64)
    dx = np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)
    dy = np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)
    nx = -dx * strength
    ny = dy * strength
    nz = np.ones_like(nx)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack([nx / l, ny / l, nz / l], axis=-1)
    r = _clamped(np.clip(rough.astype(np.float64), 0.0, 1.0) * 255.0)
    return {
        "color": color,
        "roughness": np.stack([r, r, r], axis=-1),
        "normal": _clamped((normal * 0.5 + 0.5) * 255.0),
    }


# Fibra de carbono (sarga 2 × 2)
def carbon_set():
    rng = Rng(91)
    cell = 16
    fib = _fbm(rng, 32, 3, 1, 1).astype(np.float64)
    y, x = np.mgrid[0:N, 0:N]
    horiz = ((x // cell + y // cell) % 4) < 2
    t = np.where(horiz, (y % cell) / cell, (x % cell) / cell)
    along = np.where(horiz, (x % (cell * 2)) / (cell * 2), (y % (cell * 2)) / (cell * 2))
    bump = np.sin(np.pi * t)
    height = (bump * 0.6 + np.sin(np.pi * along) * 0.15 + fib * 0.08).astype(np.float32)
    v = 14 + bump * np.where(horiz, 26, 12) + fib * 10
    color = np.stack([_clamped(v), _clamped(v * 1.02), _clamped(v * 1.08)], axis=-1)
    rough = (0.42 - bump * 0.12).astype(np.float32)
    return _from_arrays(height, color, rough, 1.0)


# Aluminio cepillado
def brushed_set(base, seed):
    rng = Rng(seed)
    streak = _fbm(rng, 2, 5, 1, 64).astype(np.float64)
    fine = _fbm(rng, 8, 3).astype(np.float64)
    height = (streak * 0.25).astype(np.float32)
    v = 0.85 + (streak - 0.5) * 0.35 + (fine - 0.5) * 0.05
    color = np.stack([_clamped(base[c] * v) for c in range(3)], axis=-1)
    rough = (0.4 + (streak - 0.5) * 0.16).astype(np.float32)
    return _from_arrays(height, color, rough, 0.6)


# Tejido antirreflejos
def fabric_set(base):
    rng = Rng(55)
    fine = _fbm(rng, 64, 3).astype(np.float64)
    mid = _fbm(rng, 8, 3).astype(np.float64)
    height = (fine * 0.5).astype(np.float32)
    v = 0.9 + (fine - 0.5) * 0.25 + (mid - 0.5) * 0.12
    color = np.stack([_clamped(base[c] * v) for c in range(3)], axis=-1)
    rough = np.full((N, N), 0.96, dtype=np.float32)
    return _from_arrays(height, color, rough, 1.2)


# Manchas del cristal
def smudge_texture():
    rng = Rng(31)
    n = _fbm(rng, 4, 5).astype(np.float64)
    v = _clamped(np.maximum(0.0, n - 0.45) * 2.2 * 255.0)
    return np.stack([v, v, v], axis=-1)


# Quita los textos de un PNG: Cycles guarda ahí su tiempo de render, y cambiaría el archivo en cada ejecución
def strip_png_text(path):
    with open(path, "rb") as f:
        data = f.read()
    out = [data[:8]]
    i = 8
    while i < len(data):
        length = struct.unpack(">I", data[i:i + 4])[0]
        end = i + 12 + length
        if data[i + 4:i + 8] != b"tEXt":
            out.append(data[i:end])
        i = end
    with open(path, "wb") as f:
        f.write(b"".join(out))


# PNG de 8 bits RGB, con la fila 0 arriba (la misma que el lienzo del prototipo)
def write_png(path, rgb):
    height, width, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(height))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & _MASK)

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))
