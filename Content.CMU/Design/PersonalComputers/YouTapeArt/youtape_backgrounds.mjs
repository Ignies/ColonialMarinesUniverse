/*
YouTape video backgrounds: the ExtraNet Editor's own Banner Studio backgrounds, rendered headlessly as PNG.

Run from anywhere:  node youtape_backgrounds.mjs [name ...]

Writes bg_<name>.png (240x135, one file pixel per art pixel; the game shows them at 2x) and attributions.yml into
Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Retro/YouTape/Backgrounds/. With names, only those
backgrounds are drawn (attributions.yml is still written for the whole list).

The pictures are not drawn here. Each one is a background module from the editor checkout (the
Ignies/ExtraNet-Editor site, js/banner/backgrounds/): the scenery, stages and pattern backgrounds the
studio already had, and the YouTape sets made for this (scenery/newsdesk.js, newsroom.js, weathermap.js,
orbit.js, colonyplaza.js, landingpad.js, galley.js, bunk.js; stages/commercial.js, testcard.js, standby.js), which
are registered in index.js so the studio has them too. Change a picture in its module, then run this again.

  EDITOR   the editor checkout, C:/cmu/ExtraNet-Editor unless YOUTAPE_EDITOR is set.
  JOBS     every background: its file name, its module, the params that differ from the module's defaults, and a
           fixed seed, so a run always draws the same pictures.

Every module draws through _kit.js paint(), which fills a pixel Grid, puts it into an OffscreenCanvas with
putImageData() and scales it onto the canvas with drawImage() and no smoothing. That is all the canvas a background
uses, so this script stands in for it with a few lines (ImageData, OffscreenCanvas, a 2D context with putImageData
and nearest-neighbour drawImage) and its own PNG writer, and needs no npm packages and nothing installed in either
repo. A module that reached for any other canvas call would throw here and is reported, not drawn.

Pixel size is 1: at 135 rows _kit.js gives a 240x135 grid (grid.u about 2), so every file pixel is an art pixel.
*/
import { deflateSync, crc32 } from 'node:zlib';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, '../../../..');
const OUT = join(REPO, 'Content.CMU/Resources/Textures/CMU14/PersonalComputers/Sites/Retro/YouTape/Backgrounds');
const EDITOR = process.env.YOUTAPE_EDITOR || 'C:/cmu/ExtraNet-Editor';
const MODULES = join(EDITOR, 'js/banner/backgrounds');
const GENERATOR = 'Content.CMU/Design/PersonalComputers/YouTapeArt/youtape_backgrounds.mjs';
const EDITOR_SOURCE = 'https://github.com/Ignies/ExtraNet-Editor';
const W = 240;
const H = 135;

// name, module (relative to js/banner/backgrounds), params over the defaults, seed.
const JOBS = [
  // Scenery the studio already had.
  ['skyline', 'scenery/skyline.js', { focus: 'right', time: 'night' }, 11],
  ['dunes', 'scenery/dunes.js', { focus: 'right', time: 'dusk' }, 12],
  ['jungle', 'scenery/jungle.js', { focus: 'right', time: 'day' }, 13],
  ['aurora', 'scenery/aurora.js', { focus: 'right', time: 'night' }, 14],
  ['shore', 'scenery/shore.js', { focus: 'right', time: 'dusk' }, 15],
  ['ridge', 'scenery/ridge.js', { focus: 'right', time: 'dusk' }, 16],
  ['neonstreet', 'scenery/neonstreet.js', { focus: 'right', time: 'night' }, 17],
  ['stationwindow', 'scenery/stationwindow.js', { focus: 'right', time: 'night' }, 18],
  ['hangar', 'scenery/hangar.js', { focus: 'right', time: 'night' }, 19],
  ['swamp', 'scenery/swamp.js', { focus: 'right', time: 'dusk' }, 20],
  // Stages the studio already had, with the stage as wide as _stage.js layout() allows (its slider stops at 0.7).
  ['tvstudio', 'stages/tvstudio.js', { width: 0.9 }, 21],
  ['desk', 'stages/desk.js', { width: 0.9 }, 22],
  ['curtain', 'stages/curtain.js', { width: 0.9 }, 23],
  ['workbench', 'stages/workbench.js', { width: 0.9 }, 24],
  ['locker', 'stages/locker.js', { width: 0.9 }, 25],
  ['shelf', 'stages/shelf.js', { width: 0.9 }, 26],
  ['corkboard', 'stages/corkboard.js', { width: 0.9 }, 27],
  // Patterns the studio already had.
  ['synthwave', 'synthwave.js', {}, 31],
  ['starfield', 'starfield.js', {}, 32],
  ['nebula', 'nebula.js', {}, 33],
  ['static', 'static.js', {}, 34],
  ['corporate', 'corporate.js', { side: 'all' }, 35],
  ['sunset', 'sunset.js', {}, 36],
  // The YouTape sets.
  ['news_desk', 'scenery/newsdesk.js', { focus: 'right' }, 41],
  ['news_network', 'scenery/newsroom.js', {}, 42],
  ['weather_map', 'scenery/weathermap.js', { focus: 'right' }, 43],
  ['weather_orbit', 'scenery/orbit.js', { focus: 'right' }, 44],
  ['field_colony', 'scenery/colonyplaza.js', { focus: 'center', time: 'dusk' }, 45],
  ['field_landing', 'scenery/landingpad.js', { focus: 'right', time: 'night' }, 46],
  ['galley', 'scenery/galley.js', { focus: 'center' }, 47],
  ['bunk', 'scenery/bunk.js', { focus: 'right' }, 48],
  ['ad_bright_1', 'stages/commercial.js', { style: 'sunburst', a: '#FFD02A', b: '#FF5A1E', floor: '#3A1060' }, 51],
  ['ad_bright_2', 'stages/commercial.js', { style: 'checker', a: '#2AE0FF', b: '#FF2AA8', floor: '#141048' }, 52],
  ['ad_bright_3', 'stages/commercial.js', { style: 'spotlight', a: '#7A2AFF', b: '#FFE85A', floor: '#1A0A30' }, 53],
  ['ad_bright_4', 'stages/commercial.js', { style: 'starburst', a: '#3AFF6A', b: '#FFFFFF', floor: '#0A3A2A' }, 54],
  ['test_card', 'stages/testcard.js', {}, 55],
  ['stand_by', 'stages/standby.js', {}, 56],
];

// -- The canvas stand-in ---------------------------------------------------------------------------------------------

class ImageData {
  constructor(data, width, height) {
    this.data = data;
    this.width = width;
    this.height = height;
  }
}

/** A canvas that only remembers the last putImageData(): what _kit.js paint() uses as its scratch surface. */
class OffscreenCanvas {
  constructor(width, height) {
    this.width = width;
    this.height = height;
    this.image = null;
    const self = this;
    this.ctx = {
      putImageData(img) {
        self.image = img;
      },
    };
  }

  getContext() {
    return this.ctx;
  }
}

globalThis.ImageData = ImageData;
globalThis.OffscreenCanvas = OffscreenCanvas;

/** The output canvas: RGBA pixels and a nearest-neighbour drawImage() of a scratch surface. */
function outputContext(width, height) {
  const data = new Uint8ClampedArray(width * height * 4);
  return {
    data,
    imageSmoothingEnabled: true,
    drawImage(src, sx, sy, sw, sh, dx, dy, dw, dh) {
      const img = src.image;
      for (let y = 0; y < height; y++) {
        const ty = (y + 0.5 - dy) / dh;
        if (ty < 0 || ty >= 1) continue;
        const py = Math.floor(sy + ty * sh);
        for (let x = 0; x < width; x++) {
          const tx = (x + 0.5 - dx) / dw;
          if (tx < 0 || tx >= 1) continue;
          const px = Math.floor(sx + tx * sw);
          const i = (py * img.width + px) * 4;
          const o = (y * width + x) * 4;
          data[o] = img.data[i];
          data[o + 1] = img.data[i + 1];
          data[o + 2] = img.data[i + 2];
          data[o + 3] = img.data[i + 3];
        }
      }
    },
  };
}

// -- PNG -------------------------------------------------------------------------------------------------------------

function chunk(type, body) {
  const head = Buffer.alloc(8);
  head.writeUInt32BE(body.length, 0);
  head.write(type, 4, 'ascii');
  const tail = Buffer.alloc(4);
  tail.writeUInt32BE(crc32(Buffer.concat([head.subarray(4), body])) >>> 0, 0);
  return Buffer.concat([head, body, tail]);
}

/** An opaque 8-bit RGB PNG of RGBA pixels. */
function png(rgba, width, height) {
  const raw = Buffer.alloc((width * 3 + 1) * height);
  for (let y = 0; y < height; y++) {
    const row = y * (width * 3 + 1);
    raw[row] = 0;
    for (let x = 0; x < width; x++) {
      const i = (y * width + x) * 4;
      raw[row + 1 + x * 3] = rgba[i];
      raw[row + 2 + x * 3] = rgba[i + 1];
      raw[row + 3 + x * 3] = rgba[i + 2];
    }
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;
  ihdr[9] = 2;
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

// -- Drawing ---------------------------------------------------------------------------------------------------------

async function render(module, params, seed) {
  const bg = (await import(pathToFileURL(join(MODULES, module)).href)).default;
  const p = {};
  for (const q of bg.params) p[q.key] = q.default;
  Object.assign(p, params, { pixel: 1 });
  const ctx = outputContext(W, H);
  bg.draw(ctx, W, H, p, seed);
  return { bg, data: ctx.data };
}

function attributions(done) {
  const lines = [];
  for (const [name, module, , , bg] of done) {
    lines.push(`- files: ["bg_${name}.png"]`);
    lines.push('  license: "CC-BY-SA-3.0"');
    lines.push(`  copyright: "YouTape video background drawn for CMU: the ExtraNet Editor Banner Studio background '${bg.name}' (js/banner/backgrounds/${module}, ${EDITOR_SOURCE}), rendered at 240x135 by ${GENERATOR}"`);
    lines.push('  source: "https://github.com/Ignies/ColonialMarinesUniverse"');
    lines.push('');
  }
  return lines.join('\n');
}

const only = new Set(process.argv.slice(2));
mkdirSync(OUT, { recursive: true });
const done = [];
let failed = 0;
for (const [name, module, params, seed] of JOBS) {
  try {
    const { bg, data } = await render(module, params, seed);
    done.push([name, module, params, seed, bg]);
    if (only.size && !only.has(name)) continue;
    writeFileSync(join(OUT, `bg_${name}.png`), png(data, W, H));
    console.log(`bg_${name}.png  <- ${module}`);
  } catch (e) {
    failed++;
    console.error(`skipped ${name} (${module}): ${e.message}`);
  }
}
writeFileSync(join(OUT, 'attributions.yml'), attributions(done), 'utf8');
console.log(`${done.length} backgrounds, ${failed} skipped, into ${OUT}`);
