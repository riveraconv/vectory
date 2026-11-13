# Cabina 3D de HAMMER, estilo INDUSTRIAL (spec docs/cabinas-3d-3-hammer.md). Traducción de buildCockpit() con
# VARIANT = 2 (disposición COLGANTE) de designs/cabina-3d-hammer.html: mismos números y mismo orden.
#
# Uso: blender -b --factory-startup --python art/blender/cockpit_hammer.py -- --out art/blender/out
# El .blend se regenera cada vez: todo vive en este script.
import datetime
import hashlib
import json
import math
import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402

from lib import blender_io as io  # noqa: E402
from lib import geometry as g  # noqa: E402
from lib import textures_industrial as tx  # noqa: E402
from lib.blade_builder import BladeBuilder, transformed  # noqa: E402

REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "assets", "cockpits")

# Colores de luz del prototipo (LIGHT y ST)
ACCENT = 0xFFA436
BEACON = 0xFF9A1A
BEACON_RED = 0xFF2A10
LED_GREEN = 0x7CFF3A
LED_AMBER = 0xFFA020
LAMP_FULL = 0xFFB000
SCREEN_LIGHT = 0xFFB000

# Intensidad base de cada luz (spec, 6.5 y 6.6): el modelo se exporta en reposo y Godot la cambia
ACCENT_SCREEN = 1.4
LED_NAV = 3.0
LED_PANEL = 1.5
LAMP_FULL_INTENSITY = 1.7   # seta con la carga llena
BEACON_OFF = 0.05

# Repetición de cada textura: se multiplica en las UV, así no hace falta KHR_texture_transform
REPEAT = {
    "M_Plate": 1.2, "M_PlateDark": 1.6, "M_PaintNose": 0.9,
    "M_Steel": 2.0, "M_Tread": 4.0, "M_Hazard": 6.0,
}

# Lo que glTF no lleva y aplica Godot (envMapIntensity, suma y transparencias del prototipo)
REFLECTIONS = {"M_PaintNose": 0.6, "M_CrtGlass": 1.4, "M_Glass": 1.3}
ADDITIVE_MATERIALS = {"M_GlassSmudge": 0.06}
TRANSPARENT_MATERIALS = {"M_ScreenShade": 0.7, "M_CrtGlass": 0.14, "M_Glass": 0.06, "M_GlassSmudge": 0.06}

ZONES = ["Nose", "Canard", "Dash", "Side_L", "Side_R", "Frame", "Console"]

SCREENS = ["Screen_Radar", "Screen_Damage", "Screen_Map", "Screen_Speed"]
LOOK = (0.0, 0.02, 0.12)

EYE_PITCH = -0.085  # la vista mira 4,9° hacia abajo; no va en el modelo, solo en las capturas

LABEL_BOOST = [f"Label_Boost_{i}" for i in (1, 2, 3)]


def contract_names():
    names = list(SCREENS)
    names += [f"{s}_Shade" for s in SCREENS]
    names += ["Accent_Radar", "Accent_Damage", "Accent_Map"]
    names += ["Led_Nose_L", "Led_Nose_R", "Led_Canard_L", "Led_Canard_R"]
    names += [f"Lamp_Boost_{i}" for i in (1, 2, 3)]
    names += ["Beacon_Boost_Lens", "Beacon_Damage_Lens", "Plate"]
    names += LABEL_BOOST + ["Label_Aux"]
    names += [f"Led_Panel_{i}" for i in range(1, 7)]
    names += ["Gauge_Hyd_Face", "Gauge_Temp_Face", "Glass_Front", "Glass_Smudge"]
    names += [f"{s}_Glass" for s in SCREENS]
    return names


# Piezas vivas con tamaño en el manifiesto: pantallas, placa, rótulos y esferas de los relojes pequeños
SIZED = SCREENS + ["Plate"] + LABEL_BOOST + ["Label_Aux", "Gauge_Hyd_Face", "Gauge_Temp_Face"]


def parse_out():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--out" in args:
        path = args[args.index("--out") + 1]
        return path if os.path.isabs(path) else os.path.join(REPO, path)
    return os.path.join(HERE, "out")


# ---------- Materiales ----------

class Materials:
    def __init__(self, texture_folder):
        plate = io.save_texture_set(texture_folder, "Plate", tx.plate_set(81, (84, 88, 76)))
        plate_dark = io.save_texture_set(texture_folder, "PlateDark", tx.plate_set(82, (50, 52, 48)))
        nose = io.save_texture_set(texture_folder, "PaintNose", tx.plate_set(87, (170, 132, 40)))
        steel = io.save_texture_set(texture_folder, "Steel", tx.steel_set(83))
        tread = io.save_texture_set(texture_folder, "Tread", tx.tread_set(84))
        hazard = io.save_texture_set(texture_folder, "Hazard", tx.hazard_set(85))
        smudge = io.save_texture_set(texture_folder, "GlassSmudge", {"color": tx.smudge(86)})

        self.plate = io.material("M_Plate", metallic=0.45, roughness=1.0, textures=plate)
        self.plate_dark = io.material("M_PlateDark", metallic=0.55, roughness=1.0, textures=plate_dark)
        self.paint_nose = io.material("M_PaintNose", metallic=0.35, roughness=1.0, textures=nose)
        self.steel = io.material("M_Steel", metallic=1.0, roughness=1.0, textures=steel)
        self.tread = io.material("M_Tread", metallic=0.9, roughness=1.0, textures=tread)
        self.hazard = io.material("M_Hazard", metallic=0.3, roughness=1.0, textures=hazard)
        self.rubber = io.material("M_Rubber", base=io.hex_color(0x111111), roughness=0.6)
        self.dark = io.material("M_Dark", base=io.hex_color(0x0B0B0A), roughness=0.9)
        self.sleeve_r = io.material("M_SleeveR", base=io.hex_color(0x6A1410), roughness=0.6)
        self.sleeve_y = io.material("M_SleeveY", base=io.hex_color(0x8A6A10), roughness=0.6)
        self.red_knob = io.material("M_RedKnob", base=io.hex_color(0xA01810), metallic=0.05, roughness=0.45)
        self.shade = io.material("M_ScreenShade", base=(0, 0, 0, 1), roughness=1.0, alpha=0.7)
        self.crt_glass = io.material("M_CrtGlass", base=(0, 0, 0, 1), roughness=0.35, alpha=0.14)
        self.glass = io.material("M_Glass", base=(0, 0, 0, 1), roughness=0.05, alpha=0.06)
        self.smudge = io.material("M_GlassSmudge", roughness=1.0, textures=smudge, alpha=0.06)
        self.emissive = {}  # nombre de la pieza → (color, intensidad)

    # Cada luz viva con su propio material, para que Godot la encienda, la apague o le cambie el color sola
    def glow(self, kind, piece, color, intensity, base=(0, 0, 0, 1)):
        self.emissive[piece] = (color, intensity)
        return io.material(f"M_{kind}_{piece}", base=base, roughness=0.4,
                           emission=io.hex_color(color), emission_strength=intensity)

    # Superficies que pinta Godot: pantallas, placa, rótulos y esferas
    @staticmethod
    def screen(piece):
        return io.material(f"M_Screen_{piece[7:]}", base=(0, 0, 0, 1), roughness=1.0)

    @staticmethod
    def label(piece, metallic=0.3):
        return io.material(f"M_Label_{piece}", base=(0, 0, 0, 1), metallic=metallic, roughness=0.6)


# ---------- Piezas del prototipo ----------

def cowl_y(x):
    return -0.200 - 0.028 * (x / 0.64) ** 2


def cowl_z(x):
    return -0.520 - 0.050 * (x / 0.64) ** 2


def mirror_x(points):
    return [(-p[0], p[1], p[2]) for p in points]


def lerp3(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


# Tornillo hexagonal mirando a +Z del padre
def hex_bolt(b, m, parent, x, y, z, r=0.006):
    return b.cyl(r, r, r * 0.8, 6, m.steel, parent, "Bolt", True, (x, y, z + r * 0.4), (math.pi / 2, 0, 0))


# Tornillo en un punto cualquiera, orientado hacia "look" (coordenadas de la raíz)
def bolt_at(b, m, parent, pos, look, r=0.006):
    grp = b.group(parent, "Bolt", pos, None, look)
    hex_bolt(b, m, grp, 0, 0, 0, r)
    return grp


# Línea de luz bajo una pantalla
def accent(b, m, parent, name, w, pos):
    return b.mesh(b.rbox(w, 0.003, 0.003, 0.001), m.glow("Emissive_Accent", name, ACCENT, ACCENT_SCREEN),
                  parent, name, True, pos)


# Rótulo fijo (placa grabada): solo el plano, el texto lo pone Godot
def label_plane(b, m, parent, name, w, h, pos):
    return b.mesh(g.plane(w, h), m.label(name), parent, name, True, pos)


# Reloj pequeño decorativo: bisel de acero, esfera (la pinta Godot una vez) y cristal
def mini_gauge(b, m, parent, name, pos):
    grp = b.group(parent, name, pos)
    r = 0.019
    b.cyl(r + 0.004, r + 0.005, 0.008, 20, m.steel, grp, f"{name}_Bezel", False, (0, 0, -0.0035), (math.pi / 2, 0, 0))
    b.mesh(g.circle(r, 24), m.label(f"{name}_Face", 0.1), grp, f"{name}_Face", True, (0, 0, 0.0008))
    b.mesh(g.circle(r, 24), m.crt_glass, grp, f"{name}_Glass", True, (0, 0, 0.002))


# CRT industrial: carcasa de chapa gruesa, cuello trasero, junta de goma, 4 tornillos, pantalla, cristal, sombra y
# línea de luz (sin asas ni visera: no las usa la disposición 2)
def make_crt(b, m, parent, name, pos, look, w, h, depth, bz, with_accent=True):
    grp = b.group(parent, f"{name}_Panel", pos, None, look)
    dep = depth
    hw, hh = w + bz * 2, h + bz * 2
    b.mesh(b.rbox(hw, hh, dep, 0.008), m.plate_dark, grp, f"{name}_Housing", pos=(0, 0, -dep / 2 - 0.002))
    b.mesh(b.rbox(hw * 0.7, hh * 0.7, dep * 0.8, 0.01), m.steel, grp, f"{name}_Neck", pos=(0, 0, -dep - dep * 0.38))
    for x, y, bw, bh in ((0, h / 2 + 0.004, w + 0.012, 0.008), (0, -h / 2 - 0.004, w + 0.012, 0.008),
                         (-w / 2 - 0.004, 0, 0.008, h + 0.016), (w / 2 + 0.004, 0, 0.008, h + 0.016)):
        b.mesh(b.rbox(bw, bh, 0.008, 0.002), m.rubber, grp, f"{name}_Gasket", pos=(x, y, 0.002))
    for sx, sy in ((-1, 1), (1, 1), (-1, -1), (1, -1)):
        hex_bolt(b, m, grp, sx * (hw / 2 - bz * 0.45), sy * (hh / 2 - bz * 0.45), 0, min(0.0065, bz * 0.28))
    b.mesh(g.plane(w, h), m.screen(name), grp, name, True, (0, 0, 0.0015))
    b.mesh(g.plane(w, h), m.crt_glass, grp, f"{name}_Glass", True, (0, 0, 0.004))
    b.mesh(g.plane(w, 0.012), m.shade, grp, f"{name}_Shade", True, (0, h / 2 - 0.004, 0.005), (0.9, 0, 0))
    if with_accent:
        accent(b, m, grp, f"Accent_{name[7:]}", w * 0.8, (0, -hh / 2 - 0.004, 0.0))
    return grp


# Baliza con jaula colgada del larguero
def beacon(b, m, parent, name, pos, color):
    grp = b.group(parent, name, pos)
    b.mesh(b.rbox(0.05, 0.014, 0.05, 0.004), m.plate_dark, grp, f"{name}_Base", pos=(0, 0.024, 0))
    lens = f"{name}_Lens"
    b.mesh(g.box_uv(g.sphere(0.016, 14, 10)), m.glow("Beacon", lens, color, BEACON_OFF, io.hex_color(0x1A0D04)),
           grp, lens, True)
    # En el prototipo las jaulas son mallas sueltas, sin boxUV ni sombra
    for a in range(4):
        b.mesh(g.torus(0.019, 0.0014, 6, 16, math.pi), m.steel, grp, f"{name}_Cage", True, None, (0, a * math.pi / 4, math.pi))
    return grp


# ---------- buildCockpit() → CockpitHammer ----------

def build_nose(b, m, root):
    nose = b.group(root, "Nose")
    st = [(-0.86, 0.92, -0.435), (-1.30, 0.86, -0.490), (-1.80, 0.74, -0.560), (-2.25, 0.56, -0.620), (-2.55, 0.40, -0.665)]

    def sec(s):
        z, w, y = s
        return [(-w, y - 0.22, z), (-w * 0.94, y - 0.04, z), (-w * 0.6, y, z),
                (w * 0.6, y, z), (w * 0.94, y - 0.04, z), (w, y - 0.22, z)]

    sections = [sec(s) for s in st]
    for k in range(5):
        b.mesh(b.strip([s[k] for s in sections], [s[k + 1] for s in sections]), m.paint_nose, nose, f"Nose_Skin_{k}")
    b.mesh(b.poly(sections[-1]), m.paint_nose, nose, "Nose_Tip")
    # Banda central de chapa lagrimada (antirreflejos)
    ag = [(w * 0.24, y + 0.002, z) for z, w, y in st]
    b.mesh(b.strip(mirror_x(ag), ag), m.tread, nose, "Nose_Tread")
    # Juntas y remaches
    for i, z in enumerate((-1.5, -2.0)):
        j = next(n for n, s in enumerate(st) if s[0] < z)
        sa, sb = st[j - 1], st[j]
        t = (z - sa[0]) / (sb[0] - sa[0])
        w = sa[1] + (sb[1] - sa[1]) * t
        y = sa[2] + (sb[2] - sa[2]) * t
        b.mesh(b.poly([(-w * 0.6, y + 0.0015, z - 0.005), (w * 0.6, y + 0.0015, z - 0.005),
                       (w * 0.6, y + 0.0015, z + 0.005), (-w * 0.6, y + 0.0015, z + 0.005)]),
               m.dark, nose, f"Nose_Seam_{i}", True)
        for q in range(-3, 4):
            b.sphere(0.006, m.steel, nose, "Rivet", True, (q / 3 * w * 0.56, y + 0.002, z + 0.016), (1.0, 0.4, 1.0))
    # Parachoques de acero en la punta
    b.beam((-0.44, -0.75, -2.57), (0.44, -0.75, -2.57), 0.07, 0.05, (0, -0.8, 0), m.steel, nose, "Nose_Bumper")
    # Balizas ámbar en los hombros del morro
    for sx in (-1, 1):
        name = "Led_Nose_L" if sx < 0 else "Led_Nose_R"
        b.sphere(0.014, m.glow("Emissive_Nav", name, BEACON, LED_NAV), nose, name, True, (sx * 0.74 * 0.95, -0.60, -1.80))


def build_canard(b, m, root):
    can = b.group(root, "Canard")
    for k in (-1, 1):
        r_le, r_te = (k * 0.38, -0.70, -2.85), (k * 0.38, -0.68, -2.30)
        t_le, t_te = (k * 2.35, -0.66, -2.95), (k * 2.35, -0.65, -2.62)

        def dn(p):
            return (p[0], p[1] - 0.07, p[2])

        b.mesh(b.poly([r_le, t_le, t_te, r_te]), m.paint_nose, can, "Canard_Top")
        b.mesh(b.poly([dn(r_le), dn(t_le), dn(t_te), dn(r_te)]), m.plate_dark, can, "Canard_Bottom")
        b.mesh(b.strip([r_le, t_le], [dn(r_le), dn(t_le)]), m.hazard, can, "Canard_LeadingEdge")
        b.mesh(b.strip([r_te, t_te], [dn(r_te), dn(t_te)]), m.plate_dark, can, "Canard_TrailingEdge")
        # Punta caída
        d_te, d_le = (k * 2.50, -1.12, -2.70), (k * 2.50, -1.12, -2.97)
        b.mesh(b.poly([t_le, t_te, d_te, d_le]), m.paint_nose, can, "Canard_Tip")
        b.mesh(b.poly([dn(t_le), dn(t_te), (d_te[0] - k * 0.05, d_te[1], d_te[2]), (d_le[0] - k * 0.05, d_le[1], d_le[2])]),
               m.plate_dark, can, "Canard_TipInner")
        name = "Led_Canard_L" if k < 0 else "Led_Canard_R"
        b.sphere(0.022, m.glow("Emissive_Nav", name, BEACON, LED_NAV), can, name, True, (k * 2.49, -1.11, -2.84))
        # Costillas atornilladas en la raíz
        for q in range(4):
            z = -2.38 - q * 0.13
            b.sphere(0.009, m.steel, can, "Rivet", True, (k * 0.46, -0.685, z), (1.0, 0.4, 1.0))
        b.beam((k * 0.40, -0.69, -2.40), (k * 1.6, -0.67, -2.62), 0.012, 0.012, (0, 1, -2.5), m.dark, can, "Canard_Seam")


def build_dash(b, m, root):
    dash = b.group(root, "Dash")
    face = b.group(dash, "Dash_Face", (0, -0.40, -0.565), None, LOOK)
    b.mesh(b.rbox(1.30, 0.40, 0.06, 0.01), m.plate_dark, face, "Dash_FacePanel")
    xs = [-0.64 + 1.28 * i / 16 for i in range(17)]
    rear = [(x, cowl_y(x), cowl_z(x)) for x in xs]
    front = [(x, -0.430 - 0.020 * (x / 0.64) ** 2, -0.86) for x in xs]
    b.mesh(b.strip(rear, front), m.tread, dash, "Glare_Top")
    lip = [(p[0], p[1] - 0.03, p[2] + 0.004) for p in rear]
    b.mesh(b.strip(rear, lip), m.steel, dash, "Glare_Lip")
    under = [(p[0], p[1] + 0.004, p[2] - 0.09) for p in lip]
    b.mesh(b.strip(lip, under), m.dark, dash, "Glare_Under", True)
    b.tube([(p[0], p[1] + 0.001, p[2] + 0.002) for p in rear], 0.008, 32, m.rubber, dash, "Glare_Edge")
    # Sin luz bajo el labio: Accent_Glare solo existe en las disposiciones 1 y 3
    for i in range(9):
        x = -0.56 + i * 0.14
        bolt_at(b, m, dash, (x, cowl_y(x) - 0.015, cowl_z(x) + 0.0065), (x, cowl_y(x) - 0.015, 1), 0.0055)


def build_sides(b, m, root):
    for k in (-1, 1):
        side = b.group(root, "Side_L" if k < 0 else "Side_R")
        b.mesh(b.rbox(0.04, 0.30, 0.50, 0.006), m.plate, side, "Cheek", pos=(k * 0.66, -0.39, -0.62))
        b.mesh(b.poly([(k * 0.64, cowl_y(0.64), cowl_z(0.64)), (k * 0.64, -0.445, -0.86), (k * 0.70, -0.24, -0.86),
                       (k * 0.70, -0.24, -0.40), (k * 0.64, cowl_y(0.64) - 0.03, cowl_z(0.64) + 0.06)]),
               m.plate, side, "Cheek_Top")
        b.mesh(b.rbox(0.10, 0.06, 1.40, 0.008), m.plate, side, "Sill", pos=(k * 0.72, -0.23, -0.25))
        b.mesh(b.poly([(k * 0.76, -0.25, -0.86), (k * 0.80, -0.25, 0.6), (k * 0.74, -0.95, 0.6), (k * 0.70, -0.95, -0.86)]),
               m.plate, side, "Wall")
        # Pilar A atornillado. El bucle del prototipo suma 0,12 en coma flotante: se repite igual
        p0, p1 = (k * 0.64, -0.44, -0.89), (k * 0.60, 0.35, -0.64)
        b.beam(p0, p1, 0.085, 0.08, (0, 0, 0.2), m.plate_dark, side, "Pillar_A")
        t = 0.08
        while t < 0.96:
            p = lerp3(p0, p1, t)
            off = g._scale(g._normalize((-p[0], -p[1], -p[2])), 0.042)
            bolt_at(b, m, side, g._add(p, off), (0, 0, 0), 0.0058)
            t += 0.12
        # Tira exterior y carril del techo
        b.beam((k * 0.68, 0.40, -0.62), (k * 0.86, -0.20, -0.66), 0.10, 0.05, (0, 0, 0.2), m.plate_dark, side, "Pillar_Outer")
        b.beam((k * 0.66, 0.42, -0.62), (k * 0.86, 0.44, 0.6), 0.08, 0.06, (0, 0, 0.1), m.plate_dark, side, "Roof_Rail")
        # Cables por el pilar con fundas de color
        c0 = g._add(lerp3(p0, p1, 0.05), (k * 0.055, 0, 0.02))
        c1 = g._add(lerp3(p0, p1, 0.95), (k * 0.055, 0, 0.03))
        b.tube([c1, (c1[0] + k * 0.01, (c0[1] + c1[1]) / 2, (c0[2] + c1[2]) / 2 + 0.03), c0,
                (c0[0] + k * 0.04, c0[1] - 0.10, c0[2] + 0.16)], 0.008, 24, m.rubber, side, "Cable")
        b.tube([(c1[0] + k * 0.016, c1[1], c1[2] + 0.01), (c0[0] + k * 0.018, c0[1] + 0.03, c0[2] + 0.02),
                (c0[0] + k * 0.07, c0[1] - 0.12, c0[2] + 0.18)], 0.0048, 20, m.sleeve_y if k < 0 else m.sleeve_r, side, "Cable_Sleeve")
        # Asidero con franjas
        b.tube([(k * 0.575, 0.22, -0.625), (k * 0.545, 0.19, -0.605), (k * 0.535, 0.03, -0.66), (k * 0.565, 0.00, -0.68)],
               0.009, 16, m.sleeve_y, side, "Grab_Handle")
        # Tubería lateral con abrazaderas (mallas sueltas en el prototipo: sin boxUV ni sombra)
        b.tube([(k * 0.735, -0.27, -0.82), (k * 0.745, -0.28, -0.2), (k * 0.74, -0.27, 0.5)], 0.014, 16, m.steel, side, "Pipe")
        z = -0.76
        while z < 0.5:
            b.mesh(g.torus(0.016, 0.0035, 6, 16), m.dark, side, "Pipe_Clamp", True, (k * 0.742, -0.277, z))
            z += 0.2


def build_frame(b, m, root):
    frame = b.group(root, "Frame")
    b.beam((-1.0, 0.37, -0.64), (1.0, 0.37, -0.64), 0.10, 0.12, (0, 0.37, 0.5), m.plate_dark, frame, "Header")
    b.mesh(g.plane(1.20, 0.045), m.hazard, frame, "Header_Hazard", True, (0, 0.345, -0.5785))
    x = -0.9
    while x <= 0.91:
        hex_bolt(b, m, frame, x, 0.398, -0.580, 0.0062)
        x += 0.15
    plate = b.group(frame, "Header_Plate", (0, 0.398, -0.5785))
    label_plane(b, m, plate, "Plate", 0.13, 0.016, (0, 0, 0.0012))
    b.mesh(b.poly([(-1.0, 0.42, -0.64), (1.0, 0.42, -0.64), (1.0, 0.46, 0.6), (-1.0, 0.46, 0.6)]), m.plate_dark, frame, "Roof", True)
    # Balizas: ámbar (boost) a la izquierda, roja (daño) a la derecha
    beacon(b, m, frame, "Beacon_Boost", (-0.45, 0.288, -0.585), BEACON)
    beacon(b, m, frame, "Beacon_Damage", (0.45, 0.288, -0.585), BEACON_RED)
    # Cristal con manchas y limpiaparabrisas aparcado
    b.mesh(b.poly([(-0.58, 0.33, -0.64), (0.58, 0.33, -0.64), (0.62, -0.42, -0.87), (-0.62, -0.42, -0.87)]),
           m.glass, frame, "Glass_Front", True)
    b.mesh(g.plane(1.25, 0.8), m.smudge, frame, "Glass_Smudge", True, (0, -0.045, -0.765), (-0.30, 0, 0))
    b.cyl(0.012, 0.014, 0.02, 12, m.steel, frame, "Wiper_Pivot", False, (0.12, -0.315, -0.826), (1.27, 0, 0))
    b.beam((0.12, -0.305, -0.824), (-0.44, -0.28, -0.816), 0.008, 0.008, (0, 0.4, 0), m.steel, frame, "Wiper_Arm")
    b.tube([(0.04, -0.298, -0.827), (-0.20, -0.292, -0.825), (-0.46, -0.286, -0.822)], 0.004, 12, m.rubber, frame, "Wiper_Blade")


# 2 · COLGANTE: pantallas colgadas del techo, salpicadero bajo y despejado
def build_colgante(b, m, root):
    con = b.group(root, "Console")
    panels = {}
    oh = b.group(con, "Overhead", (0, 0.205, -0.535), None, (0, -0.25, 0.25))
    b.mesh(b.rbox(0.72, 0.19, 0.10, 0.01), m.plate_dark, oh, "Overhead_Housing", pos=(0, 0, -0.052))
    for i in range(6):
        b.mesh(b.rbox(0.10, 0.006, 0.08, 0.002), m.steel, oh, "Overhead_Fin", pos=(-0.25 + i * 0.1, 0.098, -0.055))
    for x, y in ((-0.34, 0.08), (0.34, 0.08), (-0.34, -0.08), (0.34, -0.08)):
        hex_bolt(b, m, oh, x, y, 0, 0.0065)
    panels["Screen_Radar"] = make_crt(b, m, oh, "Screen_Radar", (-0.228, 0, 0.0), None, 0.17, 0.13, 0.04, 0.016)
    panels["Screen_Damage"] = make_crt(b, m, oh, "Screen_Damage", (0, 0, 0.0), None, 0.12, 0.12, 0.04, 0.014)
    panels["Screen_Map"] = make_crt(b, m, oh, "Screen_Map", (0.228, 0, 0.0), None, 0.17, 0.13, 0.04, 0.016)
    # Brazos y bisagras al larguero
    for k in (-1, 1):
        b.beam((k * 0.25, 0.335, -0.625), (k * 0.25, 0.275, -0.575), 0.02, 0.02, (0, 0, 1), m.steel, con, "Overhead_Arm")
        b.cyl(0.012, 0.012, 0.04, 12, m.steel, con, "Overhead_Hinge", False, (k * 0.25, 0.278, -0.575), (0, 0, math.pi / 2))
        b.tube([(k * 0.30, 0.33, -0.62), (k * 0.31, 0.31, -0.59), (k * 0.29, 0.29, -0.57)], 0.006, 10, m.rubber, con, "Overhead_Cable")
    # Salpicadero: tira de velocidad a lo ancho, sin línea de luz
    panels["Screen_Speed"] = make_crt(b, m, con, "Screen_Speed", (0, -0.262, -0.47), LOOK, 0.40, 0.075, 0.06, 0.022, False)
    # Pod derecho: boost en tres setas iluminadas con collarín
    bp = b.group(con, "Boost_Pod", (0.372, -0.288, -0.474), None, (0.372, 0.10, 0.20))
    b.mesh(b.rbox(0.215, 0.078, 0.03, 0.006), m.plate, bp, "Boost_PodBody", pos=(0, 0, -0.015))
    for x, y in ((-0.098, 0.03), (0.098, 0.03), (-0.098, -0.03), (0.098, -0.03)):
        hex_bolt(b, m, bp, x, y, 0, 0.005)
    for i in range(3):
        btn = b.group(bp, f"Boost_Button_{i + 1}", (-0.062 + i * 0.062, 0.004, 0))
        b.cyl(0.024, 0.027, 0.022, 20, m.steel, btn, "Button_Collar", False, (0, 0, 0.011), (math.pi / 2, 0, 0))
        lamp = f"Lamp_Boost_{i + 1}"
        b.cyl(0.018, 0.019, 0.016, 24, m.glow("Lamp", lamp, LAMP_FULL, LAMP_FULL_INTENSITY, io.hex_color(0x2A1A06)),
              btn, lamp, True, (0, 0, 0.028), (math.pi / 2, 0, 0))
        # Button_Label en el prototipo: renombrado para que sea único
        label_plane(b, m, btn, LABEL_BOOST[i], 0.034, 0.010, (0, -0.031, 0.0005))
    # Pod izquierdo: parada de emergencia, llave y selector (decorativos)
    lp = b.group(con, "Aux_Pod", (-0.372, -0.288, -0.474), None, (-0.372, 0.10, 0.20))
    b.mesh(b.rbox(0.215, 0.078, 0.03, 0.006), m.plate, lp, "Aux_PodBody", pos=(0, 0, -0.015))
    for x, y in ((-0.098, 0.03), (0.098, 0.03), (-0.098, -0.03), (0.098, -0.03)):
        hex_bolt(b, m, lp, x, y, 0, 0.005)
    b.cyl(0.03, 0.03, 0.004, 24, m.sleeve_y, lp, "EStop_Ring", False, (-0.058, 0.004, 0.002), (math.pi / 2, 0, 0))
    b.cyl(0.020, 0.024, 0.024, 24, m.red_knob, lp, "EStop", False, (-0.058, 0.004, 0.016), (math.pi / 2, 0, 0))
    b.cyl(0.012, 0.012, 0.01, 16, m.steel, lp, "Key_Barrel", False, (0.005, 0.004, 0.005), (math.pi / 2, 0, 0))
    b.mesh(b.rbox(0.004, 0.022, 0.004, 0.001), m.steel, lp, "Key", pos=(0.005, 0.004, 0.014))
    b.cyl(0.016, 0.018, 0.016, 6, m.rubber, lp, "Selector", False, (0.062, 0.004, 0.008), (math.pi / 2, 0, 0))
    # Aux_Label en el prototipo
    label_plane(b, m, lp, "Label_Aux", 0.17, 0.011, (0, -0.031, 0.0005))
    # Panel inferior bajo la tira de velocidad: interruptores con LED, dos relojes pequeños y franja de peligro
    lo = b.group(con, "Lower_Panel", (0, -0.362, -0.472), None, LOOK)
    b.mesh(b.rbox(0.46, 0.055, 0.02, 0.004), m.plate, lo, "Lower_PanelBody", pos=(0, 0, -0.01))
    b.mesh(g.plane(0.44, 0.008), m.hazard, lo, "Lower_Hazard", True, (0, -0.021, 0.0005))
    for x, y in ((-0.218, 0.018), (0.218, 0.018), (-0.218, -0.018), (0.218, -0.018)):
        hex_bolt(b, m, lo, x, y, 0, 0.0045)
    led_colors = [LED_GREEN, LED_AMBER, LED_GREEN, LED_GREEN, LED_AMBER, LED_GREEN]
    for i in range(6):
        x = -0.10 + i * 0.04
        tg = b.group(lo, f"Panel_Toggle_{i + 1}", (x, 0.002, 0))
        b.mesh(b.rbox(0.016, 0.020, 0.004, 0.001), m.dark, tg, "Panel_ToggleBase", pos=(0, -0.002, 0.002))
        lever = b.group(tg, "Panel_ToggleLever", (0, -0.002, 0.004), (-0.6 if i % 3 == 1 else 0.6, 0, 0))
        b.cyl(0.0016, 0.0022, 0.014, 8, m.steel, lever, "Panel_ToggleRod", False, (0, 0.007, 0))
        led = f"Led_Panel_{i + 1}"
        b.sphere(0.0025, m.glow("Emissive_PanelLed", led, led_colors[i], LED_PANEL), tg, led, True, (0, 0.0135, 0.002))
    mini_gauge(b, m, lo, "Gauge_Hyd", (-0.172, 0.004, 0))
    mini_gauge(b, m, lo, "Gauge_Temp", (0.172, 0.004, 0))
    # Tomas de aire justo debajo de los pods
    for k in (-1, 1):
        vg = b.group(con, "Dash_Vent_L" if k < 0 else "Dash_Vent_R", (k * 0.372, -0.360, -0.476), None, (k * 0.372, 0.10, 0.20))
        b.mesh(b.rbox(0.205, 0.068, 0.014, 0.004), m.plate_dark, vg, "Vent_Frame", pos=(0, 0, -0.005))
        b.mesh(b.rbox(0.186, 0.052, 0.006, 0.001), m.dark, vg, "Vent_Back", pos=(0, 0, 0.0025))
        for i in range(6):
            b.mesh(b.rbox(0.184, 0.0075, 0.004, 0.0008), m.steel, vg, "Vent_Slat", pos=(0, -0.0215 + i * 0.0086, 0.006), rot=(0.6, 0, 0))
        for sx, sy in ((-1, 1), (1, 1), (-1, -1), (1, -1)):
            hex_bolt(b, m, vg, sx * 0.095, sy * 0.028, 0.002, 0.004)
    return panels, oh


# ---------- De las piezas a objetos de Blender ----------

def uv_scale(material):
    return REPEAT.get(material.name, 1.0)


# Las piezas vivas del contrato van solas; el resto se junta por zona, material y sombra: menos objetos y menos
# llamadas de dibujo en Godot. Devuelve los objetos que no proyectan sombra.
def create_objects(builder, root, collection):
    contract = set(contract_names())
    zones = {zone: io.empty(zone, root, collection) for zone in ZONES}
    merged = {}
    no_shadow = []
    for piece in builder.pieces:
        if piece.name in contract:
            io.mesh_object(piece.name, piece.soup, piece.material, zones[piece.zone], collection, piece.matrix, uv_scale(piece.material))
            if piece.no_shadow:
                no_shadow.append(piece.name)
            continue
        key = (piece.zone, piece.material.name, piece.no_shadow)
        soup = transformed(piece.soup, piece.matrix)
        if key not in merged:
            merged[key] = (piece.material, g.Soup([], [], []))
        target = merged[key][1]
        target.positions += soup.positions
        target.normals += soup.normals
        target.uvs += soup.uvs
    for (zone, material_name, shadowless), (material, soup) in merged.items():
        name = f"{zone}_{material_name}" + ("_NoShadow" if shadowless else "")
        io.mesh_object(name, soup, material, zones[zone], collection, None, uv_scale(material))
        if shadowless:
            no_shadow.append(name)
    return no_shadow


# Falla con un mensaje claro si falta una pieza del contrato con Godot
def check_contract(root):
    present = {o.name for o in root.children_recursive}
    missing = [n for n in contract_names() if n not in present]
    if missing:
        raise RuntimeError("Faltan piezas del contrato con Godot: " + ", ".join(missing))


# De Blender (Z arriba) a Godot (Y arriba), como hace el exportador
def godot_point(obj, co):
    w = obj.matrix_world @ co
    return (w.x, w.z, -w.y)


def measure(root):
    low = [float("inf")] * 3
    high = [float("-inf")] * 3
    triangles = 0
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        triangles += len(obj.data.polygons)
        for v in obj.data.vertices:
            p = godot_point(obj, v.co)
            for i in range(3):
                low[i] = min(low[i], p[i])
                high[i] = max(high[i], p[i])
    return low, high, triangles


# Comprobación de ejes de la spec (3.3), con las piezas antes de juntarlas (coordenadas del prototipo = de Godot)
def check_axes(builder, overhead):
    def points(name):
        return [g.transform_point(p.matrix, v) for p in builder.pieces if p.name == name for v in p.soup.positions]

    tip = min(p[2] for p in points("Nose_Tip"))
    canard = points("Canard_Tip")
    canard_x = max(abs(p[0]) for p in canard)
    canard_y = min(p[1] for p in canard)
    oh = tuple(float(c) for c in overhead.world[:3, 3])
    header = [p[1] for p in points("Header")]
    header_y = (min(header) + max(header)) / 2
    ok = (abs(tip + 2.55) < 0.01 and abs(canard_x - 2.50) < 0.01 and abs(canard_y + 1.12) < 0.01
          and max(abs(oh[0]), abs(oh[1] - 0.205), abs(oh[2] + 0.535)) < 0.001 and abs(header_y - 0.37) < 0.005)
    if not ok:
        raise RuntimeError(f"Ejes mal: punta del morro z = {tip:.3f}, canard x = ±{canard_x:.3f} y = {canard_y:.3f}, "
                           f"bloque colgante {oh[0]:.3f}, {oh[1]:.3f}, {oh[2]:.3f}, larguero y = {header_y:.3f}")
    return tip, canard_x, canard_y, oh, header_y


def write_manifest(path, root, materials, no_shadow, env_name):
    low, high, triangles = measure(root)
    pieces = []
    for name in contract_names():
        obj = bpy.data.objects[name]
        entry = {"name": name}
        if name in SIZED:
            xs = [v.co.x for v in obj.data.vertices]
            ys = [v.co.y for v in obj.data.vertices]
            entry["width"] = round(max(xs) - min(xs), 4)
            entry["height"] = round(max(ys) - min(ys), 4)
        if name in materials.emissive:
            color, intensity = materials.emissive[name]
            entry["emission"] = f"#{color:06X}"
            entry["intensity"] = intensity
        pieces.append(entry)
    manifest = {
        "blender": bpy.app.version_string,
        "exported": datetime.date.today().isoformat(),
        "root": root.name,
        # buildEnv() es el del prototipo de BLADE: se reutiliza su panorama
        "environment": env_name,
        "pieces": pieces,
        "noShadow": no_shadow,
        "lowReflectionMaterials": ["M_PaintNose"],
        "lowReflectionFactor": REFLECTIONS["M_PaintNose"],
        "reflections": [{"material": k, "factor": v} for k, v in REFLECTIONS.items()],
        "additiveMaterials": [{"material": k, "opacity": v} for k, v in ADDITIVE_MATERIALS.items()],
        "transparentMaterials": [{"material": k, "opacity": v} for k, v in TRANSPARENT_MATERIALS.items()],
        "triangles": triangles,
        "bounds": {"min": [round(c, 4) for c in low], "max": [round(c, 4) for c in high]},
    }
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


# ---------- Capturas ----------

def render_previews(scene, collection, root, env_path, panels, out):
    scene.render.engine = "BLENDER_EEVEE"
    # El prototipo usa ACES: se aproxima con la vista que haya, solo para mirar
    views = {v.identifier for v in scene.view_settings.bl_rna.properties["view_transform"].enum_items}
    aces = next((n for n in ("ACES 2.0", "ACES 1.3", "ACES") if n in views), None)
    scene.view_settings.view_transform = aces or "Filmic"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = math.log2(1.05)

    def linear(hex_value):
        return io.hex_color(hex_value)[:3]

    io.preview_world(scene, env_path, linear(0x9A8F80), linear(0x15110C), 0.55, linear(0x2C241A), 0.5, (0.002, 0.003, 0.006, 1.0))
    key = io.preview_light_sun(collection, root, (0.7, 2.2, -1.6), (0, -0.3, -0.4), 2.2)
    key.data.color = linear(0xFFE2B8)
    io.preview_light_point("PreviewLamp", collection, root, (0, 0.32, -0.25), linear(0xFFB870), 0.9)
    # Luz de cada pantalla con el brillo de reposo del prototipo: 0,35 × 0,03 × área / 0,017
    sizes = {"Screen_Radar": (0.17, 0.13), "Screen_Damage": (0.12, 0.12), "Screen_Map": (0.17, 0.13), "Screen_Speed": (0.40, 0.075)}
    for name, grp in panels.items():
        w, h = sizes[name]
        io.preview_light_point(f"Preview{name}", collection, root, g.transform_point(grp.world, (0, 0, 0.06)),
                               linear(SCREEN_LIGHT), 0.35 * 0.03 * w * h / 0.017)

    folder = os.path.join(out, "previews")
    forward = (0.0, math.sin(EYE_PITCH), -math.cos(EYE_PITCH))
    io.render_preview(scene, collection, root, os.path.join(folder, "hammer_eye.png"), (0, 0, 0), forward, 70)
    io.render_preview(scene, collection, root, os.path.join(folder, "hammer_three_quarter.png"), (2.3, 0.15, 1.1), (0, -0.4, -1.3), 45)
    io.render_preview(scene, collection, root, os.path.join(folder, "hammer_overhead.png"), (0.10, 0.02, -0.12), (0, 0.205, -0.535), 50)
    return aces or "Filmic"


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    out = parse_out()
    os.makedirs(out, exist_ok=True)
    glb_path = os.path.join(ASSETS, "cockpit_hammer.glb")
    manifest_path = os.path.join(ASSETS, "cockpit_hammer.manifest.json")
    # Mismo buildEnv() que el prototipo de BLADE (mismo algoritmo y semilla 77): no se genera otro panorama
    env_name = "cockpit_blade_env.png"
    env_path = os.path.join(ASSETS, env_name)
    if not os.path.exists(env_path):
        raise RuntimeError(f"Falta el panorama de BLADE ({env_path}): ejecuta antes art/blender/run.ps1 blade")

    io.clear_file()
    scene = bpy.context.scene
    collection = scene.collection

    # Texturas sueltas aparte de las de VECTOR y BLADE: algunas se llaman igual
    materials = Materials(os.path.join(out, "textures", "hammer"))
    builder = BladeBuilder()
    root_group = builder.root
    build_nose(builder, materials, root_group)
    build_canard(builder, materials, root_group)
    build_dash(builder, materials, root_group)
    build_sides(builder, materials, root_group)
    build_frame(builder, materials, root_group)
    panels, overhead = build_colgante(builder, materials, root_group)

    tip, canard_x, canard_y, oh, header_y = check_axes(builder, overhead)

    # Raíz girada +90° en X: un punto del prototipo (x, y, z) queda en Blender en (x, −z, y) y vuelve a ser (x, y, z) en glTF
    root = io.empty("CockpitHammer", None, collection, rotation=(math.pi / 2, 0.0, 0.0))
    no_shadow = create_objects(builder, root, collection)
    bpy.context.view_layer.update()

    check_contract(root)
    write_manifest(manifest_path, root, materials, no_shadow, env_name)
    io.export_glb(glb_path, root)

    preview_collection = bpy.data.collections.new("Preview")
    scene.collection.children.link(preview_collection)
    view = render_previews(scene, preview_collection, root, env_path, panels, out)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "cockpit_hammer.blend"))

    print(f"[Cockpit] Blender {bpy.app.version_string} · capturas con {scene.render.engine} y vista {view} · panorama {env_name}")
    print(f"[Cockpit] Punta del morro z = {tip:.3f} · canard x = ±{canard_x:.3f} y = {canard_y:.3f} · "
          f"bloque colgante ({oh[0]:.3f}, {oh[1]:.3f}, {oh[2]:.3f}) · larguero y = {header_y:.3f}")
    print(f"[Cockpit] GLB {os.path.getsize(glb_path)} bytes · sha256 {sha256(glb_path)}")


main()
