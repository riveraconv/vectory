# Equivalente al objeto B del prototipo de BLADE (designs/cabina-3d-blade.html), sin Blender. No es el de VECTOR
# (builder.py): aquí todo pasa por boxUV, beam lleva la caja a lo largo de su Z, rbox limita el radio a la mitad del
# lado / 2,01 y las piezas cuelgan de grupos con posición, rotación XYZ o lookAt, como los Object3D de three.js.
# Cada pieza se apunta con su transformación respecto a la raíz de la cabina, su zona (el grupo de primer nivel) y
# su nombre del prototipo.
import math

import numpy as np

from . import geometry as g


class Group:
    def __init__(self, name, world, zone):
        self.name = name
        self.world = world
        self.zone = zone


class Piece:
    def __init__(self, soup, material, matrix, zone, name, no_shadow):
        self.soup = soup
        self.material = material
        self.matrix = matrix
        self.zone = zone
        self.name = name
        self.no_shadow = no_shadow


def _scale_matrix(s):
    m = np.identity(4)
    m[0, 0], m[1, 1], m[2, 2] = s
    return m


def _rotation_only(m):
    r = np.identity(4)
    r[:3, :3] = m[:3, :3]
    return r


class BladeBuilder:
    def __init__(self):
        self.pieces = []
        self.root = Group("CockpitBlade", g.identity(), None)

    # B.group: posición, luego rotación XYZ y, si hay objetivo, lookAt (su +Z apunta a un punto en coordenadas de la
    # raíz, como Object3D.lookAt con la cabina aún sin colgar de nada)
    def group(self, parent, name, pos=None, rot=None, look=None):
        local = g.translation(pos if pos is not None else (0.0, 0.0, 0.0))
        if rot is not None:
            local = local @ g.euler_xyz(rot)
        world = parent.world @ local
        if look is not None:
            position = tuple(world[:3, 3])
            facing = g.look_at(position, tuple(look))
            world = g.translation(position) @ _rotation_only(facing)
        zone = parent.zone if parent.zone is not None else name
        return Group(name, world, zone)

    # B.mesh con la posición, la rotación y la escala que el prototipo pone después a la malla
    def mesh(self, soup, material, parent, name, no_shadow=False, pos=None, rot=None, scale=None):
        local = g.translation(pos if pos is not None else (0.0, 0.0, 0.0))
        if rot is not None:
            local = local @ g.euler_xyz(rot)
        if scale is not None:
            local = local @ _scale_matrix(scale)
        piece = Piece(soup, material, parent.world @ local, parent.zone, name, no_shadow)
        self.pieces.append(piece)
        return piece

    @staticmethod
    def poly(points):
        return g.poly(points)

    @staticmethod
    def strip(a, b):
        return g.strip_ab(a, b)

    @staticmethod
    def rbox(w, h, d, r=0.004):
        return g.rounded_box(w, h, d, min(r, w / 2.01, h / 2.01, d / 2.01))

    # Caja redondeada entre dos puntos: Z de p0 a p1; T = toward − punto medio; X = T × Z; Y = Z × X
    def beam(self, p0, p1, w, h, toward, material, parent, name, no_shadow=False):
        a, b = tuple(p0), tuple(p1)
        length = g._length(g._sub(b, a))
        z = g._normalize(g._sub(b, a))
        mid = g._scale(g._add(a, b), 0.5)
        t = g._sub(tuple(toward), mid)
        x = g._normalize(g._cross(t, z))
        y = g._normalize(g._cross(z, x))
        soup = self.rbox(w, h, length, min(w, h) * 0.3)
        piece = Piece(soup, material, parent.world @ g.from_basis(x, y, z, mid), parent.zone, name, no_shadow)
        self.pieces.append(piece)
        return piece

    def tube(self, points, radius, seg, material, parent, name, no_shadow=False):
        return self.mesh(g.box_uv(g.tube(points, radius, seg, 10)), material, parent, name, no_shadow)

    def cyl(self, rt, rb, h, n, material, parent, name, no_shadow=False, pos=None, rot=None):
        return self.mesh(g.box_uv(g.cylinder(rt, rb, h, n)), material, parent, name, no_shadow, pos, rot)

    def sphere(self, r, material, parent, name, no_shadow=False, pos=None, scale=None):
        return self.mesh(g.box_uv(g.sphere(r, 10, 8)), material, parent, name, no_shadow, pos, None, scale)

    # Tornillo del prototipo: cilindro de 8 lados girado para mirar hacia el piloto
    def screw(self, material, parent, x, y, z, r=0.0035):
        return self.cyl(r, r, 0.003, 8, material, parent, "Screw", True, (x, y, z), (math.pi / 2, 0.0, 0.0))


# Normales con la inversa traspuesta: los remaches llevan escala en Y
def transformed(soup, m):
    normal_matrix = np.linalg.inv(m[:3, :3]).T
    normals = []
    for n in soup.normals:
        v = normal_matrix @ np.array(n)
        l = float(np.linalg.norm(v))
        normals.append(tuple(v / l) if l > 0 else (0.0, 0.0, 0.0))
    return g.Soup([g.transform_point(m, p) for p in soup.positions], normals, soup.uvs)
