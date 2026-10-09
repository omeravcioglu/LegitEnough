// Builds the game's two pixel fonts as TrueType files (every pixel is a square outline).
//   LegitPixel  - 5x7 proportional body font with lowercase, descenders and Latin-1 accents
//   LegitStamp  - bold 2px-stroke capitals for headings (lowercase maps to capitals)
// Usage: node Tools/ArtGen/fonts.js   (writes Assets/LegitEnough/Resources/LegitEnough/Fonts/*.ttf)
// One font pixel is 100 units and the em is 1200, so font-size 24 = 2 screen px per font pixel.
const fs = require('fs'), path = require('path');

const PX = 100, EM = 1200;

// ---------------------------------------------------------------- LegitPixel glyphs
// Rows top to bottom. Row 0 is cap height, row 6 sits on the baseline, rows 7-8 are descenders.
const BODY = {
  A: '.###.|#...#|#...#|#####|#...#|#...#|#...#', B: '####.|#...#|#...#|####.|#...#|#...#|####.',
  C: '.###.|#...#|#....|#....|#....|#...#|.###.', D: '####.|#...#|#...#|#...#|#...#|#...#|####.',
  E: '#####|#....|#....|####.|#....|#....|#####', F: '#####|#....|#....|####.|#....|#....|#....',
  G: '.###.|#...#|#....|#.###|#...#|#...#|.####', H: '#...#|#...#|#...#|#####|#...#|#...#|#...#',
  I: '###|.#.|.#.|.#.|.#.|.#.|###', J: '..###|...#.|...#.|...#.|...#.|#..#.|.##..',
  K: '#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#', L: '#....|#....|#....|#....|#....|#....|#####',
  M: '#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#', N: '#...#|#...#|##..#|#.#.#|#..##|#...#|#...#',
  O: '.###.|#...#|#...#|#...#|#...#|#...#|.###.', P: '####.|#...#|#...#|####.|#....|#....|#....',
  Q: '.###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#', R: '####.|#...#|#...#|####.|#.#..|#..#.|#...#',
  S: '.####|#....|#....|.###.|....#|....#|####.', T: '#####|..#..|..#..|..#..|..#..|..#..|..#..',
  U: '#...#|#...#|#...#|#...#|#...#|#...#|.###.', V: '#...#|#...#|#...#|#...#|#...#|.#.#.|..#..',
  W: '#...#|#...#|#...#|#.#.#|#.#.#|#.#.#|.#.#.', X: '#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#',
  Y: '#...#|#...#|.#.#.|..#..|..#..|..#..|..#..', Z: '#####|....#|...#.|..#..|.#...|#....|#####',
  a: '.....|.....|.###.|....#|.####|#...#|.####', b: '#....|#....|####.|#...#|#...#|#...#|####.',
  c: '.....|.....|.###.|#....|#....|#...#|.###.', d: '....#|....#|.####|#...#|#...#|#...#|.####',
  e: '.....|.....|.###.|#...#|#####|#....|.###.', f: '..##|.#..|.#..|###.|.#..|.#..|.#..',
  g: '.....|.....|.####|#...#|#...#|#...#|.####|....#|.###.', h: '#....|#....|#.##.|##..#|#...#|#...#|#...#',
  i: '.#.|...|##.|.#.|.#.|.#.|###', j: '...#|....|..##|...#|...#|...#|...#|#..#|.##.',
  k: '#...|#...|#..#|#.#.|##..|#.#.|#..#', l: '##.|.#.|.#.|.#.|.#.|.#.|###',
  m: '.....|.....|##.#.|#.#.#|#.#.#|#.#.#|#.#.#', n: '.....|.....|#.##.|##..#|#...#|#...#|#...#',
  o: '.....|.....|.###.|#...#|#...#|#...#|.###.', p: '.....|.....|####.|#...#|#...#|#...#|####.|#....|#....',
  q: '.....|.....|.####|#...#|#...#|#...#|.####|....#|....#', r: '....|....|#.##|##..|#...|#...|#...',
  s: '.....|.....|.####|#....|.###.|....#|####.', t: '.#..|.#..|###.|.#..|.#..|.#.#|..#.',
  u: '.....|.....|#...#|#...#|#...#|#..##|.##.#', v: '.....|.....|#...#|#...#|#...#|.#.#.|..#..',
  w: '.....|.....|#...#|#...#|#.#.#|#.#.#|.#.#.', x: '.....|.....|#...#|.#.#.|..#..|.#.#.|#...#',
  y: '.....|.....|#...#|#...#|#...#|#...#|.####|....#|.###.', z: '.....|.....|#####|...#.|..#..|.#...|#####',
  0: '.###.|#...#|#..##|#.#.#|##..#|#...#|.###.', 1: '..#..|.##..|..#..|..#..|..#..|..#..|.###.',
  2: '.###.|#...#|....#|...#.|..#..|.#...|#####', 3: '#####|...#.|..#..|...#.|....#|#...#|.###.',
  4: '...#.|..##.|.#.#.|#..#.|#####|...#.|...#.', 5: '#####|#....|####.|....#|....#|#...#|.###.',
  6: '..##.|.#...|#....|####.|#...#|#...#|.###.', 7: '#####|....#|...#.|..#..|.#...|.#...|.#...',
  8: '.###.|#...#|#...#|.###.|#...#|#...#|.###.', 9: '.###.|#...#|#...#|.####|....#|...#.|.##..',
  ' ': '...|...|...|...|...|...|...',
  '!': '#|#|#|#|#|.|#', '"': '#.#|#.#|#.#|...|...|...|...', '#': '.#.#.|.#.#.|#####|.#.#.|#####|.#.#.|.#.#.',
  '$': '..#..|.####|#.#..|.###.|..#.#|####.|..#..', '%': '##...|##..#|...#.|..#..|.#...|#..##|...##',
  '&': '.##..|#..#.|#.#..|.#...|#.#.#|#..#.|.##.#', "'": '#|#|#|.|.|.|.',
  '(': '..#|.#.|#..|#..|#..|.#.|..#', ')': '#..|.#.|..#|..#|..#|.#.|#..',
  '*': '.....|..#..|#.#.#|.###.|#.#.#|..#..|.....', '+': '.....|..#..|..#..|#####|..#..|..#..|.....',
  ',': '..|..|..|..|..|##|.#|#.|..', '-': '....|....|....|####|....|....|....', '.': '..|..|..|..|..|##|##',
  '/': '.....|....#|...#.|..#..|.#...|#....|.....', ':': '..|##|##|..|##|##|..', ';': '..|##|##|..|##|.#|#.',
  '<': '...#|..#.|.#..|#...|.#..|..#.|...#', '=': '....|....|####|....|####|....|....', '>': '#...|.#..|..#.|...#|..#.|.#..|#...',
  '?': '.###.|#...#|....#|...#.|..#..|.....|..#..', '@': '.###.|#...#|....#|.##.#|#.#.#|#.#.#|.###.',
  '[': '###|#..|#..|#..|#..|#..|###', '\\': '.....|#....|.#...|..#..|...#.|....#|.....', ']': '###|..#|..#|..#|..#|..#|###',
  '^': '..#..|.#.#.|#...#|.....|.....|.....|.....', '_': '.....|.....|.....|.....|.....|.....|.....|#####',
  '`': '#.|.#|..|..|..|..|..', '{': '..#|.#.|.#.|#..|.#.|.#.|..#', '|': '#|#|#|#|#|#|#', '}': '#..|.#.|.#.|..#|.#.|.#.|#..',
  '~': '.....|.....|.#...|#.#.#|...#.|.....|.....',
  '·': '..|..|..|##|##|..|..', '–': '.....|.....|.....|#####|.....|.....|.....', '—': '......|......|......|######|......|......|......', '…': '.......|.......|.......|.......|.......|#.#.#..|#.#.#..',
  '✉': '.....|#####|##.##|#.#.#|#...#|#####|.....', '⏰': '#...#|.###.|#.#.#|#.###|#...#|.###.|#...#',
  '▸': '#...|##..|###.|##..|#...|....|....', '↑': '..#..|.###.|#.#.#|..#..|..#..|..#..|..#..', '↓': '..#..|..#..|..#..|..#..|#.#.#|.###.|..#..',
  '◉': '.###.|#...#|#.#.#|#.#.#|#...#|.###.|.....', '▲': '.....|..#..|.###.|#####|.....|.....|.....', '▼': '.....|#####|.###.|..#..|.....|.....|.....',
  '◀': '...#|..##|.###|..##|...#|....|....', '▶': '#...|##..|###.|##..|#...|....|....', '●': '.....|.###.|#####|#####|#####|.###.|.....',
  '×': '.....|.....|#...#|.#.#.|..#..|.#.#.|#...#', 'ß': '.##..|#..#.|#..#.|#.#..|#..#.|#...#|#.##.',
  'Ø': '.####|#..##|#.#.#|#.#.#|#.#.#|##..#|####.', 'ø': '.....|.....|.####|#..##|#.#.#|##..#|####.',
  '°': '.#.|#.#|.#.|...|...|...|...', '•': '..|..|..|##|##|..|..', '‘': '.#|#.|#.|..|..|..|..', '’': '#|#|.|.|.|.|.',
  '“': '.#.#|#.#.|#.#.|....|....|....|....', '”': '#.#|#.#|...|...|...|...|...', '€': '..###|.#...|####.|.#...|####.|.#...|..###',
  '£': '..##.|.#..#|.#...|###..|.#...|.#...|#####', '©': '.###.|#...#|#.#.#|##..#|#.#.#|#...#|.###.',
};
// Marks used to compose accented letters (5 columns wide, centred over the letter).
const MARKS = {
  '̀': '.#...|..#..', '́': '...#.|..#..', '̂': '..#..|.#.#.', '̃': '.#..#|#.##.', '̈': '.....|.#.#.',
  '̊': '..#..|.#.#.', '̆': '.#.#.|..#..', '̌': '.#.#.|..#..', '̇': '.....|..#..',
};
const CEDILLA = '..#..|.##..';

// ---------------------------------------------------------------- LegitStamp glyphs (8 rows tall)
const STAMP = {
  A: '.####.|##..##|##..##|##..##|######|##..##|##..##|##..##', B: '#####.|##..##|##..##|#####.|##..##|##..##|##..##|#####.',
  C: '.#####|##....|##....|##....|##....|##....|##....|.#####', D: '#####.|##..##|##..##|##..##|##..##|##..##|##..##|#####.',
  E: '######|##....|##....|#####.|##....|##....|##....|######', F: '######|##....|##....|#####.|##....|##....|##....|##....',
  G: '.#####|##....|##....|##.###|##..##|##..##|##..##|.#####', H: '##..##|##..##|##..##|######|##..##|##..##|##..##|##..##',
  I: '######|..##..|..##..|..##..|..##..|..##..|..##..|######', J: '...###|....##|....##|....##|....##|##..##|##..##|.####.',
  K: '##..##|##.##.|####..|###...|####..|##.##.|##..##|##..##', L: '##....|##....|##....|##....|##....|##....|##....|######',
  M: '##...##|###.###|#######|##.#.##|##...##|##...##|##...##|##...##', N: '##..##|###.##|######|##.###|##..##|##..##|##..##|##..##',
  O: '.####.|##..##|##..##|##..##|##..##|##..##|##..##|.####.', P: '#####.|##..##|##..##|##..##|#####.|##....|##....|##....',
  Q: '.####.|##..##|##..##|##..##|##..##|##.###|##..##|.###.#', R: '#####.|##..##|##..##|##..##|#####.|##.##.|##..##|##..##',
  S: '.#####|##....|##....|.####.|....##|....##|....##|#####.', T: '######|..##..|..##..|..##..|..##..|..##..|..##..|..##..',
  U: '##..##|##..##|##..##|##..##|##..##|##..##|##..##|.####.', V: '##..##|##..##|##..##|##..##|##..##|##..##|.####.|..##..',
  W: '##...##|##...##|##...##|##...##|##.#.##|#######|###.###|##...##', X: '##..##|##..##|.####.|..##..|..##..|.####.|##..##|##..##',
  Y: '##..##|##..##|##..##|.####.|..##..|..##..|..##..|..##..', Z: '######|....##|...##.|..##..|.##...|##....|##....|######',
  0: '.####.|##..##|##.###|######|###.##|##..##|##..##|.####.', 1: '..##..|.###..|..##..|..##..|..##..|..##..|..##..|######',
  2: '.####.|##..##|....##|...##.|..##..|.##...|##....|######', 3: '#####.|....##|....##|.####.|....##|....##|....##|#####.',
  4: '...##.|..###.|.####.|##.##.|######|...##.|...##.|...##.', 5: '######|##....|##....|#####.|....##|....##|##..##|.####.',
  6: '.####.|##....|##....|#####.|##..##|##..##|##..##|.####.', 7: '######|....##|...##.|..##..|..##..|..##..|..##..|..##..',
  8: '.####.|##..##|##..##|.####.|##..##|##..##|##..##|.####.', 9: '.####.|##..##|##..##|##..##|.#####|....##|....##|.####.',
  ' ': '....|....|....|....|....|....|....|....',
  '!': '##|##|##|##|##|..|##|##', '?': '.####.|##..##|....##|...##.|..##..|......|..##..|..##..',
  '.': '..|..|..|..|..|..|##|##', ',': '..|..|..|..|..|..|##|##|.#', ':': '..|##|##|..|..|##|##|..', ';': '..|##|##|..|..|##|##|.#',
  '-': '....|....|....|####|####|....|....|....', '+': '......|..##..|..##..|######|######|..##..|..##..|......',
  '/': '....##|....##|...##.|..##..|..##..|.##...|##....|##....', "'": '##|##|.#|..|..|..|..|..', '"': '##.##|##.##|.#..#|.....|.....|.....|.....|.....',
  '(': '..##|.##.|##..|##..|##..|##..|.##.|..##', ')': '##..|.##.|..##|..##|..##|..##|.##.|##..',
  '#': '.##.##.|#######|.##.##.|.##.##.|.##.##.|#######|.##.##.|.......', '$': '..##..|.#####|##.#..|.####.|..#.##|#####.|..##..|......',
  '%': '##...#|##..##|...##.|..##..|.##...|##..##|#...##|......', '&': '.###..|##.##.|##.##.|.###..|##.###|##..#.|.###.#|......',
  '*': '......|##..##|.####.|######|.####.|##..##|......|......', '=': '......|......|######|......|######|......|......|......',
  '<': '...##|..##.|.##..|##...|.##..|..##.|...##|.....', '>': '##...|.##..|..##.|...##|..##.|.##..|##...|.....',
  '@': '.####.|##..##|....##|.##.##|##.###|##.###|.####.|......', '[': '####|##..|##..|##..|##..|##..|##..|####', ']': '####|..##|..##|..##|..##|..##|..##|####',
  '_': '......|......|......|......|......|......|......|######', '|': '##|##|##|##|##|##|##|##',
  '·': '..|..|..|##|##|..|..|..', '–': '......|......|......|######|######|......|......|......', '—': '........|........|........|########|########|........|........|........',
  '↑': '..##..|.####.|######|..##..|..##..|..##..|..##..|..##..', '↓': '..##..|..##..|..##..|..##..|..##..|######|.####.|..##..',
  '▸': '##....|####..|######|####..|##....|......|......|......', '▶': '##....|####..|######|######|####..|##....|......|......',
  '◀': '....##|..####|######|######|..####|....##|......|......', '▲': '..##..|.####.|######|######|......|......|......|......',
  '▼': '######|######|.####.|..##..|......|......|......|......', '●': '.####.|######|######|######|######|.####.|......|......',
  '✉': '######|##..##|#.##.#|#....#|#....#|######|......|......', '◉': '.####.|#....#|#.##.#|#.##.#|#....#|.####.|......|......',
  '⏰': '#.##.#|.####.|##.#.#|##.###|##...#|.####.|#....#|......', '…': '..........|..........|..........|..........|..........|..........|##.##.##..|##.##.##..',
  '×': '......|##..##|.####.|..##..|.####.|##..##|......|......', '€': '..####|.##...|######|.##...|######|.##...|..####|......',
};

function rows(s) { return s.split('|'); }

// A glyph as a set of filled pixels: {w, cells:[[col,row]...]} with row 0 = cap top, increasing downward.
function bitmap(spec, rowOffset = 0) { const r = rows(spec); const cells = []; let w = 0; r.forEach((line, y) => { w = Math.max(w, line.length); [...line].forEach((c, x) => { if (c === '#') cells.push([x, y + rowOffset]); }); }); return { w, cells }; }

function compose(ch) {
  const nfd = ch.normalize('NFD');
  if (nfd.length < 2) return null;
  const base = BODY[nfd[0]];
  if (!base) return null;
  const g = bitmap(base);
  const upper = nfd[0] === nfd[0].toUpperCase() && nfd[0] !== 'i' && nfd[0] !== 'j';
  for (const m of nfd.slice(1)) {
    if (m === '̧') { bitmap(CEDILLA, 7).cells.forEach(([x, y]) => g.cells.push([x + Math.floor((g.w - 5) / 2), y])); continue; }
    const mk = MARKS[m]; if (!mk) return null;
    const off = Math.floor((g.w - 5) / 2);
    // Capitals carry the mark above the cap line; lowercase letters use the two rows above x-height.
    const top = upper ? -2 : 0;
    if (!upper && (nfd[0] === 'i' || nfd[0] === 'j')) g.cells = g.cells.filter(([, y]) => y > 1);
    if (!upper) g.cells = g.cells.filter(([, y]) => y > 1 || nfd[0] === 'b' || nfd[0] === 'd' || nfd[0] === 'h' || nfd[0] === 'k' || nfd[0] === 'l');
    bitmap(mk, top).cells.forEach(([x, y]) => g.cells.push([x + off, y]));
  }
  return g;
}

// ---------------------------------------------------------------- TrueType writer
const be16 = v => { const b = Buffer.alloc(2); b.writeUInt16BE(v & 0xffff); return b; };
const bes16 = v => { const b = Buffer.alloc(2); b.writeInt16BE(v); return b; };
const be32 = v => { const b = Buffer.alloc(4); b.writeUInt32BE(v >>> 0); return b; };
const pad4 = b => b.length % 4 ? Buffer.concat([b, Buffer.alloc(4 - (b.length % 4))]) : b;
function checksum(buf) { const b = pad4(buf); let s = 0; for (let i = 0; i < b.length; i += 4) s = (s + b.readUInt32BE(i)) >>> 0; return s; }

/** Pixels → merged rectangles (horizontal runs, then stacked runs with equal spans). */
function rectangles(cells) {
  const byRow = new Map();
  for (const [x, y] of cells) { if (!byRow.has(y)) byRow.set(y, new Set()); byRow.get(y).add(x); }
  const runs = [];
  for (const [y, xs] of [...byRow.entries()].sort((a, b) => a[0] - b[0])) {
    const sorted = [...xs].sort((a, b) => a - b); let s = sorted[0], p = s;
    for (let i = 1; i <= sorted.length; i++) { const x = sorted[i]; if (x === p + 1) { p = x; continue; } runs.push({ x0: s, x1: p + 1, y0: y, y1: y + 1 }); s = p = x; }
  }
  const out = [];
  for (const r of runs) { const prev = out.find(o => o.x0 === r.x0 && o.x1 === r.x1 && o.y1 === r.y0); if (prev) prev.y1 = r.y1; else out.push({ ...r }); }
  return out;
}

function buildFont(opts) {
  const { family, style, glyphs, capRows, lsbPad, weight } = opts;
  // glyphs: [{code, adv, cells}] in pixel units; row r → y range [(capRows-1-r)*PX, (capRows-r)*PX]
  const list = [{ code: -1, adv: 5, cells: [] }, ...glyphs];
  const glyfParts = [], loca = [0], metrics = [];
  let xMin = 0, yMin = 0, xMax = 0, yMax = 0, maxPts = 0, maxCont = 0;
  list.forEach((g, gi) => {
    let rects = rectangles(g.cells);
    if (gi === 0) rects = [{ x0: 0, x1: 4, y0: 0, y1: 1 }, { x0: 0, x1: 4, y0: capRows - 1, y1: capRows }, { x0: 0, x1: 1, y0: 1, y1: capRows - 1 }, { x0: 3, x1: 4, y0: 1, y1: capRows - 1 }];
    if (!rects.length) { glyfParts.push(Buffer.alloc(0)); loca.push(loca[loca.length - 1]); metrics.push([g.adv * PX, 0]); return; }
    const contours = rects.map(r => {
      const X0 = (r.x0 + lsbPad) * PX, X1 = (r.x1 + lsbPad) * PX, Y0 = (capRows - r.y1) * PX, Y1 = (capRows - r.y0) * PX;
      return [[X0, Y1], [X1, Y1], [X1, Y0], [X0, Y0]];
    });
    const pts = contours.flat();
    const gx0 = Math.min(...pts.map(p => p[0])), gx1 = Math.max(...pts.map(p => p[0])), gy0 = Math.min(...pts.map(p => p[1])), gy1 = Math.max(...pts.map(p => p[1]));
    xMin = Math.min(xMin, gx0); yMin = Math.min(yMin, gy0); xMax = Math.max(xMax, gx1); yMax = Math.max(yMax, gy1);
    maxPts = Math.max(maxPts, pts.length); maxCont = Math.max(maxCont, contours.length);
    const parts = [bes16(contours.length), bes16(gx0), bes16(gy0), bes16(gx1), bes16(gy1)];
    let end = -1; for (const c of contours) { end += c.length; parts.push(be16(end)); }
    parts.push(be16(0));
    parts.push(Buffer.alloc(pts.length, 1)); // all on-curve, long coordinates
    let px = 0; for (const p of pts) { parts.push(bes16(p[0] - px)); px = p[0]; }
    let py = 0; for (const p of pts) { parts.push(bes16(p[1] - py)); py = p[1]; }
    const data = pad4(Buffer.concat(parts));
    glyfParts.push(data); loca.push(loca[loca.length - 1] + data.length);
    metrics.push([g.adv * PX, gx0]);
  });
  const numGlyphs = list.length;
  const glyf = Buffer.concat(glyfParts);
  const locaBuf = Buffer.concat(loca.map(be32));
  const advMax = Math.max(...metrics.map(m => m[0]));
  const ascender = EM - 200, descender = -200;

  const stamp1904 = Buffer.alloc(8); stamp1904.writeBigUInt64BE(BigInt(Math.floor(Date.UTC(2026, 9, 5) / 1000) + 2082844800));
  const head = Buffer.concat([be32(0x00010000), be32(0x00010000), be32(0), be32(0x5F0F3CF5), be16(0x000B), be16(EM),
    stamp1904, stamp1904, bes16(xMin), bes16(yMin), bes16(xMax), bes16(yMax), be16(weight >= 700 ? 1 : 0), be16(6), bes16(2), bes16(1), bes16(0)]);
  const lineGap = PX; // one font pixel between lines
  const hhea = Buffer.concat([be32(0x00010000), bes16(ascender), bes16(descender), bes16(lineGap), be16(advMax), bes16(0), bes16(0), bes16(xMax),
    bes16(1), bes16(0), bes16(0), Buffer.alloc(8), bes16(0), be16(numGlyphs)]);
  const maxp = Buffer.concat([be32(0x00010000), be16(numGlyphs), be16(maxPts), be16(maxCont), be16(0), be16(0), be16(2), be16(0), be16(0), be16(0), be16(0), be16(0), be16(0), be16(0), be16(0)]);
  const hmtx = Buffer.concat(metrics.map(([a, l]) => Buffer.concat([be16(a), bes16(l)])));
  const codes = list.slice(1).map((g, i) => ({ code: g.code, gid: i + 1 })).filter(c => c.code <= 0xffff).sort((a, b) => a.code - b.code);
  const segs = codes.map(c => ({ start: c.code, end: c.code, delta: (c.gid - c.code) & 0xffff }));
  segs.push({ start: 0xffff, end: 0xffff, delta: 1 });
  const segX2 = segs.length * 2, entrySel = Math.floor(Math.log2(segs.length)), searchRange = 2 * Math.pow(2, entrySel);
  const sub = Buffer.concat([be16(4), be16(16 + segs.length * 8), be16(0), be16(segX2), be16(searchRange), be16(entrySel), be16(segX2 - searchRange),
    ...segs.map(s => be16(s.end)), be16(0), ...segs.map(s => be16(s.start)), ...segs.map(s => be16(s.delta)), ...segs.map(() => be16(0))]);
  const cmap = Buffer.concat([be16(0), be16(2), be16(0), be16(3), be32(20), be16(3), be16(1), be32(20), sub]);
  const avg = Math.round(metrics.slice(1).reduce((s, m) => s + m[0], 0) / Math.max(1, numGlyphs - 1));
  const firstChar = Math.min(...codes.map(c => c.code)), lastChar = Math.max(...codes.map(c => c.code));
  const os2 = Buffer.concat([be16(4), bes16(avg), be16(weight), be16(5), be16(0), bes16(650), bes16(600), bes16(0), bes16(75), bes16(650), bes16(600), bes16(0), bes16(350),
    bes16(PX), bes16(300), bes16(0), Buffer.alloc(10), be32(7), be32(0), be32(0), be32(0), Buffer.from('LGIT'), be16(weight >= 700 ? 0xA0 : 0xC0),
    be16(firstChar), be16(lastChar), bes16(ascender), bes16(descender), bes16(lineGap), be16(ascender), be16(-descender + lineGap), be32(1), be32(0), bes16(500), bes16(capRows * PX), be16(0), be16(32), be16(1)]);
  const post = Buffer.concat([be32(0x00030000), be32(0), bes16(-100), bes16(PX), be32(0), be32(0), be32(0), be32(0), be32(0)]);
  const psName = (family + '-' + style).replace(/\s/g, '');
  const names = [[1, family], [2, style], [3, psName + ';1.0'], [4, family + ' ' + style], [5, 'Version 1.0'], [6, psName]];
  const strs = names.map(([, s]) => Buffer.from(s, 'utf16le').swap16());
  let off = 0; const recs = names.map(([id], i) => { const r = Buffer.concat([be16(3), be16(1), be16(0x409), be16(id), be16(strs[i].length), be16(off)]); off += strs[i].length; return r; });
  const name = Buffer.concat([be16(0), be16(names.length), be16(6 + 12 * names.length), ...recs, ...strs]);

  const tables = { 'OS/2': os2, cmap, glyf, head, hhea, hmtx, loca: locaBuf, maxp, name, post };
  const tags = Object.keys(tables).sort();
  const n = tags.length, es = Math.floor(Math.log2(n)), sr = Math.pow(2, es) * 16;
  let offset = 12 + 16 * n; const dir = [], bodies = [];
  for (const t of tags) { const b = tables[t]; dir.push(Buffer.concat([Buffer.from(t), be32(checksum(b)), be32(offset), be32(b.length)])); bodies.push(pad4(b)); offset += pad4(b).length; }
  const font = Buffer.concat([be32(0x00010000), be16(n), be16(sr), be16(es), be16(n * 16 - sr), ...dir, ...bodies]);
  // head.checkSumAdjustment
  const headOffset = 12 + 16 * n + bodies.slice(0, tags.indexOf('head')).reduce((s, b) => s + b.length, 0);
  font.writeUInt32BE((0xB1B0AFBA - checksum(font)) >>> 0, headOffset + 8);
  return font;
}

// ---------------------------------------------------------------- assemble
function trimmed(g, keepFull) {
  if (keepFull || !g.cells.length) return { ...g, lsb: 0, adv: g.w + 1 };
  const xs = g.cells.map(c => c[0]); const x0 = Math.min(...xs), x1 = Math.max(...xs);
  return { w: x1 - x0 + 1, cells: g.cells.map(([x, y]) => [x - x0, y]), lsb: 0, adv: x1 - x0 + 2 };
}

function bodyGlyphs() {
  const out = [], seen = new Set();
  const add = (ch, g, full) => { const t = trimmed(g, full); out.push({ code: ch.codePointAt(0), adv: ch === ' ' ? 4 : t.adv, cells: t.cells }); seen.add(ch); };
  for (const ch of Object.keys(BODY)) add(ch, bitmap(BODY[ch]), /[0-9]/.test(ch));
  for (let cp = 0xc0; cp <= 0x17f; cp++) { const ch = String.fromCodePoint(cp); if (seen.has(ch)) continue; const g = compose(ch); if (g) add(ch, g, false); }
  add(' ', bitmap(BODY[' ']), false);
  return out;
}

function stampGlyphs() {
  const out = [];
  const add = (ch, spec) => { const g = trimmed(bitmap(spec), false); out.push({ code: ch.codePointAt(0), adv: ch === ' ' ? 4 : g.adv, cells: g.cells }); };
  for (const ch of Object.keys(STAMP)) {
    add(ch, STAMP[ch]);
    if (/[A-Z]/.test(ch)) add(ch.toLowerCase(), STAMP[ch]);
  }
  add(' ', STAMP[' ']);
  return out;
}

const OUT = path.join(__dirname, '..', '..', 'Assets', 'LegitEnough', 'Resources', 'LegitEnough', 'Fonts');
fs.mkdirSync(OUT, { recursive: true });
const body = buildFont({ family: 'Legit Pixel', style: 'Regular', glyphs: bodyGlyphs(), capRows: 7, lsbPad: 0, weight: 400 });
const stamp = buildFont({ family: 'Legit Stamp', style: 'Bold', glyphs: stampGlyphs(), capRows: 8, lsbPad: 0, weight: 700 });
fs.writeFileSync(path.join(OUT, 'LegitPixel.ttf'), body);
fs.writeFileSync(path.join(OUT, 'LegitStamp.ttf'), stamp);
console.log('LegitPixel.ttf', body.length, 'bytes;', 'LegitStamp.ttf', stamp.length, 'bytes ->', OUT);

// The same glyphs as C# tables, for text painted into the pixel art at runtime.
const csq = s => JSON.stringify(s); // a JSON string literal is also a valid C# string literal here
const table = (name, src, doc) => `        /// <summary>${doc}</summary>\n        public static readonly string[] ${name} =\n        {\n` +
  Object.keys(src).map(k => `            ${csq(k)}, ${csq(src[k])},`).join('\n') + '\n        };\n';
const { FR } = require('./lib');
const cs = `// Generated by Tools/ArtGen/fonts.js from the same tables as the UI fonts. Do not edit by hand.
namespace LegitEnough.Game
{
    /// <summary>Glyph bitmaps as key/rows pairs. '#' is a pixel; rows are separated by '|'.</summary>
    static class PixelGlyphs
    {
${table('Tiny', FR, '3x5 capitals for tiny labels painted on props.')}
${table('Body', BODY, '5x7 proportional body glyphs (rows 7-8 are descenders), same as LegitPixel.ttf.')}
${table('Stamp', STAMP, 'Bold 8-row capitals, same as LegitStamp.ttf.')}    }
}
`;
const csOut = path.join(__dirname, '..', '..', 'Assets', 'LegitEnough', 'Scripts', 'Game', 'Pixel', 'PixelGlyphs.cs');
fs.mkdirSync(path.dirname(csOut), { recursive: true });
fs.writeFileSync(csOut, cs);
console.log('wrote', csOut);
module.exports = { BODY, STAMP, bitmap };
