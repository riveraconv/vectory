# Geometría del casco 3D de HAMMER, variante MINERO (spec docs/cascos-3d-3-hammer.md, 3.4), sin Blender: traducción
# de las piezas de designs/casco-3d-hammer.html con VAR = 1 (prof, fusPt, skinY, skinNormal, fusPatch, fusCap,
# skinGrid, skinRibbon, canPt, canardGeos, gridGeo, slab, profileExtrude, quadGeo, sideGeo, polyTube, beam, rbox y
# cylG), con los mismos números. Su fuselaje no es el de VECTOR ni el de BLADE (superelipse, cara roma), así que va
# aparte; reutiliza de hull_geometry lo que es igual en los tres prototipos (indexed, flat, extrude, torno, cajas y
# el tubo cerrado) sin cambiarlo.
import math

from . import geometry as g
from . import hull_geometry as hg

js_round = hg.js_round
lerp3 = hg._lerp3


def _clamp01(v):
    return max(0.0, min(1.0, v))


# ---------- Fuselaje: las 4 secciones del wireframe MARTILLO y la cara roma del morro ----------

Z0, Z1 = -3.6, 2.2
PZ = [-2.6, -1.0, 1.2, 2.2]
PV = {"hw": [0.6, 0.85, 0.9, 0.85], "top": [0.45, 0.7, 0.72, 0.68], "bot": [-0.2, -0.3, -0.32, -0.32],
      "side": [0.08, 0.1, 0.1, 0.1]}
TIP = {"hw": 0.20, "top": 0.14, "bot": 0.0, "side": 0.06}   # cara roma del morro
N_SEC = 3.2   # exponente de la superelipse (MINERO): sección casi cuadrada


# Hermite monótona entre las secciones (la de VECTOR y BLADE con las secciones de HAMMER)
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


# De la cara roma a la 1.ª sección: cada valor = cara + (sección − cara) × (1 − (1 − t)²)
def prof(z):
    if z < PZ[0]:
        t = _clamp01((z - Z0) / (PZ[0] - Z0))
        e = 1 - (1 - t) ** 2
        return {k: TIP[k] + (PV[k][0] - TIP[k]) * e for k in ("hw", "top", "bot", "side")}
    return {k: herm(PV[k], z) for k in ("hw", "top", "bot", "side")}


def _sign(x):
    return (x > 0) - (x < 0)


# Superelipse centrada en (0, side). θ = 0 arista derecha, π/2 lomo, π arista izquierda, 3π/2 panza
def fus_pt(z, th):
    p = prof(z)
    c, s = math.cos(th), math.sin(th)
    ex = 2 / N_SEC
    x = p["hw"] * _sign(c) * abs(c) ** ex
    if s >= 0:
        y = p["side"] + (p["top"] - p["side"]) * abs(s) ** ex
    else:
        y = p["side"] - (p["side"] - p["bot"]) * abs(s) ** ex
    return (x, y, z)


def skin_y(x, z, upper):
    p = prof(z)
    r = min(1.0, abs(x) / max(1e-6, p["hw"]))
    c = r ** (N_SEC / 2)
    s = math.sqrt(max(0.0, 1 - c * c)) ** (2 / N_SEC)
    return p["side"] + (p["top"] - p["side"]) * s if upper else p["side"] - (p["side"] - p["bot"]) * s


def fus_top_y(x, z):
    return skin_y(x, z, True)


def fus_bot_y(x, z):
    return skin_y(x, z, False)


# Normal de la piel por diferencias centradas de 1 cm
def skin_normal(x, z, upper=True):
    e = 0.01
    f = fus_top_y if upper else fus_bot_y
    dx = (f(x + e, z) - f(x - e, z)) / (2 * e)
    dz = (f(x, z + e) - f(x, z - e)) / (2 * e)
    n = (-dx, 1.0, -dz) if upper else (dx, -1.0, dz)
    l = math.sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2])
    return (n[0] / l, n[1] / l, n[2] / l)


def z_of_u(u):
    return Z0 + (Z1 - Z0) * u ** 1.3


def u_of_z(z):
    return ((z - Z0) / (Z1 - Z0)) ** (1 / 1.3)


# Trozo de piel entre θ0–θ1 y z0–z1. UV en metros: (z, θ × radio medio)
def fus_patch(t0, t1, z0, z1):
    u0, u1 = u_of_z(z0), u_of_z(z1)
    nz = max(4, js_round(70 * (u1 - u0)))
    nt = max(3, js_round(96 * (t1 - t0) / (math.pi * 2)))
    pos, uv, idx = [], [], []
    for i in range(nz + 1):
        z = z_of_u(u0 + (u1 - u0) * i / nz)
        p = prof(z)
        rr = (p["hw"] + (p["top"] - p["bot"]) / 2) / 2
        for j in range(nt + 1):
            th = t0 + (t1 - t0) * j / nt
            pos.append(fus_pt(z, th))
            uv.append((z, th * rr))
    for i in range(nz):
        for j in range(nt):
            a = i * (nt + 1) + j
            b = a + nt + 1
            idx += [a, b, a + 1, a + 1, b, b + 1]
    return hg.indexed(pos, uv, idx)


# Tapa en abanico de 96 lados (cara roma del morro y trasera)
def fus_cap(z):
    p = prof(z)
    cy = (p["top"] + p["bot"]) / 2
    nt = 96
    pos, uv, idx = [(0.0, cy, z)], [(0.0, cy)], []
    for j in range(nt + 1):
        q = fus_pt(z, j / nt * math.pi * 2)
        pos.append(q)
        uv.append((q[0], q[1]))
    for j in range(1, nt + 1):
        idx += [0, j, j + 1]
    return hg.indexed(pos, uv, idx)


# Rejilla pegada a la piel: z0–z1, x entre xr(z)[0] y xr(z)[1], desplazada off por la normal. UV en metros (z, x)
def skin_grid(z0, z1, xr, off, nz, nx, upper=True):
    pos, uv, idx = [], [], []
    f = fus_top_y if upper else fus_bot_y
    for i in range(nz + 1):
        z = z0 + (z1 - z0) * i / nz
        a, b = xr(z)
        for j in range(nx + 1):
            x = a + (b - a) * j / nx
            n = skin_normal(x, z, upper)
            y = f(x, z)
            pos.append((x + n[0] * off, y + n[1] * off, z + n[2] * off))
            uv.append((z, x))
    for i in range(nz):
        for j in range(nx):
            a = i * (nx + 1) + j
            b = a + nx + 1
            idx += [a, b, a + 1, a + 1, b, b + 1]
    return hg.indexed(pos, uv, idx)


# Cinta pegada a la piel siguiendo una polilínea en planta [(x, z), …] (juntas de panel)
def skin_ribbon(pts, w, off, upper=True):
    pos, uv, idx = [], [], []
    f = fus_top_y if upper else fus_bot_y
    acc = 0.0
    for i, q in enumerate(pts):
        a = pts[max(0, i - 1)]
        b = pts[min(len(pts) - 1, i + 1)]
        tx, tz = b[0] - a[0], b[1] - a[1]
        l = math.hypot(tx, tz) or 1.0
        tx /= l
        tz /= l
        if i:
            acc += math.hypot(q[0] - pts[i - 1][0], q[1] - pts[i - 1][1])
        for s, (x, z) in enumerate(((q[0] - tz * w / 2, q[1] + tx * w / 2), (q[0] + tz * w / 2, q[1] - tx * w / 2))):
            hw = prof(z)["hw"] * 0.998
            x = max(-hw, min(hw, x))
            n = skin_normal(x, z, upper)
            pos.append((x + n[0] * off, f(x, z) + n[1] * off, z + n[2] * off))
            uv.append((acc, s * w))
    for i in range(len(pts) - 1):
        a = i * 2
        idx += [a, a + 2, a + 1, a + 1, a + 2, a + 3]
    return hg.indexed(pos, uv, idx)


# Juntas de panel: tres transversales y dos longitudinales
def seam_lines():
    lines = []
    for z in (-2.6, 0.3, 1.3):
        h = prof(z)["hw"] * 0.97
        lines.append([(-h + 2 * h * i / 16, z) for i in range(17)])
    for k in (-1, 1):
        pts = []
        for i in range(25):
            z = -3.3 + 5.4 * i / 24
            pts.append((k * prof(z)["hw"] * 0.62, z))
        lines.append(pts)
    return lines


# ---------- Rejillas facetadas ----------

# gridGeo: fn(i, j) → (x, y, z), i 0..NI, j 0..NJ. UV en metros (z, x). Facetada (toNonIndexed)
def grid_geo(ni, nj, fn):
    verts, uvs = [], []
    for i in range(ni + 1):
        for j in range(nj + 1):
            p = fn(i, j)
            verts.append(tuple(p))
            uvs.append((p[2], p[0]))
    pos, uv = [], []
    for i in range(ni):
        for j in range(nj):
            a = i * (nj + 1) + j
            b = a + nj + 1
            for v in (a, b, a + 1, a + 1, b, b + 1):
                pos.append(verts[v])
                uv.append(uvs[v])
    return hg.flat(pos, uv)


# ---------- Canard: la planta del wireframe con grosor real ----------

CAN = {"A": (0.30, 0.13, -3.05), "B": (2.47, 0.0, -2.62), "C": (2.47, 0.0, -1.70), "D": (0.55, 0.13, -1.50),
       "th0": 0.15, "th1": 0.07}
CT = [0, 0.12, 0.35, 0.6, 0.78, 1]


def can_pt(k, s, t, side):
    p = lerp3(lerp3(CAN["A"], CAN["B"], s), lerp3(CAN["D"], CAN["C"], s), t)
    th = CAN["th0"] + (CAN["th1"] - CAN["th0"]) * s
    f = 0.3 + 0.7 * min(1.0, t / 0.12, (1 - t) / 0.22)
    return (k * p[0], p[1] + side * th * f / 2, p[2])


def canard_geos(k):
    ns, nc = 12, len(CT) - 1
    return {
        "top": grid_geo(ns, nc, lambda i, j: can_pt(k, i / ns, CT[j], 1)),
        "bot": grid_geo(ns, nc, lambda i, j: can_pt(k, i / ns, CT[j], -1)),
        "le": grid_geo(ns, 1, lambda i, j: can_pt(k, i / ns, 0, -1 if j else 1)),
        "te": grid_geo(ns, 1, lambda i, j: can_pt(k, i / ns, 1, -1 if j else 1)),
        "tip": grid_geo(nc, 1, lambda i, j: can_pt(k, 1, CT[i], -1 if j else 1)),
    }


# Punta caída (b · (2,5, −0,5, −2,2) · c del wireframe) y aleta trasera, en su plano
DROOP = [(-2.64, 0.02), (-1.70, 0.02), (-2.08, -0.49), (-2.32, -0.49)]
FIN = [(-0.05, -0.2), (1.0, 0.75), (1.4, 0.75), (1.22, -0.2)]
FIN_HAZARD = [(0.94, 0.64), (1.4, 0.64), (1.4, 0.75), (1.0, 0.75)]


# slab: polígono plano [(a, b), …] en la base (O, U, V), extruido t a lo largo de U × V (centrado) y biselado
def slab(pts, origin, u, v, t, bevel=0.008):
    if bevel > 0:
        soup = hg.extrude(pts, t, bevel, bevel, 2)
    else:
        soup = hg.extrude(pts, t)
    soup = g.transformed(soup, g.translation((0.0, 0.0, -t / 2)))
    uu = g._normalize(tuple(u))
    vv = g._normalize(tuple(v))
    n = g._normalize(g._cross(uu, vv))
    return g.transformed(soup, g.from_basis(uu, vv, n, tuple(origin)))


# ---------- Cabina de grúa y compartimento del motor ----------

CABP = [(-1.84, 0.40), (-1.84, 0.64), (-1.54, 1.04), (-0.44, 1.04), (-0.20, 0.84), (-0.20, 0.42)]
CAB_W, CAB_B = 0.60, 0.015
ROOF = 1.04 + CAB_B
WS1, WS2, WSN = (-1.84, 0.64), (-1.54, 1.04), (-0.8, 0.6)   # parabrisas (z, y) y su normal hacia fuera
SIDEWIN = [(-1.67, 0.76), (-1.505, 0.98), (-0.62, 0.98), (-0.62, 0.76)]
BAYP = [(0.55, 0.52), (0.55, 0.78), (0.72, 0.90), (1.82, 0.90), (1.90, 0.83), (1.90, 0.52)]
BAY_W, BAY_B = 0.42, 0.015
BAY_TOP = 0.90 + BAY_B


def ws_pt(x, f, off):
    return (x, WS1[1] + (WS2[1] - WS1[1]) * f + WSN[1] * off, WS1[0] + (WS2[0] - WS1[0]) * f + WSN[0] * off)


# Perfil (z, y) extruido a lo ancho, centrado en x, con semiancho total hw (bisel incluido):
# rotateY(−π/2) lleva la forma (z, y) al mundo y la extrusión a −x; luego se centra
def profile_extrude(profile, hw, bev):
    d = 2 * hw - 2 * bev
    soup = hg.extrude(profile, d, bev, bev, 2)
    soup = hg.rotate_y(soup, -math.pi / 2)
    return g.transformed(soup, g.translation((d / 2, 0.0, 0.0)))


# quadGeo: cuadrilátero indexado, UV (z, y)
def quad_geo(p):
    return hg.indexed([tuple(q) for q in p], [(q[2], q[1]) for q in p], [0, 1, 2, 0, 2, 3])


# sideGeo: ShapeGeometry de un cuadrilátero (z, y) girada −π/2 en Y (plano x = 0); UV (z, y) de la forma
def side_geo(pts):
    pos, uv = [], []
    for tri in ((0, 1, 2), (0, 2, 3)):
        for i in tri:
            z, y = pts[i]
            pos.append((-0.0, y, z))
            uv.append((z, y))
    soup = hg.flat(pos, uv)
    n = (-1.0, 0.0, 0.0)
    return g.Soup(soup.positions, [n] * len(pos), soup.uvs)


# ---------- Primitivas del prototipo ----------

# rbox: RoundedBoxGeometry de 2 segmentos con el radio limitado a lado / 2,01, ya con boxUV
def rbox(w, h, d, r=0.006):
    return g.rounded_box(w, h, d, min(r, w / 2.01, h / 2.01, d / 2.01))


# cylG: CylinderGeometry con boxUV
def cyl(rt, rb, h, n):
    return g.box_uv(g.cylinder(rt, rb, h, n))


# Tubo por una polilínea: cada tramo se divide en 6; Catmull-Rom centrípeta con 3 tramos por punto
def poly_tube(pts, r, closed=False, sides=10):
    p = []
    n = len(pts)
    segs = n if closed else n - 1
    for i in range(segs):
        a, b = pts[i], pts[(i + 1) % n]
        for k in range(6):
            p.append(lerp3(a, b, k / 6))
    if not closed:
        p.append(tuple(pts[-1]))
    if closed:
        return hg.closed_tube(p, r, len(p) * 3, sides)
    return g.tube(p, r, len(p) * 3, sides)


# Caja redondeada entre dos puntos (beam): Z de p0 a p1; T = toward − punto medio; X = T × Z; Y = Z × X.
# Devuelve la malla y su transformación
def beam(p0, p1, w, h, toward):
    a, b = tuple(p0), tuple(p1)
    length = g._length(g._sub(b, a))
    z = g._normalize(g._sub(b, a))
    mid = g._scale(g._add(a, b), 0.5)
    t = g._sub(tuple(toward), mid)
    x = g._normalize(g._cross(t, z))
    y = g._normalize(g._cross(z, x))
    return rbox(w, h, length, min(w, h) * 0.3), g.from_basis(x, y, z, mid)
