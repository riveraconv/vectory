# Cabina 3D de BLADE, estilo MILITAR (spec docs/cabinas-3d-2-blade.md). Traducción de buildCockpit(4) de
# designs/cabina-3d-blade.html (disposición 4 · DEFINITIVA): mismos números y mismo orden.
#
# Uso: blender -b --factory-startup --python art/blender/cockpit_blade.py -- --out art/blender/out
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
from lib import textures_military as tx  # noqa: E402
from lib.blade_builder import BladeBuilder, transformed  # noqa: E402
from lib.textures import strip_png_text  # noqa: E402

REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "assets", "cockpits")

# Colores de luz del prototipo (LIGHT)
ACCENT = 0x7CFF5A
NAV_L = 0xFF2A1E
NAV_R = 0x3DFF6A
SENSOR = 0x7CFF5A

# Intensidad base de cada luz (spec, 6.6): el modelo se exporta encendido y Godot la cambia
ACCENT_SCREEN = 1.6
ACCENT_GLARE = 0.45
LED_VISOR = 0.35
LED_NOSE = 3.0
LED_SENSOR = 2.5

# Repetición de cada textura: se multiplica en las UV, así no hace falta KHR_texture_transform
REPEAT = {
    "M_Paint": 3.0, "M_PaintDark": 3.0, "M_PaintNose": 1.5,
    "M_Crinkle": 8.0, "M_AntiSlip": 10.0, "M_Gunmetal": 2.0, "M_Hazard": 12.0,
}

# Lo que glTF no lleva y aplica Godot (envMapIntensity, suma y transparencias del prototipo)
REFLECTIONS = {"M_PaintNose": 0.5, "M_Glass": 1.3, "M_CombinerGlass": 1.5}
ADDITIVE_MATERIALS = {"M_GlassSmudge": 0.05, "M_ScreenHUD": 1.0}
TRANSPARENT_MATERIALS = {"M_ScreenShade": 0.7, "M_Glass": 0.05, "M_CombinerGlass": 0.10, "M_GlassSmudge": 0.05}

ZONES = ["Nose", "Dash", "Side_L", "Side_R", "Frame", "Console"]

SCREENS = ["Screen_Speed", "Screen_Radar", "Screen_Map"]
LOOK = (0.0, 0.02, 0.12)

EYE_PITCH = -0.10  # la vista mira 5,7° hacia abajo; no va en el modelo, solo en las capturas


def contract_names():
    names = ["Screen_HUD"]
    names += SCREENS
    names += [f"{s}_Shade" for s in SCREENS]
    names += [f"Accent_{s[7:]}" for s in SCREENS]
    names += ["Accent_Glare"]
    names += [f"Led_Visor_{s[7:]}" for s in SCREENS]
    names += ["Led_Nose_L", "Led_Nose_R", "Led_Sensor", "Lamp_Caution", "Lamp_Warning", "Plate",
              "Combiner_Plate", "Glass_Canopy", "Glass_Smudge"]
    return names


# Piezas vivas con tamaño en el manifiesto: pantallas, pilotos y placa
SIZED = ["Screen_HUD"] + SCREENS + ["Lamp_Caution", "Lamp_Warning", "Plate"]


def parse_out():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--out" in args:
        path = args[args.index("--out") + 1]
        return path if os.path.isabs(path) else os.path.join(REPO, path)
    return os.path.join(HERE, "out")


# ---------- Materiales ----------

class Materials:
    def __init__(self, texture_folder):
        paint_maps = tx.paint_set(61, (72, 80, 72))
        paint_dark_maps = dict(paint_maps)
        paint_dark_maps["color"] = tx.tinted(paint_maps["color"], (0.55, 0.58, 0.56))
        paint = io.save_texture_set(texture_folder, "Paint", paint_maps)
        paint_dark = io.save_texture_set(texture_folder, "PaintDark", {"color": paint_dark_maps["color"]})
        paint_dark["roughness"] = paint["roughness"]
        paint_dark["normal"] = paint["normal"]
        nose = io.save_texture_set(texture_folder, "PaintNose", tx.paint_set(67, (92, 100, 94)))
        crinkle = io.save_texture_set(texture_folder, "Crinkle", tx.crinkle_set(62))
        anti_slip = io.save_texture_set(texture_folder, "AntiSlip", tx.anti_slip_set(63))
        gunmetal = io.save_texture_set(texture_folder, "Gunmetal", tx.gunmetal_set(64))
        hazard = io.save_texture_set(texture_folder, "Hazard", tx.hazard_set(65))
        smudge = io.save_texture_set(texture_folder, "GlassSmudge", {"color": tx.smudge_texture(66)})

        self.paint = io.material("M_Paint", metallic=0.15, roughness=1.0, textures=paint)
        self.paint_dark = io.material("M_PaintDark", metallic=0.15, roughness=1.0, textures=paint_dark)
        self.paint_nose = io.material("M_PaintNose", metallic=0.12, roughness=1.0, textures=nose)
        self.crinkle = io.material("M_Crinkle", metallic=0.1, roughness=1.0, textures=crinkle)
        self.anti_slip = io.material("M_AntiSlip", metallic=0.0, roughness=1.0, textures=anti_slip)
        self.gunmetal = io.material("M_Gunmetal", metallic=1.0, roughness=1.0, textures=gunmetal)
        self.hazard = io.material("M_Hazard", metallic=0.1, roughness=1.0, textures=hazard)
        self.rubber = io.material("M_Rubber", base=io.hex_color(0x0B0C0B), roughness=0.95)
        self.dark = io.material("M_Dark", base=io.hex_color(0x050605), roughness=0.9)
        self.mirror = io.material("M_Mirror", base=io.hex_color(0xAAB0B0), metallic=1.0, roughness=0.04)
        self.shade = io.material("M_ScreenShade", base=(0, 0, 0, 1), roughness=1.0, alpha=0.7)
        self.glass = io.material("M_Glass", base=(0, 0, 0, 1), roughness=0.04, alpha=0.05)
        self.combiner_glass = io.material("M_CombinerGlass", base=io.hex_color(0x0A2A10), roughness=0.03, alpha=0.10)
        self.smudge = io.material("M_GlassSmudge", roughness=1.0, textures=smudge, alpha=0.05)
        self.emissive = {}  # nombre de la pieza → (color, intensidad)

    # Cada luz viva con su propio material, para que Godot la encienda y la apague sola
    def glow(self, kind, piece, color, intensity):
        self.emissive[piece] = (color, intensity)
        return io.material(f"M_Emissive_{kind}_{piece}", base=(0, 0, 0, 1), roughness=0.4,
                           emission=io.hex_color(color), emission_strength=intensity)

    # Superficies que pinta Godot: pantallas, cristal HUD, pilotos MASTER y placa
    @staticmethod
    def screen(piece):
        return io.material(f"M_Screen_{piece[7:]}", base=(0, 0, 0, 1), roughness=1.0)

    @staticmethod
    def screen_hud():
        return io.material("M_ScreenHUD", base=(0, 0, 0, 1), roughness=1.0)

    @staticmethod
    def lamp(piece):
        return io.material(f"M_Lamp_{piece[5:]}", base=(0, 0, 0, 1), roughness=1.0)

    @staticmethod
    def plate():
        return io.material("M_Plate", base=(0, 0, 0, 1), metallic=0.3, roughness=0.6)


# ---------- Cabina: buildCockpit(4) del prototipo ----------

def cowl_y(x):
    return -0.168 - 0.030 * (x / 0.64) ** 2


def cowl_z(x):
    return -0.505 - 0.060 * (x / 0.64) ** 2


def mirror_x(points):
    return [(-p[0], p[1], p[2]) for p in points]


def _first_below(st, z):
    return next(i for i, s in enumerate(st) if s[0] < z)


def _at(st, z):
    j = _first_below(st, z)
    a, b = st[j - 1], st[j]
    t = (z - a[0]) / (b[0] - a[0])
    return a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t, j


# Línea de luz bajo una pantalla
def accent(b, m, parent, name, w, pos, base):
    return b.mesh(b.rbox(w, 0.003, 0.003, 0.001), m.glow("Accent", name, ACCENT, base), parent, name, True, pos)


def build_nose(b, m, root):
    nose = b.group(root, "Nose")
    st = [  # z, semiancho, alto de la cubierta
        (-0.80, 0.62, -0.225), (-1.30, 0.55, -0.245), (-2.00, 0.44, -0.270), (-2.80, 0.32, -0.295),
        (-3.60, 0.20, -0.320), (-4.30, 0.09, -0.340), (-4.75, 0.03, -0.350)]

    def sec(s):
        z, w, y = s
        return [(-w, y - 0.10, z), (-w * 0.92, y - 0.016, z), (-w * 0.55, y, z),
                (w * 0.55, y, z), (w * 0.92, y - 0.016, z), (w, y - 0.10, z)]

    sections = [sec(s) for s in st]
    # Cinco caras a lo largo: costado, chaflán, cubierta, chaflán, costado
    for k in range(5):
        a = [s[k] for s in sections]
        c = [s[k + 1] for s in sections]
        b.mesh(b.strip(a, c), m.paint_nose, nose, f"Nose_Skin_{k}")
    b.mesh(b.poly(sections[-1]), m.paint_nose, nose, "Nose_Tip")
    # Panel antirreflejos (mate) en la cubierta, delante del cristal
    ag = [(w * 0.22, y + 0.002, z) for z, w, y in st[:4]]
    b.mesh(b.strip(mirror_x(ag), ag), m.anti_slip, nose, "Nose_AntiGlare")
    # Juntas transversales y longitudinales
    for i, z in enumerate((-1.6, -2.4, -3.2, -4.0)):
        w, y, _ = _at(st, z)
        b.mesh(b.poly([(-w * 0.55, y + 0.0015, z - 0.004), (w * 0.55, y + 0.0015, z - 0.004),
                       (w * 0.55, y + 0.0015, z + 0.004), (-w * 0.55, y + 0.0015, z + 0.004)]),
               m.dark, nose, f"Nose_Seam_{i}", True)
    for sx in (-1, 1):
        b.tube([(sx * w * 0.55, y + 0.002, z) for z, w, y in st[:6]], 0.002, 24, m.dark, nose, "Nose_SeamL", True)
    # Remaches a lo largo de la junta del chaflán
    for i in range(14):
        z = -0.9 - i * 0.22
        w, y, j = _at(st, z)
        if j < 1:
            continue
        for sx in (-1, 1):
            b.sphere(0.0045, m.gunmetal, nose, "Rivet", True, (sx * w * 0.70, y - 0.004, z), (1.0, 0.4, 1.0))
    # Luces de posición: roja a la izquierda, verde a la derecha
    b.sphere(0.012, m.glow("NavL", "Led_Nose_L", NAV_L, LED_NOSE), nose, "Led_Nose_L", True, (-0.44 * 0.96, -0.27 - 0.05, -2.0))
    b.sphere(0.012, m.glow("NavR", "Led_Nose_R", NAV_R, LED_NOSE), nose, "Led_Nose_R", True, (0.44 * 0.96, -0.27 - 0.05, -2.0))
    # Sonda (pitot) en la punta y antena de cuchilla en la cubierta
    b.tube([(0, -0.352, -4.74), (0, -0.352, -5.05), (0, -0.352, -5.45)], 0.008, 4, m.gunmetal, nose, "Nose_Pitot")
    b.cyl(0, 0.008, 0.06, 10, m.gunmetal, nose, "Nose_PitotTip", False, (0, -0.352, -5.48), (-math.pi / 2, 0, 0))
    b.beam((0, -0.262, -1.70), (0, -0.215, -1.86), 0.004, 0.05, (0, 1, -1.78), m.paint_dark, nose, "Nose_Antenna")


def build_dash(b, m, root):
    dash = b.group(root, "Dash")
    # Frontal del salpicadero, inclinado hacia el piloto
    face = b.group(dash, "Dash_Face", (0, -0.32, -0.545), None, LOOK)
    b.mesh(b.rbox(1.30, 0.34, 0.05, 0.01), m.crinkle, face, "Dash_FacePanel")
    # Cubre-instrumentos: cubierta antideslizante, labio con canto y sombra debajo
    xs = [-0.64 + 1.28 * i / 16 for i in range(17)]
    rear = [(x, cowl_y(x), cowl_z(x)) for x in xs]
    front = [(x, -0.215 - 0.020 * (x / 0.64) ** 2, -0.80) for x in xs]
    b.mesh(b.strip(rear, front), m.anti_slip, dash, "Glare_Top")
    lip = [(p[0], p[1] - 0.026, p[2] + 0.004) for p in rear]
    b.mesh(b.strip(rear, lip), m.paint, dash, "Glare_Lip")
    under = [(p[0], p[1] + 0.004, p[2] - 0.09) for p in lip]
    b.mesh(b.strip(lip, under), m.dark, dash, "Glare_Under", True)
    b.tube([(p[0], p[1] + 0.001, p[2] + 0.002) for p in rear], 0.006, 32, m.rubber, dash, "Glare_Edge")
    # Luz de cortesía bajo el labio (ilumina el panel)
    accent(b, m, dash, "Accent_Glare", 0.9, (0, -0.186, -0.535), ACCENT_GLARE)
    return dash, face


def build_sides(b, m, root):
    sides = {}
    # Mejillas laterales y alféizares del cristal
    for sx in (-1, 1):
        side = b.group(root, "Side_L" if sx < 0 else "Side_R")
        sides[sx] = side
        b.mesh(b.rbox(0.03, 0.26, 0.48, 0.006), m.paint, side, "Cheek", pos=(sx * 0.655, -0.375, -0.62))
        b.mesh(b.poly([(sx * 0.64, cowl_y(0.64 * sx), cowl_z(0.64)), (sx * 0.64, -0.235, -0.80), (sx * 0.665, -0.25, -0.86),
                       (sx * 0.665, -0.25, -0.40), (sx * 0.64, cowl_y(0.64) - 0.03, cowl_z(0.64) + 0.06)]),
               m.paint, side, "Cheek_Top")
        b.mesh(b.rbox(0.075, 0.05, 1.25, 0.008), m.paint, side, "Sill", pos=(sx * 0.645, -0.24, -0.28))
        b.tube([(sx * 0.62, -0.212, -0.90), (sx * 0.62, -0.212, -0.30), (sx * 0.62, -0.212, 0.30)], 0.007, 8,
               m.gunmetal, side, "Canopy_Rail")
        # El bucle del prototipo suma 0,05 en coma flotante: se repite igual para tener los mismos remaches
        z = -0.86
        while z < -0.30:
            b.sphere(0.004, m.gunmetal, side, "Rivet", True, (sx * 0.672, -0.215, z), (1.0, 0.45, 1.0))
            z += 0.05
    # Tirador de suelta del cristal (franjas) en el alféizar izquierdo
    left = sides[-1]
    b.beam((-0.60, -0.200, -0.70), (-0.60, -0.200, -0.60), 0.014, 0.014, (-0.60, 1, -0.65), m.hazard, left, "Canopy_Handle")
    b.beam((-0.60, -0.214, -0.66), (-0.60, -0.200, -0.66), 0.008, 0.008, (-1, -0.2, -0.66), m.gunmetal, left, "Canopy_HandleStem")


def build_frame(b, m, root):
    frame = b.group(root, "Frame")
    bow_r = [(0.62, -0.20, -0.86), (0.60, 0.05, -0.80), (0.48, 0.27, -0.72), (0.26, 0.40, -0.65), (0, 0.44, -0.62)]
    bow = mirror_x(bow_r) + list(reversed(bow_r[:4]))
    b.tube(bow, 0.016, 64, m.paint_dark, frame, "Canopy_Bow")
    b.tube([(p[0] * 0.985, p[1] - 0.004, p[2] + 0.018) for p in bow], 0.006, 64, m.rubber, frame, "Canopy_Seal")
    # Sensor central con su piloto
    b.mesh(b.rbox(0.07, 0.026, 0.045, 0.006), m.paint_dark, frame, "Frame_Sensor", pos=(0, 0.412, -0.60))
    b.sphere(0.004, m.glow("Sensor", "Led_Sensor", SENSOR, LED_SENSOR), frame, "Led_Sensor", True, (0, 0.398, -0.577))
    # Retrovisores en el arco
    for sx in (-1, 1):
        mg = b.group(frame, "Mirror_L" if sx < 0 else "Mirror_R", (sx * 0.30, 0.372, -0.635), (0.35, -sx * 0.25, 0))
        b.mesh(b.rbox(0.10, 0.032, 0.014, 0.005), m.paint_dark, mg, "Mirror_Body")
        b.mesh(g.plane(0.09, 0.024), m.mirror, mg, "Mirror_Glass", True, (0, 0, 0.0075))
        b.beam((0, 0.016, -0.004), (0, 0.034, -0.010), 0.01, 0.01, (0, 0.025, 1), m.gunmetal, mg, "Mirror_Arm")
    # Cristal: cúpula casi invisible con reflejos y manchas
    dome = g.sphere_section(0.95, 48, 24, math.pi * 1.12, math.pi * 0.76, math.pi * 0.12, math.pi * 0.42)
    b.mesh(dome, m.glass, frame, "Glass_Canopy", True, (0, -0.22, -0.05))
    b.mesh(g.plane(1.5, 0.75), m.smudge, frame, "Glass_Smudge", True, (0, 0.10, -0.78))


# Piloto con rótulo (MASTER CAUTION / MASTER WARN). El texto lo pone Godot
def make_lamp(b, m, parent, name, pos, look):
    housing = b.group(parent, f"{name}_Housing", pos, None, look)
    b.mesh(b.rbox(0.058, 0.026, 0.022, 0.003), m.crinkle, housing, f"{name}_Box", pos=(0, 0, -0.011))
    b.mesh(g.plane(0.048, 0.019), m.lamp(name), housing, name, True, (0, 0, 0.0012))


# Cristal HUD (combiner) con velocidad y boost
def build_combiner(b, m, con):
    cb = b.group(con, "Combiner")
    b.mesh(b.rbox(0.20, 0.03, 0.07, 0.006), m.crinkle, cb, "Combiner_Base", pos=(0, -0.163, -0.575))
    for i in range(6):
        b.mesh(b.rbox(0.018, 0.009, 0.006, 0.002), m.rubber, cb, "UFC_Key", pos=(-0.075 + i * 0.03, -0.163, -0.5385))
    for sx in (-1, 1):
        b.beam((sx * 0.112, -0.150, -0.585), (sx * 0.112, -0.018, -0.565), 0.008, 0.008, (0, -0.08, 1), m.gunmetal, cb, "Combiner_Post")
    gl = b.group(cb, "Combiner_Glass", (0, -0.086, -0.575), (0.15, 0, 0))
    b.mesh(g.plane(0.22, 0.13), m.combiner_glass, gl, "Combiner_Plate", True)
    b.mesh(g.plane(0.22, 0.13), m.screen_hud(), gl, "Screen_HUD", True, (0, 0, 0.0015))
    b.tube([(-0.112, -0.065, 0), (-0.112, 0.065, 0), (0.112, 0.065, 0), (0.112, -0.065, 0)], 0.003, 12, m.gunmetal, gl, "Combiner_Frame")


# MFD militar en caja que sobresale del frontal (makeMFD con pod y visor): carcasa de pintura rugosa, bisel,
# botones OSB, balancines, pestaña de anclaje con nervios, visera con luz, pantalla, sombra y línea de luz
def make_mfd(b, m, parent, name, pos, look, w, h, visor, depth):
    grp = b.group(parent, f"{name}_Panel", pos, None, look)
    dep, bz = depth, 0.022
    hw, hh = w + bz * 2, h + bz * 2
    b.mesh(b.rbox(hw, hh, dep, 0.006), m.crinkle, grp, f"{name}_Housing", pos=(0, 0, -dep / 2 - 0.001))
    # Bisel interior
    for x, y, bw, bh in ((0, h / 2 + 0.003, w + 0.006, 0.006), (0, -h / 2 - 0.003, w + 0.006, 0.006),
                         (-w / 2 - 0.003, 0, 0.006, h + 0.012), (w / 2 + 0.003, 0, 0.006, h + 0.012)):
        b.mesh(b.rbox(bw, bh, 0.006, 0.0015), m.dark, grp, f"{name}_Bezel", pos=(x, y, 0.005))
    # Botones OSB
    n_h = max(3, tx._js_round(w / 0.026))
    n_v = max(3, tx._js_round(h / 0.026))
    for i in range(n_h):
        x = -w / 2 + w * (i + 0.5) / n_h
        for y in (h / 2 + bz * 0.55, -h / 2 - bz * 0.55):
            b.mesh(b.rbox(0.011, 0.007, 0.007, 0.002), m.rubber, grp, "OSB", pos=(x, y, 0.006))
    for i in range(n_v):
        y = -h / 2 + h * (i + 0.5) / n_v
        for x in (-w / 2 - bz * 0.55, w / 2 + bz * 0.55):
            b.mesh(b.rbox(0.007, 0.011, 0.007, 0.002), m.rubber, grp, "OSB", pos=(x, y, 0.006))
    # Balancines de brillo y contraste en las esquinas
    for sx, sy in ((-1, 1), (1, 1), (-1, -1), (1, -1)):
        b.mesh(b.rbox(0.009, 0.009, 0.006, 0.002), m.gunmetal, grp, "Rocker",
               pos=(sx * (w / 2 + bz * 0.6), sy * (h / 2 + bz * 0.6), 0.006))
    # Pestaña de anclaje atornillada al fondo y nervios laterales
    fz = -dep + 0.010
    b.mesh(b.rbox(hw + 0.026, hh + 0.022, 0.008, 0.003), m.paint, grp, f"{name}_Flange", pos=(0, 0, fz))
    for sx, sy in ((-1, 1), (1, 1), (-1, -1), (1, -1)):
        b.screw(m.gunmetal, grp, sx * (hw / 2 + 0.008), sy * (hh / 2 + 0.006), fz + 0.0045, 0.003)
    for sx in (-1, 1):
        for k in (0.3, -0.3):
            b.mesh(b.rbox(0.004, 0.010, dep - 0.02, 0.0015), m.crinkle, grp, f"{name}_Rib",
                   pos=(sx * (hw / 2 + 0.002), k * hh, -dep / 2 - 0.004))
    b.mesh(b.rbox(hw - 0.02, 0.003, dep - 0.03, 0.001), m.dark, grp, f"{name}_Seam", pos=(0, -hh / 2 - 0.0005, -dep / 2 - 0.002))
    b.screw(m.gunmetal, grp, -hw / 2 + 0.006, hh / 2 - 0.006, 0.003)
    b.screw(m.gunmetal, grp, hw / 2 - 0.006, hh / 2 - 0.006, 0.003)
    b.screw(m.gunmetal, grp, -hw / 2 + 0.006, -hh / 2 + 0.006, 0.003)
    b.screw(m.gunmetal, grp, hw / 2 - 0.006, -hh / 2 + 0.006, 0.003)
    # Visera: chapa inclinada sobre la pantalla, laterales que la cierran y luz tenue por debajo
    vd = visor
    vt = hh / 2 + 0.004
    a = 0.32
    fy = vt - vd * math.sin(a)
    vz = vd * math.cos(a)
    vg = b.group(grp, f"{name}_Visor")
    b.mesh(b.rbox(hw + 0.014, 0.006, vd, 0.002), m.crinkle, vg, f"{name}_VisorTop",
           pos=(0, (vt + fy) / 2 + 0.003, vz / 2 - 0.004), rot=(a, 0, 0))
    b.mesh(b.rbox(hw + 0.018, 0.009, 0.010, 0.003), m.rubber, vg, f"{name}_VisorLip", pos=(0, fy + 0.002, vz - 0.004))
    for sx in (-1, 1):
        x = sx * (hw / 2 + 0.007)
        b.mesh(b.poly([(x, vt + 0.006, -0.006), (x, fy + 0.006, vz), (x, fy - 0.014, vz - 0.006), (x, vt - 0.045, -0.002)]),
               m.crinkle, vg, f"{name}_VisorSide")
    led = f"Led_Visor_{name[7:]}"
    b.mesh(b.rbox(hw * 0.7, 0.002, 0.002, 0.0008), m.glow("Accent", led, ACCENT, LED_VISOR), vg, led, True,
           (0, fy - 0.004, vz - 0.010))
    b.screw(m.gunmetal, vg, -hw / 2 + 0.004, fy + 0.0, vz - 0.012, 0.0028)
    b.screw(m.gunmetal, vg, hw / 2 - 0.004, fy + 0.0, vz - 0.012, 0.0028)
    # Pantalla, sombra y línea de luz
    b.mesh(g.plane(w, h), m.screen(name), grp, name, True, (0, 0, 0.0015))
    b.mesh(g.plane(w, 0.012), m.shade, grp, f"{name}_Shade", True, (0, h / 2 - 0.004, 0.006), (0.9, 0, 0))
    accent(b, m, grp, f"Accent_{name[7:]}", w * 0.8, (0, -hh / 2 - 0.004, 0.004), ACCENT_SCREEN)
    return grp


def build_console(b, m, root, dash):
    # Pilotos MASTER CAUTION / MASTER WARN sobre el cubre-instrumentos
    make_lamp(b, m, dash, "Lamp_Caution", (-0.215, cowl_y(0.215) + 0.012, cowl_z(0.215) - 0.012), (-0.215, 0.1, 0.3))
    make_lamp(b, m, dash, "Lamp_Warning", (0.215, cowl_y(0.215) + 0.012, cowl_z(0.215) - 0.012), (0.215, 0.1, 0.3))
    # Disposición 4 · DEFINITIVA: cristal HUD y pantallas en cajas que sobresalen 3,5 cm hacia el piloto, con visera
    con = b.group(root, "Console")
    build_combiner(b, m, con)
    panels = {}
    panels["Screen_Radar"] = make_mfd(b, m, con, "Screen_Radar", (0, -0.279, -0.455), LOOK, 0.17, 0.14, 0.040, 0.10)
    panels["Screen_Speed"] = make_mfd(b, m, con, "Screen_Speed", (-0.232, -0.279, -0.463), LOOK, 0.105, 0.14, 0.040, 0.10)
    panels["Screen_Map"] = make_mfd(b, m, con, "Screen_Map", (0.232, -0.279, -0.463), LOOK, 0.105, 0.14, 0.040, 0.10)
    # Placa con el nombre en el frontal
    plate = b.group(dash, "Dash_Plate", (0, -0.355, -0.465), None, LOOK)
    b.mesh(g.plane(0.10, 0.0125), m.plate(), plate, "Plate", True)
    return panels


# ---------- Añadido sobre el prototipo: vida en el frontal (spec, 3.4) ----------
# Todo en coordenadas del frontal (Dash_Face): X a la derecha, Y hacia arriba por la cara inclinada y Z hacia el piloto.
# La cara delantera del frontal está en z = 0,025. Solo decoración: nada vivo, nada nuevo en el contrato con Godot.

FACE_FRONT = 0.025


# Piezas pequeñas del añadido con menos triángulos que las del prototipo: caja redondeada de 1 segmento y esfera de
# 6 × 4. Son menudas y no se nota, y sin esto el frontal costaba ~30.000 triángulos.
def small_box(w, h, d, r):
    return g.rounded_box(w, h, d, min(r, w / 2.01, h / 2.01, d / 2.01), 1)


def small_rivet(b, m, parent, pos):
    return b.mesh(g.box_uv(g.sphere(0.0035, 6, 4)), m.gunmetal, parent, "Rivet", True, pos, None, (1.0, 1.0, 0.4))


# Interruptor de palanca. up = 1 hacia arriba, −1 hacia abajo
def toggle_switch(b, m, panel, x, y, z, up):
    b.cyl(0.0055, 0.0055, 0.004, 12, m.gunmetal, panel, "Toggle_Nut", False, (x, y, z + 0.002), (math.pi / 2, 0, 0))
    tilt = math.pi / 2 - 0.4 * up
    axis = (0.0, math.cos(tilt), math.sin(tilt))
    length = 0.02
    mid = (x, y + axis[1] * (0.004 + length / 2), z + axis[2] * (0.004 + length / 2))
    b.cyl(0.0018, 0.0026, length, 10, m.gunmetal, panel, "Toggle_Lever", False, mid, (tilt, 0, 0))
    tip = (x, y + axis[1] * (0.004 + length), z + axis[2] * (0.004 + length))
    b.mesh(g.box_uv(g.sphere(0.0032, 8, 6)), m.gunmetal, panel, "Toggle_Tip", False, tip)


# Selector giratorio: aro oscuro con marcas, mando de goma y aguja de pavonado
def rotary_selector(b, m, panel, x, y, z, radius, pointer_angle):
    b.cyl(radius * 1.45, radius * 1.45, 0.003, 32, m.dark, panel, "Selector_Ring", False, (x, y, z + 0.0015), (math.pi / 2, 0, 0))
    for i in range(5):
        a = math.radians(-60 + 30 * i)
        r = radius * 1.25
        b.mesh(b.poly([(-0.00075, -0.00225, 0), (0.00075, -0.00225, 0), (0.00075, 0.00225, 0), (-0.00075, 0.00225, 0)]),
               m.paint, panel, "Selector_Tick", True, (x + math.sin(a) * r, y + math.cos(a) * r, z + 0.0031), (0, 0, -a))
    b.cyl(radius, radius * 1.08, 0.012, 24, m.rubber, panel, "Selector_Knob", False, (x, y, z + 0.009), (math.pi / 2, 0, 0))
    b.mesh(small_box(0.003, radius * 1.6, 0.003, 0.001), m.gunmetal, panel, "Selector_Pointer",
           pos=(x, y, z + 0.0155), rot=(0, 0, -pointer_angle))


# Interruptor con tapa de seguridad a franjas (tipo MASTER ARM), cerrada
def guarded_switch(b, m, panel, x, y, z):
    b.mesh(b.rbox(0.036, 0.046, 0.004, 0.0015), m.dark, panel, "Arm_Base", pos=(x, y, z + 0.002))
    b.mesh(b.rbox(0.030, 0.038, 0.011, 0.003), m.hazard, panel, "Arm_Cover", pos=(x, y - 0.002, z + 0.0095))
    b.cyl(0.003, 0.003, 0.032, 10, m.gunmetal, panel, "Arm_Hinge", False, (x, y + 0.019, z + 0.007), (0, 0, math.pi / 2))


# Placa de interruptores atornillada a un lado de las cajas: 2 × 3 palancas con guardas, un selector y, en la
# izquierda, el interruptor con tapa; en la derecha, un segundo selector
def switch_panel(b, m, face, sx):
    panel = b.group(face, "Switch_Panel_L" if sx < 0 else "Switch_Panel_R", (sx * 0.47, -0.005, FACE_FRONT))
    w, h, t = 0.24, 0.17, 0.006
    b.mesh(b.rbox(w, h, t, 0.003), m.paint, panel, "Switch_Plate", pos=(0, 0, t / 2))
    top = t
    for cx, cy in ((-1, 1), (1, 1), (-1, -1), (1, -1)):
        b.screw(m.gunmetal, panel, cx * (w / 2 - 0.009), cy * (h / 2 - 0.009), top + 0.0015)
    # Palancas en el lado de las cajas; selector y tapa hacia fuera
    pattern = ((1, 1, -1), (1, -1, 1))
    columns = (-0.085, -0.050, -0.015)
    for row, y in enumerate((0.035, -0.035)):
        for col, x in enumerate(columns):
            toggle_switch(b, m, panel, sx * x, y, top, pattern[row][col])
        # Guardas entre columnas, compartidas por las palancas vecinas
        for x in (columns[0] - 0.0175, columns[0] + 0.0175, columns[1] + 0.0175, columns[2] + 0.0175):
            b.mesh(small_box(0.0025, 0.028, 0.013, 0.001), m.paint_dark, panel, "Toggle_Guard", pos=(sx * x, y, top + 0.0065))
    rotary_selector(b, m, panel, sx * 0.062, 0.038, top, 0.011, sx * 0.5)
    if sx < 0:
        guarded_switch(b, m, panel, sx * 0.062, -0.040, top)
    else:
        rotary_selector(b, m, panel, sx * 0.062, -0.040, top, 0.008, -0.5)


# Juntas que dividen el frontal en paneles y remaches a lo largo
def face_seams(b, m, face):
    z = FACE_FRONT + 0.0006
    y0, y1 = -0.17, 0.12
    for sx in (-1, 1):
        for x in (0.132, 0.335, 0.628):
            b.mesh(b.poly([(sx * x - 0.002, y0, z), (sx * x + 0.002, y0, z), (sx * x + 0.002, y1, z), (sx * x - 0.002, y1, z)]),
                   m.dark, face, "Face_Seam", True)
        # Junta horizontal por encima de la placa de interruptores
        xa, xb = 0.335, 0.628
        b.mesh(b.poly([(sx * xa, 0.098, z), (sx * xb, 0.098, z), (sx * xb, 0.102, z), (sx * xa, 0.102, z)]),
               m.dark, face, "Face_Seam", True)
        # Remaches junto a las juntas verticales de fuera y a lo largo de la horizontal
        for x in (0.335 - 0.008, 0.628 - 0.008):
            y = -0.14
            while y < 0.11:
                small_rivet(b, m, face, (sx * x, y, FACE_FRONT))
                y += 0.035
        x = 0.36
        while x < 0.61:
            small_rivet(b, m, face, (sx * x, 0.108, FACE_FRONT))
            x += 0.035


def build_dash_details(b, m, face):
    face_seams(b, m, face)
    for sx in (-1, 1):
        switch_panel(b, m, face, sx)


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
def check_axes(builder):
    def points(name):
        return [g.transform_point(p.matrix, v) for p in builder.pieces if p.name == name for v in p.soup.positions]

    tip = min(p[2] for p in points("Nose_Tip"))
    pitot = [p[2] for p in points("Nose_PitotTip")]
    pitot_mid = (min(pitot) + max(pitot)) / 2
    bow = max(p[1] for p in points("Canopy_Bow"))
    if abs(tip + 4.75) > 0.01 or abs(pitot_mid + 5.48) > 0.01 or abs(bow - 0.456) > 0.005:
        raise RuntimeError(f"Ejes mal: punta del morro z = {tip:.3f}, sonda z = {pitot_mid:.3f}, arco y = {bow:.3f}")
    return tip, pitot_mid, bow


def write_manifest(path, root, materials, no_shadow):
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
        "pieces": pieces,
        "noShadow": no_shadow,
        # Mismo formato que el de VECTOR, más los reflejos y las transparencias que esta cabina necesita
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


# ---------- Panorama de reflejos: buildEnv() del prototipo ----------

def _smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def _lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def _emission_material(name, color=None, attribute=None):
    mat = bpy.data.materials.new(name)
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    em = nodes.new("ShaderNodeEmission")
    if attribute is not None:
        attr = nodes.new("ShaderNodeAttribute")
        attr.attribute_name = attribute
        mat.node_tree.links.new(attr.outputs["Color"], em.inputs["Color"])
    else:
        em.inputs["Color"].default_value = (color[0], color[1], color[2], 1.0)
    mat.node_tree.links.new(em.outputs[0], out.inputs[0])
    return mat


# Esfera de 50 m con el degradado de la noche (naranja en el horizonte), 40 luces naranjas de ciudad a 40 m y
# 10 azules arriba, con semilla 77. Cycles, porque Eevee no tiene cámara panorámica. Misma cámara que la de VECTOR:
# el centro de la imagen mira hacia −Z del prototipo.
def render_environment(path):
    scene = bpy.data.scenes.new("Env")
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 16
    scene.cycles.seed = 0
    scene.cycles.use_denoising = False
    scene.cycles.max_bounces = 0
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0

    world = bpy.data.worlds.new("EnvWorld")
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0, 0, 0, 1)
    scene.world = world

    collection = scene.collection
    root = io.empty("EnvRoot", None, collection, rotation=(math.pi / 2, 0.0, 0.0))

    # Cielo: SphereGeometry(50, 64, 32) con un color por vértice según su altura
    sky = g.sphere(50.0, 64, 32)
    verts, faces, colors, index = [], [], [], {}
    for i in range(0, len(sky.positions), 3):
        face = []
        for p in sky.positions[i:i + 3]:
            key = (round(p[0], 6), round(p[1], 6), round(p[2], 6))
            if key not in index:
                index[key] = len(verts)
                verts.append(p)
                h = p[1] / 50
                if h >= 0:
                    c = _lerp((0.34, 0.13, 0.04), (0.01, 0.014, 0.04), _smoothstep(0, 0.45, h))
                else:
                    c = _lerp((0.22, 0.09, 0.03), (0.008, 0.009, 0.01), _smoothstep(0, -0.3, h))
                colors.append(c)
            face.append(index[key])
        if len(set(face)) == 3:
            faces.append(face)
    mesh = bpy.data.meshes.new("EnvSky")
    mesh.from_pydata(verts, [], faces)
    attr = mesh.color_attributes.new("SkyColor", "FLOAT_COLOR", "POINT")
    for i, c in enumerate(colors):
        attr.data[i].color = (c[0], c[1], c[2], 1.0)
    mesh.materials.append(_emission_material("EnvSkyMat", attribute="SkyColor"))
    obj = bpy.data.objects.new("EnvSky", mesh)
    collection.objects.link(obj)
    obj.parent = root

    r = tx.Rng(77)
    city = _emission_material("EnvCity", (1.6, 0.7, 0.2))
    for i in range(40):
        a = r.next() * math.pi * 2
        w = 1 + r.next() * 4
        h = 0.3 + r.next() * 1.2
        pos = (math.cos(a) * 40, 0.5 + r.next() * 2, math.sin(a) * 40)
        io.mesh_object(f"EnvCity{i}", g.plane(w, h), city, root, collection, g.look_at(pos, (0.0, pos[1], 0.0)))
    blue = _emission_material("EnvBlue", (0.5, 0.8, 2.2))
    for i in range(10):
        a = r.next() * math.pi * 2
        pos = (math.cos(a) * 30, 22 + r.next() * 12, math.sin(a) * 30)
        io.mesh_object(f"EnvBlue{i}", g.plane(6.0, 1.2), blue, root, collection, g.look_at(pos, (0.0, 0.0, 0.0)))

    cam = bpy.data.cameras.new("EnvCamera")
    cam.type = "PANO"
    cam.panorama_type = "EQUIRECTANGULAR"
    cam.clip_start = 0.01
    cam.clip_end = 100.0
    cam_obj = bpy.data.objects.new("EnvCamera", cam)
    collection.objects.link(cam_obj)
    cam_obj.parent = root
    scene.camera = cam_obj

    scene.render.resolution_x = 1024
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.render.dither_intensity = 0.0
    for prop in scene.render.bl_rna.properties:
        if prop.identifier.startswith("use_stamp"):
            setattr(scene.render, prop.identifier, False)
    scene.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True, scene=scene.name)
    strip_png_text(path)
    bpy.data.scenes.remove(scene)


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

    io.preview_world(scene, env_path, linear(0xB0B8C8), linear(0x101010), 0.6, linear(0x1C221C), 0.45, (0.002, 0.003, 0.006, 1.0))
    io.preview_light_sun(collection, root, (0.7, 2.2, -1.6), (0, -0.3, -0.4), 2.6)
    io.preview_light_point("PreviewLamp", collection, root, (0, 0.32, -0.25), (1, 1, 1), 0.08)
    # Luz de cada pantalla con el brillo de reposo del prototipo: 0,35 × 0,03 × área / 0,017
    screen_light = linear(0xCFFFC0)
    sizes = {"Screen_Radar": (0.17, 0.14), "Screen_Speed": (0.105, 0.14), "Screen_Map": (0.105, 0.14)}
    for name, grp in panels.items():
        w, h = sizes[name]
        io.preview_light_point(f"Preview{name}", collection, root, g.transform_point(grp.world, (0, 0, 0.06)),
                               screen_light, 0.35 * 0.03 * w * h / 0.017)

    folder = os.path.join(out, "previews")
    forward = (0.0, math.sin(EYE_PITCH), -math.cos(EYE_PITCH))
    io.render_preview(scene, collection, root, os.path.join(folder, "blade_eye.png"), (0, 0, 0), forward, 70)
    io.render_preview(scene, collection, root, os.path.join(folder, "blade_three_quarter.png"), (2.2, 1.5, 1.6), (0, -0.3, -1.7), 45)
    io.render_preview(scene, collection, root, os.path.join(folder, "blade_screens.png"), (0, -0.06, -0.12), (0, -0.23, -0.5), 55)
    return aces or "Filmic"


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    out = parse_out()
    os.makedirs(out, exist_ok=True)
    glb_path = os.path.join(ASSETS, "cockpit_blade.glb")
    manifest_path = os.path.join(ASSETS, "cockpit_blade.manifest.json")
    env_path = os.path.join(ASSETS, "cockpit_blade_env.png")

    io.clear_file()
    scene = bpy.context.scene
    collection = scene.collection

    render_environment(env_path)

    # Texturas sueltas aparte de las de VECTOR: algunas se llaman igual
    materials = Materials(os.path.join(out, "textures", "blade"))
    builder = BladeBuilder()
    root_group = builder.root
    build_nose(builder, materials, root_group)
    dash, face = build_dash(builder, materials, root_group)
    build_sides(builder, materials, root_group)
    build_frame(builder, materials, root_group)
    panels = build_console(builder, materials, root_group, dash)
    # Añadido sobre el prototipo, al final para no cambiar el orden de lo que viene de él
    build_dash_details(builder, materials, face)

    tip, pitot, bow = check_axes(builder)

    # Raíz girada +90° en X: un punto del prototipo (x, y, z) queda en Blender en (x, −z, y) y vuelve a ser (x, y, z) en glTF
    root = io.empty("CockpitBlade", None, collection, rotation=(math.pi / 2, 0.0, 0.0))
    no_shadow = create_objects(builder, root, collection)
    bpy.context.view_layer.update()

    check_contract(root)
    write_manifest(manifest_path, root, materials, no_shadow)
    io.export_glb(glb_path, root)

    # Solo en las capturas: el cristal HUD es negro y opaco hasta que Godot lo pone en modo suma
    bpy.data.objects["Screen_HUD"].hide_render = True

    preview_collection = bpy.data.collections.new("Preview")
    scene.collection.children.link(preview_collection)
    view = render_previews(scene, preview_collection, root, env_path, panels, out)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "cockpit_blade.blend"))

    print(f"[Cockpit] Blender {bpy.app.version_string} · capturas con {scene.render.engine} y vista {view}")
    print(f"[Cockpit] Punta del morro z = {tip:.3f} · sonda z = {pitot:.3f} · arco y = {bow:.3f}")
    print(f"[Cockpit] GLB {os.path.getsize(glb_path)} bytes · sha256 {sha256(glb_path)}")


main()
