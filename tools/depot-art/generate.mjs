// Génère les images des bâtiments du dépôt avec Amazon Bedrock (modèles Stability AI).
//
//   node tools/depot-art/generate.mjs --dry-run          affiche ce qui serait généré, sans appeler AWS
//   node tools/depot-art/generate.mjs                    génère les images manquantes dans tools/depot-art/out/
//   node tools/depot-art/generate.mjs --only office_lvl1,fueltank_lvl2
//   node tools/depot-art/generate.mjs --force            regénère même si l'image existe déjà
//   node tools/depot-art/generate.mjs --renormalize      refait seulement le recadrage (cut/ → out/), sans appel AWS
//   node tools/depot-art/generate.mjs --install          copie out/*.png dans Assets/Resources/UI/WareHouse/
//
// Clé : variable d'environnement AWS_BEARER_TOKEN_BEDROCK (clé d'API Bedrock), ou à défaut
// le fichier tools/depot-art/bedrock-key.txt (ignoré par git).
// Les descriptions (une par image) sont dans prompts.json.
//
// Deux appels par image : 1) texte → image sur fond uni, 2) suppression du fond → PNG transparent.

import { readFileSync, writeFileSync, existsSync, mkdirSync, copyFileSync, readdirSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import http from 'node:http';
import { execFileSync } from 'node:child_process';

// Derrière un proxy d'entreprise (HTTPS_PROXY défini), le fetch de Node l'ignore par défaut :
// on l'active ici pour ne pas avoir à lancer le script avec NODE_USE_ENV_PROXY=1.
if (process.env.HTTPS_PROXY || process.env.https_proxy) http.setGlobalProxyFromEnv?.();

const HERE = dirname(fileURLToPath(import.meta.url));
const RAW_DIR = join(HERE, 'raw');   // image générée, fond uni (pour contrôle / retouche)
const CUT_DIR = join(HERE, 'cut');   // fond supprimé, taille brute du modèle
const OUT_DIR = join(HERE, 'out');   // image finale : fond transparent, recadrée sur l'emprise du hangar
const INSTALL_DIR = join(HERE, '..', '..', 'Assets', 'Resources', 'UI', 'WareHouse');
const REFERENCE = join(HERE, 'ref-canvas.png');   // canevas + emprise figés de TOUS les bâtiments (copie de l'ancien hub_lvl1)

const REGION = process.env.AWS_REGION || 'us-west-2';   // seule région des modèles Stability
const GENERATE_MODEL = 'stability.sd3-5-large-v1:0';
const REMOVE_BG_MODEL = 'us.stability.stable-image-remove-background-v1:0';
const ASPECT_RATIO = '5:4';   // même format que le canevas des bâtiments (1402 × 1122)

const args = process.argv.slice(2);
const flag = name => args.includes(name);
const option = name => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : null; };

function readKey() {
  if (process.env.AWS_BEARER_TOKEN_BEDROCK) return process.env.AWS_BEARER_TOKEN_BEDROCK.trim();
  const file = join(HERE, 'bedrock-key.txt');
  if (existsSync(file)) return readFileSync(file, 'utf8').trim();
  return null;
}

async function invoke(key, modelId, body) {
  const url = `https://bedrock-runtime.${REGION}.amazonaws.com/model/${encodeURIComponent(modelId)}/invoke`;
  const res = await fetch(url, {
    method: 'POST',
    headers: { Authorization: `Bearer ${key}`, 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${modelId} → HTTP ${res.status} : ${text.slice(0, 400)}`);
  const json = JSON.parse(text);
  const reason = json.finish_reasons?.find(r => r);   // null = succès
  if (reason) throw new Error(`${modelId} → refusé : ${reason}`);
  if (!json.images?.[0]) throw new Error(`${modelId} → réponse sans image : ${text.slice(0, 200)}`);
  return Buffer.from(json.images[0], 'base64');
}

// Recadre cut/<name>.png sur l'emprise du hangar de référence → out/<name>.png (aucun appel AWS).
// Garantit que tous les bâtiments ont la même taille, quelle que soit l'image générée.
function normalize(name) {
  const target = join(OUT_DIR, `${name}.png`);
  rmSync(target, { force: true });   // pas de vieille image prise pour un succès
  const flip = JSON.parse(readFileSync(join(HERE, 'prompts.json'), 'utf8')).images.find(i => i.name === name)?.flip;
  let out;
  try {
    out = execFileSync('powershell', [
      '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', join(HERE, 'normalize.ps1'),
      '-In', join(CUT_DIR, `${name}.png`), '-Out', target, '-Ref', REFERENCE,
      ...(flip ? ['-Flip'] : []),
    ], { encoding: 'utf8', cwd: HERE, stdio: ['ignore', 'pipe', 'pipe'] });
  } catch (e) {
    throw new Error(`recadrage de ${name} : ${(e.stderr || e.message).toString().trim()}`);
  }
  if (!existsSync(target)) throw new Error(`recadrage de ${name} : aucun fichier produit`);
  return out.trim();
}

// Le style peut contenir {building} (gabarit) : la description y est insérée ; sinon on concatène.
function buildPrompt(config, i) {
  if (i.prompt) return i.prompt;   // prompt complet propre à l'image (ex. fond de décor)
  return config.style.includes('{building}')
    ? config.style.replace('{building}', i.description)
    : `${i.description} ${config.style}`;
}

function renormalize() {
  mkdirSync(OUT_DIR, { recursive: true });
  const names = existsSync(CUT_DIR) ? readdirSync(CUT_DIR).filter(f => f.endsWith('.png')).map(f => f.slice(0, -4)) : [];
  if (!names.length) { console.log('Rien à recadrer : cut/ est vide.'); return; }
  for (const n of names) console.log(`${n} : ${normalize(n)}`);
}

function install() {
  if (!existsSync(OUT_DIR)) { console.log('Rien à installer : out/ est vide.'); return; }
  const files = readdirSync(OUT_DIR).filter(f => f.endsWith('.png'));
  for (const f of files) {
    copyFileSync(join(OUT_DIR, f), join(INSTALL_DIR, f));
    console.log(`installé  ${f}`);
  }
  console.log(`${files.length} image(s) copiée(s) dans ${INSTALL_DIR}`);
}

async function main() {
  if (flag('--install')) return install();
  if (flag('--renormalize')) return renormalize();

  const config = JSON.parse(readFileSync(join(HERE, 'prompts.json'), 'utf8'));
  const only = option('--only')?.split(',').map(s => s.trim());
  const unknown = only?.filter(n => !config.images.some(i => i.name === n)) ?? [];
  if (unknown.length) throw new Error(`Nom(s) inconnu(s) dans --only : ${unknown.join(', ')}`);

  const todo = config.images
    .filter(i => !only || only.includes(i.name))
    .filter(i => flag('--force') || !existsSync(join(OUT_DIR, `${i.name}.png`)));

  if (todo.length === 0) { console.log('Tout est déjà généré (utilise --force pour refaire).'); return; }

  if (flag('--dry-run')) {
    for (const i of todo) console.log(`\n[${i.name}]  seed ${i.seed}\n${buildPrompt(config, i)}`);
    console.log(`\n${todo.length} image(s), 2 appels Bedrock chacune (${GENERATE_MODEL}, puis ${REMOVE_BG_MODEL}), région ${REGION}.`);
    return;
  }

  const key = readKey();
  if (!key) throw new Error('Clé absente : définis AWS_BEARER_TOKEN_BEDROCK ou crée tools/depot-art/bedrock-key.txt');

  mkdirSync(RAW_DIR, { recursive: true });
  mkdirSync(CUT_DIR, { recursive: true });
  mkdirSync(OUT_DIR, { recursive: true });

  let failed = 0;
  for (const i of todo) {
    try {
      process.stdout.write(`${i.name} : génération… `);
      const raw = await invoke(key, GENERATE_MODEL, {
        prompt: buildPrompt(config, i),
        negative_prompt: config.negative,
        mode: 'text-to-image',
        aspect_ratio: i.aspect || ASPECT_RATIO,
        seed: i.seed,
        output_format: 'png',
      });
      writeFileSync(join(RAW_DIR, `${i.name}.png`), raw);

      // Fond de décor (sol du dépôt) : image pleine, ni détourage ni recadrage.
      if (i.background) {
        writeFileSync(join(OUT_DIR, `${i.name}.png`), raw);
        console.log('fond plein ok');
        continue;
      }

      process.stdout.write('détourage… ');
      const cut = await invoke(key, REMOVE_BG_MODEL, { image: raw.toString('base64'), output_format: 'png' });
      writeFileSync(join(CUT_DIR, `${i.name}.png`), cut);

      process.stdout.write('recadrage… ');
      console.log(normalize(i.name));
    } catch (e) {
      failed++;
      console.log(`ÉCHEC\n  ${e.message}`);
    }
  }
  console.log(`\n${todo.length - failed}/${todo.length} image(s) dans ${OUT_DIR}`);
  if (failed) process.exitCode = 1;
}

main().catch(e => { console.error(e.message); process.exitCode = 1; });
