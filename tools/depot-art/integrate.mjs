// Génère les bâtiments du dépôt DIRECTEMENT DANS la carte (intégration réaliste) :
//   1) découpe la zone de l'emplacement dans depotMap.png,
//   2) Stable Diffusion « image → image » y peint le bâtiment (même angle, lumière, style),
//   3) recolle le morceau : couleurs alignées sur la carte + bords fondus.
//
//   node tools/depot-art/integrate.mjs                 génère ce qui manque dans integrated/
//   node tools/depot-art/integrate.mjs --only hub_lvl2,office_lvl1 --force
//   node tools/depot-art/integrate.mjs --repatch       refait seulement l'étape 3 (sans appel AWS)
//   node tools/depot-art/integrate.mjs --install       copie integrated/*.png dans Assets/Resources/UI/WareHouse/
//
// Descriptions, graines et emplacements : integrate.json. Clé : bedrock-key.txt (ignoré par git).
import { readFileSync, existsSync, mkdirSync, copyFileSync, readdirSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';

const HERE = dirname(fileURLToPath(import.meta.url));
const WORK = join(HERE, 'inpaint');       // découpes + images générées brutes
const OUT = join(HERE, 'integrated');     // visuels finaux prêts pour le jeu
const INSTALL_DIR = join(HERE, '..', '..', 'Assets', 'Resources', 'UI', 'WareHouse');
const cfg = JSON.parse(readFileSync(join(HERE, 'integrate.json'), 'utf8'));

const args = process.argv.slice(2);
const flag = n => args.includes(n);
const option = n => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : null; };

const ps = (...a) => execFileSync('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', join(HERE, 'inpaint.ps1'), ...a],
  { encoding: 'utf8', cwd: HERE, stdio: ['ignore', 'pipe', 'pipe'] }).trim();

// Découpe d'un emplacement (une fois) → { file, w, h } (taille exacte de l'emplacement en px de carte).
const slotCache = {};
function slotCrop(slot) {
  if (slotCache[slot]) return slotCache[slot];
  const s = cfg.slots[slot];
  const file = join(WORK, `crop_${slot}.png`);
  const msg = ps('-Mode', 'crop', '-Map', join(HERE, cfg.map), '-Cx', String(s.cx), '-Cy', String(s.cy),
    '-W', String(cfg.slotWidth), '-Aspect', String(cfg.slotAspect), '-Scale', '3', '-Out', file);
  const m = msg.match(/emplacement ([\d.]+)x([\d.]+)/);
  if (!m) throw new Error('découpe : ' + msg);
  return (slotCache[slot] = { file, w: m[1], h: m[2] });
}

function patch(item) {
  const crop = slotCrop(item.slot);
  const out = join(OUT, `${item.name}.png`);
  rmSync(out, { force: true });
  return ps('-Mode', 'patch', '-Gen', join(WORK, `gen_${item.name}.png`), '-Crop', crop.file, '-Out', out,
    '-Feather', '0.22', '-Scale', '2', '-SlotW', crop.w, '-SlotH', crop.h);
}

function main() {
  mkdirSync(WORK, { recursive: true });
  mkdirSync(OUT, { recursive: true });

  if (flag('--install')) {
    const files = readdirSync(OUT).filter(f => f.endsWith('.png'));
    for (const f of files) copyFileSync(join(OUT, f), join(INSTALL_DIR, f));
    console.log(`${files.length} visuel(s) copié(s) dans ${INSTALL_DIR}`);
    return;
  }

  const only = option('--only')?.split(',').map(s => s.trim());
  let items = cfg.images.filter(i => !only || only.includes(i.name));

  if (flag('--repatch')) {
    for (const i of items) if (existsSync(join(WORK, `gen_${i.name}.png`))) console.log(i.name, ':', patch(i));
    return;
  }

  items = items.filter(i => flag('--force') || !existsSync(join(OUT, `${i.name}.png`)));
  let failed = 0;
  for (const i of items) {
    try {
      const crop = slotCrop(i.slot);
      process.stdout.write(`${i.name} : génération… `);
      execFileSync('node', [join(HERE, 'img2img.mjs'), crop.file, join(WORK, `gen_${i.name}.png`),
        String(i.strength ?? cfg.strength), String(i.seed), i.description], { stdio: ['ignore', 'pipe', 'pipe'] });
      process.stdout.write('raccord… ');
      console.log(patch(i));
    } catch (e) {
      failed++;
      console.log('ÉCHEC\n  ' + String(e.stderr || e.message).trim());
    }
  }
  console.log(`\n${items.length - failed}/${items.length} visuel(s) dans ${OUT}`);
  if (failed) process.exitCode = 1;
}

main();
