# Faros, luces de freno nuevas y toberas de retro de los cascos 3D, y la silueta para la sombra (spec
# docs/efectos-8-luces.md, 3). Común a los tres hull_<nave>.py: cada uno llama a su build_<nave>() al final de
# build_hull() y crea los vacíos de lamp_empties_<nave>(). Medidas de designs/faros-retro-*.html (los diseños elegidos:
# VECTOR · C VISERA, BLADE · C OJOS, HAMMER · A DEFENSA) y de la tabla 3.1 de la spec.
#
# Sin Blender salvo los materiales: la geometría son sopas de triángulos, como el resto de los cascos.
import math

import numpy as np

from . import geometry as g
from . import hull_geometry as hg
from . import textures as tx

T = g.translation
R = g.euler_xyz

HEAD = 0xFFF4E6          # lentes de los faros (BLADE, más fría)
HEAD_BLADE = 0xF4F7FF
HEAD_K = {"vector": 6.0, "blade": 7.0, "hammer": 7.0}   # intensidad de partida, la de cada maqueta
REFLECTOR_K = 0.35
BRAKE = 0xFF2A1E
BRAKE_K = 2.4
RETRO_GLOW_K = 0.15       # boca del retro en reposo
RETRO_MID = {"vector": 0x73CCFF, "blade": 0x73CCFF, "hammer": 0xFF8A3A}
GLASS_OPACITY = 0.16

# Retro a 45° hacia arriba en el casco y en la cabina (spec v1.2): dirección del chorro en coordenadas del casco
RETRO_DIR = (0.0, 1.0, -1.0)

# Materiales que Godot trata aparte (se suman a los COAT y TRANSPARENT de cada casco)
COAT = {"M_LampGlass": (1.0, 0.03)}
TRANSPARENT = {"M_LampGlass": GLASS_OPACITY}


# ---------- Materiales ----------

class LampMaterials:
    # m: los Materials del casco (su glow() apunta cada luz viva en m.emissive)
    def __init__(self, m, io, ship):
        self.m = m
        self.io = io
        self.ship = ship
        self.chrome = io.material("M_Chrome", base=io.hex_color(0xDFE4EA), metallic=1.0, roughness=0.10)
        self.housing = io.material("M_LampHousing", base=io.hex_color(0x15171B), metallic=0.6, roughness=0.45)
        self.glass = io.material("M_LampGlass", base=io.hex_color(0xFFFFFF), metallic=0.0, roughness=0.03,
                                 coat=1.0, coat_roughness=0.03, alpha=GLASS_OPACITY)
        self.retro_ti = io.material("M_RetroTitanium", base=io.hex_color(0x80858C), metallic=1.0, roughness=0.30)
        self.retro_inner = io.material("M_RetroInner", base=io.hex_color(0x040404), metallic=0.0, roughness=0.95)
        # La trampilla de BLADE, como en su maqueta (M_RetroFlap); con un M_PaintArmor saldría la máscara de camuflaje
        # sin pintar, porque Godot solo pinta las placas de su contrato
        self.flap = io.material("M_RetroFlap", base=io.hex_color(0x3B4038), metallic=0.6, roughness=0.5)

    def head(self, piece):
        color = HEAD_BLADE if self.ship == "blade" else HEAD
        return self.m.glow(piece, color, HEAD_K[self.ship])

    def brake(self, piece):
        return self.m.glow(piece, BRAKE, BRAKE_K)

    def retro_glow(self, piece):
        return self.m.glow(piece, RETRO_MID[self.ship], RETRO_GLOW_K)

    # Reflector del faro: metal que brilla un poco con el faro encendido. Pieza viva (tipo head): material propio
    def reflector(self, piece):
        color = HEAD_BLADE if self.ship == "blade" else HEAD
        self.m.emissive[piece] = (color, REFLECTOR_K)
        return self.io.material(f"M_Reflector_{piece}", base=self.io.hex_color(0xCFD6DE), metallic=1.0, roughness=0.14,
                                emission=self.io.hex_color(color), emission_strength=REFLECTOR_K)


# ---------- Ayudas ----------

def merged(parts):
    # Varias sopas con su matriz en una sola sopa (una pieza con nombre hecha de varias formas)
    soup = g.Soup([], [], [])
    for s, m in parts:
        t = g.transformed(s, m)
        soup.positions += t.positions
        soup.normals += [g._normalize(n) for n in t.normals]
        soup.uvs += t.uvs
    return soup


def open_cylinder(r0, r1, z0, z1, segments):
    # Tubo sin tapas a lo largo de Z: r0 en z0, r1 en z1 (CylinderGeometry abierto de three.js, girado)
    return hg.lathe([(r0, z0), (r1, z1)], segments)


def facing(pos, direction):
    # Matriz con su −Z hacia direction (fwdGroup de la maqueta), en pos
    f = g._normalize(direction)
    z = (-f[0], -f[1], -f[2])
    up = (0.0, 1.0, 0.0) if abs(f[1]) < 0.999 else (1.0, 0.0, 0.0)
    x = g._normalize(g._cross(up, z))
    y = g._cross(z, x)
    return g.from_basis(x, y, z, pos)


def sides():
    return (("L", -1), ("R", 1))


def _faro(b, lm, name, light, pos, r, depth):
    # Faro redondo (roundLamp de la maqueta): carcasa, tapa, aro, reflector, bombilla y cristal. Boca en z 0, cuerpo
    # hacia +Z (atrás), mirando hacia delante
    grp = T(pos)
    b.add(open_cylinder(r * 1.14, r * 1.14, 0.0, depth, 28), lm.housing, grp, f"{name}_Housing")
    b.add(g.circle(r * 1.14, 28), lm.housing, grp @ T((0.0, 0.0, depth)), f"{name}_Back")
    b.add(g.torus(r * 1.1, r * 0.075, 8, 32), lm.chrome, grp, f"{name}_Bezel")
    piece = f"{name}_Reflector"
    b.add(hg.lathe([(r * 1.04, 0.003), (r * 0.72, r * 0.42), (r * 0.34, r * 0.72)], 32), lm.reflector(piece), grp, piece)
    b.add(g.circle(r * 0.36, 24), lm.head(light), grp @ T((0.0, 0.0, r * 0.7)), light, True)
    b.add(g.circle(r * 1.08, 28), lm.glass, grp @ T((0.0, 0.0, -0.004)), f"{name}_Glass", True)


# ---------- Nombres (contrato con Godot) ----------

def _retro_names(parts):
    return [f"Retro_{s}_{p}" for s, _ in sides() for p in parts]


VECTOR_PIECES = ["Light_Head", "Headlight_Frame_F", "Headlight_Frame_R"] + _retro_names(("Scoop", "Inner", "Glow"))
BLADE_PIECES = (["Light_Brake", "Light_Head_L_Top", "Light_Head_R_Top", "Light_Head_L_Bottom", "Light_Head_R_Bottom"]
                + _retro_names(("Slot", "Glow", "Flap")))
_LAMP_PARTS = ("Housing", "Back", "Bezel", "Reflector", "Glass", "Bracket")
HAMMER_PIECES = (["Brake_Mount", "Light_Brake", "Brake_Frame"]
                 + [f"Headlight_{s}_{p}" for s, _ in sides() for p in _LAMP_PARTS]
                 + ["Light_Head_L", "Light_Head_R"]
                 + _retro_names(("Barrel", "Back", "Lip", "Clamp_0", "Clamp_1", "Mount", "Inner", "Glow")))

# Lo que va con material de luz (las piezas vivas nuevas)
VECTOR_LIGHTS = ["Light_Head", "Retro_L_Glow", "Retro_R_Glow"]
BLADE_LIGHTS = ["Light_Brake", "Light_Head_L_Top", "Light_Head_R_Top", "Light_Head_L_Bottom", "Light_Head_R_Bottom",
                "Retro_L_Glow", "Retro_R_Glow"]
HAMMER_LIGHTS = ["Light_Brake", "Light_Head_L", "Light_Head_R", "Headlight_L_Reflector", "Headlight_R_Reflector",
                 "Retro_L_Glow", "Retro_R_Glow"]


# ---------- VECTOR · VISERA + tomas dorsales ----------

def vector_retro_frame(k):
    z = -2.05
    y = hg.fus_top_y(0.28, z, hg.N_NOCTURNA) + 0.022
    return T((k * 0.28, y, z)) @ R((-0.08, 0.0, 0.0))


def build_vector(b, m, io):
    lm = LampMaterials(m, io, "vector")
    n = hg.N_NOCTURNA
    b.zone = "Fuselage"
    # banda de luz que abraza la punta del morro (secBand = fus_patch con su separación), con sus dos marcos oscuros
    b.add(hg.fus_patch(-0.32, math.pi + 0.32, -3.70, -3.64, n, 0.005), lm.head("Light_Head"), name="Light_Head",
          no_shadow=True)
    b.add(hg.fus_patch(-0.34, math.pi + 0.34, -3.715, -3.70, n, 0.006), lm.housing, name="Headlight_Frame_F")
    b.add(hg.fus_patch(-0.34, math.pi + 0.34, -3.64, -3.625, n, 0.006), lm.housing, name="Headlight_Frame_R")
    for s, k in sides():
        grp = vector_retro_frame(k)
        b.add(hg.box(0.10, 0.06, 0.28), m.paint, grp @ T((0.0, 0.0, 0.02)), f"Retro_{s}_Scoop")
        b.add(hg.box(0.075, 0.04, 0.02), lm.retro_inner, grp @ T((0.0, 0.0, -0.115)), f"Retro_{s}_Inner")
        piece = f"Retro_{s}_Glow"
        b.add(g.plane(0.072, 0.036), lm.retro_glow(piece), grp @ T((0.0, 0.0, -0.126)), piece, True)


def lamp_empties_vector():
    out = {}
    for s, k in sides():
        out[f"Spot_Head_{s}"] = (T((k * 0.10, 0.03, -3.74)), None)
        grp = vector_retro_frame(k)
        out[f"Retro_{s}"] = (grp, None)
        p = g.transform_point(grp, (0.0, 0.0, -0.128))
        out[f"Retro_{s}_Point"] = (facing(p, RETRO_DIR), RETRO_DIR)
    return out


# ---------- BLADE · OJOS + ranuras con trampilla ----------

# La maqueta las pone en (±0,35; −2,30), debajo de la placa de blindaje 1 del casco (x 0,18–0,46, z −2,53 a −1,87),
# que las tapa. Van al hueco libre entre las placas 0 y 1 (z −2,675 a −2,53), lo más cerca posible
BLADE_RETRO_X, BLADE_RETRO_Z = 0.30, -2.60


def blade_retro_frame(bg, k):
    z = BLADE_RETRO_Z
    return T((k * BLADE_RETRO_X, bg.fus_top_y(BLADE_RETRO_X, z), z))


def build_blade(b, m, io, bg):
    lm = LampMaterials(m, io, "blade")
    b.zone = "Fuselage"
    # freno en «=»: debajo de la cola blanca (0,16 × 0,03 × 0,01), separada lo mismo que su alto
    tail_y = bg.prof(bg.Z1)["top"] - 0.07
    b.add(hg.box(0.16, 0.03, 0.01), lm.brake("Light_Brake"), T((0.0, tail_y - 0.06, bg.Z1 + 0.006)), "Light_Brake", True)
    # rendijas que siguen el filo del morro, arriba y abajo
    for s, k in sides():
        top, bot = [], []
        for i in range(13):
            z = -4.15 + 0.55 * i / 12
            x = bg.fus_pt(z, 0.0)[0] - 0.02
            top.append((k * x, bg.fus_top_y(x, z) + 0.004, z))
        for i in range(9):
            z = -4.0 + 0.3 * i / 8
            x = bg.fus_pt(z, 0.0)[0] - 0.02
            bot.append((k * x, bg.fus_bot_y(x, z) - 0.004, z))
        piece = f"Light_Head_{s}_Top"
        b.add(g.tube(top, 0.007, max(12, len(top) * 8), 8), lm.head(piece), name=piece, no_shadow=True)
        piece = f"Light_Head_{s}_Bottom"
        b.add(g.tube(bot, 0.006, max(12, len(bot) * 8), 8), lm.head(piece), name=piece, no_shadow=True)
        # retro: hueco oscuro hundido en la piel, boca luminosa al frente y trampilla colgada de su bisagra
        grp = blade_retro_frame(bg, k)
        b.add(hg.box(0.17, 0.03, 0.12), lm.retro_inner, grp @ T((0.0, -0.012, 0.0)), f"Retro_{s}_Slot")
        piece = f"Retro_{s}_Glow"
        b.add(g.plane(0.15, 0.014), lm.retro_glow(piece), grp @ T((0.0, -0.004, -0.061)), piece, True)
        # la trampilla lleva el origen en la bisagra: Godot la gira en x sin más
        b.add(merged([(hg.box(0.19, 0.012, 0.135), T((0.0, 0.0, -0.0675)))]), lm.flap,
              grp @ T((0.0, 0.006, 0.065)), f"Retro_{s}_Flap")


def lamp_empties_blade(bg):
    out = {}
    for s, k in sides():
        zb = -3.95
        p = bg.fus_pt(zb, 0.0)
        out[f"Spot_Head_{s}"] = (T((k * p[0], p[1], zb - 0.03)), None)
        grp = blade_retro_frame(bg, k)
        out[f"Retro_{s}"] = (grp, None)
        out[f"Retro_{s}_FlapHinge"] = (grp @ T((0.0, 0.006, 0.065)), None)
        pt = g.transform_point(grp, (0.0, 0.004, -0.062))
        out[f"Retro_{s}_Point"] = (facing(pt, RETRO_DIR), RETRO_DIR)
    return out


# ---------- HAMMER · DEFENSA + toberas laterales; freno central ----------

def build_hammer(b, m, io):
    lm = LampMaterials(m, io, "hammer")
    b.zone = "Fuselage"
    # freno central en la tapa trasera, entre los motores y por encima de las toberas
    b.add(hg.box(0.36, 0.08, 0.03), lm.housing, T((0.0, 0.555, 2.215)), "Brake_Mount")
    b.add(g.plane(0.30, 0.05), lm.brake("Light_Brake"), T((0.0, 0.555, 2.2315)), "Light_Brake", True)
    w, h, f = 0.30, 0.05, 0.01
    frame = [(hg.box(w + 2 * f, f, 0.006), T((0.0, h / 2 + f / 2, 0.0))),
             (hg.box(w + 2 * f, f, 0.006), T((0.0, -h / 2 - f / 2, 0.0))),
             (hg.box(f, h, 0.006), T((w / 2 + f / 2, 0.0, 0.0))),
             (hg.box(f, h, 0.006), T((-w / 2 - f / 2, 0.0, 0.0)))]
    b.add(merged(frame), lm.chrome, T((0.0, 0.555, 2.2325)), "Brake_Frame")
    # faros redondos de camión encima del tubo de arriba de la defensa
    b.zone = "Extras"
    for s, k in sides():
        x, z = k * 0.40, -3.70
        _faro(b, lm, f"Headlight_{s}", f"Light_Head_{s}", (x, 0.255, z), 0.085, 0.13)
        b.add(hg.box(0.03, 0.09, 0.03), lm.housing, T((x, 0.17, z + 0.06)), f"Headlight_{s}_Bracket")
    # toberas de retro de acero a los lados del morro
    b.zone = "Fuselage"
    for s, k in sides():
        grp = T((k * 0.64, 0.10, -2.85))
        # el cañón, más ancho en la boca, como en la maqueta (0,09 delante, 0,085 detrás), abierto por delante
        b.add(open_cylinder(0.09, 0.085, -0.20, 0.20, 24), lm.retro_ti, grp, f"Retro_{s}_Barrel")
        b.add(g.circle(0.085, 24), lm.retro_ti, grp @ T((0.0, 0.0, 0.20)), f"Retro_{s}_Back")
        b.add(g.torus(0.09, 0.014, 8, 28), lm.retro_ti, grp @ T((0.0, 0.0, -0.20)), f"Retro_{s}_Lip")
        for i, zc in enumerate((-0.08, 0.10)):
            b.add(g.torus(0.093, 0.012, 6, 24), lm.housing, grp @ T((0.0, 0.0, zc)), f"Retro_{s}_Clamp_{i}")
        b.add(hg.box(0.10, 0.04, 0.24), lm.housing, grp @ T((-k * 0.06, 0.0, 0.01)), f"Retro_{s}_Mount")
        b.add(g.circle(0.078, 24), lm.retro_inner, grp @ T((0.0, 0.0, -0.19)), f"Retro_{s}_Inner")
        piece = f"Retro_{s}_Glow"
        b.add(g.circle(0.065, 24), lm.retro_glow(piece), grp @ T((0.0, 0.0, -0.192)), piece, True)


def lamp_empties_hammer():
    out = {}
    for s, k in sides():
        out[f"Spot_Head_{s}"] = (T((k * 0.40, 0.255, -3.72)), None)
        grp = T((k * 0.64, 0.10, -2.85))
        out[f"Retro_{s}"] = (grp, None)
        p = g.transform_point(grp, (0.0, 0.0, -0.205))
        out[f"Retro_{s}_Point"] = (facing(p, RETRO_DIR), RETRO_DIR)
    return out


# ---------- Silueta para la sombra (3.3) ----------

SHADOW_WIDTH = 256
SHADOW_MARGIN = 1.2
SHADOW_BLUR = 7.0     # px sobre 256 (blur(7px) de la maqueta: desviación típica)
SHADOW_SUPERSAMPLE = 4


def _triangles_xz(pieces, skip):
    tris = []
    for piece in pieces:
        if piece.name in skip:
            continue
        m = piece.matrix
        pts = np.array(piece.soup.positions, dtype=np.float64)
        if not len(pts):
            continue
        pts = pts @ np.asarray(m[:3, :3]).T + np.asarray(m[:3, 3])
        tris.append(pts[:, [0, 2]].reshape(-1, 3, 2))
    return np.concatenate(tris)


def _gaussian_blur(img, sigma):
    r = int(math.ceil(sigma * 3))
    x = np.arange(-r, r + 1, dtype=np.float64)
    k = np.exp(-x * x / (2 * sigma * sigma))
    k /= k.sum()
    pad = np.pad(img, r, mode="constant")
    tmp = np.zeros_like(pad)
    for i, w in enumerate(k):
        tmp[:, r:-r] += w * pad[:, i:i + pad.shape[1] - 2 * r]
    out = np.zeros_like(pad)
    for i, w in enumerate(k):
        out[r:-r, :] += w * tmp[i:i + pad.shape[0] - 2 * r, :]
    return out[r:-r, r:-r]


# Planta del casco vista desde arriba: blanco = nave, gris desenfocado, 256 px de ancho y el alto en proporción.
# La fila 0 es la del morro (z mínima) y la columna 0 la de x mínima. Devuelve lo que va al manifiesto.
def write_shadow(path, pieces, skip=()):
    tris = _triangles_xz(pieces, set(skip))
    lo = tris.reshape(-1, 2).min(axis=0) - SHADOW_MARGIN
    hi = tris.reshape(-1, 2).max(axis=0) + SHADOW_MARGIN
    width_m, length_m = hi - lo
    w = SHADOW_WIDTH
    h = int(round(w * length_m / width_m))
    ss = SHADOW_SUPERSAMPLE
    W, H = w * ss, h * ss
    px = width_m / W
    pz = length_m / H
    mask = np.zeros((H, W), dtype=bool)
    # a píxeles (centro de cada píxel en +0,5)
    tp = np.empty_like(tris)
    tp[..., 0] = (tris[..., 0] - lo[0]) / px - 0.5
    tp[..., 1] = (tris[..., 1] - lo[1]) / pz - 0.5
    for a, b, c in tp:
        x0 = max(0, int(math.floor(min(a[0], b[0], c[0]))))
        x1 = min(W - 1, int(math.ceil(max(a[0], b[0], c[0]))))
        y0 = max(0, int(math.floor(min(a[1], b[1], c[1]))))
        y1 = min(H - 1, int(math.ceil(max(a[1], b[1], c[1]))))
        if x1 < x0 or y1 < y0:
            continue
        area = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
        if abs(area) < 1e-12:
            continue
        xs = np.arange(x0, x1 + 1, dtype=np.float64)
        ys = np.arange(y0, y1 + 1, dtype=np.float64)
        X, Y = np.meshgrid(xs, ys)
        w0 = (b[0] - X) * (c[1] - Y) - (b[1] - Y) * (c[0] - X)
        w1 = (c[0] - X) * (a[1] - Y) - (c[1] - Y) * (a[0] - X)
        w2 = (a[0] - X) * (b[1] - Y) - (a[1] - Y) * (b[0] - X)
        if area > 0:
            inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        else:
            inside = (w0 <= 0) & (w1 <= 0) & (w2 <= 0)
        mask[y0:y1 + 1, x0:x1 + 1] |= inside
    small = mask.reshape(h, ss, w, ss).mean(axis=(1, 3))
    img = np.clip(_gaussian_blur(small, SHADOW_BLUR), 0.0, 1.0)
    v = np.round(img * 255.0).astype(np.uint8)
    tx.write_png(path, np.stack([v, v, v], axis=-1))
    center = (lo + hi) / 2
    return {
        "image": path.replace("\\", "/").split("/")[-1],
        "pixels": [w, h],
        "size": [round(float(width_m), 4), round(float(length_m), 4)],
        "center": [round(float(center[0]), 4), round(float(center[1]), 4)],
        "rows": "+z",
    }
