// Construit vehicles.json (une image par véhicule) à partir du VehicleCatalog.asset du jeu.
// La description est déduite de l'identifiant du véhicule ; à relancer si le catalogue change.
//   node tools/game-art/build-vehicles.mjs
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const asset = readFileSync(join(HERE, '..', '..', 'Assets', '_Project', 'ScriptableObjects', 'Vehicles', 'VehicleCatalog.asset'), 'utf8');
const ids = [...asset.matchAll(/^  - id: (\S+)/gm)].map(m => m[1]);

// Règles : première correspondance gagnante (de la plus précise à la plus générale).
const RULES = [
  [/pickup/, 'A light pick-up truck with an open cargo bed.'],
  [/van_occasion/, 'An old used panel van with a rounded roof and sliding side door, slightly dented and dusty, no separate cargo box.'],
  [/van_premium_elec/, 'A modern premium electric delivery van with a sleek aerodynamic body.'],
  [/utilitaire|fourgon_compact/, 'A compact white delivery van.'],
  [/fourgon_grand_volume|fourgon_xl/, 'A large high-roof cargo van.'],
  [/chassis_cabine/, 'A light chassis-cab truck with a small box body.'],
  [/camion_grue/, 'A rigid truck with a folding hydraulic loading crane mounted behind the cab and a flatbed.'],
  [/petit_porteur/, 'A small rigid box truck with two axles.'],
  [/porteur_urbain|porteur_distrib/, 'A medium rigid box truck with two axles for city distribution.'],
  [/regional|porteur_renforce|heavy_rigid/, 'A large rigid box truck with three axles.'],
  [/poids_lourd_premium/, 'A premium heavy rigid truck with four axles, a long box body and a high cab.'],
  [/poids_lourd/, 'A heavy rigid truck with three axles and a long box body.'],
  [/frigo_pharma/, 'A large refrigerated semi-trailer truck with a white insulated trailer and a cooling unit.'],
  [/frigo/, 'A refrigerated rigid truck with a white insulated box body and a cooling unit above the cab.'],
  [/benne_carriere/, 'A huge quarry dump truck with a massive tipper body.'],
  [/benne/, 'A tipper dump truck with a steel tipping body.'],
  [/citerne_alim/, 'A tanker truck with a polished stainless steel food tank.'],
  [/citerne_carburant/, 'A fuel tanker truck with a white aluminium tank.'],
  [/citerne_chimique/, 'A chemical tanker truck with a cylindrical tank and hazard warning plates.'],
  [/porte_conteneur_double/, 'A container truck pulling a long trailer carrying two shipping containers.'],
  [/porte_conteneur_40/, 'A container truck pulling a trailer carrying one 40-foot shipping container.'],
  [/porte_conteneur/, 'A container truck pulling a trailer carrying one 20-foot shipping container.'],
  [/semi_double/, 'A semi-trailer truck: tractor unit pulling a tall double-deck box trailer.'],
  [/semi_megatrailer/, 'A semi-trailer truck: tractor unit pulling a high-volume mega trailer.'],
  [/semi_tautliner/, 'A semi-trailer truck: tractor unit pulling a curtain-sider trailer.'],
  [/v8_heavy_duty/, 'A powerful American-style long-nose tractor unit with chrome exhaust stacks, pulling a box trailer.'],
  [/tracteur_high_end/, 'A premium modern tractor unit with a high cab, pulling a curtain-sider trailer.'],
  [/tracteur_longue/, 'A long-haul tractor unit with a sleeper cab, pulling a box trailer.'],
  [/tracteur_standard/, 'A standard tractor unit pulling a curtain-sider semi-trailer.'],
  [/convoi_eolien/, 'A heavy haulage truck pulling an extendable trailer carrying a giant wind turbine blade.'],
  [/convoi_90t|tracteur_convoi/, 'A heavy haulage tractor pulling a low-loader trailer carrying a large excavator.'],
  [/tracteur_lourd/, 'A heavy haulage road truck pulling a multi-axle flat low-loader trailer carrying a big grey industrial electrical transformer box.'],
  [/ultra_mover/, 'A massive heavy haulage tractor pulling a very long multi-axle trailer carrying a huge industrial machine.'],
  [/train_routier_outback/, 'An outback road train: a big long-nose road truck cab (not a farm tractor) pulling four long trailers in a row.'],
  [/train_routier_triple/, 'A road train: a tractor unit pulling three trailers in a row.'],
  [/train_routier/, 'A road train: a tractor unit pulling two trailers in a row.'],
  [/mega_convoi_titan/, 'A gigantic heavy haulage convoy: two tractor units pulling a self-propelled modular trailer carrying a huge refinery column.'],
  [/mega_convoi_industriel/, 'A gigantic heavy haulage convoy pulling a modular trailer with many axles carrying a huge industrial reactor.'],
  [/mega_convoi/, 'A heavy haulage truck pulling a long modular trailer with many axles carrying a huge horizontal grey industrial boiler tank, the whole convoy fills the image.'],
];

// Corrections après revue : graine changée (image ratée) ou miroir (camion tourné à droite).
const RESEED = { van_occasion: 7600, heavy_rigid_19t: 7601, porteur_renforce_18t: 7602, poids_lourd_32t: 7503, tracteur_lourd_150t: 7504,
  semi_tautliner_34t: 7605, train_routier_outback_160t: 7506, mega_convoi_300t: 7607, frigo_compact_12t: 7608 };
// Caisse blanche sur fond = taches au détourage : on la fait peindre en gris-bleu clair.
const TINTED_BOX = new Set(['heavy_rigid_19t', 'porteur_renforce_18t', 'semi_tautliner_34t', 'frigo_compact_12t']);
const FLIP = new Set(['porteur_distrib_12t', 'porteur_urbain_10t']);

const images = ids.map((id, i) => {
  const rule = RULES.find(([re]) => re.test(id));
  if (!rule) throw new Error('Pas de description pour ' + id);
  const desc = TINTED_BOX.has(id) ? rule[1] + ' The cargo box is painted light blue-grey with a dark blue stripe.' : rule[1];
  const img = { name: id, seed: RESEED[id] ?? 7000 + i, description: desc };
  if (FLIP.has(id)) img.flip = true;
  return img;
});

const cfg = {
  style: 'Flat vector illustration of a fictional vehicle, side view from the left, facing left, the whole vehicle visible and centred. '
    + 'Minimalist mobile game icon style, clean geometric shapes, solid flat colours, simple smooth front grille, '
    + 'white cab with a blue stripe, crisp outlines. Plain flat solid medium grey background.',
  negative: 'logo, emblem, badge, brand, lettering, text, license plate, watermark, photo, photorealistic, 3D render, people, driver, road, ground, landscape, shadow, multiple vehicles, cropped, blurry',
  aspect: '3:2',
  removeBackground: true,
  fit: { w: 512, h: 320, pad: 0.05, bottom: true },
  installDir: '../../Assets/Resources/UI/Vehicles',
  images,
};
writeFileSync(join(HERE, 'vehicles.json'), JSON.stringify(cfg, null, 2));
console.log(`${images.length} véhicules -> vehicles.json`);
