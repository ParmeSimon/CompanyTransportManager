# 🗺️ Feuille de route — CompanyTransportManager

> Bloc-note des modifs restantes. Cases à cocher = à faire. 
> Ordre conseillé en bas du fichier.

---

## ✅ Déjà fait (rappel)
- Contrats au **départ du dépôt** (livraison dépôt→ville / collecte ville→dépôt).
- **Dépôt = vraie position** du joueur (entrée « maison », plus de snap sur Rennes).
- **Tournées multi-arrêts** (skill `MultiStopContractsUnlocked`, dépôt en début OU fin).
- Popup contrat : **toutes les villes** affichées pour une tournée.
- Carte : tracé **qui passe par chaque escale** (marqueurs A/B + escales numérotées).
- **873 villes / 150 pays** dans le catalogue.
- Icônes minus/x, sliders & toggles refaits, logo d'entreprise importable.

---

## A. 🚢✈️ Ports & Aéroports (fret maritime & aérien)
*But : débloquer les trajets intercontinentaux (le routier ne traverse pas les océans).*

- [ ] **A1 — Données hubs** : ajouter `hasPort` et `hasAirport` (bool) à `CityEntry` 
  (`Assets/_Project/Scripts/Entities/Map/CityEntry.cs`). Tagger les villes côtières / grandes métropoles (le script `tools/gen_world_cities.py` peut le faire en masse).
- [ ] **A2 — Modes de transport** : étendre `VehicleRoutingProfile` 
  (`Car, HeavyGoodsVehicle` → + `Sea`, `Air`).
- [ ] **A3 — Routing great-circle** : nouveau `GreatCircleProvider : IRoutingProvider` 
  (ligne grand-cercle, **sans API**) pour Sea/Air. `MapSystem.GetRouteAsync` aiguille selon le profil (ORS pour route, GreatCircle pour mer/air).
- [ ] **A4 — Véhicules** : ajouter catégories `CargoShip` / `CargoPlane` à `VehicleCategory` 
  (+ entrées dans le `VehicleCatalog`, profil de routing associé). L'onglet véhicules les triera déjà par catégorie.
- [ ] **A5 — Contrats intercontinentaux** : dans `ContractGenerator`, si origine et destination sont sur des **continents différents** → exiger un mode Sea/Air et router **hub→hub** (port↔port, aéroport↔aéroport). Sinon, route normale.
- [ ] **A6 — Affichage carte** : 
  - Marqueurs spécifiques **port** ⚓ et **aéroport** ✈️ sur les villes-hubs. 
  - Tracé maritime/aérien en **pointillés** (style distinct du routier) via `RouteOverlayView` (le `GreatCircleProvider` renvoie déjà une polyline en arc).
- [ ] **A7 — GeoRegions** : compléter `_continent` / `_borders` pour les pays **hors Europe** 
  (`Assets/_Project/Scripts/Entities/Map/GeoRegions.cs`) → progression géographique mondiale + savoir quel contrat est « intercontinental ».
- [ ] **A8 — Skills** : nœud(s) d'arbre pour débloquer fret maritime puis aérien (effets flag, comme `ContractCountryReach`).

---

## B. 📅 Boucle de rétention quotidienne *(priorité commu)* — ✅ FAIT
*But : donner une raison de revenir chaque jour.*

- [x] **B1 — Missions journalières** : 3 missions/jour (distance, contrats, contrats difficiles, gains, tournées, pays visités) avec barre de progression + bouton « Récupérer » → dollars / lingots / point de skill. Reset quotidien. (`DailySystem`, suivi via `OnContractDelivered`.)
- [x] **B2 — Récompense de connexion** : cycle 7 jours, **streak** consécutif, J7 = point de skill. Popup auto au lancement si quelque chose à réclamer.
- [x] **B3 — Événement du jour** : rotatif (froid / rush / récoltes / fêtes / carburant), **multiplicateur de récompense** appliqué aux contrats (`LiveEvents` + hook `ContractSystem`).

> UI : popup **« Quotidien »** (`DailyHubPopupView`) ouvrable via le bouton 🎯 du header (bannière événement + streak + missions). Sons + haptique à la réclamation.

---

## C. 🎮 Game feel / satisfaction *(meilleur rapport plaisir/effort)* — ✅ FAIT
*But : rendre chaque action satisfaisante.*

- [x] **C1 — Fin de contrat juteuse** : son (caisse) + **gerbe d'argent +X$** + confettis à la livraison (succès uniquement). `JuiceOverlay` + event `OnContractDelivered`.
- [x] **C2 — Haptique** sur livraison, level-up, achat (`Core/Haptics.cs`, mobile only, respecte le son coupé).
- [x] **C3 — Feedback de progression** : pop **NIVEAU SUPÉRIEUR**, toast déblocage de skill, toast nouveau véhicule (`JuiceOverlay`).
- [x] **C4 — Camion animé sur la carte** : avance le long du tracé (déjà là) + repositionnement plus fluide (0,25 s) + léger battement « vivant ».

> Sons générés **procéduralement** (`Audio/Sfx.cs`, aucun asset). Tu peux les remplacer plus tard par de vrais .wav si tu veux.

---

## D. 🏆 Compétition sociale *(le système d'amis existe déjà)* — ✅ FAIT (local)
*But : transformer des joueurs solo en communauté.*

- [x] **D1 — Classement par km** : la popup Social a 2 onglets **Amis | Classement** ; le classement (km parcourus) montre **TA LIGUE** (1er de ta ligue + ta position), **TOP MONDIAL** (top 6 + toi), **TES AMIS**. Renouvelé chaque semaine (`Leaderboard`).
- [x] **D2 — Comparaison amis** : intégré dans `FriendsPopupView` (onglet Classement) ; les amis réels (`FriendsData`) apparaissent dans le classement.
- [x] **D3 — 5 ligues + récompenses** : Bronze → Argent → Or → Platine → Diamant (selon les km). En fin de semaine, **lot dollars + gold selon ligue × placement**, réclamable dans Quotidien ET onglet Classement (`DailySystem.ClaimLeague` / `Leaderboard.LeagueReward`).
- [x] **D4 — Partage** : `OnInviteFriend` (lien d'invitation via `NativeShare`) dans l'onglet Amis.

> ⚠️ **Local/mock** : le classement = joueur + amis + entreprises rivales générées (renouvelées chaque semaine). À rebrancher sur un **vrai backend** quand dispo (la structure `Leaderboard`/`LeaderboardEntry` est prête).

---

## E. ⚠️ Risque & profondeur — ✅ FAIT (E2 partiel)
*But : rendre les choix tendus et intéressants.*

- [x] **E1 — Deadlines** : chaque contrat a un **délai** (rythme exigé ~72 km/h). Livré à temps → **+15 %** ; en retard (véhicule trop lent, ex. gros convoi) → **-20 %**. Affiché dans la popup contrat. (`ContractInstance.deadlineTimeUtcTicks`, `ContractSystem.Finalize`)
- [~] **E2 — Aléas de route** : le système d'**accidents** existant est branché à la réputation (pénalité). *À faire plus tard : incidents non-fatals (panne/crevaison = retard + surcoût) qui prolongent le trajet.*
- [x] **E3 — Prix dynamiques** : prix du **carburant fluctuant** (`Market`, déterministe par horloge) → acheter au bon moment. Prix + tendance ▲▼ affichés dans l'onglet Essence. *(Cargaisons : déjà via l'événement du jour, section B3.)*
- [x] **E4 — Réputation** : 5 paliers (Inconnu → Réputé → Renommé → Établi → Légendaire), monte avec les livraisons à l'heure, baisse avec accidents/retards. **Bonus de récompense par palier** (0 → +15 %). Toast + confettis à la montée de palier. (`ReputationSystem`)

---

## F. 🎨 Identité visuelle & personnalisation *(croissance par le partage)*
*But : les gens partagent ce qu'ils customisent.*

- [ ] **F1 — Livrées / skins de camions** aux couleurs + logo de l'entreprise.
- [ ] **F2 — Personnalisation du dépôt** (apparence, déco).
- [ ] **F3 — Étiqueter le marqueur dépôt** sur la carte (« Dépôt » / nom d'entreprise).

---

## G. 🎯 Objectifs long terme
*But : donner un horizon aux joueurs investis.*

- [ ] **G1 — Succès / achievements** (100 contrats, 1er camion légendaire, 50 pays…).
- [ ] **G2 — Multi-dépôts** : acheter des dépôts dans d'autres villes = hubs-relais (s'emboîte avec A. ports/aéroports).
- [ ] **G3 — Prestige** : relancer en plus gros, pour les hardcore.

---

## 🔧 Dette technique / divers
- [ ] Carte : tracé inter-continental encore en **ligne droite** tant que A. (ports/aéroports) n'est pas fait.
- [ ] Vérifier que les **anciens contrats** (générés avant le départ-dépôt) se renouvellent bien dans la vraie save.
- [ ] Android : import d'image (logo) **non branché** (iOS OK) → plugin galerie si besoin.
- [ ] iOS : renseigner **Photo Library Usage Description** (Player Settings) pour l'import du logo.

---

## ⭐ Ordre conseillé
1. **C1–C4 — Game feel** (fin de contrat + camion animé) → plaisir immédiat, petit effort.
2. **B1–B3 — Missions / événements** → rétention = la commu.
3. **D1–D2 — Classements amis** → dimension sociale.
4. **A1–A8 — Ports & aéroports** → expansion de contenu (trajets mondiaux).
5. **E + F + G** → profondeur, personnalisation, end-game.
