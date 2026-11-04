// VECTORY — Cabina definitiva de VECTOR: 3 · DOBLE PANTALLA.
// Pantalla de juego 1280×720 (px). Usa las piezas comunes de cabinas-lib.js (en el repo: lib.js).
// Ejecutar con: node cabina-vector.js  → escribe project/Cab_Vector_3.dc.html
const fs = require('fs');
const path = require('path');
const L = require('./lib.js');
const { C, SCREEN, pts, inset, off, line, panel, screw, vent, switches, seam, brackets, screen, micro, label, speedText, orbs, dmg, dmgText, speedBar, board, OUT } = L;

// 3 · DOBLE PANTALLA (v3: cabina envolvente, laterales que se curvan hacia dentro, morro de punta ancha)
const bez = (p0, p1, p2, n) => { const o = []; for (let i = 0; i <= n; i++) { const t = i / n, a = (1 - t) * (1 - t), b = 2 * (1 - t) * t, c = t * t; o.push([a * p0[0] + b * p1[0] + c * p2[0], a * p0[1] + b * p1[1] + c * p2[1]]); } return o; };
const mirrorX = a => a.map(p => [1280 - p[0], p[1]]);
function wideNose(y0, tipY, baseHW, tipHW) {
  const L = [640 - baseHW, y0], TL = [640 - tipHW, tipY], TR = [640 + tipHW, tipY], R = [640 + baseHW, y0];
  return `<polygon points="${pts([L, TL, [640, tipY], [640, y0]])}" fill="url(#noseL)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>` +
    `<polygon points="${pts([R, TR, [640, tipY], [640, y0]])}" fill="url(#noseR)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>` +
    `<polygon points="${pts([TL, TR, [640 + tipHW + 6, tipY + 8], [640 - tipHW - 6, tipY + 8]])}" fill="#16213A" stroke="${C.cyan}" stroke-width="1.2" stroke-linejoin="round"></polygon>` +
    line([[640, tipY], [640, y0]], '#BFF6FF', 1.2, 0.7) +
    line([[640 - baseHW * 0.5, y0 - 4], [640 - tipHW * 0.6, tipY + 22], [640 + tipHW * 0.6, tipY + 22], [640 + baseHW * 0.5, y0 - 4]], C.blue, 1, 0.5) +
    `<circle cx="${640 - tipHW + 10}" cy="${tipY + 4}" r="2" fill="${C.cyan}" opacity="0.8"></circle><circle cx="${640 + tipHW - 10}" cy="${tipY + 4}" r="2" fill="${C.cyan}" opacity="0.8"></circle>`;
}
// Morro con dos niveles de anchura: tramo cercano (ancho) y tramo lejano, más estrecho, tras el cristal, hasta la punta ancha.
function wideNose2(y0, hw0, y1, hw1, tipY, tipHW) {
  const B = k => [640 + k * hw0, y0], M = k => [640 + k * hw1, y1], T = k => [640 + k * tipHW, tipY];
  let s = '';
  [-1, 1].forEach(k => {
    const near = [B(k), M(k), [640, y1], [640, y0]], far = [M(k), T(k), [640, tipY], [640, y1]];
    s += `<polygon points="${pts(near)}" fill="url(#${k < 0 ? 'noseL' : 'noseR'})" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>`;
    s += `<polygon points="${pts(far)}" fill="url(#${k < 0 ? 'noseL' : 'noseR'})" stroke="${C.cyan}" stroke-width="1.4" stroke-linejoin="round" opacity="0.92"></polygon>`;
    // canto del escalón entre los dos niveles
    s += `<polygon points="${pts([M(k), [640 + k * (hw1 + 10), y1 + 6], [640, y1 + 6], [640, y1]])}" fill="#070B16" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.7"></polygon>`;
    // juntas y paneles de cada tramo
    s += line([[640 + k * hw0 * 0.55, y0 - 2], [640 + k * hw1 * 0.6, y1 + 8]], C.blue, 1, 0.5);
    s += line([[640 + k * hw1 * 0.55, y1 - 2], [640 + k * tipHW * 0.6, tipY + 6]], C.blue, 1, 0.45);
    s += screw((640 + k * hw0 * 0.78).toFixed(1), y0 - 10) + screw((640 + k * hw1 * 0.7).toFixed(1), y1 + 14);
  });
  // escalón: línea de luz entre el tramo cercano y el lejano
  s += line([M(-1), M(1)], '#BFF6FF', 1, 0.35);
  // punta ancha y plana
  s += `<polygon points="${pts([T(-1), T(1), [640 + tipHW + 6, tipY + 8], [640 - tipHW - 6, tipY + 8]])}" fill="#16213A" stroke="${C.cyan}" stroke-width="1.2" stroke-linejoin="round"></polygon>`;
  s += line([[640, tipY], [640, y0]], '#BFF6FF', 1.2, 0.7);
  s += `<circle cx="${640 - tipHW + 10}" cy="${tipY + 4}" r="2" fill="${C.cyan}" opacity="0.8"></circle><circle cx="${640 + tipHW - 10}" cy="${tipY + 4}" r="2" fill="${C.cyan}" opacity="0.8"></circle>`;
  return s;
}
// Morro escalonado: varios niveles de anchura, cada uno más lejano y estrecho, con un escalón marcado entre ellos.
function wideNoseN(levels, tipY, tipHW) {
  const lv = [...levels, [tipY, tipHW]];
  let s = '';
  for (let i = 0; i < lv.length - 1; i++) {
    const [ya, ha] = lv[i], [yb, hb] = lv[i + 1], last = i === lv.length - 2;
    const op = (1 - i * 0.06).toFixed(2);
    [-1, 1].forEach(k => {
      // al no ser el último tramo, el siguiente nace un poco hundido (escalón)
      const inner = last ? hb : hb + 12;
      const face = [[640 + k * ha, ya], [640 + k * inner, yb], [640, yb], [640, ya]];
      s += `<polygon points="${pts(face)}" fill="url(#${k < 0 ? 'noseL' : 'noseR'})" stroke="${C.cyan}" stroke-width="${(1.6 - i * 0.15).toFixed(2)}" stroke-linejoin="round" opacity="${op}"></polygon>`;
      s += line([[640 + k * ha * 0.55, ya - 2], [640 + k * inner * 0.6, yb + 6]], C.blue, 1, 0.5);
      if (!last) {
        // canto del escalón: cara oscura que baja del tramo siguiente
        s += `<polygon points="${pts([[640 + k * inner, yb], [640 + k * hb, yb - 8], [640, yb - 8], [640, yb]])}" fill="#05080F" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.8"></polygon>`;
        s += screw((640 + k * inner * 0.75).toFixed(1), yb + 8);
      }
    });
    if (!last) s += line([[640 - hb, yb - 8], [640 + hb, yb - 8]], '#BFF6FF', 1, 0.4) + line([[640 - (hb + 12), yb], [640 + hb + 12, yb]], C.cyan, 1, 0.5);
  }
  // punta ancha y plana
  s += `<polygon points="${pts([[640 - tipHW, tipY], [640 + tipHW, tipY], [640 + tipHW + 6, tipY + 7], [640 - tipHW - 6, tipY + 7]])}" fill="#16213A" stroke="${C.cyan}" stroke-width="1.2" stroke-linejoin="round"></polygon>`;
  s += line([[640, tipY], [640, levels[0][0]]], '#BFF6FF', 1.2, 0.7);
  s += `<circle cx="${640 - tipHW + 9}" cy="${tipY + 3}" r="2" fill="${C.cyan}" opacity="0.8"></circle><circle cx="${640 + tipHW - 9}" cy="${tipY + 3}" r="2" fill="${C.cyan}" opacity="0.8"></circle>`;
  return s;
}
const VECTOR_3 = ({
  id: 'Cab_Vector_3', name: 'DOBLE PANTALLA', top: 32,
  defs: `<linearGradient id="shadowDown" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#000" stop-opacity="0.7"></stop><stop offset="1" stop-color="#000" stop-opacity="0"></stop></linearGradient>
<linearGradient id="aoL" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#000" stop-opacity="0.55"></stop><stop offset="1" stop-color="#000" stop-opacity="0"></stop></linearGradient>
<linearGradient id="aoR" x1="1" y1="0" x2="0" y2="0"><stop offset="0" stop-color="#000" stop-opacity="0.55"></stop><stop offset="1" stop-color="#000" stop-opacity="0"></stop></linearGradient>
<linearGradient id="cowl" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1C2A48"></stop><stop offset="0.5" stop-color="#101A30"></stop><stop offset="1" stop-color="#070B16"></stop></linearGradient>
<linearGradient id="bezelIn" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#000" stop-opacity="0.8"></stop><stop offset="1" stop-color="#000" stop-opacity="0"></stop></linearGradient>`,
  svg: () => {
    let s = '';
    const cowlCurve = bez([0, 540], [190, 610], [440, 614], 64);
    const DASH_UP = 34;
    const cowlY0 = x => { const xx = x > 640 ? 1280 - x : x; if (xx >= 440) return 614; for (let i = 1; i < cowlCurve.length; i++) { const p0 = cowlCurve[i - 1], p1 = cowlCurve[i]; if (xx <= p1[0]) { const t = (xx - p0[0]) / (p1[0] - p0[0] || 1); return p0[1] + (p1[1] - p0[1]) * t; } } return 614; };
    const cowlY = x => cowlY0(x) - DASH_UP;
    // sombra interior del cristal (oclusión en los bordes): da sensación de estar dentro
    s += `<rect x="0" y="0" width="200" height="720" fill="url(#aoL)"></rect><rect x="1080" y="0" width="200" height="720" fill="url(#aoR)"></rect>`;
    s += wideNoseN([[580, 200], [556, 140], [536, 90]], 514, 40);
    // marco del cristal: recto y facetado (no envolvente tipo casco)
    const topBar = [[0, 0], [1280, 0], [1280, 18], [1120, 18], [1080, 34], [200, 34], [160, 18], [0, 18]];
    s += `<polygon points="${pts(off(topBar, 0, 8))}" fill="url(#pFront)" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.7"></polygon>`;
    s += panel(topBar, { fill: 'url(#pPillar)', bevel: false, hi: [[200, 37], [1080, 37]] });
    s += screw(640, 14) + screw(420, 24) + screw(860, 24);
    // ventanas laterales: un segundo pilar fino separa el cristal frontal del lateral, así la cabina rodea sin cerrarse
    [[1, 0], [-1, 1280]].forEach(([k, o]) => {
      const X = x => o + k * x;
      const yb = x => cowlY(X(x)) + 2;
      // franja exterior: baja hasta la visera
      s += `<polygon points="${pts([[X(0), 18], [X(26), 18], [X(104), yb(104)], [X(0), yb(0)]])}" fill="url(#pPillar)" stroke="${C.cyan}" stroke-width="1.4" stroke-linejoin="round"></polygon>`;
      s += line([[X(8), 30], [X(8), yb(8) - 6]], C.blue, 1, 0.5);
      for (let y = 60; y < 520; y += 46) s += screw(X(14), y);
      // cristal lateral algo más oscuro, con junta
      s += `<polygon points="${pts([[X(26), 18], [X(160), 18], [X(326), yb(326)], [X(104), yb(104)]])}" fill="#000" opacity="0.18"></polygon>`;
      s += line([[X(30), 22], [X(108), yb(108) - 4]], C.cyan, 1, 0.25) + line([[X(156), 22], [X(320), yb(320) - 4]], C.cyan, 1, 0.2);
      // pilar A recto con canto, que llega hasta la visera
      const front = [[X(160), 18], [X(196), 34], [X(360), yb(360)], [X(322), yb(322)]];
      const side = [[X(196), 34], [X(220), 34], [X(382), yb(382)], [X(360), yb(360)]];
      s += `<polygon points="${pts(side)}" fill="url(#pSide)" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.7"></polygon>`;
      s += `<polygon points="${pts(front)}" fill="url(#pPillar)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>`;
      s += line([front[1], front[2]], '#BFF6FF', 1, 0.3);
      // detalle del pilar: moldura central, remaches y tira de luz en el canto
      s += line([[X(178), 30], [X(341), yb(341) - 6]], C.blue, 1, 0.55);
      for (let t = 0.12; t < 0.95; t += 0.14) { const px = X(180 + 160 * t), py = 30 + (yb(340) - 36) * t; s += screw(px.toFixed(1), py.toFixed(1)); }
      s += line([[X(200), 60], [X(214), 120]], C.cyan, 2, 0.6) + line([[X(338), 520], [X(352), yb(352) - 14]], C.cyan, 2, 0.6);
      // base del pilar: pieza de anclaje sobre la visera
      s += `<polygon points="${pts([[X(312), yb(312) - 16], [X(392), yb(392) - 16], [X(398), yb(398)], [X(306), yb(306)]])}" fill="url(#pConsole)" stroke="${C.cyan}" stroke-width="1.2" stroke-linejoin="round"></polygon>`;
      s += screw(X(326), yb(326) - 8) + screw(X(382), yb(382) - 8);
    });
    // barra superior: sensor central, pilotos y rejillas
    s += `<rect x="600" y="18" width="80" height="20" fill="url(#pConsole)" stroke="${C.cyan}" stroke-width="1.2"></rect><circle cx="640" cy="28" r="5" fill="#04070F" stroke="${C.blue}" stroke-width="1"></circle><circle cx="640" cy="28" r="2" fill="${C.cyan}" opacity="0.7"></circle>`;
    s += vent(470, 20, 100, 10, 12, 1) + vent(710, 20, 100, 10, 12, -1);
    [[590, C.cyan], [692, C.orange]].forEach(([x, c]) => { s += `<circle cx="${x}" cy="28" r="1.8" fill="${c}" opacity="0.8"></circle>`; });
    s += micro(240, 30, 'VCT-01', 'start', C.grey, 0.45, 8) + micro(1040, 30, 'SYS', 'end', C.grey, 0.45, 8);
    // material ganado abajo al subir el salpicadero: panel inferior robusto
    s += `<rect x="0" y="${720 - DASH_UP - 8}" width="1280" height="${DASH_UP + 8}" fill="url(#pFront)" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.7"></rect>`;
    s += seam([[0, 720 - DASH_UP + 4], [1280, 720 - DASH_UP + 4]]);
    for (let x = 40; x < 1280; x += 80) s += screw(x, 720 - DASH_UP / 2 + 2);
    s += vent(150, 720 - DASH_UP + 10, 180, 16, 18, 2) + vent(950, 720 - DASH_UP + 10, 180, 16, 18, -2);
    s += micro(640, 716, 'VECTOR · VCT-01', 'middle', C.grey, 0.4, 8);
    s += `<g transform="translate(0 -${DASH_UP})">`;
    // salpicadero en dos planos: capota (visera) con canto, y frontal más abajo
    const cowlTopL = bez([0, 540], [190, 610], [440, 614], 16);
    const cowlTop = [...cowlTopL, [840, 614], ...mirrorX(cowlTopL).reverse()];
    const cowlLip = cowlTop.map(p => [p[0], p[1] + 18]);
    s += `<polygon points="${pts([...cowlTop, ...cowlLip.slice().reverse()])}" fill="url(#cowl)" stroke="${C.cyan}" stroke-width="1.6" stroke-linejoin="round"></polygon>`;
    s += line(cowlTop.map(p => [p[0], p[1] + 3]), '#BFF6FF', 1, 0.35);
    s += `<polygon points="${pts([...cowlLip, [1280, 720], [0, 720]])}" fill="url(#pTop)" stroke="${C.blue}" stroke-width="1" stroke-opacity="0.7"></polygon>`;
    // sombra que proyecta la visera sobre el frontal
    s += `<polygon points="${pts([...cowlLip, ...cowlLip.map(p => [p[0], p[1] + 46]).reverse()])}" fill="url(#shadowDown)"></polygon>`;
    // detalle de la visera: costura, ranuras de ventilación y pilotos
    s += line(cowlTop.map(p => [p[0], p[1] + 10]), C.blue, 1, 0.45, ' stroke-dasharray="3 4"');
    for (let x = 470; x <= 810; x += 34) if (x < 600 || x > 680) s += line([[x, 620], [x + 18, 620]], C.blue, 2, 0.6);
    s += `<circle cx="452" cy="624" r="1.8" fill="${C.cyan}" opacity="0.8"></circle><circle cx="828" cy="624" r="1.8" fill="${C.orange}" opacity="0.8"></circle>`;
    // pantallas hundidas en el frontal, bajo la visera, giradas hacia el piloto
    const frameL = [[132, 610], [398, 640], [390, 716], [120, 694]];
    const scrL = [[150, 624], [380, 652], [372, 704], [140, 684]];
    const both = [[frameL, scrL, 8, 262], [mirrorX(frameL).map((p, i, a) => a[[1, 0, 3, 2][i]]), mirrorX(scrL).map((p, i, a) => a[[1, 0, 3, 2][i]]), -8, 1018]];
    // huecos en el frontal: las carcasas del HUD quedan hundidas
    const sink = (fr, grow) => {
      const rim = inset(fr, -grow);
      let o = `<polygon points="${pts(rim)}" fill="#02030A" stroke="${C.cyan}" stroke-width="1.4" stroke-linejoin="round"></polygon>`;
      // paredes interiores del hueco: arriba en sombra, lados algo más claros, abajo con un poco de luz
      const walls = [[0, 1, 'url(#bezelIn)', 1], [1, 2, '#070B16', 0.9], [3, 0, '#070B16', 0.9], [2, 3, '#0D1424', 0.8]];
      walls.forEach(([i, j, f, op]) => { o += `<polygon points="${pts([rim[i], rim[j], fr[j], fr[i]])}" fill="${f}" opacity="${op}" stroke="${C.blue}" stroke-width="0.8" stroke-opacity="0.5"></polygon>`; });
      o += line([rim[0], rim[1]], '#BFF6FF', 1, 0.3);
      return o;
    };
    both.forEach(([fr]) => { s += sink(fr, 18); });
    s += sink([[500, 656], [780, 656], [800, 720], [480, 720]], 16);
    both.forEach(([fr, sc]) => {
      s += screen(sc, [190, 90]);
      s += panel(fr, { fill: 'url(#pConsole)', bevel: 5, depth: 12 });
      s += screen(sc);
      s += `<polygon points="${pts([fr[0], fr[1], [fr[1][0], fr[1][1] + 16], [fr[0][0], fr[0][1] + 16]])}" fill="url(#shadowDown)" opacity="0.8"></polygon>`;
      // canto interior: la pantalla queda hundida
      const c0 = sc[0], c1 = sc[1];
      s += `<polygon points="${pts([c0, c1, [c1[0] - (c1[0] > c0[0] ? 4 : -4), c1[1] + 10], [c0[0] + (c1[0] > c0[0] ? 4 : -4), c0[1] + 10]])}" fill="url(#bezelIn)"></polygon>`;
    });
    s += `<g transform="rotate(8 262 668)">${brackets(160, 630, 206, 62, 8)}${micro(170, 646, 'HULL STATUS')}${dmgText(358, 646, 'end')}${dmg(184, 658, 52, 16, 8)}${micro(184, 688, 'INTEGRITY')}<path d="M250 684L358 684" stroke="${C.blue}" stroke-width="1" opacity="0.6"></path></g>`;
    s += `<g transform="rotate(-8 1018 668)">${brackets(914, 630, 206, 62, 8)}${micro(924, 646, 'BOOST')}${micro(1112, 646, '{{boostTxt}}', 'end', C.orange, 0.8)}${speedText(1066, 682, 40, 'end')}${label(1074, 682, 'KM/H', 10, 'start')}${speedBar(926, 690, 176, 4)}${orbs(936, 664, 7, 10)}</g>`;
    // etiquetas y marcas en los marcos de las pantallas
    s += micro(140, 714, 'L-MFD', 'start', C.grey, 0.45, 8) + micro(1140, 714, 'R-MFD', 'end', C.grey, 0.45, 8);
    s += screw(130, 604) + screw(390, 634) + screw(1150, 604) + screw(890, 634);
    // consola central con grosor
    s += panel([[500, 656], [780, 656], [800, 720], [480, 720]], { fill: 'url(#pConsole)', bevel: 6, depth: 0, hi: [[502, 659], [778, 659]] });
    s += `<polygon points="${pts([[500, 656], [780, 656], [782, 672], [498, 672]])}" fill="url(#shadowDown)" opacity="0.8"></polygon>`;
    s += vent(530, 670, 220, 20, 22, 2) + switches(546, 708, 10, 20, [0, 3, 4, 8]);
    s += screw(494, 666) + screw(786, 666);
    s += micro(540, 700, 'NAV', 'start', C.grey, 0.5, 7) + micro(640, 700, 'COM', 'middle', C.grey, 0.5, 7) + micro(740, 700, 'AUX', 'end', C.grey, 0.5, 7);
    s += line([[510, 694], [770, 694]], C.blue, 1, 0.4);
    s += `</g>`;
    return s;
  }
});


fs.writeFileSync(path.join(OUT, VECTOR_3.id + '.dc.html'), board(VECTOR_3, 2, 'Vector'));
console.log('ok', VECTOR_3.id);
