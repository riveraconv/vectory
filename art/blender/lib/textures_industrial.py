# Texturas del estilo INDUSTRIAL (cabina de HAMMER, spec docs/cabinas-3d-3-hammer.md, 3.5), sin Blender: mismo
# algoritmo y mismas semillas que designs/cabina-3d-hammer.html (scratchMap, plateSet, steelSet, treadSet, hazardSet,
# smudgeTex). El ruido (mkRng, valueNoise, fbm, fromArrays) es el mismo que el del prototipo de BLADE, así que se
# reutiliza de textures_military sin tocarlo. Los Float32Array de JavaScript se repiten guardando en float32.
import math

import numpy as np

from .textures_military import N, Rng, fbm, from_arrays, smudge_texture, value_noise


def _f32(a):
    return np.asarray(a, dtype=np.float64).astype(np.float32)


def _rgb(channels):
    return np.stack([_f32(c) for c in channels], axis=-1)


# Math.round de JavaScript: la mitad redondea hacia arriba (−2,5 → −2; 2,5 → 3)
def _js_round(x):
    return math.floor(x + 0.5)


# Arañazos: segmentos rectos de 1 px rasterizados en un array, en el orden de llamadas del prototipo
def scratch_map(r, count):
    s = np.zeros(N * N, dtype=np.float32)
    for _ in range(count):
        x = r.next() * N
        y = r.next() * N
        a = r.next() * math.pi * 2
        length = 6 + r.next() * 46
        v = 0.3 + r.next() * 0.7
        ca, sa = math.cos(a), math.sin(a)
        t = 0
        while t < length:
            xi = ((_js_round(x + ca * t) % N) + N) % N
            yi = ((_js_round(y + sa * t) % N) + N) % N
            i = yi * N + xi
            s[i] = max(float(s[i]), v)
            t += 1
    return s.reshape(N, N)


# Chapa pintada atornillada: paneles de 256 px (filas alternas desplazadas 128), juntas, remaches, desconchones,
# arañazos y mugre
def plate_set(seed, base):
    r = Rng(seed)
    m = fbm(128, 4, r).astype(np.float64)
    grime = fbm(48, 3, r).astype(np.float64)
    chip = fbm(20, 3, r).astype(np.float64)
    fine = value_noise(2, r).astype(np.float64)
    sc = scratch_map(r, 50).astype(np.float64)
    P, ins, gap, rr = 256, 12, 32, 4.2
    y, x = np.mgrid[0:N, 0:N]
    row = y // P
    lx = (((x + row * 128) % P) + P) % P
    ly = y % P
    seam = ((lx < 3) | (ly < 3)).astype(np.float64)
    lxf = lx.astype(np.float64)
    lyf = ly.astype(np.float64)

    def row_r(cy):
        rx = np.minimum(P - ins, np.maximum(ins, np.floor((lxf - ins) / gap + 0.5) * gap + ins))
        return np.hypot(lxf - rx, lyf - cy)

    def col_r(cx):
        ry = np.minimum(P - ins, np.maximum(ins, np.floor((lyf - ins) / gap + 0.5) * gap + ins))
        return np.hypot(lxf - cx, lyf - ry)

    d = np.minimum(np.minimum(row_r(ins), row_r(P - ins)), np.minimum(col_r(ins), col_r(P - ins)))
    riv = np.where(d < rr, np.sqrt(np.maximum(0.0, 1 - (d / rr) ** 2)), 0.0)
    ch = np.where(chip > 0.86, np.minimum(1.0, (chip - 0.86) * 10), 0.0)
    g = np.maximum(0.0, grime - 0.55) * 1.1 + seam * 0.4
    k = (0.86 + 0.22 * m + 0.04 * fine) * (1 - np.minimum(0.6, g * 0.5))
    worn = (118, 116, 108)
    scratch = (172, 168, 156)
    channels = []
    for c in range(3):
        v = base[c] * k * (1 - ch) + worn[c] * ch * (0.9 + 0.2 * fine)
        v = v * (1 - sc * 0.35) + scratch[c] * sc * 0.35
        v = np.where(seam > 0, v * 0.35, v)
        v = v + riv * 26
        channels.append(v)
    rough = _f32(0.64 + 0.14 * m + 0.15 * g - 0.3 * ch - 0.3 * sc - 0.15 * riv)
    height = _f32(m * 0.2 + fine * 0.04 + riv * 0.9 - seam * 0.6 - ch * 0.15 - sc * 0.05)
    return from_arrays(_rgb(channels), rough, height, 4)


# Acero desnudo gastado (rayado en filas y con manchas)
def steel_set(seed):
    r = Rng(seed)
    row = r.many(N).astype(np.float64)[:, None]
    m = fbm(96, 4, r).astype(np.float64)
    stain = fbm(40, 3, r).astype(np.float64)
    sc = scratch_map(r, 90).astype(np.float64)
    n = row * 0.4 + m * 0.6
    st = np.maximum(0.0, stain - 0.55) * 1.8
    k = (0.84 + 0.24 * n) * (1 - st * 0.45)
    color = _rgb([132 * k + sc * 18, 128 * k + sc * 18, 120 * k * (1 - st * 0.15) + sc * 17])
    rough = _f32(0.40 + 0.16 * n + 0.2 * st - 0.15 * sc)
    height = _f32(row * 0.25 - sc * 0.05)
    return from_arrays(color, rough, height, 1.5)


# Chapa lagrimada (diamante en relieve, orientación alterna por celdas de 32 px)
def tread_set(seed):
    r = Rng(seed)
    m = fbm(96, 4, r).astype(np.float64)
    grime = fbm(32, 3, r).astype(np.float64)
    C = 32
    y, x = np.mgrid[0:N, 0:N]
    cx, cy = x // C, y // C
    u = (x % C) / C - 0.5
    v = (y % C) / C - 0.5
    s = np.where((cx + cy) % 2 == 0, 1.0, -1.0)
    a = (u + s * v) * 0.7071
    b = (u - s * v) * 0.7071
    dd = (a / 0.36) ** 2 + (b / 0.085) ** 2
    bump = np.where(dd < 1, np.sqrt(np.maximum(0.0, 1 - dd)), 0.0)
    g = np.maximum(0.0, grime - 0.4) * 1.5 * (1 - bump)
    k = (0.70 + 0.16 * m + bump * 0.35) * (1 - np.minimum(0.7, g * 0.6))
    color = _rgb([104 * k, 103 * k, 98 * k])
    rough = _f32(0.55 - bump * 0.22 + g * 0.25)
    height = _f32(bump * 0.8 + m * 0.1)
    return from_arrays(color, rough, height, 3)


# Franjas amarillas y negras poco gastadas (no es la de BLADE: otros colores y otro umbral de desgaste)
def hazard_set(seed):
    r = Rng(seed)
    w = fbm(32, 3, r).astype(np.float64)
    y, x = np.mgrid[0:N, 0:N]
    yellow = ((x + y) % 128) < 64
    worn = w > 0.80
    k = 0.88 + 0.16 * w
    channels = []
    for wc, yc, bc in ((86, 214, 20), (86, 158, 20), (80, 28, 18)):
        channels.append(np.where(worn, wc, np.where(yellow, yc, bc)) * k)
    rough = _f32(np.where(worn, 0.45, 0.65))
    height = _f32(np.where(worn, 0.0, 0.2))
    return from_arrays(_rgb(channels), rough, height, 2)


# Manchas del cristal: smudgeTex es la misma función que en BLADE, con otra semilla
def smudge(seed):
    return smudge_texture(seed)
