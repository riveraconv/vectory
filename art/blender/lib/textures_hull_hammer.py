# Texturas del casco 3D de HAMMER, variante MINERO (spec docs/cascos-3d-3-hammer.md, 3.5), sin Blender.
# La chapa oscura, el acero, la chapa lagrimada y las franjas son los de la cabina de HAMMER (textures_industrial:
# plate_set(82), steel_set(83), tread_set(84) y hazard_set(85), con las mismas semillas); el titanio y el cobre, el
# metal cepillado de VECTOR (textures_hull.brushed_set). Lo nuevo es la pintura como máscara.
#
# La pintura cambia en el juego, así que no se guarda su color: plateSet es lineal en el color de la pintura,
#   color = pintura × MUL + ADD
# con MUL (un canal) = k · (1 − desconchón) · (1 − 0,35 · arañazo) · junta y ADD (RGB) lo que no depende de la
# pintura (metal de los desconchones, arañazos y brillo de los remaches, con la misma junta). Godot reconstruye el
# color con las dos máscaras. Los ruidos se sacan con las mismas llamadas, en el mismo orden, que plate_set de
# textures_industrial (que no se toca, para no cambiar ni un byte de la cabina).
import numpy as np

from .textures_industrial import scratch_map
from .textures_military import N, Rng, _clamped, fbm, from_arrays, value_noise

MUL_SCALE = 1.25                  # MUL va de 0 a 1,12: se guarda como MUL / 1,25
CHIP_METAL = (118, 116, 108)      # metal de los desconchones
SCRATCH = (172, 168, 156)         # arañazos
RIVET_GLOW = 26                   # brillo de los remaches


def _f32(a):
    return np.asarray(a, dtype=np.float64).astype(np.float32)


# Capas de plateSet(seed, pintura): MUL (N × N), ADD (N × N × 3), rugosidad y relieve, en float64
def plate_layers(seed):
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
    joint = np.where(seam > 0, 0.35, 1.0)
    mul = k * (1 - ch) * (1 - sc * 0.35) * joint
    add = np.stack([(CHIP_METAL[c] * ch * (0.9 + 0.2 * fine) * (1 - sc * 0.35) + SCRATCH[c] * sc * 0.35) * joint
                    + riv * RIVET_GLOW for c in range(3)], axis=-1)
    rough = _f32(0.64 + 0.14 * m + 0.15 * g - 0.3 * ch - 0.3 * sc - 0.15 * riv)
    height = _f32(m * 0.2 + fine * 0.04 + riv * 0.9 - seam * 0.6 - ch * 0.15 - sc * 0.05)
    return mul, add, rough, height


# Máscaras de la pintura en 8 bits: MUL / 1,25 en gris (N × N) y ADD en RGB (N × N × 3)
def paint_masks(seed):
    mul, add, _, _ = plate_layers(seed)
    return _clamped(mul / MUL_SCALE * 255.0), _clamped(add)


# Rugosidad y normal de la pintura (no dependen del color); el color va como la máscara MUL en gris
def paint_surface(seed):
    mul, _, rough, height = plate_layers(seed)
    maps = from_arrays(np.stack([_f32(mul / MUL_SCALE * 255.0)] * 3, axis=-1), rough, height, 4)
    return maps


# Color reconstruido desde las máscaras de 8 bits, como lo hará Godot (para comprobarlas)
def reconstruct(mul8, add8, paint):
    mul = mul8.astype(np.float64) / 255.0 * MUL_SCALE
    return _clamped(np.stack([paint[c] * mul + add8[..., c].astype(np.float64) for c in range(3)], axis=-1))
