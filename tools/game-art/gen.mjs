// Générateur d'images de jeu (Amazon Bedrock, Stable Diffusion 3.5 Large) piloté par une liste JSON.
//
//   node tools/game-art/gen.mjs vehicles.json [--only a,b] [--force] [--dry-run]
//   node tools/game-art/gen.mjs vehicles.json --install     copie out/<liste>/*.png vers installDir
//
// Format de la liste : { "style", "negative", "aspect", "removeBackground", "fit": {w,h,pad,bottom},
//                        "installDir", "images": [ { "name", "seed", "description" } ] }
// Clé : AWS_BEARER_TOKEN_BEDROCK ou tools/depot-art/bedrock-key.txt (ignoré par git).
import { readFileSync, writeFileSync, existsSync, mkdirSync, copyFileSync, readdirSync, rmSync } from 'node:fs';
import { dirname, join, basename, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';
import http from 'node:http';

if (process.env.HTTPS_PROXY || process.env.https_proxy) http.setGlobalProxyFromEnv?.();

const HERE = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const listPath = resolve(HERE, args.find(a => a.endsWith('.json')) || '');
const flag = n => args.includes(n);
const option = n => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : null; };

const cfg = JSON.parse(readFileSync(listPath, 'utf8'));
const NAME = basename(listPath, '.json');
const RAW = join(HERE, 'raw', NAME), CUT = join(HERE, 'cut', NAME), OUT = join(HERE, 'out', NAME);

function readKey() {
  if (process.env.AWS_BEARER_TOKEN_BEDROCK) return process.env.AWS_BEARER_TOKEN_BEDROCK.trim();
  const f = join(HERE, '..', 'depot-art', 'bedrock-key.txt');
  return existsSync(f) ? readFileSync(f, 'utf8').trim() : null;
}

async function invoke(key, modelId, body) {
  const res = await fetch(`https://bedrock-runtime.us-west-2.amazonaws.com/model/${encodeURIComponent(modelId)}/invoke`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${key}`, 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${modelId} → HTTP ${res.status} : ${text.slice(0, 300)}`);
  const json = JSON.parse(text);
  const reason = json.finish_reasons?.find(r => r);
  if (reason) throw new Error(`${modelId} → refusé : ${reason}`);
  if (!json.images?.[0]) throw new Error(`${modelId} → pas d'image`);
  return Buffer.from(json.images[0], 'base64');
}

function fit(name) {
  const f = cfg.fit;
  const item = cfg.images.find(i => i.name === name);
  const out = join(OUT, `${name}.png`);
  rmSync(out, { force: true });
  execFileSync('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', join(HERE, 'fit.ps1'),
    '-In', join(CUT, `${name}.png`), '-Out', out, '-W', String(f.w), '-H', String(f.h),
    '-Pad', String(f.pad ?? 0.06), ...(f.bottom ? ['-Bottom'] : []), ...(item?.flip ? ['-Flip'] : [])], { stdio: ['ignore', 'pipe', 'pipe'] });
  if (!existsSync(out)) throw new Error('recadrage : aucun fichier');
}

async function main() {
  mkdirSync(RAW, { recursive: true }); mkdirSync(CUT, { recursive: true }); mkdirSync(OUT, { recursive: true });

  if (flag('--install')) {
    const dest = resolve(HERE, cfg.installDir);
    mkdirSync(dest, { recursive: true });
    const files = readdirSync(OUT).filter(f => f.endsWith('.png'));
    for (const f of files) copyFileSync(join(OUT, f), join(dest, f));
    console.log(`${files.length} image(s) copiée(s) dans ${dest}`);
    return;
  }
  if (flag('--refit')) {
    for (const f of readdirSync(CUT).filter(f => f.endsWith('.png'))) { fit(f.slice(0, -4)); console.log('ok', f); }
    return;
  }

  const only = option('--only')?.split(',').map(s => s.trim());
  const todo = cfg.images
    .filter(i => !only || only.includes(i.name))
    .filter(i => flag('--force') || !existsSync(join(OUT, `${i.name}.png`)));

  if (flag('--dry-run')) {
    for (const i of todo) console.log(`[${i.name}] ${i.description}`);
    console.log(`${todo.length} image(s), ${cfg.removeBackground ? 2 : 1} appel(s) chacune.`);
    return;
  }

  const key = readKey();
  if (!key) throw new Error('Clé Bedrock absente');
  let failed = 0;
  for (const i of todo) {
    try {
      process.stdout.write(`${i.name} : génération… `);
      const raw = await invoke(key, 'stability.sd3-5-large-v1:0', {
        prompt: `${i.description} ${cfg.style}`, negative_prompt: cfg.negative, mode: 'text-to-image',
        aspect_ratio: cfg.aspect, seed: i.seed, output_format: 'png',
      });
      writeFileSync(join(RAW, `${i.name}.png`), raw);
      let img = raw;
      if (cfg.removeBackground) {
        process.stdout.write('détourage… ');
        img = await invoke(key, 'us.stability.stable-image-remove-background-v1:0', { image: raw.toString('base64'), output_format: 'png' });
      }
      writeFileSync(join(CUT, `${i.name}.png`), img);
      process.stdout.write('cadrage… ');
      fit(i.name);
      console.log('ok');
    } catch (e) {
      failed++;
      console.log('ÉCHEC\n  ' + String(e.stderr || e.message).trim());
    }
  }
  console.log(`\n${todo.length - failed}/${todo.length} image(s) dans ${OUT}`);
  if (failed) process.exitCode = 1;
}

main().catch(e => { console.error(e.message); process.exitCode = 1; });
