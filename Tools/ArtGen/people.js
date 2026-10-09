// Modular customers: a 36x72 shop sprite and an 80x80 photo-booth portrait.
const { Pix, hex, mul, mix } = require('./lib');
const OUT = '#16120f';

const SKIN = { pale: '#f0c8a8', light: '#dba882', tan: '#c08a62', olive: '#a87a52', brown: '#7e5638', deep: '#58392a' };

// ---------- shop sprite ----------
function person(s) {
  const P = new Pix(36, 72);
  const skin = hex(SKIN[s.skin] || s.skin), skinS = mul(skin, 0.8), skinD = mul(skin, 0.62);
  const hair = hex(s.hair || '#2a1d14'), hairL = mix(hair, '#ffffff', 0.18);
  const top = hex(s.topCol || '#5a5a5a'), topS = mul(top, 0.78), topL = mix(top, '#ffffff', 0.12);
  const pants = hex(s.pants || '#2e3038'), shoes = hex(s.shoes || '#1e1a17');
  const sit = !!s.sit, hy = 4 + (s.slouch ? 1 : 0), look = s.look || 0;
  const pose = s.pose || 'idle';
  const sh = s.narrow ? 1 : 0;

  // long hair behind shoulders
  if (s.hairStyle === 'long') { P.rect(10, hy + 2, 16, 18, hair); P.rect(10, hy + 14, 2, 6, mul(hair, 0.8)); P.rect(24, hy + 14, 2, 6, mul(hair, 0.8)); }

  // legs
  if (sit) {
    P.rect(10, 45, 16, 6, pants); P.rect(10, 50, 6, 6, pants); P.rect(20, 50, 6, 6, pants);
    P.rect(16, 46, 4, 2, mul(pants, 0.8)); P.rect(9, 56, 7, 2, shoes); P.rect(20, 56, 7, 2, shoes);
  } else {
    P.rect(11, 45, 14, 4, pants); P.rect(11, 49, 6, 16, pants); P.rect(19, 49, 6, 16, pants);
    P.rect(23, 49, 2, 16, mul(pants, 0.8)); P.vl(17, 47, 64, mul(pants, 0.7));
    P.rect(10, 65, 7, 3, shoes); P.rect(19, 65, 7, 3, shoes); P.hl(10, 16, 65, mix(shoes, '#ffffff', 0.15)); P.hl(19, 25, 65, mix(shoes, '#ffffff', 0.15));
  }

  // torso + arms
  const tx0 = 9 + sh, tx1 = 26 - sh;
  for (let y = 19; y <= 45; y++) { const inset = y > 38 ? 1 : 0; P.hl(tx0 + inset, tx1 - inset, y, top); }
  P.hl(tx0 + 1, tx1 - 1, 18, top);
  const armTop = 20, armBot = 42;
  const sleeveEnd = s.top === 'tshirt' ? 28 : armBot;
  const armL = tx0 - 3, armR = tx1 + 1;
  if (pose !== 'crossed') {
    P.rect(armL, armTop, 3, sleeveEnd - armTop, top); P.rect(armR, armTop, 3, sleeveEnd - armTop, topS);
    if (sleeveEnd < armBot) { P.rect(armL, sleeveEnd, 3, armBot - sleeveEnd, skin); P.rect(armR, sleeveEnd, 3, armBot - sleeveEnd, skinS); }
    if (pose !== 'pockets' && pose !== 'phone') { P.rect(armL, armBot, 3, 3, skin); P.rect(armR, armBot, 3, 3, skinS); }
    if (pose === 'phone') { P.rect(armL, armBot, 3, 3, skin); P.rect(armR, armTop, 3, 12, topS); P.rect(armR - 6, 31, 8, 3, topS); P.rect(armR - 7, 29, 3, 4, skin); P.rect(armR - 9, 28, 3, 5, '#1a1f2a'); P.rect(armR - 9, 29, 2, 3, '#8fe0e8', 1); }
  } else {
    P.rect(armL, armTop, 3, 10, top); P.rect(armR, armTop, 3, 10, topS);
    P.rect(tx0 - 2, 29, tx1 - tx0 + 5, 5, mul(top, 0.9)); P.hl(tx0 - 2, tx1 + 2, 29, topL); P.hl(tx0 - 2, tx1 + 2, 33, topS);
    P.rect(tx0, 30, 2, 2, skin); P.rect(tx1 - 1, 31, 2, 2, skinS);
  }
  // shading
  for (let y = 18; y <= 45; y++) for (let x = tx1 - 3; x <= tx1; x++) { const c = P.get(x, y); if (c && c === top) P.set(x, y, topS); }
  P.vl(tx0, 20, 40, topL);

  // garment details
  const t = s.top;
  if (t === 'coat') {
    P.rect(tx0, 44, tx1 - tx0 + 1, 10, top); P.rect(tx1 - 3, 44, 4, 10, topS); P.vl(17, 21, 53, topS); P.vl(18, 21, 53, topS);
    P.line(14, 19, 17, 28, topL); P.line(21, 19, 18, 28, topS); P.rect(16, 19, 4, 3, s.topCol2 || '#cfc6ae');
    P.hl(tx0, tx1, 36, mul(top, 0.7)); P.rect(16, 35, 3, 3, '#8a7a4a');
    P.rect(13, 25, 1, 1, '#2a2218'); P.rect(13, 30, 1, 1, '#2a2218');
  } else if (t === 'hoodie') {
    P.rect(12, 17, 12, 3, topS); P.rect(13, 17, 10, 1, topL);
    P.rect(13, 33, 10, 6, mul(top, 0.88)); P.hl(13, 22, 33, topS);
    P.vl(16, 20, 26, '#d8d0bc'); P.vl(19, 20, 26, '#d8d0bc');
  } else if (t === 'suit') {
    P.rect(16, 19, 4, 12, s.topCol2 || '#d8d2c2'); P.vl(17, 20, 30, s.tie || '#7a2a2a'); P.vl(18, 20, 30, s.tie || '#7a2a2a');
    P.line(15, 19, 17, 32, topS); P.line(20, 19, 18, 32, topS); P.rect(11, 26, 3, 1, topL);
  } else if (t === 'jacket') {
    P.vl(18, 20, 44, mul(top, 0.6)); P.rect(14, 18, 8, 2, topS); P.rect(16, 19, 4, 3, s.topCol2 || '#e0d8c4');
    P.rect(11, 33, 3, 1, topS); P.rect(22, 33, 3, 1, topS);
  } else if (t === 'work') {
    P.rect(tx0, 31, tx1 - tx0 + 1, 2, '#d8d6c8'); P.rect(armL, 31, 3, 2, '#d8d6c8'); P.rect(armR, 31, 3, 2, '#b8b6a8');
    P.vl(17, 19, 44, topS); P.rect(14, 19, 8, 2, topS);
  } else if (t === 'cardigan') {
    P.rect(15, 19, 6, 26, s.topCol2 || '#d6ccb4'); P.vl(14, 19, 44, topS); P.vl(21, 19, 44, topS);
    P.rect(12, 34, 2, 3, topS); P.rect(23, 34, 2, 3, topS);
  } else if (t === 'tshirt') {
    P.rect(15, 18, 6, 2, skin); P.hl(14, 21, 20, topS);
    if (s.print) P.rect(15, 25, 6, 4, s.print);
  }
  if (s.fur) { P.rect(11, 17, 14, 4, s.fur); P.speck(11, 17, 14, 4, mul(s.fur, 0.8), 0.4, 3); }

  // neck
  P.rect(15, hy + 12, 6, 4, skinS); P.hl(15, 20, hy + 13, skinD);

  // head
  const rows = [[14, 21], [13, 22], [12, 23], [12, 23], [12, 23], [12, 23], [12, 23], [12, 23], [12, 23], [12, 23], [13, 22], [13, 22], [14, 21]];
  rows.forEach(([a, b], r) => P.hl(a, b, hy + r, skin));
  rows.forEach(([a, b], r) => { if (r > 1) { P.set(b, hy + r, skinS); if (r > 3) P.set(b - 1, hy + r, mix(skin, skinS, 0.5)); } });
  P.hl(14, 21, hy + 12, skinS);
  // ears
  P.rect(11, hy + 6, 1, 3, skinS); P.rect(24, hy + 6, 1, 3, skinD);
  if (s.earring) { P.set(11, hy + 9, s.earring); P.set(24, hy + 9, s.earring); }
  // eyes
  const ex1 = 14 + look, ex2 = 20 + look, ey = hy + 6;
  if (s.eyes === 'closed') { P.hl(ex1 - 1, ex1, ey + 1, skinD); P.hl(ex2, ex2 + 1, ey + 1, skinD); }
  else if (pose === 'phone') { P.hl(ex1 - 1, ex1, ey + 1, '#1d1714'); P.hl(ex2, ex2 + 1, ey + 1, '#1d1714'); P.hl(ex1 - 1, ex1, ey, skinS); P.hl(ex2, ex2 + 1, ey, skinS); }
  else { P.rect(ex1, ey, 1, 2, '#1d1714'); P.rect(ex2, ey, 1, 2, '#1d1714'); P.set(ex1 - (look >= 0 ? 1 : -1), ey, '#e8e2d6'); P.set(ex2 - (look >= 0 ? 1 : -1), ey, '#e8e2d6'); }
  // brows
  const bc = s.brow || mul(hair, 0.85);
  if (s.mood === 'angry') { P.set(ex1 - 1, ey - 2, bc); P.set(ex1, ey - 1, bc); P.set(ex2, ey - 1, bc); P.set(ex2 + 1, ey - 2, bc); }
  else if (s.mood === 'nervous') { P.set(ex1 - 1, ey - 1, bc); P.set(ex1, ey - 2, bc); P.set(ex2, ey - 2, bc); P.set(ex2 + 1, ey - 1, bc); }
  else { P.hl(ex1 - 1, ex1, ey - 2, bc); P.hl(ex2, ex2 + 1, ey - 2, bc); }
  // nose + mouth
  P.set(17 + look + (look > 0 ? 1 : 0), hy + 8, skinS); P.set(18 + look, hy + 9, skinD);
  const lip = mix(skinD, '#8a3a32', 0.45), mx = 16 + look;
  if (s.mood === 'smile') { P.hl(mx, mx + 3, hy + 10, lip); P.set(mx - 1, hy + 9, lip); P.set(mx + 4, hy + 9, lip); }
  else if (s.mood === 'angry' || s.mood === 'annoyed') { P.hl(mx, mx + 3, hy + 10, lip); P.set(mx - 1, hy + 11, lip); if (s.mood === 'angry') P.set(mx + 4, hy + 11, lip); }
  else if (s.mood === 'tired') { P.hl(mx + 1, mx + 2, hy + 10, lip); P.set(mx + 1, hy + 11, skinD); }
  else P.hl(mx, mx + 3, hy + 10, lip);
  if (s.blush) { P.set(13, hy + 9, s.blush); P.set(22, hy + 9, s.blush); }

  // facial hair
  const fh = s.facial;
  if (fh === 'stubble' || fh === 'beard' || fh === 'bigbeard') for (let y = hy + 8; y <= hy + 12; y++) for (let x = 12; x <= 23; x++) if (P.get(x, y) && (x + y) % 2 === 0 && !(y === hy + 10 && x >= mx && x <= mx + 3)) { const c = P.get(x, y); if (c === skin || c === skinS) P.set(x, y, mix(c, hair, 0.3)); }
  if (fh === 'beard' || fh === 'bigbeard') {
    P.rect(12, hy + 7, 2, 5, hair); P.rect(22, hy + 7, 2, 5, mul(hair, 0.85)); P.hl(13, 22, hy + 11, hair); P.hl(14, 21, hy + 12, hair); P.hl(15, 20, hy + 13, mul(hair, 0.85));
    P.hl(mx - 1, mx + 4, hy + 9, hair); P.hl(mx, mx + 3, hy + 10, lip);
    if (fh === 'bigbeard') { P.hl(13, 22, hy + 14, hair); P.hl(14, 21, hy + 15, hair); P.hl(15, 20, hy + 16, mul(hair, 0.85)); P.rect(11, hy + 8, 1, 5, hair); P.rect(24, hy + 8, 1, 5, mul(hair, 0.85)); }
  }
  if (fh === 'mustache') { P.hl(mx - 1, mx + 4, hy + 9, hair); }
  if (fh === 'bigmustache') { P.hl(mx - 2, mx + 5, hy + 9, hair); P.hl(mx - 1, mx + 4, hy + 8, hair); P.set(mx - 2, hy + 10, hair); P.set(mx + 5, hy + 10, hair); P.set(mx - 2, hy + 11, mul(hair, 0.8)); P.set(mx + 5, hy + 11, mul(hair, 0.8)); P.set(18 + look, hy + 8, skinD); }

  // hair
  const hs = s.hairStyle;
  const cap = () => { P.hl(14, 21, hy - 1, hair); P.hl(12, 23, hy, hair); P.hl(12, 23, hy + 1, hair); P.hl(12, 14, hy + 2, hair); P.hl(21, 23, hy + 2, hair); P.vl(12, hy + 3, hy + 5, hair); P.vl(23, hy + 3, hy + 5, mul(hair, 0.85)); P.hl(14, 18, hy - 1, hairL); };
  if (hs === 'short') { cap(); P.hl(15, 19, hy + 2, hair); }
  else if (hs === 'neat') { cap(); P.hl(12, 17, hy + 2, hair); P.set(18, hy, mul(hair, 0.7)); P.hl(13, 16, hy + 3, hair); }
  else if (hs === 'messy') { cap(); P.hl(13, 20, hy + 2, hair); P.set(13, hy + 3, hair); P.set(16, hy + 3, hair); P.set(19, hy + 3, hair); P.set(14, hy - 2, hair); P.set(18, hy - 2, hair); P.set(21, hy - 2, hair); P.set(11, hy + 1, hair); P.set(24, hy + 2, hair); P.set(11, hy + 4, hair); }
  else if (hs === 'curly') { for (let y = hy - 3; y <= hy + 3; y++) for (let x = 10; x <= 25; x++) { const dx = (x - 17.5) / 8, dy = (y - (hy + 1)) / 4.6; if (dx * dx + dy * dy < 1 && !(y >= hy + 2 && x >= 14 && x <= 21)) P.set(x, y, ((x * 3 + y * 5) % 4 === 0) ? hairL : hair); } P.rect(10, hy + 3, 3, 5, hair); P.rect(23, hy + 3, 3, 5, mul(hair, 0.85)); P.set(10, hy + 8, hair); P.set(25, hy + 8, hair); }
  else if (hs === 'long') { cap(); P.hl(12, 16, hy + 2, hair); P.rect(11, hy + 1, 2, 10, hair); P.rect(23, hy + 1, 2, 10, mul(hair, 0.85)); }
  else if (hs === 'bun') { cap(); P.hl(13, 22, hy + 2, hair); P.ell(17.5, hy - 2.5, 3, 2.5, hair); P.set(17, hy - 3, hairL); }
  else if (hs === 'bald') { P.rect(12, hy + 3, 1, 4, hair); P.rect(23, hy + 3, 1, 4, mul(hair, 0.85)); P.set(15, hy + 1, mix(skin, '#ffffff', 0.35)); P.set(16, hy + 1, mix(skin, '#ffffff', 0.2)); }
  else if (hs === 'flattop') { P.rect(12, hy - 3, 12, 5, hair); P.hl(12, 23, hy - 3, hairL); P.rect(12, hy + 2, 2, 4, hair); P.rect(22, hy + 2, 2, 4, mul(hair, 0.85)); P.hl(14, 21, hy + 2, hair); }
  if (s.gray) P.speck(10, hy - 4, 16, 12, '#9a958c', 0.22, 7);

  // hats
  const hc = hex(s.hatCol || '#3a3a40'), hcS = mul(hc, 0.75), hcL = mix(hc, '#ffffff', 0.15);
  if (s.hat === 'cap') { P.rect(12, hy - 2, 12, 4, hc); P.hl(13, 22, hy - 3, hc); P.hl(14, 18, hy - 2, hcL); P.hl(10, 23, hy + 2, hcS); P.set(17, hy - 3, hcS); }
  if (s.hat === 'flatcap') { P.rect(12, hy - 2, 12, 4, hc); P.hl(13, 23, hy - 3, hcL); P.hl(11, 24, hy + 1, hc); P.hl(11, 22, hy + 2, hcS); P.speck(12, hy - 3, 12, 4, hcS, 0.25, 2); }
  if (s.hat === 'beanie') { for (let y = hy - 3; y <= hy + 2; y++) P.hl(y === hy - 3 ? 14 : 12, y === hy - 3 ? 21 : 23, y, hc); P.rect(12, hy + 1, 12, 2, hcL); P.set(17, hy - 4, hcL); P.set(18, hy - 4, hcL); for (let x = 13; x < 23; x += 2) P.vl(x, hy - 2, hy, hcS); }
  if (s.hat === 'fedora') { P.rect(13, hy - 4, 10, 5, hc); P.hl(14, 21, hy - 5, hcS); P.hl(13, 22, hy - 1, s.band || '#6a2a24'); P.hl(9, 26, hy + 1, hcS); P.hl(10, 25, hy + 1, hc); }
  // glasses
  const gl = s.glasses;
  if (gl === 'glasses' || gl === 'gold') { const fc = gl === 'gold' ? '#c9a24a' : '#1a1614'; P.box(ex1 - 2, ey - 1, 4, 4, fc); P.box(ex2 - 1, ey - 1, 4, 4, fc); P.hl(ex1 + 2, ex2 - 2, ey, fc); P.set(ex1 - 1, ey, '#cfe2ea'); P.set(ex2, ey, '#cfe2ea'); }
  if (gl === 'sun') { P.rect(ex1 - 2, ey - 1, 4, 3, '#121214'); P.rect(ex2 - 1, ey - 1, 4, 3, '#121214'); P.hl(ex1 + 2, ex2 - 2, ey, '#121214'); P.set(ex1 - 1, ey - 1, '#6a7a8a'); P.set(ex2, ey - 1, '#6a7a8a'); }
  if (s.headphones) { const c = hex(s.headphones); P.hl(13, 22, hy - 3, c); P.set(12, hy - 2, c); P.set(23, hy - 2, c); P.vl(11, hy - 1, hy + 3, c); P.vl(24, hy - 1, hy + 3, c); P.rect(10, hy + 4, 3, 5, c); P.rect(23, hy + 4, 3, 5, mul(c, 0.8)); }
  if (s.scarf) { const c = hex(s.scarf); P.rect(13, hy + 13, 10, 4, c); P.rect(19, hy + 16, 3, 10, mul(c, 0.85)); P.hl(13, 22, hy + 14, mul(c, 0.85)); P.hl(19, 21, hy + 25, mix(c, '#ffffff', 0.2)); }
  if (s.bag) { const c = hex(s.bag); P.line(10, 19, 25, 36, mul(c, 0.7)); P.rect(24, 34, 8, 9, c); P.hl(24, 31, 34, mix(c, '#ffffff', 0.15)); P.rect(27, 37, 2, 2, '#c9a24a'); P.vl(31, 35, 42, mul(c, 0.7)); }
  if (s.handbag) { const c = hex(s.handbag); P.rect(4, 40, 6, 6, c); P.hl(5, 8, 38, '#c9a24a'); P.set(4, 39, '#c9a24a'); P.set(9, 39, '#c9a24a'); }
  if (s.backpack) { const c = hex(s.backpack); P.rect(8, 20, 2, 12, c); P.rect(26, 20, 2, 12, mul(c, 0.8)); }
  if (s.wet) { P.speck(9, 18, 18, 6, mix(top, '#9ab0c0', 0.5), 0.35, 11); P.set(8, 47, '#7f97ad'); P.set(27, 50, '#7f97ad'); }
  if (s.umbrella) { P.vl(30, 26, 60, '#22293a'); P.rect(29, 32, 3, 24, '#2b3550'); P.set(30, 25, '#8a7a5a'); P.set(29, 26, '#8a7a5a'); P.set(31, 62, '#7f97ad'); P.set(30, 65, '#7f97ad'); }
  return P.outlined(OUT);
}

// ---------- photo-booth portrait (80x80) ----------
function portrait(o) {
  const yaw = o.yaw || 0, pitch = o.pitch || 0, ex = o.expr || 'neutral';
  const P = new Pix(80, 80);
  const s = o.spec || {};
  const skin = hex(SKIN[s.skin || 'tan']), skinS = mul(skin, 0.82), skinD = mul(skin, 0.64), skinL = mix(skin, '#fff4e0', 0.22);
  const hair = hex(s.hair || '#2b2420'), hairL = mix(hair, '#ffffff', 0.2);
  if (o.bg !== false) {
    const bg = hex(o.bgCol || '#8ea4b5');
    for (let y = 0; y < 80; y++) for (let x = 0; x < 80; x++) {
      const f = Math.sin(x * 0.33 + Math.sin(y * 0.05 + x * 0.04) * 2.2) + (y < 6 ? Math.sin(x * 0.6) * 0.6 : 0);
      P.set(x, y, f > 0.7 ? mix(bg, '#ffffff', 0.1) : f < -0.6 ? mul(bg, 0.86) : bg);
    }
  }
  const body = new Pix(80, 80);
  // shoulders + coat
  const coat = hex(s.coat || '#8a7a55'), coatS = mul(coat, 0.78), coatL = mix(coat, '#ffffff', 0.12);
  body.ell(40 + yaw * 0.5, 88, 38, 24, coat);
  for (let y = 64; y < 80; y++) for (let x = 0; x < 80; x++) if (body.get(x, y) && x > 54) body.set(x, y, coatS);
  // shirt V + collar
  for (let y = 62; y < 80; y++) { const w = Math.max(0, 9 - (y - 62) * 0.55); body.hl(Math.round(40 - w), Math.round(40 + w), y, s.shirt || '#d4cbb2'); }
  body.line(30, 63, 37, 79, coatL); body.line(29, 63, 36, 79, coatL); body.line(50, 63, 43, 79, coatS); body.line(51, 63, 44, 79, coatS);
  body.line(28, 62, 22, 72, coatL); body.line(52, 62, 58, 72, coatS);
  // neck
  const ncx = 40 + yaw * 1.5;
  body.rect(Math.round(ncx - 6), 50, 12, 15, skinS); body.rect(Math.round(ncx + 2), 50, 4, 15, skinD);
  body.hl(Math.round(ncx - 6), Math.round(ncx + 5), 50 + Math.max(0, pitch) * 2, skinD);
  for (let y = 62; y < 66; y++) { const w = Math.max(0, 7 - (y - 62) * 1.6); body.hl(Math.round(40 - w), Math.round(40 + w), y, skinS); }

  // head
  const hcx = 40 + yaw * 2, hcy = 35 + pitch * 1.5, rx = 15, ry = 19;
  const H = new Pix(80, 80);
  for (let y = Math.floor(hcy - ry); y <= hcy + ry; y++) {
    const dy = (y + 0.5 - hcy) / ry; if (Math.abs(dy) > 1) continue;
    let w = rx * Math.sqrt(1 - dy * dy); if (dy > 0.15) w *= 1 - 0.38 * Math.pow((dy - 0.15) / 0.85, 1.4);
    const cxo = dy > 0.2 ? yaw * 2 * (dy - 0.2) : 0;
    for (let x = Math.floor(hcx - w + cxo); x <= hcx + w + cxo; x++) H.set(x, y, skin);
  }
  // ears
  const fx = hcx + yaw * 6, fy = hcy + 2 + pitch * 4, eyeY = Math.round(fy - 2);
  const earY = Math.round(eyeY + 1 - pitch * 2);
  const drawEar = (x0, dir, wide) => { for (let j = 0; j < 9; j++) { const w = (j === 0 || j === 8) ? 1 : wide; for (let i = 0; i < w; i++) H.set(x0 + dir * i, earY + j, j > 2 && j < 7 && i === w - 1 ? skinD : skinS); } };
  if (yaw <= 0) drawEar(Math.round(hcx + rx), 1, yaw < 0 ? 4 : 3);
  if (yaw >= 0) drawEar(Math.round(hcx - rx - 1), -1, yaw > 0 ? 4 : 3);
  // face shading: shadow side right
  const shadeX = hcx + rx * 0.45 + yaw * 3;
  for (let y = 0; y < 80; y++) for (let x = 0; x < 80; x++) { const c = H.get(x, y); if (c && c === skin) { if (x > shadeX + 3) H.set(x, y, skinD); else if (x > shadeX) H.set(x, y, skinS); else if (x < hcx - rx * 0.55 && y < hcy) H.set(x, y, skinL); } }
  // jaw shadow
  for (let x = 0; x < 80; x++) { for (let y = 79; y > 0; y--) { if (H.get(x, y)) { H.set(x, y, skinD); if (pitch <= 0) H.set(x, y - 1, skinS); break; } } }

  // features only land on head pixels, so a turned face never pokes past the jaw
  const setAny = H.set.bind(H), onHead = H.c.map(c => !!c);
  H.set = (x, y, c, em) => { x = Math.round(x); y = Math.round(y); if (x >= 0 && y >= 0 && x < 80 && y < 80 && onHead[y * 80 + x]) setAny(x, y, c, em); };
  // eyes
  const eL = { cx: Math.round(fx - 7 + (yaw < 0 ? 1 : 0)), w: yaw < 0 ? 5 : 7 };
  const eR = { cx: Math.round(fx + 7 - (yaw > 0 ? 1 : 0)), w: yaw > 0 ? 5 : 7 };
  const eyesState = o.eyes || (ex === 'closed' ? 'closed' : ex === 'tired' ? 'half' : 'open');
  const lidC = '#2a1d18', white = '#efe8dc', iris = s.iris || '#4a3324';
  const gaze = Math.round(yaw * 1 + (o.gaze || 0));
  const drawEye = (e) => {
    const x0 = e.cx - Math.floor(e.w / 2), x1 = x0 + e.w - 1;
    if (eyesState === 'closed') { H.hl(x0, x1, eyeY + 1, skinS); H.hl(x0 + 1, x1 - 1, eyeY + 2, lidC); H.set(x0, eyeY + 1, lidC); H.set(x1, eyeY + 1, lidC); return; }
    H.hl(x0 + 1, x1 - 1, eyeY, lidC);
    H.hl(x0, x1, eyeY + 1, white); H.hl(x0 + 1, x1 - 1, eyeY + 2, white);
    const ic = e.cx + gaze; H.rect(ic - 1, eyeY + 1, 3, 2, iris); H.set(ic, eyeY + 1, '#140e0b'); H.set(ic, eyeY + 2, '#140e0b'); H.set(ic - 1, eyeY + 1, '#ffffff');
    H.set(x0, eyeY + 1, lidC); H.set(x1, eyeY + 1, mix(lidC, skin, 0.4));
    H.hl(x0 + 1, x1 - 1, eyeY + 3, skinS);
    if (eyesState === 'half') { H.hl(x0, x1, eyeY + 1, skinS); H.hl(x0, x1, eyeY, skinS); H.hl(x0 + 1, x1 - 1, eyeY + 1, lidC); H.set(x0, eyeY + 2, lidC); }
    if (ex === 'tired') H.hl(x0 + 1, x1 - 1, eyeY + 4, mix(skinD, '#6a4a5a', 0.3));
  };
  drawEye(eL); drawEye(eR);
  // brows
  const bc = mul(hair, 0.9);
  const brow = (e, side) => {
    const x0 = e.cx - Math.floor(e.w / 2) - 1, x1 = x0 + e.w + 1; const by = eyeY - 4;
    for (let x = x0; x <= x1; x++) {
      const t = (x - x0) / (x1 - x0); const inner = side < 0 ? t : 1 - t; // 1 at inner end
      let off = 0;
      if (ex === 'angry') off = Math.round(inner * 2);
      else if (ex === 'nervous') off = -Math.round(inner * 2);
      else if (ex === 'annoyed') off = side < 0 ? 1 : Math.round(inner * 1);
      else if (ex === 'tired') off = Math.round((1 - inner) * 2);
      else if (ex === 'smile') off = -Math.round(Math.sin(t * Math.PI) * 1);
      H.set(x, by + off, bc); if (inner > 0.35) H.set(x, by + off + 1, bc);
    }
  };
  brow(eL, -1); brow(eR, 1);
  // nose
  const nx = Math.round(fx + yaw * 2);
  for (let y = eyeY + 2; y <= fy + 5; y++) H.set(nx + 2, y, skinS);
  H.hl(nx - 2, nx + 2, Math.round(fy + 6), skinS); H.set(nx - 2, Math.round(fy + 7), skinD); H.set(nx + 1, Math.round(fy + 7), skinD);
  H.set(nx - 1, Math.round(fy + 4), skinL); H.set(nx + 3, Math.round(fy + 6), skinD);
  if (pitch < 0) { H.hl(nx - 2, nx - 1, Math.round(fy + 7), '#3a2420'); H.hl(nx + 1, nx + 2, Math.round(fy + 7), '#3a2420'); }
  // mouth
  const my = Math.round(fy + 11 - (pitch > 0 ? 1 : 0)), mx = Math.round(fx + yaw * 1);
  const lip = mix(skinD, '#7a2e2a', 0.55), lipL = mix(skin, '#b0604f', 0.4);
  if (ex === 'smile') { H.hl(mx - 4, mx + 4, my, '#efe9da'); H.hl(mx - 4, mx + 4, my - 1, lip); H.set(mx - 5, my - 2, lip); H.set(mx + 5, my - 2, lip); H.hl(mx - 3, mx + 3, my + 1, lip); H.hl(mx - 2, mx + 2, my + 2, lipL); }
  else if (ex === 'angry') { H.hl(mx - 4, mx + 4, my, lip); H.set(mx - 5, my + 1, lip); H.set(mx + 5, my + 1, lip); H.hl(mx - 2, mx + 2, my + 2, skinS); }
  else if (ex === 'annoyed') { H.hl(mx - 3, mx + 3, my, lip); H.set(mx + 4, my + 1, lip); H.hl(mx - 2, mx + 2, my + 1, lipL); }
  else if (ex === 'nervous') { for (let x = mx - 3; x <= mx + 3; x++) H.set(x, my + ((x & 1) ? 0 : 1), lip); H.hl(mx - 2, mx + 2, my + 2, lipL); }
  else if (ex === 'tired') { H.hl(mx - 3, mx + 3, my, lip); H.rect(mx - 1, my + 1, 3, 1, '#3a1e1a'); H.hl(mx - 2, mx + 2, my + 2, lipL); }
  else { H.hl(mx - 4, mx + 4, my, lip); H.hl(mx - 3, mx + 3, my + 1, lipL); }
  // stubble
  if (s.stubble) for (let y = my - 1; y < 80; y++) for (let x = 0; x < 80; x++) { const c = H.get(x, y); if (c && (x + y) % 2 === 0 && (c === skin || c === skinS || c === skinD) && !(y === my && Math.abs(x - mx) < 5)) H.set(x, y, mix(c, hair, 0.22)); }
  if (s.mustache) { H.hl(mx - 5, mx + 5, my - 2, hair); H.hl(mx - 4, mx + 4, my - 3, hair); H.set(mx - 6, my - 1, hair); H.set(mx + 6, my - 1, hair); }
  H.set = setAny;
  // hair cap
  const hairline = hcy - ry * 0.42 + pitch * -3 + (pitch > 0 ? 4 : 0);
  const hx = hcx - yaw * 1.5;
  for (let y = Math.floor(hcy - ry - 3); y < hcy + 4; y++) for (let x = 0; x < 80; x++) {
    const dx = (x + 0.5 - hx) / (rx + 2), dy = (y + 0.5 - (hcy - 1)) / (ry + 2.5);
    if (dx * dx + dy * dy > 1) continue;
    const fringe = Math.sin(x * 0.9) * 0.6 + (x - hx) * 0.12 * (yaw === 0 ? 1 : -yaw);
    const side = Math.abs(x - hcx) > rx - 3 && y < eyeY - 1 && ((x < hcx && yaw <= 0) || (x > hcx && yaw >= 0) || Math.abs(x - hcx) > rx - 1);
    if (y < hairline + fringe || side) {
      let c = hair; if (x < hx - 4 && y < hcy - 12) c = hairL; if (x > hx + 8) c = mul(hair, 0.8);
      if (s.gray && (((Math.sin(x * 12.9898 + y * 78.233) * 43758.5453) % 1 + 1) % 1) < 0.16) c = '#8f8a84';
      H.set(x, y, c);
    }
  }
  const all = new Pix(80, 80); all.blit(body, 0, 0); all.blit(H, 0, 0);
  P.blit(all.outlined('#1a1410'), 0, 0);
  return P;
}

module.exports = { person, portrait, SKIN };
