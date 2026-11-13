// Construit drivers.json : portraits de pilotes FICTIFS rangés par ORIGINE et GENRE, exactement
// comme les familles du jeu (Systems/Hr/DriverOrigins.cs). Fichiers : <origine>_<genre>_<n>.png
//   node tools/game-art/build-drivers.mjs            (liste)
//   node tools/game-art/build-drivers.mjs --migrate  (renomme une fois les anciens portraits fr/ger/est/mag)
import { readFileSync, writeFileSync, existsSync, renameSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const listPath = join(HERE, 'drivers.json');
const base = JSON.parse(readFileSync(listPath, 'utf8'));

// Anciens groupes (portraits déjà générés) → nouvelles familles.
const LEGACY = { fr: 'eu_west', ger: 'eu_north', est: 'eu_east', mag: 'africa_north' };

// origine, description du visage, nombre minimal de portraits par genre
const FAMILIES = {
  eu_west:      { who: 'Western European', min: 6 },
  eu_north:     { who: 'Northern European (Scandinavian or German)', min: 6 },
  eu_east:      { who: 'Eastern European', min: 6 },
  africa_north: { who: 'North African or Middle Eastern', min: 6 },
  africa_sub:   { who: 'Black West African or East African', min: 6 },
  asia_east:    { who: 'East Asian (Chinese, Japanese, Korean or Vietnamese)', min: 6 },
  asia_south:   { who: 'South Asian (Indian, Pakistani or Bangladeshi)', min: 6 },
};

const AGES = ['in his twenties', 'in his thirties', 'in his forties', 'in his fifties', 'in his sixties'];
const HAIR_M = ['short dark hair', 'a shaved head', 'curly black hair', 'grey hair', 'short hair', 'messy dark hair',
                'receding greying hair', 'a buzz cut', 'wavy hair', 'neatly combed hair'];
const FACE_M = ['clean-shaven', 'with light stubble', 'with a short beard', 'with a moustache', 'with a full beard', 'with a goatee'];
const HAIR_F = ['hair in a ponytail', 'short grey hair', 'long dark hair', 'a bob haircut', 'curly hair', 'hair tied back',
                'shoulder-length hair', 'a short pixie cut', 'braided hair'];
const EXTRA = ['', '', '', 'wearing glasses', '', '', 'wearing a cap', ''];

let state = 20261112;
const rnd = () => { state |= 0; state = (state + 0x6D2B79F5) | 0; let t = Math.imul(state ^ (state >>> 15), 1 | state);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
const pick = arr => arr[Math.floor(rnd() * arr.length)];

// 1) Reprend les portraits existants (renommés vers les nouvelles familles).
const images = [];
for (const img of base.images) {
  const m = img.name.match(/^([a-z]+)_([mf])_(\d+)$/);
  const fam = m && LEGACY[m[1]];
  const name = fam ? `${fam}_${m[2]}_${m[3]}` : img.name;
  images.push({ ...img, name });
}

// 2) Complète chaque famille × genre jusqu'au minimum.
let seed = 9000;
for (const [fam, f] of Object.entries(FAMILIES)) {
  for (const g of ['m', 'f']) {
    let n = images.filter(i => i.name.startsWith(`${fam}_${g}_`)).length;
    while (n < f.min) {
      n++;
      const age = pick(AGES);
      const who = g === 'm'
        ? `A ${f.who} man ${age} with ${pick(HAIR_M)}, ${pick(FACE_M)}`
        : `A ${f.who} woman ${age.replace('his', 'her')} with ${pick(HAIR_F)}`;
      const extra = pick(EXTRA);
      images.push({ name: `${fam}_${g}_${n}`, seed: seed++, description: `${who}${extra ? ', ' + extra : ''}.` });
    }
  }
}

// Renommage des fichiers déjà générés (une seule fois).
if (process.argv.includes('--migrate')) {
  for (const dir of ['raw', 'cut', 'out'].map(d => join(HERE, d, 'drivers'))) {
    for (const img of base.images) {
      const m = img.name.match(/^([a-z]+)_([mf])_(\d+)$/);
      const fam = m && LEGACY[m[1]];
      if (!fam) continue;
      const from = join(dir, `${img.name}.png`), to = join(dir, `${fam}_${m[2]}_${m[3]}.png`);
      if (existsSync(from)) renameSync(from, to);
    }
  }
  console.log('anciens portraits renommés');
}

base.images = images;
writeFileSync(listPath, JSON.stringify(base, null, 2));
const counts = {};
for (const i of images) { const k = i.name.replace(/_\d+$/, ''); counts[k] = (counts[k] || 0) + 1; }
console.log(`${images.length} portraits -> drivers.json`, counts);
