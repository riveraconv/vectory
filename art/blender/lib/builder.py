# Equivalente a makeBuilder del prototipo, sin Blender: B.poly, B.strip, B.rbox, B.beam, B.tube, B.light, B.led y
# B.vent. Cada llamada apunta una pieza con su material, su transformación respecto a la raíz de la cabina y su zona.
# Las piezas con nombre (las vivas del contrato con Godot) se quedan solas; el resto se junta luego por zona y material.
from . import geometry as g


class Piece:
    def __init__(self, soup, material, matrix, zone, name=None, no_shadow=False):
        self.soup = soup
        self.material = material
        self.matrix = matrix
        self.zone = zone
        self.name = name
        self.no_shadow = no_shadow


class Builder:
    def __init__(self):
        self.pieces = []
        self.zone = "Dash"

    def add(self, soup, material, matrix=None, name=None, no_shadow=False, zone=None):
        piece = Piece(soup, material, g.identity() if matrix is None else matrix, zone or self.zone, name, no_shadow)
        self.pieces.append(piece)
        return piece

    def poly(self, points, material, parent=None, name=None, no_shadow=False):
        return self.add(g.poly(points), material, parent, name, no_shadow)

    def strip(self, a, b, material, parent=None):
        return self.add(g.strip(a, b), material, parent)

    def rbox(self, w, h, d, material, pos, rot=None, parent=None, r=None, name=None):
        radius = r if r is not None else min(0.006, min(w, h, d) / 2 - 0.0005)
        m = g.translation(pos)
        if rot is not None:
            m = m @ g.euler_xyz(rot)
        if parent is not None:
            m = parent @ m
        return self.add(g.rounded_box(w, h, d, max(0.0005, radius)), material, m, name)

    def beam(self, p0, p1, w, d, material, toward=(0.0, 0.0, 0.3), r=None):
        a, b = tuple(p0), tuple(p1)
        y = g._sub(b, a)
        length = g._length(y)
        y = g._normalize(y)
        mid = g._scale(g._add(a, b), 0.5)
        z = g._normalize(g._sub(tuple(toward), mid))
        x = g._normalize(g._cross(y, z))
        z = g._normalize(g._cross(x, y))
        radius = r if r is not None else min(0.01, min(w, d) / 2 - 0.001)
        return self.add(g.rounded_box(w, length, d, radius), material, g.from_basis(x, y, z, mid))

    def tube(self, points, radius, material, parent=None, seg=48):
        return self.add(g.tube(points, radius, seg), material, parent)

    def light(self, points, material, name, radius=0.0022, parent=None):
        return self.add(g.tube(points, radius, 32), material, parent, name, no_shadow=True)

    def led(self, pos, material, name, radius=0.0035, parent=None):
        m = g.translation(pos)
        if parent is not None:
            m = parent @ m
        return self.add(g.sphere(radius), material, m, name, no_shadow=True)

    # Rejilla de ventilación con lamas, orientada con lookAt como en el prototipo
    def vent(self, pos, w, h, n, material, look, slat, dark):
        group = g.look_at(tuple(pos), tuple(look))
        self.rbox(w + 0.016, h + 0.016, 0.012, material, (0, 0, -0.004), None, group, 0.003)
        self.rbox(w, h, 0.008, dark, (0, 0, 0.0025), None, group, 0.001)
        for i in range(n):
            self.rbox(w - 0.004, max(0.003, h / n * 0.55), 0.004, slat,
                      (0, -h / 2 + (i + 0.5) * h / n, 0.006), (0.6, 0, 0), group, 0.0008)
