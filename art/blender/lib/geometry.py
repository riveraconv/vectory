# Geometría sin Blender: repite las mismas operaciones que three.js (RoundedBoxGeometry, TubeGeometry,
# CatmullRomCurve3, SphereGeometry, CylinderGeometry, PlaneGeometry, Object3D.lookAt...) para que las piezas
# salgan iguales que en el prototipo. Cada malla es una "sopa" de triángulos: 3 vértices por triángulo,
# con su normal y su UV.
import math

import numpy as np


class Soup:
    def __init__(self, positions, normals, uvs):
        self.positions = positions  # lista de (x, y, z), de 3 en 3
        self.normals = normals
        self.uvs = uvs

    @property
    def triangles(self):
        return len(self.positions) // 3


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def _scale(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _length(a):
    return math.sqrt(_dot(a, a))


def _normalize(a):
    l = _length(a)
    return (a[0] / l, a[1] / l, a[2] / l) if l > 0 else (0.0, 0.0, 0.0)


# ---------- Matrices (4 × 4, convención de columnas, como three.js) ----------

def identity():
    return np.identity(4)


def translation(pos):
    m = np.identity(4)
    m[:3, 3] = pos
    return m


# Rotación en el orden XYZ de three.js
def euler_xyz(rot):
    x, y, z = rot
    a, b = math.cos(x), math.sin(x)
    c, d = math.cos(y), math.sin(y)
    e, f = math.cos(z), math.sin(z)
    m = np.identity(4)
    m[0, 0], m[0, 1], m[0, 2] = c * e, -c * f, d
    m[1, 0], m[1, 1], m[1, 2] = a * f + b * e * d, a * e - b * f * d, -b * c
    m[2, 0], m[2, 1], m[2, 2] = b * f - a * e * d, b * e + a * f * d, a * c
    return m


def from_basis(x, y, z, pos=(0.0, 0.0, 0.0)):
    m = np.identity(4)
    m[:3, 0], m[:3, 1], m[:3, 2], m[:3, 3] = x, y, z, pos
    return m


# Object3D.lookAt en un objeto que no es cámara: su +Z apunta al objetivo
def look_at(pos, target, up=(0.0, 1.0, 0.0)):
    z = _sub(target, pos)
    if _dot(z, z) == 0.0:
        z = (0.0, 0.0, 1.0)
    z = _normalize(z)
    x = _cross(up, z)
    if _dot(x, x) == 0.0:
        z2 = (z[0] + 0.0001, z[1], z[2]) if abs(up[2]) == 1.0 else (z[0], z[1], z[2] + 0.0001)
        z = _normalize(z2)
        x = _cross(up, z)
    x = _normalize(x)
    y = _cross(z, x)
    return from_basis(x, y, z, pos)


def transform_point(m, p):
    return (
        m[0, 0] * p[0] + m[0, 1] * p[1] + m[0, 2] * p[2] + m[0, 3],
        m[1, 0] * p[0] + m[1, 1] * p[1] + m[1, 2] * p[2] + m[1, 3],
        m[2, 0] * p[0] + m[2, 1] * p[1] + m[2, 2] * p[2] + m[2, 3],
    )


def transform_direction(m, d):
    return (
        m[0, 0] * d[0] + m[0, 1] * d[1] + m[0, 2] * d[2],
        m[1, 0] * d[0] + m[1, 1] * d[1] + m[1, 2] * d[2],
        m[2, 0] * d[0] + m[2, 1] * d[1] + m[2, 2] * d[2],
    )


def transformed(soup, m):
    return Soup(
        [transform_point(m, p) for p in soup.positions],
        [transform_direction(m, n) for n in soup.normals],
        soup.uvs,
    )


# ---------- UV ----------

# boxUV del prototipo: UV en metros según el eje dominante de la normal de cada cara. Coordenadas locales de la malla.
def box_uv(soup):
    uvs = []
    p = soup.positions
    for i in range(0, len(p), 3):
        a, b, c = p[i], p[i + 1], p[i + 2]
        n = _cross(_sub(b, a), _sub(c, a))
        ax, ay, az = abs(n[0]), abs(n[1]), abs(n[2])
        for v in (a, b, c):
            if ax >= ay and ax >= az:
                uvs.append((v[2], v[1]))
            elif ay >= az:
                uvs.append((v[0], v[2]))
            else:
                uvs.append((v[0], v[1]))
    return Soup(soup.positions, soup.normals, uvs)


def _flat(positions):
    normals = []
    for i in range(0, len(positions), 3):
        a, b, c = positions[i], positions[i + 1], positions[i + 2]
        n = _normalize(_cross(_sub(c, b), _sub(a, b)))
        normals += [n, n, n]
    return Soup(positions, normals, [(0.0, 0.0)] * len(positions))


def _indexed(vertices, normals, uvs, indices):
    return Soup([vertices[i] for i in indices], [normals[i] for i in indices], [uvs[i] for i in indices])


# ---------- Mallas ----------

# B.poly: polígono plano en abanico desde el primer punto
def poly(points):
    pos = []
    for i in range(1, len(points) - 1):
        pos += [tuple(points[0]), tuple(points[i]), tuple(points[i + 1])]
    return box_uv(_flat(pos))


# B.strip: tira de cuadriláteros entre dos polilíneas
def strip(a, b):
    pos = []
    for i in range(len(a) - 1):
        pos += [tuple(a[i]), tuple(b[i]), tuple(a[i + 1]), tuple(a[i + 1]), tuple(b[i]), tuple(b[i + 1])]
    return box_uv(_flat(pos))


def _box_plane(u, v, w, udir, vdir, width, height, depth, grid):
    vertices, indices = [], []
    for iy in range(grid + 1):
        y = iy * (height / grid) - height / 2
        for ix in range(grid + 1):
            x = ix * (width / grid) - width / 2
            vec = [0.0, 0.0, 0.0]
            vec[u], vec[v], vec[w] = x * udir, y * vdir, depth / 2
            vertices.append(tuple(vec))
    row = grid + 1
    for iy in range(grid):
        for ix in range(grid):
            a = ix + row * iy
            b = ix + row * (iy + 1)
            c = ix + 1 + row * (iy + 1)
            d = ix + 1 + row * iy
            indices += [a, b, d, b, c, d]
    return [vertices[i] for i in indices]


def _sign(x):
    return (x > 0) - (x < 0)


# RoundedBoxGeometry de three.js, ya con box_uv: es lo que hace B.rbox y B.beam
def rounded_box(w, h, d, radius, segments=2):
    grid = segments * 2 + 1
    radius = min(w / 2, h / 2, d / 2, radius)
    cube = (
        _box_plane(2, 1, 0, -1, -1, 1, 1, 1, grid)
        + _box_plane(2, 1, 0, 1, -1, 1, 1, -1, grid)
        + _box_plane(0, 2, 1, 1, 1, 1, 1, 1, grid)
        + _box_plane(0, 2, 1, 1, -1, 1, 1, -1, grid)
        + _box_plane(0, 1, 2, 1, -1, 1, 1, 1, grid)
        + _box_plane(0, 1, 2, -1, -1, 1, 1, -1, grid)
    )
    half = (w / 2 - radius, h / 2 - radius, d / 2 - radius)
    half_segment = 0.5 / grid
    positions, normals = [], []
    for p in cube:
        n = _normalize((
            p[0] - _sign(p[0]) * half_segment,
            p[1] - _sign(p[1]) * half_segment,
            p[2] - _sign(p[2]) * half_segment,
        ))
        positions.append((
            half[0] * _sign(p[0]) + n[0] * radius,
            half[1] * _sign(p[1]) + n[1] * radius,
            half[2] * _sign(p[2]) + n[2] * radius,
        ))
        normals.append(n)
    return box_uv(Soup(positions, normals, [(0.0, 0.0)] * len(positions)))


# CatmullRomCurve3 de three.js: centrípeta, abierta, con su parametrización por longitud de arco
class CatmullRom:
    ARC_DIVISIONS = 200

    def __init__(self, points):
        self.points = [tuple(p) for p in points]
        self._lengths = None

    @staticmethod
    def _coefficients(x0, x1, x2, x3, dt0, dt1, dt2):
        t1 = (x1 - x0) / dt0 - (x2 - x0) / (dt0 + dt1) + (x2 - x1) / dt1
        t2 = (x2 - x1) / dt1 - (x3 - x1) / (dt1 + dt2) + (x3 - x2) / dt2
        t1 *= dt1
        t2 *= dt1
        return x1, t1, -3 * x1 + 3 * x2 - 2 * t1 - t2, 2 * x1 - 2 * x2 + t1 + t2

    def point(self, t):
        pts = self.points
        l = len(pts)
        p = (l - 1) * t
        index = math.floor(p)
        weight = p - index
        if weight == 0 and index == l - 1:
            index = l - 2
            weight = 1.0
        p0 = pts[index - 1] if index > 0 else _add(_sub(pts[0], pts[1]), pts[0])
        p1 = pts[index % l]
        p2 = pts[(index + 1) % l]
        p3 = pts[index + 2] if index + 2 < l else _add(_sub(pts[l - 1], pts[l - 2]), pts[l - 1])
        dt0 = _dot(_sub(p0, p1), _sub(p0, p1)) ** 0.25
        dt1 = _dot(_sub(p1, p2), _sub(p1, p2)) ** 0.25
        dt2 = _dot(_sub(p2, p3), _sub(p2, p3)) ** 0.25
        if dt1 < 1e-4:
            dt1 = 1.0
        if dt0 < 1e-4:
            dt0 = dt1
        if dt2 < 1e-4:
            dt2 = dt1
        out = []
        for c in range(3):
            c0, c1, c2, c3 = self._coefficients(p0[c], p1[c], p2[c], p3[c], dt0, dt1, dt2)
            out.append(c0 + c1 * weight + c2 * weight * weight + c3 * weight * weight * weight)
        return tuple(out)

    def lengths(self):
        if self._lengths is None:
            self._lengths = [0.0]
            last = self.point(0.0)
            total = 0.0
            for i in range(1, self.ARC_DIVISIONS + 1):
                current = self.point(i / self.ARC_DIVISIONS)
                total += _length(_sub(current, last))
                self._lengths.append(total)
                last = current
        return self._lengths

    def u_to_t(self, u):
        arcs = self.lengths()
        count = len(arcs)
        target = u * arcs[count - 1]
        low, high = 0, count - 1
        while low <= high:
            i = math.floor(low + (high - low) / 2)
            diff = arcs[i] - target
            if diff < 0:
                low = i + 1
            elif diff > 0:
                high = i - 1
            else:
                high = i
                break
        i = high
        if arcs[i] == target:
            return i / (count - 1)
        fraction = (target - arcs[i]) / (arcs[i + 1] - arcs[i])
        return (i + fraction) / (count - 1)

    def point_at(self, u):
        return self.point(self.u_to_t(u))

    def tangent_at(self, u):
        t = self.u_to_t(u)
        t1 = max(0.0, t - 0.0001)
        t2 = min(1.0, t + 0.0001)
        return _normalize(_sub(self.point(t2), self.point(t1)))


def _rotate_around(v, axis, angle):
    c, s = math.cos(angle), math.sin(angle)
    return _add(_add(_scale(v, c), _scale(_cross(axis, v), s)), _scale(axis, _dot(axis, v) * (1 - c)))


# TubeGeometry de three.js (B.tube): marcos de Frenet iguales, sin tapas y con sus UV de 0 a 1
def tube(points, radius, tubular=48, radial=10):
    curve = CatmullRom(points)
    tangents = [curve.tangent_at(i / tubular) for i in range(tubular + 1)]
    t0 = tangents[0]
    least = float("inf")
    normal = (0.0, 0.0, 0.0)
    if abs(t0[0]) <= least:
        least = abs(t0[0])
        normal = (1.0, 0.0, 0.0)
    if abs(t0[1]) <= least:
        least = abs(t0[1])
        normal = (0.0, 1.0, 0.0)
    if abs(t0[2]) <= least:
        normal = (0.0, 0.0, 1.0)
    vec = _normalize(_cross(t0, normal))
    normals = [_cross(t0, vec)]
    binormals = [_cross(t0, normals[0])]
    for i in range(1, tubular + 1):
        n = normals[i - 1]
        vec = _cross(tangents[i - 1], tangents[i])
        if _length(vec) > 2.220446049250313e-16:
            vec = _normalize(vec)
            theta = math.acos(max(-1.0, min(1.0, _dot(tangents[i - 1], tangents[i]))))
            n = _rotate_around(n, vec, theta)
        normals.append(n)
        binormals.append(_cross(tangents[i], n))
    verts, norms, uvs, indices = [], [], [], []
    for i in range(tubular + 1):
        center = curve.point_at(i / tubular)
        for j in range(radial + 1):
            v = j / radial * math.pi * 2
            sin, cos = math.sin(v), -math.cos(v)
            n = _normalize(_add(_scale(normals[i], cos), _scale(binormals[i], sin)))
            verts.append(_add(center, _scale(n, radius)))
            norms.append(n)
            uvs.append((i / tubular, j / radial))
    for j in range(1, tubular + 1):
        for i in range(1, radial + 1):
            a = (radial + 1) * (j - 1) + (i - 1)
            b = (radial + 1) * j + (i - 1)
            c = (radial + 1) * j + i
            d = (radial + 1) * (j - 1) + i
            indices += [a, b, d, b, c, d]
    return _indexed(verts, norms, uvs, indices)


# SphereGeometry de three.js (LEDs): 10 × 8 por defecto
def sphere(radius, width=10, height=8):
    verts, norms, uvs, grid, indices = [], [], [], [], []
    for iy in range(height + 1):
        row = []
        v = iy / height
        for ix in range(width + 1):
            u = ix / width
            p = (
                -radius * math.cos(u * math.pi * 2) * math.sin(v * math.pi),
                radius * math.cos(v * math.pi),
                radius * math.sin(u * math.pi * 2) * math.sin(v * math.pi),
            )
            verts.append(p)
            norms.append(_normalize(p))
            uvs.append((u, 1 - v))
            row.append(len(verts) - 1)
        grid.append(row)
    for iy in range(height):
        for ix in range(width):
            a, b = grid[iy][ix + 1], grid[iy][ix]
            c, d = grid[iy + 1][ix], grid[iy + 1][ix + 1]
            if iy != 0:
                indices += [a, b, d]
            if iy != height - 1:
                indices += [b, c, d]
    return _indexed(verts, norms, uvs, indices)


# SphereGeometry de three.js con todos sus parámetros: un trozo de esfera (cúpula del cristal de BLADE).
# x = −r·cos φ·sen θ, y = r·cos θ, z = r·sen φ·sen θ, con φ y θ desde su inicio y a lo largo de su recorrido.
def sphere_section(radius, width, height, phi_start, phi_length, theta_start, theta_length):
    theta_end = min(theta_start + theta_length, math.pi)
    verts, norms, uvs, grid, indices = [], [], [], [], []
    for iy in range(height + 1):
        row = []
        v = iy / height
        u_offset = 0.0
        if iy == 0 and theta_start == 0:
            u_offset = 0.5 / width
        elif iy == height and theta_end == math.pi:
            u_offset = -0.5 / width
        for ix in range(width + 1):
            u = ix / width
            phi = phi_start + u * phi_length
            theta = theta_start + v * theta_length
            p = (
                -radius * math.cos(phi) * math.sin(theta),
                radius * math.cos(theta),
                radius * math.sin(phi) * math.sin(theta),
            )
            verts.append(p)
            norms.append(_normalize(p))
            uvs.append((u + u_offset, 1 - v))
            row.append(len(verts) - 1)
        grid.append(row)
    for iy in range(height):
        for ix in range(width):
            a, b = grid[iy][ix + 1], grid[iy][ix]
            c, d = grid[iy + 1][ix], grid[iy + 1][ix + 1]
            if iy != 0 or theta_start > 0:
                indices += [a, b, d]
            if iy != height - 1 or theta_end < math.pi:
                indices += [b, c, d]
    return _indexed(verts, norms, uvs, indices)


# B.strip del prototipo de BLADE: los mismos cuadriláteros que strip, partidos por la otra diagonal
def strip_ab(a, b):
    pos = []
    for i in range(len(a) - 1):
        pos += [tuple(a[i]), tuple(b[i]), tuple(b[i + 1]), tuple(a[i]), tuple(b[i + 1]), tuple(a[i + 1])]
    return box_uv(_flat(pos))


# CylinderGeometry de three.js (mandos y botones), con tapas
def cylinder(radius_top, radius_bottom, height, radial):
    verts, norms, uvs, indices = [], [], [], []
    slope = (radius_bottom - radius_top) / height
    grid = []
    for y in range(2):
        row = []
        r = y * (radius_bottom - radius_top) + radius_top
        for x in range(radial + 1):
            u = x / radial
            sin, cos = math.sin(u * math.pi * 2), math.cos(u * math.pi * 2)
            verts.append((r * sin, -y * height + height / 2, r * cos))
            norms.append(_normalize((sin, slope, cos)))
            uvs.append((u, 1 - y))
            row.append(len(verts) - 1)
        grid.append(row)
    for x in range(radial):
        a, b, c, d = grid[0][x], grid[1][x], grid[1][x + 1], grid[0][x + 1]
        indices += [a, b, d, b, c, d]
    for top in (True, False):
        sign = 1 if top else -1
        r = radius_top if top else radius_bottom
        center_start = len(verts)
        for _ in range(radial):
            verts.append((0.0, height / 2 * sign, 0.0))
            norms.append((0.0, float(sign), 0.0))
            uvs.append((0.5, 0.5))
        ring_start = len(verts)
        for x in range(radial + 1):
            theta = x / radial * math.pi * 2
            cos, sin = math.cos(theta), math.sin(theta)
            verts.append((r * sin, height / 2 * sign, r * cos))
            norms.append((0.0, float(sign), 0.0))
            uvs.append((cos * 0.5 + 0.5, sin * 0.5 * sign + 0.5))
        for x in range(radial):
            c, i = center_start + x, ring_start + x
            indices += [i, i + 1, c] if top else [i + 1, i, c]
    return _indexed(verts, norms, uvs, indices)


# PlaneGeometry de three.js: UV 0-1 entero, normal +Z
def plane(width, height):
    verts, norms, uvs, indices = [], [], [], []
    for iy in range(2):
        for ix in range(2):
            verts.append((ix * width - width / 2, -(iy * height - height / 2), 0.0))
            norms.append((0.0, 0.0, 1.0))
            uvs.append((float(ix), float(1 - iy)))
    indices = [0, 2, 1, 2, 3, 1]
    return _indexed(verts, norms, uvs, indices)


# TorusGeometry de three.js (jaulas de las balizas y abrazaderas de HAMMER), con su arco. Sin boxUV: UV 0-1
def torus(radius, tube, radial, tubular, arc=math.pi * 2):
    verts, norms, uvs, indices = [], [], [], []
    for j in range(radial + 1):
        for i in range(tubular + 1):
            u = i / tubular * arc
            v = j / radial * math.pi * 2
            p = (
                (radius + tube * math.cos(v)) * math.cos(u),
                (radius + tube * math.cos(v)) * math.sin(u),
                tube * math.sin(v),
            )
            center = (radius * math.cos(u), radius * math.sin(u), 0.0)
            verts.append(p)
            norms.append(_normalize(_sub(p, center)))
            uvs.append((i / tubular, j / radial))
    for j in range(1, radial + 1):
        for i in range(1, tubular + 1):
            a = (tubular + 1) * j + i - 1
            b = (tubular + 1) * (j - 1) + i - 1
            c = (tubular + 1) * (j - 1) + i
            d = (tubular + 1) * j + i
            indices += [a, b, d, b, c, d]
    return _indexed(verts, norms, uvs, indices)


# CircleGeometry de three.js (esferas de los relojes pequeños de HAMMER): UV 0-1 entero, normal +Z
def circle(radius, segments):
    verts, norms, uvs, indices = [(0.0, 0.0, 0.0)], [(0.0, 0.0, 1.0)], [(0.5, 0.5)], []
    for s in range(segments + 1):
        a = s / segments * math.pi * 2
        verts.append((radius * math.cos(a), radius * math.sin(a), 0.0))
        norms.append((0.0, 0.0, 1.0))
        uvs.append(((math.cos(a) + 1) / 2, (math.sin(a) + 1) / 2))
    for i in range(1, segments + 1):
        indices += [i, i + 1, 0]
    return _indexed(verts, norms, uvs, indices)
