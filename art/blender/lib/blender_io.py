# Lo que sí depende de Blender: mallas, materiales de glTF (Principled BSDF con imágenes), panorama, capturas y exportación.
import math
import os

import bpy
from mathutils import Matrix

from . import geometry as g
from .textures import strip_png_text, write_png


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


# Color #RRGGBB en el espacio lineal de Blender, igual que THREE.Color(0xRRGGBB)
def hex_color(value, alpha=1.0):
    return (
        srgb_to_linear(((value >> 16) & 255) / 255),
        srgb_to_linear(((value >> 8) & 255) / 255),
        srgb_to_linear((value & 255) / 255),
        alpha,
    )


def clear_file():
    for scene in list(bpy.data.scenes)[1:]:
        bpy.data.scenes.remove(scene)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.images):
        for item in list(block):
            block.remove(item)


def empty(name, parent, collection, rotation=None):
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.parent = parent
    if rotation is not None:
        obj.rotation_euler = rotation
    return obj


# Crea un objeto con la sopa de triángulos. Suelda vértices por posición: normales y UV van por esquina,
# así que el aspecto no cambia y el modelo pesa mucho menos.
def mesh_object(name, soup, material, parent, collection, matrix=None, uv_scale=1.0):
    index = {}
    verts, faces, normals, uvs = [], [], [], []
    p = soup.positions
    for i in range(0, len(p), 3):
        face = []
        for j in (i, i + 1, i + 2):
            key = (round(p[j][0], 6), round(p[j][1], 6), round(p[j][2], 6))
            v = index.get(key)
            if v is None:
                v = index[key] = len(verts)
                verts.append(p[j])
            face.append(v)
        if face[0] == face[1] or face[1] == face[2] or face[0] == face[2]:
            continue
        faces.append(face)
        for j in (i, i + 1, i + 2):
            normals.append(soup.normals[j])
            uvs += [soup.uvs[j][0] * uv_scale, soup.uvs[j][1] * uv_scale]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    layer = mesh.uv_layers.new(name="UVMap")
    layer.data.foreach_set("uv", uvs)
    mesh.update()
    mesh.normals_split_custom_set(normals)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = parent
    if matrix is not None:
        obj.matrix_basis = Matrix([list(row) for row in matrix])
    return obj


def save_texture_set(folder, name, maps):
    paths = {}
    for key, pixels in maps.items():
        path = os.path.join(folder, f"T_{name}_{key}.png")
        write_png(path, pixels)
        paths[key] = path
    return paths


def _image(path, non_color):
    img = bpy.data.images.load(path, check_existing=True)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    return img


def material(name, base=(0, 0, 0, 1), metallic=0.0, roughness=0.5, coat=0.0, coat_roughness=0.03,
             textures=None, emission=None, emission_strength=0.0, alpha=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_backface_culling = False
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = base
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Coat Weight"].default_value = coat
    bsdf.inputs["Coat Roughness"].default_value = coat_roughness
    if textures:
        if "color" in textures:
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = _image(textures["color"], False)
            links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if "roughness" in textures:
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = _image(textures["roughness"], True)
            links.new(tex.outputs["Color"], bsdf.inputs["Roughness"])
        if "normal" in textures:
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = _image(textures["normal"], True)
            normal = nodes.new("ShaderNodeNormalMap")
            links.new(tex.outputs["Color"], normal.inputs["Color"])
            links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        mat.surface_render_method = "BLENDED"
    return mat


def _standard_view(scene):
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0


def _render_png(scene, path, width, height):
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.render.dither_intensity = 0.0
    # Sin fecha ni tiempo de render dentro del PNG: dos ejecuciones tienen que dar el mismo archivo
    for prop in scene.render.bl_rna.properties:
        if prop.identifier.startswith("use_stamp"):
            setattr(scene.render, prop.identifier, False)
    scene.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True, scene=scene.name)


# Panorama de reflejos: los mismos planos que el bloque «Entorno para reflejos» del prototipo, en una escena aparte.
# Cycles, porque Eevee no tiene cámara panorámica. El centro de la imagen mira hacia −Z del prototipo.
def render_environment(path):
    scene = bpy.data.scenes.new("Env")
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 16
    scene.cycles.seed = 0
    scene.cycles.use_denoising = False
    scene.cycles.max_bounces = 0
    _standard_view(scene)

    world = bpy.data.worlds.new("EnvWorld")
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = hex_color(0x020306)
    background.inputs["Strength"].default_value = 1.0
    scene.world = world

    collection = scene.collection
    root = empty("EnvRoot", None, collection, rotation=(math.pi / 2, 0.0, 0.0))

    def glowing(r, gr, b):
        mat = bpy.data.materials.new("Env")
        nodes = mat.node_tree.nodes
        nodes.clear()
        out = nodes.new("ShaderNodeOutputMaterial")
        em = nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = (r, gr, b, 1.0)
        mat.node_tree.links.new(em.outputs[0], out.inputs[0])
        return mat

    city = glowing(0.55, 0.24, 0.08)
    for i in range(18):
        a = i / 18 * math.pi * 2
        h = 1 + (i * 7 % 5) * 0.6
        pos = (math.cos(a) * 12, h / 2 - 0.4, math.sin(a) * 12)
        mesh_object(f"EnvCity{i}", g.plane(2.5, h), city, root, collection, g.look_at(pos, (0.0, h / 2 - 0.4, 0.0)))
    bars = glowing(0.1, 0.22, 0.8)
    for i in range(4):
        m = g.translation((0.0, 9.0, -12.0 + i * 8)) @ g.euler_xyz((math.pi / 2, 0.0, 0.0))
        mesh_object(f"EnvBar{i}", g.plane(14.0, 0.35), bars, root, collection, m)
    top = glowing(0.05, 0.07, 0.14)
    m = g.translation((0.0, 10.0, 0.0)) @ g.euler_xyz((math.pi / 2, 0.0, 0.0))
    mesh_object("EnvTop", g.plane(30.0, 30.0), top, root, collection, m)

    cam = bpy.data.cameras.new("EnvCamera")
    cam.type = "PANO"
    cam.panorama_type = "EQUIRECTANGULAR"
    cam.clip_start = 0.01
    cam.clip_end = 100.0
    cam_obj = bpy.data.objects.new("EnvCamera", cam)
    collection.objects.link(cam_obj)
    cam_obj.parent = root
    scene.camera = cam_obj

    _render_png(scene, path, 1024, 512)
    strip_png_text(path)
    bpy.data.scenes.remove(scene)


# Mundo de las capturas: el panorama para los reflejos, más la luz de hemisferio y ambiente del prototipo.
# Los rayos de cámara ven el fondo oscuro.
def preview_world(scene, env_path, hemi_sky, hemi_ground, hemi_strength, ambient, ambient_strength, background):
    world = bpy.data.worlds.new("PreviewWorld")
    tree = world.node_tree
    nodes = tree.nodes
    links = tree.links
    out = next(n for n in nodes if n.type == "OUTPUT_WORLD")
    nodes.remove(next(n for n in nodes if n.type == "BACKGROUND"))

    coords = nodes.new("ShaderNodeTexCoord")
    # El fondo de Blender tiene el centro de la imagen en +X; el panorama, en −Z del prototipo = +Y de Blender
    rotate = nodes.new("ShaderNodeVectorRotate")
    rotate.rotation_type = "Z_AXIS"
    rotate.inputs["Angle"].default_value = -math.pi / 2
    links.new(coords.outputs["Generated"], rotate.inputs["Vector"])
    env = nodes.new("ShaderNodeTexEnvironment")
    env.image = bpy.data.images.load(env_path, check_existing=True)
    links.new(rotate.outputs["Vector"], env.inputs["Vector"])

    # Hemisferio: mezcla por la altura. three.js da irradiancia; un fondo uniforme de radiancia L da π × L
    sep = nodes.new("ShaderNodeSeparateXYZ")
    links.new(coords.outputs["Generated"], sep.inputs[0])
    half = nodes.new("ShaderNodeMath")
    half.operation = "MULTIPLY_ADD"
    half.inputs[1].default_value = 0.5
    half.inputs[2].default_value = 0.5
    links.new(sep.outputs["Z"], half.inputs[0])
    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    k = hemi_strength / math.pi
    mix.inputs["A"].default_value = (hemi_ground[0] * k, hemi_ground[1] * k, hemi_ground[2] * k, 1.0)
    mix.inputs["B"].default_value = (hemi_sky[0] * k, hemi_sky[1] * k, hemi_sky[2] * k, 1.0)
    links.new(half.outputs[0], mix.inputs["Factor"])

    a = ambient_strength / math.pi
    amb = nodes.new("ShaderNodeRGB")
    amb.outputs[0].default_value = (ambient[0] * a, ambient[1] * a, ambient[2] * a, 1.0)
    add1 = nodes.new("ShaderNodeMix")
    add1.data_type = "RGBA"
    add1.blend_type = "ADD"
    add1.inputs["Factor"].default_value = 1.0
    links.new(env.outputs["Color"], add1.inputs["A"])
    links.new(mix.outputs["Result"], add1.inputs["B"])
    add2 = nodes.new("ShaderNodeMix")
    add2.data_type = "RGBA"
    add2.blend_type = "ADD"
    add2.inputs["Factor"].default_value = 1.0
    links.new(add1.outputs["Result"], add2.inputs["A"])
    links.new(amb.outputs[0], add2.inputs["B"])

    lit = nodes.new("ShaderNodeBackground")
    links.new(add2.outputs["Result"], lit.inputs["Color"])
    dark = nodes.new("ShaderNodeBackground")
    dark.inputs["Color"].default_value = background
    path = nodes.new("ShaderNodeLightPath")
    select = nodes.new("ShaderNodeMixShader")
    links.new(path.outputs["Is Camera Ray"], select.inputs[0])
    links.new(lit.outputs[0], select.inputs[1])
    links.new(dark.outputs[0], select.inputs[2])
    links.new(select.outputs[0], out.inputs["Surface"])
    scene.world = world


def preview_light_sun(collection, parent, source, target, strength):
    light = bpy.data.lights.new("PreviewKey", "SUN")
    light.energy = strength
    light.angle = math.radians(4.0)
    obj = bpy.data.objects.new("PreviewKey", light)
    collection.objects.link(obj)
    obj.parent = parent
    # Los focos de Blender miran hacia su −Z, y three.js mira con +Z hacia el objetivo: se le da la vuelta
    obj.matrix_basis = Matrix([list(r) for r in g.look_at(tuple(target), tuple(source))])
    return obj


def preview_light_point(name, collection, parent, position, color, candela, radius=0.01):
    light = bpy.data.lights.new(name, "POINT")
    light.color = color[:3]
    light.energy = candela * 4 * math.pi
    light.shadow_soft_size = radius
    obj = bpy.data.objects.new(name, light)
    collection.objects.link(obj)
    obj.parent = parent
    obj.location = position
    return obj


def render_preview(scene, collection, parent, path, eye, target, fov_degrees, width=1280, height=720):
    cam = bpy.data.cameras.new("PreviewCamera")
    cam.sensor_fit = "VERTICAL"
    cam.angle_y = math.radians(fov_degrees)
    cam.clip_start = 0.02
    cam.clip_end = 30.0
    obj = bpy.data.objects.new("PreviewCamera", cam)
    collection.objects.link(obj)
    obj.parent = parent
    # La cámara mira hacia su −Z: lookAt con el objetivo y el ojo cambiados
    m = g.look_at(tuple(eye), tuple(2 * e - t for e, t in zip(eye, target)))
    obj.matrix_basis = Matrix([list(r) for r in m])
    scene.camera = obj
    _render_png(scene, path, width, height)
    bpy.data.objects.remove(obj)
    bpy.data.cameras.remove(cam)


def export_glb(path, root):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = root
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=path,
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_normals=True,
        export_tangents=False,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_cameras=False,
        export_lights=False,
        export_animations=False,
    )
