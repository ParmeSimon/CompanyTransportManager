#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Ajoute des véhicules au VehicleCatalog.asset (toutes catégories, début → end game).
Conserve l'existant, saute les ids déjà présents, ajoute à la fin de la liste `vehicles:`."""
import re, os

ASSET = os.path.join(os.path.dirname(__file__), "..",
    "Assets/_Project/ScriptableObjects/Vehicles/VehicleCatalog.asset")

# Catégories (index sérialisé) :
# 0 Fourgonnette · 1 Camion · 2 PoidsLourd · 3 SemiRemorque · 4 ConvoiExceptionnel
# 5 Frigorifique · 6 Benne · 7 Citerne · 8 PorteConteneur · 9 TrainRoutier · 10 MegaConvoi

# id, name, cat, price, maint, speed, cap, maxKm, tank, cons, profile, minLvl
V = [
 # ── Fourgonnettes (early, profil voiture) ──
 ("pickup_leger","Pick-up Léger",0,5000,300,115,4,12000,60,8.5,0,1),
 ("fourgon_compact","Fourgon Compact",0,12000,450,120,7,18000,70,9,0,2),
 ("fourgon_xl_premium","Fourgon XL Premium",0,64000,1200,125,16,42000,95,9.5,0,4),
 # ── Camions ──
 ("porteur_urbain_10t","Porteur Urbain 10t",1,40000,2000,110,10,30000,150,18,1,3),
 ("porteur_renforce_18t","Porteur Renforcé 18t",1,95000,4800,100,18,45000,200,24,1,5),
 ("camion_grue_14t","Camion-Grue 14t",1,120000,6500,90,14,40000,220,27,1,6),
 # ── Poids lourds (catégorie vide jusqu'ici) ──
 ("poids_lourd_26t","Poids Lourd 26t",2,150000,8000,95,26,60000,300,30,1,6),
 ("poids_lourd_32t","Poids Lourd 32t",2,210000,11000,90,32,75000,350,33,1,7),
 ("poids_lourd_premium_38t","Poids Lourd Premium 38t",2,300000,15000,92,38,95000,400,35,1,9),
 # ── Bennes (BTP) ──
 ("benne_chantier_18t","Benne de Chantier 18t",6,130000,7000,85,18,50000,260,28,1,4),
 ("benne_tp_26t","Benne TP 26t",6,220000,12000,80,26,70000,320,34,1,6),
 ("benne_carriere_40t","Benne de Carrière 40t",6,480000,26000,65,40,90000,420,46,1,9),
 # ── Frigorifiques ──
 ("frigo_compact_12t","Frigorifique Compact 12t",5,90000,5000,105,12,40000,180,21,1,5),
 ("frigo_renforce_22t","Frigorifique Renforcé 22t",5,190000,10500,98,22,65000,280,29,1,7),
 ("frigo_pharma_30t","Frigorifique Pharma 30t",5,380000,19000,100,30,90000,360,32,1,10),
 # ── Citernes ──
 ("citerne_alim_24t","Citerne Alimentaire 24t",7,240000,13000,90,24,70000,320,33,1,8),
 ("citerne_carburant_30t","Citerne Carburant 30t",7,360000,19000,88,30,85000,380,37,1,10),
 ("citerne_chimique_36t","Citerne Chimique 36t",7,520000,28000,85,36,100000,440,41,1,12),
 # ── Semi-remorques ──
 ("semi_tautliner_34t","Semi Tautliner 34t",3,280000,14000,95,34,90000,400,34,1,10),
 ("semi_megatrailer_40t","Semi Mega-Trailer 40t",3,420000,21000,92,40,110000,450,38,1,12),
 ("semi_double_44t","Semi Double Plancher 44t",3,600000,30000,90,44,130000,500,42,1,14),
 # ── Porte-conteneurs ──
 ("porte_conteneur_20","Porte-Conteneur 20'",8,350000,18000,90,28,90000,400,36,1,13),
 ("porte_conteneur_40","Porte-Conteneur 40'",8,520000,27000,88,36,110000,460,40,1,15),
 ("porte_conteneur_double_45t","Porte-Conteneur Double 45t",8,780000,40000,85,45,130000,520,45,1,17),
 # ── Convois exceptionnels ──
 ("convoi_90t","Convoi Exceptionnel 90t",4,1500000,95000,55,90,120000,1100,90,1,16),
 ("convoi_eolien_120t","Convoi Éolien 120t",4,2400000,150000,45,120,130000,1500,110,1,18),
 # ── Trains routiers (late game) ──
 ("train_routier_double_70t","Train Routier Double 70t",9,1900000,110000,80,70,150000,900,70,1,20),
 ("train_routier_triple_110t","Train Routier Triple 110t",9,3200000,180000,75,110,170000,1300,95,1,22),
 ("train_routier_outback_160t","Train Routier Outback 160t",9,5000000,280000,70,160,190000,1800,125,1,24),
 # ── Méga-convois (end game) ──
 ("mega_convoi_300t","Méga-Convoi 300t",10,8000000,450000,40,300,160000,2600,160,1,26),
 ("mega_convoi_industriel_500t","Méga-Convoi Industriel 500t",10,15000000,820000,32,500,180000,3500,230,1,30),
 ("mega_convoi_titan_800t","Méga-Convoi Titan 800t",10,30000000,1600000,25,800,200000,5000,340,1,34),
]

def yml_name(s):
    return f'"{s}"' if any(c in s for c in "'\":#") else s

def main():
    text = open(ASSET, encoding="utf-8").read()
    existing = set(re.findall(r"^  - id: (.+)$", text, re.M))
    blocks, added = [], 0
    for (vid, name, cat, price, maint, spd, cap, mx, tank, cons, prof, lvl) in V:
        if vid in existing:
            continue
        added += 1
        blocks.append(
            f"  - id: {vid}\n"
            f"    displayName: {yml_name(name)}\n"
            f"    category: {cat}\n"
            f"    icon: {{fileID: 0}}\n"
            f"    purchasePrice: {price}\n"
            f"    maintenanceCost: {maint}\n"
            f"    speedKmh: {spd}\n"
            f"    capacity: {cap}\n"
            f"    maxKilometers: {mx}\n"
            f"    fuelTankCapacityLiters: {tank}\n"
            f"    fuelConsumptionLPer100Km: {cons}\n"
            f"    routingProfile: {prof}\n"
            f"    minCompanyLevelRequired: {lvl}\n")
    if not text.endswith("\n"): text += "\n"
    open(ASSET, "w", encoding="utf-8").write(text + "".join(blocks))
    print(f"Véhicules ajoutés : {added}")
    print(f"Total catalogue   : {len(existing) + added}")

if __name__ == "__main__":
    main()
