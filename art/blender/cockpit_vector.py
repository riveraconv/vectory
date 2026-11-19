# Cabina 3D de VECTOR, estilo CARRERAS (spec docs/cabinas-3d-1-vector.md). Traducción de buildCockpit() de
# designs/cabina-3d-vector-radar.html, colocación 1 sin intercambiar: mismos números y mismo orden.
# El morro es una copia del casco 3D (hull_vector.py), sin la viga central del cristal (spec docs/cabinas-3d-4-morros.md).
#
# Uso: blender -b --factory-startup --python art/blender/cockpit_vector.py -- --out art/blender/out
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
import hull_vector as hull  # noqa: E402
from lib import geometry as g  # noqa: E402
from lib import nose_copy as nc  # noqa: E402
from lib import textures as tx  # noqa: E402
from lib.builder import Builder  # noqa: E402

REPO = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(REPO, "assets", "cockpits")

# Ojo de la cabina en coordenadas del casco y corte de la copia del morro (spec docs/cabinas-3d-4-morros.md, 3.3)
EYE = (0.0, 0.80, -0.95)
CUT = 0.30
HULL_DROP = ["Canopy_Glass"]

HOUSING_T = 0.013
PILLAR_W = 0.034
PILLAR_D = 0.045

ACCENT = 0xFF2D2D
LED_CONSOLE = 0xFF8A00
SHIFT_GREEN = 0x3DFF8A
SHIFT_RED = 0xFF2D2D
SHIFT_BLUE = 0x3D7BFF

# Repetición de cada textura: se multiplica en las UV, así no hace falta KHR_texture_transform
REPEAT = {
    "M_Carbon": 20.0, "M_CarbonNose": 20.0, "M_CarbonNoseSide": 20.0,
    "M_Alu": 2.0, "M_AluDark": 2.0,
    "M_Fabric": 5.0,
    "M_GlassSmudge": 2.2,
}

LOW_REFLECTION_MATERIALS = []   # el morro de carbono ya no existe: lo sustituye la copia del casco
LOW_REFLECTION_FACTOR = 0.35
ADDITIVE_MATERIALS = {"M_GlassSmudge": 0.012}

ZONES = ["Nose", "Dash", "Console", "Frame", "Side_L", "Side_R"]


def contract_names():
    names = ["Screen_L", "Screen_R", "Screen_L_Shade", "Screen_R_Shade",
             "Screen_Radar", "Screen_Map", "Screen_Radar_Shade", "Screen_Map_Shade",
             "Accent_Visor", "Accent_Screen_L", "Accent_Screen_R", "Accent_Radar", "Accent_Map", "Glass_Smudge"]
    names += [f"ShiftLight_{i:02d}" for i in range(15)]
    names += [f"ShiftLight_Base_{i:02d}" for i in range(15)]
    names += [f"Led_Console_{i}" for i in range(4)]
    return names


def parse_out():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--out" in args:
        path = args[args.index("--out") + 1]
        return path if os.path.isabs(path) else os.path.join(REPO, path)
    return os.path.join(HERE, "out")


# ---------- Materiales ----------

class Materials:
    def __init__(self, texture_folder):
        carbon = io.save_texture_set(texture_folder, "Carbon", tx.carbon_set())
        alu = io.save_texture_set(texture_folder, "Alu", tx.brushed_set((182, 188, 196), 41))
        alu_dark = io.save_texture_set(texture_folder, "AluDark", tx.brushed_set((58, 62, 70), 42))
        fabric = io.save_texture_set(texture_folder, "Fabric", tx.fabric_set((25, 26, 29)))
        smudge = io.save_texture_set(texture_folder, "Smudge", {"color": tx.smudge_texture()})

        self.carbon = io.material("M_Carbon", metallic=0.25, coat=1.0, coat_roughness=0.06, textures=carbon)
        self.alu = io.material("M_Alu", metallic=1.0, textures=alu)
        self.alu_dark = io.material("M_AluDark", metallic=1.0, textures=alu_dark)
        self.fabric = io.material("M_Fabric", metallic=0.0, textures=fabric)
        self.paint = io.material("M_Paint", base=io.hex_color(0xB5121B), metallic=0.1, roughness=0.32,
                                 coat=1.0, coat_roughness=0.05)
        self.dark = io.material("M_Dark", base=io.hex_color(0x060606), roughness=0.9)
        self.shade = io.material("M_ScreenShade", base=(0, 0, 0, 1), roughness=1.0, alpha=0.7)
        self.smudge = io.material("M_GlassSmudge", roughness=1.0, textures=smudge, alpha=0.012)
        self.emissive = {}  # nombre de la pieza → (color, intensidad)

    def glow(self, kind, piece, color, intensity):
        self.emissive[piece] = (color, intensity)
        return io.material(f"M_Emissive_{kind}_{piece}", base=(0, 0, 0, 1), roughness=0.4,
                           emission=io.hex_color(color), emission_strength=intensity)

    @staticmethod
    def screen(piece):
        return io.material(f"M_Screen_{piece}", base=(0, 0, 0, 1), roughness=1.0)


# ---------- Cabina: buildCockpit() del prototipo ----------

def build_dash(b, m):
    b.zone = "Dash"
    nu = 40
    us = [-1 + 2 * i / nu for i in range(nu + 1)]

    def s_f(u):
        a = max(0.0, (abs(u) - 0.35) / 0.65)
        return a * a

    front = [(0.90 * u, -0.300 + 0.19 * s_f(u), -0.80 + 0.32 * s_f(u)) for u in us]
    lip = [(p[0] * 0.97, p[1] + 0.035, p[2] + 0.15) for p in front]
    lip2 = [(p[0], p[1] - 0.03, p[2] + 0.004) for p in lip]
    bottom = [(p[0] * 0.97, -0.72, p[2] + 0.13) for p in lip2]
    b.strip(front, lip, m.fabric)
    b.strip(lip, lip2, m.alu_dark)
    b.strip(lip2, bottom, m.carbon)
    b.tube([(p[0], p[1] - 0.004, p[2] + 0.002) for p in lip], 0.008, m.alu_dark, None, 80)  # canto redondeado
    b.tube([(p[0], p[1] - 0.003, p[2]) for p in front], 0.006, m.fabric, None, 80)
    visor = [(p[0] * 0.995, p[1] - 0.012, p[2] + 0.008) for p in lip2][4:-4]
    b.light(visor, m.glow("Accent", "Accent_Visor", ACCENT, 2.2), "Accent_Visor", 0.0016)


# makePanel del prototipo: carcasa, fondo, sombra interior, bisel, línea de luz y superficie de la pantalla
def make_panel(b, m, screen_name, accent_name, pos, look, w, h, d):
    group = g.look_at(tuple(pos), tuple(look))
    t = HOUSING_T
    b.rbox(w + 2 * t, t, d, m.alu_dark, (0, h / 2 + t / 2, -d / 2), None, group)
    b.rbox(w + 2 * t, t, d, m.alu_dark, (0, -h / 2 - t / 2, -d / 2), None, group)
    b.rbox(t, h, d, m.alu_dark, (-w / 2 - t / 2, 0, -d / 2), None, group)
    b.rbox(t, h, d, m.alu_dark, (w / 2 + t / 2, 0, -d / 2), None, group)
    b.rbox(w + 2 * t, h + 2 * t, 0.01, m.dark, (0, 0, -d - 0.005), None, group, 0.002)
    shade = group @ g.translation((0, h / 2 - 0.001, -d / 2)) @ g.euler_xyz((math.pi / 2, 0, 0))
    b.add(g.plane(w, d), m.shade, shade, f"{screen_name}_Shade", no_shadow=True)
    bezel_w = w + 2 * t + 0.03
    b.rbox(bezel_w, 0.03, 0.012, m.alu, (0, -h / 2 - t - 0.012, -0.004), None, group, 0.004)
    b.light([(-w / 2 - t, -h / 2 - t - 0.003, 0.001), (w / 2 + t, -h / 2 - t - 0.003, 0.001)],
            m.glow("Accent", accent_name, ACCENT, 2.0), accent_name, 0.0012, group)
    surface = group @ g.translation((0, 0, -d + 0.004))
    b.add(g.plane(w, h), m.screen(screen_name), surface, screen_name, no_shadow=True)
    return group


def build_screens(b, m):
    b.zone = "Dash"
    look = (0, 0.02, 0.12)
    panels = {}
    panels["Screen_L"] = make_panel(b, m, "Screen_L", "Accent_Screen_L", (-0.41, -0.285, -0.528), look, 0.27, 0.085, 0.045)
    panels["Screen_R"] = make_panel(b, m, "Screen_R", "Accent_Screen_R", (0.41, -0.285, -0.528), look, 0.27, 0.085, 0.045)
    panels["Screen_Radar"] = make_panel(b, m, "Screen_Radar", "Accent_Radar", (-0.205, -0.265, -0.545), look, 0.09, 0.09, 0.035)
    panels["Screen_Map"] = make_panel(b, m, "Screen_Map", "Accent_Map", (0.205, -0.265, -0.545), look, 0.09, 0.09, 0.035)
    return panels


def build_console(b, m):
    b.zone = "Console"
    group = g.translation((0, -0.345, -0.565)) @ g.euler_xyz((0.45, 0, 0))
    b.rbox(0.26, 0.075, 0.13, m.carbon, (0, 0, 0), None, group, 0.008)
    for i, x in enumerate((-0.09, 0.0, 0.09)):
        knob = group @ g.translation((x, 0.046, -0.01))
        b.add(g.cylinder(0.014, 0.016, 0.016, 24), m.paint if i == 1 else m.alu, knob)
        b.rbox(0.003, 0.002, 0.012, m.dark, (x, 0.0545, -0.016), None, group, 0.0005)
    for i, x in enumerate((-0.105, -0.035, 0.035, 0.105)):
        b.add(g.cylinder(0.007, 0.007, 0.006, 16), m.dark, group @ g.translation((x, 0.041, 0.035)))
        name = f"Led_Console_{i}"
        b.led((x, 0.045, 0.035), m.glow("LedConsole", name, LED_CONSOLE, 1.4), name, 0.0025, group)


def build_frame(b, m):
    b.zone = "Frame"
    top_y, top_z = 0.395, -0.56
    b.beam((-1.3, top_y, top_z), (1.3, top_y, top_z), 0.065, 0.095, m.alu_dark, (0, -0.3, 0.3))
    # Luces de cambio sobre la visera. Se exportan con su intensidad encendida: Godot las apaga
    b.zone = "Dash"
    for i in range(15):
        x = -0.098 + i * 0.014
        b.rbox(0.011, 0.008, 0.01, m.dark, (x, -0.258, -0.66), None, None, 0.0015, name=f"ShiftLight_Base_{i:02d}")
        kind, color = ("ShiftGreen", SHIFT_GREEN) if i < 5 else (("ShiftRed", SHIFT_RED) if i < 10 else ("ShiftBlue", SHIFT_BLUE))
        name = f"ShiftLight_{i:02d}"
        b.led((x, -0.2535, -0.655), m.glow(kind, name, color, 3.5), name, 0.0035)
    # Techo: no proyecta sombra
    b.zone = "Frame"
    b.poly([(-1.3, 0.43, -0.60), (1.3, 0.43, -0.60), (1.3, 0.47, 0.6), (-1.3, 0.47, 0.6)], m.carbon, name="Frame_Roof", no_shadow=True)


def build_sides(b, m):
    for k, zone in ((-1, "Side_L"), (1, "Side_R")):
        b.zone = zone
        # Pilar A
        b.beam((k * 0.50, 0.37, -0.552), (k * 0.45, -0.29, -0.78), PILLAR_W, PILLAR_D, m.alu_dark, (0, 0, 0.2))
        # Base del pilar
        b.rbox(0.10, 0.032, 0.075, m.alu_dark, (k * 0.45, -0.283, -0.76), (0, -k * 0.5, 0))
        # Tira exterior entre cristal frontal y lateral
        b.beam((k * 0.66, 0.40, -0.45), (k * 0.84, -0.15, -0.52), 0.09, 0.045, m.alu_dark, (0, 0, 0.2))
        # Carril del techo y pilar trasero
        b.beam((k * 0.80, 0.43, -0.48), (k * 0.90, 0.43, 0.7), 0.07, 0.06, m.alu_dark, (0, 0, 0.1))
        b.beam((k * 0.88, 0.42, 0.55), (k * 0.96, -0.15, 0.65), 0.12, 0.05, m.alu_dark, (0, 0, 0))
        # Brazola y pared lateral
        b.beam((k * 0.84, -0.15, -0.50), (k * 0.88, -0.14, 0.8), 0.11, 0.05, m.carbon, (0, 1, 0.2))
        b.poly([(k * 0.79, -0.17, -0.45), (k * 0.83, -0.17, 0.8), (k * 0.72, -0.9, 0.8), (k * 0.70, -0.9, -0.45)], m.fabric)
        b.vent((k * 0.752, -0.30, -0.12), 0.12, 0.07, 6, m.alu_dark, (0, -0.3, -0.12), m.alu_dark, m.dark)
        # Salidas de aire en el frontal
        b.vent((k * 0.70, -0.30, -0.52), 0.09, 0.05, 5, m.carbon, (0, 0.05, 0.1), m.carbon, m.dark)
        # Tirador de anclaje en aluminio
        b.tube([(k * 0.82, -0.12, -0.2), (k * 0.80, -0.08, -0.12), (k * 0.80, -0.08, 0.02), (k * 0.82, -0.12, 0.1)], 0.007, m.alu)


def build_glass(b, m):
    b.zone = "Frame"
    quad = [(-0.47, 0.36, -0.566), (0.47, 0.36, -0.566), (0.42, -0.27, -0.795), (-0.42, -0.27, -0.795)]
    b.poly(quad, m.smudge, name="Glass_Smudge", no_shadow=True)


# ---------- De las piezas a objetos de Blender ----------

def uv_scale(material):
    return REPEAT.get(material.name, 1.0)


def create_objects(builder, root, collection):
    zones = {zone: io.empty(zone, root, collection) for zone in ZONES}
    # Las piezas sin nombre se juntan por zona y material: menos objetos y menos llamadas de dibujo en Godot
    merged = {}
    for piece in builder.pieces:
        if piece.name is not None:
            io.mesh_object(piece.name, piece.soup, piece.material, zones[piece.zone], collection, piece.matrix, uv_scale(piece.material))
            continue
        key = (piece.zone, piece.material.name)
        soup = g.transformed(piece.soup, piece.matrix)
        if key not in merged:
            merged[key] = (piece.material, g.Soup([], [], []))
        target = merged[key][1]
        target.positions += soup.positions
        target.normals += soup.normals
        target.uvs += soup.uvs
    for (zone, material_name), (material, soup) in merged.items():
        io.mesh_object(f"{zone}_{material_name}", soup, material, zones[zone], collection, None, uv_scale(material))


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


# Comprobaciones de la spec (docs/cabinas-3d-4-morros.md, 3.3), en coordenadas de la cabina: punta del morro de la
# copia en (0; −0,80; −3,05), techo en y ≈ +0,45 y ninguna pieza del marco en x = 0 entre el larguero y el salpicadero
# (la viga central ya no está)
def check_axes(builder, nose):
    tip = min((p for piece in nose for p in piece.soup.positions), key=lambda p: p[2])
    roof = bpy.data.objects["Frame_Roof"]
    roof_y = [godot_point(roof, v.co)[1] for v in roof.data.vertices]
    roof_mid = (min(roof_y) + max(roof_y)) / 2
    if max(abs(tip[0]), abs(tip[1] + 0.80), abs(tip[2] + 3.05)) > 0.005 or abs(roof_mid - 0.45) > 0.01:
        raise RuntimeError(f"Ejes mal: punta del morro en {tuple(round(c, 4) for c in tip)}, techo en y = {roof_mid:.3f}")
    halo = [piece for piece in builder.pieces if piece.zone in ("Frame", "Side_L", "Side_R")
            and piece.name != "Glass_Smudge"
            and any(abs(p[0]) < 0.03 and -0.26 < p[1] < 0.35 for p in g.transformed(piece.soup, piece.matrix).positions)]
    if halo:
        raise RuntimeError("Queda una pieza del marco en x = 0 entre el larguero y el salpicadero")
    return tip, roof_mid


def write_manifest(path, root, materials, panels, no_shadow, nose_entries, nose_low, nose_high):
    low, high, triangles = measure(root)
    pieces = []
    for name in contract_names():
        obj = bpy.data.objects[name]
        entry = {"name": name}
        if name in panels:
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
        "lowReflectionMaterials": LOW_REFLECTION_MATERIALS,
        "lowReflectionFactor": LOW_REFLECTION_FACTOR,
        "additiveMaterials": [{"material": k, "opacity": v} for k, v in ADDITIVE_MATERIALS.items()],
        "triangles": triangles,
        "bounds": {"min": [round(c, 4) for c in low], "max": [round(c, 4) for c in high]},
        # Grupos de la cabina (sin Halo: la viga central ya no está) y copia del morro del casco (spec
        # docs/cabinas-3d-4-morros.md, 3.2 y 5)
        "zones": ZONES,
        "nose": nc.manifest("hull_vector", EYE, CUT, nose_entries, nose_low, nose_high),
    }
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


def render_previews(scene, collection, root, env_path, panels, out):
    engine = "BLENDER_EEVEE"
    scene.render.engine = engine
    # El prototipo usa ACES: se aproxima con la vista que haya, solo para mirar
    views = {v.identifier for v in scene.view_settings.bl_rna.properties["view_transform"].enum_items}
    aces = next((n for n in ("ACES 2.0", "ACES 1.3", "ACES") if n in views), None)
    scene.view_settings.view_transform = aces or "Filmic"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = math.log2(1.05)

    def linear(hex_value):
        return io.hex_color(hex_value)[:3]

    io.preview_world(scene, env_path, linear(0xB0B8C8), linear(0x101010), 0.6, linear(0x1C1C22), 0.45, (0.002, 0.003, 0.006, 1.0))
    io.preview_light_sun(collection, root, (0.7, 2.2, -1.6), (0, -0.3, -0.4), 2.6)
    io.preview_light_point("PreviewLamp", collection, root, (0, 0.32, -0.25), (1, 1, 1), 0.08)
    for name, group in panels.items():
        strength = 0.35 * (0.3 if name in ("Screen_L", "Screen_R") else 0.2)
        io.preview_light_point(f"Preview{name}", collection, root, g.transform_point(group, (0, 0, 0.06)), (1, 1, 1), strength)

    folder = os.path.join(out, "previews")
    io.render_preview(scene, collection, root, os.path.join(folder, "eye.png"), (0, 0, 0), (0, 0, -1), 70)
    # Desde el ojo con el morro nuevo (spec docs/cabinas-3d-4-morros.md, 3.3)
    nc.hide_livery_in_previews(root, ("Livery",))
    # Dos capturas: con la copia pintada antes que la cabina, como la maqueta (la cabina tapa lo que tiene detrás), y
    # con las dos en el mismo espacio (lo que esté más cerca del ojo, delante)
    nc.render_layered(scene, collection, root, os.path.join(folder, "vector_eye_new.png"), (0, 0, 0), (0, 0, -1), 70,
                      nc.copied_names(root))
    io.render_preview(scene, collection, root, os.path.join(folder, "vector_eye_new_depth.png"), (0, 0, 0), (0, 0, -1), 70)
    io.render_preview(scene, collection, root, os.path.join(folder, "three_quarter.png"), (2.4, 1.6, 2.6), (0, -0.25, -0.9), 45)
    io.render_preview(scene, collection, root, os.path.join(folder, "screens.png"), (0, -0.08, -0.05), (0, -0.3, -0.58), 60)
    return aces or "Filmic"


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    out = parse_out()
    os.makedirs(out, exist_ok=True)
    glb_path = os.path.join(ASSETS, "cockpit_vector.glb")
    manifest_path = os.path.join(ASSETS, "cockpit_vector.manifest.json")
    env_path = os.path.join(ASSETS, "cockpit_env.png")

    io.clear_file()
    scene = bpy.context.scene
    collection = scene.collection

    io.render_environment(env_path)

    # Primero el casco, en memoria: sus materiales e imágenes pasan a llevar el prefijo Hull_ antes de crear los de la
    # cabina, que a veces se llaman igual
    hull_materials, hull_builder = hull.build_hull(out)
    nose = nc.front_pieces(hull_builder.pieces, EYE, CUT, HULL_DROP)
    nc.check_cut(nose, CUT)
    originals = nc.prefix_hull_data()

    materials = Materials(os.path.join(out, "textures"))
    builder = Builder()
    build_dash(builder, materials)
    panels = build_screens(builder, materials)
    build_console(builder, materials)
    build_frame(builder, materials)
    build_sides(builder, materials)
    build_glass(builder, materials)

    # Raíz girada +90° en X: un punto del prototipo (x, y, z) queda en Blender en (x, −z, y) y vuelve a ser (x, y, z) en glTF
    root = io.empty("CockpitVector", None, collection, rotation=(math.pi / 2, 0.0, 0.0))
    create_objects(builder, root, collection)
    nose_entries = nc.create_objects(nose, hull.contract_names(), bpy.data.objects["Nose"], collection, hull.uv_args,
                                     originals, set(hull_materials.emissive))
    bpy.context.view_layer.update()

    check_contract(root)
    tip, roof = check_axes(builder, nose)
    no_shadow = [p.name for p in builder.pieces if p.no_shadow] + [e["name"] for e in nose_entries if e["noShadow"]]
    nose_low, nose_high = nc.bounds(nose)
    write_manifest(manifest_path, root, materials, panels, no_shadow, nose_entries, nose_low, nose_high)
    io.export_glb(glb_path, root)

    preview_collection = bpy.data.collections.new("Preview")
    scene.collection.children.link(preview_collection)
    view = render_previews(scene, preview_collection, root, env_path, panels, out)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "cockpit_vector.blend"))

    print(f"[Cockpit] Blender {bpy.app.version_string} · capturas con {scene.render.engine} y vista {view}")
    print(f"[Cockpit] Punta del morro {tuple(round(float(c), 4) for c in tip)} · techo y = {roof:.3f}")
    nc.report(nose_entries, nose, nose_high)
    print(f"[Cockpit] GLB {os.path.getsize(glb_path)} bytes · sha256 {sha256(glb_path)}")


main()
