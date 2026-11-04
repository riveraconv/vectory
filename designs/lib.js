// Genera las cabinas de Vector (5 opciones, v2 "sofisticada") para el lienzo "VECTORY — Naves", página Cabinas.
// Pantalla de juego 1280×720. Paneles opacos con volumen (caras, biseles, sombreado por planos),
// detalle mecánico decorativo, HUD integrado más rico y luz (pantallas, reflejos, tinte de boost y de daño).
const fs = require('fs');
const path = require('path');
const OUT = path.join(__dirname, 'project');

const C = { cyan: '#36E7FF', blue: '#245BFF', orange: '#FF9D32', bg: '#05070D', grey: '#A6B8D8', green: '#3DFF8A', yellow: '#FFE04A', red: '#FF4D5E' };
const SCREEN = '#02040A';

const pts = a => a.map(p => p[0].toFixed(1) + ',' + p[1].toFixed(1)).join(' ');
const centroid = a => [a.reduce((s, p) => s + p[0], 0) / a.length, a.reduce((s, p) => s + p[1], 0) / a.length];
const inset = (a, d) => { const c = centroid(a); return a.map(p => { const dx = c[0] - p[0], dy = c[1] - p[1], n = Math.hypot(dx, dy) || 1; return [p[0] + dx / n * d, p[1] + dy / n * d]; }); };
const off = (a, dx, dy) => a.map(p => [p[0] + dx, p[1] + dy]);
const line = (a, col, w, op, extra) => `<polyline points="${pts(a)}" fill="none" stroke="${col}" stroke-width="${w}" opacity="${op}" stroke-linejoin="round" stroke-linecap="round"${extra || ''}></polyline>`;

// Panel con volumen: cara con degradado, bisel interior, arista luminosa y brillo en el borde superior.
// depth > 0 añade una cara frontal más oscura por debajo (grosor).
function panel(a, opts) {
  opts = opts || {};
  const fill = opts.fill || 'url(#pTop)';
  let s = '';
  if (opts.depth) {
    const lo = off(a, 0, opts.depth);
    s += `<polygon points="${pts(lo)}" fill="url(#pFront)" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.6"></polygon>`;
  }
  s += `<polygon points="${pts(a)}" fill="${fill}" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>`;
  if (opts.bevel !== false) s += `<polygon points="${pts(inset(a, opts.bevel || 7))}" fill="none" stroke="${C.blue}" stroke-width="1" opacity="0.5" stroke-linejoin="round"></polygon>`;
  if (opts.hi) s += line(opts.hi, '#BFF6FF', 1, 0.35);
  return s;
}
// Pilar con grosor: cara frontal + cara lateral más oscura.
function pillar(front, side) {
  return `<polygon points="${pts(side)}" fill="url(#pSide)" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.7"></polygon>` +
    `<polygon points="${pts(front)}" fill="url(#pPillar)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>` +
    line([front[0], front[3]], '#BFF6FF', 1, 0.25);
}
// Detalle mecánico
const screw = (x, y) => `<circle cx="${x}" cy="${y}" r="3" fill="#0B1222" stroke="${C.blue}" stroke-width="1" opacity="0.9"></circle><line x1="${x - 2}" y1="${y}" x2="${x + 2}" y2="${y}" stroke="${C.blue}" stroke-width="0.8"></line>`;
function vent(x, y, w, h, n, slant) {
  let d = '';
  for (let i = 0; i < n; i++) { const xx = x + (i + 0.5) * w / n; d += `M${(xx - (slant || 0)).toFixed(1)} ${y + h}L${(xx + (slant || 0)).toFixed(1)} ${y}`; }
  return `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="#04070F" stroke="${C.blue}" stroke-width="1" opacity="0.9"></rect><path d="${d}" stroke="${C.blue}" stroke-width="1.2" opacity="0.55"></path>`;
}
function switches(x, y, n, gap, lit) {
  let s = '';
  for (let i = 0; i < n; i++) {
    const xx = x + i * gap;
    s += `<rect x="${xx}" y="${y}" width="10" height="14" rx="1.5" fill="#0B1222" stroke="${C.blue}" stroke-width="1"></rect><rect x="${xx + 3}" y="${y + 3}" width="4" height="5" fill="${C.grey}" opacity="0.45"></rect>`;
    s += `<circle cx="${xx + 5}" cy="${y - 5}" r="1.6" fill="${(lit || []).includes(i) ? C.cyan : C.blue}" opacity="${(lit || []).includes(i) ? 0.9 : 0.5}"></circle>`;
  }
  return s;
}
const seam = (a) => line(a, C.blue, 1, 0.4, ' stroke-dasharray="6 5"');
// Marco de pantalla con esquinas
function brackets(x, y, w, h, l) {
  l = l || 10;
  return line([[x, y + l], [x, y], [x + l, y]], C.cyan, 1.2, 0.8) + line([[x + w - l, y], [x + w, y], [x + w, y + l]], C.cyan, 1.2, 0.8) +
    line([[x, y + h - l], [x, y + h], [x + l, y + h]], C.cyan, 1.2, 0.8) + line([[x + w - l, y + h], [x + w, y + h], [x + w, y + h - l]], C.cyan, 1.2, 0.8);
}
// Pantalla: fondo, líneas de barrido, halo que ilumina el panel de alrededor
function screen(poly, glow) {
  const c = centroid(poly);
  let s = '';
  if (glow) s += `<ellipse cx="${c[0].toFixed(1)}" cy="${c[1].toFixed(1)}" rx="${glow[0]}" ry="${glow[1]}" fill="url(#scrGlow)" opacity="{{scrGlowOp}}"></ellipse>`;
  s += `<polygon points="${pts(poly)}" fill="${SCREEN}" stroke="${C.blue}" stroke-width="1"></polygon>`;
  s += `<polygon points="${pts(poly)}" fill="url(#scan)" opacity="0.5"></polygon>`;
  return s;
}
const micro = (x, y, txt, anchor, col, op, size) => `<text x="${x}" y="${y}" text-anchor="${anchor || 'start'}" font-family="Share Tech Mono, monospace" font-size="${size || 9}" letter-spacing="1.5" fill="${col || C.grey}" opacity="${op ?? 0.6}">${txt}</text>`;
const label = (x, y, txt, size, anchor, col, op) => `<text x="${x}" y="${y}" text-anchor="${anchor || 'middle'}" font-family="Share Tech Mono, monospace" font-size="${size || 11}" letter-spacing="2" fill="${col || C.grey}" opacity="${op ?? 0.85}">${txt}</text>`;
const speedText = (x, y, size, anchor, font) => `<text x="${x}" y="${y}" text-anchor="${anchor || 'middle'}" font-family="${font || 'Share Tech Mono, monospace'}" font-size="${size}" fill="{{spdColor}}" filter="url(#glowSoft)" style="transition: fill 0.25s;">{{spd}}</text>`;

// ---------- HUD dinámico ----------
const T = ' style="transition: fill-opacity 0.25s, stroke-opacity 0.25s, fill 0.25s;"';
function orbs(x, y, r, gap, dir) {
  let s = '';
  for (let i = 0; i < 3; i++) {
    const cx = dir === 'v' ? x : x + i * (2 * r + gap), cy = dir === 'v' ? y - i * (2 * r + gap) : y;
    s += `<circle cx="${cx}" cy="${cy}" r="${r + 3}" fill="none" stroke="${C.blue}" stroke-width="1" opacity="0.5"></circle>`;
    s += `<circle cx="${cx}" cy="${cy}" r="${r}" fill="{{o${i}.fill}}" fill-opacity="{{o${i}.fo}}" stroke="{{o${i}.stroke}}" stroke-opacity="{{o${i}.so}}" stroke-width="1.2" filter="{{o${i}.filter}}"${T}></circle>`;
  }
  return s;
}
function diamonds(x, y, r, gap) {
  let s = '';
  for (let i = 0; i < 3; i++) {
    const cx = x + i * (2 * r + gap);
    s += `<polygon points="${cx},${y - r} ${cx + r},${y} ${cx},${y + r} ${cx - r},${y}" fill="{{o${i}.fill}}" fill-opacity="{{o${i}.fo}}" stroke="{{o${i}.stroke}}" stroke-opacity="{{o${i}.so}}" stroke-width="1.2" filter="{{o${i}.filter}}"${T}></polygon>`;
  }
  return s;
}
function boostBars(x, y, w, h, gap) {
  let s = '';
  for (let i = 0; i < 3; i++) s += `<polygon points="${pts([[x + i * (w + gap) + 4, y], [x + i * (w + gap) + w, y], [x + i * (w + gap) + w - 4, y + h], [x + i * (w + gap), y + h]])}" fill="{{o${i}.fill}}" fill-opacity="{{o${i}.fo}}" stroke="{{o${i}.stroke}}" stroke-opacity="{{o${i}.so}}" stroke-width="1.2" filter="{{o${i}.filter}}"${T}></polygon>`;
  return s;
}
function dmg(x, y, w, h, gap, dir) {
  let s = '';
  for (let i = 0; i < 3; i++) {
    const bx = dir === 'v' ? x : x + i * (w + gap), by = dir === 'v' ? y - i * (h + gap) : y;
    s += `<rect class="{{d${i}.cls}}" x="${bx}" y="${by}" width="${w}" height="${h}" fill="{{d${i}.fill}}" fill-opacity="{{d${i}.fo}}" stroke="{{d${i}.stroke}}" stroke-opacity="{{d${i}.so}}" stroke-width="1" filter="{{d${i}.filter}}"${T}></rect>`;
  }
  return s;
}
const dmgText = (x, y, anchor) => `<text x="${x}" y="${y}" text-anchor="${anchor || 'start'}" font-family="Share Tech Mono, monospace" font-size="10" letter-spacing="2" fill="{{dmgCol}}" opacity="0.9">{{dmgTxt}}</text>`;
// Barra de velocidad con marcas
function speedBar(x, y, w, h) {
  let tk = '';
  for (let i = 0; i <= 10; i++) { const xx = x + w * i / 10; tk += `M${xx.toFixed(1)} ${y + h + 2}L${xx.toFixed(1)} ${y + h + (i % 5 === 0 ? 7 : 4)}`; }
  const topX = x + w * 396 / 496;
  return `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${C.blue}" opacity="0.3"></rect>` +
    `<rect x="${x}" y="${y}" width="{{bar${w}}}" height="${h}" fill="{{spdColor}}"${T}></rect>` +
    `<path d="${tk}" stroke="${C.grey}" stroke-width="1" opacity="0.6"></path>` +
    `<line x1="${topX.toFixed(1)}" y1="${y - 3}" x2="${topX.toFixed(1)}" y2="${y + h + 3}" stroke="${C.orange}" stroke-width="1" opacity="0.7"></line>`;
}

// ---------- escena común ----------
function scene() {
  let s = `<rect x="0" y="0" width="1280" height="720" fill="url(#sky)"></rect>`;
  const towers = [[40, 210, 60], [120, 170, 44], [180, 235, 70], [270, 150, 38], [330, 215, 56], [420, 190, 40], [470, 240, 52], [850, 230, 48], [905, 160, 40], [960, 205, 66], [1040, 140, 36], [1090, 220, 58], [1160, 180, 46], [1220, 230, 60]];
  let tw = '', win = '';
  towers.forEach(t => {
    tw += `M${t[0]} 300L${t[0]} ${t[1]}L${t[0] + t[2]} ${t[1]}L${t[0] + t[2]} 300`;
    for (let yy = t[1] + 12; yy < 296; yy += 14) win += `M${t[0] + 6} ${yy}L${t[0] + t[2] - 6} ${yy}`;
  });
  s += `<path d="${tw}" fill="none" stroke="${C.orange}" stroke-width="1" opacity="0.2"></path><path d="${win}" fill="none" stroke="${C.orange}" stroke-width="0.6" opacity="0.07"></path>`;
  s += `<polygon points="640,300 1540,720 -260,720" fill="#060A16"></polygon>`;
  let g = '';
  g += `<line x1="640" y1="300" x2="-260" y2="720" stroke="#5A82FF" stroke-width="2"></line><line x1="640" y1="300" x2="1540" y2="720" stroke="#5A82FF" stroke-width="2"></line>`;
  [213, 640, 1067].forEach(x => { g += `<line x1="640" y1="300" x2="${x}" y2="720" stroke="${C.blue}" stroke-width="1"></line>`; });
  [571, 500, 458.5, 431.2, 412, 397.7, 386.6, 377.8, 370.6, 364.6, 359.6, 355.3, 351.5].forEach(y => {
    const t = (y - 300) / 420, hw = 900 * t;
    g += `<line x1="${(640 - hw).toFixed(1)}" y1="${y}" x2="${(640 + hw).toFixed(1)}" y2="${y}" stroke="${C.blue}" stroke-width="1" opacity="0.55"></line>`;
  });
  [-1, 1].forEach(k => {
    const bx = x => 640 + k * x;
    g += `<line x1="${bx(14)}" y1="300" x2="${bx(1040)}" y2="720" stroke="${C.cyan}" stroke-width="1.6"></line>`;
    g += `<line x1="${bx(14)}" y1="294" x2="${bx(1040)}" y2="560" stroke="${C.cyan}" stroke-width="1.6"></line>`;
    g += `<line x1="${bx(14)}" y1="297" x2="${bx(1040)}" y2="640" stroke="${C.blue}" stroke-width="1" opacity="0.5"></line>`;
    [0.08, 0.16, 0.27, 0.42, 0.62, 0.9].forEach(t => {
      const x = 14 + 1026 * t;
      g += `<line x1="${bx(x).toFixed(1)}" y1="${(300 + 420 * t).toFixed(1)}" x2="${bx(x).toFixed(1)}" y2="${(294 + 266 * t).toFixed(1)}" stroke="${C.blue}" stroke-width="1" opacity="0.4"></line>`;
    });
  });
  s += `<g filter="url(#glow)">${g}</g>`;
  return s;
}
// Reflejos tenues en el cristal
const glass = () => `<g opacity="0.07" stroke="#DFFBFF" stroke-width="2" fill="none"><line x1="380" y1="120" x2="520" y2="40"></line><line x1="410" y1="140" x2="580" y2="44"></line><line x1="900" y1="260" x2="1000" y2="200"></line></g>`;
// Tintes de luz: boost (naranja) y daño fuerte (rojo) desde los bordes
const tints = () => `<rect x="0" y="0" width="1280" height="720" fill="url(#tintOrange)" opacity="{{boostTint}}" style="transition: opacity 0.3s;"></rect><rect class="{{dmgTintCls}}" x="0" y="0" width="1280" height="720" fill="url(#tintRed)" opacity="{{dmgTint}}"></rect>`;

// Morro de Vector visto desde dentro: rombo con dos caras sombreadas y arista central.
function vectorNose(y0, tipY) {
  return `<polygon points="${pts([[468, y0], [640, tipY], [640, y0]])}" fill="url(#noseL)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>` +
    `<polygon points="${pts([[812, y0], [640, tipY], [640, y0]])}" fill="url(#noseR)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>` +
    line([[640, tipY], [640, y0]], '#BFF6FF', 1.2, 0.7) +
    line([[560, y0 - 4], [640, tipY + 24], [720, y0 - 4]], C.blue, 1, 0.5) +
    `<circle cx="640" cy="${tipY + 10}" r="2" fill="${C.cyan}" opacity="0.8"></circle>`;
}
// ---------- lógica común ----------
function script(gauge, extra) {
  return `
class Component extends DCLogic {
  constructor(props) { super(props); this.t = 0; }
  componentDidMount() {
    var self = this, last = null;
    var tick = function (ts) {
      var dt = last === null ? 0 : Math.min((ts - last) / 1000, 0.05); last = ts;
      if (self.props.demo ?? true) { self.t += dt; self.forceUpdate(); }
      self.raf = window.requestAnimationFrame(tick);
    };
    this.raf = window.requestAnimationFrame(tick);
  }
  componentWillUnmount() { if (this.raf) window.cancelAnimationFrame(this.raf); }
  arc(cx, cy, r, a0, a1) {
    var p = function (a) { var t = a * Math.PI / 180; return [cx + Math.cos(t) * r, cy + Math.sin(t) * r]; };
    var s = p(a0), e = p(a1), big = Math.abs(a1 - a0) > 180 ? 1 : 0;
    return 'M' + s[0].toFixed(1) + ' ' + s[1].toFixed(1) + 'A' + r + ' ' + r + ' 0 ' + big + ' 1 ' + e[0].toFixed(1) + ' ' + e[1].toFixed(1);
  }
  renderVals() {
    var demo = this.props.demo ?? true;
    var TOP = 396, MAX = 496;
    var speed, charges, cooldown, boost, dmgLvl;
    if (demo) {
      var c = this.t % 16;
      if (c < 6) speed = TOP * (1 - Math.pow(1 - c / 6, 2));
      else if (c < 8) speed = TOP + 100 * Math.min(1, (c - 6) / 0.5);
      else if (c < 11) speed = TOP + 100 * (1 - (c - 8) / 3);
      else if (c < 13) speed = TOP - 200 * ((c - 11) / 2);
      else speed = 196 + 200 * ((c - 13) / 3);
      boost = c >= 6 && c < 8;
      charges = c < 6 ? 3 : 2;
      cooldown = c >= 6 && c < 13;
      dmgLvl = Math.floor(this.t / 16) % 4;
    } else {
      speed = this.props.speed ?? 312;
      charges = this.props.charges ?? 2;
      cooldown = this.props.cooldown ?? false;
      boost = this.props.boost ?? false;
      dmgLvl = this.props.damage ?? 1;
    }
    var over = speed > TOP + 1 || boost;
    var spdColor = over ? '${C.orange}' : '${C.cyan}';
    var o = [];
    for (var i = 0; i < 3; i++) {
      var has = i < charges;
      if (has && dmgLvl === 3) o.push({ fill: '${C.red}', fo: 0.3, stroke: '${C.red}', so: 0.55, filter: 'none' });
      else if (has && cooldown) o.push({ fill: '${C.orange}', fo: 0.3, stroke: '${C.orange}', so: 0.55, filter: 'none' });
      else if (has) o.push({ fill: '${C.orange}', fo: 1, stroke: '${C.orange}', so: 1, filter: 'url(#glowSoft)' });
      else o.push({ fill: '${C.blue}', fo: 0.25, stroke: '${C.blue}', so: 0.7, filter: 'none' });
    }
    var col = dmgLvl === 0 ? '${C.green}' : (dmgLvl === 1 ? '${C.yellow}' : '${C.red}');
    var d = [];
    for (var j = 0; j < 3; j++) {
      var on = j < dmgLvl;
      d.push({ fill: col, fo: on ? 1 : 0, stroke: col, so: on ? 1 : 0.6, filter: on ? 'url(#glowSoft)' : 'none', cls: dmgLvl === 3 ? 'blink' : '' });
    }
    var f = Math.min(1, speed / MAX);
    var out = {
      spd: String(Math.round(speed)), spdColor: spdColor,
      o0: o[0], o1: o[1], o2: o[2], d0: d[0], d1: d[1], d2: d[2],
      dmgCol: col, dmgTxt: ['NOMINAL', 'LIGHT', 'HEAVY', 'CRITICAL'][dmgLvl],
      boostTxt: dmgLvl === 3 && charges > 0 ? 'LOCKED' : (boost ? 'ACTIVE' : (cooldown && charges > 0 ? 'COOLDOWN' : (charges > 0 ? 'READY' : 'CHARGING'))),
      gear: over ? 'OVR' : (speed > TOP * 0.85 ? 'MAX' : 'RUN'),
      scrGlowOp: (0.35 + 0.35 * f).toFixed(2),
      boostTint: boost ? 0.55 : (over ? 0.25 : 0),
      dmgTint: dmgLvl === 3 ? 0.45 : (dmgLvl === 2 ? 0.15 : 0),
      dmgTintCls: dmgLvl === 3 ? 'pulse' : '',
      bar156: (156 * f).toFixed(1), bar176: (176 * f).toFixed(1),
      visorMark: 'M552 ' + (542 - 72 * f).toFixed(1) + 'L570 ' + (542 - 72 * f).toFixed(1)
    };
    ${gauge ? `var g = ${JSON.stringify(gauge)};
    out.arcBg = this.arc(g.cx, g.cy, g.r, g.a0, g.a1);
    out.arcRed = this.arc(g.cx, g.cy, g.r, g.a0 + (g.a1 - g.a0) * TOP / MAX, g.a1);
    out.arcFg = this.arc(g.cx, g.cy, g.r, g.a0, g.a0 + Math.max(0.5, (g.a1 - g.a0) * f));
    var tk = '', nums = '';
    for (var k = 0; k <= 10; k++) {
      var a = (g.a0 + (g.a1 - g.a0) * k / 10) * Math.PI / 180, l = k % 2 === 0 ? 18 : 13;
      tk += 'M' + (g.cx + Math.cos(a) * (g.r + 9)).toFixed(1) + ' ' + (g.cy + Math.sin(a) * (g.r + 9)).toFixed(1) + 'L' + (g.cx + Math.cos(a) * (g.r + l)).toFixed(1) + ' ' + (g.cy + Math.sin(a) * (g.r + l)).toFixed(1);
    }
    out.arcTicks = tk;
    var na = [0, 100, 200, 300, 400];
    out.arcNums = '';
    var an = (g.a0 + (g.a1 - g.a0) * f) * Math.PI / 180;
    out.needle = 'M' + (g.cx + Math.cos(an) * (g.r - 14)).toFixed(1) + ' ' + (g.cy + Math.sin(an) * (g.r - 14)).toFixed(1) + 'L' + (g.cx + Math.cos(an) * (g.r + 6)).toFixed(1) + ' ' + (g.cy + Math.sin(an) * (g.r + 6)).toFixed(1);` : `out.arcBg = 'M0 0'; out.arcRed = 'M0 0'; out.arcFg = 'M0 0'; out.arcTicks = 'M0 0'; out.needle = 'M0 0';`}
    out.ringFg = this.arc(640, 684, 43, -90, -90 + Math.max(0.5, 359 * f));
    var rt = '';
    for (var q = 0; q < 24; q++) { var aa = q / 24 * Math.PI * 2; rt += 'M' + (640 + Math.cos(aa) * 32).toFixed(1) + ' ' + (684 + Math.sin(aa) * 32).toFixed(1) + 'L' + (640 + Math.cos(aa) * (q % 6 === 0 ? 27 : 30)).toFixed(1) + ' ' + (684 + Math.sin(aa) * (q % 6 === 0 ? 27 : 30)).toFixed(1); }
    out.ringTicks = rt;
    ${extra || ''}
    return out;
  }
}`;
}

// números fijos del arco (estáticos, se pintan en el SVG)
function arcNumbers(g) {
  let s = '';
  [0, 100, 200, 300, 400].forEach(v => {
    const a = (g.a0 + (g.a1 - g.a0) * v / 496) * Math.PI / 180;
    s += `<text x="${(g.cx + Math.cos(a) * (g.r + 30)).toFixed(1)}" y="${(g.cy + Math.sin(a) * (g.r + 30) + 3).toFixed(1)}">${v}</text>`;
  });
  return s;
}

function board(cp, idx, ship) {
  ship = ship || 'Vector';
  const props = {
    demo: { editor: 'boolean', default: true, section: 'HUD' },
    speed: { editor: 'range', min: 0, max: 520, step: 1, unit: 'km/h', default: 312, section: 'HUD' },
    boost: { editor: 'boolean', default: false, section: 'HUD' },
    charges: { editor: 'int', min: 0, max: 3, default: 2, section: 'HUD' },
    cooldown: { editor: 'boolean', default: false, section: 'HUD' },
    damage: { editor: 'int', min: 0, max: 3, default: 1, section: 'HUD' },
    $preview: { width: 1280, height: 720 }
  };
  let svg = cp.svg();
  if (cp.gauge) svg = svg.replace('{{arcNums}}', arcNumbers(cp.gauge));
  return `<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>${ship} · cabina ${idx + 1} · ${cp.name}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Orbitron:wght@500;700;900&amp;family=Share+Tech+Mono&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:#05070D}
@keyframes dmgblink{0%{opacity:1}50%{opacity:0.25}}
.blink{animation:dmgblink 0.5s steps(1) infinite}
@keyframes tintpulse{0%,100%{opacity:0.45}50%{opacity:0.2}}
.pulse{animation:tintpulse 1s ease-in-out infinite}
</style>
</helmet>
<div style="position: relative; width: 1280px; height: 720px; overflow: hidden; background: #05070D;">
<svg role="img" aria-label="Cabina ${cp.name} de ${ship} en carrera" width="1280" height="720" viewBox="0 0 1280 720" style="position: absolute; left: 0; top: 0;">
<defs>
${cp.defs || ''}
<linearGradient id="sky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#05070D"></stop><stop offset="0.3" stop-color="#140D0A"></stop><stop offset="0.42" stop-color="#2A1A0E"></stop><stop offset="0.45" stop-color="#05070D"></stop><stop offset="1" stop-color="#05070D"></stop></linearGradient>
<linearGradient id="pTop" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#16213A"></stop><stop offset="0.25" stop-color="#0C1324"></stop><stop offset="1" stop-color="#060A14"></stop></linearGradient>
<linearGradient id="pConsole" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1A2744"></stop><stop offset="1" stop-color="#0A1020"></stop></linearGradient>
<linearGradient id="pFront" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#070B16"></stop><stop offset="1" stop-color="#03050A"></stop></linearGradient>
<linearGradient id="pPillar" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#121B30"></stop><stop offset="1" stop-color="#0A1020"></stop></linearGradient>
<linearGradient id="pSide" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#060A14"></stop><stop offset="1" stop-color="#03050A"></stop></linearGradient>
<linearGradient id="noseL" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#0A1020"></stop><stop offset="1" stop-color="#16213A"></stop></linearGradient>
<linearGradient id="noseR" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#101A30"></stop><stop offset="1" stop-color="#070B16"></stop></linearGradient>
<linearGradient id="visor" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#36E7FF" stop-opacity="0.10"></stop><stop offset="1" stop-color="#36E7FF" stop-opacity="0.03"></stop></linearGradient>
<radialGradient id="scrGlow"><stop offset="0" stop-color="#36E7FF" stop-opacity="0.22"></stop><stop offset="1" stop-color="#36E7FF" stop-opacity="0"></stop></radialGradient>
<radialGradient id="tintOrange" cx="0.5" cy="0.55" r="0.75"><stop offset="0.55" stop-color="#FF9D32" stop-opacity="0"></stop><stop offset="1" stop-color="#FF9D32" stop-opacity="0.35"></stop></radialGradient>
<radialGradient id="tintRed" cx="0.5" cy="0.55" r="0.75"><stop offset="0.5" stop-color="#FF4D5E" stop-opacity="0"></stop><stop offset="1" stop-color="#FF4D5E" stop-opacity="0.4"></stop></radialGradient>
<pattern id="scan" width="4" height="3" patternUnits="userSpaceOnUse"><rect width="4" height="1" fill="#36E7FF" opacity="0.08"></rect></pattern>
<filter id="glow" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="2" result="b"></feGaussianBlur><feMerge><feMergeNode in="b"></feMergeNode><feMergeNode in="SourceGraphic"></feMergeNode></feMerge></filter>
<filter id="glowSoft" x="-40%" y="-40%" width="180%" height="180%"><feGaussianBlur stdDeviation="2.4" result="b"></feGaussianBlur><feMerge><feMergeNode in="b"></feMergeNode><feMergeNode in="SourceGraphic"></feMergeNode></feMerge></filter>
</defs>
${scene()}
${glass()}
<g>${svg}</g>
${tints()}
</svg>
<div style="position: absolute; left: 40px; top: ${cp.top}px; font-family: 'Share Tech Mono', monospace; font-size: 26px; letter-spacing: 0.08em; color: #36E7FF;">1 / 8</div>
<div style="position: absolute; right: 40px; top: ${cp.top}px; display: flex; flex-direction: column; align-items: flex-end; gap: 4px; font-family: 'Share Tech Mono', monospace;">
<span style="font-size: 26px; letter-spacing: 0.08em; color: #36E7FF;">LAP 2/3</span>
<span style="font-size: 20px; letter-spacing: 0.08em; color: #36E7FF;">0:44.218</span>
<span style="font-size: 16px; letter-spacing: 0.08em; color: #A6B8D8;">BEST 1:13.902</span>
</div>
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='${JSON.stringify(props)}'>${script(cp.gauge, cp.extra)}
</script>
</body>
</html>
`;
}

module.exports = { C, SCREEN, pts, centroid, inset, off, line, panel, pillar, screw, vent, switches, seam, brackets, screen, micro, label, speedText, T, orbs, diamonds, boostBars, dmg, dmgText, speedBar, scene, glass, tints, board, OUT };
