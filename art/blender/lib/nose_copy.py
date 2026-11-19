# Copia del morro del casco 3D para la cabina (spec docs/cabinas-3d-4-morros.md, 3.1 y 5). Común a las tres cabinas.
#
# La cabina construye en memoria el casco de su nave (build_hull de su hull_<nave>.py) y aquí:
#   1. se quitan las piezas que taparían la vista (el cristal de la cúpula, la cabina de grúa de HAMMER…);
#   2. se corta todo con el plano z = z_ojo + corte (coordenadas del casco) y se queda la parte de delante;
#   3. se mueve restando el ojo: un punto p del casco cae en la cabina en p − ojo;
#   4. se crean los objetos con el nombre que tienen en el .glb del casco y el prefijo Nose_.
# Los materiales e imágenes del casco llevan el prefijo Hull_ dentro de la cabina, porque algunos se llaman igual que
# los de la cabina (M_Paint, M_Dark…). Las piezas se trabajan antes de Blender, con sus sopas de triángulos.
import bpy
import numpy as np

from . import blender_io as io
from . import geometry as g
from . import textures as tx
from .builder import Piece

PREFIX = "Nose_"
DATA_PREFIX = "Hull_"
EPSILON = 1e-9


# ---------- Piezas ----------

# Piezas del casco por delante del corte, en coordenadas de la cabina (origen en el ojo), con su transformación ya
# aplicada. drop_names / drop_zones: piezas y zonas que no van. Conserva el orden, la zona y el nombre de cada pieza.
def front_pieces(pieces, eye, cut, drop_names=(), drop_zones=()):
    cut_z = eye[2] + cut
    out = []
    for piece in pieces:
        if piece.name in drop_names or piece.zone in drop_zones:
            continue
        soup = clip_front(baked(piece), cut_z)
        if not soup.positions:
            continue
        out.append(Piece(moved(soup, eye), piece.material, g.identity(), piece.zone, piece.name, piece.no_shadow))
    return out


# Las piezas enteras que se llevan aparte (los barrotes de la cúpula de BLADE), sin cortar, movidas al ojo
def whole_pieces(pieces, eye, names):
    return [Piece(moved(baked(p), eye), p.material, g.identity(), p.zone, p.name, p.no_shadow)
            for p in pieces if p.name in names]


# La sopa con la transformación de la pieza aplicada; las normales con la inversa traspuesta, como el casco al juntar
def baked(piece):
    m = piece.matrix
    nm = np.linalg.inv(m[:3, :3]).T
    return g.Soup([g.transform_point(m, p) for p in piece.soup.positions],
                  [g._normalize(tuple(nm @ np.array(n))) for n in piece.soup.normals],
                  list(piece.soup.uvs))


def moved(soup, eye):
    ex, ey, ez = eye
    return g.Soup([(p[0] - ex, p[1] - ey, p[2] - ez) for p in soup.positions], soup.normals, soup.uvs)


# Se queda con lo que hay en z ≤ cut_z. Los triángulos que cruzan el plano se cortan (Sutherland-Hodgman sobre el
# triángulo), interpolando posición, normal y UV, y se vuelven a triangular en abanico con el mismo sentido.
def clip_front(soup, cut_z):
    return _clip(soup, cut_z, True)


# Lo contrario: se queda con lo que hay en z ≥ cut_z (los barrotes de la cúpula de BLADE, por detrás de su arco)
def clip_back(soup, cut_z):
    return _clip(soup, cut_z, False)


def _clip(soup, cut_z, keep_front):
    pos, nrm, uvs = soup.positions, soup.normals, soup.uvs
    out = g.Soup([], [], [])
    for i in range(0, len(pos), 3):
        tri = [(pos[j], nrm[j], uvs[j]) for j in (i, i + 1, i + 2)]
        inside = [(v[0][2] <= cut_z) == keep_front for v in tri]
        if all(inside):
            for p, n, uv in tri:
                out.positions.append(p)
                out.normals.append(n)
                out.uvs.append(uv)
            continue
        if not any(inside):
            continue
        poly = []
        for k in range(3):
            a, b = tri[k], tri[(k + 1) % 3]
            a_in, b_in = inside[k], inside[(k + 1) % 3]
            if a_in:
                poly.append(a)
            if a_in != b_in:
                poly.append(_cross_point(a, b, cut_z))
        for k in range(1, len(poly) - 1):
            for p, n, uv in (poly[0], poly[k], poly[k + 1]):
                out.positions.append(p)
                out.normals.append(n)
                out.uvs.append(uv)
    return out


def _cross_point(a, b, cut_z):
    za, zb = a[0][2], b[0][2]
    t = (cut_z - za) / (zb - za) if abs(zb - za) > EPSILON else 0.0

    def lerp(u, v):
        return tuple(u[c] + (v[c] - u[c]) * t for c in range(len(u)))

    p = lerp(a[0], b[0])
    return (p[0], p[1], cut_z), g._normalize(lerp(a[1], b[1])), lerp(a[2], b[2])


# ---------- Materiales ----------

# Pone el prefijo Hull_ a todos los materiales e imágenes que hay ahora (los del casco, recién construido), antes de
# crear los de la cabina. Devuelve el nombre original de cada material, por su nombre nuevo.
def prefix_hull_data():
    originals = {}
    for material in list(bpy.data.materials):
        original = material.name
        material.name = DATA_PREFIX + original
        originals[material.name] = original
    for image in list(bpy.data.images):
        image.name = DATA_PREFIX + image.name
    return originals


class _Named:
    def __init__(self, name):
        self.name = name


# ---------- Objetos ----------

# Crea los objetos de la copia colgando de parent, como los crea el casco: las piezas de su contrato solas y el resto
# juntas por zona, material y sombra (mismo nombre que en el .glb del casco), con el prefijo dado.
# uv_args: la función del casco (escala de las UV y de la UV2 por material), que recibe el nombre original.
# live: nombres de pieza del casco que son vivas (luces, acentos, toberas).
# Devuelve una entrada por objeto: nombre, nombre en el casco, zona del casco, material original, si es viva y si no
# da sombra.
# hull_contract: el contrato del casco, si las piezas solas de la cabina no lo están en el casco (los barrotes de BLADE):
# entonces su nombre en el casco es el del objeto donde el casco las junta.
def create_objects(pieces, contract, parent, collection, uv_args, originals, live, prefix=PREFIX, hull_contract=None):
    contract = set(contract)
    in_hull = None if hull_contract is None else set(hull_contract)
    entries = []
    merged = {}
    for piece in pieces:
        original = originals.get(piece.material.name, piece.material.name)
        if piece.name in contract:
            entry = _object(prefix, piece.name, piece.zone, piece.soup, piece.material, original, parent,
                            collection, uv_args, piece.name in live, piece.no_shadow)
            if in_hull is not None and piece.name not in in_hull:
                entry["hull"] = f"{piece.zone}_{original}" + ("_NoShadow" if piece.no_shadow else "")
            entries.append(entry)
            continue
        key = (piece.zone, piece.material.name, piece.no_shadow)
        if key not in merged:
            merged[key] = (piece.material, original, g.Soup([], [], []))
        target = merged[key][2]
        target.positions += piece.soup.positions
        target.normals += piece.soup.normals
        target.uvs += piece.soup.uvs
    for (zone, _, shadowless), (material, original, soup) in merged.items():
        hull_name = f"{zone}_{original}" + ("_NoShadow" if shadowless else "")
        entries.append(_object(prefix, hull_name, zone, soup, material, original, parent, collection, uv_args, False,
                               shadowless))
    return entries


def _object(prefix, hull_name, zone, soup, material, original, parent, collection, uv_args, live, no_shadow):
    uv1, uv2 = uv_args(_Named(original))
    name = prefix + hull_name
    io.mesh_object(name, soup, material, parent, collection, None, uv1, uv2)
    return {"name": name, "hull": hull_name, "zone": zone, "material": original, "live": live, "noShadow": no_shadow}


# ---------- Comprobaciones ----------

# Caja de un conjunto de piezas (coordenadas de la cabina = de Godot)
def bounds(pieces):
    low = [float("inf")] * 3
    high = [float("-inf")] * 3
    for piece in pieces:
        for p in piece.soup.positions:
            for c in range(3):
                low[c] = min(low[c], p[c])
                high[c] = max(high[c], p[c])
    return low, high


# Falla si alguna pieza de la copia queda por detrás del corte (z > corte en la cabina)
def check_cut(pieces, cut):
    behind = sorted({p.name or p.zone for p in pieces if any(v[2] > cut + 1e-6 for v in p.soup.positions)})
    if behind:
        raise RuntimeError("Piezas del casco por detrás del corte: " + ", ".join(behind))


def triangles(pieces):
    return sum(len(p.soup.positions) // 3 for p in pieces)


TRIANGLE_WARNING = 60000


def report(entries, pieces, high):
    count = triangles(pieces)
    print(f"[Cockpit] Copia del morro: {len(entries)} objetos, {count} triángulos, z hasta {high[2]:.4f}")
    if count > TRIANGLE_WARNING:
        print(f"[Cockpit] AVISO: la copia del morro suma {count} triángulos (más de {TRIANGLE_WARNING})")


# ---------- Manifiesto y capturas ----------

# Entrada "nose" del manifiesto de la cabina (spec 5): cada pieza con su nombre en el casco, su material del casco (en
# la cabina lleva el prefijo Hull_) y si es viva
# frame: piezas enteras del casco que van en otro grupo de la cabina (los barrotes de BLADE, en Frame)
def manifest(hull_name, eye, cut, entries, low, high, frame=()):
    def listed(items):
        return [{k: e[k] for k in ("name", "hull", "material", "live")} for e in items]

    data = {
        "hull": hull_name,
        "eye": list(eye),
        "cut": cut,
        "materialPrefix": DATA_PREFIX,
        "pieces": listed(entries),
        "bounds": {"min": [round(float(c), 4) for c in low], "max": [round(float(c), 4) for c in high]},
    }
    if frame:
        data["frame"] = listed(frame)
    return data


# Capturas: los materiales de máscara del casco (camuflaje, MUL de HAMMER) por los de prueba ya coloreados. swap va
# por el nombre original; en la cabina llevan el prefijo Hull_
def swap_preview_materials(root, swap):
    for obj in root.children_recursive:
        if obj.type != "MESH" or not obj.data.materials:
            continue
        name = obj.data.materials[0].name
        if name.startswith(DATA_PREFIX) and name[len(DATA_PREFIX):] in swap:
            obj.data.materials[0] = swap[name[len(DATA_PREFIX):]]


# Las calcomanías salen negras y opacas sin su dibujo (lo pone Godot): en las capturas, ocultas, con las zonas de librea
def hide_livery_in_previews(root, livery_zones):
    for obj in root.children_recursive:
        name = obj.name
        if name.startswith(PREFIX + "Decal_") or any(name.startswith(PREFIX + z + "_") for z in livery_zones):
            obj.hide_render = True


# Captura como la maqueta y el juego: primero la copia del morro sola (con el fondo) y encima la cabina sin ella, con
# su alfa. Así lo que la cabina tapa no se ve aunque la copia esté más cerca del ojo. back: nombres de la copia.
def render_layered(scene, collection, root, path, eye, target, fov, back):
    import os
    back = set(back)
    meshes = [o for o in root.children_recursive if o.type == "MESH"]
    saved = {o: o.hide_render for o in meshes}
    back_path, front_path = path + ".back.png", path + ".front.png"
    for o in meshes:
        o.hide_render = saved[o] or o.name not in back
    io.render_preview(scene, collection, root, back_path, eye, target, fov)
    for o in meshes:
        o.hide_render = saved[o] or o.name in back
    transparent = scene.render.film_transparent
    scene.render.film_transparent = True
    io.render_preview(scene, collection, root, front_path, eye, target, fov, alpha=True)
    scene.render.film_transparent = transparent
    for o in meshes:
        o.hide_render = saved[o]

    def pixels(p):
        img = bpy.data.images.load(p)
        w, h = img.size
        a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
        bpy.data.images.remove(img)
        return a

    b, f = pixels(back_path), pixels(front_path)
    alpha = f[..., 3:4]
    rgb = f[..., :3] * alpha + b[..., :3] * (1 - alpha)
    tx.write_png(path, np.flipud(np.clip(np.round(rgb * 255), 0, 255).astype(np.uint8)))
    os.remove(back_path)
    os.remove(front_path)


# Objetos de la cabina que vienen del casco: la copia del morro y, en BLADE, los barrotes de la cúpula
def copied_names(root):
    return [o.name for o in root.children_recursive
            if o.type == "MESH" and (o.name.startswith(PREFIX) or o.name.startswith("Canopy_Bar_"))]
