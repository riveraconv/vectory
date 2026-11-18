# Casco 3D de VECTOR, estilo CARRERAS, variante NOCTURNA (spec docs/cascos-3d-1-vector.md). Traducción de buildHull()
# con VAR = 3 y SPAN = 2.6 de designs/casco-3d-vector.html, más la geometría de addLivery(): mismos números y mismo orden.
#
# Uso: blender -b --factory-startup --python art/blender/hull_vector.py -- --out art/blender/out
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
from lib import hull_geometry as hg  # noqa: E402
from lib import textures_hull as tx  # noqa: E402
from lib.builder import Builder  # noqa: E402

REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "assets", "ships")
COCKPIT_ASSETS = os.path.join(REPO, "assets", "cockpits")

N = hg.N_NOCTURNA   # exponente de la sección del fuselaje (NOCTURNA)
SPAN = 2.6          # semiancho del ala (punta)

# Colores del prototipo (COL y la pintura GRAFITO, la de por defecto)
PAINT_COLOR = 0x222429
ACCENT = 0xFF2D2D
TAIL = 0xFF1A1A
NOZZLE_GLOW = 0x245BFF
NOZZLE_CORE = 0x7FA6FF
STRIPE = 0xFF2D2D   # franjas de la librea de GRAFITO
PIN = 0xF2F2F2      # fondo del dorsal de GRAFITO: filete del ala

# Repetición de cada textura: se multiplica en las UV, así no hace falta KHR_texture_transform
REPEAT = {"M_Carbon": 4.0, "M_CarbonSatin": 4.0, "M_Paint": 1.0, "M_Alu": 2.0, "M_Titanium": 2.0,
          "M_LiveryStripe": 1.0, "M_LiveryPin": 1.0}
# Materiales con quemaduras: llevan la segunda capa de UV (metros sin repetición × 1,1)
SCORCH_MATERIALS = ["M_Paint", "M_Carbon", "M_CarbonSatin", "M_Alu", "M_Titanium", "M_LiveryStripe", "M_LiveryPin"]
UV2_SCALE = 1.1

# Lo que glTF no lleva o Godot no lee, para que lo aplique el casco al cargar
COAT = {"M_Paint": (0.5, 0.22), "M_Carbon": (1.0, 0.05), "M_CarbonSatin": (0.3, 0.35), "M_CanopyIris": (1.0, 0.0),
        "M_LiveryStripe": (1.0, 0.04), "M_LiveryPin": (1.0, 0.04),
        "M_Decal_Number": (1.0, 0.05), "M_Decal_Sponsor": (1.0, 0.05), "M_Decal_Checker": (1.0, 0.05)}
ROUGHNESS_FACTOR = {"M_Paint": 1.7}
REFLECTIONS = {"M_CanopyIris": 1.8}
IRIDESCENCE = {"M_CanopyIris": {"factor": 1.0, "ior": 1.6, "thickness": [220, 620]}}
TRANSPARENT = {"M_CanopyIris": 0.7}
DECAL_MATERIALS = ["M_Decal_Number", "M_Decal_Sponsor", "M_Decal_Checker"]

ZONES = ["Fuselage", "Canopy", "Wing_L", "Wing_R", "Pod_L", "Pod_R", "Livery"]
SIDES = (("L", -1), ("R", 1))

PAINTED = ["Fuselage_Upper", "Endplate_L", "Endplate_R", "Pod_L_Cowl", "Pod_R_Cowl"]
ACCENTS = ["Accent_Chine_L", "Accent_Chine_R", "Accent_WingLE_L", "Accent_WingLE_R",
           "Accent_Endplate_L", "Accent_Endplate_R", "Accent_PodBand_L", "Accent_PodBand_R"]
ACCENT_LIGHTS = ["Light_Nose_L", "Light_Nose_R", "Light_Fin_L", "Light_Fin_R"]
TAIL_LIGHTS = ["Light_Tail_L", "Light_Tail_R"]
NOZZLE_LIGHTS = ["Nozzle_Glow_L", "Nozzle_Glow_R", "Nozzle_Core_L", "Nozzle_Core_R"]
STRIPES = ["Livery_TwinStripe_R_Front", "Livery_TwinStripe_R_Rear", "Livery_TwinStripe_L_Front",
           "Livery_TwinStripe_L_Rear", "Livery_WingBand_L", "Livery_WingBand_R"]
PINS = ["Livery_WingPin_L", "Livery_WingPin_R"]
DECALS = ["Decal_Number_Nose", "Decal_Number_Pod_L", "Decal_Number_Pod_R", "Decal_Sponsor_L", "Decal_Sponsor_R",
          "Decal_Sponsor_Pod_L", "Decal_Sponsor_Pod_R", "Decal_Checker_L", "Decal_Checker_R"]
EMPTIES = {"Exhaust_L": (-0.75, 0.1, 3.2), "Exhaust_R": (0.75, 0.1, 3.2),
           "Trail_L": (-SPAN, -0.05, 2.72), "Trail_R": (SPAN, -0.05, 2.72), "Smoke": (0.75, 0.5, 2.2)}
# Calcomanías planas de las góndolas: medidas del contrato
DECAL_SIZES = {"Decal_Number_Pod": (0.34, 0.204), "Decal_Sponsor_Pod": (0.42, 0.105), "Decal_Checker": (0.56, 0.14)}


def contract_names():
    return (PAINTED + ACCENTS + ACCENT_LIGHTS + TAIL_LIGHTS + NOZZLE_LIGHTS + ["Canopy_Glass"]
            + STRIPES + PINS + DECALS)


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
        carbon = io.save_texture_set(texture_folder, "Carbon", tx.carbon_set(11, 0.0))
        satin = io.save_texture_set(texture_folder, "CarbonSatin", tx.carbon_set(12, 0.28))
        paint = io.save_texture_set(texture_folder, "PaintNeutral", tx.paint_set(24, (255, 255, 255)))
        alu = io.save_texture_set(texture_folder, "Alu", tx.brushed_set(31, (176, 182, 190), False))
        ti = io.save_texture_set(texture_folder, "Titanium", tx.brushed_set(32, (150, 148, 146), True))

        self.paint = pbr("M_Paint", paint, PAINT_COLOR, 0.6, 1.7, 0.5, 0.22, 0.4)
        self.carbon = pbr("M_Carbon", carbon, None, 0.2, 1.0, 1.0, 0.05)
        self.carbon_satin = pbr("M_CarbonSatin", satin, None, 0.2, 1.0, 0.3, 0.35)
        self.alu = pbr("M_Alu", alu, None, 1.0)
        self.ti = pbr("M_Titanium", ti, None, 1.0)
        self.stripe = pbr("M_LiveryStripe", paint, STRIPE, 0.1, 1.0, 1.0, 0.04, 0.4)
        self.pin = pbr("M_LiveryPin", paint, PIN, 0.1, 1.0, 1.0, 0.04, 0.4)
        self.dark = io.material("M_Dark", base=io.hex_color(0x0B0B0D), metallic=0.2, roughness=0.7)
        self.intake = io.material("M_Intake", base=io.hex_color(0x050506), metallic=0.1, roughness=0.9)
        self.iris = io.material("M_CanopyIris", base=io.hex_color(0x120709), metallic=0.1, roughness=0.05,
                                coat=1.0, coat_roughness=0.0, alpha=0.7)
        bsdf = next(n for n in self.iris.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        thickness = _bsdf_input(bsdf, "Thin Film Thickness")
        if thickness is not None:
            thickness.default_value = 420.0
            _bsdf_input(bsdf, "Thin Film IOR").default_value = 1.6
        # Calcomanías: negras y opacas en el modelo; Godot pone su dibujo con alfa
        self.decals = {n: io.material(n, base=(0, 0, 0, 1), roughness=0.35, coat=1.0, coat_roughness=0.05)
                       for n in DECAL_MATERIALS}
        self.emissive = {}  # nombre de la pieza → (color, intensidad)

    # Cada luz viva con su propio material, para que Godot la encienda, la apague o le cambie el color sola
    def glow(self, piece, color, intensity):
        self.emissive[piece] = (color, intensity)
        return io.material(f"M_Emissive_{piece}", base=(0, 0, 0, 1), roughness=0.4,
                           emission=io.hex_color(color), emission_strength=intensity)


# ---------- Matrices ----------

T = g.translation
R = g.euler_xyz


def S(sx, sy, sz):
    return np.diag([sx, sy, sz, 1.0])


def sym(t0, t1):
    return [(t0, t1), (math.pi - t1, math.pi - t0)]


# ---------- buildHull() ----------

def build_fuselage(b, m):
    b.zone = "Fuselage"
    b.add(hg.fus_patch(0, math.pi, hg.Z0, hg.Z1, N), m.paint, name="Fuselage_Upper")
    b.add(hg.fus_patch(math.pi, math.pi * 2, hg.Z0, hg.Z1, N), m.carbon_satin, name="Fuselage_Lower")
    b.add(hg.fus_cap(N), m.dark, name="Fuselage_Rear")
    # lomo de carbono, cortado por la cúpula (el cristal es transparente)
    for i, z in enumerate(hg.cut_canopy((-3.9, 2.6), True)):
        b.add(hg.fus_patch(math.pi / 2 - 0.26, math.pi / 2 + 0.26, z[0], z[1], N, 0.004), m.carbon,
              name="Livery_Spine" + ("_Rear" if i else "_Front"))
    lights = [(t, (-3.7, 2.6), 0.9, "Accent_Chine") for t in sym(0.10, 0.135)]
    lights += [(t, (-3.55, -3.0), 3.0, "Light_Nose") for t in sym(0.2, 0.36)]
    for t, z, k, name in lights:
        piece = name + ("_R" if t[0] < 1.5 else "_L")
        b.add(hg.fus_patch(t[0], t[1], z[0], z[1], N, 0.006), m.glow(piece, ACCENT, k), name=piece, no_shadow=True)
    # pilotos traseros y difusor
    for k in (-1, 1):
        piece = "Light_Tail_L" if k < 0 else "Light_Tail_R"
        b.add(hg.box(0.36, 0.035, 0.012), m.glow(piece, TAIL, 3.5), T((k * 0.3, 0.24, hg.Z1 + 0.006)), piece, True)
    for i in range(5):
        b.add(hg.box(0.012, 0.12, 0.75), m.carbon, T((-0.36 + i * 0.18, -0.25, 2.22)), "Diffuser_Fin")
    # splitter bajo el morro
    b.add(hg.splitter(), m.carbon_satin, T((0.0, -0.2, 0.0)), "Splitter")


def build_canopy(b, m):
    b.zone = "Canopy"
    b.add(hg.canopy(N), m.iris, name="Canopy_Glass", no_shadow=True)
    b.add(hg.closed_tube(hg.canopy_ring(N), 0.016, 192, 8), m.dark, name="Canopy_Seal")


def build_side(b, m, side, k):
    # ---- ala, deriva y sus luces ----
    b.zone = "Wing_" + side
    b.add(hg.wing(k, SPAN), m.carbon, name=f"Wing_{side}_Skin")
    pts = []
    for i in range(9):
        p = hg.wing_le(k, SPAN, 0.12 + 0.85 * i / 8)
        pts.append((p[0], p[1] + 0.004, p[2] - 0.008))
    piece = f"Accent_WingLE_{side}"
    b.add(g.tube(pts, 0.011, 24, 12), m.glow(piece, ACCENT, 1.0), name=piece, no_shadow=True)
    b.add(hg.endplate(), m.paint, T((k * SPAN + 0.0175, 0.0, 0.0)), f"Endplate_{side}")
    piece = f"Accent_Endplate_{side}"
    b.add(g.tube([(k * (SPAN + 0.026), -0.07, 2.33), (k * (SPAN + 0.026), 0.30, 2.56)], 0.008, 8, 12),
          m.glow(piece, ACCENT, 1.0), name=piece, no_shadow=True)
    piece = f"Light_Fin_{side}"
    b.add(g.sphere(0.026, 12, 8), m.glow(piece, ACCENT, 3.5), T((k * SPAN, 0.32, 2.64)), piece, True)

    # ---- góndola: mismo sitio y largo que las toberas del wireframe, achatada ----
    b.zone = "Pod_" + side
    pod = T((k * 0.75, 0.1, 0.0))
    POD, NOZ = hg.POD, hg.NOZ
    b.add(hg.oct_loft(POD, 0.83, 2.72, 40), m.paint, pod, f"Pod_{side}_Cowl")
    piece = f"Accent_PodBand_{side}"
    b.add(hg.oct_loft(POD, 1.30, 1.34, 1, 0.004), m.glow(piece, ACCENT, 1.0), pod, piece, True)
    for i, z in enumerate((1.62, 2.40)):
        b.add(hg.oct_loft(POD, z, z + 0.012, 1, 0.002), m.dark, pod, f"Pod_{side}_Seam_{i}", True)
    # toma de aire: labio de aluminio, conducto oscuro, cara del compresor con cono y álabes
    a, bb, c, u = hg.sec_at(POD, 0.83)
    r0 = hg.oct_ring(*hg.sec_at(POD, 0.80))
    a2, b2, c2, u2 = hg.sec_at(POD, 1.16)
    b.add(hg.oct_loft(POD, 0.80, 0.83, 2), m.alu, pod, f"Pod_{side}_IntakeLip")
    b.add(hg.oct_annulus(r0, 0.80, hg.oct_ring(a - 0.035, bb - 0.035, c, u), 0.83), m.alu, pod, f"Pod_{side}_IntakeLipIn")
    b.add(hg.oct_loft(POD, 0.83, 1.16, 6, 0.0, 0.035), m.intake, pod, f"Pod_{side}_IntakeDuct")
    b.add(hg.oct_fan(hg.oct_ring(a2 - 0.035, b2 - 0.035, c2, u2), 1.16), m.intake, pod, f"Pod_{side}_Face")
    fan = pod @ T((0.0, 0.025, 0.0)) @ S(1.0, 0.7, 1.0)
    b.add(hg.lathe([(0.0, 0.92), (0.04, 0.96), (0.08, 1.06), (0.085, 1.15)], 24), m.ti, fan, f"Pod_{side}_Spinner")
    for q in range(16):
        ang = q / 16 * math.pi * 2
        b.add(hg.box(0.01, 0.2, 0.045), m.ti, fan @ T((math.sin(ang) * 0.19, math.cos(ang) * 0.19, 1.12)) @ R((0.0, 0.55, -ang)),
              f"Pod_{side}_Blade")
    # rejilla de refrigeración en el lomo de la góndola
    for q in range(4):
        b.add(hg.box(0.26, 0.014, 0.05), m.dark, pod @ T((0.0, 0.268, 1.86 + q * 0.11)) @ R((-0.35, 0.0, 0.0)), f"Pod_{side}_Vent")
    b.add(hg.box(0.30, 0.006, 0.47), m.alu, pod @ T((0.0, 0.268, 2.025)), f"Pod_{side}_VentFrame")
    # tobera bidimensional: boca aplanada de titanio con flaps, paredes laterales y actuadores
    b.add(hg.oct_loft(NOZ, 2.72, 3.20, 16), m.ti, pod, f"Nozzle_{side}")
    a, bb, c, _ = hg.sec_at(NOZ, 3.20)
    b.add(hg.oct_annulus(hg.oct_ring(a, bb, c), 3.20, hg.oct_ring(a - 0.03, bb - 0.03, c * 0.6), 3.20), m.ti, pod, f"Nozzle_{side}_Lip")
    b.add(hg.oct_loft(NOZ, 2.86, 3.20, 6, 0.0, 0.03), m.intake, pod, f"Nozzle_{side}_Inner")
    for s in (1, -1):
        flap = pod @ T((0.0, s * 0.15, 3.19)) @ R((s * 0.14, 0.0, 0.0))
        b.add(hg.box(0.56, 0.016, 0.24), m.ti, flap, f"Nozzle_{side}" + ("_FlapTop" if s > 0 else "_FlapBottom"))
        for q in range(-2, 3):
            b.add(hg.box(0.006, 0.02, 0.22), m.dark, flap @ T((q * 0.11, s * 0.002, 0.0)), "Nozzle_FlapGroove")
        b.add(hg.box(0.016, 0.26, 0.22), m.ti, pod @ T((s * 0.33, 0.0, 3.2)), f"Nozzle_{side}" + ("_WallR" if s > 0 else "_WallL"))
        b.add(g.tube([(s * 0.43, 0.0, 2.45), (s * 0.425, 0.0, 2.75), (s * 0.36, 0.0, 3.08)], 0.016, 8, 12), m.ti, pod,
              f"Nozzle_{side}_Actuator")
        b.add(g.cylinder(0.026, 0.026, 0.06, 12), m.alu, pod @ T((s * 0.43, 0.0, 2.5)), f"Nozzle_{side}_ActuatorBody")
    # fondo luminoso azul de la tobera y su núcleo
    piece = f"Nozzle_Glow_{side}"
    b.add(hg.rrect(0.62, 0.24, 0.06), m.glow(piece, NOZZLE_GLOW, 2.2), pod @ T((0.0, 0.0, 2.9)), piece, True)
    piece = f"Nozzle_Core_{side}"
    b.add(hg.rrect(0.36, 0.08, 0.04), m.glow(piece, NOZZLE_CORE, 3.5), pod @ T((0.0, 0.0, 2.95)), piece, True)


# ---------- addLivery(): solo la geometría; los dibujos los pone Godot ----------

def build_livery(b, m):
    b.zone = "Livery"
    for i, t in enumerate(sym(math.pi / 2 - 0.40, math.pi / 2 - 0.29)):
        for j, z in enumerate(hg.cut_canopy((-3.95, 2.6), True)):
            b.add(hg.fus_patch(t[0], t[1], z[0], z[1], N, 0.006), m.stripe,
                  name="Livery_TwinStripe_" + ("L" if i else "R") + ("_Rear" if j else "_Front"))
    # dorsal redondo en la nariz (se lee desde arriba con el morro hacia delante)
    b.add(hg.fus_patch(math.pi / 2 - 0.5, math.pi / 2 + 0.5, -2.98, -2.62, N, 0.008, lambda u, v: (1 - v, 1 - u)),
          m.decals["M_Decal_Number"], name="Decal_Number_Nose", no_shadow=True)
    # pegatinas de los flancos (en el lado izquierdo, la imagen se da la vuelta para leerse bien)
    for i, t in enumerate(sym(0.26, 0.78)):
        uv_fn = (lambda u, v: (u, 1 - v)) if i else (lambda u, v: (1 - u, v))
        b.add(hg.fus_patch(t[0], t[1], -1.75, -0.35, N, 0.008, uv_fn), m.decals["M_Decal_Sponsor"],
              name="Decal_Sponsor_" + ("L" if i else "R"), no_shadow=True)
    for side, k in SIDES:
        b.add(hg.wing(k, SPAN, 0.42, 0.6, 0.004), m.stripe, name=f"Livery_WingBand_{side}")
        b.add(hg.wing(k, SPAN, 0.63, 0.67, 0.004), m.pin, name=f"Livery_WingPin_{side}")
        # góndolas: dorsal y pegatina en la cara exterior, damero en el techo
        pod = T((k * 0.75, 0.1, 0.0))
        ax, rot = k * (0.42 + 0.006), k * math.pi / 2
        b.add(g.plane(0.34, 0.204), m.decals["M_Decal_Number"], pod @ T((ax, 0.0, 1.86)) @ R((0.0, rot, 0.0)),
              f"Decal_Number_Pod_{side}", True)
        b.add(g.plane(0.42, 0.105), m.decals["M_Decal_Sponsor"], pod @ T((ax, -0.02, 2.4)) @ R((0.0, rot, 0.0)),
              f"Decal_Sponsor_Pod_{side}", True)
        b.add(g.plane(0.56, 0.14), m.decals["M_Decal_Checker"], pod @ T((0.0, 0.275, 2.56)) @ R((-math.pi / 2, 0.0, 0.0)),
              f"Decal_Checker_{side}", True)


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
        m = piece.matrix
        # normales con la inversa traspuesta: el ventilador va escalado 1 × 0,7 × 1
        nm = np.linalg.inv(m[:3, :3]).T
        if key not in merged:
            merged[key] = (piece.material, g.Soup([], [], []))
        target = merged[key][1]
        target.positions += [g.transform_point(m, p) for p in piece.soup.positions]
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
    parents = {"Exhaust_L": zones["Pod_L"], "Exhaust_R": zones["Pod_R"], "Trail_L": zones["Wing_L"],
               "Trail_R": zones["Wing_R"], "Smoke": root}
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


def livery_objects(zones):
    return set(zones["Livery"].children_recursive)


# Comprobación de la spec (3.3), en coordenadas de Godot y sin librea
def check_axes(root, zones):
    body = [o for o in root.children_recursive if o not in livery_objects(zones)]
    low, high, _ = bounds(body)
    objs = bpy.data.objects

    def ob(name):
        return bounds([objs[name]])

    tip_low, _, _ = ob("Fuselage_Upper")
    can_low, can_high, _ = ob("Canopy_Glass")
    _, ep_high, _ = ob("Endplate_R")
    cowl_low, cowl_high, _ = ob("Pod_R_Cowl")
    lip_low, _, _ = ob("Pod_R_M_Alu")
    exhaust = godot_point(objs["Exhaust_R"], objs["Exhaust_R"].location * 0)
    half = max(-low[0], high[0])
    checks = [
        ("punta del morro z", tip_low[2], -4.00, 0.005),
        ("final de los flaps z", high[2], 3.31, 0.005),
        ("caja z mín", low[2], -4.00, 0.005),
        ("semiancho", half, 2.634, 0.005),
        ("caja y mín", low[1], -0.31, 0.005),
        ("caja y máx", high[1], 0.706, 0.005),
        ("cima de la cúpula y", can_high[1], 0.692, 0.015),
        ("arriba de las derivas y", ep_high[1], 0.316, 0.005),
        ("góndola x mín", cowl_low[0], 0.33, 0.005),
        ("góndola x máx", cowl_high[0], 1.17, 0.005),
        ("toma de aire z", lip_low[2], 0.80, 0.005),
        ("techo de la góndola y", cowl_high[1], 0.42, 0.005),
        ("boca de la tobera z", exhaust[2], 3.20, 0.001),
    ]
    bad = [f"{name} = {value:.4f} (debe ser {want} ± {tol})" for name, value, want, tol in checks if abs(value - want) > tol]
    if bad:
        raise RuntimeError("Medidas mal: " + "; ".join(bad))
    return checks, low, high


def r4(v):
    return [round(c, 4) for c in v]


def write_manifest(path, root, zones, materials, no_shadow, env_name):
    livery = livery_objects(zones)
    body = [o for o in root.children_recursive if o not in livery]
    low, high, tris = bounds(body)
    low_l, high_l, tris_l = bounds(root.children_recursive)
    objs = bpy.data.objects
    pieces = []
    for name in contract_names():
        obj = objs[name]
        entry = {"name": name}
        for prefix, (w, h) in DECAL_SIZES.items():
            if name.startswith(prefix):
                xs = [v.co.x for v in obj.data.vertices]
                ys = [v.co.y for v in obj.data.vertices]
                entry["width"] = round(max(xs) - min(xs), 4)
                entry["height"] = round(max(ys) - min(ys), 4)
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
        # buildEnv() del prototipo del casco es el de BLADE (misma semilla 77): se reutiliza su panorama
        "environment": env_name,
        "pieces": pieces,
        "empties": empties,
        "painted": PAINTED,
        "accents": ACCENTS + ACCENT_LIGHTS,
        "tailLights": TAIL_LIGHTS,
        "nozzleLights": NOZZLE_LIGHTS,
        "stripes": STRIPES,
        "pins": PINS,
        "decals": DECALS,
        "livery": sorted(o.name for o in livery),
        "noShadow": no_shadow,
        "coat": [{"material": k, "coat": c, "roughness": r} for k, (c, r) in COAT.items()],
        "roughnessFactors": [{"material": k, "factor": v} for k, v in ROUGHNESS_FACTOR.items()],
        "iridescence": [dict(material=k, **v) for k, v in IRIDESCENCE.items()],
        "reflections": [{"material": k, "factor": v} for k, v in REFLECTIONS.items()],
        "transparentMaterials": [{"material": k, "opacity": v} for k, v in TRANSPARENT.items()],
        "decalMaterials": DECAL_MATERIALS,
        "scorchMaterials": SCORCH_MATERIALS,
        "uv2Scale": UV2_SCALE,
        "triangles": tris,
        "trianglesWithLivery": tris_l,
        "bounds": {"min": r4(low), "max": r4(high)},
        "boundsWithLivery": {"min": r4(low_l), "max": r4(high_l)},
        "halfWidth": round(max(-low[0], high[0]), 4),
        "halfWidthWithLivery": round(max(-low_l[0], high_l[0]), 4),
    }
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")
    return manifest


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


def render_previews(scene, collection, root, zones, env_path, out):
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

    no_shadow = set()
    for obj in root.children_recursive:
        if obj.type == "MESH" and (obj.name.endswith("_NoShadow") or obj.name in NO_SHADOW_NAMES):
            no_shadow.add(obj)
            obj.visible_shadow = False

    livery = livery_objects(zones)
    folder = os.path.join(out, "previews")
    shots = [("tq", (-7.6, 3.0, -7.4), (0, 0.25, 0.2), 38), ("top", (0, 16.35, 0.25), (0, 0, 0.2), 34),
             ("side", (11, 0.4, -0.3), (0, 0.3, -0.3), 34), ("rear", (4, 1.8, 13.5), (0, 0.3, 0.5), 34)]
    for obj in livery:
        obj.hide_render = True
    for name, eye, target, fov in shots:
        io.render_preview(scene, collection, root, os.path.join(folder, f"hull_vector_{name}.png"), eye, target, fov)
    # Librea con la textura de prueba en las calcomanías: ninguna flecha girada ni en espejo
    sizes = {"Decal_Number_Nose": (512, 512), "Decal_Number_Pod": (640, 384), "Decal_Sponsor": (1024, 256),
             "Decal_Checker": (512, 128)}
    for obj in livery:
        obj.hide_render = False
        if obj.name in DECALS:
            key = next(k for k in sizes if obj.name.startswith(k))
            obj.data.materials[0] = test_decal_material(obj.name, *sizes[key])
    io.render_preview(scene, collection, root, os.path.join(folder, "hull_vector_livery.png"), *shots[0][1:])
    return aces or "Filmic"


NO_SHADOW_NAMES = set()


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    out = parse_out()
    os.makedirs(out, exist_ok=True)
    glb_path = os.path.join(ASSETS, "hull_vector.glb")
    manifest_path = os.path.join(ASSETS, "hull_vector.manifest.json")
    env_name = "cockpit_blade_env.png"
    env_path = os.path.join(COCKPIT_ASSETS, env_name)
    if not os.path.exists(env_path):
        raise RuntimeError(f"Falta el panorama de BLADE ({env_path}): ejecuta antes art/blender/run.ps1 blade")

    io.clear_file()
    scene = bpy.context.scene
    collection = scene.collection

    # Texturas sueltas aparte de las de las cabinas: algunas se llaman igual
    materials = Materials(os.path.join(out, "textures", "hull_vector"))
    builder = Builder()
    build_fuselage(builder, materials)
    build_canopy(builder, materials)
    for side, k in SIDES:
        build_side(builder, materials, side, k)
    build_livery(builder, materials)

    # Raíz girada +90° en X: un punto del prototipo (x, y, z) queda en Blender en (x, −z, y) y vuelve a ser (x, y, z) en glTF
    root = io.empty("HullVector", None, collection, rotation=(math.pi / 2, 0.0, 0.0))
    zones = {zone: io.empty(zone, root, collection) for zone in ZONES}
    no_shadow = create_objects(builder, zones, collection)
    NO_SHADOW_NAMES.update(no_shadow)
    create_empties(zones, root, collection)
    bpy.context.view_layer.update()

    check_contract(root)
    checks, low, high = check_axes(root, zones)
    manifest = write_manifest(manifest_path, root, zones, materials, no_shadow, env_name)
    io.export_glb(glb_path, root)

    preview_collection = bpy.data.collections.new("Preview")
    scene.collection.children.link(preview_collection)
    view = render_previews(scene, preview_collection, root, zones, env_path, out)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "hull_vector.blend"))

    print(f"[Hull] Blender {bpy.app.version_string} · capturas con {scene.render.engine} y vista {view} · panorama {env_name}")
    for name, value, want, tol in checks:
        print(f"[Hull] {name}: {value:.4f} (spec {want} ± {tol})")
    print(f"[Hull] Caja sin librea {r4(low)} → {r4(high)} · largo {high[2] - low[2]:.3f} · alto {high[1] - low[1]:.3f}")
    print(f"[Hull] Triángulos {manifest['triangles']} sin librea · {manifest['trianglesWithLivery']} con librea")
    print(f"[Hull] GLB {os.path.getsize(glb_path)} bytes · sha256 {sha256(glb_path)}")


main()
