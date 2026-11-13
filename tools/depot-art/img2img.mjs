// Image → image avec Stable Diffusion 3.5 Large (Bedrock) : peint un bâtiment sur un
// morceau découpé de la carte du dépôt, pour qu'il ait le même style, angle et éclairage.
//
//   node img2img.mjs <entree.png> <sortie.png> <force 0..1> <seed> "<description>"
//
// Clé : AWS_BEARER_TOKEN_BEDROCK ou bedrock-key.txt (ignoré par git). Région us-west-2.
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import http from 'node:http';

if (process.env.HTTPS_PROXY || process.env.https_proxy) http.setGlobalProxyFromEnv?.();

const HERE = dirname(fileURLToPath(import.meta.url));
const [input, output, strengthArg, seedArg, description] = process.argv.slice(2);
if (!input || !output || !description) {
  console.error('usage: node img2img.mjs <entree.png> <sortie.png> <force> <seed> "<description>"');
  process.exit(1);
}

const key = process.env.AWS_BEARER_TOKEN_BEDROCK?.trim()
  || (existsSync(join(HERE, 'bedrock-key.txt')) ? readFileSync(join(HERE, 'bedrock-key.txt'), 'utf8').trim() : null);
if (!key) { console.error('Clé Bedrock absente'); process.exit(1); }

const STYLE = 'Isometric aerial view, same camera angle, perspective, lighting and painted game-art style as the input image. '
  + 'The building stands on the asphalt parking lot of the image, with a soft shadow on the ground, sharp details. '
  + 'Grey concrete walls and blue painted steel trim.';
const NEGATIVE = 'text, letters, logo, watermark, people, cars, trucks, blurry, photo frame, border, white background, floating, cropped building';

const res = await fetch('https://bedrock-runtime.us-west-2.amazonaws.com/model/stability.sd3-5-large-v1%3A0/invoke', {
  method: 'POST',
  headers: { Authorization: `Bearer ${key}`, 'Content-Type': 'application/json', Accept: 'application/json' },
  body: JSON.stringify({
    mode: 'image-to-image',
    image: readFileSync(input).toString('base64'),
    strength: Number(strengthArg ?? 0.75),
    seed: Number(seedArg ?? 0),
    prompt: `${description} ${STYLE}`,
    negative_prompt: NEGATIVE,
    output_format: 'png',
  }),
});
const text = await res.text();
if (!res.ok) { console.error(`HTTP ${res.status}: ${text.slice(0, 400)}`); process.exit(1); }
const json = JSON.parse(text);
const reason = json.finish_reasons?.find(r => r);
if (reason || !json.images?.[0]) { console.error('refusé : ' + (reason || text.slice(0, 200))); process.exit(1); }
writeFileSync(output, Buffer.from(json.images[0], 'base64'));
console.log('ok ->', output);
