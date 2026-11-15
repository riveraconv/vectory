# Texturas del casco 3D de BLADE (spec docs/cascos-3d-2-blade.md, 3.5), sin Blender: mismo algoritmo y mismas semillas
# que designs/casco-3d-blade.html (camoSet, hazardSet y antiSlipSet del casco). Su ruido es el de textures_military
# (mkRng, valueNoise, fbm y fromArrays son los mismos), que no cambia. La cabina de BLADE tiene un hazard_set y un
# anti_slip_set con otro algoritmo: estos llevan el prefijo hull_ para no confundirlos.
# La pintura (paintSet) y el metal (brushedSet) son los del casco de VECTOR (textures_hull).
import numpy as np

from .textures_military import N, Rng, _clamped, fbm, from_arrays, value_noise

CHIP_METAL = (118, 120, 116)   # imprimación metálica que asoma en los desconchones


def _f32(a):
    return np.asarray(a, dtype=np.float64).astype(np.float32)


# Ruidos de camoSet en su orden: tono 1, tono 2, desconchones y grano
def _camo_noise(seed):
    r = Rng(seed)
    n1 = fbm(128, 4, r).astype(np.float64)
    n2 = fbm(96, 3, r).astype(np.float64)
    chips = fbm(8, 3, r).astype(np.float64)
    grain = value_noise(1, r).astype(np.float64)
    return n1, n2, chips, grain


# 0 = base (c0), 1 = claro (c1), 2 = oscuro (c2); oscuro si n1 < 0,44, si no claro si n2 > 0,55
def _camo_tone(n1, n2):
    return np.where(n1 < 0.44, 2, np.where(n2 > 0.55, 1, 0))


def _camo_surface(chip, grain):
    rough = _f32(np.where(chip, 0.38, 0.68 + 0.08 * grain))
    height = _f32(np.where(chip, 0.0, 0.35 + grain * 0.1))
    return rough, height


# camoSet(seed, c0, c1, c2) del prototipo: camuflaje de bordes duros con desconchones (color, rugosidad y normal)
def camo_set(seed, c0, c1, c2):
    n1, n2, chips, grain = _camo_noise(seed)
    tone = _camo_tone(n1, n2)
    chip = chips > 0.79
    k = 0.93 + 0.1 * grain
    tones = (c0, c1, c2)
    channels = []
    for c in range(3):
        base = np.choose(tone, [float(t[c]) for t in tones])
        channels.append(_f32(np.minimum(255.0, np.where(chip, float(CHIP_METAL[c]), base) * k)))
    rough, height = _camo_surface(chip, grain)
    return from_arrays(np.stack(channels, axis=-1), rough, height, 1.4)


# Máscara del camuflaje (lo que no depende de la pintura), con la rugosidad y la normal de camoSet:
# R = tono (0 base, 128 claro, 255 oscuro) · G = desconchón (255) · B = brillo del grano, (k − 0,9) / 0,2.
# Godot reconstruye el color: (G ? metal : tono[R]) × (0,9 + 0,2 × B).
def camo_mask(seed):
    n1, n2, chips, grain = _camo_noise(seed)
    tone = _camo_tone(n1, n2)
    chip = chips > 0.79
    k = 0.93 + 0.1 * grain
    mask = np.stack([
        np.choose(tone, [0, 128, 255]).astype(np.uint8),
        np.where(chip, 255, 0).astype(np.uint8),
        _clamped((k - 0.9) / 0.2 * 255.0),
    ], axis=-1)
    rough, height = _camo_surface(chip, grain)
    maps = from_arrays(np.zeros((N, N, 3), dtype=np.float32), rough, height, 1.4)
    maps["color"] = mask
    return maps


# Color reconstruido desde la máscara, como hará el shader de Godot (para comparar con camo_set)
def camo_from_mask(mask, c0, c1, c2):
    tone = np.where(mask[..., 0] < 64, 0, np.where(mask[..., 0] < 192, 1, 2))
    chip = mask[..., 1] > 127
    k = 0.9 + 0.2 * mask[..., 2].astype(np.float64) / 255.0
    tones = (c0, c1, c2)
    channels = []
    for c in range(3):
        base = np.choose(tone, [float(t[c]) for t in tones])
        channels.append(np.minimum(255.0, np.where(chip, float(CHIP_METAL[c]), base) * k))
    return _clamped(np.stack(channels, axis=-1))


# hazardSet del casco: franjas diagonales amarillas y negras de 64 px, gastadas
def hull_hazard_set(seed):
    r = Rng(seed)
    wear = fbm(16, 3, r).astype(np.float64)
    grain = value_noise(1, r).astype(np.float64)
    y, x = np.mgrid[0:N, 0:N]
    yellow = ((x + y) % 128) < 64
    worn = wear > 0.72
    k = 0.92 + 0.1 * grain
    channels = []
    for c, (yc, kc, wc) in enumerate(((214, 20, 70), (170, 20, 66), (30, 18, 52))):
        base = np.where(worn, wc, np.where(yellow, yc, kc))
        channels.append(_f32(base * k))
    rough = _f32(np.where(worn, 0.8, 0.55))
    height = _f32(np.where(worn, 0.0, 0.3))
    return from_arrays(np.stack(channels, axis=-1), rough, height, 1)


# antiSlipSet del casco: grano grueso, oscuro y muy rugoso
def hull_antislip_set(seed):
    r = Rng(seed)
    grit = value_noise(1, r).astype(np.float64)
    clump = fbm(4, 2, r).astype(np.float64)
    gr = grit * 0.6 + clump * 0.4
    k = 0.85 + 0.3 * gr
    color = np.stack([_f32(30 * k), _f32(33 * k), _f32(30 * k)], axis=-1)
    rough = _f32(0.88 + 0.08 * gr)
    return from_arrays(color, rough, _f32(gr), 3)
