# Casco 3D de HAMMER, estilo INDUSTRIAL, variante MINERO (spec docs/cascos-3d-3-hammer.md). Traducción de buildHull()
# con VAR = 1 y LIV = true de designs/casco-3d-hammer.html, con sus marcas de flota (la geometría; los dibujos los pone
# Godot): mismos números y mismo orden.
#
# Uso: blender -b --factory-startup --python art/blender/hull_hammer.py -- --out art/blender/out
# El .blend se regenera cada vez: todo vive en este script.
import datetime
import hashlib
import json
import math
import os
import re
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
from lib import hull_hammer_geometry as hh  # noqa: E402
from lib import textures_hull as tx  # noqa: E402
from lib import textures_hull_hammer as th  # noqa: E402
from lib import textures_industrial as ti  # noqa: E402
from lib.builder import Builder  # noqa: E402

REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "assets", "ships")
COCKPIT_ASSETS = os.path.join(REPO, "assets", "cockpits")

# Pintura por defecto: AMARILLO OBRA (casco y cabina), la que sale al abrir el prototipo
SITE_YELLOW = (170, 132, 40)

# Colores del prototipo (COL)
AMBER = 0xFF9A1A
RED = 0xFF2A10
TAIL = 0xFF2A1E
WORK = 0xFFF2DA
HEAT = 0xFF5A14
HEAT_CORE = 0xFFB46A
BEACON_BASE = 0x1A0D04
BEACON_OFF = 0.05   # balizas del techo apagadas (en reposo)

# Repetición de cada textura: se multiplica en las UV, así no hace falta KHR_texture_transform
REPEAT = {"M_Paint": 0.5, "M_Paint_Canard": 0.5, "M_Paint_Fin": 0.5, "M_Paint_Bay": 0.8, "M_PaintCab": 0.8,
          "M_PlateDark": 0.5, "M_PlateDark_Canard": 0.5, "M_PlateDark_Bay": 0.8,
          "M_Steel": 1.5, "M_Titanium": 2.0, "M_Copper": 2.0, "M_Tread": 2.0, "M_Hazard": 1.2}
# Materiales con quemaduras: llevan la segunda capa de UV (metros sin repetición × 1,1)
SCORCH_MATERIALS = list(REPEAT)
UV2_SCALE = 1.1

# Lo que glTF no lleva o Godot no lee, para que lo aplique el casco al cargar
COAT = {"M_CabGlass": (1.0, 0.0)}
REFLECTIONS = {"M_Paint": 0.6, "M_Paint_Canard": 0.6, "M_Paint_Fin": 0.6, "M_Paint_Bay": 0.6, "M_PaintCab": 0.6,
               "M_CabGlass": 1.6}
TRANSPARENT = {"M_CabGlass": 0.55}
DECAL_MATERIALS = ["M_Decal_Unit", "M_Decal_Logo", "M_Decal_NoStep", "M_Decal_Hot", "M_Decal_Stencil"]
# Pintura como máscara (sección 3.5): color = pintura × (MUL × 1,25) + ADD. Godot pone el color de cada grupo.
PAINT_GROUPS = {
    "body": {"seed": 87, "mul": "hull_hammer_body_mul.png", "add": "hull_hammer_body_add.png",
             "materials": ["M_Paint", "M_Paint_Canard", "M_Paint_Fin", "M_Paint_Bay"]},
    "cab": {"seed": 89, "mul": "hull_hammer_cab_mul.png", "add": "hull_hammer_cab_add.png",
            "materials": ["M_PaintCab"]},
}

SIDES = (("L", -1), ("R", 1))
ZONES = ["Fuselage", "Canard_L", "Canard_R", "Fin_L", "Fin_R", "Engine_L", "Engine_R", "Cab", "Extras", "EngineBay",
         "Markings"]
# Grupos dentro de la cabina (balizas y focos)
SUBZONES = {"Beacon_Boost": "Cab", "Beacon_Damage": "Cab", "Work_0": "Cab", "Work_1": "Cab", "Work_2": "Cab",
            "Work_3": "Cab"}

PAINTED_BODY = ["Fuselage_Upper", "Canard_L_Top", "Canard_R_Top", "Canard_L_Droop", "Canard_R_Droop", "Fin_L_Plate",
                "Fin_R_Plate", "EngineBay_Body"]
PAINTED_CAB = ["Cab_Body"]
LEADING_EDGES = ["Canard_L_LeadingEdge", "Canard_R_LeadingEdge"]
LIVERY_HAZARDS = ["Fin_L_Hazard", "Fin_R_Hazard", "EngineBay_Hazard"]
AMBER_LIGHTS = ["Light_Nose_L", "Light_Nose_R", "Light_Canard_L", "Light_Canard_R"]
TAIL_LIGHTS = ["Light_Tail_L", "Light_Tail_R", "Light_TailBar_L", "Light_TailBar_R"]
WORK_LIGHTS = [f"Light_Work_{i}" for i in range(4)]
BEACONS = ["Beacon_Boost_Lens", "Beacon_Damage_Lens"]
NOZZLE_LIGHTS = ["Nozzle_Glow_L", "Nozzle_Glow_R", "Nozzle_Core_L", "Nozzle_Core_R"]
GLASS = ["Cab_Windshield", "Cab_SideWindow_L", "Cab_SideWindow_R"]
DECALS = ["Decal_Unit_L", "Decal_Unit_R", "Decal_Logo_L", "Decal_Logo_R", "Decal_NoStep_L", "Decal_NoStep_R",
          "Decal_Hot_L", "Decal_Hot_R", "Decal_Cab_L", "Decal_Cab_R"]
# Luces que titilan con el daño (6.5): las balizas del techo y las toberas no
FLICKER_LIGHTS = AMBER_LIGHTS + TAIL_LIGHTS + WORK_LIGHTS
EMPTIES = {"Exhaust_L": (-0.45, 0.20, 3.00), "Exhaust_R": (0.45, 0.20, 3.00),
           "Trail_L": (-2.50, -0.50, -2.20), "Trail_R": (2.50, -0.50, -2.20), "Smoke": (0.40, 1.33, -0.06)}
# Extras del techo, como ROOF_EXTRAS del prototipo (por el nombre de la pieza o de su grupo)
ROOF_EXTRAS = re.compile(r"^(Beacon_|Work_|Stack|Mast|RollBar_|Light_Mast|Cab_RoofRail)")


def contract_names():
    return (PAINTED_BODY + PAINTED_CAB + LEADING_EDGES + LIVERY_HAZARDS + AMBER_LIGHTS + TAIL_LIGHTS + WORK_LIGHTS
            + BEACONS + NOZZLE_LIGHTS + GLASS + DECALS)


def parse_out():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--out" in args:
        path = args[args.index("--out") + 1]
        return path if os.path.isabs(path) else os.path.join(REPO, path)
    return os.path.join(HERE, "out")


# ---------- Materiales ----------

# Principled BSDF con texturas de imagen: color, rugosidad y normal
def pbr(name, maps, metallic=0.0, normal_strength=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_backface_culling = False
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Metallic"].default_value = metallic
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(maps["color"], check_existing=True)
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    rough = nodes.new("ShaderNodeTexImage")
    rough.image = bpy.data.images.load(maps["roughness"], check_existing=True)
    rough.image.colorspace_settings.name = "Non-Color"
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
        body = io.save_texture_set(texture_folder, "PaintBody", th.paint_surface(87))
        cab = io.save_texture_set(texture_folder, "PaintCab", th.paint_surface(89))
        dark = io.save_texture_set(texture_folder, "PlateDark", ti.plate_set(82, (50, 52, 48)))
        steel = io.save_texture_set(texture_folder, "Steel", ti.steel_set(83))
        tread = io.save_texture_set(texture_folder, "Tread", ti.tread_set(84))
        hazard = io.save_texture_set(texture_folder, "Hazard", ti.hazard_set(85))
        ti_maps = io.save_texture_set(texture_folder, "Titanium", tx.brushed_set(32, (150, 148, 146), True))
        copper = io.save_texture_set(texture_folder, "Copper", tx.brushed_set(33, (170, 100, 60), False))

        # La pintura es una máscara: en el modelo, MUL como color; Godot reconstruye el de la pintura
        self.paint = pbr("M_Paint", body, 0.35, 0.8)
        self.paint_canard = pbr("M_Paint_Canard", body, 0.35, 0.8)
        self.paint_fin = pbr("M_Paint_Fin", body, 0.35, 0.8)
        self.paint_bay = pbr("M_Paint_Bay", body, 0.35, 0.8)
        self.paint_cab = pbr("M_PaintCab", cab, 0.35, 0.8)
        self.dark = pbr("M_PlateDark", dark, 0.55, 0.8)
        self.dark_canard = pbr("M_PlateDark_Canard", dark, 0.55, 0.8)
        self.dark_bay = pbr("M_PlateDark_Bay", dark, 0.55, 0.8)
        self.steel = pbr("M_Steel", steel, 1.0)
        self.ti = pbr("M_Titanium", ti_maps, 1.0)
        self.copper = pbr("M_Copper", copper, 1.0)
        self.tread = pbr("M_Tread", tread, 0.9)
        self.hazard = pbr("M_Hazard", hazard, 0.3)
        self.rubber = io.material("M_Rubber", base=io.hex_color(0x141414), metallic=0.0, roughness=0.78)
        self.black = io.material("M_BlackPaint", base=io.hex_color(0x17171A), metallic=0.35, roughness=0.5)
        self.mdark = io.material("M_Dark", base=io.hex_color(0x0B0B0A), metallic=0.2, roughness=0.8)
        self.intake = io.material("M_Intake", base=io.hex_color(0x050506), metallic=0.1, roughness=0.9)
        self.cab_inner = io.material("M_CabInterior", base=io.hex_color(0x07080A), metallic=0.0, roughness=0.9)
        self.glass = io.material("M_CabGlass", base=io.hex_color(0x0A0D10), metallic=0.1, roughness=0.05,
                                 coat=1.0, coat_roughness=0.0, alpha=0.55)
        self.beacon_base = io.material("M_BeaconBase", base=io.hex_color(0x202020), metallic=0.5, roughness=0.6)
        self.beacon_cage = io.material("M_BeaconCage", base=io.hex_color(0x8A8882), metallic=1.0, roughness=0.35)
        # Calcomanías: negras y opacas en el modelo; Godot pone su dibujo con alfa
        self.decals = {n: io.material(n, base=(0, 0, 0, 1), roughness=0.6) for n in DECAL_MATERIALS}
        self.emissive = {}  # nombre de la pieza → (color, intensidad)

    # Cada luz viva con su propio material, para que Godot la encienda, la apague o le cambie el color sola
    def glow(self, piece, color, intensity, prefix="M_Emissive_", base=None):
        self.emissive[piece] = (color, intensity)
        return io.material(f"{prefix}{piece}", base=io.hex_color(base) if base is not None else (0, 0, 0, 1),
                           roughness=0.4, emission=io.hex_color(color), emission_strength=intensity)


# ---------- Matrices ----------

T = g.translation
R = g.euler_xyz
PI = math.pi


def rot_x(soup, angle):
    return g.transformed(soup, R((angle, 0.0, 0.0)))


# ---------- buildHull() ----------

def build_fuselage(b, m):
    b.zone = "Fuselage"
    b.add(hh.fus_patch(0, PI, hh.Z0, hh.Z1), m.paint, name="Fuselage_Upper")
    b.add(hh.fus_patch(PI, 2 * PI, hh.Z0, hh.Z1), m.dark, name="Fuselage_Lower")
    b.add(hh.fus_cap(hh.Z1), m.mdark, name="Fuselage_Rear")
    b.add(hh.fus_cap(hh.Z0), m.dark, name="Fuselage_Front")
    # banda de chapa lagrimada en el lomo del morro (la del morro de la cabina 3D)
    b.add(hh.skin_grid(-3.35, -1.92, lambda z: (lambda w: (-w, w))(hh.prof(z)["hw"] * 0.32), 0.004, 14, 6),
          m.tread, name="Nose_Tread")
    # juntas de panel: transversales y longitudinales
    for i, pts in enumerate(hh.seam_lines()):
        b.add(hh.skin_ribbon(pts, 0.012, 0.0015), m.mdark, name=f"Seam_{i}", no_shadow=True)
    # balizas ámbar en los hombros del morro (las mismas que ve el piloto)
    for side, k in SIDES:
        z = -2.4
        p = hh.prof(z)
        piece = f"Light_Nose_{side}"
        b.add(g.sphere(0.03, 12, 8), m.glow(piece, AMBER, 2.4), T((k * (p["hw"] + 0.006), p["side"] + 0.03, z)),
              piece, True)
    # patines de acero bajo la panza
    for side, k in SIDES:
        b.add(hh.rbox(0.09, 0.04, 2.9, 0.01), m.steel, T((k * 0.36, -0.32, 0.25)), f"Skid_{side}")
    # pilotos traseros en la tapa
    for side, k in SIDES:
        piece = f"Light_TailBar_{side}"
        b.add(hg.box(0.14, 0.04, 0.012), m.glow(piece, TAIL, 2.2), T((k * 0.74, 0.34, hh.Z1 + 0.006)), piece, True)


def build_canard(b, m, side, k):
    b.zone = "Canard_" + side
    geos = hh.canard_geos(k)
    b.add(geos["top"], m.paint_canard, name=f"Canard_{side}_Top")
    b.add(geos["bot"], m.dark_canard, name=f"Canard_{side}_Bottom")
    # con marcas, franjas; sin marcas, chapa oscura (lo cambia Godot)
    b.add(geos["le"], m.hazard, name=f"Canard_{side}_LeadingEdge")
    b.add(geos["te"], m.dark_canard, name=f"Canard_{side}_TrailingEdge")
    b.add(geos["tip"], m.dark_canard, name=f"Canard_{side}_TipEnd")
    b.add(hh.slab(hh.DROOP, (k * 2.465, 0.0, 0.0), (0, 0, 1), (0, 1, 0), 0.05, 0.006), m.paint_canard,
          name=f"Canard_{side}_Droop")
    b.add(hh.rbox(0.07, 0.03, 0.30, 0.008), m.steel, T((k * 2.465, -0.505, -2.20)), f"Canard_{side}_Skid")
    # baliza ámbar en el borde de la punta caída (la que ve el piloto)
    piece = f"Light_Canard_{side}"
    b.add(g.sphere(0.032, 12, 8), m.glow(piece, AMBER, 2.4), T((k * 2.47, -0.24, -2.50)), piece, True)
    # costilla atornillada en la raíz
    a = hh.can_pt(k, 0.16, 0.05, 1)
    c = hh.can_pt(k, 0.16, 0.95, 1)
    soup, mtx = hh.beam((a[0], a[1] + 0.006, a[2]), (c[0], c[1] + 0.006, c[2]), 0.035, 0.012, (k * 3, 5, -2.3))
    b.add(soup, m.steel, mtx, f"Canard_{side}_Rib")


def build_fin(b, m, side, k):
    b.zone = "Fin_" + side
    origin, v = (k * 0.85, 0.3, 1.0), (k * 0.6, 0.8, 0.0)
    b.add(hh.slab(hh.FIN, origin, (0, 0, 1), v, 0.05, 0.008), m.paint_fin, name=f"Fin_{side}_Plate")
    # franja de peligro arriba: solo con marcas
    b.add(hh.slab(hh.FIN_HAZARD, origin, (0, 0, 1), v, 0.058, 0.004), m.hazard, name=f"Fin_{side}_Hazard")
    # piloto rojo arriba y atrás
    piece = f"Light_Tail_{side}"
    b.add(g.sphere(0.028, 12, 8), m.glow(piece, TAIL, 2.4), T((k * (0.85 + 0.6 * 0.74), 0.3 + 0.8 * 0.74, 2.38)),
          piece, True)


def build_engine(b, m, side, k):
    b.zone = "Engine_" + side
    eg = T((k * 0.45, 0.2, 0.0))
    b.add(hg.lathe([(0.40, 1.70), (0.40, 2.58)], 40), m.ti, eg, f"Engine_{side}_Barrel")
    b.add(hg.lathe([(0.40, 2.20), (0.435, 2.21), (0.435, 2.27), (0.40, 2.28)], 40), m.steel, eg, f"Engine_{side}_Collar")
    for i, z in enumerate((2.34, 2.42, 2.50)):
        b.add(hg.lathe([(0.40, z - 0.012), (0.47, z - 0.006), (0.47, z + 0.006), (0.40, z + 0.012)], 40), m.steel, eg,
              f"Engine_{side}_CoolRing_{i}")
    b.add(hg.lathe([(0.40, 2.58), (0.385, 2.62), (0.30, 2.96), (0.29, 3.0), (0.25, 3.0), (0.205, 2.82)], 40), m.ti, eg,
          f"Nozzle_{side}")
    # fondo luminoso naranja (los de VECTOR y BLADE son azules)
    piece = f"Nozzle_Glow_{side}"
    b.add(g.circle(0.205, 32), m.glow(piece, HEAT, 2.2), eg @ T((0.0, 0.0, 2.821)), piece, True)
    piece = f"Nozzle_Core_{side}"
    b.add(g.circle(0.10, 24), m.glow(piece, HEAT_CORE, 3.5), eg @ T((0.0, 0.0, 2.83)), piece, True)


def build_cab(b, m):
    b.zone = "Cab"
    b.add(hh.profile_extrude(hh.CABP, hh.CAB_W, hh.CAB_B), m.paint_cab, name="Cab_Body")
    # parabrisas: fondo oscuro, cristal y junta de goma
    o = hh.CAB_B

    def quad(off):
        return [hh.ws_pt(-0.50, 0.10, o + off), hh.ws_pt(0.50, 0.10, o + off), hh.ws_pt(0.47, 0.91, o + off),
                hh.ws_pt(-0.47, 0.91, o + off)]

    b.add(hh.quad_geo(quad(0.002)), m.cab_inner, name="Cab_WindshieldBack", no_shadow=True)
    b.add(hh.quad_geo(quad(0.005)), m.glass, name="Cab_Windshield", no_shadow=True)
    b.add(hh.poly_tube(quad(0.004), 0.012, True, 8), m.rubber, name="Cab_WindshieldSeal")
    # limpiaparabrisas aparcado abajo
    b.add(hh.cyl(0.016, 0.018, 0.03, 12), m.steel, T(hh.ws_pt(0.16, 0.06, o + 0.012)) @ R((-0.93, 0.0, 0.0)),
          "Wiper_Pivot")
    soup, mtx = hh.beam(hh.ws_pt(0.16, 0.06, o + 0.022), hh.ws_pt(-0.36, 0.09, o + 0.022), 0.012, 0.012, (0, 3, -3))
    b.add(soup, m.steel, mtx, "Wiper_Arm")
    b.add(g.tube([hh.ws_pt(0.08, 0.08, o + 0.016), hh.ws_pt(-0.15, 0.085, o + 0.016), hh.ws_pt(-0.40, 0.09, o + 0.016)],
                 0.006, 12, 12), m.rubber, name="Wiper_Blade")
    roof = hh.ROOF
    for side, k in SIDES:  # ventanas laterales
        x = k * hh.CAB_W
        b.add(hh.side_geo(hh.SIDEWIN), m.cab_inner, T((x + k * 0.002, 0.0, 0.0)), f"Cab_SideBack_{side}", True)
        b.add(hh.side_geo(hh.SIDEWIN), m.glass, T((x + k * 0.005, 0.0, 0.0)), f"Cab_SideWindow_{side}", True)
        b.add(hh.poly_tube([(x + k * 0.004, y, z) for z, y in hh.SIDEWIN], 0.011, True, 8), m.rubber,
              name=f"Cab_SideSeal_{side}")
        # asidero y barandilla del techo
        b.add(g.tube([(k * 0.54, roof, -1.36), (k * 0.54, roof + 0.06, -1.30), (k * 0.54, roof + 0.06, -0.60),
                      (k * 0.54, roof, -0.54)], 0.013, 24, 12), m.steel, name=f"Cab_RoofRail_{side}")
        b.add(g.tube([(x + k * 0.01, 0.92, -0.50), (x + k * 0.05, 0.90, -0.46), (x + k * 0.05, 0.70, -0.46),
                      (x + k * 0.01, 0.68, -0.50)], 0.011, 16, 12), m.hazard, name=f"Cab_Grab_{side}")
    # rótulo a los dos lados: solo con marcas
    for side, k in SIDES:
        b.add(g.plane(0.56, 0.13), m.decals["M_Decal_Stencil"],
              T((k * (hh.CAB_W + 0.004), 0.69, -1.18)) @ R((0.0, k * PI / 2, 0.0)), f"Decal_Cab_{side}", True)
    # balizas con jaula: ámbar (boost) a la izquierda y roja (daño) a la derecha, como en la cabina 3D
    build_beacon(b, m, "Beacon_Boost", (-0.40, roof, -1.30), AMBER)
    build_beacon(b, m, "Beacon_Damage", (0.40, roof, -1.30), RED)
    # focos de trabajo
    for i, x in enumerate((-0.24, -0.08, 0.08, 0.24)):
        build_work_light(b, m, f"Work_{i}", (x, roof, -1.44))
    b.zone = "Cab"


def build_beacon(b, m, name, pos, color):
    b.zone = name
    grp = T(pos)
    b.add(hh.rbox(0.09, 0.025, 0.09, 0.006), m.beacon_base, grp @ T((0.0, 0.0125, 0.0)), name + "_Base")
    lens = name + "_Lens"
    b.add(g.sphere(0.035, 14, 10), m.glow(lens, color, BEACON_OFF, "M_Beacon_", BEACON_BASE), grp @ T((0.0, 0.06, 0.0)),
          lens, True)
    # medios toros sueltos: en el prototipo no proyectan sombra
    for a in range(4):
        b.add(g.torus(0.042, 0.003, 6, 16, PI), m.beacon_cage, grp @ T((0.0, 0.06, 0.0)) @ R((0.0, a * PI / 4, 0.0)),
              name + "_Cage", True)


def build_work_light(b, m, name, pos):
    b.zone = name
    grp = T(pos)
    b.add(hh.rbox(0.13, 0.075, 0.07, 0.01), m.black, grp @ T((0.0, 0.05, 0.0)), name + "_Housing")
    b.add(hh.rbox(0.02, 0.05, 0.02, 0.004), m.steel, grp @ T((0.0, 0.01, 0.0)), name + "_Mount")
    piece = "Light_" + name
    b.add(g.plane(0.11, 0.055), m.glow(piece, WORK, 2.0), grp @ T((0.0, 0.05, -0.0365)), piece, True)


def build_extras(b, m):
    b.zone = "Extras"
    # defensa tubular delante del morro
    up = [(-0.52, 0.14, -3.0), (-0.48, 0.14, -3.6), (-0.3, 0.14, -3.8), (0.3, 0.14, -3.8), (0.48, 0.14, -3.6),
          (0.52, 0.14, -3.0)]
    b.add(hh.poly_tube(up, 0.026), m.black, name="BullBar_Top")
    b.add(hh.poly_tube([(x, -0.08, z) for x, _, z in up], 0.026), m.black, name="BullBar_Bottom")
    for i, x in enumerate((-0.3, 0.0, 0.3)):
        b.add(hh.poly_tube([(x, -0.08, -3.8), (x, 0.14, -3.8)], 0.022), m.black, name=f"BullBar_Post_{i}")
    for side, k in SIDES:
        b.add(hh.poly_tube([(k * 0.48, -0.08, -3.6), (k * 0.48, 0.14, -3.6)], 0.022), m.black, name=f"BullBar_Side_{side}")
    # rejilla sobre el parabrisas
    o = hh.CAB_B + 0.05
    for i, x in enumerate((-0.25, 0.0, 0.25)):
        b.add(hh.poly_tube([hh.ws_pt(x, 0.04, o), hh.ws_pt(x, 0.97, o)], 0.016), m.black, name=f"Guard_Bar_{i}")
    for i, f in enumerate((0.04, 0.97)):
        b.add(hh.poly_tube([hh.ws_pt(-0.52, f, o), hh.ws_pt(0.52, f, o)], 0.018), m.black, name=f"Guard_Cross_{i}")
    for _, k in SIDES:
        for f in (0.04, 0.97):
            b.add(hh.poly_tube([hh.ws_pt(k * 0.52, f, hh.CAB_B), hh.ws_pt(k * 0.52, f, o)], 0.014), m.black,
                  name="Guard_Standoff")
    # arcos antivuelco
    for i, z in enumerate((-1.24, -0.60)):
        b.add(hh.poly_tube([(-0.64, 0.62, z), (-0.64, 1.10, z), (-0.52, 1.17, z), (0.52, 1.17, z), (0.64, 1.10, z),
                            (0.64, 0.62, z)], 0.026), m.black, name=f"RollBar_{i}")
    # escape vertical detrás de la cabina
    x, z = 0.40, -0.06
    y0 = hh.fus_top_y(x, z) - 0.02
    b.add(hh.cyl(0.055, 0.055, 1.30 - y0, 16), m.steel, T((x, (y0 + 1.30) / 2, z)), "Stack")
    for i, y in enumerate((0.85, 1.10)):
        b.add(hh.cyl(0.064, 0.064, 0.03, 16), m.black, T((x, y, z)), f"Stack_Clamp_{i}")
    b.add(hh.cyl(0.062, 0.062, 0.008, 16), m.steel, T((x, 1.31, z + 0.03)) @ R((0.7, 0.0, 0.0)), "Stack_Cap")
    b.add(g.circle(0.05, 16), m.intake, T((x, 1.301, z)) @ R((-PI / 2, 0.0, 0.0)), "Stack_Mouth")


def build_bay(b, m):
    b.zone = "EngineBay"
    top = hh.BAY_TOP
    b.add(hh.profile_extrude(hh.BAYP, hh.BAY_W, hh.BAY_B), m.paint_bay, name="EngineBay_Body")
    # tapa de registro atornillada en el techo
    b.add(hh.rbox(0.56, 0.02, 0.62, 0.006), m.dark_bay, T((0.0, top + 0.005, 1.05)), "EngineBay_Hatch")
    for x, z in ((-0.25, 0.78), (0.25, 0.78), (-0.25, 1.32), (0.25, 1.32)):
        b.add(hh.cyl(0.014, 0.014, 0.012, 6), m.steel, T((x, top + 0.018, z)), "EngineBay_HatchBolt", True)
    # rejilla de admisión detrás de la tapa
    b.add(rot_x(g.plane(0.56, 0.34), -PI / 2), m.intake, T((0.0, top + 0.002, 1.58)), "EngineBay_IntakeBack", True)
    for i in range(7):
        b.add(hh.rbox(0.56, 0.012, 0.022, 0.003), m.steel, T((0.0, top + 0.01, 1.43 + i * 0.05)), "EngineBay_IntakeSlat")
    # lamas de ventilación en los costados
    for side, k in SIDES:
        x = k * (hh.BAY_W + 0.002)
        b.add(g.plane(0.62, 0.16), m.intake, T((x, 0.74, 1.30)) @ R((0.0, k * PI / 2, 0.0)), f"EngineBay_Vent_{side}",
              True)
        for i in range(6):
            b.add(hh.rbox(0.02, 0.012, 0.60, 0.003), m.dark_bay,
                  T((x + k * 0.008, 0.68 + i * 0.024, 1.30)) @ R((0.0, 0.0, k * 0.6)), "EngineBay_VentSlat")
        for z, y in ((0.98, 0.84), (1.62, 0.84), (0.98, 0.64), (1.62, 0.64)):
            b.add(hh.cyl(0.011, 0.011, 0.01, 6), m.steel, T((x + k * 0.004, y, z)) @ R((0.0, 0.0, PI / 2)),
                  "EngineBay_Bolt", True)
    # franja de peligro del frente: solo con marcas
    b.add(g.plane(0.80, 0.05), m.hazard, T((0.0, 0.64, 0.55 - hh.BAY_B - 0.002)) @ R((0.0, PI, 0.0)),
          "EngineBay_Hazard", True)


def build_side_guards_ladders_pipes(b, m):
    b.zone = "Extras"
    # rejas discretas en las ventanas laterales
    for side, k in SIDES:
        x, xs = k * (hh.CAB_W + 0.035), k * hh.CAB_W
        for i, z in enumerate((-1.44, -0.92)):
            b.add(hh.poly_tube([(x, 0.75, z), (x, 0.99, z)], 0.011), m.black, name=f"SideGuard_{side}_Bar_{i}")
        b.add(hh.poly_tube([(x, 0.87, -1.50), (x, 0.87, -0.66)], 0.011), m.black, name=f"SideGuard_{side}_Cross")
        for y, z in ((0.87, -1.50), (0.87, -0.66)):
            b.add(hh.poly_tube([(xs, y, z), (x, y, z)], 0.009), m.black, name="SideGuard_Standoff")
    # escalerilla de mano cortita a cada lado, del costado a la base de la cabina
    for side, k in SIDES:
        z0 = -0.92
        b0, t0 = (k * 0.97, -0.04), (k * 0.80, 0.70)
        for i, dz in enumerate((-0.11, 0.11)):
            b.add(hh.poly_tube([(b0[0], b0[1], z0 + dz), (t0[0], t0[1], z0 + dz), (k * 0.70, 0.78, z0 + dz)], 0.012),
                  m.black, name=f"Ladder_{side}_Rail_{i}")
        for i in range(4):
            t = 0.1 + i * 0.26
            p = hh.lerp3((b0[0], b0[1], 0.0), (t0[0], t0[1], 0.0), t)
            b.add(hh.poly_tube([(p[0], p[1], z0 - 0.11), (p[0], p[1], z0 + 0.11)], 0.009), m.steel,
                  name=f"Ladder_{side}_Rung_{i}")
        for y in (0.15, 0.62):
            t = (y - b0[1]) / (t0[1] - b0[1])
            x = b0[0] + (t0[0] - b0[0]) * t
            b.add(hh.poly_tube([(x, y, z0), (k * (hh.prof(z0)["hw"] - 0.02), min(y, 0.3), z0)], 0.01), m.black,
                  name=f"Ladder_{side}_Bracket")
    # tuberías de cobre: de la cabina al compartimento y de este a los motores
    for side, k in SIDES:
        x0 = k * 0.30
        b.add(g.tube([(x0, hh.fus_top_y(0.30, -0.12) + 0.04, -0.12), (k * 0.31, hh.fus_top_y(0.31, 0.25) + 0.05, 0.25),
                      (k * 0.32, 0.70, 0.56)], 0.034, 24, 12), m.copper, name=f"Pipe_Front_{side}")
        b.add(g.tube([(k * 0.32, 0.74, 1.89), (k * 0.38, 0.71, 2.05), (k * 0.45, 0.665, 2.24)], 0.034, 20, 12), m.copper,
              name=f"Pipe_Rear_{side}")
        # abrazadera suelta: en el prototipo no proyecta sombra
        b.add(g.torus(0.04, 0.008, 6, 16), m.steel, T((k * 0.305, hh.fus_top_y(0.305, 0.15) + 0.05, 0.15)),
              "Pipe_Clamp", True)


# ---------- Marcas de flota: solo la geometría; los dibujos los pone Godot ----------

def build_markings(b, m):
    b.zone = "Markings"
    for side, k in SIDES:
        # número de unidad en la cara exterior de cada aleta (se lee desde el costado con el morro delante)
        x_axis = (0.0, 0.0, -k)
        y_axis = g._normalize((k * 0.6, 0.8, 0.0))
        z_axis = g._cross(x_axis, y_axis)
        pos = g._add((k * (0.85 + 0.6 * 0.28), 0.3 + 0.8 * 0.28, 1.0 + 0.95), g._scale(z_axis, 0.025 + 0.012))
        b.add(g.plane(0.44, 0.34), m.decals["M_Decal_Unit"], g.from_basis(x_axis, y_axis, z_axis, pos),
              f"Decal_Unit_{side}", True)
        # logotipo y NO STEP encima del canard (se leen desde arriba con el morro arriba)
        for material, sp, tp, w, h, name in (("M_Decal_Logo", 0.42, 0.5, 0.46, 0.46, "Decal_Logo_"),
                                             ("M_Decal_NoStep", 0.80, 0.5, 0.44, 0.14, "Decal_NoStep_")):
            p = hh.can_pt(k, sp, tp, 1)
            b.add(rot_x(g.plane(w, h), -PI / 2), m.decals[material],
                  T((p[0], p[1] + 0.005, p[2])) @ R((0.0, 0.0, -k * 0.06)), name + side, True)
        # HOT detrás de las lamas del compartimento del motor
        b.add(g.plane(0.17, 0.055), m.decals["M_Decal_Hot"],
              T((k * (hh.BAY_W + 0.004), 0.70, 1.72)) @ R((0.0, k * PI / 2, 0.0)), f"Decal_Hot_{side}", True)


# Piezas que solo se ven con marcas (no todas están en el grupo Markings)
def livery_only(builder):
    return sorted({p.name for p in builder.pieces if p.zone == "Markings"} | set(LIVERY_HAZARDS)
                  | {"Decal_Cab_L", "Decal_Cab_R"})


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
    parents = {"Exhaust_L": zones["Engine_L"], "Exhaust_R": zones["Engine_R"], "Trail_L": zones["Canard_L"],
               "Trail_R": zones["Canard_R"], "Smoke": root}
    for name, pos in EMPTIES.items():
        obj = io.empty(name, parents[name], collection)
        obj.matrix_basis = Matrix([list(r) for r in T(pos)])


def check_contract(root):
    present = {o.name for o in root.children_recursive}
    missing = [n for n in contract_names() + list(EMPTIES) + list(SUBZONES) if n not in present]
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


# Caja sin los extras del techo, desde las piezas (antes de juntarlas): coordenadas del prototipo = las de Godot
def bounds_without_roof_extras(builder):
    low = [float("inf")] * 3
    high = [float("-inf")] * 3
    for piece in builder.pieces:
        if ROOF_EXTRAS.match(piece.name or "") or ROOF_EXTRAS.match(piece.zone):
            continue
        for p in piece.soup.positions:
            q = g.transform_point(piece.matrix, p)
            for i in range(3):
                low[i] = min(low[i], q[i])
                high[i] = max(high[i], q[i])
    return low, high


# Comprobación de la spec (3.3), en coordenadas de Godot y con marcas
def check_axes(root, builder):
    objs = bpy.data.objects
    low, high, _ = bounds(list(root.children_recursive))
    roof_low, roof_high = bounds_without_roof_extras(builder)
    front_low, _, _ = bounds([objs["Fuselage_Upper"]])
    _, nozzle_high, _ = bounds([objs["Engine_L_M_Titanium"]])
    _, cab_high, _ = bounds([objs["Cab_Body"]])
    _, bay_high, _ = bounds([objs["EngineBay_Body"]])
    half = max(-low[0], high[0])
    checks = [
        ("cara roma del morro z", front_low[2], -3.60, 0.005),
        ("delante de la defensa z", low[2], -3.829, 0.005),
        ("boca de las toberas z", nozzle_high[2], 3.00, 0.005),
        ("caja z máx", high[2], 3.00, 0.005),
        ("semiancho", half, 2.502, 0.005),
        ("caja y mín", low[1], -0.52, 0.005),
        ("caja y máx", high[1], 1.353, 0.005),
        ("sin extras del techo y mín", roof_low[1], -0.52, 0.005),
        ("sin extras del techo y máx", roof_high[1], 1.084, 0.005),
        ("techo de la cabina y", cab_high[1], 1.055, 0.005),
        ("tapa del compartimento y", bay_high[1], 0.915, 0.005),
    ]
    for name, want in EMPTIES.items():
        p = godot_point(objs[name], objs[name].location * 0)
        checks += [(f"{name} {'xyz'[i]}", p[i], want[i], 0.001) for i in range(3)]
    bad = [f"{name} = {value:.4f} (debe ser {want} ± {tol})" for name, value, want, tol in checks if abs(value - want) > tol]
    if bad:
        raise RuntimeError("Medidas mal: " + "; ".join(bad))
    return checks, low, high, roof_low, roof_high


def r4(v):
    return [round(c, 4) for c in v]


def write_manifest(path, root, materials, no_shadow, livery, roof_box, env_name):
    every = list(root.children_recursive)
    objs = bpy.data.objects
    livery_objects = {objs[n] for n in livery}
    body = [o for o in every if o not in livery_objects]
    low, high, tris = bounds(body)
    low_m, high_m, tris_m = bounds(every)
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
        "paintedBody": PAINTED_BODY,
        "paintedCab": PAINTED_CAB,
        # con marcas M_Hazard (×1,2); sin marcas M_PlateDark (×0,5): sus UV vienen horneadas para las franjas
        "leadingEdges": {"pieces": LEADING_EDGES, "withLivery": "M_Hazard", "withoutLivery": "M_PlateDark",
                         "uvScaleWithoutLivery": round(REPEAT["M_PlateDark"] / REPEAT["M_Hazard"], 6)},
        "amberLights": AMBER_LIGHTS,
        "tailLights": TAIL_LIGHTS,
        "workLights": WORK_LIGHTS,
        "beacons": [{"name": "Beacon_Boost_Lens", "role": "boost", "color": f"#{AMBER:06X}"},
                    {"name": "Beacon_Damage_Lens", "role": "damage", "color": f"#{RED:06X}"}],
        "nozzleLights": NOZZLE_LIGHTS,
        "flickerLights": FLICKER_LIGHTS,
        "glass": GLASS,
        "decals": DECALS,
        # "livery": las piezas que solo se ven con marcas (mismo campo que VECTOR y BLADE)
        "livery": livery,
        "noShadow": no_shadow,
        "paintMasks": [dict(group=k, mulScale=th.MUL_SCALE, **v) for k, v in PAINT_GROUPS.items()],
        "repeats": [{"material": k, "repeat": v} for k, v in REPEAT.items()],
        "coat": [{"material": k, "coat": c, "roughness": r} for k, (c, r) in COAT.items()],
        "reflections": [{"material": k, "factor": v} for k, v in REFLECTIONS.items()],
        "transparentMaterials": [{"material": k, "opacity": v} for k, v in TRANSPARENT.items()],
        "decalMaterials": DECAL_MATERIALS,
        "scorchMaterials": SCORCH_MATERIALS,
        "uv2Scale": UV2_SCALE,
        "triangles": tris,
        "trianglesWithLivery": tris_m,
        # caja sin marcas, con marcas y sin los extras del techo (con marcas)
        "bounds": {"min": r4(low), "max": r4(high)},
        "boundsWithLivery": {"min": r4(low_m), "max": r4(high_m)},
        "boundsWithoutRoofExtras": {"min": r4(roof_box[0]), "max": r4(roof_box[1])},
        "halfWidth": round(max(-low[0], high[0]), 4),
        "halfWidthWithLivery": round(max(-low_m[0], high_m[0]), 4),
    }
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")
    return manifest


# PNG de 8 bits sin fecha: gris (MUL) o RGB (ADD)
def write_mask_png(path, pixels):
    import struct
    import zlib
    height, width = pixels.shape[:2]
    color_type = 2 if pixels.ndim == 3 else 0
    raw = b"".join(b"\x00" + pixels[y].tobytes() for y in range(height))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(tx._chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, color_type, 0, 0, 0)))
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


# Material de prueba de las capturas: AMARILLO OBRA ya coloreado, con la regla de la sección 6.2 (plateSet del
# prototipo con el color de la pintura, que es lo que reconstruyen las máscaras)
def preview_paint_materials(materials):
    folder = materials.folder
    body = io.save_texture_set(folder, "PreviewBody", ti.plate_set(87, SITE_YELLOW))
    cab = io.save_texture_set(folder, "PreviewCab", ti.plate_set(89, SITE_YELLOW))
    swap = {n: pbr("M_Preview_" + n, body, 0.35, 0.8) for n in PAINT_GROUPS["body"]["materials"]}
    swap["M_PaintCab"] = pbr("M_Preview_M_PaintCab", cab, 0.35, 0.8)
    return swap


def render_previews(scene, collection, root, materials, env_path, out):
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

    # AMARILLO OBRA coloreado en lugar de la máscara (solo en las capturas y en el .blend)
    swap = preview_paint_materials(materials)
    for obj in root.children_recursive:
        if obj.type == "MESH" and obj.data.materials and obj.data.materials[0].name in swap:
            obj.data.materials[0] = swap[obj.data.materials[0].name]

    folder = os.path.join(out, "previews")
    # las del prototipo con 0,75 m menos de altura
    shots = [("tq", (-8.2, 3.4, -7.4), (0, 0.3, -0.4), 38), ("top", (0, 17.75, -0.4), (0, 0, -0.4), 34),
             ("side", (12.5, 0.5, -0.4), (0, 0.3, -0.4), 34), ("rear", (5, 2.2, 13.5), (0, 0.3, 0.6), 34)]
    objs = bpy.data.objects
    # las calcomanías se ven negras y opacas sin su dibujo: en estas capturas, ocultas (las franjas sí se ven)
    for name in DECALS:
        objs[name].hide_render = True
    for name, eye, target, fov in shots:
        io.render_preview(scene, collection, root, os.path.join(folder, f"hull_hammer_{name}.png"), eye, target, fov)
    # Marcas con la textura de prueba: ninguna flecha girada ni en espejo
    sizes = {"Decal_Unit": (512, 384), "Decal_Logo": (512, 512), "Decal_NoStep": (512, 160), "Decal_Hot": (512, 160),
             "Decal_Cab": (512, 128)}
    for name in DECALS:
        objs[name].hide_render = False
        key = next(k for k in sizes if name.startswith(k))
        objs[name].data.materials[0] = test_decal_material(name, *sizes[key])
    io.render_preview(scene, collection, root, os.path.join(folder, "hull_hammer_markings.png"), *shots[0][1:])
    return aces or "Filmic"


NO_SHADOW_NAMES = set()


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    out = parse_out()
    os.makedirs(out, exist_ok=True)
    glb_path = os.path.join(ASSETS, "hull_hammer.glb")
    manifest_path = os.path.join(ASSETS, "hull_hammer.manifest.json")
    env_name = "cockpit_blade_env.png"
    env_path = os.path.join(COCKPIT_ASSETS, env_name)
    if not os.path.exists(env_path):
        raise RuntimeError(f"Falta el panorama de BLADE ({env_path}): ejecuta antes art/blender/run.ps1 blade")

    io.clear_file()
    scene = bpy.context.scene
    collection = scene.collection

    # Texturas sueltas aparte de las de las cabinas y de los otros cascos: algunas se llaman igual
    materials = Materials(os.path.join(out, "textures", "hull_hammer"))
    builder = Builder()
    build_fuselage(builder, materials)
    for side, k in SIDES:
        build_canard(builder, materials, side, k)
    for side, k in SIDES:
        build_fin(builder, materials, side, k)
    for side, k in SIDES:
        build_engine(builder, materials, side, k)
    build_cab(builder, materials)
    build_extras(builder, materials)
    build_bay(builder, materials)
    build_side_guards_ladders_pipes(builder, materials)
    build_markings(builder, materials)

    # Raíz girada +90° en X: un punto del prototipo (x, y, z) queda en Blender en (x, −z, y) y vuelve a ser (x, y, z) en glTF
    root = io.empty("HullHammer", None, collection, rotation=(math.pi / 2, 0.0, 0.0))
    zones = {zone: io.empty(zone, root, collection) for zone in ZONES}
    for zone, parent in SUBZONES.items():
        zones[zone] = io.empty(zone, zones[parent], collection)
    no_shadow = create_objects(builder, zones, collection)
    NO_SHADOW_NAMES.update(no_shadow)
    create_empties(zones, root, collection)
    bpy.context.view_layer.update()

    check_contract(root)
    checks, low, high, roof_low, roof_high = check_axes(root, builder)
    livery = livery_only(builder)
    manifest = write_manifest(manifest_path, root, materials, no_shadow, livery, (roof_low, roof_high), env_name)
    io.export_glb(glb_path, root)

    # Máscaras de la pintura sin pérdidas, aparte del .glb (Godot reconstruye el color con ellas)
    for group in PAINT_GROUPS.values():
        mul8, add8 = th.paint_masks(group["seed"])
        write_mask_png(os.path.join(ASSETS, group["mul"]), mul8)
        write_mask_png(os.path.join(ASSETS, group["add"]), add8)

    preview_collection = bpy.data.collections.new("Preview")
    scene.collection.children.link(preview_collection)
    view = render_previews(scene, preview_collection, root, materials, env_path, out)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "hull_hammer.blend"))

    print(f"[Hull] Blender {bpy.app.version_string} · capturas con {scene.render.engine} y vista {view} · panorama {env_name}")
    for name, value, want, tol in checks:
        print(f"[Hull] {name}: {value:.4f} (spec {want} ± {tol})")
    print(f"[Hull] Caja con marcas {r4(low)} → {r4(high)} · largo {high[2] - low[2]:.3f} · alto {high[1] - low[1]:.3f}"
          f" · sin extras del techo alto {roof_high[1] - roof_low[1]:.3f}")
    b = manifest["boundsWithLivery"]
    size = [b["max"][i] - b["min"][i] for i in range(3)]
    print(f"[Hull] Caja {size} · esfera {math.sqrt(sum(s * s for s in size)):.3f} m")
    print(f"[Hull] Triángulos {manifest['triangles']} sin marcas · {manifest['trianglesWithLivery']} con marcas"
          f" (antes de quitar los de tamaño cero: {sum(p.soup.triangles for p in builder.pieces)} con marcas)")
    print(f"[Hull] Objetos {sum(1 for o in root.children_recursive if o.type == 'MESH')} mallas")
    print(f"[Hull] GLB {os.path.getsize(glb_path)} bytes · sha256 {sha256(glb_path)}")


main()
