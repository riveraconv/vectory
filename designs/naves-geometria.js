// Genera los artboards del lienzo "VECTORY — Naves".
// Unidades: metros. Morro hacia -Z, X a la derecha, Y arriba (como Spaceship.cs).
const fs = require('fs');
const path = require('path');
const OUT = path.join(__dirname, 'project');
fs.mkdirSync(OUT, { recursive: true });

// ---------- primitivas ----------
function Ship() { this.L = []; this.nozzles = []; }
Ship.prototype.add = function (p, q, c) { this.L.push([p, q, c || 'c']); };
Ship.prototype.poly = function (pts, c, closed) {
  for (let i = 0; i < pts.length - (closed === false ? 1 : 0); i++) this.add(pts[i], pts[(i + 1) % pts.length], c);
};
// Casco por secciones en rombo (como Vector). s = [{z,hw,top,bot,side}], nose/tail = punto opcional.
Ship.prototype.loft = function (s, nose, tail, cx) {
  cx = cx || 0; let prev = null;
  for (let i = 0; i < s.length; i++) {
    const k = s[i];
    const r = [[cx, k.top, k.z], [cx + k.hw, k.side ?? 0.1, k.z], [cx, k.bot, k.z], [cx - k.hw, k.side ?? 0.1, k.z]];
    this.poly(r, 'c');
    for (let j = 0; j < 4; j++) {
      if (i === 0) { if (nose) this.add(nose, r[j]); } else this.add(prev[j], r[j]);
    }
    prev = r;
  }
  if (tail) for (let j = 0; j < 4; j++) this.add(prev[j], tail);
};
// Casco por secciones hexagonales (más robusto). s = [{z,hw,top,bot,mid}]
Ship.prototype.loft6 = function (s, nose, cx) {
  cx = cx || 0; let prev = null;
  for (let i = 0; i < s.length; i++) {
    const k = s[i], m = k.mid ?? 0.15, w2 = k.hw * 0.55;
    const r = [[cx - w2, k.top, k.z], [cx + w2, k.top, k.z], [cx + k.hw, m, k.z], [cx + w2, k.bot, k.z], [cx - w2, k.bot, k.z], [cx - k.hw, m, k.z]];
    this.poly(r, 'c');
    for (let j = 0; j < 6; j++) {
      if (i === 0) { if (nose) this.add(nose, r[j]); } else this.add(prev[j], r[j]);
    }
    prev = r;
  }
};
function ring(cx, cy, z, rad, n) {
  const o = []; n = n || 6;
  for (let k = 0; k < n; k++) { const a = Math.PI * 2 * k / n; o.push([cx + Math.cos(a) * rad, cy + Math.sin(a) * rad, z]); }
  return o;
}
// Tobera como las de Vector: anillo delantero, trasero, boca azul y núcleo.
Ship.prototype.nozzle = function (cx, cy, z0, z1, r) {
  const f = ring(cx, cy, z0, r), b = ring(cx, cy, z1, r), n = ring(cx, cy, z1 + 0.4, r * 0.7), core = [cx, cy, z1 + 0.2];
  for (let k = 0; k < 6; k++) {
    this.add(f[k], f[(k + 1) % 6]); this.add(b[k], b[(k + 1) % 6]); this.add(f[k], b[k]);
    this.add(n[k], n[(k + 1) % 6], 'b'); this.add(b[k], n[k]); this.add(core, n[k], 'b');
  }
  this.nozzles.push([cx, cy]);
};
// Pieza simétrica: llama a fn(s) con s = +1 y -1.
Ship.prototype.mirror = function (fn) { fn(1); fn(-1); };

// ---------- naves ----------
const ships = [];
function def(id, group, code, name, desc, build) { const s = new Ship(); build(s); ships.push({ id, group, code, name, desc, ship: s }); }

// VECTOR: copia exacta de la geometría de Spaceship.cs (la misma que el holograma del selector).
def('Vector', 'VECTOR', 'REF', 'VECTOR', 'Nave actual · referencia de tamaño', s => {
  const zs = [-2.5, -0.5, 1.5, 2.6], hw = [0.5, 0.9, 0.8, 0.7], top = [0.35, 0.6, 0.55, 0.5], bot = [-0.2, -0.3, -0.3, -0.3], side = [0.05, 0.1, 0.1, 0.1];
  s.loft(zs.map((z, i) => ({ z, hw: hw[i], top: top[i], bot: bot[i], side: side[i] })), [0, 0, -4]);
  s.mirror(k => {
    const rf = [k * 0.9, 0.1, -0.5], rr = [k * 0.8, 0.1, 2.0], tf = [k * 3.4, -0.05, 2.2], tr = [k * 3.4, -0.05, 2.7], fin = [k * 3.4, 0.7, 2.7];
    s.poly([rf, tf, tr, rr]); s.add(rf, tr); s.add(tf, fin); s.add(fin, tr);
  });
  // toberas de Vector (z 0.8 → 2.8, boca a 3.2, radio 0.4)
  s.mirror(k => s.nozzle(k * 0.75, 0.1, 0.8, 2.8, 0.4));
});

// ---- INTERCEPTOR: punta alta, poca tolerancia ----
def('Interceptor_A', 'INTERCEPTOR', 'A', 'DARDO', 'Aguja larga sin alas · canards y estabilizadores cortos · 1 tobera', s => {
  s.loft([
    { z: -3.2, hw: 0.3, top: 0.28, bot: -0.12, side: 0.05 },
    { z: -0.8, hw: 0.55, top: 0.5, bot: -0.22 },
    { z: 2.0, hw: 0.55, top: 0.48, bot: -0.24 },
    { z: 3.4, hw: 0.48, top: 0.42, bot: -0.24 }
  ], [0, 0.02, -5.6]);
  s.mirror(k => {
    s.poly([[k * 0.38, 0.05, -2.4], [k * 1.05, 0, -1.7], [k * 1.05, 0, -1.4], [k * 0.45, 0.06, -1.6]]);
    s.poly([[k * 0.52, 0.06, 2.0], [k * 1.25, 0, 3.0], [k * 1.25, 0, 3.4], [k * 0.48, 0.06, 3.4]]);
    s.add([k * 1.25, 0, 3.0], [k * 1.25, 0.45, 3.4]); s.add([k * 1.25, 0.45, 3.4], [k * 1.25, 0, 3.4]);
  });
  s.nozzle(0, 0.1, 2.4, 3.6, 0.36);
});

def('Interceptor_B', 'INTERCEPTOR', 'B', 'DELTA', 'Fuselaje estrecho · ala delta muy barrida · 2 toberas juntas', s => {
  s.loft([
    { z: -3.0, hw: 0.32, top: 0.3, bot: -0.12, side: 0.05 },
    { z: -0.5, hw: 0.6, top: 0.52, bot: -0.24 },
    { z: 2.2, hw: 0.62, top: 0.48, bot: -0.26 },
    { z: 3.2, hw: 0.58, top: 0.44, bot: -0.26 }
  ], [0, 0.02, -5.0]);
  s.mirror(k => {
    const rf = [k * 0.5, 0.05, -1.6], tip = [k * 2.3, -0.05, 2.7], tipR = [k * 2.3, -0.05, 3.1], rr = [k * 0.6, 0.05, 3.1];
    s.poly([rf, tip, tipR, rr]); s.add(rf, tipR);
    s.add([k * 0.55, 0.05, 0.6], [k * 1.55, -0.02, 2.0]);
    s.add(tip, [k * 2.3, 0.5, 3.1]); s.add([k * 2.3, 0.5, 3.1], tipR);
  });
  s.mirror(k => s.nozzle(k * 0.32, 0.1, 2.0, 3.2, 0.3));
});

def('Interceptor_C', 'INTERCEPTOR', 'C', 'AGUJA', 'Sin alas horizontales · dos derivas altas inclinadas · la más estrecha', s => {
  s.loft([
    { z: -3.4, hw: 0.28, top: 0.26, bot: -0.12, side: 0.04 },
    { z: -1.0, hw: 0.5, top: 0.5, bot: -0.22 },
    { z: 1.8, hw: 0.55, top: 0.52, bot: -0.24 },
    { z: 3.3, hw: 0.5, top: 0.46, bot: -0.24 }
  ], [0, 0.02, -5.8]);
  s.mirror(k => {
    const b0 = [k * 0.42, 0.42, 1.0], b1 = [k * 0.42, 0.42, 3.3], t0 = [k * 0.95, 1.55, 2.6], t1 = [k * 0.95, 1.55, 3.4];
    s.poly([b0, t0, t1, b1]); s.add(b0, t1);
    // quilla ventral corta
    s.poly([[k * 0.3, -0.2, 1.6], [k * 0.55, -0.6, 2.9], [k * 0.55, -0.6, 3.3], [k * 0.3, -0.22, 3.3]]);
  });
  s.mirror(k => s.nozzle(k * 0.26, 0.12, 2.2, 3.4, 0.24));
});

def('Interceptor_D', 'INTERCEPTOR', 'D', 'CATAMARÁN', 'Dos cascos finos unidos por un ala · cabina central adelantada', s => {
  s.mirror(k => s.loft([
    { z: -2.4, hw: 0.22, top: 0.2, bot: -0.12, side: 0.04 },
    { z: 0.0, hw: 0.32, top: 0.34, bot: -0.2 },
    { z: 2.8, hw: 0.3, top: 0.32, bot: -0.2 }
  ], [k * 1.25, 0.02, -4.6], null, k * 1.25));
  // ala que une los cascos
  s.poly([[-0.95, 0.08, -0.6], [0.95, 0.08, -0.6], [0.95, 0.08, 1.8], [-0.95, 0.08, 1.8]]);
  s.add([-0.95, 0.08, 0.6], [0.95, 0.08, 0.6]);
  // cápsula de cabina central
  s.loft([
    { z: -2.6, hw: 0.28, top: 0.32, bot: -0.08, side: 0.08 },
    { z: -1.0, hw: 0.42, top: 0.5, bot: -0.12, side: 0.1 },
    { z: 0.6, hw: 0.36, top: 0.42, bot: 0.0, side: 0.1 }
  ], [0, 0.1, -3.8], [0, 0.1, 1.6]);
  s.mirror(k => s.nozzle(k * 1.25, 0.08, 2.2, 3.0, 0.26));
});

def('Interceptor_E', 'INTERCEPTOR', 'E', 'HOJA', 'Cuña plana muy baja · forma de flecha · 2 toberas planas', s => {
  s.loft([
    { z: -3.0, hw: 0.45, top: 0.16, bot: -0.08, side: 0.03 },
    { z: -1.0, hw: 0.9, top: 0.26, bot: -0.12, side: 0.04 },
    { z: 1.2, hw: 1.35, top: 0.3, bot: -0.14, side: 0.04 },
    { z: 2.8, hw: 1.55, top: 0.28, bot: -0.14, side: 0.04 }
  ], [0, 0.02, -5.2]);
  // cubierta de cabina
  s.loft([
    { z: -1.8, hw: 0.2, top: 0.3, bot: 0.18, side: 0.22 },
    { z: -0.4, hw: 0.32, top: 0.48, bot: 0.24, side: 0.26 },
    { z: 0.8, hw: 0.26, top: 0.4, bot: 0.28, side: 0.28 }
  ], [0, 0.18, -2.6], [0, 0.29, 1.6]);
  // puntas traseras en flecha
  s.mirror(k => { s.poly([[k * 1.55, 0.04, 2.8], [k * 1.6, 0.0, 3.6], [k * 1.1, 0.04, 2.8]]); });
  s.mirror(k => s.nozzle(k * 0.6, 0.08, 2.2, 2.9, 0.2));
});

// ---- VANGUARD: aceleración y salida fuerte ----
def('Vanguard_A', 'VANGUARD', 'A', 'BLOQUE', 'Casco corto y alto · alas cortas · 4 toberas en bloque 2×2', s => {
  s.loft6([
    { z: -2.4, hw: 0.7, top: 0.45, bot: -0.15, mid: 0.12 },
    { z: -0.8, hw: 1.2, top: 0.85, bot: -0.32 },
    { z: 1.4, hw: 1.3, top: 0.9, bot: -0.35 },
    { z: 2.3, hw: 1.25, top: 0.85, bot: -0.35 }
  ], [0, 0.1, -3.6]);
  s.mirror(k => {
    s.poly([[k * 1.3, 0.15, 0.0], [k * 2.2, 0.05, 1.1], [k * 2.2, 0.05, 1.9], [k * 1.3, 0.15, 2.0]]);
    s.add([k * 1.3, 0.15, 0.0], [k * 2.2, 0.05, 1.9]);
  });
  s.mirror(k => { s.nozzle(k * 0.55, 0.52, 2.3, 3.0, 0.3); s.nozzle(k * 0.55, -0.02, 2.3, 3.0, 0.3); });
});

def('Vanguard_B', 'VANGUARD', 'B', 'TRIMOTOR', 'Tres motores grandes en triángulo · alas cortas caídas', s => {
  s.loft([
    { z: -2.2, hw: 0.55, top: 0.4, bot: -0.15, side: 0.08 },
    { z: -0.4, hw: 1.0, top: 0.8, bot: -0.3 },
    { z: 1.4, hw: 1.05, top: 0.85, bot: -0.32 }
  ], [0, 0.05, -3.6]);
  s.mirror(k => {
    const r0 = [k * 1.0, 0.1, -0.3], t0 = [k * 2.1, -0.3, 0.8], t1 = [k * 2.1, -0.3, 1.5], r1 = [k * 1.0, 0.1, 1.5];
    s.poly([r0, t0, t1, r1]); s.add(r0, t1);
  });
  s.nozzle(0, 0.62, 1.2, 2.6, 0.44);
  s.mirror(k => s.nozzle(k * 0.62, -0.05, 1.2, 2.6, 0.44));
});

def('Vanguard_C', 'VANGUARD', 'C', 'MARTILLO', 'Ala canard ancha en el morro · cuerpo estrecho detrás · 2 toberas grandes', s => {
  s.loft([
    { z: -2.6, hw: 0.6, top: 0.45, bot: -0.2, side: 0.08 },
    { z: -1.0, hw: 0.85, top: 0.7, bot: -0.3 },
    { z: 1.2, hw: 0.9, top: 0.72, bot: -0.32 },
    { z: 2.2, hw: 0.85, top: 0.68, bot: -0.32 }
  ], [0, 0.05, -3.6]);
  s.mirror(k => {
    const a = [k * 0.6, 0.12, -3.0], b = [k * 2.5, 0.0, -2.6], c = [k * 2.5, 0.0, -1.7], d = [k * 0.8, 0.12, -1.5];
    s.poly([a, b, c, d]); s.add(a, c);
    s.add(b, [k * 2.5, -0.5, -2.2]); s.add([k * 2.5, -0.5, -2.2], c);
    // aletas traseras pequeñas
    s.poly([[k * 0.85, 0.3, 1.0], [k * 1.3, 0.9, 2.0], [k * 1.3, 0.9, 2.4], [k * 0.8, 0.3, 2.2]]);
  });
  s.mirror(k => s.nozzle(k * 0.45, 0.2, 1.4, 2.6, 0.4));
});

def('Vanguard_D', 'VANGUARD', 'D', 'ESCARABAJO', 'Caparazón hexagonal · dos góndolas laterales con los motores', s => {
  s.loft6([
    { z: -2.2, hw: 0.75, top: 0.45, bot: -0.15, mid: 0.1 },
    { z: -0.6, hw: 1.15, top: 0.8, bot: -0.3 },
    { z: 1.2, hw: 1.1, top: 0.78, bot: -0.3 },
    { z: 2.0, hw: 0.9, top: 0.6, bot: -0.25 }
  ], [0, 0.1, -3.2]);
  s.mirror(k => {
    s.loft6([
      { z: -0.8, hw: 0.32, top: 0.38, bot: -0.2, mid: 0.08 },
      { z: 1.8, hw: 0.45, top: 0.52, bot: -0.3, mid: 0.1 }
    ], [k * 1.75, 0.08, -1.6], k * 1.75);
    s.add([k * 1.1, 0.15, -0.4], [k * 1.43, 0.1, -0.2]); s.add([k * 1.1, 0.15, 1.2], [k * 1.3, 0.1, 1.4]);
    s.nozzle(k * 1.75, 0.1, 1.8, 2.6, 0.4);
  });
});

def('Vanguard_E', 'VANGUARD', 'E', 'ARIETE', 'Cuña baja que sube a un bloque trasero · 3 motores en fila', s => {
  s.loft6([
    { z: -2.8, hw: 0.8, top: 0.15, bot: -0.12, mid: 0.0 },
    { z: -1.0, hw: 1.3, top: 0.45, bot: -0.28, mid: 0.05 },
    { z: 0.8, hw: 1.45, top: 0.85, bot: -0.32, mid: 0.1 },
    { z: 1.9, hw: 1.45, top: 0.95, bot: -0.32, mid: 0.1 }
  ], [0, -0.02, -4.0]);
  s.mirror(k => {
    s.poly([[k * 1.0, 0.85, 0.6], [k * 1.15, 1.6, 1.6], [k * 1.15, 1.6, 2.0], [k * 1.0, 0.92, 1.9]]);
  });
  s.nozzle(0, 0.32, 1.9, 2.6, 0.38);
  s.mirror(k => s.nozzle(k * 0.85, 0.32, 1.9, 2.6, 0.38));
});

// ---------- medidas ----------
function measure(L) {
  let x = 0, z0 = 1e9, z1 = -1e9, y0 = 1e9, y1 = -1e9;
  L.forEach(l => [l[0], l[1]].forEach(p => {
    x = Math.max(x, Math.abs(p[0])); z0 = Math.min(z0, p[2]); z1 = Math.max(z1, p[2]); y0 = Math.min(y0, p[1]); y1 = Math.max(y1, p[1]);
  }));
  return { half: x, len: z1 - z0, h: y1 - y0, z0, z1, y0, y1 };
}
const vectorShip = ships[0].ship;
const VM = measure(vectorShip.L);

// ---------- proyecciones ----------
const f1 = v => v.toFixed(1);
function pathOf(L, P, filter) {
  let d = '';
  L.forEach(l => { if (filter && !filter(l)) return; const a = P(l[0]), b = P(l[1]); d += 'M' + f1(a[0]) + ' ' + f1(a[1]) + 'L' + f1(b[0]) + ' ' + f1(b[1]); });
  return d || 'M0 0';
}
const SCALE = 26; // px por metro, igual en todas las naves para comparar tamaños

function rotX(a) { const c = Math.cos(a), s = Math.sin(a); return [1, 0, 0, 0, c, -s, 0, s, c]; }
function rotY(a) { const c = Math.cos(a), s = Math.sin(a); return [c, 0, s, 0, 1, 0, -s, 0, c]; }
function mul(A, B) { const o = []; for (let i = 0; i < 3; i++) for (let j = 0; j < 3; j++) o.push(A[i * 3] * B[j] + A[i * 3 + 1] * B[3 + j] + A[i * 3 + 2] * B[6 + j]); return o; }
const R34 = mul(rotX(0.38), rotY(2.5)); // misma orientación inicial que el holograma del selector

function views(ship, M, W, H) {
  const zc = (M.z0 + M.z1) / 2, yc = (M.y0 + M.y1) / 2;
  // Planta: morro hacia arriba
  const top = p => [W / 2 + p[0] * SCALE, H / 2 + (p[2] - zc) * SCALE];
  // Perfil: morro a la izquierda
  const side = p => [W / 2 + (p[2] - zc) * SCALE, H / 2 - (p[1] - yc) * SCALE];
  // 3/4 con perspectiva suave
  const D = 18, K = 30;
  const tq = p => {
    const x = p[0], y = p[1] - yc, z = p[2] - zc;
    const X = R34[0] * x + R34[1] * y + R34[2] * z, Y = R34[3] * x + R34[4] * y + R34[5] * z, Z = R34[6] * x + R34[7] * y + R34[8] * z;
    const s = D / (D - Z); return [W / 2 + X * K * s, H / 2 - Y * K * s, Z];
  };
  const tqDepth = l => (tq(l[0])[2] + tq(l[1])[2]) / 2;
  return { top, side, tq, tqDepth, zc, yc };
}

// ---------- artboard ----------
const W = 1240, BH = 560, VW = 380, VH = 400;

function svgView(label, inner, extra) {
  return `
      <div style="display: flex; flex-direction: column; gap: 8px;">
        <span style="font-family: 'Share Tech Mono', monospace; font-size: 13px; letter-spacing: 0.22em; color: #A6B8D8;">${label}</span>
        <svg role="img" aria-label="${extra}" width="${VW}" height="${VH}" viewBox="0 0 ${VW} ${VH}" style="display: block; border: 1px solid rgba(36,91,255,0.35); background: #070A14;">
          <defs>
            <filter id="g" x="-20%" y="-20%" width="140%" height="140%">
              <feGaussianBlur stdDeviation="1.8" result="b"></feGaussianBlur>
              <feMerge><feMergeNode in="b"></feMergeNode><feMergeNode in="SourceGraphic"></feMergeNode></feMerge>
            </filter>
          </defs>${inner}
        </svg>
      </div>`;
}

function grid(W, H) {
  let d = '';
  const step = SCALE; // 1 m
  for (let x = W / 2 % step; x < W; x += step) d += `M${f1(x)} 0L${f1(x)} ${H}`;
  for (let y = H / 2 % step; y < H; y += step) d += `M0 ${f1(y)}L${W} ${f1(y)}`;
  return `
          <path d="${d}" stroke="#245BFF" stroke-width="0.5" opacity="0.14" fill="none"></path>`;
}

function board(entry) {
  const s = entry.ship, M = measure(s.L), isRef = entry.group === 'VECTOR';
  const v = views(s, M, VW, VH), vv = views(vectorShip, VM, VW, VH);
  const cyan = l => l[2] !== 'b', blue = l => l[2] === 'b';
  const ghost = (P) => isRef ? '' : `
          <path d="${pathOf(vectorShip.L, P)}" stroke="#A6B8D8" stroke-width="0.8" opacity="0.22" fill="none" stroke-dasharray="2 3"></path>`;
  const shipPaths = (P) => `
          <g filter="url(#g)" fill="none" stroke-linecap="round">
            <path d="${pathOf(s.L, P, blue)}" stroke="#245BFF" stroke-width="1.3"></path>
            <path d="${pathOf(s.L, P, cyan)}" stroke="#36E7FF" stroke-width="1.3"></path>
          </g>`;

  // planta: banda de colisión (semiancho medido)
  const cx = VW / 2, hw = M.half * SCALE;
  const band = `
          <line x1="${f1(cx - hw)}" y1="14" x2="${f1(cx - hw)}" y2="${VH - 14}" stroke="#FF9D32" stroke-width="1" stroke-dasharray="4 5" opacity="0.7"></line>
          <line x1="${f1(cx + hw)}" y1="14" x2="${f1(cx + hw)}" y2="${VH - 14}" stroke="#FF9D32" stroke-width="1" stroke-dasharray="4 5" opacity="0.7"></line>`;
  const vband = isRef ? '' : `
          <line x1="${f1(cx - 1.9 * SCALE)}" y1="14" x2="${f1(cx - 1.9 * SCALE)}" y2="${VH - 14}" stroke="#A6B8D8" stroke-width="0.8" stroke-dasharray="1 4" opacity="0.4"></line>
          <line x1="${f1(cx + 1.9 * SCALE)}" y1="14" x2="${f1(cx + 1.9 * SCALE)}" y2="${VH - 14}" stroke="#A6B8D8" stroke-width="0.8" stroke-dasharray="1 4" opacity="0.4"></line>`;

  // 3/4 por bandas de profundidad (aristas lejanas más tenues, como el holograma)
  const bandOf = l => { const z = v.tqDepth(l); return z < -1.2 ? 0.35 : (z < 1.2 ? 0.65 : 1); };
  let tqInner = '';
  [0.35, 0.65, 1].forEach(o => {
    tqInner += `
            <path d="${pathOf(s.L, v.tq, l => blue(l) && bandOf(l) === o)}" stroke="#245BFF" stroke-width="1.3" opacity="${o}"></path>
            <path d="${pathOf(s.L, v.tq, l => cyan(l) && bandOf(l) === o)}" stroke="#36E7FF" stroke-width="1.3" opacity="${o}"></path>`;
  });
  const tq = `
          <g filter="url(#g)" fill="none" stroke-linecap="round">${tqInner}
          </g>`;

  const scaleBar = `
          <line x1="16" y1="${VH - 18}" x2="${16 + 2 * SCALE}" y2="${VH - 18}" stroke="#A6B8D8" stroke-width="1"></line>
          <line x1="16" y1="${VH - 22}" x2="16" y2="${VH - 14}" stroke="#A6B8D8" stroke-width="1"></line>
          <line x1="${16 + 2 * SCALE}" y1="${VH - 22}" x2="${16 + 2 * SCALE}" y2="${VH - 14}" stroke="#A6B8D8" stroke-width="1"></line>
          <text x="${20 + 2 * SCALE}" y="${VH - 14}" font-family="Share Tech Mono, monospace" font-size="11" fill="#A6B8D8">2 m</text>`;

  const fmt = n => n.toFixed(1).replace('.', ',');
  const dl = (v, vRef) => isRef ? '' : ` <span style="color: ${v > vRef + 0.05 ? '#FF9D32' : (v < vRef - 0.05 ? '#36E7FF' : '#A6B8D8')};">(${v >= vRef ? '+' : '−'}${fmt(Math.abs(v - vRef))})</span>`;
  const stats = [
    ['LARGO', fmt(M.len) + ' m', dl(M.len, VM.len)],
    ['ENVERGADURA', fmt(M.half * 2) + ' m', dl(M.half * 2, VM.half * 2)],
    ['ALTO', fmt(M.h) + ' m', dl(M.h, VM.h)],
    ['SEMIANCHO GEOMÉTRICO', fmt(M.half) + ' m', dl(M.half, VM.half)],
    ['TOBERAS', String(s.nozzles.length), '']
  ];
  const statHtml = stats.map(st => `
        <div style="display: flex; flex-direction: column; gap: 4px;">
          <span style="font-family: 'Share Tech Mono', monospace; font-size: 11px; letter-spacing: 0.16em; color: #A6B8D8;">${st[0]}</span>
          <span style="font-family: 'Orbitron', sans-serif; font-weight: 500; font-size: 17px; letter-spacing: 0.04em; color: #36E7FF;">${st[1]}<span style="font-family: 'Share Tech Mono', monospace; font-size: 13px;">${st[2]}</span></span>
        </div>`).join('');

  const head = isRef ? 'REFERENCE' : `${entry.group} · OPTION ${entry.code}`;
  const legend = isRef
    ? 'Naranja: semiancho geométrico (punta de ala al eje). La colisión de hoy usa 1,9 m.'
    : 'Gris punteado: Vector, para comparar tamaño. Naranja: semiancho de esta nave. Gris fino: semiancho de Vector.';

  return `<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>${entry.name}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Orbitron:wght@500;700;900&amp;family=Share+Tech+Mono&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:#05070D}
</style>
</helmet>
<div style="width: ${W}px; height: ${BH}px; box-sizing: border-box; padding: 28px 32px; background: #05070D; display: flex; flex-direction: column; gap: 18px; font-family: 'Orbitron', sans-serif;">
  <div style="display: flex; justify-content: space-between; align-items: flex-end; gap: 24px;">
    <div style="display: flex; flex-direction: column; gap: 6px;">
      <span style="font-family: 'Share Tech Mono', monospace; font-size: 13px; letter-spacing: 0.22em; color: ${isRef ? '#A6B8D8' : '#36E7FF'};">${head}</span>
      <div style="display: flex; align-items: baseline; gap: 18px;">
        <h1 style="margin: 0; font-weight: 900; font-size: 34px; letter-spacing: 0.08em; color: #36E7FF; text-shadow: 0 0 14px rgba(54,231,255,0.45);">${entry.name}</h1>
        <span style="font-family: 'Share Tech Mono', monospace; font-size: 14px; letter-spacing: 0.08em; color: #A6B8D8;">${entry.desc}</span>
      </div>
    </div>
    <div style="display: flex; gap: 26px;">${statHtml}
    </div>
  </div>
  <div style="display: flex; gap: 20px;">${svgView('PLANTA', grid(VW, VH) + ghost(vv.top) + vband + band + shipPaths(v.top) + scaleBar, entry.name + ' vista en planta')}${svgView('PERFIL', grid(VW, VH) + ghost(vv.side) + shipPaths(v.side) + scaleBar, entry.name + ' vista de perfil')}${svgView('TRES CUARTOS', tq, entry.name + ' vista de tres cuartos')}
  </div>
  <span style="font-family: 'Share Tech Mono', monospace; font-size: 12px; letter-spacing: 0.06em; color: rgba(166,184,216,0.8);">${legend}</span>
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{"$preview":{"width":${W},"height":${BH}}}'>
class Component extends DCLogic {
  renderVals() { return {}; }
}
</script>
</body>
</html>
`;
}

// ---------- escribir ----------
const files = {};
ships.forEach(e => {
  const name = e.id === 'Vector' ? 'Main.dc.html' : e.id + '.dc.html';
  fs.writeFileSync(path.join(OUT, name), board(e));
  files[e.id] = name;
  const M = measure(e.ship.L);
  console.log(e.id, 'len', M.len.toFixed(2), 'half', M.half.toFixed(2), 'h', M.h.toFixed(2), 'noz', e.ship.nozzles.length, 'lines', e.ship.L.length);
});

// índice del lienzo
const GAP = 80, ROW = BH + 360;
const boards = {}, order = [];
const rowY = { VECTOR: 300, INTERCEPTOR: 300 + ROW, VANGUARD: 300 + 2 * ROW };
const col = { VECTOR: 0, INTERCEPTOR: 0, VANGUARD: 0 };
ships.forEach(e => {
  const f = files[e.id];
  boards[f] = { x: col[e.group] * (W + GAP), y: rowY[e.group], w: W, h: BH, title: e.group === 'VECTOR' ? 'Vector · referencia' : `${e.group[0] + e.group.slice(1).toLowerCase()} ${e.code} · ${e.name[0] + e.name.slice(1).toLowerCase()}` };
  col[e.group]++;
  order.push(f);
});
const rowW = 5 * W + 4 * GAP;
const canvas = {
  v: 3,
  createdOnFiles: { v: 1, at: new Date().toISOString().replace(/\.\d+Z$/, 'Z') },
  title: 'VECTORY — Naves',
  launch: { view: 'canvas' },
  pages: [],
  boards, order,
  notes: {
    tRef: { x: 0, y: rowY.VECTOR - 260, text: 'Referencia: Vector', kind: 'title1', maxW: W },
    tInt: { x: 0, y: rowY.INTERCEPTOR - 260, text: 'Interceptor · 5 opciones', kind: 'title1', maxW: rowW },
    tVan: { x: 0, y: rowY.VANGUARD - 260, text: 'Vanguard · 5 opciones', kind: 'title1', maxW: rowW }
  },
  designSystems: []
};
fs.writeFileSync(path.join(OUT, 'canvas.json'), JSON.stringify(canvas, null, 1));
