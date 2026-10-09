// Renders every image on the visual-direction canvas into ./out (pre-scaled, nearest neighbour).
// Usage: node Tools/ArtGen/build.js
const fs = require('fs'), path = require('path');
const { Pix, save } = require('./lib');
const { drawShop, lights } = require('./scene');
const { person, portrait } = require('./people');
const C = require('./cast');
const A = require('./assets');

const OUT = path.join(__dirname, 'out');
fs.mkdirSync(OUT, { recursive: true });
const o = f => path.join(OUT, f);

// scenes
const night = drawShop({ tier: 1, time: 'night' });
save(o('shop-night-x4.png'), night, 4, lights({ tier: 1, time: 'night' }));
save(o('shop-night-x2.png'), night, 2, lights({ tier: 1, time: 'night' }));
save(o('shop-morning-x2.png'), drawShop({ tier: 1, time: 'morning' }), 2, lights({ tier: 1, time: 'morning' }));
save(o('shop-evening-x2.png'), drawShop({ tier: 1, time: 'evening' }), 2, lights({ tier: 1, time: 'evening' }));
save(o('shop-flash-x2.png'), night, 2, lights({ tier: 1, time: 'night', flash: true }));
save(o('shop-day100-x2.png'), drawShop({ tier: 3, time: 'night' }), 2, lights({ tier: 3, time: 'night' }));
save(o('shop-empty-x4.png'), drawShop({ tier: 1, time: 'night', people: false }), 4, lights({ tier: 1, time: 'night' }));
// capsule: a crop of the night scene, lights shifted with it
const capLights = lights({ tier: 1, time: 'night' });
capLights.lights = capLights.lights.map(l => ({ ...l, x: l.x - 70, y: l.y - 40 }));
save(o('capsule-x3.png'), night.crop(70, 40, 307, 143), 3, capLights);

// cast lineup
const keys = ['samir', 'nervous', 'wealthy', 'impatient', 'tired', 'student', 'drunk'];
const lineup = new Pix(keys.length * 40, 76);
keys.forEach((k, i) => { lineup.ell(i * 40 + 20, 72, 12, 2, '#1f1b17'); lineup.blit(person(C[k]), i * 40 + 2, 2); });
save(o('cast-x5.png'), lineup, 5);

// modular kit strips (head crops)
const base = { skin: 'tan', hair: '#2a1d14', hairStyle: 'short', top: 'tshirt', topCol: '#6a6a62', mood: 'neutral' };
const strip = specs => { const S = new Pix(specs.length * 28, 28); specs.forEach((sp, i) => S.blit(person({ ...base, ...sp }).crop(4, 0, 28, 28), i * 28, 0)); return S; };
save(o('kit-hair-x4.png'), strip([{ hairStyle: 'short' }, { hairStyle: 'neat' }, { hairStyle: 'messy' }, { hairStyle: 'curly' }, { hairStyle: 'long', hair: '#5a3a24' }, { hairStyle: 'bun', hair: '#7a4a2a' }, { hairStyle: 'bald' }, { hairStyle: 'flattop' }]), 4);
save(o('kit-face-x4.png'), strip([{ facial: null }, { facial: 'stubble' }, { facial: 'mustache' }, { facial: 'bigmustache' }, { facial: 'beard' }, { facial: 'bigbeard' }]), 4);
save(o('kit-acc-x4.png'), strip([{ glasses: 'glasses' }, { glasses: 'sun' }, { hat: 'cap', hatCol: '#7a2a2a' }, { hat: 'beanie', hatCol: '#4a5a4a' }, { hat: 'fedora', hatCol: '#3a3530' }, { hat: 'flatcap', hatCol: '#5a5040' }, { headphones: '#d8d2c4' }, { scarf: '#9a4a3a' }]), 4);
save(o('kit-skin-x4.png'), strip(['pale', 'light', 'tan', 'olive', 'brown', 'deep'].map(s => ({ skin: s }))), 4);

// photo-booth portraits
const ps = { skin: 'tan', stubble: true, gray: true };
save(o('portrait-hero-x7.png'), portrait({ spec: ps, yaw: 0, pitch: 0, expr: 'neutral' }), 7);
const poses = new Pix(3 * 80 + 2 * 4, 3 * 80 + 2 * 4);
[-1, 0, 1].forEach((p, j) => [-1, 0, 1].forEach((y, i) => poses.blit(portrait({ yaw: y, pitch: p, spec: ps }), i * 84, j * 84)));
save(o('portrait-poses-x2.png'), poses, 2);
const exprs = ['neutral', 'smile', 'annoyed', 'angry', 'nervous', 'tired', 'closed'];
const faces = new Pix(exprs.length * 56 + (exprs.length - 1) * 4, 56);
exprs.forEach((e, i) => faces.blit(portrait({ expr: e, spec: ps }).crop(12, 10, 56, 56), i * 60, 0));
save(o('portrait-expr-x2.png'), faces, 2);
const blink = new Pix(4 * 36 + 3 * 4, 14);
['open', 'half', 'closed', 'open'].forEach((s, i) => blink.blit(portrait({ eyes: s, spec: ps, gaze: i === 3 ? -1 : 0 }).crop(22, 26, 36, 14), i * 40, 0));
save(o('portrait-blink-x4.png'), blink, 4);

// products, icons, flags, logos
save(o('products-x4.png'), A.products(), 4);
save(o('icons-x3.png'), A.icons(), 3);
save(o('flags-x4.png'), A.flags(), 4);
save(o('logo-a-x8.png'), A.logo('A'), 8);
save(o('logo-a-x3.png'), A.logo('A'), 3);
save(o('logo-a-x1.png'), A.logo('A'), 1);
save(o('logo-b-x5.png'), A.logo('B'), 5);
save(o('logo-c-x6.png'), A.logo('C'), 6);
save(o('logo-c-x2.png'), A.logo('C'), 2);

console.log(fs.readdirSync(OUT).length + ' images in ' + OUT);
