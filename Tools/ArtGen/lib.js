// Tiny pixel-art toolkit: buffer, primitives, 3x5 font, banded lighting, PNG out.
const fs = require('fs'), zlib = require('zlib');

const crcT = new Int32Array(256).map((_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c; });
function crc(buf) { let c = -1; for (const b of buf) c = crcT[(c ^ b) & 255] ^ (c >>> 8); return (c ^ -1) >>> 0; }
function chunk(t, d) { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); }
function pngBuf(w, h, rgba) {
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0; rgba.copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4); }
  const ih = Buffer.alloc(13); ih.writeUInt32BE(w, 0); ih.writeUInt32BE(h, 4); ih[8] = 8; ih[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ih), chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}

const hex = h => { if (Array.isArray(h)) return h; h = h.replace('#', ''); return [parseInt(h.slice(0, 2), 16) / 255, parseInt(h.slice(2, 4), 16) / 255, parseInt(h.slice(4, 6), 16) / 255]; };
const mul = (c, k) => { c = hex(c); return Array.isArray(k) ? [c[0] * k[0], c[1] * k[1], c[2] * k[2]] : [c[0] * k, c[1] * k, c[2] * k]; };
const mix = (a, b, t) => { a = hex(a); b = hex(b); return a.map((v, i) => v + (b[i] - v) * t); };
const rng = s => () => { s |= 0; s = s + 0x6D2B79F5 | 0; let t = Math.imul(s ^ s >>> 15, 1 | s); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };

const FR = {
  A: '.#.|#.#|###|#.#|#.#', B: '##.|#.#|##.|#.#|##.', C: '.##|#..|#..|#..|.##', D: '##.|#.#|#.#|#.#|##.', E: '###|#..|##.|#..|###',
  F: '###|#..|##.|#..|#..', G: '.##|#..|#.#|#.#|.##', H: '#.#|#.#|###|#.#|#.#', I: '###|.#.|.#.|.#.|###', J: '..#|..#|..#|#.#|.#.',
  K: '#.#|#.#|##.|#.#|#.#', L: '#..|#..|#..|#..|###', M: '#.#|###|###|#.#|#.#', N: '###|#.#|#.#|#.#|#.#', O: '.#.|#.#|#.#|#.#|.#.',
  P: '##.|#.#|##.|#..|#..', Q: '.#.|#.#|#.#|##.|.##', R: '##.|#.#|##.|#.#|#.#', S: '.##|#..|.#.|..#|##.', T: '###|.#.|.#.|.#.|.#.',
  U: '#.#|#.#|#.#|#.#|###', V: '#.#|#.#|#.#|#.#|.#.', W: '#.#|#.#|###|###|#.#', X: '#.#|#.#|.#.|#.#|#.#', Y: '#.#|#.#|.#.|.#.|.#.',
  Z: '###|..#|.#.|#..|###', 0: '###|#.#|#.#|#.#|###', 1: '.#.|##.|.#.|.#.|###', 2: '##.|..#|.#.|#..|###', 3: '##.|..#|.#.|..#|##.',
  4: '#.#|#.#|###|..#|..#', 5: '###|#..|##.|..#|##.', 6: '.##|#..|###|#.#|###', 7: '###|..#|.#.|.#.|.#.', 8: '###|#.#|###|#.#|###',
  9: '###|#.#|###|..#|##.', '!': '.#.|.#.|.#.|...|.#.', '?': '##.|..#|.#.|...|.#.', '.': '...|...|...|...|.#.', '$': '.##|##.|.#.|.##|##.',
  '-': '...|...|###|...|...', ':': '...|.#.|...|.#.|...', "'": '.#.|.#.|...|...|...', '/': '..#|..#|.#.|#..|#..', ' ': '...|...|...|...|...',
  '*': '#.#|.#.|#.#|...|...', '%': '#.#|..#|.#.|#..|#.#', ',': '...|...|...|.#.|#..', '#': '#.#|###|#.#|###|#.#',
};
const F = {}; for (const k in FR) F[k] = FR[k].replace(/\|/g, '');

class Pix {
  constructor(w, h) { this.w = w; this.h = h; this.c = new Array(w * h).fill(null); this.e = new Uint8Array(w * h); }
  set(x, y, c, em = 0) { x = Math.round(x); y = Math.round(y); if (x < 0 || y < 0 || x >= this.w || y >= this.h || c == null) return; const i = y * this.w + x; this.c[i] = hex(c); this.e[i] = em ? 1 : 0; }
  get(x, y) { if (x < 0 || y < 0 || x >= this.w || y >= this.h) return null; return this.c[y * this.w + x]; }
  has(x, y) { return this.get(x, y) != null; }
  rect(x, y, w, h, c, em) { if (c == null) return; c = hex(c); for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) this.set(x + i, y + j, c, em); }
  hl(x0, x1, y, c, em) { this.rect(Math.min(x0, x1), y, Math.abs(x1 - x0) + 1, 1, c, em); }
  vl(x, y0, y1, c, em) { this.rect(x, Math.min(y0, y1), 1, Math.abs(y1 - y0) + 1, c, em); }
  box(x, y, w, h, c, em) { this.hl(x, x + w - 1, y, c, em); this.hl(x, x + w - 1, y + h - 1, c, em); this.vl(x, y, y + h - 1, c, em); this.vl(x + w - 1, y, y + h - 1, c, em); }
  ell(cx, cy, rx, ry, c, em) { if (c == null) return; c = hex(c); for (let y = Math.floor(cy - ry); y <= Math.ceil(cy + ry); y++) for (let x = Math.floor(cx - rx); x <= Math.ceil(cx + rx); x++) { const dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry; if (dx * dx + dy * dy <= 1) this.set(x, y, c, em); } }
  ring(cx, cy, rx, ry, c, em) { c = hex(c); for (let y = Math.floor(cy - ry - 1); y <= Math.ceil(cy + ry + 1); y++) for (let x = Math.floor(cx - rx - 1); x <= Math.ceil(cx + rx + 1); x++) { const dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry; const d = Math.sqrt(dx * dx + dy * dy); if (Math.abs(d - 1) < 0.5 / Math.min(rx, ry) + 0.06) this.set(x, y, c, em); } }
  line(x0, y0, x1, y1, c, em) { c = hex(c); x0 = Math.round(x0); y0 = Math.round(y0); x1 = Math.round(x1); y1 = Math.round(y1); const dx = Math.abs(x1 - x0), dy = -Math.abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1; let err = dx + dy; for (;;) { this.set(x0, y0, c, em); if (x0 === x1 && y0 === y1) break; const e2 = 2 * err; if (e2 >= dy) { err += dy; x0 += sx; } if (e2 <= dx) { err += dx; y0 += sy; } } }
  speck(x, y, w, h, c, d, seed = 1, onlyFilled = true) { const r = rng(seed); c = hex(c); for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) if (r() < d && (!onlyFilled || this.has(x + i, y + j))) this.set(x + i, y + j, c, this.e[(y + j) * this.w + x + i]); }
  checker(x, y, w, h, c, ph = 0, em) { c = hex(c); for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) if ((x + i + y + j + ph) % 2 === 0) this.set(x + i, y + j, c, em); }
  tint(x, y, w, h, k) { for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) { const c = this.get(x + i, y + j); if (c) this.c[(y + j) * this.w + x + i] = mul(c, k); } }
  text(s, x, y, c, em, mirror) {
    s = String(s).toUpperCase(); if (mirror) s = s.split('').reverse().join('');
    let cx = x; for (const ch of s) { const g = F[ch] || F[' ']; for (let r = 0; r < 5; r++) for (let q = 0; q < 3; q++) { const qq = mirror ? 2 - q : q; if (g[r * 3 + qq] === '#') this.set(cx + q, y + r, c, em); } cx += 4; }
  }
  blit(src, ox, oy, flip) { for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) { const sx = flip ? src.w - 1 - x : x; const c = src.c[y * src.w + sx]; if (c) this.set(ox + x, oy + y, c, src.e[y * src.w + sx]); } }
  outlined(c) { const o = new Pix(this.w, this.h); o.c = this.c.slice(); o.e = this.e.slice(); for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) if (!this.has(x, y) && (this.has(x - 1, y) || this.has(x + 1, y) || this.has(x, y - 1) || this.has(x, y + 1))) o.set(x, y, c); return o; }
  crop(x, y, w, h) { const o = new Pix(w, h); for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) { const c = this.get(x + i, y + j); if (c) o.set(i, j, c, this.e[(y + j) * this.w + x + i]); } return o; }
}

const B4 = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]];
function lightAt(x, y, o) {
  const L = o.ambient.slice();
  for (const l of o.lights) { const dx = (x - l.x) / l.rx, dy = (y - l.y) / l.ry; const d = Math.sqrt(dx * dx + dy * dy); if (d < 1) { const f = Math.pow(1 - d, l.p || 1.5) * l.k; L[0] += l.c[0] * f; L[1] += l.c[1] * f; L[2] += l.c[2] * f; } }
  const b = (B4[y & 3][x & 3] + 0.5) / 16, n = o.bands || 8;
  return L.map(v => { const s = v * n, fl = Math.floor(s), fr = s - fl, t = 0.5 + (b - 0.5) * 0.34; return (fr > t ? fl + 1 : fl) / n; });
}
function save(file, P, scale = 1, o = {}) {
  const W = P.w * scale, H = P.h * scale, buf = Buffer.alloc(W * H * 4);
  const floor = o.floor || [0.03, 0.035, 0.05];
  for (let y = 0; y < P.h; y++) for (let x = 0; x < P.w; x++) {
    const i = y * P.w + x; let c = P.c[i]; let a = 255;
    if (!c) { if (o.bg) { c = hex(o.bg); } else { a = 0; c = [0, 0, 0]; } }
    else if (o.lights && !P.e[i]) { const L = lightAt(x, y, o); c = [Math.max(floor[0], c[0] * L[0]), Math.max(floor[1], c[1] * L[1]), Math.max(floor[2], c[2] * L[2])]; }
    const r = Math.min(255, Math.round(c[0] * 255)), g = Math.min(255, Math.round(c[1] * 255)), bb = Math.min(255, Math.round(c[2] * 255));
    for (let sy = 0; sy < scale; sy++) for (let sx = 0; sx < scale; sx++) { const k = ((y * scale + sy) * W + x * scale + sx) * 4; buf[k] = r; buf[k + 1] = g; buf[k + 2] = bb; buf[k + 3] = a; }
  }
  fs.writeFileSync(file, pngBuf(W, H, buf));
}
module.exports = { Pix, hex, mul, mix, rng, save, F, FR };
