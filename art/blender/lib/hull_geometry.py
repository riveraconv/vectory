# Geometría del casco 3D de VECTOR (spec docs/cascos-3d-1-vector.md, 3.4), sin Blender: traducción de las piezas de
# designs/casco-3d-vector.html (prof, herm, fusPt, fusPatch, fusTopY, canopyGeo, fusCap, wingGeo, endplateGeo, lathe,
# octRing, secAt, octLoft, octAnnulus, octFan, rrect y el tubo cerrado de la junta), con los mismos números. Las mallas
# indexadas sacan sus normales como computeVertexNormals de three.js: suaves donde comparten vértice, vivas donde no.
# Devuelve las mismas sopas de triángulos que lib/geometry.py; las cabinas no usan nada de aquí.
import math

from . import geometry as g

# ---------- Utilidades ----------


def js_round(x):
    return math.floor(x + 0.5)


def _sign(x):
    return (x > 0) - (x < 0)


def _lerp3(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


# geoFrom del prototipo: malla indexada con las normales de computeVertexNormals (suma de (C − B) × (A − B) por vértice)
def indexed(positions, uvs, indices):
    sums = [[0.0, 0.0, 0.0] for _ in positions]
    for i in range(0, len(indices), 3):
        a, b, c = indices[i], indices[i + 1], indices[i + 2]
        pa, pb, pc = positions[a], positions[b], positions[c]
        n = g._cross(g._sub(pc, pb), g._sub(pa, pb))
        for v in (a, b, c):
            s = sums[v]
            s[0] += n[0]
            s[1] += n[1]
            s[2] += n[2]
    normals = [g._normalize(tuple(s)) for s in sums]
    return g._indexed([tuple(p) for p in positions], normals, [tuple(u) for u in uvs], indices)


# Sopa con una normal plana por triángulo (geometrías no indexadas de three.js: BoxGeometry, ExtrudeGeometry…)
def flat(positions, uvs):
    normals = []
    for i in range(0, len(positions), 3):
        a, b, c = positions[i], positions[i + 1], positions[i + 2]
        n = g._normalize(g._cross(g._sub(c, b), g._sub(a, b)))
        normals += [n, n, n]
    return g.Soup([tuple(p) for p in positions], normals, [tuple(u) for u in uvs])


# ---------- Fuselaje: las 4 secciones en rombo del wireframe y la punta del morro ----------

Z0, Z1 = -4.0, 2.6
PZ = [-2.5, -0.5, 1.5, 2.6]
PV = {"hw": [0.5, 0.9, 0.8, 0.7], "top": [0.35, 0.6, 0.55, 0.5], "bot": [-0.2, -0.3, -0.3, -0.3], "side": [0.05, 0.1, 0.1, 0.1]}
N_NOCTURNA = 1.7


# Hermite monótona entre las secciones (tangentes de herm(), con el recorte de Fritsch-Carlson)
def herm(vals, z):
    i = 0
    while i < len(PZ) - 2 and z > PZ[i + 1]:
        i += 1
    z0, z1 = PZ[i], PZ[i + 1]
    h = z1 - z0
    t = min(1.0, max(0.0, (z - z0) / h))
    n = len(PZ)

    def tan(k):
        if k == 0:
            return 0.0
        if k == n - 1:
            return (vals[k] - vals[k - 1]) / (PZ[k] - PZ[k - 1])
        return (vals[k + 1] - vals[k - 1]) / (PZ[k + 1] - PZ[k - 1])

    m0, m1 = tan(i), tan(i + 1)
    d = (vals[i + 1] - vals[i]) / h
    if d == 0:
        m0 = m1 = 0.0
    else:
        if m0 / d < 0:
            m0 = 0.0
        if m1 / d < 0:
            m1 = 0.0
        a, b = m0 / d, m1 / d
        s = a * a + b * b
        if s > 9:
            k = 3 / math.sqrt(s)
            m0 = k * a * d
            m1 = k * b * d
    t2 = t * t
    t3 = t2 * t
    return ((2 * t3 - 3 * t2 + 1) * vals[i] + (t3 - 2 * t2 + t) * h * m0
            + (-2 * t3 + 3 * t2) * vals[i + 1] + (t3 - t2) * h * m1)


def prof(z):
    if z < PZ[0]:
        t = max(0.0, (z - Z0) / (PZ[0] - Z0))
        e = 1 - (1 - t) * (1 - t)
        return {k: PV[k][0] * e for k in PV}
    return {k: herm(PV[k], z) for k in PV}


# Sección en superelipse de exponente n entre los 4 puntos del rombo. θ = 0 derecha, π/2 arriba, π izquierda.
def fus_pt(z, th, n, off=0.0):
    p = prof(z)
    c, s, e = math.cos(th), math.sin(th), 2 / n
    x = p["hw"] * _sign(c) * abs(c) ** e
    y = p["side"] + (p["top"] - p["side"] if s >= 0 else p["side"] - p["bot"]) * _sign(s) * abs(s) ** e
    if not off:
        return (x, y, z)
    cy = (p["top"] + p["bot"]) / 2
    dx, dy = x, y - cy
    l = math.hypot(dx, dy) or 1.0
    return (x + dx / l * off, y + dy / l * off, z)


def z_of_u(u):
    return Z0 + (Z1 - Z0) * u ** 1.5


def u_of_z(z):
    return ((z - Z0) / (Z1 - Z0)) ** (1 / 1.5)


# Radio medio de la sección a esa z: lo usan las UV en metros de la piel
def mean_radius(z):
    p = prof(z)
    return (p["hw"] + (p["top"] - p["bot"]) / 2) / 2


# Trozo de piel entre θ0–θ1 y z0–z1. UV en metros (z, θ × radio medio) o las de uv_fn(u, v), de 0 a 1
def fus_patch(t0, t1, z0, z1, n, off=0.0, uv_fn=None):
    u0, u1 = u_of_z(z0), u_of_z(z1)
    nz = max(4, js_round(70 * (u1 - u0)))
    nt = max(3, js_round(64 * (t1 - t0) / (math.pi * 2)))
    pos, uv, idx = [], [], []
    for i in range(nz + 1):
        z = z_of_u(u0 + (u1 - u0) * i / nz)
        rr = mean_radius(z)
        for j in range(nt + 1):
            th = t0 + (t1 - t0) * j / nt
            pos.append(fus_pt(z, th, n, off))
            uv.append(uv_fn(i / nz, j / nt) if uv_fn else (z, th * rr))
    for i in range(nz):
        for j in range(nt):
            a = i * (nt + 1) + j
            b = a + nt + 1
            idx += [a, b, a + 1, a + 1, b, b + 1]
    return indexed(pos, uv, idx)


# Altura de la piel de arriba en (x, z): la inversa de la superelipse
def fus_top_y(x, z, n):
    p = prof(z)
    e = 2 / n
    r = min(1.0, abs(x) / max(1e-6, p["hw"]))
    c = r ** (1 / e)
    s = math.sqrt(max(0.0, 1 - c * c))
    return p["side"] + (p["top"] - p["side"]) * s ** e


CAN = {"A": 0.34, "B": 1.15, "zc": -0.85, "H": 0.11}


# Cúpula: planta elíptica; altura = piel + 0,002 + H·(1 − r²)^0,6, así el borde toca el lomo
def canopy(n):
    nr, np_ = 16, 72
    pos, uv, idx = [], [], []
    for i in range(nr + 1):
        for j in range(np_ + 1):
            r = i / nr
            f = j / np_ * math.pi * 2
            x = CAN["A"] * r * math.sin(f)
            z = CAN["zc"] + CAN["B"] * r * math.cos(f)
            pos.append((x, fus_top_y(x, z, n) + 0.002 + CAN["H"] * max(0.0, 1 - r * r) ** 0.6, z))
            uv.append((x, z))
    for i in range(nr):
        for j in range(np_):
            a = i * (np_ + 1) + j
            b = a + np_ + 1
            idx += [a, b, a + 1, a + 1, b, b + 1]
    return indexed(pos, uv, idx)


# Anillo del borde de la cúpula para la junta, 4 mm por encima de la piel
def canopy_ring(n, count=96, lift=0.004):
    ring = []
    for i in range(count):
        f = i / count * math.pi * 2
        x = CAN["A"] * math.sin(f)
        z = CAN["zc"] + CAN["B"] * math.cos(f)
        ring.append((x, fus_top_y(x, z, n) + lift, z))
    return ring


# Tapa trasera en abanico
def fus_cap(n):
    p = prof(Z1)
    cy = (p["top"] + p["bot"]) / 2
    nt = 64
    pos, uv, idx = [(0.0, cy, Z1)], [(0.0, cy)], []
    for j in range(nt + 1):
        q = fus_pt(Z1, j / nt * math.pi * 2, n, 0.0)
        pos.append(q)
        uv.append((q[0], q[1]))
    for j in range(1, nt + 1):
        idx += [0, j, j + 1]
    return indexed(pos, uv, idx)


# Las franjas del lomo se cortan donde está la cúpula
def cut_canopy(z, cut):
    if not cut:
        return [z]
    a = CAN["zc"] - CAN["B"] - 0.03
    b = CAN["zc"] + CAN["B"] + 0.03
    out = []
    if z[0] < a:
        out.append((z[0], min(a, z[1])))
    if z[1] > b:
        out.append((max(b, z[0]), z[1]))
    return out


# ---------- Ala: planta del wireframe con la punta en x = ±S, perfil NACA simétrico ----------

def naca(x):
    return 0.2969 * math.sqrt(x) - 0.126 * x - 0.3516 * x * x + 0.2843 * x ** 3 - 0.1036 * x ** 4


def wing_corners(k, s):
    return (k * 0.9, 0.1, -0.5), (k * 0.8, 0.1, 2.0), (k * s, -0.05, 2.2), (k * s, -0.05, 2.7)


def wing(k, s, ta=None, tb=None, off=0.0):
    rf, rr, tf, tr = wing_corners(k, s)
    # la raíz se prolonga hacia dentro del fuselaje (hasta x 0,5): sin hueco en el encaje
    t0 = (0.5 - 0.9) / (s - 0.9)
    ns, nc = 12, 16
    cs = [(1 - math.cos(math.pi * j / (nc - 1))) / 2 for j in range(nc)]
    loop = [(c, 1) for c in cs] + [(c, -1) for c in reversed(cs[1:nc - 1])]
    l = len(loop)
    pos, uv, idx = [], [], []
    for i in range(ns + 1):
        t = t0 + (1 - t0) * i / ns if ta is None else ta + (tb - ta) * i / ns
        tc = max(0.0, t)
        le = _lerp3(rf, tf, t)
        te = _lerp3(rr, tr, t)
        th = 0.16 * (1 - tc) + 0.045 * tc
        for c, sg in loop:
            p = _lerp3(le, te, c)
            p = (p[0], p[1] + sg * (th / 2 * naca(c) / 0.05 + off), p[2])
            pos.append(p)
            uv.append((p[2], p[0]))
    for i in range(ns):
        for j in range(l):
            a = i * l + j
            b = a + l
            a1 = i * l + (j + 1) % l
            b1 = a1 + l
            idx += [a, b, a1, a1, b, b1]
    return indexed(pos, uv, idx)


# Borde de ataque en t (recta de rf a tf)
def wing_le(k, s, t):
    rf, _, tf, _ = wing_corners(k, s)
    return _lerp3(rf, tf, t)


# ---------- Formas planas y extrusiones (Shape, ShapeGeometry y ExtrudeGeometry de three.js) ----------

def _signed_area(pts):
    a = 0.0
    for i in range(len(pts)):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % len(pts)]
        a += x0 * y1 - x1 * y0
    return a / 2


# Desplazamiento en inglete hacia fuera de un contorno convexo en sentido antihorario (getBevelVec). Con capped, como
# three.js: un inglete de más de √2 se acorta a √2 (esquinas agudas). VECTOR no lo usa, así su deriva no cambia.
def _bevel_vectors(pts, capped=False):
    out = []
    n = len(pts)
    for i in range(n):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % n]
        d1 = (p1[0] - p0[0], p1[1] - p0[1])
        d2 = (p2[0] - p1[0], p2[1] - p1[1])
        l1, l2 = math.hypot(*d1), math.hypot(*d2)
        n1 = (d1[1] / l1, -d1[0] / l1)
        n2 = (d2[1] / l2, -d2[0] / l2)
        k = 1 + n1[0] * n2[0] + n1[1] * n2[1]
        v = ((n1[0] + n2[0]) / k, (n1[1] + n2[1]) / k)
        lensq = v[0] * v[0] + v[1] * v[1]
        if capped and lensq > 2:
            shrink = math.sqrt(lensq / 2)
            v = (v[0] / shrink, v[1] / shrink)
        out.append(v)
    return out


# ExtrudeGeometry de un polígono convexo: tapas en abanico y paredes; con bisel, las capas de three.js
# (cos / sen de t con t = b / segmentos). Normales planas por triángulo y UV de WorldUVGenerator (metros de la forma).
def extrude(shape, depth, bevel_thickness=0.0, bevel_size=0.0, bevel_segments=0, capped_bevel=False):
    pts = list(shape)
    if _signed_area(pts) < 0:
        pts.reverse()
    vec = _bevel_vectors(pts, capped_bevel)
    layers = []
    if bevel_segments > 0:
        for b in range(bevel_segments):
            t = b / bevel_segments
            layers.append((-bevel_thickness * math.cos(t * math.pi / 2), bevel_size * math.sin(t * math.pi / 2)))
        layers.append((0.0, bevel_size))
        layers.append((depth, bevel_size))
        for b in range(bevel_segments - 1, -1, -1):
            t = b / bevel_segments
            layers.append((depth + bevel_thickness * math.cos(t * math.pi / 2), bevel_size * math.sin(t * math.pi / 2)))
    else:
        layers = [(0.0, 0.0), (depth, 0.0)]
    rings = [[(p[0] + v[0] * bs, p[1] + v[1] * bs, z) for p, v in zip(pts, vec)] for z, bs in layers]
    pos, uv = [], []
    n = len(pts)
    # tapa de delante (mira a −z) y de detrás (mira a +z)
    front, back = rings[0], rings[-1]
    for i in range(1, n - 1):
        for p in (front[0], front[i + 1], front[i]):
            pos.append(p)
            uv.append((p[0], p[1]))
        for p in (back[0], back[i], back[i + 1]):
            pos.append(p)
            uv.append((p[0], p[1]))
    for r in range(len(rings) - 1):
        lo, hi = rings[r], rings[r + 1]
        for i in range(n):
            j = (i + 1) % n
            a, b, c, d = lo[i], lo[j], hi[j], hi[i]
            use_x = abs(a[1] - b[1]) < abs(a[0] - b[0])
            for p in (a, b, d, b, c, d):
                pos.append(p)
                uv.append((p[0] if use_x else p[1], 1 - p[2]))
    return flat(pos, uv)


def rotate_y(soup, angle):
    return g.transformed(soup, g.euler_xyz((0.0, angle, 0.0)))


def rotate_x(soup, angle):
    return g.transformed(soup, g.euler_xyz((angle, 0.0, 0.0)))


# Deriva: el triángulo del wireframe a media altura, 3,5 cm de grueso con bisel de 6 mm. x de la forma → z
def endplate():
    shape = [(2.32, -0.08), (2.56, 0.31), (2.70, 0.31), (2.70, -0.08)]
    return rotate_y(extrude(shape, 0.035, 0.006, 0.006, 2), -math.pi / 2)


# Splitter bajo el morro: extruido 1,8 cm, girado para quedar tumbado (y de la forma → z)
def splitter():
    shape = [(0.0, -3.9), (0.46, -2.85), (0.62, -2.35), (-0.62, -2.35), (-0.46, -2.85)]
    return rotate_x(extrude(shape, 0.018), math.pi / 2)


# rrect + ShapeGeometry(forma, 6): rectángulo redondeado con curvas cuadráticas de 6 tramos, plano en z = 0
def rrect(w, h, r, divisions=6):
    x, y = -w / 2, -h / 2
    pts = []

    def add(p):
        if not pts or (abs(pts[-1][0] - p[0]) > 1e-12 or abs(pts[-1][1] - p[1]) > 1e-12):
            pts.append(p)

    def quad(p0, p1, p2):
        for d in range(divisions + 1):
            t = d / divisions
            a, b, c = (1 - t) ** 2, 2 * (1 - t) * t, t * t
            add((a * p0[0] + b * p1[0] + c * p2[0], a * p0[1] + b * p1[1] + c * p2[1]))

    add((x + r, y))
    add((x + w - r, y))
    quad((x + w - r, y), (x + w, y), (x + w, y + r))
    add((x + w, y + h - r))
    quad((x + w, y + h - r), (x + w, y + h), (x + w - r, y + h))
    add((x + r, y + h))
    quad((x + r, y + h), (x, y + h), (x, y + h - r))
    add((x, y + r))
    quad((x, y + r), (x, y), (x + r, y))
    if abs(pts[-1][0] - pts[0][0]) < 1e-12 and abs(pts[-1][1] - pts[0][1]) < 1e-12:
        pts.pop()
    pos, uv = [], []
    for i in range(1, len(pts) - 1):
        for p in (pts[0], pts[i], pts[i + 1]):
            pos.append((p[0], p[1], 0.0))
            uv.append((p[0], p[1]))
    return flat(pos, uv)


# BoxGeometry de three.js: 12 triángulos, normales planas; UV en metros por la cara (boxUV)
def box(w, h, d):
    x, y, z = w / 2, h / 2, d / 2
    faces = [
        [(x, -y, z), (x, -y, -z), (x, y, -z), (x, y, z)],
        [(-x, -y, -z), (-x, -y, z), (-x, y, z), (-x, y, -z)],
        [(-x, y, z), (x, y, z), (x, y, -z), (-x, y, -z)],
        [(-x, -y, -z), (x, -y, -z), (x, -y, z), (-x, -y, z)],
        [(-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z)],
        [(x, -y, -z), (-x, -y, -z), (-x, y, -z), (x, y, -z)],
    ]
    pos = []
    for f in faces:
        pos += [f[0], f[1], f[2], f[0], f[2], f[3]]
    return g.box_uv(flat(pos, [(0.0, 0.0)] * len(pos)))


# LatheGeometry de three.js alrededor de su Y, con sus normales del perfil, y luego rotateX(π/2): eje = Z.
# UV del prototipo: (z, ángulo × 0,4) con la costura por debajo.
def lathe(profile, segments):
    pts = [(r, z) for r, z in profile]
    normals0 = []
    prev = None
    for j in range(len(pts)):
        if j == 0:
            dx, dy = pts[1][0] - pts[0][0], pts[1][1] - pts[0][1]
            prev = (dy, -dx)
            normals0.append(g._normalize((dy, -dx, 0.0)))
        elif j == len(pts) - 1:
            normals0.append(g._normalize((prev[0], prev[1], 0.0)))
        else:
            dx, dy = pts[j + 1][0] - pts[j][0], pts[j + 1][1] - pts[j][1]
            cur = (dy, -dx)
            normals0.append(g._normalize((cur[0] + prev[0], cur[1] + prev[1], 0.0)))
            prev = cur
    verts, norms, uvs, idx = [], [], [], []
    for i in range(segments + 1):
        phi = i / segments * math.pi * 2
        sn, cs = math.sin(phi), math.cos(phi)
        for j, (r, zp) in enumerate(pts):
            v = (r * sn, zp, r * cs)
            nn = normals0[j]
            nv = (nn[0] * sn, nn[1], nn[0] * cs)
            # rotateX(π/2): (x, y, z) → (x, −z, y)
            p = (v[0], -v[2], v[1])
            verts.append(p)
            norms.append(g._normalize((nv[0], -nv[2], nv[1])))
            uvs.append((p[2], math.atan2(p[0], p[1]) * 0.4))
    n = len(pts)
    for i in range(segments):
        for j in range(n - 1):
            base = j + i * n
            a, b, c, d = base, base + n, base + n + 1, base + 1
            idx += [a, b, d, c, d, b]
    return g._indexed(verts, norms, uvs, idx)


# ---------- Góndolas y toberas: secciones octogonales biseladas [z, a, b, c, u] ----------

POD = [[0.80, 0.34, 0.20, 0.08, 0.08], [0.92, 0.40, 0.25, 0.10, 0.07], [1.10, 0.42, 0.27, 0.11, 0.05],
       [1.70, 0.42, 0.27, 0.11, 0.0], [2.55, 0.42, 0.27, 0.11, 0.0], [2.72, 0.41, 0.26, 0.10, 0.0]]
NOZ = [[2.72, 0.41, 0.26, 0.10, 0.0], [2.90, 0.40, 0.235, 0.09, 0.0], [3.20, 0.36, 0.17, 0.05, 0.0]]


def sec_at(sx, z):
    i = 0
    while i < len(sx) - 2 and z > sx[i + 1][0]:
        i += 1
    a, b = sx[i], sx[i + 1]
    t = min(1.0, max(0.0, (z - a[0]) / (b[0] - a[0])))
    t = t * t * (3 - 2 * t)
    return [a[k] + (b[k] - a[k]) * t for k in range(1, 5)]


def oct_ring(a, b, c, u=0.0):
    return [(a, b - c + u), (a - c, b + u), (-(a - c), b + u), (-a, b - c + u),
            (-a, -(b - c)), (-(a - c), -b), (a - c, -b), (a, -(b - c))]


# Cada una de las 8 caras es una tira propia (aristas vivas entre caras). off: por fuera; inset: por dentro
def oct_loft(sx, z0, z1, nz, off=0.0, inset=0.0):
    pos, uv, idx, rings = [], [], [], []
    for i in range(nz + 1):
        z = z0 + (z1 - z0) * i / nz
        a, b, c, u = sec_at(sx, z)
        rings.append((z, oct_ring(a + off - inset, b + off - inset, c, u)))
    base = 0
    per = 0.0
    for f in range(8):
        r0 = rings[0][1]
        ln = math.hypot(r0[(f + 1) % 8][0] - r0[f][0], r0[(f + 1) % 8][1] - r0[f][1])
        for i in range(nz + 1):
            z, ring = rings[i]
            p, q = ring[f], ring[(f + 1) % 8]
            pos += [(p[0], p[1], z), (q[0], q[1], z)]
            uv += [(z, per), (z, per + ln)]
        for i in range(nz):
            a = base + i * 2
            idx += [a, a + 2, a + 1, a + 1, a + 2, a + 3]
        base += (nz + 1) * 2
        per += ln
    return indexed(pos, uv, idx)


def oct_annulus(ro, zo, ri, zi):
    pos, uv, idx = [], [], []
    for f in range(8):
        h = (f + 1) % 8
        a = f * 4
        for p in ((ro[f][0], ro[f][1], zo), (ro[h][0], ro[h][1], zo), (ri[f][0], ri[f][1], zi), (ri[h][0], ri[h][1], zi)):
            pos.append(p)
            uv.append((p[0], p[1]))
        idx += [a, a + 2, a + 1, a + 1, a + 2, a + 3]
    return indexed(pos, uv, idx)


def oct_fan(ring, z):
    pos, uv, idx = [(0.0, 0.0, z)], [(0.0, 0.0)], []
    for p in ring:
        pos.append((p[0], p[1], z))
        uv.append((p[0], p[1]))
    for f in range(8):
        idx += [0, f + 1, (f + 1) % 8 + 1]
    return indexed(pos, uv, idx)


# ---------- Tubo cerrado (junta de la cúpula): CatmullRomCurve3 cerrada y centrípeta + TubeGeometry cerrado ----------

class ClosedCatmullRom(g.CatmullRom):
    def point(self, t):
        pts = self.points
        l = len(pts)
        p = l * t
        index = math.floor(p)
        weight = p - index
        index += 0 if index > 0 else (math.floor(abs(index) / l) + 1) * l
        p0 = pts[(index - 1) % l]
        p1 = pts[index % l]
        p2 = pts[(index + 1) % l]
        p3 = pts[(index + 2) % l]
        dt0 = g._dot(g._sub(p0, p1), g._sub(p0, p1)) ** 0.25
        dt1 = g._dot(g._sub(p1, p2), g._sub(p1, p2)) ** 0.25
        dt2 = g._dot(g._sub(p2, p3), g._sub(p2, p3)) ** 0.25
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


def closed_tube(points, radius, tubular, radial):
    curve = ClosedCatmullRom(points)
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
    vec = g._normalize(g._cross(t0, normal))
    normals = [g._cross(t0, vec)]
    for i in range(1, tubular + 1):
        n = normals[i - 1]
        vec = g._cross(tangents[i - 1], tangents[i])
        if g._length(vec) > 2.220446049250313e-16:
            vec = g._normalize(vec)
            theta = math.acos(max(-1.0, min(1.0, g._dot(tangents[i - 1], tangents[i]))))
            n = g._rotate_around(n, vec, theta)
        normals.append(n)
    # cerrado: el giro que sobra se reparte a lo largo del tubo, como computeFrenetFrames
    theta = math.acos(max(-1.0, min(1.0, g._dot(normals[0], normals[tubular])))) / tubular
    if g._dot(tangents[0], g._cross(normals[0], normals[tubular])) > 0:
        theta = -theta
    for i in range(1, tubular + 1):
        normals[i] = g._rotate_around(normals[i], tangents[i], theta * i)
    binormals = [g._cross(tangents[i], normals[i]) for i in range(tubular + 1)]
    verts, norms, uvs, idx = [], [], [], []
    for i in range(tubular + 1):
        k = i if i < tubular else 0
        center = curve.point_at(k / tubular)
        for j in range(radial + 1):
            v = j / radial * math.pi * 2
            sn, cs = math.sin(v), -math.cos(v)
            nn = g._normalize(g._add(g._scale(normals[k], cs), g._scale(binormals[k], sn)))
            verts.append(g._add(center, g._scale(nn, radius)))
            norms.append(nn)
            uvs.append((i / tubular, j / radial))
    for j in range(1, tubular + 1):
        for i in range(1, radial + 1):
            a = (radial + 1) * (j - 1) + (i - 1)
            b = (radial + 1) * j + (i - 1)
            c = (radial + 1) * j + i
            d = (radial + 1) * (j - 1) + i
            idx += [a, b, d, b, c, d]
    return g._indexed(verts, norms, uvs, idx)
