# Geometría del casco 3D de BLADE, variante ASALTO (spec docs/cascos-3d-2-blade.md, 3.4), sin Blender: traducción de
# las piezas de designs/casco-3d-blade.html con VAR = 3 (prof, fusPt, fusTopY, fusBotY, skinNormal, fusPatch, fusCap,
# skinGrid, skinRibbon, canopyGeo, fairingGeo, prongGeo y la base de plateAt), con los mismos números.
# Su fuselaje no es el de VECTOR (sección de lente, otras secciones), así que va aparte; reutiliza de hull_geometry
# lo que es igual en los dos prototipos (indexed, extrude, octógonos, torno, cajas) sin cambiar lo que usa VECTOR (el bisel de las puntas pide el tope de inglete de three.js: capped_bevel).
import math

from . import geometry as g
from . import hull_geometry as hg

js_round = hg.js_round

# ---------- Fuselaje: las 4 secciones del wireframe HOJA y la punta del morro ----------

Z0, Z1, NOSE_Y = -5.2, 2.8, 0.02
PZ = [-3.0, -1.0, 1.2, 2.8]
PV = {"hw": [0.45, 0.9, 1.35, 1.55], "top": [0.16, 0.26, 0.30, 0.28], "bot": [-0.08, -0.12, -0.14, -0.14],
      "side": [0.03, 0.04, 0.04, 0.04]}
P = 1.2   # exponente de la sección (ASALTO): apenas curvada, con el filo lateral vivo


def _clamp01(v):
    return max(0.0, min(1.0, v))


# Hermite monótona entre las secciones (la de VECTOR con las secciones de BLADE)
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


# Del morro (0, 0,02, −5,2) a la 1.ª sección: 1 − (1 − t)^1,6 (punta algo ojival)
def prof(z):
    if z < PZ[0]:
        t = max(0.0, (z - Z0) / (PZ[0] - Z0))
        e = 1 - (1 - t) ** 1.6
        return {"hw": PV["hw"][0] * e, "top": NOSE_Y + (PV["top"][0] - NOSE_Y) * e,
                "bot": NOSE_Y + (PV["bot"][0] - NOSE_Y) * e, "side": NOSE_Y + (PV["side"][0] - NOSE_Y) * e}
    return {k: herm(PV[k], z) for k in PV}


# Sección de lente. θ = 0 arista derecha, π/2 lomo, π arista izquierda, 3π/2 panza. x = hw·cos θ.
def fus_pt(z, th, off=0.0):
    p = prof(z)
    t = math.cos(th)
    upper = th <= math.pi + 1e-9
    a = abs(t)
    k = 1 - a ** P
    x = p["hw"] * t
    y = p["side"] + (p["top"] - p["side"]) * k if upper else p["side"] - (p["side"] - p["bot"]) * k
    if not off:
        return (x, y, z)
    hw = max(1e-6, p["hw"])
    sl = P * a ** (P - 1) * hg._sign(t) / hw
    if upper:
        nx, ny = (p["top"] - p["side"]) * sl, 1.0
    else:
        nx, ny = (p["side"] - p["bot"]) * sl, -1.0
    l = math.hypot(nx, ny)
    return (x + nx / l * off, y + ny / l * off, z)


def fus_top_y(x, z):
    p = prof(z)
    r = min(1.0, abs(x) / max(1e-6, p["hw"]))
    return p["side"] + (p["top"] - p["side"]) * (1 - r ** P)


def fus_bot_y(x, z):
    p = prof(z)
    r = min(1.0, abs(x) / max(1e-6, p["hw"]))
    return p["side"] - (p["side"] - p["bot"]) * (1 - r ** P)


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
    return Z0 + (Z1 - Z0) * u ** 1.5


def u_of_z(z):
    return ((z - Z0) / (Z1 - Z0)) ** (1 / 1.5)


# Trozo de piel entre θ0–θ1 y z0–z1. UV en metros: (z, θ × radio medio)
def fus_patch(t0, t1, z0, z1, off=0.0, uv_fn=None):
    u0, u1 = u_of_z(z0), u_of_z(z1)
    nz = max(4, js_round(80 * (u1 - u0)))
    nt = max(3, js_round(72 * (t1 - t0) / (math.pi * 2)))
    pos, uv, idx = [], [], []
    for i in range(nz + 1):
        z = z_of_u(u0 + (u1 - u0) * i / nz)
        p = prof(z)
        rr = (p["hw"] + (p["top"] - p["bot"]) / 2) / 2
        for j in range(nt + 1):
            th = t0 + (t1 - t0) * j / nt
            pos.append(fus_pt(z, th, off))
            uv.append(uv_fn(i / nz, j / nt) if uv_fn else (z, th * rr))
    for i in range(nz):
        for j in range(nt):
            a = i * (nt + 1) + j
            b = a + nt + 1
            idx += [a, b, a + 1, a + 1, b, b + 1]
    return hg.indexed(pos, uv, idx)


# Tapa trasera en abanico, 72 lados
def fus_cap():
    p = prof(Z1)
    cy = (p["top"] + p["bot"]) / 2
    nt = 72
    pos, uv, idx = [(0.0, cy, Z1)], [(0.0, cy)], []
    for j in range(nt + 1):
        q = fus_pt(Z1, j / nt * math.pi * 2)
        pos.append(q)
        uv.append((q[0], q[1]))
    for j in range(1, nt + 1):
        idx += [0, j, j + 1]
    return hg.indexed(pos, uv, idx)


# Rejilla pegada a la piel (antirreflejos y calcomanías): z0–z1, x entre xr(z)[0] y xr(z)[1], desplazada off por la
# normal. uv_fn(iu, jv): iu de delante (z0) a atrás, jv de xr[0] a xr[1]; sin uv_fn, UV en metros (z, x).
def skin_grid(z0, z1, xr, off, nz, nx, uv_fn=None, upper=True):
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
            uv.append(uv_fn(i / nz, j / nx) if uv_fn else (z, x))
    for i in range(nz):
        for j in range(nx):
            a = i * (nx + 1) + j
            b = a + nx + 1
            idx += [a, b, a + 1, a + 1, b, b + 1]
    return hg.indexed(pos, uv, idx)


# Cinta pegada a la piel siguiendo una polilínea en planta [(x, z), …] (juntas de panel, tiras de luz)
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


# ---------- Cúpula BURBUJA: planta elíptica con A en x, Bf hacia el morro y Br hacia atrás desde zc ----------

CAN = {"A": 0.34, "zc": -0.55, "Bf": 1.15, "Br": 1.7, "H": 0.22}


def can_b(z):
    return CAN["Bf"] if z < CAN["zc"] else CAN["Br"]


def canopy_y(x, z):
    r2 = (x / CAN["A"]) ** 2 + ((z - CAN["zc"]) / can_b(z)) ** 2
    return fus_top_y(x, z) + 0.002 + CAN["H"] * math.sqrt(max(0.0, 1 - r2))


def canopy():
    nr, np_ = 20, 84
    pos, uv, idx = [], [], []
    for i in range(nr + 1):
        for j in range(np_ + 1):
            r = i / nr
            f = j / np_ * math.pi * 2
            x = CAN["A"] * r * math.sin(f)
            d = -r * math.cos(f)
            z = CAN["zc"] + d * (CAN["Bf"] if d < 0 else CAN["Br"])
            pos.append((x, fus_top_y(x, z) + 0.002 + CAN["H"] * math.sqrt(max(0.0, 1 - r * r)), z))
            uv.append((x, z))
    for i in range(nr):
        for j in range(np_):
            a = i * (np_ + 1) + j
            b = a + np_ + 1
            idx += [a, b, a + 1, a + 1, b, b + 1]
    return hg.indexed(pos, uv, idx)


def canopy_half_x(z):
    return CAN["A"] * math.sqrt(max(0.0, 1 - ((z - CAN["zc"]) / can_b(z)) ** 2))


def canopy_half_z(x):
    return math.sqrt(max(0.0, 1 - (x / CAN["A"]) ** 2))


# Anillo del borde de la cúpula para la junta, 5 mm por encima de la piel
def canopy_ring(count=120, lift=0.005):
    ring = []
    for i in range(count):
        f = i / count * math.pi * 2
        x = CAN["A"] * math.sin(f)
        d = -math.cos(f)
        z = CAN["zc"] + d * (CAN["Bf"] if d < 0 else CAN["Br"])
        ring.append((x, fus_top_y(x, z) + lift, z))
    return ring


# ---------- Carenado detrás de la cúpula ----------

FAIR = {"z0": 0.85, "z1": 2.15, "w0": 0.30, "w1": 0.10, "h0": 0.10}


def fair_w(z):
    return FAIR["w0"] + (FAIR["w1"] - FAIR["w0"]) * _clamp01((z - FAIR["z0"]) / (FAIR["z1"] - FAIR["z0"]))


def fair_h(z):
    return 0.004 + FAIR["h0"] * (1 - _clamp01((z - FAIR["z0"]) / (FAIR["z1"] - FAIR["z0"]))) ** 1.2


def fair_y(x, z):
    w = fair_w(z)
    return fus_top_y(x, z) + 0.001 + fair_h(z) * max(0.0, 1 - (x / w) ** 2) ** 0.7


def fairing():
    nz, nx = 26, 20
    pos, uv, idx = [], [], []
    for i in range(nz + 1):
        z = FAIR["z0"] + (FAIR["z1"] - FAIR["z0"]) * i / nz
        w = fair_w(z)
        for j in range(nx + 1):
            x = -w + 2 * w * j / nx
            pos.append((x, fair_y(x, z), z))
            uv.append((z, x))
    for i in range(nz):
        for j in range(nx):
            a = i * (nx + 1) + j
            b = a + nx + 1
            idx += [a, b, a + 1, a + 1, b, b + 1]
    return hg.indexed(pos, uv, idx)


# Suelo de la cabina (canopyFloorGeo): la planta de la cúpula pegada a la piel 1,5 mm por encima, y por encima del
# arranque del carenado que queda bajo el cristal, sin pasar de 1 mm por debajo del cristal
def canopy_floor():
    nr, np_ = 20, 84
    pos, uv, idx = [], [], []
    for i in range(nr + 1):
        for j in range(np_ + 1):
            r = i / nr
            f = j / np_ * math.pi * 2
            x = CAN["A"] * r * math.sin(f)
            d = -r * math.cos(f)
            z = CAN["zc"] + d * (CAN["Bf"] if d < 0 else CAN["Br"])
            skin = fus_top_y(x, z)
            fair = fair_y(x, z) if FAIR["z0"] <= z <= FAIR["z1"] and abs(x) < fair_w(z) else skin
            glass = skin + 0.002 + CAN["H"] * math.sqrt(max(0.0, 1 - r * r))
            pos.append((x, min(max(skin, fair) + 0.0015, glass - 0.001), z))
            uv.append((z, x))
    for i in range(nr):
        for j in range(np_):
            a = i * (np_ + 1) + j
            b = a + np_ + 1
            idx += [a, b, a + 1, a + 1, b, b + 1]
    return hg.indexed(pos, uv, idx)


# ---------- Puntas traseras en flecha: el triángulo del wireframe con grosor real ----------

PRONG_BASE = [(1.10, 2.75), (1.55, 2.75), (1.579, 3.25), (1.394, 3.25)]
PRONG_TIP = [(1.394, 3.25), (1.579, 3.25), (1.60, 3.60)]


# Forma en planta extruida 0,04 m con bisel 0,008 × 0,006 y 2 segmentos; rotateX(π/2) y +0,06 en y: de y 0,012 a 0,068
def prong(k, pts):
    shape = [(k * x, z) for x, z in pts]
    soup = hg.rotate_x(hg.extrude(shape, 0.04, 0.008, 0.006, 2, True), math.pi / 2)
    return g.transformed(soup, g.translation((0.0, 0.06, 0.0)))


# ---------- Juntas de panel: transversales delante y detrás de la cúpula y longitudinales ----------

def seam_lines():
    def across(z):
        h = prof(z)["hw"] * 0.97
        return [(-h, z), (h, z)]

    lines = [across(-2.75), across(2.4)]
    lines += [[(k * 0.18, -4.3), (k * 0.18, -2.75)] for k in (-1, 1)]
    for k in (-1, 1):
        lines.append([(k * prof(-2.75 + 5.15 * i / 16)["hw"] * 0.68, -2.75 + 5.15 * i / 16) for i in range(17)])
    return lines


# Base de plateAt: placa apoyada en la piel de arriba con la normal del casco (derecha, normal, adelante)
def plate_frame(x, z, t):
    up = skin_normal(x, z, True)
    fwd = g._normalize(g._sub((0.0, 0.0, 1.0), g._scale(up, up[2])))
    right = g._normalize(g._cross(up, fwd))
    pos = g._add((x, fus_top_y(x, z), z), g._scale(up, t / 2 - 0.004))
    return g.from_basis(right, up, fwd, pos)
