# Casco 3D de BLADE, estilo MILITAR, variante ASALTO (spec docs/cascos-3d-2-blade.md). Traducción de buildHull()
# con VAR = 3 y LIV = true de designs/casco-3d-blade.html, con sus marcas (la geometría; los dibujos los pone Godot):
# mismos números y mismo orden.
#
# Uso: blender -b --factory-startup --python art/blender/hull_blade.py -- --out art/blender/out
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
import numpy as np  # noqa: E402
from mathutils import Matrix  # noqa: E402

from lib import blender_io as io  # noqa: E402
from lib import geometry as g  # noqa: E402
from lib import hull_blade_geometry as bg  # noqa: E402
from lib import hull_geometry as hg  # noqa: E402
from lib import textures_hull as tx  # noqa: E402
from lib import textures_hull_blade as tb  # noqa: E402
from lib.builder import Builder  # noqa: E402

REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "assets", "ships")
COCKPIT_ASSETS = os.path.join(REPO, "assets", "cockpits")

# Pintura por defecto: BOSQUE (tres tonos: base, claro, oscuro), la que sale al abrir el prototipo
WOODLAND = ((86, 96, 64), (118, 122, 96), (44, 51, 38))

# Colores del prototipo (COL)
NAV_L = 0xFF2A1E
NAV_R = 0x3DFF6A
TAIL = 0xF4F7FF
FORM = 0xB6FF6A
STROBE = 0xFF2A1E
NOZZLE_GLOW = 0x245BFF
NOZZLE_CORE = 0x7FA6FF
NAV = 2.4    # intensidad de las luces de posición (Vd.nav)
FORM_K = 0.8  # tiras de formación (Vd.form)


def avg_of(cols):
    return [(cols[0][i] * 2 + cols[1][i] + cols[2][i]) / 4 for i in range(3)]


# botOf del prototipo: la panza va lisa en un tono neutro sacado de la pintura
def bottom_of(cols):
    return [hg.js_round(v + ((150, 150, 145)[i] - v) * 0.35) for i, v in enumerate(avg_of(cols))]


def rgb_hex(c):
    return (c[0] << 16) | (c[1] << 8) | c[2]


# Repetición de cada textura: se multiplica en las UV, así no hace falta KHR_texture_transform
REPEAT = {"M_Paint": 0.35, "M_PaintArmor": 0.6, "M_PaintBottom": 1.0, "M_AntiSlip": 6.0, "M_Hazard": 3.0,
          "M_Gunmetal": 2.0, "M_Titanium": 2.0}
# Materiales con quemaduras: llevan la segunda capa de UV (metros sin repetición × 1,1)
SCORCH_MATERIALS = ["M_Paint", "M_PaintArmor", "M_PaintBottom", "M_AntiSlip", "M_Hazard", "M_Gunmetal", "M_Titanium"]
UV2_SCALE = 1.1

# Lo que glTF no lleva o Godot no lee, para que lo aplique el casco al cargar
COAT = {"M_CanopyArmor": (1.0, 0.0), "M_SensorGlass": (1.0, 0.0)}
ROUGHNESS_FACTOR = {"M_PaintBottom": 2.6}
REFLECTIONS = {"M_CanopyArmor": 1.4}
IRIDESCENCE = {"M_SensorGlass": {"factor": 0.6, "ior": 1.5, "thickness": [300, 600]}}
TRANSPARENT = {"M_CanopyArmor": 0.72}
DECAL_MATERIALS = ["M_Decal_Number", "M_Decal_Insignia"]
# Máscaras del camuflaje: R tono, G desconchón, B grano (sección 3.5). Godot pone los colores de la pintura.
CAMO_MASKS = {"M_Paint": ("hull_blade_camo_mask.png", 55), "M_PaintArmor": ("hull_blade_armor_mask.png", 57)}

ZONES = ["Fuselage", "Canopy", "Prong_L", "Prong_R", "Pod_L", "Pod_R", "Armor", "Markings"]
SIDES = (("L", -1), ("R", 1))

PAINTED = ["Fuselage_Upper", "Prong_L_Base", "Prong_R_Base", "Pod_L_Hump", "Pod_R_Hump", "Fairing"]
TIPS = ["Prong_L_Tip", "Prong_R_Tip"]
ARMOR = [f"Armor_{i}_{s}_Plate" for i in range(4) for s, _ in SIDES]
BOTTOM = ["Fuselage_Lower"]
ACCENTS = ["Accent_FormNose_L", "Accent_FormNose_R", "Accent_FormProng_L", "Accent_FormProng_R"]
NAV_LIGHTS = ["Light_Nav_L", "Light_Nav_R", "Light_Tip_L", "Light_Tip_R"]
TAIL_LIGHTS = ["Light_Tail"]
STROBES = ["Light_Strobe_Top", "Light_Strobe_Bottom"]
NOZZLE_LIGHTS = ["Nozzle_Glow_L", "Nozzle_Glow_R", "Nozzle_Core_L", "Nozzle_Core_R"]
GLASS = ["Canopy_Glass", "Sensor_Ball"]
# Suelo de la cabina: el color base de la pintura, liso (el camuflaje no se ve a través del cristal)
FLOOR = ["Canopy_Floor"]
DECALS = ["Decal_Number_L", "Decal_Number_R", "Decal_Insignia_L", "Decal_Insignia_R"]
# Luces que titilan con el daño (6.5): la anticolisión y las toberas no
FLICKER_LIGHTS = ACCENTS + NAV_LIGHTS + TAIL_LIGHTS
EMPTIES = {"Exhaust_L": (-0.60, 0.08, 3.30), "Exhaust_R": (0.60, 0.08, 3.30),
           "Trail_L": (-1.60, 0.0, 3.60), "Trail_R": (1.60, 0.0, 3.60), "Smoke": (0.60, 0.35, 2.40)}
PROBE = "Nose_Pitot"


def contract_names():
    return (PAINTED + TIPS + ARMOR + BOTTOM + ACCENTS + NAV_LIGHTS + TAIL_LIGHTS + STROBES + NOZZLE_LIGHTS + GLASS + FLOOR
            + DECALS + [PROBE])


def parse_out():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--out" in args:
        path = args[args.index("--out") + 1]
        return path if os.path.isabs(path) else os.path.join(REPO, path)
    return os.path.join(HERE, "out")


# ---------- Materiales ----------

def _bsdf_input(bsdf, *names):
    for n in names:
        if n in bsdf.inputs:
            return bsdf.inputs[n]
    return None


# Principled BSDF con texturas de imagen: color (× un color, como map × color de three.js), rugosidad y normal
def pbr(name, maps, color=None, metallic=0.0, rough_factor=1.0, coat=0.0, coat_rough=0.0, normal_strength=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_backface_culling = False
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Coat Weight"].default_value = coat
    bsdf.inputs["Coat Roughness"].default_value = coat_rough
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(maps["color"], check_existing=True)
    if color is not None:
        mix = nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        links.new(tex.outputs["Color"], mix.inputs["A"])
        mix.inputs["B"].default_value = io.hex_color(color)
        links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    else:
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    rough = nodes.new("ShaderNodeTexImage")
    rough.image = bpy.data.images.load(maps["roughness"], check_existing=True)
    rough.image.colorspace_settings.name = "Non-Color"
    if rough_factor != 1.0:
        mul = nodes.new("ShaderNodeMath")
        mul.operation = "MULTIPLY"
        mul.use_clamp = True
        links.new(rough.outputs["Color"], mul.inputs[0])
        mul.inputs[1].default_value = rough_factor
        links.new(mul.outputs[0], bsdf.inputs["Roughness"])
    else:
        links.new(rough.outputs["Color"], bsdf.inputs["Roughness"])
    nrm = nodes.new("ShaderNodeTexImage")
    nrm.image = bpy.data.images.load(maps["normal"], check_existing=True)
    nrm.image.colorspace_settings.name = "Non-Color"
    nm = nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = normal_strength
    links.new(nrm.outputs["Color"], nm.inputs["Color"])
    links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


class Materials:
    def __init__(self, texture_folder):
        self.folder = texture_folder
        camo = io.save_texture_set(texture_folder, "CamoMask", tb.camo_mask(55))
        armor = io.save_texture_set(texture_folder, "ArmorMask", tb.camo_mask(57))
        bottom = io.save_texture_set(texture_folder, "PaintNeutral", tx.paint_set(58, (255, 255, 255)))
        gun = io.save_texture_set(texture_folder, "Gunmetal", tx.brushed_set(31, (92, 96, 100), False))
        ti = io.save_texture_set(texture_folder, "Titanium", tx.brushed_set(32, (150, 148, 146), True))
        hazard = io.save_texture_set(texture_folder, "Hazard", tb.hull_hazard_set(65))
        anti = io.save_texture_set(texture_folder, "AntiSlip", tb.hull_antislip_set(63))
        self.mask_maps = {"M_Paint": camo, "M_PaintArmor": armor}
        self.bottom_maps = bottom

        # El camuflaje es una máscara: en el modelo, la máscara como color; Godot reconstruye el de la pintura
        self.paint = pbr("M_Paint", camo, None, 0.1, 1.0, 0.0, 0.0, 0.6)
        self.armor = pbr("M_PaintArmor", armor, None, 0.15, 1.0, 0.0, 0.0, 0.6)
        self.bottom = pbr("M_PaintBottom", bottom, rgb_hex(bottom_of(WOODLAND)), 0.1, 2.6, 0.0, 0.0, 0.4)
        self.anti = pbr("M_AntiSlip", anti, None, 0.0)
        self.hazard = pbr("M_Hazard", hazard, None, 0.1)
        self.gun = pbr("M_Gunmetal", gun, None, 1.0)
        self.ti = pbr("M_Titanium", ti, None, 1.0)
        self.dark = io.material("M_Dark", base=io.hex_color(0x0B0B0D), metallic=0.2, roughness=0.7)
        self.intake = io.material("M_Intake", base=io.hex_color(0x050506), metallic=0.1, roughness=0.9)
        self.floor = io.material("M_CanopyFloor", base=io.hex_color(rgb_hex(WOODLAND[0])), metallic=0.1, roughness=0.7)
        self.glass = io.material("M_CanopyArmor", base=io.hex_color(0x0A0D0A), metallic=0.1, roughness=0.06,
                                 coat=1.0, coat_roughness=0.0, alpha=0.72)
        self.sensor = io.material("M_SensorGlass", base=io.hex_color(0x05070A), metallic=0.3, roughness=0.05,
                                  coat=1.0, coat_roughness=0.0)
        bsdf = next(n for n in self.sensor.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        thickness = _bsdf_input(bsdf, "Thin Film Thickness")
        if thickness is not None:
            thickness.default_value = 450.0
            _bsdf_input(bsdf, "Thin Film IOR").default_value = 1.5
        # Calcomanías: negras y opacas en el modelo; Godot pone su dibujo con alfa
        self.decals = {n: io.material(n, base=(0, 0, 0, 1), roughness=0.6) for n in DECAL_MATERIALS}
        self.emissive = {}  # nombre de la pieza → (color, intensidad)

    # Cada luz viva con su propio material, para que Godot la encienda, la apague o le cambie el color sola
    def glow(self, piece, color, intensity):
        self.emissive[piece] = (color, intensity)
        return io.material(f"M_Emissive_{piece}", base=(0, 0, 0, 1), roughness=0.4,
                           emission=io.hex_color(color), emission_strength=intensity)


# ---------- Matrices ----------

T = g.translation
R = g.euler_xyz


# ---------- buildHull() ----------

def build_fuselage(b, m):
    b.zone = "Fuselage"
    b.add(bg.fus_patch(0, math.pi, bg.Z0, bg.Z1), m.paint, name="Fuselage_Upper")
    b.add(bg.fus_patch(math.pi, math.pi * 2, bg.Z0, bg.Z1), m.bottom, name="Fuselage_Lower")
    b.add(bg.fus_cap(), m.dark, name="Fuselage_Rear")
    # panel antirreflejos delante de la cúpula (lo que se ve desde la cabina)
    b.add(bg.skin_grid(-2.65, -1.62, lambda z: (lambda w: (-w, w))(0.10 + 0.16 * bg._clamp01((z + 2.65) / 1.03)),
                       0.003, 10, 8), m.anti, name="AntiGlare")
    # juntas de panel
    for i, pts in enumerate(bg.seam_lines()):
        b.add(bg.skin_ribbon(pts, 0.009, 0.0015), m.dark, name=f"Seam_{i}", no_shadow=True)
    # sonda del morro: más corta que la de la cabina 3D (llega a z −5,75)
    b.add(hg.lathe([(0.0, -5.75), (0.006, -5.72), (0.008, -5.5), (0.014, -5.45), (0.016, -5.2), (0.03, -5.05)], 16),
          m.gun, T((0.0, bg.NOSE_Y, 0.0)), PROBE)
    # luces de posición del morro: roja a la izquierda, verde a la derecha
    for side, k in SIDES:
        z = -3.3
        p = bg.prof(z)
        piece = f"Light_Nav_{side}"
        b.add(g.sphere(0.022, 12, 8), m.glow(piece, NAV_L if k < 0 else NAV_R, NAV),
              T((k * (p["hw"] + 0.004), p["side"] + 0.006, z)), piece, True)
    # tiras de formación a los lados del morro
    for side, k in SIDES:
        pts = [(k * bg.prof(-3.05 + 0.85 * i / 10)["hw"] * 0.86, -3.05 + 0.85 * i / 10) for i in range(11)]
        piece = f"Accent_FormNose_{side}"
        b.add(bg.skin_ribbon(pts, 0.03, 0.004), m.glow(piece, FORM, FORM_K), name=piece, no_shadow=True)
    # luz blanca de cola y anticolisión (apagada en reposo: destella en el juego)
    b.add(hg.box(0.16, 0.03, 0.01), m.glow("Light_Tail", TAIL, NAV * 1.2),
          T((0.0, bg.prof(bg.Z1)["top"] - 0.07, bg.Z1 + 0.006)), "Light_Tail", True)
    dome = g.sphere_section(0.03, 12, 8, 0.0, math.pi * 2, 0.0, math.pi / 2)
    b.add(dome, m.glow("Light_Strobe_Top", STROBE, 0.0), T((0.0, bg.fair_y(0, 1.5) - 0.004, 1.5)), "Light_Strobe_Top", True)
    b.add(dome, m.glow("Light_Strobe_Bottom", STROBE, 0.0),
          T((0.0, bg.fus_bot_y(0, 0.3) + 0.004, 0.3)) @ R((math.pi, 0.0, 0.0)), "Light_Strobe_Bottom", True)
    # bola de sensores bajo el morro
    z = -3.1
    y = bg.fus_bot_y(0, z)
    b.add(g.cylinder(0.1, 0.11, 0.05, 20), m.gun, T((0.0, y - 0.01, z)), "Sensor_Mount")
    b.add(g.sphere(0.085, 24, 16), m.sensor, T((0.0, y - 0.07, z)), "Sensor_Ball")


def build_canopy(b, m):
    b.zone = "Canopy"
    b.add(bg.canopy(), m.glass, name="Canopy_Glass", no_shadow=True)
    b.add(hg.closed_tube(bg.canopy_ring(), 0.014, 240, 8), m.gun, name="Canopy_Seal")
    # arco fino delante del piloto (el mismo que se ve desde dentro)
    z = -1.15
    h = bg.canopy_half_x(z)
    pts = [(-h + 2 * h * i / 16, bg.canopy_y((-h + 2 * h * i / 16) * 0.999, z) + 0.006, z) for i in range(17)]
    b.add(g.tube(pts, 0.013, 32, 8), m.gun, name="Canopy_Bow")
    # barras de la cúpula blindada
    for i, x in enumerate((-0.13, 0.0, 0.13)):
        hz = bg.canopy_half_z(x)
        pts = []
        for j in range(19):
            d = -1 + 2 * j / 18
            zz = bg.CAN["zc"] + d * hz * (bg.CAN["Bf"] if d < 0 else bg.CAN["Br"]) * 0.97
            pts.append((x, bg.canopy_y(x, zz) + 0.008, zz))
        b.add(g.tube(pts, 0.012, 40, 8), m.gun, name=f"Canopy_Bar_{i}")
    # carenado detrás de la cúpula
    b.add(bg.fairing(), m.paint, name="Fairing")
    # suelo de la cabina, del color base de la pintura (sin camuflaje)
    b.add(bg.canopy_floor(), m.floor, name="Canopy_Floor", no_shadow=True)


NAC = [[0.55, 0.26, 0.05, 0.03, 0.0], [1.30, 0.28, 0.15, 0.06, 0.0], [2.20, 0.28, 0.17, 0.07, 0.0],
       [2.80, 0.28, 0.17, 0.07, 0.0]]
NOZ = [[2.80, 0.28, 0.17, 0.07, 0.0], [3.05, 0.27, 0.14, 0.06, 0.0], [3.30, 0.25, 0.10, 0.04, 0.0]]


def build_side(b, m, side, k):
    # ---- punta trasera: base, punta, tira de formación y luz de posición ----
    b.zone = "Prong_" + side
    b.add(bg.prong(k, bg.PRONG_BASE), m.paint, name=f"Prong_{side}_Base")
    b.add(bg.prong(k, bg.PRONG_TIP), m.hazard, name=f"Prong_{side}_Tip")
    piece = f"Accent_FormProng_{side}"
    b.add(g.tube([(k * 1.565, 0.072, 2.9), (k * 1.585, 0.072, 3.42)], 0.007, 6, 12), m.glow(piece, FORM, FORM_K),
          name=piece, no_shadow=True)
    piece = f"Light_Tip_{side}"
    b.add(g.sphere(0.024, 12, 8), m.glow(piece, NAV_L if k < 0 else NAV_R, NAV), T((k * 1.588, 0.04, 3.52)), piece, True)

    # ---- joroba del motor y tobera plana ----
    b.zone = "Pod_" + side
    pod = T((k * 0.6, 0.08, 0.0))
    b.add(hg.oct_loft(NAC, 0.55, 2.80, 40), m.paint, pod, f"Pod_{side}_Hump")
    # toma de aire enrasada en el lomo de la joroba: hueco oscuro con lamas
    z0, z1 = 1.08, 1.5
    a, bb, c, _ = hg.sec_at(NAC, (z0 + z1) / 2)
    b.add(hg.box(2 * (a - c) - 0.04, 0.004, z1 - z0), m.intake, pod @ T((0.0, bb + 0.002, (z0 + z1) / 2)), f"Pod_{side}_Intake")
    for q in range(6):
        zq = z0 + 0.04 + q * 0.07
        b.add(hg.box(2 * (a - c) - 0.06, 0.012, 0.03), m.gun,
              pod @ T((0.0, hg.sec_at(NAC, zq)[1] + 0.006, zq)) @ R((-0.5, 0.0, 0.0)), f"Pod_{side}_IntakeVane")
    for i, z in enumerate((1.9, 2.55)):
        b.add(hg.oct_loft(NAC, z, z + 0.012, 1, 0.002), m.dark, pod, f"Pod_{side}_Seam_{i}", True)
    # tobera bidimensional de titanio con flaps, paredes laterales y fondo luminoso
    b.add(hg.oct_loft(NOZ, 2.80, 3.30, 16), m.ti, pod, f"Nozzle_{side}")
    a, bb, c, _ = hg.sec_at(NOZ, 3.30)
    b.add(hg.oct_annulus(hg.oct_ring(a, bb, c), 3.30, hg.oct_ring(a - 0.025, bb - 0.025, c * 0.6), 3.30), m.ti, pod,
          f"Nozzle_{side}_Lip")
    b.add(hg.oct_loft(NOZ, 2.95, 3.30, 6, 0.0, 0.025), m.intake, pod, f"Nozzle_{side}_Inner")
    for s in (1, -1):
        flap = pod @ T((0.0, s * 0.092, 3.31)) @ R((s * 0.12, 0.0, 0.0))
        b.add(hg.box(0.44, 0.012, 0.18), m.ti, flap, f"Nozzle_{side}" + ("_FlapTop" if s > 0 else "_FlapBottom"))
        for q in range(-2, 3):
            b.add(hg.box(0.005, 0.016, 0.16), m.dark, flap @ T((q * 0.085, s * 0.002, 0.0)), "Nozzle_FlapGroove")
        b.add(hg.box(0.012, 0.17, 0.16), m.ti, pod @ T((s * 0.255, 0.0, 3.31)), f"Nozzle_{side}" + ("_WallR" if s > 0 else "_WallL"))
    piece = f"Nozzle_Glow_{side}"
    b.add(hg.rrect(0.42, 0.14, 0.04), m.glow(piece, NOZZLE_GLOW, 2.2), pod @ T((0.0, 0.0, 3.0)), piece, True)
    piece = f"Nozzle_Core_{side}"
    b.add(hg.rrect(0.26, 0.05, 0.022), m.glow(piece, NOZZLE_CORE, 3.5), pod @ T((0.0, 0.0, 3.05)), piece, True)


ARMOR_PLATES = [(0.2, -2.95, 0.24, 0.55), (0.32, -2.2, 0.28, 0.66), (0.62, -0.8, 0.34, 0.95), (1.0, 0.6, 0.32, 0.8)]


def build_armor(b, m):
    b.zone = "Armor"
    t = 0.022
    for i, (x, z, w, l) in enumerate(ARMOR_PLATES):
        for side, k in SIDES:
            name = f"Armor_{i}_{side}"
            frame = bg.plate_frame(k * x, z, t)
            b.add(hg.box(w, t, l), m.armor, frame, name + "_Plate")
            bolt = g.cylinder(0.016, 0.016, 0.012, 10)
            for a, c in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
                b.add(bolt, m.gun, frame @ T((a * (w / 2 - 0.035), t / 2 + 0.004, c * (l / 2 - 0.035))), name + "_Bolt", True)


# ---------- Marcas: solo la geometría; los dibujos los pone Godot ----------

def build_markings(b, m):
    b.zone = "Markings"
    # número: en el lomo del morro a cada lado, se lee desde el costado
    for side, k in SIDES:
        def xr(z, k=k):
            h = bg.prof(z)["hw"]
            return (0.04, h * 0.84) if k > 0 else (-h * 0.84, -0.04)
        uv_fn = (lambda iu, jv: (1 - iu, 1 - jv)) if k > 0 else (lambda iu, jv: (iu, jv))
        b.add(bg.skin_grid(-4.05, -3.15, xr, 0.004, 12, 6, uv_fn), m.decals["M_Decal_Number"],
              name=f"Decal_Number_{side}", no_shadow=True)
    # distintivo arriba, uno a cada lado (se lee desde arriba con el morro hacia delante)
    for side, k in SIDES:
        xs = (0.95, 1.4) if k > 0 else (-1.4, -0.95)
        b.add(bg.skin_grid(1.6, 2.1, lambda z, xs=xs: xs, 0.004, 8, 8, lambda iu, jv: (jv, 1 - iu)),
              m.decals["M_Decal_Insignia"], name=f"Decal_Insignia_{side}", no_shadow=True)


# ---------- De las piezas a objetos de Blender ----------

def uv_args(material):
    return REPEAT.get(material.name, 1.0), (UV2_SCALE if material.name in SCORCH_MATERIALS else None)


# Las piezas del contrato van solas; el resto se junta por zona, material y sombra (menos llamadas de dibujo).
# Devuelve los objetos que no proyectan sombra.
def create_objects(builder, zones, collection):
    contract = set(contract_names())
    merged = {}
    no_shadow = []
    for piece in builder.pieces:
        if piece.name in contract:
            uv1, uv2 = uv_args(piece.material)
            io.mesh_object(piece.name, piece.soup, piece.material, zones[piece.zone], collection, piece.matrix, uv1, uv2)
            if piece.no_shadow:
                no_shadow.append(piece.name)
            continue
        key = (piece.zone, piece.material.name, piece.no_shadow)
        mtx = piece.matrix
        nm = np.linalg.inv(mtx[:3, :3]).T
        if key not in merged:
            merged[key] = (piece.material, g.Soup([], [], []))
        target = merged[key][1]
        target.positions += [g.transform_point(mtx, p) for p in piece.soup.positions]
        target.normals += [g._normalize(tuple(nm @ np.array(n))) for n in piece.soup.normals]
        target.uvs += piece.soup.uvs
    for (zone, material_name, shadowless), (material, soup) in merged.items():
        name = f"{zone}_{material_name}" + ("_NoShadow" if shadowless else "")
        uv1, uv2 = uv_args(material)
        io.mesh_object(name, soup, material, zones[zone], collection, None, uv1, uv2)
        if shadowless:
            no_shadow.append(name)
    return no_shadow


def create_empties(zones, root, collection):
    parents = {"Exhaust_L": zones["Pod_L"], "Exhaust_R": zones["Pod_R"], "Trail_L": zones["Prong_L"],
               "Trail_R": zones["Prong_R"], "Smoke": root}
    for name, pos in EMPTIES.items():
        obj = io.empty(name, parents[name], collection)
        obj.matrix_basis = Matrix([list(r) for r in T(pos)])


def check_contract(root):
    present = {o.name for o in root.children_recursive}
    missing = [n for n in contract_names() + list(EMPTIES) if n not in present]
    if missing:
        raise RuntimeError("Faltan piezas del contrato con Godot: " + ", ".join(missing))


# De Blender (Z arriba) a Godot (Y arriba), como hace el exportador
def godot_point(obj, co):
    w = obj.matrix_world @ co
    return (w.x, w.z, -w.y)


def bounds(objects):
    low = [float("inf")] * 3
    high = [float("-inf")] * 3
    triangles = 0
    for obj in objects:
        if obj.type != "MESH":
            continue
        triangles += len(obj.data.polygons)
        for v in obj.data.vertices:
            p = godot_point(obj, v.co)
            for i in range(3):
                low[i] = min(low[i], p[i])
                high[i] = max(high[i], p[i])
    return low, high, triangles


def marking_objects(zones):
    return set(zones["Markings"].children_recursive)


# Comprobación de la spec (3.3), en coordenadas de Godot y con marcas
def check_axes(root):
    objs = bpy.data.objects
    every = list(root.children_recursive)
    low, high, _ = bounds([o for o in every if o.name != PROBE])

    def ob(name):
        return bounds([objs[name]])

    tip_low, tip_high, _ = ob("Fuselage_Upper")
    probe_low, _, _ = ob(PROBE)
    _, prong_high, _ = ob("Prong_R_Tip")
    _, can_high, _ = ob("Canopy_Glass")
    half = max(-low[0], high[0])
    checks = [
        ("punta del morro z", tip_low[2], -5.20, 0.005),
        ("punta de la sonda z", probe_low[2], -5.75, 0.005),
        ("final de las puntas traseras z", prong_high[2], 3.608, 0.005),
        ("caja z mín (sin sonda)", low[2], -5.20, 0.005),
        ("caja z máx", high[2], 3.608, 0.005),
        ("semiancho", half, 1.612, 0.005),
        ("caja y mín", low[1], -0.234, 0.005),
        ("caja y máx", high[1], 0.519, 0.005),
        ("cima de la cúpula y", can_high[1], 0.499, 0.01),
    ]
    for name, want in EMPTIES.items():
        p = godot_point(objs[name], objs[name].location * 0)
        checks += [(f"{name} {'xyz'[i]}", p[i], want[i], 0.001) for i in range(3)]
    bad = [f"{name} = {value:.4f} (debe ser {want} ± {tol})" for name, value, want, tol in checks if abs(value - want) > tol]
    if bad:
        raise RuntimeError("Medidas mal: " + "; ".join(bad))
    return checks, low, high


def r4(v):
    return [round(c, 4) for c in v]


def write_manifest(path, root, zones, materials, no_shadow, env_name):
    markings = marking_objects(zones)
    every = list(root.children_recursive)
    body = [o for o in every if o not in markings]
    low, high, tris = bounds(body)
    low_m, high_m, tris_m = bounds(every)
    low_p, high_p, _ = bounds([o for o in body if o.name != PROBE])
    objs = bpy.data.objects
    pieces = []
    for name in contract_names():
        entry = {"name": name}
        if name in materials.emissive:
            color, intensity = materials.emissive[name]
            entry["emission"] = f"#{color:06X}"
            entry["intensity"] = intensity
        pieces.append(entry)
    empties = [{"name": n, "position": r4(godot_point(objs[n], objs[n].location * 0))} for n in EMPTIES]
    manifest = {
        "blender": bpy.app.version_string,
        "exported": datetime.date.today().isoformat(),
        "root": root.name,
        # buildEnv() del prototipo del casco es el de BLADE (semilla 77): se reutiliza su panorama
        "environment": env_name,
        "pieces": pieces,
        "empties": empties,
        "painted": PAINTED,
        "tips": TIPS,
        "armor": ARMOR,
        "bottom": BOTTOM,
        "accents": ACCENTS,
        "navLights": NAV_LIGHTS,
        "tailLights": TAIL_LIGHTS,
        "strobes": STROBES,
        "nozzleLights": NOZZLE_LIGHTS,
        "flickerLights": FLICKER_LIGHTS,
        "decals": DECALS,
        "floor": FLOOR,
        # "livery": las piezas que solo se ven con marcas (mismo campo que VECTOR)
        "livery": sorted(o.name for o in markings),
        "probe": PROBE,
        "noShadow": no_shadow,
        "camoMasks": [{"material": k, "mask": f, "seed": s} for k, (f, s) in CAMO_MASKS.items()],
        "repeats": [{"material": k, "repeat": v} for k, v in REPEAT.items()],
        "coat": [{"material": k, "coat": c, "roughness": r} for k, (c, r) in COAT.items()],
        "roughnessFactors": [{"material": k, "factor": v} for k, v in ROUGHNESS_FACTOR.items()],
        "iridescence": [dict(material=k, **v) for k, v in IRIDESCENCE.items()],
        "reflections": [{"material": k, "factor": v} for k, v in REFLECTIONS.items()],
        "transparentMaterials": [{"material": k, "opacity": v} for k, v in TRANSPARENT.items()],
        "decalMaterials": DECAL_MATERIALS,
        "scorchMaterials": SCORCH_MATERIALS,
        "scorchMasks": [f"hull_scorch_{i}.png" for i in (1, 2, 3)],
        "uv2Scale": UV2_SCALE,
        "triangles": tris,
        "trianglesWithLivery": tris_m,
        # caja sin marcas y con la sonda (la nave entera: holograma), con marcas, y sin marcas ni sonda
        "bounds": {"min": r4(low), "max": r4(high)},
        "boundsWithLivery": {"min": r4(low_m), "max": r4(high_m)},
        "boundsWithoutProbe": {"min": r4(low_p), "max": r4(high_p)},
        "halfWidth": round(max(-low[0], high[0]), 4),
        "halfWidthWithLivery": round(max(-low_m[0], high_m[0]), 4),
    }
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")
    return manifest


# PNG RGB de 8 bits sin fecha (máscaras del camuflaje)
def write_rgb_png(path, rgb):
    height, width, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(height))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    import struct
    import zlib
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(tx._chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)))
        f.write(tx._chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(tx._chunk(b"IEND", b""))


# ---------- Capturas ----------

def test_decal_material(name, width, height):
    pattern = tx.decal_test_pattern(width, height)
    img = bpy.data.images.new(f"T_DecalTest_{name}", width, height, alpha=True)
    img.pixels.foreach_set((np.flipud(pattern).astype(np.float32) / 255.0).ravel())
    img.pack()  # dentro del .blend: si no, al abrirlo la imagen generada sale vacía
    mat = bpy.data.materials.new(f"M_DecalTest_{name}")
    mat.use_backface_culling = False
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = img
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.5
    links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 0.35
    return mat


# Material de prueba de las capturas: el camuflaje BOSQUE ya coloreado, con la regla de la sección 6.2
def preview_paint_materials(materials):
    cols = [list(c) for c in WOODLAND]
    armor_cols = [[hg.js_round(x * 0.85) for x in c] for c in cols]
    folder = materials.folder
    paint = io.save_texture_set(folder, "PreviewCamo", tb.camo_set(55, *cols))
    armor = io.save_texture_set(folder, "PreviewArmor", tb.camo_set(57, *armor_cols))
    return {"M_Paint": pbr("M_Preview_Paint", paint, None, 0.1, 1.0, 0.0, 0.0, 0.6),
            "M_PaintArmor": pbr("M_Preview_PaintArmor", armor, None, 0.15, 1.0, 0.0, 0.0, 0.6)}


def render_previews(scene, collection, root, zones, materials, env_path, out):
    scene.render.engine = "BLENDER_EEVEE"
    views = {v.identifier for v in scene.view_settings.bl_rna.properties["view_transform"].enum_items}
    aces = next((n for n in ("ACES 2.0", "ACES 1.3", "ACES") if n in views), None)
    scene.view_settings.view_transform = aces or "Filmic"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0

    def linear(hex_value):
        return io.hex_color(hex_value)[:3]

    io.preview_world(scene, env_path, linear(0xB0B8C8), linear(0x101010), 0.3, (0, 0, 0), 0.0, (0.002, 0.003, 0.006, 1.0))
    for name, source, color, strength in (("Key", (4, 10, -5), 0xFFF1E0, 2.4), ("Rim", (-6, 2.5, 9), 0xFF9D50, 1.3),
                                          ("Fill", (8, 3, 2), 0x5F8CFF, 0.55)):
        light = io.preview_light_sun(collection, root, source, (0, 0, 0), strength)
        light.name = "Preview" + name
        light.data.color = linear(color)
        if name != "Key":
            light.data.use_shadow = False

    for obj in root.children_recursive:
        if obj.type == "MESH" and (obj.name.endswith("_NoShadow") or obj.name in NO_SHADOW_NAMES):
            obj.visible_shadow = False

    # BOSQUE coloreado en lugar de la máscara (solo en las capturas y en el .blend)
    swap = preview_paint_materials(materials)
    for obj in root.children_recursive:
        if obj.type == "MESH" and obj.data.materials and obj.data.materials[0].name in swap:
            obj.data.materials[0] = swap[obj.data.materials[0].name]

    folder = os.path.join(out, "previews")
    # las del prototipo con 0,65 m menos de altura
    shots = [("tq", (-8.6, 3.1, -8.4), (0, 0.15, -0.3), 38), ("top", (0, 18.85, -0.8), (0, 0, -0.8), 34),
             ("side", (12.5, 0.4, -0.8), (0, 0.2, -0.8), 34), ("rear", (4.5, 1.8, 14.5), (0, 0.2, 0.8), 34)]
    markings = marking_objects(zones)
    # las calcomanías se ven negras y opacas sin su dibujo: en estas capturas, ocultas
    for obj in markings:
        obj.hide_render = True
    for name, eye, target, fov in shots:
        io.render_preview(scene, collection, root, os.path.join(folder, f"hull_blade_{name}.png"), eye, target, fov)
    # Marcas con la textura de prueba: ninguna flecha girada ni en espejo
    sizes = {"Decal_Number": (1024, 320), "Decal_Insignia": (512, 512)}
    for obj in markings:
        obj.hide_render = False
        if obj.name in DECALS:
            key = next(k for k in sizes if obj.name.startswith(k))
            obj.data.materials[0] = test_decal_material(obj.name, *sizes[key])
    io.render_preview(scene, collection, root, os.path.join(folder, "hull_blade_markings.png"), *shots[1][1:])
    return aces or "Filmic"


NO_SHADOW_NAMES = set()


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    out = parse_out()
    os.makedirs(out, exist_ok=True)
    glb_path = os.path.join(ASSETS, "hull_blade.glb")
    manifest_path = os.path.join(ASSETS, "hull_blade.manifest.json")
    env_name = "cockpit_blade_env.png"
    env_path = os.path.join(COCKPIT_ASSETS, env_name)
    if not os.path.exists(env_path):
        raise RuntimeError(f"Falta el panorama de BLADE ({env_path}): ejecuta antes art/blender/run.ps1 blade")

    io.clear_file()
    scene = bpy.context.scene
    collection = scene.collection

    # Texturas sueltas aparte de las de las cabinas y de VECTOR: algunas se llaman igual
    materials = Materials(os.path.join(out, "textures", "hull_blade"))
    builder = Builder()
    build_fuselage(builder, materials)
    build_canopy(builder, materials)
    for side, k in SIDES:
        build_side(builder, materials, side, k)
    build_armor(builder, materials)
    build_markings(builder, materials)

    # Raíz girada +90° en X: un punto del prototipo (x, y, z) queda en Blender en (x, −z, y) y vuelve a ser (x, y, z) en glTF
    root = io.empty("HullBlade", None, collection, rotation=(math.pi / 2, 0.0, 0.0))
    zones = {zone: io.empty(zone, root, collection) for zone in ZONES}
    no_shadow = create_objects(builder, zones, collection)
    NO_SHADOW_NAMES.update(no_shadow)
    create_empties(zones, root, collection)
    bpy.context.view_layer.update()

    check_contract(root)
    checks, low, high = check_axes(root)
    manifest = write_manifest(manifest_path, root, zones, materials, no_shadow, env_name)
    io.export_glb(glb_path, root)

    # Máscaras del camuflaje sin pérdidas, aparte del .glb (Godot las lee tal cual, con filtro más cercano)
    for material, (file_name, seed) in CAMO_MASKS.items():
        write_rgb_png(os.path.join(ASSETS, file_name), tb.camo_mask(seed)["color"])

    preview_collection = bpy.data.collections.new("Preview")
    scene.collection.children.link(preview_collection)
    view = render_previews(scene, preview_collection, root, zones, materials, env_path, out)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "hull_blade.blend"))

    print(f"[Hull] Blender {bpy.app.version_string} · capturas con {scene.render.engine} y vista {view} · panorama {env_name}")
    for name, value, want, tol in checks:
        print(f"[Hull] {name}: {value:.4f} (spec {want} ± {tol})")
    print(f"[Hull] Caja con marcas y sin sonda {r4(low)} → {r4(high)} · largo {high[2] - low[2]:.3f} · alto {high[1] - low[1]:.3f}")
    b = manifest["bounds"]
    size = [b["max"][i] - b["min"][i] for i in range(3)]
    print(f"[Hull] Caja con sonda {size} · esfera {math.sqrt(sum(s * s for s in size)):.3f} m")
    print(f"[Hull] Triángulos {manifest['triangles']} sin marcas · {manifest['trianglesWithLivery']} con marcas")
    print(f"[Hull] GLB {os.path.getsize(glb_path)} bytes · sha256 {sha256(glb_path)}")


main()
