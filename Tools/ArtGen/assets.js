// Product art, desktop icons, country tab flags, logo.
const { Pix, hex, mul, mix, rng } = require('./lib');

function products() {
  const P = new Pix(6 * 48, 40);
  const shadow = (x) => P.ell(x + 24, 36, 18, 2.5, '#1a1612');
  // 0 OLD CAMERA
  let x = 0; shadow(x);
  P.rect(x + 12, 16, 26, 17, '#3a3a38'); P.hl(x + 12, x + 37, 16, '#5a5a56'); P.rect(x + 33, 16, 5, 17, '#2a2a28'); P.hl(x + 12, x + 37, 25, '#8a2a22');
  P.rect(x + 16, 11, 9, 5, '#b9b9b0'); P.hl(x + 16, x + 24, 11, '#e0e0d8'); P.rect(x + 17, 12, 7, 3, '#f4f2e0');
  P.ell(x + 24, 25, 6, 6, '#1e1e1d'); P.ell(x + 24, 25, 4, 4, '#2e3e50'); P.set(x + 22, 23, '#9ab8d8'); P.set(x + 23, 23, '#6a88a8');
  for (let i = 0; i < 9; i++) P.rect(x + 30 + (i % 3), 18 + i, 7, 2, i % 2 ? '#8e8f8a' : '#a3a49e');
  P.rect(x + 32, 13, 4, 3, '#2a2a28');
  // 1 PRO CAMERA
  x = 48; shadow(x);
  P.rect(x + 10, 14, 24, 19, '#1a1a1c'); P.hl(x + 10, x + 33, 14, '#3a3a40'); P.rect(x + 16, 9, 10, 6, '#1a1a1c'); P.hl(x + 16, x + 25, 9, '#3a3a40');
  P.rect(x + 8, 16, 5, 16, '#222226'); P.rect(x + 28, 11, 4, 3, '#2a2a2e'); P.set(x + 30, 11, '#c84a3a');
  P.rect(x + 20, 17, 20, 15, '#141416'); P.vl(x + 34, 17, 31, '#c8402e'); P.vl(x + 35, 17, 31, '#8a2a1e'); P.rect(x + 38, 18, 3, 13, '#0c0c0e'); P.rect(x + 39, 21, 2, 7, '#3a5a8a'); P.set(x + 39, 21, '#a8c8f0');
  P.hl(x + 22, x + 32, 19, '#2e2e34');
  // 2 CRT
  x = 96; shadow(x);
  P.rect(x + 9, 4, 30, 26, '#b3a888'); P.hl(x + 9, x + 38, 4, '#c8bea0'); P.rect(x + 35, 4, 4, 26, '#968c6f'); P.rect(x + 12, 7, 22, 18, '#8a8269'); P.rect(x + 14, 9, 18, 14, '#1d5656', 1);
  P.rect(x + 16, 11, 12, 6, '#cbc4ad', 1); P.hl(x + 16, x + 27, 11, '#4b3f6e', 1); for (let y = 9; y < 23; y += 2) for (let xx = x + 14; xx < x + 32; xx++) { const c = P.get(xx, y); if (c) P.set(xx, y, mul(c, 0.85), 1); }
  P.rect(x + 18, 30, 12, 3, '#8f866b'); P.rect(x + 14, 33, 20, 2, '#a39a7e');
  // 3 DUAL MONITORS
  x = 144; shadow(x);
  for (const [ox, w] of [[3, 20], [24, 21]]) { P.rect(x + ox, 8, w, 15, '#18181b'); P.rect(x + ox + 1, 9, w - 2, 12, '#20354a', 1); P.rect(x + ox + 2, 10, w - 4, 3, '#3c4e7a', 1); P.hl(x + ox + 3, x + ox + w - 4, 15, '#8aa0b8', 1); P.hl(x + ox + 3, x + ox + w - 6, 18, '#8aa0b8', 1); P.rect(x + ox + w / 2 - 1, 23, 2, 9, '#2a2a2e'); P.rect(x + ox + w / 2 - 5, 32, 10, 2, '#2a2a2e'); }
  // 4 TAPED PRINTER
  x = 192; shadow(x);
  P.rect(x + 8, 8, 32, 4, '#ddd5bd'); P.rect(x + 6, 12, 36, 21, '#a8a390'); P.hl(x + 6, x + 41, 12, '#bdb8a4'); P.rect(x + 37, 12, 5, 21, '#8f8a78'); P.rect(x + 10, 23, 26, 2, '#2a2824'); P.rect(x + 13, 25, 18, 7, '#e9e2cc');
  for (let i = 0; i < 8; i++) P.rect(x + 6 + i, 13 + i, 4, 2, i % 3 ? '#8e8f8a' : '#a3a49e');
  P.rect(x + 28, 13, 10, 9, '#e2cd48'); P.hl(x + 29, x + 36, 15, '#5a4a1a'); P.hl(x + 29, x + 34, 17, '#5a4a1a'); P.set(x + 8, 28, '#6fe06a', 1);
  // 5 LASER PRINTER
  x = 240; shadow(x);
  P.rect(x + 5, 8, 38, 26, '#2a2a2e'); P.hl(x + 5, x + 42, 8, '#4a4a50'); P.rect(x + 38, 8, 5, 26, '#1e1e22'); P.rect(x + 8, 11, 12, 5, '#1a2a3e', 1); P.hl(x + 9, x + 17, 13, '#7fd0ff', 1);
  P.rect(x + 9, 22, 28, 2, '#0e0e10'); P.rect(x + 11, 24, 22, 8, '#ece6d2'); P.hl(x + 13, x + 28, 26, '#7a7466'); P.hl(x + 13, x + 25, 29, '#7a7466'); P.set(x + 34, 12, '#6fe06a', 1);
  return P.outlined('#120f0c');
}

function icons() {
  const P = new Pix(6 * 18, 18);
  // ORDERS clipboard
  let x = 1; P.rect(x + 2, 2, 12, 14, '#8a6a42'); P.rect(x + 3, 4, 10, 11, '#e8e0c8'); P.rect(x + 5, 1, 6, 3, '#9a9a92'); for (let i = 0; i < 4; i++) { P.rect(x + 4, 6 + i * 2, 1, 1, i === 2 ? '#c84a3a' : '#5a9a4a'); P.hl(x + 6, x + 11, 6 + i * 2, '#6a6252'); }
  // MESSAGES envelope
  x = 19; P.rect(x + 1, 4, 15, 10, '#e8e0c8'); P.line(x + 1, 4, x + 8, 10, '#8a826a'); P.line(x + 15, 4, x + 8, 10, '#8a826a'); P.rect(x + 12, 2, 4, 4, '#e0a83a'); P.set(x + 13, 3, '#fff2c0');
  // CLIENTS card
  x = 37; P.rect(x + 1, 3, 15, 11, '#cfc8b2'); P.ell(x + 5, 7, 2, 2, '#6a5a4a'); P.rect(x + 3, 9, 5, 3, '#6a5a4a'); P.hl(x + 9, x + 14, 6, '#6a6252'); P.hl(x + 9, x + 13, 8, '#8a826a'); P.hl(x + 9, x + 14, 10, '#8a826a');
  // NEWS
  x = 55; P.rect(x + 1, 2, 14, 14, '#e8e0c8'); P.rect(x + 2, 3, 12, 3, '#3a3a3a'); P.rect(x + 2, 7, 5, 5, '#8fa6b8'); for (let i = 0; i < 4; i++) P.hl(x + 8, x + 13, 7 + i * 2, '#6a6252'); P.hl(x + 2, x + 13, 14, '#6a6252');
  // SHOP crate
  x = 73; P.rect(x + 2, 6, 13, 10, '#a8804a'); P.hl(x + 2, x + 14, 6, '#c8a06a'); P.vl(x + 8, 6, 15, '#7a5a32'); P.hl(x + 2, x + 14, 10, '#7a5a32'); P.rect(x + 4, 2, 9, 4, '#5a9a4a'); P.text('$', x + 7, 1, '#e8e0c8');
  // EDITOR passport
  x = 91; P.rect(x + 3, 2, 11, 14, '#6e1e28'); P.hl(x + 3, x + 13, 2, '#8a2a34'); P.ell(x + 8.5, 8, 3, 3, '#c9a24a'); P.ell(x + 8.5, 8, 1.5, 1.5, '#6e1e28'); P.hl(x + 5, x + 12, 13, '#c9a24a');
  return P.outlined('#120f0c');
}

function flags() {
  const P = new Pix(10 * 14, 10);
  const f = (i, draw) => { const x = i * 14 + 1, y = 1; P.rect(x, y, 12, 8, '#ffffff'); draw(x, y); };
  const v3 = (a, b, c) => (x, y) => { P.rect(x, y, 4, 8, a); P.rect(x + 4, y, 4, 8, b); P.rect(x + 8, y, 4, 8, c); };
  const h3 = (a, b, c) => (x, y) => { P.rect(x, y, 12, 3, a); P.rect(x, y + 3, 12, 2, b); P.rect(x, y + 5, 12, 3, c); };
  f(0, v3('#2a4a9a', '#e8e4dc', '#c83a3a'));
  f(1, h3('#1a1a1a', '#c83a2a', '#e0b030'));
  f(2, (x, y) => { P.rect(x, y, 12, 8, '#c83a3a'); P.ell(x + 5, y + 4, 2.6, 2.6, '#f0ece4'); P.ell(x + 6, y + 4, 2.2, 2.2, '#c83a3a'); P.set(x + 8, y + 4, '#f0ece4'); P.set(x + 9, y + 3, '#f0ece4'); P.set(x + 9, y + 5, '#f0ece4'); });
  f(3, v3('#3a8a4a', '#e8e4dc', '#c83a3a'));
  f(4, (x, y) => { P.rect(x, y, 12, 8, '#2a3a7a'); P.line(x, y, x + 11, y + 7, '#e8e4dc'); P.line(x + 11, y, x, y + 7, '#e8e4dc'); P.rect(x, y + 3, 12, 2, '#e8e4dc'); P.rect(x + 5, y, 2, 8, '#e8e4dc'); P.hl(x, x + 11, y + 4 - 0, '#c83a3a'); P.vl(x + 5, y, y + 7, '#c83a3a'); });
  f(5, (x, y) => { for (let j = 0; j < 8; j++) P.hl(x, x + 11, y + j, j % 2 ? '#e8e4dc' : '#c83a3a'); P.rect(x, y, 5, 4, '#2a3a7a'); P.set(x + 1, y + 1, '#e8e4dc'); P.set(x + 3, y + 2, '#e8e4dc'); });
  f(6, (x, y) => { P.rect(x, y, 12, 8, '#f0ece4'); P.ell(x + 6, y + 4, 2.5, 2.5, '#c83a3a'); });
  f(7, (x, y) => { P.rect(x, y, 12, 8, '#f0ece4'); P.ell(x + 6, y + 4, 2.5, 2.5, '#c83a3a'); P.hl(x + 4, x + 8, y + 5, '#2a4a9a'); P.hl(x + 5, x + 7, y + 6, '#2a4a9a'); P.set(x + 1, y + 1, '#1a1a1a'); P.set(x + 10, y + 1, '#1a1a1a'); P.set(x + 1, y + 6, '#1a1a1a'); P.set(x + 10, y + 6, '#1a1a1a'); });
  f(8, (x, y) => { h3('#3a8a4a', '#f0ece4', '#c83a3a')(x, y); P.set(x + 6, y + 4, '#c83a3a'); P.set(x + 5, y + 3, '#c83a3a'); });
  f(9, (x, y) => { P.rect(x, y, 3, 8, '#c83a3a'); P.rect(x + 3, y, 9, 3, '#3a8a4a'); P.rect(x + 3, y + 3, 9, 2, '#f0ece4'); P.rect(x + 3, y + 5, 9, 3, '#1a1a1a'); });
  return P.outlined('#120f0c');
}

// chunky 6x8 logo glyphs
const LG = {
  L: ['##....', '##....', '##....', '##....', '##....', '##....', '######', '######'],
  E: ['######', '######', '##....', '#####.', '#####.', '##....', '######', '######'],
  G: ['.#####', '######', '##....', '##.###', '##.###', '##..##', '######', '.####.'],
  I: ['######', '######', '..##..', '..##..', '..##..', '..##..', '######', '######'],
  T: ['######', '######', '..##..', '..##..', '..##..', '..##..', '..##..', '..##..'],
  N: ['##..##', '###.##', '######', '######', '##.###', '##..##', '##..##', '##..##'],
  O: ['.####.', '######', '##..##', '##..##', '##..##', '##..##', '######', '.####.'],
  U: ['##..##', '##..##', '##..##', '##..##', '##..##', '##..##', '######', '.####.'],
  H: ['##..##', '##..##', '##..##', '######', '######', '##..##', '##..##', '##..##'],
};
function word(P, s, x, y, c, jitter = []) { let cx = x; [...s].forEach((ch, i) => { const g = LG[ch]; const dy = jitter[i] || 0; g.forEach((row, r) => [...row].forEach((v, q) => { if (v === '#') P.set(cx + q, y + r + dy, c); })); cx += 7; }); return cx - x - 1; }

function rotate(src, deg) {
  const a = deg * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a), cx = src.w / 2, cy = src.h / 2; const o = new Pix(src.w, src.h);
  for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) { const dx = x + 0.5 - cx, dy = y + 0.5 - cy; const sx = Math.floor(ca * dx + sa * dy + cx), sy = Math.floor(-sa * dx + ca * dy + cy); const c = src.get(sx, sy); if (c) o.set(x, y, c); }
  return o;
}
function wear(P, seed, amt) { const r = rng(seed); for (let i = 0; i < P.c.length; i++) if (P.c[i] && r() < amt) P.c[i] = null; }

function logo(variant) {
  if (variant === 'A') {
    const W = 64, H = 40, P = new Pix(W, H);
    const S = new Pix(W, H); const red = '#c0392b';
    S.box(10, 3, 45, 18, red); S.box(11, 4, 43, 16, red); word(S, 'LEGIT', 15, 8, red);
    wear(S, 3, 0.06); const R = rotate(S, -4);
    P.blit(R, 0, 0);
    const E = new Pix(W, H); word(E, 'ENOUGH', 20, 23, '#16120f', [0, 0, 1, 0, 0, -1]); const E2 = E.outlined('#16120f');
    const F2 = new Pix(W, H); word(F2, 'ENOUGH', 20, 23, '#ece3c8', [0, 0, 1, 0, 0, -1]);
    P.blit(E2, 1, 1); P.blit(F2, 0, 0);
    return P;
  }
  if (variant === 'B') { // round passport-style stamp
    const W = 56, H = 56, P = new Pix(W, H); const ink = '#2f4f8a';
    P.ring(28, 28, 26, 26, ink); P.ring(28, 28, 23.5, 23.5, ink); P.ring(28, 28, 13, 13, ink);
    word(P, 'LEGIT', 11, 18, ink); P.hl(8, 47, 28, ink);
    const sm = 'ENOUGH'; let x = 13; for (const ch of sm) { P.text(ch, x, 31, ink); x += 5; }
    for (const [dx, dy] of [[0, -20], [0, 20], [-20, 0], [20, 0]]) P.ell(28 + dx, 28 + dy, 1, 1, ink);
    wear(P, 8, 0.14); return rotate(P, 8);
  }
  if (variant === 'C') { // small-size mark
    const P = new Pix(20, 20); P.rect(1, 1, 18, 18, '#c0392b'); P.rect(3, 3, 14, 14, '#16120f'); word(P, 'L', 4, 5, '#ece3c8'); word(P, 'E', 10, 7, '#c0392b'); wear(P, 4, 0.03); return P;
  }
}

module.exports = { products, icons, flags, logo };
