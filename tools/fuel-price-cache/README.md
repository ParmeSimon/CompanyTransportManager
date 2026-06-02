# Cache du prix du carburant (gratuit, sans serveur)

Le cours du Brent est récupéré **1×/jour** par GitHub Actions et publié dans `fuel.json`
sur la branche **`fuel-data`** de ce repo. Tous les joueurs lisent ce même fichier
→ prix identique pour tous, clé jamais embarquée dans l'app, **1 seul appel/jour** quel
que soit le nombre de joueurs (le quota gratuit 25/j suffit largement, à vie).

```
Alpha Vantage ──(1 appel/jour, GitHub Actions)──> branche fuel-data / fuel.json ──> tous les téléphones
```

Le workflow vit dans [`.github/workflows/update-fuel.yml`](../../.github/workflows/update-fuel.yml).
La branche `fuel-data` ne contient QUE `fuel.json` → la branche `main` (ton code) reste propre.

## Mise en place (≈ 3 min, une seule fois)

1. **Pousse** le workflow `.github/workflows/update-fuel.yml` sur `main` (commit normal).

2. **Ajoute la clé en secret** : repo → *Settings → Secrets and variables → Actions →
   New repository secret* :
   - Name : `ALPHAVANTAGE_KEY`
   - Secret : ta clé gratuite (https://www.alphavantage.co/support/#api-key)

3. **Lance-le une fois** : onglet *Actions → Update fuel price → Run workflow*.
   Une branche `fuel-data` est créée avec un `fuel.json` du type :
   ```json
   { "date": "2026-05-26", "brent": [102.75, 106.9, 105.84, ...] }
   ```

4. **C'est déjà branché** dans l'app : `RealFuelMarket.CacheUrl` pointe sur
   `https://raw.githubusercontent.com/ParmeSimon/CompanyTransportManager/fuel-data/fuel.json`.

Ensuite le cron tourne tous les jours à 06:00 UTC. Si le fichier est injoignable
(repo passé privé, panne…), le jeu retombe automatiquement sur le marché simulé.

## Important
- ⚠️ **Le repo doit rester PUBLIC** : sinon `raw.githubusercontent.com` exige un token et
  l'app ne pourra pas lire `fuel.json`.
- La clé Alpha Vantage n'est **jamais** dans l'app ni dans `fuel.json` — uniquement dans
  les Secrets GitHub.
- `raw.githubusercontent.com` est servi via le CDN Fastly → encaisse un très gros trafic.
