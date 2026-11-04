// VECTORY — Cabinas definitivas de HOJA (Interceptor): 1 · BURBUJA y MARTILLO (Vanguard): 1 · BÚNKER.
// Pantalla de juego 1280×720 (px). Usa las piezas comunes de cabinas-lib.js (en el repo: lib.js).
// Ejecutar con: node cabinas-hoja-martillo-def.js  → escribe project/Cab_Hoja_1.dc.html y project/Cab_Martillo_1.dc.html
const fs = require('fs');
const path = require('path');
const L = require('./lib.js');
const { C, SCREEN, pts, off, line, panel, pillar, screw, vent, switches, seam, brackets, screen, micro, label, speedText, T, orbs, diamonds, boostBars, dmg, dmgText, speedBar, board, OUT } = L;

// ---------- piezas extra ----------
function chevrons(x, y, s, gap) {
  let o = '';
  for (let i = 0; i < 3; i++) {
    const yy = y - i * (s * 0.6 + gap);
    o += `<polygon points="${pts([[x - s, yy + s * 0.6], [x, yy], [x + s, yy + s * 0.6], [x + s, yy + s * 0.6 + 5], [x, yy + 5], [x - s, yy + s * 0.6 + 5]])}" fill="{{o${i}.fill}}" fill-opacity="{{o${i}.fo}}" stroke="{{o${i}.stroke}}" stroke-opacity="{{o${i}.so}}" stroke-width="1" filter="{{o${i}.filter}}"${T}></polygon>`;
  }
  return o;
}
function tris(x, y, w, h, gap) {
  let o = '';
  for (let i = 0; i < 3; i++) {
    const bx = x + i * (w / 2 + gap), up = i % 2 === 0;
    const p = up ? [[bx, y + h], [bx + w / 2, y], [bx + w, y + h]] : [[bx, y], [bx + w, y], [bx + w / 2, y + h]];
    o += `<polygon class="{{d${i}.cls}}" points="${pts(p)}" fill="{{d${i}.fill}}" fill-opacity="{{d${i}.fo}}" stroke="{{d${i}.stroke}}" stroke-opacity="{{d${i}.so}}" stroke-width="1" filter="{{d${i}.filter}}"${T}></polygon>`;
  }
  return o;
}
function tubes(x, y, w, h, gap) {
  let o = '';
  for (let i = 0; i < 3; i++) {
    const bx = x + i * (w + gap);
    o += `<rect x="${bx - 3}" y="${y - 4}" width="${w + 6}" height="${h + 8}" rx="${(w + 6) / 2}" fill="#04070F" stroke="${C.blue}" stroke-width="1"></rect>`;
    o += `<rect x="${bx}" y="${y}" width="${w}" height="${h}" rx="${w / 2}" fill="{{o${i}.fill}}" fill-opacity="{{o${i}.fo}}" stroke="{{o${i}.stroke}}" stroke-opacity="{{o${i}.so}}" stroke-width="1" filter="{{o${i}.filter}}"${T}></rect>`;
    for (let k = 1; k < 4; k++) o += line([[bx + 1, y + h * k / 4], [bx + w - 1, y + h * k / 4]], C.bg, 1, 0.6);
  }
  return o;
}
function plates(x, y, w, h, gap) {
  let o = '';
  for (let i = 0; i < 3; i++) {
    const bx = x + i * (w + gap);
    o += `<rect class="{{d${i}.cls}}" x="${bx}" y="${y}" width="${w}" height="${h}" fill="{{d${i}.fill}}" fill-opacity="{{d${i}.fo}}" stroke="{{d${i}.stroke}}" stroke-opacity="{{d${i}.so}}" stroke-width="1.4" filter="{{d${i}.filter}}"${T}></rect>`;
    o += screw(bx + 5, y + 5) + screw(bx + w - 5, y + h - 5);
  }
  return o;
}
// Barra vertical de velocidad (rellena de abajo arriba). Necesita extra vbar.
function vbar(x, y, w, h) {
  let tk = '';
  for (let i = 0; i <= 10; i++) { const yy = y + h - h * i / 10; tk += `M${x + w + 2} ${yy.toFixed(1)}L${x + w + (i % 5 === 0 ? 10 : 6)} ${yy.toFixed(1)}`; }
  const topY = y + h - h * 396 / 496;
  return `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${C.blue}" opacity="0.3"></rect>` +
    `<rect x="${x}" y="{{vY}}" width="${w}" height="{{vH}}" fill="{{spdColor}}" filter="url(#glowSoft)"${T}></rect>` +
    `<path d="${tk}" stroke="${C.grey}" stroke-width="1" opacity="0.6"></path>` +
    `<line x1="${x - 3}" y1="${topY.toFixed(1)}" x2="${x + w + 3}" y2="${topY.toFixed(1)}" stroke="${C.orange}" stroke-width="1" opacity="0.8"></line>`;
}
const vbarExtra = (y, h) => `out.vH = (${h} * f).toFixed(1); out.vY = (${y} + ${h} - ${h} * f).toFixed(1);`;

// Martillo: morro corto y ancho, con el canard cruzando delante y las puntas caídas.
function martilloNose(y0, cy) {
  let s = '';
  [-1, 1].forEach(k => {
    const X = x => 640 + k * x;
    const wing = [[X(110), cy], [X(520), cy - 10], [X(530), cy + 10], [X(510), cy + 22], [X(120), cy + 30]];
    s += `<polygon points="${pts(wing)}" fill="url(#pTop)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>`;
    s += line([[X(140), cy + 12], [X(500), cy + 4]], C.blue, 1, 0.55);
    // punta caída
    s += `<polygon points="${pts([[X(520), cy - 10], [X(560), cy + 40], [X(550), cy + 52], [X(530), cy + 10]])}" fill="url(#pFront)" stroke="${C.cyan}" stroke-width="1.4" stroke-linejoin="round"></polygon>`;
    s += `<circle cx="${X(540)}" cy="${cy + 34}" r="2" fill="${C.orange}" opacity="0.8"></circle>`;
  });
  s += `<polygon points="${pts([[500, y0], [560, cy - 6], [720, cy - 6], [780, y0]])}" fill="url(#noseL)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>`;
  s += line([[560, cy - 6], [720, cy - 6]], '#BFF6FF', 1, 0.5) + line([[600, cy + 10], [680, cy + 10]], C.blue, 1, 0.5);
  return s;
}

// H1 · BURBUJA (v2): arco de burbuja fino con canto, pilares finos hasta la visera, ventanas laterales,
// salpicadero en dos planos más alto, pantalla larga hundida y morro de cubierta plana larguísimo.
const bezH = (p0, p1, p2, n) => { const o = []; for (let i = 0; i <= n; i++) { const t = i / n, a = (1 - t) * (1 - t), b = 2 * (1 - t) * t, c = t * t; o.push([a * p0[0] + b * p1[0] + c * p2[0], a * p0[1] + b * p1[1] + c * p2[1]]); } return o; };
const mirX = a => a.map(p => [1280 - p[0], p[1]]);
const deepDefs = `<linearGradient id="shadowDown" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#000" stop-opacity="0.7"></stop><stop offset="1" stop-color="#000" stop-opacity="0"></stop></linearGradient>
<linearGradient id="aoL" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#000" stop-opacity="0.55"></stop><stop offset="1" stop-color="#000" stop-opacity="0"></stop></linearGradient>
<linearGradient id="aoR" x1="1" y1="0" x2="0" y2="0"><stop offset="0" stop-color="#000" stop-opacity="0.55"></stop><stop offset="1" stop-color="#000" stop-opacity="0"></stop></linearGradient>
<linearGradient id="cowl" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1C2A48"></stop><stop offset="0.5" stop-color="#101A30"></stop><stop offset="1" stop-color="#070B16"></stop></linearGradient>
<linearGradient id="bezelIn" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#000" stop-opacity="0.8"></stop><stop offset="1" stop-color="#000" stop-opacity="0"></stop></linearGradient>
<linearGradient id="deckFar" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#0C1324"></stop><stop offset="1" stop-color="#1A2744"></stop></linearGradient>`;
function sinkH(fr, grow) {
  const rim = L.inset(fr, -grow);
  let o = `<polygon points="${pts(rim)}" fill="#02030A" stroke="${C.cyan}" stroke-width="1.4" stroke-linejoin="round"></polygon>`;
  [[0, 1, 'url(#bezelIn)', 1], [1, 2, '#070B16', 0.9], [3, 0, '#070B16', 0.9], [2, 3, '#0D1424', 0.8]].forEach(([i, j, f, op]) => { o += `<polygon points="${pts([rim[i], rim[j], fr[j], fr[i]])}" fill="${f}" opacity="${op}" stroke="${C.blue}" stroke-width="0.8" stroke-opacity="0.5"></polygon>`; });
  return o + line([rim[0], rim[1]], '#BFF6FF', 1, 0.3);
}
// Morro de Hoja: cubierta plana larguísima que llega casi al horizonte, con juntas que convergen y paneles por tramos.
function hojaLongNose(y0, hw, tipY) {
  const t = [640, tipY], l = [640 - hw, y0], r = [640 + hw, y0];
  let s = `<polygon points="${pts([l, [640 - 10, tipY], [640 + 10, tipY], r])}" fill="url(#deckFar)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>`;
  // bisel de los bordes de la cubierta (canto fino)
  s += `<polygon points="${pts([l, [640 - 10, tipY], [640 - 14, tipY + 3], [640 - hw - 10, y0 + 6]])}" fill="#070B16" stroke="${C.blue}" stroke-width="0.8" stroke-opacity="0.6"></polygon>`;
  s += `<polygon points="${pts([r, [640 + 10, tipY], [640 + 14, tipY + 3], [640 + hw + 10, y0 + 6]])}" fill="#070B16" stroke="${C.blue}" stroke-width="0.8" stroke-opacity="0.6"></polygon>`;
  [0.25, 0.5, 0.75].forEach(f => { s += line([[640 - hw * f, y0], t], C.blue, 1, 0.5) + line([[640 + hw * f, y0], t], C.blue, 1, 0.5); });
  s += line([t, [640, y0]], '#BFF6FF', 1, 0.5);
  // juntas transversales, más juntas cuanto más lejos (sensación de largo)
  [0.15, 0.32, 0.5, 0.66, 0.79, 0.89, 0.96].forEach(k => { const yy = y0 - (y0 - tipY) * k, w = hw * (1 - k) + 10 * k; s += L.seam([[640 - w, yy], [640 + w, yy]]); });
  // pilotos de posición a lo largo del borde
  [0.3, 0.6, 0.85].forEach(k => { const yy = y0 - (y0 - tipY) * k, w = hw * (1 - k) + 10 * k; s += `<circle cx="${(640 - w + 6).toFixed(1)}" cy="${yy.toFixed(1)}" r="1.8" fill="${C.cyan}" opacity="0.7"></circle><circle cx="${(640 + w - 6).toFixed(1)}" cy="${yy.toFixed(1)}" r="1.8" fill="${C.cyan}" opacity="0.7"></circle>`; });
  s += `<circle cx="640" cy="${tipY + 2}" r="2.2" fill="${C.cyan}" opacity="0.9"></circle>`;
  return s;
}
// Rejilla en paralelogramo inclinada hacia fuera (dir -1 = lado izquierdo, +1 = derecho): más ancha y baja.
function ventOut(x, y, w, h, n, dir) {
  const sk = dir * h * 0.9;
  const box = [[x + sk, y], [x + w + sk, y], [x + w, y + h], [x, y + h]];
  let d = '';
  for (let i = 0; i < n; i++) { const f = (i + 0.5) / n; d += `M${(x + w * f).toFixed(1)} ${y + h}L${(x + w * f + sk).toFixed(1)} ${y}`; }
  return `<polygon points="${pts(box)}" fill="#04070F" stroke="${C.blue}" stroke-width="1" opacity="0.9"></polygon>` +
    `<polygon points="${pts([[x + sk, y], [x + w + sk, y], [x + w + sk * 0.7, y + 3], [x + sk * 0.7, y + 3]])}" fill="#000" opacity="0.6"></polygon>` +
    `<path d="${d}" stroke="${C.blue}" stroke-width="1.4" opacity="0.6"></path>` + line([box[3], box[2]], '#BFF6FF', 1, 0.2);
}
const HOJA_1 = ({
  id: 'Cab_Hoja_1', name: 'BURBUJA', top: 32,
  defs: deepDefs,
  svg: () => {
    const UP = 34;
    const cowlCurve = bezH([0, 548], [200, 608], [420, 612], 64);
    const cowlY0 = x => { const xx = x > 640 ? 1280 - x : x; if (xx >= 420) return 612; for (let i = 1; i < cowlCurve.length; i++) { const p0 = cowlCurve[i - 1], p1 = cowlCurve[i]; if (xx <= p1[0]) { const t = (xx - p0[0]) / (p1[0] - p0[0] || 1); return p0[1] + (p1[1] - p0[1]) * t; } } return 612; };
    const cowlY = x => cowlY0(x) - UP;
    let s = `<rect x="0" y="0" width="200" height="720" fill="url(#aoL)"></rect><rect x="1080" y="0" width="200" height="720" fill="url(#aoR)"></rect>`;
    s += hojaLongNose(580, 430, 440);
    // arco de la burbuja: fino, con canto
    const arcB = bezH([0, 112], [640, -48], [1280, 112], 28), arcE = bezH([0, 132], [640, -28], [1280, 132], 28);
    s += `<polygon points="${pts([...arcB, ...arcE.slice().reverse()])}" fill="url(#pFront)" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.7"></polygon>`;
    s += `<polygon points="${pts([[0, 0], [1280, 0], ...arcB.slice().reverse()])}" fill="url(#pPillar)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>`;
    s += line(arcE, '#BFF6FF', 1, 0.25) + line(bezH([120, 70], [640, -40], [1160, 70], 24), C.blue, 1, 0.45);
    s += `<rect x="604" y="12" width="72" height="18" fill="url(#pConsole)" stroke="${C.cyan}" stroke-width="1.2"></rect><circle cx="640" cy="21" r="4" fill="#04070F" stroke="${C.blue}" stroke-width="1"></circle><circle cx="640" cy="21" r="1.8" fill="${C.cyan}" opacity="0.7"></circle>`;
    s += screw(300, 56) + screw(980, 56) + screw(460, 30) + screw(820, 30);
    // franjas exteriores, ventanas laterales y pilares finos hasta la visera
    [[1, 0], [-1, 1280]].forEach(([k, o]) => {
      const X = x => o + k * x, yb = x => cowlY(X(x)) + 2;
      const arcAt = x => { const xx = k > 0 ? x : x; const t = xx / 1280; return (1 - t) * (1 - t) * 132 + 2 * (1 - t) * t * -28 + t * t * 132; };
      s += `<polygon points="${pts([[X(0), 110], [X(22), arcAt(22) - 2], [X(96), yb(96)], [X(0), yb(0)]])}" fill="url(#pPillar)" stroke="${C.cyan}" stroke-width="1.4" stroke-linejoin="round"></polygon>`;
      for (let y = 160; y < 520; y += 50) s += screw(X(12), y);
      s += `<polygon points="${pts([[X(22), arcAt(22)], [X(120), arcAt(120)], [X(300), yb(300)], [X(96), yb(96)]])}" fill="#000" opacity="0.18"></polygon>`;
      s += line([[X(26), arcAt(26) + 4], [X(100), yb(100) - 4]], C.cyan, 1, 0.25);
      const front = [[X(120), arcAt(120)], [X(146), arcAt(146) + 4], [X(332), yb(332)], [X(300), yb(300)]];
      const side = [[X(146), arcAt(146) + 4], [X(164), arcAt(164) + 6], [X(350), yb(350)], [X(332), yb(332)]];
      s += `<polygon points="${pts(side)}" fill="url(#pSide)" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.7"></polygon>`;
      s += `<polygon points="${pts(front)}" fill="url(#pPillar)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>`;
      s += line([front[1], front[2]], '#BFF6FF', 1, 0.3) + line([[X(133), arcAt(133) + 8], [X(316), yb(316) - 6]], C.blue, 1, 0.5);
      for (let t = 0.15; t < 0.95; t += 0.16) s += screw(X(134 + 182 * t).toFixed(1), (arcAt(134) + 8 + (yb(316) - arcAt(134) - 14) * t).toFixed(1));
      s += `<polygon points="${pts([[X(290), yb(290) - 14], [X(362), yb(362) - 14], [X(368), yb(368)], [X(284), yb(284)]])}" fill="url(#pConsole)" stroke="${C.cyan}" stroke-width="1.2" stroke-linejoin="round"></polygon>`;
    });
    // panel inferior robusto
    s += `<rect x="0" y="${720 - UP - 8}" width="1280" height="${UP + 8}" fill="url(#pFront)" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.7"></rect>`;
    s += L.seam([[0, 720 - UP + 4], [1280, 720 - UP + 4]]);
    for (let x = 40; x < 1280; x += 80) s += screw(x, 720 - UP / 2 + 2);
    s += ventOut(110, 720 - UP + 12, 240, 12, 26, -1) + ventOut(930, 720 - UP + 12, 240, 12, 26, 1) + micro(640, 716, 'HOJA · INT-02', 'middle', C.grey, 0.4, 8);
    s += `<g transform="translate(0 -${UP})">`;
    // salpicadero en dos planos: visera y frontal, con sombra
    const cowlTopL = bezH([0, 548], [200, 608], [420, 612], 16);
    const cowlTop = [...cowlTopL, [860, 612], ...mirX(cowlTopL).reverse()];
    const cowlLip = cowlTop.map(p => [p[0], p[1] + 16]);
    s += `<polygon points="${pts([...cowlTop, ...cowlLip.slice().reverse()])}" fill="url(#cowl)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>`;
    s += line(cowlTop.map(p => [p[0], p[1] + 3]), '#BFF6FF', 1, 0.35) + line(cowlTop.map(p => [p[0], p[1] + 9]), C.blue, 1, 0.45, ' stroke-dasharray="3 4"');
    s += `<polygon points="${pts([...cowlLip, [1280, 720], [0, 720]])}" fill="url(#pTop)" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.7"></polygon>`;
    s += `<polygon points="${pts([...cowlLip, ...cowlLip.map(p => [p[0], p[1] + 44]).reverse()])}" fill="url(#shadowDown)"></polygon>`;
    // pantalla larga hundida
    const frame = [[360, 640], [920, 640], [934, 702], [346, 702]];
    s += sinkH(frame, 18);
    s += screen([[380, 650], [900, 650], [912, 692], [368, 692]], [320, 60]);
    s += panel(frame, { fill: 'url(#pConsole)', bevel: 5, depth: 8 });
    s += `<polygon points="${pts([frame[0], frame[1], [frame[1][0], frame[1][1] + 14], [frame[0][0], frame[0][1] + 14]])}" fill="url(#shadowDown)" opacity="0.8"></polygon>`;
    s += screen([[380, 650], [900, 650], [912, 692], [368, 692]]);
    s += brackets(384, 654, 512, 34, 6);
    s += micro(396, 666, 'HULL') + dmg(396, 670, 26, 6, 5) + dmgText(396, 687);
    s += speedText(640, 684, 34) + label(700, 684, 'KM/H', 9, 'start') + speedBar(510, 694, 260, 2);
    s += micro(884, 666, '{{boostTxt}}', 'end', C.orange, 0.8) + orbs(830, 678, 5, 8);
    s += micro(352, 714, 'MFD-01', 'start', C.grey, 0.45, 8);
    s += ventOut(50, 646, 250, 16, 28, -1) + ventOut(980, 646, 250, 16, 28, 1);
    s += switches(100, 690, 10, 18, [0, 3]) + switches(1010, 690, 10, 18, [2, 7, 9]);
    s += screw(70, 630) + screw(1210, 630);
    s += `</g>`;
    return s;
  }
});

// M1 · BÚNKER: cristal dividido en tres por barras gruesas; gran dial central con aguja; boost con tapas de seguridad; daño en placas.
// Rejilla blindada con detalle: marco con bisel, hueco, lamas con luz y sombra, travesaño, tornillos y placa.
function ventArmor(x, y, w, h, n, tag) {
  let o = `<rect x="${x - 6}" y="${y - 6}" width="${w + 12}" height="${h + 12}" fill="url(#pConsole)" stroke="${C.cyan}" stroke-width="1.4"></rect>`;
  o += `<rect x="${x - 2}" y="${y - 2}" width="${w + 4}" height="${h + 4}" fill="none" stroke="${C.blue}" stroke-width="1" opacity="0.6"></rect>`;
  o += `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="#02030A"></rect>`;
  o += `<rect x="${x}" y="${y}" width="${w}" height="${Math.min(10, h / 3)}" fill="url(#bezelIn)"></rect>`;
  const step = h / n;
  for (let i = 0; i < n; i++) {
    const yy = y + step * (i + 0.5);
    o += `<rect x="${x + 2}" y="${(yy - step * 0.28).toFixed(1)}" width="${w - 4}" height="${(step * 0.42).toFixed(1)}" fill="#0D1424"></rect>`;
    o += line([[x + 2, yy - step * 0.28], [x + w - 2, yy - step * 0.28]], '#BFF6FF', 1, 0.22);
    o += line([[x + 2, yy + step * 0.16], [x + w - 2, yy + step * 0.16]], '#000', 1, 0.7);
  }
  o += `<rect x="${x + w / 2 - 3}" y="${y}" width="6" height="${h}" fill="url(#pPillar)" stroke="${C.blue}" stroke-width="1"></rect>`;
  o += screw(x - 1, y - 1) + screw(x + w + 1, y - 1) + screw(x - 1, y + h + 1) + screw(x + w + 1, y + h + 1);
  if (tag) {
    o += `<rect x="${x + 4}" y="${y + h + 8}" width="${tag.length * 6 + 10}" height="11" fill="#04070F" stroke="${C.blue}" stroke-width="0.8"></rect>` + micro(x + 9, y + h + 17, tag, 'start', C.grey, 0.6, 7);
    let st = '';
    for (let k = 0; k < 6; k++) st += `<polygon points="${pts([[x + w - 60 + k * 10, y + h + 19], [x + w - 54 + k * 10, y + h + 8], [x + w - 50 + k * 10, y + h + 8], [x + w - 56 + k * 10, y + h + 19]])}" fill="${C.orange}" opacity="0.55"></polygon>`;
    o += `<rect x="${x + w - 62}" y="${y + h + 8}" width="62" height="11" fill="#04070F"></rect>` + st;
  }
  return o;
}
// Desgaste en un pilar: remaches en los bordes, arañazos, manchas de suciedad, desconchones y una abolladura.
function weather(f, seed) {
  let r = seed;
  const rnd = () => { r = (r * 16807) % 2147483647; return (r - 1) / 2147483646; };
  const lerp = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];
  const P = (u, v) => lerp(lerp(f[0], f[3], u), lerp(f[1], f[2], u), v);
  let o = '';
  // manchas de suciedad (más abajo, donde se acumula)
  for (let i = 0; i < 5; i++) { const u = 0.35 + rnd() * 0.6, v = 0.2 + rnd() * 0.6, c = P(u, v); o += `<ellipse cx="${c[0].toFixed(1)}" cy="${c[1].toFixed(1)}" rx="${(4 + rnd() * 8).toFixed(1)}" ry="${(10 + rnd() * 24).toFixed(1)}" fill="#000" opacity="${(0.18 + rnd() * 0.2).toFixed(2)}"></ellipse>`; }
  // remaches en los dos bordes
  for (let u = 0.04; u < 0.98; u += 0.055) { [0.12, 0.88].forEach(v => { const c = P(u, v); o += `<circle cx="${c[0].toFixed(1)}" cy="${c[1].toFixed(1)}" r="2.2" fill="#121B30" stroke="${C.blue}" stroke-width="0.8"></circle><circle cx="${(c[0] - 0.7).toFixed(1)}" cy="${(c[1] - 0.7).toFixed(1)}" r="0.7" fill="#BFF6FF" opacity="0.4"></circle>`; }); }
  // arañazos finos
  for (let i = 0; i < 6; i++) {
    const u = 0.05 + rnd() * 0.9, v = 0.2 + rnd() * 0.6, a = P(u, v), len = 6 + rnd() * 22, ang = (-0.9 + rnd() * 1.8);
    const b = [a[0] + Math.cos(ang) * len * 0.5, a[1] + Math.sin(ang) * len];
    o += line([a, b], '#BFF6FF', 0.7, (0.22 + rnd() * 0.25).toFixed(2));
    if (rnd() > 0.6) o += line([[a[0] + 1.5, a[1] + 1], [b[0] + 1.5, b[1] + 1]], '#000', 0.6, 0.4);
  }
  // desconchones en el borde (óxido apagado)
  for (let i = 0; i < 6; i++) { const u = 0.08 + rnd() * 0.85, v = rnd() > 0.5 ? 0.03 : 0.97, c = P(u, v); o += `<polygon points="${pts([[c[0] - 2, c[1] - 3], [c[0] + 3, c[1] - 1], [c[0] + 1, c[1] + 4], [c[0] - 3, c[1] + 2]])}" fill="${C.orange}" opacity="${(0.18 + rnd() * 0.18).toFixed(2)}"></polygon>`; }
  // abolladura con su brillo
  const d = P(0.3 + rnd() * 0.4, 0.5);
  o += `<ellipse cx="${d[0].toFixed(1)}" cy="${d[1].toFixed(1)}" rx="7" ry="11" fill="#000" opacity="0.3"></ellipse><path d="M ${(d[0] - 6).toFixed(1)} ${(d[1] - 4).toFixed(1)} Q ${d[0].toFixed(1)} ${(d[1] - 12).toFixed(1)} ${(d[0] + 6).toFixed(1)} ${(d[1] - 4).toFixed(1)}" fill="none" stroke="#BFF6FF" stroke-width="0.8" opacity="0.3"></path>`;
  return o;
}
const bezM = (p0, p1, p2, n) => { const o = []; for (let i = 0; i <= n; i++) { const t = i / n, a = (1 - t) * (1 - t), b = 2 * (1 - t) * t, c = t * t; o.push([a * p0[0] + b * p1[0] + c * p2[0], a * p0[1] + b * p1[1] + c * p2[1]]); } return o; };
// Pilar de una sola pieza: el mismo polígono se abre en curva abajo y apoya en el salpicadero (sin corte).
function pillarOne(f, side, dashY, flare) {
  const lerp = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];
  const yTop = Math.max(f[2][1], f[3][1]) - 60;
  const TL = lerp(f[0], f[3], (yTop - f[0][1]) / (f[3][1] - f[0][1])), TR = lerp(f[1], f[2], (yTop - f[1][1]) / (f[2][1] - f[1][1]));
  const xl = Math.min(f[3][0], f[2][0]) - flare, xr = Math.max(f[3][0], f[2][0]) + flare;
  const BL = [xl, dashY(xl) + 8], BR = [xr, dashY(xr) + 8];
  const curveL = bezM(TL, [TL[0] + (f[3][0] - TL[0]) * 0.9, BL[1] - 8], BL, 14);
  const curveR = bezM(TR, [TR[0] + (f[2][0] - TR[0]) * 0.9, BR[1] - 8], BR, 14);
  const body = [f[0], ...curveL, ...curveR.slice().reverse(), f[1]];
  // cara lateral prolongada hasta el salpicadero
  const sd = side.map(p => p.slice());
  sd[2] = [sd[2][0], dashY(sd[2][0]) + 8]; sd[3] = [sd[3][0], dashY(sd[3][0]) + 8];
  let o = `<polygon points="${pts(sd)}" fill="url(#pSide)"></polygon>` + line([sd[0], sd[1], sd[2]], C.blue, 1, 0.7) + line([sd[0], sd[3]], C.blue, 1, 0.7);
  o += `<polygon points="${pts(body)}" fill="url(#pPillar)"></polygon>`;
  // contorno sin la base: así no queda ninguna línea de corte con el salpicadero
  o += line([f[1], f[0], ...curveL], C.cyan, 1.6, 1) + line([f[1], ...curveR], C.cyan, 1.6, 1);
  o += line([f[0], TL], '#BFF6FF', 1, 0.25);
  // sombra suave donde apoya
  o += `<polygon points="${pts([...curveL.slice(6), ...curveR.slice(6).reverse()])}" fill="url(#footShade)"></polygon>`;
  return o;
}
const MARTILLO_1 = ({
  id: 'Cab_Martillo_1', name: 'BÚNKER', top: 32,
  gauge: { cx: 640, cy: 672, r: 48, a0: 150, a1: 390 },
  defs: deepDefs+ `<linearGradient id="footShade" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#000" stop-opacity="0"></stop><stop offset="1" stop-color="#000" stop-opacity="0.35"></stop></linearGradient>`,
  svg: () => {
    let s = martilloNose(600, 548);
    s += panel([[0, 0], [1280, 0], [1280, 70], [1280, 90], [0, 90]], { fill: 'url(#pPillar)', bevel: 8, depth: 8 });
    s += ventArmor(510, 22, 260, 38, 7) + screw(30, 30) + screw(1250, 30) + screw(470, 50) + screw(810, 50);
    // barras gruesas
    const dash = [[0, 590], [230, 590], [420, 600], [860, 600], [1050, 590], [1280, 590], [1280, 720], [0, 720]];
    s += panel(dash, { bevel: 12, hi: [[0, 593], [230, 593], [420, 603], [860, 603], [1050, 593], [1280, 593]] });
    const dashY = x => { const d = [[0, 590], [230, 590], [420, 600], [860, 600], [1050, 590], [1280, 590]]; for (let i = 1; i < d.length; i++) if (x <= d[i][0]) { const t = (x - d[i - 1][0]) / (d[i][0] - d[i - 1][0]); return d[i - 1][1] + (d[i][1] - d[i - 1][1]) * t; } return 590; };
    s += pillarOne([[110, 90], [190, 90], [300, 580], [230, 590]], [[190, 90], [222, 90], [330, 574], [300, 580]], dashY, 20);
    s += pillarOne([[1090, 90], [1170, 90], [1050, 590], [980, 580]], [[1058, 90], [1090, 90], [980, 580], [950, 574]], dashY, 20);
    s += pillarOne([[452, 90], [480, 90], [436, 598], [414, 598]], [[480, 90], [492, 90], [446, 598], [436, 598]], dashY, 12);
    s += pillarOne([[800, 90], [828, 90], [866, 598], [844, 598]], [[788, 90], [800, 90], [844, 598], [834, 598]], dashY, 12);
    s += weather([[110, 90], [190, 90], [300, 580], [230, 590]], 11) + weather([[1090, 90], [1170, 90], [1050, 590], [980, 580]], 23);
    s += weather([[452, 90], [480, 90], [436, 598], [414, 598]], 37) + weather([[800, 90], [828, 90], [866, 598], [844, 598]], 51);
    for (let x = 30; x < 1280; x += 80) s += screw(x, 708);
    // dial central
    s += `<circle cx="640" cy="672" r="110" fill="url(#scrGlow)" opacity="{{scrGlowOp}}"></circle>`;
    s += `<circle cx="640" cy="672" r="104" fill="#02030A" stroke="${C.cyan}" stroke-width="1.4"></circle><circle cx="640" cy="672" r="104" fill="url(#bezelIn)" opacity="0.9"></circle>`;
    s += `<path d="M ${640 - 100} 672 A 100 100 0 0 1 ${640 + 100} 672" fill="none" stroke="#BFF6FF" stroke-width="1" opacity="0.25"></path>`;
    s += `<circle cx="640" cy="680" r="92" fill="#070B15" stroke="${C.blue}" stroke-width="1"></circle>`;
    s += `<circle cx="640" cy="672" r="92" fill="url(#pConsole)" stroke="${C.cyan}" stroke-width="2"></circle>`;
    s += `<circle cx="640" cy="672" r="80" fill="${SCREEN}" stroke="${C.blue}" stroke-width="1"></circle>`;
    s += `<path d="M ${640 - 80} 672 A 80 80 0 0 1 ${640 + 80} 672" fill="none" stroke="#000" stroke-width="10" opacity="0.45"></path>`;
    for (let k = 0; k < 40; k++) { const a = (150 + 240 * k / 40) * Math.PI / 180; s += line([[640 + Math.cos(a) * 74, 672 + Math.sin(a) * 74], [640 + Math.cos(a) * 77, 672 + Math.sin(a) * 77]], C.blue, 1, 0.5); }
    for (let k = 0; k < 8; k++) { const a = k / 8 * Math.PI * 2; s += screw((640 + Math.cos(a) * 86).toFixed(1), (672 + Math.sin(a) * 86).toFixed(1)); }
    s += `<path d="{{arcBg}}" fill="none" stroke="${C.blue}" stroke-width="6" opacity="0.35"></path><path d="{{arcRed}}" fill="none" stroke="${C.orange}" stroke-width="6" opacity="0.35"></path>`;
    s += `<path d="{{arcFg}}" fill="none" stroke="{{spdColor}}" stroke-width="6" filter="url(#glowSoft)"></path><path d="{{arcTicks}}" fill="none" stroke="${C.grey}" stroke-width="1" opacity="0.6"></path>`;
    s += `<g font-family="Share Tech Mono, monospace" font-size="8" fill="${C.grey}" opacity="0.7" text-anchor="middle">{{arcNums}}</g>`;
    s += `<path d="{{needle}}" stroke="#DFFBFF" stroke-width="2.4"></path><circle cx="640" cy="672" r="6" fill="#0B1222" stroke="${C.cyan}" stroke-width="1"></circle>`;
    s += speedText(640, 706, 22) + micro(640, 718, 'KM/H', 'middle', C.grey, 0.7, 8);
    // boost: tres luces con tapa de seguridad
    s += sinkH([[790, 628], [990, 628], [1000, 708], [780, 708]], 14);
    s += panel([[790, 628], [990, 628], [1000, 708], [780, 708]], { fill: 'url(#pConsole)', depth: 10, bevel: 6 });
    s += `<polygon points="${pts([[790, 628], [990, 628], [991, 642], [789, 642]])}" fill="url(#shadowDown)" opacity="0.8"></polygon>`;
    s += screw(796, 634) + screw(984, 634) + screw(788, 700) + screw(992, 700) + micro(890, 704, 'ARM · FIRE', 'middle', C.grey, 0.45, 7);
    s += micro(804, 646, 'BOOST') + micro(986, 646, '{{boostTxt}}', 'end', C.orange, 0.8);
    [830, 890, 950].forEach(x => { s += `<rect x="${x - 22}" y="656" width="44" height="40" fill="#04070F" stroke="${C.blue}" stroke-width="1"></rect><rect x="${x - 22}" y="656" width="44" height="8" fill="url(#bezelIn)"></rect>` + line([[x - 22, 656], [x - 14, 648], [x + 14, 648], [x + 22, 656]], C.orange, 1, 0.5) + `<rect x="${x - 16}" y="645" width="8" height="4" fill="#0B1222" stroke="${C.blue}" stroke-width="0.8"></rect><rect x="${x + 8}" y="645" width="8" height="4" fill="#0B1222" stroke="${C.blue}" stroke-width="0.8"></rect>`; });
    s += orbs(830, 676, 11, 38);
    // daño: placas
    s += sinkH([[290, 628], [490, 628], [500, 708], [280, 708]], 14);
    s += panel([[290, 628], [490, 628], [500, 708], [280, 708]], { fill: 'url(#pConsole)', depth: 10, bevel: 6 });
    s += `<polygon points="${pts([[290, 628], [490, 628], [491, 642], [289, 642]])}" fill="url(#shadowDown)" opacity="0.8"></polygon>`;
    s += screw(296, 634) + screw(484, 634) + screw(288, 700) + screw(492, 700) + micro(390, 704, 'HULL PLATING', 'middle', C.grey, 0.45, 7);
    s += micro(304, 646, 'HULL') + dmgText(486, 646, 'end') + plates(306, 656, 52, 40, 10);
    s += ventArmor(46, 622, 188, 58, 8, 'INTAKE L') + ventArmor(1046, 622, 188, 58, 8, 'INTAKE R');
    return s;
  }
});


const extraDefs = `<linearGradient id="deck" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1A2744"></stop><stop offset="1" stop-color="#0A1020"></stop></linearGradient>`;
HOJA_1.defs = (HOJA_1.defs || '') + extraDefs;
MARTILLO_1.defs = (MARTILLO_1.defs || '') + extraDefs;
fs.writeFileSync(path.join(OUT, HOJA_1.id + '.dc.html'), board(HOJA_1, 0, 'Hoja'));
fs.writeFileSync(path.join(OUT, MARTILLO_1.id + '.dc.html'), board(MARTILLO_1, 0, 'Martillo'));
console.log('ok', HOJA_1.id, MARTILLO_1.id);
