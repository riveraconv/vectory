# Texturas del estilo MILITAR (cabina de BLADE, spec docs/cabinas-3d-2-blade.md, 3.5), sin Blender: mismo algoritmo
# y mismas semillas que designs/cabina-3d-blade.html (mkRng, valueNoise, fbm, fromArrays, paintSet, crinkleSet,
# antiSlipSet, gunmetalSet, hazardSet, smudgeTex). Su ruido no es el de la cabina de VECTOR (textures.py), así que va
# aparte y aquel no cambia. Los Float32Array y Uint8ClampedArray de JavaScript se repiten con float32 y con redondeo a
# par (np.rint), así salen los mismos bytes.
import math

import numpy as np

N = 512
_MASK = 0xFFFFFFFF


# Xorshift de 32 bits del prototipo de BLADE: la semilla entra tal cual (el de VECTOR la multiplica)
class Rng:
    def __init__(self, seed):
        self.s = (seed & _MASK) or 1

    def next(self):
        s = self.s
        s ^= (s << 13) & _MASK
        s ^= s >> 17
        s ^= (s << 5) & _MASK
        self.s = s
        return s / 4294967296

    def many(self, count):
        return np.array([self.next() for _ in range(count)], dtype=np.float32)


# Math.round de JavaScript: la mitad redondea hacia arriba
def _js_round(x):
    return math.floor(x + 0.5)


def value_noise(cell, rng):
    g = max(1, _js_round(N / cell))
    v = rng.many(g * g).astype(np.float64)
    k = g / N
    ax = np.arange(N, dtype=np.float64) * k
    i0 = np.floor(ax).astype(np.int64)
    t = ax - i0
    s = t * t * (3 - 2 * t)
    lo = i0 % g
    hi = (i0 + 1) % g
    sy = s[:, None]
    sx = s[None, :]
    ya, yb = lo[:, None], hi[:, None]
    xa, xb = lo[None, :], hi[None, :]
    a = v[ya * g + xa]
    b = v[ya * g + xb]
    c = v[yb * g + xa]
    d = v[yb * g + xb]
    return ((a + (b - a) * sx) * (1 - sy) + (c + (d - c) * sx) * sy).astype(np.float32)


def fbm(cell, octaves, rng):
    out = np.zeros((N, N), dtype=np.float32)
    amp = 1.0
    total = 0.0
    for o in range(octaves):
        n = value_noise(max(1.0, cell / (1 << o)), rng)
        out = (out.astype(np.float64) + n.astype(np.float64) * amp).astype(np.float32)
        total += amp
        amp *= 0.5
    return (out.astype(np.float64) / total).astype(np.float32)


def _clamped(a):
    return np.rint(np.clip(a, 0.0, 255.0)).astype(np.uint8)


# fromArrays del prototipo: color en 0–255, rugosidad en 0–1, relieve en 0–1. Normal con la V hacia arriba (OpenGL)
def from_arrays(color, rough, height, strength):
    h = height.astype(np.float64)
    du = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * strength
    dv = (np.roll(h, 1, axis=0) - np.roll(h, -1, axis=0)) * strength
    nx = -du
    ny = -dv
    nz = np.ones_like(nx)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack([nx / l, ny / l, nz / l], axis=-1)
    r = _clamped(np.maximum(0.0, np.minimum(255.0, rough.astype(np.float64) * 255.0)))
    return {
        "color": _clamped(color.astype(np.float64)),
        "roughness": np.stack([r, r, r], axis=-1),
        "normal": _clamped((normal * 0.5 + 0.5) * 255.0),
    }


def _rgb(channels):
    return np.stack([c.astype(np.float32) for c in channels], axis=-1)


# Pintura gris verdosa mate con desconchones
def paint_set(seed, base):
    r = Rng(seed)
    m = fbm(128, 4, r).astype(np.float64)
    ch = fbm(24, 3, r).astype(np.float64)
    fine = value_noise(2, r).astype(np.float64)
    chip = np.where(ch > 0.76, np.minimum(1.0, (ch - 0.76) * 12), 0.0)
    k = 0.90 + 0.18 * m + 0.04 * fine
    gray = (126, 130, 128)
    color = _rgb([base[c] * k * (1 - chip) + gray[c] * chip for c in range(3)])
    rough = (0.70 + 0.16 * m - 0.32 * chip).astype(np.float32)
    height = (m * 0.25 + fine * 0.05 - chip * 0.35).astype(np.float32)
    return from_arrays(color, rough, height, 2.5)


# Pintura rugosa negra de paneles (crinkle)
def crinkle_set(seed):
    r = Rng(seed)
    a = value_noise(2, r).astype(np.float64)
    b = value_noise(5, r).astype(np.float64)
    m = fbm(96, 3, r).astype(np.float64)
    n = a * 0.6 + b * 0.4
    k = 0.88 + 0.22 * n + 0.08 * m
    color = _rgb([22 * k, 25 * k, 23 * k])
    rough = (0.80 + 0.12 * n).astype(np.float32)
    return from_arrays(color, rough, n.astype(np.float32), 5)


# Antideslizante del cubre-instrumentos y del panel antirreflejos del morro
def anti_slip_set(seed):
    r = Rng(seed)
    g = value_noise(1, r).astype(np.float64)
    m = fbm(64, 3, r).astype(np.float64)
    k = 0.82 + 0.30 * g + 0.12 * m
    color = _rgb([36 * k, 39 * k, 38 * k])
    rough = (0.93 + 0.05 * g).astype(np.float32)
    height = (g * 0.7 + m * 0.3).astype(np.float32)
    return from_arrays(color, rough, height, 3)


# Pavonado: metal de cañón cepillado
def gunmetal_set(seed):
    r = Rng(seed)
    row = r.many(N).astype(np.float64)[:, None]
    m = fbm(128, 3, r).astype(np.float64)
    n = row * 0.5 + m * 0.5
    k = 0.86 + 0.24 * n
    color = _rgb([88 * k, 93 * k, 99 * k])
    rough = (0.34 + 0.16 * n).astype(np.float32)
    height = np.broadcast_to(row * 0.3, (N, N)).astype(np.float32)
    return from_arrays(color, rough, height, 1.5)


# Franjas amarillas y negras con desgaste (tirador del cristal)
def hazard_set(seed):
    r = Rng(seed)
    w = fbm(32, 3, r).astype(np.float64)
    y, x = np.mgrid[0:N, 0:N]
    yellow = ((x + y) % 128) < 64
    worn = w > 0.72
    k = 0.9 + 0.15 * w
    channels = []
    for c, (yc, bc, wc) in enumerate(((214, 18, 70), (168, 18, 72), (26, 16, 70))):
        base = np.where(worn, wc, np.where(yellow, yc, bc))
        channels.append(base * k)
    rough = np.where(worn, 0.45, 0.6).astype(np.float32)
    height = np.where(worn, 0.0, 0.2).astype(np.float32)
    return from_arrays(_rgb(channels), rough, height, 2)


# Manchas del cristal
def smudge_texture(seed):
    r = Rng(seed)
    n = fbm(96, 5, r).astype(np.float64)
    v = np.maximum(0.0, (n - 0.45) * 2.4)
    c = _clamped(np.minimum(255.0, v * v * 255.0))
    return np.stack([c, c, c], axis=-1)


def _srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _linear_to_srgb(c):
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.maximum(c, 0.0), 1 / 2.4) - 0.055)


# Color de una textura multiplicado por un color lineal, como hace three.js con map × color (M_PaintDark).
# Se guarda en la propia textura: así glTF no necesita un factor sobre la imagen.
def tinted(color, factor):
    lin = _srgb_to_linear(color.astype(np.float64) / 255.0)
    out = np.stack([lin[..., c] * factor[c] for c in range(3)], axis=-1)
    return _clamped(_linear_to_srgb(out) * 255.0)
