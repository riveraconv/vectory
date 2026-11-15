# Texturas del casco 3D de VECTOR (spec docs/cascos-3d-1-vector.md, 3.5 y 3.6), sin Blender: mismo algoritmo y
# mismas semillas que designs/casco-3d-vector.html (carbonSet, paintSet, brushedSet y scorchTex). Su mkRng, valueNoise,
# fbm y fromArrays son los del prototipo de BLADE: se reutilizan de textures_military sin tocarlos. Los Float32Array y
# Uint8ClampedArray de JavaScript se repiten con float32 y con redondeo a par (np.rint), así salen los mismos bytes.
import os
import struct
import zlib

import numpy as np

from .textures_military import N, Rng, _clamped, fbm, from_arrays, value_noise

_MASK = 0xFFFFFFFF


def _f32(a):
    return np.asarray(a, dtype=np.float64).astype(np.float32)


# Carbono en sarga 2×2: mechas de 16 px; las dos direcciones con brillo distinto (0,62)
def carbon_set(seed, satin):
    r = Rng(seed)
    m = fbm(128, 3, r).astype(np.float64)
    fine = value_noise(1, r).astype(np.float64)
    t = 16
    y, x = np.mgrid[0:N, 0:N]
    ci, cj = x // t, y // t
    warp = ((ci - cj) % 4) < 2
    across = np.where(warp, (x % t) / t, (y % t) / t)
    along = np.where(warp, (y % t) / t, (x % t) / t)
    sh = np.sin(np.pi * across)
    end = 0.82 + 0.18 * np.sin(np.pi * along)
    b = np.where(warp, 1.0, 0.62)
    k = (0.5 + 0.5 * sh) * b * end * (0.92 + 0.12 * m) + (fine - 0.5) * 0.04
    color = np.stack([_f32(64 * k), _f32(67 * k), _f32(75 * k)], axis=-1)
    rough = _f32(0.30 + 0.16 * (1 - sh) + satin)
    height = _f32(sh * 0.6 * end)
    return from_arrays(color, rough, height, 2)


# Pintura: color con piel de naranja muy suave y un poco de metalizado fino
def paint_set(seed, rgb):
    r = Rng(seed)
    peel = fbm(8, 3, r).astype(np.float64)
    flake = value_noise(1, r).astype(np.float64)
    m = fbm(160, 3, r).astype(np.float64)
    k = 0.97 + 0.04 * m + (flake - 0.5) * 0.035
    color = np.stack([_f32(np.minimum(255.0, rgb[c] * k)) for c in range(3)], axis=-1)
    rough = _f32(0.22 + 0.06 * peel + 0.05 * (flake - 0.5))
    height = _f32(peel * 0.35)
    return from_arrays(color, rough, height, 0.7)


# Metal cepillado en filas; con heat, tintes de calor azules y dorados del titanio
def brushed_set(seed, rgb, heat):
    r = Rng(seed)
    row = r.many(N).astype(np.float64)[:, None]
    m = fbm(96, 4, r).astype(np.float64)
    h = fbm(192, 3, r).astype(np.float64)
    n = row * 0.35 + m * 0.65
    k = 0.86 + 0.2 * n
    blue, gold = (92, 110, 170), (190, 150, 88)
    channels = []
    for c in range(3):
        v = float(rgb[c])
        if heat:
            col = np.where(h < 0.5, v + (blue[c] - v) * (0.5 - h) * 1.6, v + (gold[c] - v) * (h - 0.5) * 1.4)
        else:
            col = np.full((N, N), v)
        channels.append(_f32(col * k))
    color = np.stack(channels, axis=-1)
    rough = _f32(0.26 + 0.14 * n)
    height = _f32(np.broadcast_to(row * 0.2, (N, N)))
    return from_arrays(color, rough, height, 1.2)


# Ruido de las quemaduras (scorchTex): un canal de 0 a 255
def scorch_noise(seed):
    r = Rng(seed)
    n = fbm(96, 5, r).astype(np.float64)
    return _clamped(np.maximum(0.0, np.minimum(255.0, n * 255.0)))


# Máscara de un nivel de daño: smoothstep(th, th + 0,16, ruido) con th = 1 − daño, como el shader del prototipo
def scorch_mask(noise, damage):
    th = 1.0 - damage
    v = noise.astype(np.float64) / 255.0
    t = np.clip((v - th) / 0.16, 0.0, 1.0)
    return _clamped(t * t * (3 - 2 * t) * 255.0)


SCORCH_DAMAGE = (0.24, 0.36, 0.48)  # niveles 1, 2 y 3


def _chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & _MASK)


# PNG de 8 bits en gris, con la fila 0 arriba y sin fecha
def write_gray_png(path, gray):
    height, width = gray.shape
    raw = b"".join(b"\x00" + gray[y].tobytes() for y in range(height))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 0, 0, 0, 0)))
        f.write(_chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(_chunk(b"IEND", b""))


# Textura de prueba de las calcomanías (solo para la captura de la librea): cuadrícula, flecha hacia arriba y una
# esquina roja arriba a la derecha, para ver si alguna sale girada o en espejo. RGBA, fila 0 arriba.
def decal_test_pattern(width, height):
    img = np.zeros((height, width, 4), dtype=np.uint8)
    img[..., :3] = (230, 230, 230)
    img[..., 3] = 255
    y, x = np.mgrid[0:height, 0:width]
    step = max(8, min(width, height) // 8)
    lines = ((x % step) < 3) | ((y % step) < 3)
    img[lines, :3] = (40, 40, 40)
    cx = width / 2
    # flecha: asta y punta hacia la fila 0
    shaft = (np.abs(x - cx) < width * 0.04) & (y > height * 0.35) & (y < height * 0.85)
    head = (y >= height * 0.12) & (y <= height * 0.4) & (np.abs(x - cx) <= (y - height * 0.12) * (width * 0.18) / (height * 0.28))
    img[shaft | head, :3] = (20, 60, 220)
    corner = (x > width * 0.8) & (y < height * 0.2)
    img[corner, :3] = (220, 30, 30)
    return img
