// The shop, 400x225 native. tier 1 = Day 1 dump, tier 3 = Day 100.
const { Pix, hex, mul, mix, rng } = require('./lib');
const { person } = require('./people');
const C = require('./cast');

function windowView(P, x0, y0, w, h, time) {
  const r = rng(5);
  const sky = time === 'morning' ? ['#9fb4c6', '#c9d3d6'] : time === 'evening' ? ['#4e3f63', '#d98a5a'] : ['#0d1522', '#18263b'];
  for (let y = 0; y < h; y++) P.hl(x0, x0 + w - 1, y0 + y, mix(sky[0], sky[1], y / h), 1);
  const bld = time === 'morning' ? '#6d7177' : time === 'evening' ? '#3a2e3c' : '#0a0f18';
  const lit = time === 'morning' ? '#4f5964' : '#d6a24c';
  // buildings across the street
  const bx = [[0, 12, 14], [11, 20, 8], [19, 34, 18], [33, 48, 11]];
  for (const [a, b, top] of bx) { P.rect(x0 + a, y0 + top, b - a, h - top, bld, 1); for (let yy = y0 + top + 3; yy < y0 + h - 14; yy += 5) for (let xx = x0 + a + 2; xx < x0 + b - 2; xx += 4) P.rect(xx, yy, 2, 2, r() < (time === 'morning' ? 0.9 : 0.35) ? lit : mul(bld, 1.6), 1); }
  // street
  P.rect(x0, y0 + h - 11, w, 11, time === 'morning' ? '#77797a' : time === 'evening' ? '#4a3a3e' : '#101823', 1);
  P.hl(x0, x0 + w - 1, y0 + h - 11, time === 'morning' ? '#8d8f8e' : '#1c2635', 1);
  // street lamp
  const lx = x0 + w - 12; P.vl(lx, y0 + 8, y0 + h - 4, '#06090f', 1); P.hl(lx - 5, lx, y0 + 8, '#06090f', 1);
  if (time !== 'morning') { P.rect(lx - 6, y0 + 9, 3, 1, '#ffd98a', 1); for (let i = 0; i < 18; i++) { const a = r() * 6.28, d = 2 + r() * 4; P.set(lx - 5 + Math.cos(a) * d, y0 + 11 + Math.sin(a) * d * 0.7, '#6a5636', 1); } for (let yy = y0 + h - 9; yy < y0 + h; yy += 2) P.set(lx - 5 + (yy % 3), yy, '#b07a3a', 1); }
  // parked car silhouette
  P.rect(x0 + 4, y0 + h - 8, 18, 5, time === 'morning' ? '#4a5a6a' : '#06080d', 1); P.rect(x0 + 8, y0 + h - 11, 10, 3, time === 'morning' ? '#4a5a6a' : '#06080d', 1);
  if (time !== 'morning') P.set(x0 + 21, y0 + h - 7, '#c43a2a', 1);
  // rain
  if (time === 'night') { for (let i = 0; i < 70; i++) { const x = x0 + Math.floor(r() * w), y = y0 + Math.floor(r() * h); P.set(x, y, '#4f6a8c', 1); P.set(x - 1, y + 1, '#3a5070', 1); } for (let i = 0; i < 14; i++) P.set(x0 + Math.floor(r() * w), y0 + Math.floor(r() * h), '#8aa4c0', 1); }
}

function crt(P, x, y, time) {
  // 72x80 beige CRT, screen at +10,+9 size 52x44
  P.rect(x, y, 72, 58, '#b3a888'); P.rect(x + 1, y, 70, 1, '#c8bea0'); P.rect(x + 66, y, 6, 58, '#968c6f'); P.rect(x, y + 57, 72, 2, '#7d7560');
  P.rect(x + 6, y + 5, 58, 48, '#8a8269'); P.box(x + 6, y + 5, 58, 48, '#6e6753');
  const sx = x + 9, sy = y + 8, sw = 52, sh = 42;
  P.rect(sx, sy, sw, sh, '#1d5656', 1);
  P.set(sx, sy, '#8a8269'); P.set(sx + sw - 1, sy, '#8a8269'); P.set(sx, sy + sh - 1, '#8a8269'); P.set(sx + sw - 1, sy + sh - 1, '#8a8269');
  // desktop icons
  for (let i = 0; i < 4; i++) { P.rect(sx + 3, sy + 3 + i * 8, 5, 4, ['#d6b65a', '#d8d2be', '#8fb4c8', '#c86a5a'][i], 1); }
  // ORDERS window
  P.rect(sx + 11, sy + 3, 38, 30, '#cbc4ad', 1); P.rect(sx + 11, sy + 3, 38, 7, '#4b3f6e', 1); P.text('ORDERS', sx + 13, sy + 4, '#ece6d2', 1);
  P.rect(sx + 44, sy + 4, 4, 4, '#cbc4ad', 1);
  const st = ['#7fae5a', '#e0a83a', '#c84a3a', '#7fae5a'];
  for (let i = 0; i < 4; i++) { const yy = sy + 12 + i * 5; P.rect(sx + 13, yy, 3, 3, st[i], 1); P.hl(sx + 18, sx + 34, yy + 1, '#7b745f', 1); P.hl(sx + 38, sx + 46, yy + 1, '#9b937c', 1); }
  // taskbar
  P.rect(sx, sy + sh - 5, sw, 5, '#bdb49a', 1); P.rect(sx + 1, sy + sh - 4, 9, 3, '#8a3a3a', 1); P.rect(sx + sw - 6, sy + sh - 4, 4, 3, '#f0b23a', 1);
  // toast
  P.rect(sx + 30, sy + sh - 13, 20, 7, '#ece6d2', 1); P.rect(sx + 30, sy + sh - 13, 2, 7, '#f0b23a', 1); P.hl(sx + 34, sx + 47, sy + sh - 11, '#6a6352', 1); P.hl(sx + 34, sx + 43, sy + sh - 9, '#9a927c', 1);
  // scanlines + glare
  for (let yy = sy; yy < sy + sh; yy += 2) for (let xx = sx; xx < sx + sw; xx++) { const c = P.get(xx, yy); if (c) P.set(xx, yy, mul(c, 0.86), 1); }
  P.set(sx + 2, sy + 1, '#cfeeea', 1); P.set(sx + 3, sy + 1, '#cfeeea', 1); P.set(sx + 1, sy + 2, '#cfeeea', 1);
  // sticky on bezel
  P.rect(x + 58, y - 3, 9, 9, '#d9c64a'); P.hl(x + 59, x + 65, y, '#8a7a2a'); P.hl(x + 59, x + 64, y + 2, '#8a7a2a');
  // neck + foot
  P.rect(x + 26, y + 59, 20, 5, '#8f866b'); P.rect(x + 18, y + 64, 36, 4, '#a39a7e'); P.hl(x + 18, x + 53, y + 67, '#6e6753');
}

function monitors(P, x, y) {
  for (const [ox, w] of [[0, 38], [40, 40]]) {
    P.rect(x + ox, y, w, 30, '#18181b'); P.rect(x + ox + 2, y + 2, w - 4, 25, '#20354a', 1);
    P.rect(x + ox + 4, y + 4, w - 8, 5, '#3c4e7a', 1); P.text(ox ? 'MSG' : 'ORDERS', x + ox + 5, y + 4, '#e8e4d6', 1);
    for (let i = 0; i < 3; i++) { P.rect(x + ox + 5, y + 12 + i * 5, 3, 3, ['#7fae5a', '#e0a83a', '#7fae5a'][i], 1); P.hl(x + ox + 10, x + ox + w - 7, y + 13 + i * 5, '#8aa0b8', 1); }
    P.rect(x + ox + w / 2 - 2, y + 30, 4, 8, '#2a2a2e'); P.rect(x + ox + w / 2 - 8, y + 38, 16, 2, '#2a2a2e');
  }
}

function printer(P, x, y, tier) {
  if (tier === 1) {
    P.rect(x + 6, y - 8, 56, 9, '#8f8a78'); P.rect(x + 9, y - 12, 50, 5, '#ddd5bd'); P.hl(x + 9, x + 58, y - 12, '#efe8d2');
    P.rect(x, y, 70, 44, '#a8a390'); P.hl(x, x + 69, y, '#bdb8a4'); P.rect(x + 62, y, 8, 44, '#8f8a78'); P.hl(x, x + 69, y + 43, '#6f6b5d');
    P.rect(x + 8, y + 22, 52, 3, '#2a2824');
    P.rect(x + 14, y + 25, 34, 16, '#e9e2cc'); for (let i = 0; i < 4; i++) P.hl(x + 17, x + 40 - i * 3, y + 28 + i * 3, '#8f887a');
    // duct tape
    for (let i = 0; i < 14; i++) { P.rect(x + 1 + i, y + 2 + i, 6, 2, i % 3 ? '#8e8f8a' : '#a3a49e'); }
    P.set(x + 4, y + 30, '#6fe06a', 1); P.set(x + 5, y + 30, '#6fe06a', 1);
    // HIT IT IF IT STOPS
    P.rect(x + 46, y + 2, 23, 20, '#e2cd48'); P.hl(x + 46, x + 68, y + 21, '#b8a33a');
    P.text('HIT', x + 47, y + 3, '#2a2418'); P.text('IT', x + 61, y + 3, '#2a2418'); P.text('IF IT', x + 47, y + 9, '#2a2418'); P.text('STOPS', x + 47, y + 15, '#2a2418');
    P.rect(x + 27, y + 4, 17, 16, '#e08aa0'); P.text('BUT', x + 28, y + 5, '#3a1a22'); P.text('SOFT', x + 28, y + 10, '#3a1a22'); P.text('LY', x + 28, y + 15, '#3a1a22');
  } else {
    P.rect(x, y - 6, 74, 52, '#2a2a2e'); P.hl(x, x + 73, y - 6, '#4a4a50'); P.rect(x + 66, y - 6, 8, 52, '#1e1e22');
    P.rect(x + 6, y - 2, 20, 9, '#1a2a3e', 1); P.text('READY', x + 7, y, '#7fd0ff', 1);
    P.rect(x + 8, y + 20, 56, 3, '#0e0e10'); P.rect(x + 12, y + 23, 44, 18, '#ece6d2'); for (let i = 0; i < 4; i++) P.hl(x + 15, x + 46, y + 26 + i * 3, '#7a7466');
    P.set(x + 60, y + 2, '#6fe06a', 1); P.set(x + 60, y + 5, '#6fb0ff', 1);
  }
}

function drawShop(o) {
  const t = o.tier || 1, time = o.time || 'night', people = o.people !== false;
  const P = new Pix(400, 225), r = rng(9);
  const T1 = t === 1;
  // ceiling
  P.rect(0, 0, 400, 16, T1 ? '#2a2822' : '#23211e');
  if (T1) { for (let x = 0; x < 400; x += 40) P.vl(x, 0, 15, '#1f1d18'); P.speck(40, 2, 32, 12, '#3d3628', 0.35, 3); P.rect(120, 0, 40, 3, '#1a1814'); }
  P.hl(0, 399, 15, '#191814'); P.hl(0, 399, 16, '#1f1e19');
  if (T1) { P.rect(148, 3, 104, 7, '#6e6e66'); P.hl(148, 251, 3, '#8a8a80'); P.hl(150, 249, 9, '#4a4a44'); P.hl(152, 222, 7, '#f2f5d8', 1); P.hl(152, 222, 6, '#d8dcb8', 1); P.hl(223, 248, 7, '#8a8c78', 1); P.hl(223, 248, 6, '#7a7c6a', 1); P.set(200, 10, '#3a3a34'); }
  else { for (const px of [160, 250]) { P.vl(px, 0, 19, '#1a1a18'); P.rect(px - 6, 20, 13, 5, '#2f4a3a'); P.hl(px - 7, px + 7, 25, '#a88a4a'); P.hl(px - 4, px + 4, 26, '#ffe2a0', 1); P.hl(px - 5, px - 2, 20, '#4a6a52'); } }
  // wall
  const wU = T1 ? '#7a7358' : '#4b5654', wU2 = T1 ? '#6f6850' : '#45504e';
  P.rect(0, 17, 400, 103, wU); P.speck(0, 17, 400, 103, wU2, 0.16, 1);
  if (T1) {
    for (let x = 28; x < 92; x++) { const d = 4 + Math.round(6 * Math.sin((x - 28) / 64 * Math.PI) + r() * 3); P.vl(x, 17, 17 + d, '#6a6249'); P.set(x, 18 + d, '#5e573f'); }
    let cx = 250; for (let y = 17; y < 46; y++) { cx += r() < 0.5 ? 0 : r() < 0.5 ? 1 : -1; P.set(cx, y, '#4e4836'); }
    P.speck(0, 100, 400, 20, '#655e47', 0.25, 4);
  }
  // wainscot
  if (T1) { P.rect(0, 120, 400, 30, '#465140'); P.speck(0, 120, 400, 30, '#3d4738', 0.2, 6); P.hl(0, 399, 119, '#2f352a'); P.hl(0, 399, 120, '#5a6650'); }
  else { P.rect(0, 120, 400, 30, '#4a3826'); for (let x = 0; x < 400; x += 12) P.vl(x, 121, 149, '#3a2b1d'); P.hl(0, 399, 119, '#8a7444'); P.hl(0, 399, 120, '#a88e56'); }
  // floor
  for (let y = 150; y < 225; y++) for (let x = 0; x < 400; x++) { const tile = (Math.floor(x / 12) + Math.floor((y - 150) / 6)) % 2; P.set(x, y, T1 ? (tile ? '#4a4334' : '#3f392c') : ((Math.floor(y / 4) % 2) ? '#5a4330' : '#523d2c')); }
  if (!T1) for (let y = 150; y < 225; y += 4) for (let x = (y * 7) % 37; x < 400; x += 37) P.set(x, y, '#3d2c1e');
  P.hl(0, 399, 148, '#25211a'); P.hl(0, 399, 149, '#2d281f');
  if (T1 && time === 'night') { for (let i = 0; i < 6; i++) { const fx = 96 + i * 9, fy = 154 + i * 3 + (i % 2) * 2; P.rect(fx, fy, 3, 2, '#363022'); P.set(fx + 1, fy, '#5d6a72'); } P.rect(110, 149, 12, 2, '#5d6a72'); P.hl(112, 120, 150, '#7f8f98'); }

  // fuse box + wires (T1) / filing cabinet (T3)
  if (T1) {
    P.rect(10, 40, 22, 28, '#6b6c64'); P.box(10, 40, 22, 28, '#4a4b45'); P.rect(12, 42, 18, 3, '#7c7d74'); P.set(28, 55, '#c9a24a');
    P.rect(12, 46, 4, 2, '#d8cf9a'); P.text('!', 20, 47, '#c8a83a');
    P.line(14, 40, 14, 17, '#1d1c1a'); P.line(18, 40, 22, 17, '#1d1c1a'); P.line(26, 40, 26, 30, '#6a2a22'); P.line(26, 30, 40, 22, '#6a2a22'); P.line(40, 22, 40, 17, '#6a2a22');
    for (let x = 32; x < 66; x++) P.set(x, 68 + Math.round(Math.sin((x - 32) / 34 * Math.PI) * 9), '#1d1c1a');
    P.line(250, 17, 304, 17, '#1d1c1a'); P.line(304, 17, 309, 38, '#1d1c1a'); for (let x = 256; x < 304; x += 12) P.set(x, 18, '#9a9a90');
  } else {
    P.rect(6, 72, 30, 50, '#5c6266'); P.hl(6, 35, 72, '#747a7e'); for (let d = 0; d < 3; d++) { P.box(8, 75 + d * 16, 26, 14, '#4a4f53'); P.rect(18, 80 + d * 16, 6, 2, '#b9a46a'); }
    P.rect(40, 28, 26, 30, '#6b5434'); P.rect(42, 30, 22, 26, '#d8cfb2'); P.ell(53, 41, 7, 7, '#a8a07c'); P.text('LIC', 47, 39, '#5a3a2a'); P.text('*', 58, 49, '#a33a2e');
  }

  // door
  P.rect(72, 48, 38, 102, '#2f271e'); P.rect(75, 51, 32, 98, T1 ? '#5e4b36' : '#4e3a2a');
  P.box(78, 94, 26, 22, mul(T1 ? '#5e4b36' : '#4e3a2a', 0.8)); P.box(78, 120, 26, 22, mul(T1 ? '#5e4b36' : '#4e3a2a', 0.8));
  P.rect(79, 55, 24, 34, '#1a1a1a'); windowView(P, 80, 56, 22, 32, time);
  for (let y = 56; y < 88; y++) for (let x = 80; x < 102; x++) { const c = P.get(x, y); if (c && (x + y) % 3 === 0) P.set(x, y, mix(c, '#7f97ad', 0.35), 1); }
  const neonOn = time !== 'morning';
  P.box(82, 64, 18, 9, neonOn ? '#7a2f4a' : '#4a3a40', neonOn); P.text('OPEN', 84, 66, neonOn ? '#ff6f9a' : '#6a4a52', neonOn, true);
  P.rect(101, 104, 3, 3, '#c9a24a'); P.set(101, 104, '#ead08a');
  P.rect(76, 139, 30, 9, '#6d6e66'); P.hl(76, 105, 139, '#8a8b82');
  P.rect(89, 44, 4, 4, '#b0913e'); P.set(90, 44, '#e0c06a');
  if (T1) { P.rect(84, 100, 12, 8, '#d8cfb2'); P.text('NO', 86, 101, '#3a2a20'); P.hl(85, 94, 107, '#b8b0a0'); }

  // coat rack
  P.vl(115, 62, 148, '#3a2c20'); P.hl(111, 119, 148, '#3a2c20'); P.hl(112, 118, 62, '#3a2c20'); P.set(111, 61, '#3a2c20'); P.set(119, 61, '#3a2c20');
  if (T1) { // wet coat + umbrella
    for (let y = 64; y < 96; y++) { const w = 3 + Math.min(4, (y - 64) / 4); P.hl(Math.round(113 - w), Math.round(113 + w), y, y % 7 === 0 ? '#3f3f31' : '#4a4a3a'); }
    P.vl(120, 64, 97, '#2a3550'); P.rect(119, 70, 3, 22, '#2b3550'); P.set(120, 100, '#6d86a0'); P.set(120, 104, '#6d86a0'); P.rect(116, 148, 9, 2, '#5d6a72');
  } else { for (let y = 64; y < 92; y++) { const w = 3 + Math.min(4, (y - 64) / 4); P.hl(Math.round(113 - w), Math.round(113 + w), y, '#2a2b36'); } }

  // window
  P.rect(124, 36, 52, 60, '#2c2a23'); P.rect(127, 39, 46, 54, '#111'); windowView(P, 127, 39, 46, 54, time);
  P.vl(150, 39, 92, '#2c2a23'); P.hl(127, 172, 66, '#2c2a23');
  // blinds
  const bl = T1 ? '#a69e80' : '#c8bfa4';
  for (let y = 39; y < (T1 ? 52 : 47); y += 2) P.hl(127, 172, y, bl);
  if (T1) { P.line(127, 52, 146, 60, bl); P.line(127, 53, 145, 61, mul(bl, 0.8)); }
  P.vl(170, 39, 70, '#8a826a'); P.rect(169, 70, 3, 3, '#8a826a');
  P.rect(122, 96, 56, 3, T1 ? '#5d553f' : '#6a5440'); P.hl(122, 177, 96, T1 ? '#776d52' : '#8a7058');
  if (!T1) { P.rect(160, 88, 8, 8, '#8a5a3a'); P.ell(164, 84, 6, 6, '#3f6a3a'); P.ell(161, 82, 3, 4, '#4f7a46'); P.ell(167, 85, 3, 3, '#365c32'); }

  // poster / art
  if (T1) {
    P.rect(182, 26, 30, 38, '#c9bf9e'); P.rect(184, 28, 26, 14, '#d88a5a'); P.ell(197, 40, 8, 6, '#f0c25a'); P.rect(184, 36, 26, 6, '#5a6a8a'); P.rect(193, 33, 4, 7, '#3a2a22'); P.set(197, 32, '#3a2a22'); P.set(198, 31, '#3a2a22');
    P.text('TRUST', 186, 44, '#7a2a22'); P.text('THE', 190, 50, '#7a2a22'); P.text('PROCES', 184, 56, '#7a2a22');
    P.set(211, 26, '#7a7358'); P.set(210, 26, '#e0d8bc'); P.set(211, 27, '#e0d8bc'); P.rect(182, 26, 2, 2, '#d8d2b8');
  } else {
    P.rect(182, 28, 32, 26, '#3a2a1e'); P.rect(184, 30, 28, 22, '#c9b78a'); P.rect(184, 42, 28, 10, '#6a7a5a'); P.ell(204, 36, 4, 4, '#e8d8a0'); P.rect(188, 38, 3, 6, '#3a3a2a');
  }
  // certificate
  P.rect(258, 34, 18, 16, '#6b5434'); P.rect(260, 36, 14, 12, '#d8cfb2'); P.hl(262, 271, 38, '#8a7a5a'); P.hl(262, 268, 41, '#a89a7a'); P.ell(270, 45, 2, 2, '#a33a2e');
  // clock
  P.ell(289, 30, 8, 8, '#2a2723'); P.ell(289, 30, 6.5, 6.5, '#d9d2bb');
  for (const [dx, dy] of [[0, -5], [5, 0], [0, 5], [-5, 0]]) P.set(289 + dx, 30 + dy, '#4a4438');
  if (T1) { P.line(289, 30, 286, 34, '#1a1814'); P.line(289, 30, 291, 25, '#1a1814'); P.line(285, 26, 293, 33, '#9a9480'); }
  else { P.line(289, 30, 293, 31, '#1a1814'); P.line(289, 30, 289, 25, '#1a1814'); }

  // trash can
  if (T1) { P.rect(166, 134, 10, 15, '#5a5c58'); P.hl(165, 176, 134, '#74766f'); P.ell(169, 132, 3, 2, '#d8d0b8'); P.ell(173, 131, 2, 2, '#c8c0a6'); P.set(178, 147, '#d8d0b8'); P.set(179, 147, '#c0b89e'); }

  // photo corner
  if (T1) {
    P.hl(318, 390, 24, '#3a3a36'); P.hl(318, 390, 25, '#2a2a27');
    for (let x = 320; x <= 386; x++) { const sag = Math.round(Math.sin(((x - 320) % 22) / 22 * Math.PI) * 2); const bottom = 134 + Math.round(Math.sin(x * 0.7) * 1.5); for (let y = 26 + sag; y < bottom; y++) { const f = Math.sin(x * 0.42 + Math.sin(y * 0.04) * 1.6); P.set(x, y, f > 0.72 ? '#a0b4c3' : f < -0.6 ? '#7891a3' : '#8ba2b4'); } }
    for (const cx of [320, 342, 364, 386]) P.rect(cx - 1, 24, 3, 3, '#6a6a62');
    P.rect(380, 120, 7, 5, '#8e8f8a');
    // clamp lamp
    P.vl(309, 46, 148, '#2e2e2c'); P.line(309, 148, 303, 150, '#2e2e2c'); P.line(309, 148, 315, 150, '#2e2e2c');
    for (let i = 0; i < 8; i++) P.hl(304 + Math.floor(i / 2), 310 + i, 40 + i, i < 2 ? '#c4c4bc' : '#a8a8a0');
    P.rect(312, 47, 4, 2, '#fff6d8', 1); P.line(309, 50, 300, 90, '#1d1c1a'); P.line(300, 90, 302, 149, '#1d1c1a');
    // plastic stool
    P.rect(344, 128, 18, 3, '#cdc6ae'); P.hl(344, 361, 128, '#e0dac6'); P.line(346, 131, 343, 149, '#b8b19a'); P.line(359, 131, 362, 149, '#b8b19a'); P.line(352, 131, 352, 149, '#a8a18a'); P.set(350, 129, '#8a8470');
    // tripod + old camera
    P.line(382, 108, 371, 149, '#2a2a28'); P.line(382, 108, 393, 149, '#2a2a28'); P.vl(382, 108, 149, '#232321');
    P.rect(374, 99, 16, 9, '#3a3a38'); P.hl(374, 389, 99, '#555552'); P.rect(369, 101, 5, 5, '#1e1e1d'); P.set(369, 103, '#5a7a9a'); P.rect(378, 96, 6, 3, '#b9b9b0'); P.hl(374, 389, 104, '#8a2a22'); P.rect(386, 100, 3, 3, '#8e8f8a');
    // photo guide
    P.rect(391, 40, 9, 30, '#d8d0b4'); P.ring(396, 49, 3, 4, '#6a6250'); P.hl(393, 399, 58, '#6a6250'); P.hl(393, 399, 62, '#9a927a'); P.rect(392, 66, 3, 3, '#7fae5a'); P.rect(396, 66, 3, 3, '#c84a3a');
  } else {
    P.rect(316, 20, 76, 5, '#1e1e20'); P.rect(320, 25, 68, 125, '#8c9396');
    for (let y = 150; y < 168; y++) P.hl(320 - Math.round((y - 150) * 0.3), 387 + Math.round((y - 150) * 0.3), y, mix('#8c9396', '#a3aaad', (y - 150) / 18));
    for (let y = 25; y < 150; y++) for (let x = 320; x < 388; x++) { const dx = (x - 354) / 40, dy = (y - 80) / 70; if (dx * dx + dy * dy < 0.5) P.set(x, y, '#9aa1a4'); }
    for (const sx of [300, 391]) { P.rect(sx, 38, 16, 22, '#202022'); P.rect(sx + 2, 40, 12, 18, '#f4f1e6', 1); P.vl(sx + 8, 60, 166, '#2a2a2c'); P.line(sx + 8, 166, sx + 2, 170, '#2a2a2c'); P.line(sx + 8, 166, sx + 14, 170, '#2a2a2c'); }
    P.rect(343, 124, 20, 4, '#3a2e2a'); P.hl(343, 362, 124, '#5a4a40'); P.rect(345, 128, 16, 3, '#6a4a30'); P.vl(346, 131, 160, '#5a3a24'); P.vl(359, 131, 160, '#5a3a24'); P.hl(346, 359, 150, '#5a3a24');
    P.line(372, 104, 362, 168, '#141414'); P.line(372, 104, 384, 168, '#141414'); P.vl(372, 104, 168, '#1a1a1a');
    P.rect(364, 92, 18, 12, '#18181a'); P.hl(364, 381, 92, '#3a3a3e'); P.rect(356, 94, 8, 8, '#101012'); P.vl(358, 94, 101, '#a83a2a'); P.set(356, 97, '#6a8aba'); P.rect(377, 89, 4, 3, '#2a2a2e');
  }

  // waiting customer
  if (people) {
    if (T1) { // two plastic chairs, student in one
      for (const [cx, col] of [[132, '#9c5a3c'], [154, '#4d6b66']]) { P.rect(cx - 8, 110, 16, 16, col); P.hl(cx - 8, cx + 7, 110, mix(col, '#ffffff', 0.15)); P.rect(cx - 8, 126, 17, 4, mul(col, 0.85)); P.vl(cx - 7, 130, 148, '#3a3a38'); P.vl(cx + 7, 130, 148, '#3a3a38'); }
      P.blit(person({ ...C.nervous, sit: true, hat: null, look: -1 }), 114, 82);
    } else {
      P.rect(122, 112, 46, 14, '#5a2e2a'); P.hl(122, 167, 112, '#7a4a3e'); P.rect(122, 126, 46, 5, '#4a2622'); P.vl(124, 131, 148, '#2a1a14'); P.vl(165, 131, 148, '#2a1a14');
      P.blit(person({ ...C.samir, sit: true, mood: 'neutral' }), 128, 83);
    }
  } else if (T1) {
    for (const [cx, col] of [[132, '#9c5a3c'], [154, '#4d6b66']]) { P.rect(cx - 8, 110, 16, 16, col); P.hl(cx - 8, cx + 7, 110, mix(col, '#ffffff', 0.15)); P.rect(cx - 8, 126, 17, 4, mul(col, 0.85)); P.vl(cx - 7, 130, 148, '#3a3a38'); P.vl(cx + 7, 130, 148, '#3a3a38'); }
  }
  // customer at counter
  if (people) P.blit(person(T1 ? C.samir : C.wealthy), 212, 73);

  // counter
  const ct = T1 ? '#7a5a3b' : '#3a2a20', ctL = T1 ? '#8e6b47' : '#5a4434', ctF = T1 ? '#55402c' : '#2e2119';
  P.rect(180, 126, 124, 8, ct); P.hl(180, 303, 126, ctL); P.hl(180, 303, 133, T1 ? '#4e3624' : '#a88a4a');
  P.rect(182, 134, 120, 42, ctF);
  for (let x = 182; x < 302; x += 14) P.vl(x, 135, 175, mul(ctF, 0.82));
  if (T1) { P.speck(182, 160, 120, 16, '#3d2c1c', 0.2, 12); for (let i = 0; i < 9; i++) P.hl(185 + Math.floor(r() * 110), 187 + Math.floor(r() * 110), 127 + Math.floor(r() * 6), '#6a4c30'); }
  else { for (let x = 186; x < 300; x += 28) P.box(x, 140, 24, 30, '#241a13'); }
  if (T1) { P.rect(198, 144, 42, 11, '#d6cdb2'); P.text('NO REFUND', 200, 147, '#2a211b'); P.rect(197, 143, 3, 2, '#c8c0a4'); P.rect(238, 143, 3, 2, '#c8c0a4'); }
  else { P.rect(220, 146, 46, 9, '#a88a4a'); P.text('NO REFUND', 222, 148, '#3a2a14'); }
  // counter objects
  P.ell(186, 123, 3, 2.5, '#c9a24a'); P.hl(183, 189, 125, '#8a6a2a'); P.set(185, 121, '#f0d88a');
  if (people) {
    P.rect(248, 127, 11, 4, '#6f8a5a'); P.rect(251, 126, 11, 4, '#7f9a66'); P.hl(251, 261, 126, '#9ab07a'); P.set(256, 128, '#5a7048');
    P.rect(264, 125, 22, 7, '#c4a35a'); P.hl(264, 285, 125, '#d8b86a'); P.rect(266, 123, 7, 2, '#c4a35a'); P.hl(265, 284, 131, '#9a7e42'); P.hl(267, 283, 124, '#e8e0cc');
  }
  if (T1) { P.rect(292, 116, 8, 10, '#8aa0a0'); P.hl(292, 299, 116, '#b8c8c8'); P.rect(293, 122, 6, 4, '#b89a4a'); P.text('$', 294, 117, '#3a3a30'); }

  // ---- foreground desk ----
  P.rect(0, 176, 400, 43, T1 ? '#5a4430' : '#4a3424'); P.hl(0, 399, 176, T1 ? '#6e5640' : '#6a4c36'); P.rect(0, 219, 400, 6, T1 ? '#3a2b1e' : '#2a1d14'); P.hl(0, 399, 219, T1 ? '#47352a' : '#3a2a1e');
  if (T1) { P.speck(0, 177, 400, 42, '#523e2b', 0.15, 21); P.ring(140, 186, 4, 2, '#4a3624'); P.ring(318, 214, 4, 2, '#4a3624'); }

  // computer
  if (T1) crt(P, 2, 128, time); else monitors(P, 2, 136);
  // keyboard
  if (T1) { P.rect(76, 200, 52, 14, '#b8ae90'); P.hl(76, 127, 200, '#cac0a2'); P.hl(76, 127, 213, '#8a826a'); for (let y = 202; y < 212; y += 3) for (let x = 78; x < 126; x += 3) P.rect(x, y, 2, 2, '#d4cbb0'); P.rect(92, 211, 20, 1, '#d4cbb0'); }
  else { P.rect(76, 202, 56, 10, '#26262a'); for (let y = 204; y < 211; y += 3) for (let x = 78; x < 130; x += 3) P.rect(x, y, 2, 2, '#3e3e44'); }
  P.ell(138, 207, 3, 4, T1 ? '#b8ae90' : '#26262a'); P.vl(138, 203, 205, T1 ? '#8a826a' : '#44444a');

  // papers
  const sheet = (x, y, w, h, skew, seed) => { const rr = rng(seed); for (let j = 0; j < h; j++) { const sx = x + Math.round(j * skew); P.hl(sx + 1, sx + w, y + j + 1, '#2a2016'); } for (let j = 0; j < h; j++) { const sx = x + Math.round(j * skew); P.hl(sx, sx + w - 1, y + j, '#d5ccb4'); if (j % 3 === 2 && j < h - 2) P.hl(sx + 2, sx + 2 + Math.floor(w * (0.4 + rr() * 0.45)), y + j, '#a39a82'); } };
  if (T1) { sheet(98, 180, 26, 15, 0.3, 2); sheet(126, 184, 26, 18, -0.25, 3); sheet(108, 196, 22, 14, 0.15, 4); P.ring(140, 192, 3, 2, '#9a7a52'); }
  else { for (let i = 0; i < 4; i++) sheet(104 - i, 186 - i, 28, 16, 0, 5 + i); }
  // passport
  P.rect(156, 189, 50, 26, '#6e1e28'); P.hl(156, 205, 189, '#8a2a34'); P.rect(158, 190, 46, 23, '#e6dcc4'); P.vl(181, 190, 212, '#b8ad92'); P.vl(180, 190, 212, '#cfc4a8');
  P.rect(161, 194, 10, 12, '#9fb2c0'); P.ell(166, 198.5, 2.5, 3, '#5a6a78'); P.ell(166, 205, 4.5, 2, '#5a6a78');
  for (let y = 195; y < 207; y += 3) P.hl(173, 178, y, '#a89c80');
  for (let y = 193; y < 207; y += 3) P.hl(184, 200, y, '#a89c80');
  P.ring(196, 201, 4, 4, '#a8473c'); P.hl(160, 202, 209, '#7a7060'); P.hl(160, 202, 211, '#7a7060');
  P.line(207, 212, 219, 203, '#2a3a6a'); P.set(219, 203, '#c9a24a');
  // handbook
  const hb = T1 ? 1 : 2;
  for (let k = hb - 1; k >= 0; k--) { const yy = 182 - k * 5; P.rect(222, yy, 32, 24, k ? '#3a4a5a' : '#3d5944'); P.rect(222, yy, 4, 24, k ? '#2a3a4a' : '#2c4232'); P.hl(222, 253, yy + 23, '#1e2a22'); P.rect(230, yy + 6, 18, 7, '#d8cfb2'); P.hl(232, 246, yy + 8, '#5a5040'); P.hl(232, 242, yy + 10, '#8a8070'); }
  const tabs = ['#c84a3a', '#e0c04a', '#4a7ac8', '#e8e2d0', '#5aa05a', '#a05ac8'];
  for (let i = 0; i < (T1 ? 5 : 6); i++) P.rect(254, 183 + i * 4, 3, 3, tabs[i]);
  P.rect(234, 179, 5, 4, '#e2cd48'); P.rect(243, 180, 4, 3, '#e08aa0');
  // lamp
  const lc = T1 ? '#4a6a52' : '#a88a4a', la = T1 ? '#2c3a2e' : '#7a6234';
  P.rect(264, 204, 16, 6, lc); P.hl(264, 279, 204, mix(lc, '#ffffff', 0.2)); P.line(271, 204, 268, 160, la); P.line(270, 204, 267, 160, la); P.line(268, 160, 258, 152, la);
  for (let i = 0; i < 9; i++) P.hl(250 - i, 258 + Math.floor(i / 3), 151 + i, i < 2 ? mix(lc, '#ffffff', 0.2) : lc);
  P.hl(242, 258, 160, '#fff2c0', 1); P.hl(244, 255, 161, '#ffe39a', 1);
  // mug
  P.rect(284, 196, 10, 12, T1 ? '#d8d0bc' : '#2a3a4a'); P.ell(289, 196, 5, 1.5, '#3a2416'); P.ring(296, 201, 2, 3, T1 ? '#d8d0bc' : '#2a3a4a'); P.vl(293, 197, 207, T1 ? '#b0a894' : '#1e2a36');
  if (T1) { P.set(288, 202, '#8a3a2a'); P.set(290, 200, '#8a3a2a'); P.set(291, 199, '#a89c80'); }
  // calculator
  P.rect(300, 197, 14, 17, '#3a3a38'); P.rect(302, 199, 10, 3, '#9fc28a', 1); for (let y = 204; y < 213; y += 3) for (let x = 302; x < 312; x += 3) P.rect(x, y, 2, 2, '#6a6a66');
  // stapler + pens
  P.rect(144, 212, 12, 4, '#7a2a24'); P.hl(144, 155, 212, '#9a3a30'); P.line(64, 214, 74, 210, '#c8a03a');
  // printer
  printer(P, 326, 158, t);
  // coins
  if (T1) { P.set(232, 214, '#c9a24a'); P.set(236, 216, '#b0b0a8'); P.set(240, 213, '#c9a24a'); }
  return P;
}

function lights(o) {
  const t = o.tier || 1, time = o.time || 'night';
  const L = [];
  if (t === 1) {
    L.push({ x: 200, y: 6, rx: 210, ry: 150, c: [0.5, 0.56, 0.4], k: time === 'morning' ? 0.3 : 0.8, p: 1.3 });
    L.push({ x: 236, y: 74, rx: 70, ry: 64, c: [0.5, 0.52, 0.4], k: time === 'morning' ? 0.2 : 0.55 });
    L.push({ x: 326, y: 64, rx: 80, ry: 95, c: [0.85, 0.8, 0.62], k: 0.9 });
    L.push({ x: 36, y: 152, rx: 72, ry: 60, c: [0.22, 0.55, 0.58], k: time === 'morning' ? 0.3 : 0.85 });
  } else {
    L.push({ x: 160, y: 26, rx: 140, ry: 120, c: [0.95, 0.72, 0.45], k: 0.75 }); L.push({ x: 250, y: 26, rx: 140, ry: 120, c: [0.95, 0.72, 0.45], k: 0.75 });
    L.push({ x: 310, y: 50, rx: 110, ry: 120, c: [0.9, 0.9, 0.86], k: 0.9 }); L.push({ x: 396, y: 50, rx: 90, ry: 120, c: [0.9, 0.9, 0.86], k: 0.7 });
    L.push({ x: 40, y: 150, rx: 70, ry: 50, c: [0.25, 0.4, 0.6], k: 0.7 });
  }
  L.push({ x: 236, y: 196, rx: 95, ry: 40, c: [1.25, 0.86, 0.48], k: time === 'morning' ? 0.5 : 1, p: 1.2 });
  L.push({ x: 250, y: 168, rx: 40, ry: 26, c: [1, 0.75, 0.4], k: 0.5 });
  if (time === 'night') { L.push({ x: 150, y: 70, rx: 50, ry: 70, c: [0.18, 0.26, 0.45], k: 0.6 }); L.push({ x: 91, y: 70, rx: 30, ry: 40, c: [0.6, 0.18, 0.32], k: 0.45 }); }
  if (time === 'morning') L.push({ x: 150, y: 70, rx: 170, ry: 160, c: [0.55, 0.62, 0.72], k: 1.0, p: 1 });
  if (time === 'evening') L.push({ x: 150, y: 72, rx: 150, ry: 140, c: [0.85, 0.48, 0.3], k: 0.8, p: 1.1 });
  if (o.flash) L.push({ x: 370, y: 100, rx: 420, ry: 300, c: [1.6, 1.6, 1.62], k: 1, p: 0.55 });
  const amb = time === 'morning' ? [0.6, 0.63, 0.7] : time === 'evening' ? [0.4, 0.34, 0.36] : [0.25, 0.26, 0.33];
  return { ambient: amb, lights: L, bands: 7 };
}

module.exports = { drawShop, lights };
