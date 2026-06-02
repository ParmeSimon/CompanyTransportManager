#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Ajoute des villes du monde entier au CityCatalog.asset (≈5-8 par pays).
Conserve l'existant, évite les doublons d'id, n'ajoute que les nouveautés."""
import re, unicodedata, os

ASSET = os.path.join(os.path.dirname(__file__), "..",
    "Assets/_Project/ScriptableObjects/Map/CityCatalog.asset")

# country(FR) -> [(city, lat, lon), ...]
DATA = {
# ── EUROPE (complément des pays existants + voisins) ──
"France":[("Toulouse",43.6045,1.4440),("Nantes",47.2184,-1.5536),("Strasbourg",48.5734,7.7521),("Rennes",48.1173,-1.6778),("Reims",49.2583,4.0317)],
"Allemagne":[("Brême",53.0793,8.8017),("Hanovre",52.3759,9.7320),("Nuremberg",49.4521,11.0767),("Dresde",51.0504,13.7373),("Leipzig",51.3397,12.3731)],
"Espagne":[("Bilbao",43.2630,-2.9350),("Malaga",36.7213,-4.4214),("Saragosse",41.6488,-0.8891),("Murcie",37.9922,-1.1307),("Palma",39.5696,2.6502)],
"Italie":[("Bologne",44.4949,11.3426),("Florence",43.7696,11.2558),("Bari",41.1171,16.8719),("Catane",37.5079,15.0830),("Vérone",45.4384,10.9916)],
"Royaume-Uni":[("Édimbourg",55.9533,-3.1883),("Bristol",51.4545,-2.5879),("Cardiff",51.4816,-3.1791),("Belfast",54.5973,-5.9301),("Newcastle",54.9783,-1.6178)],
"Pologne":[("Gdansk",54.3520,18.6466),("Szczecin",53.4285,14.5528),("Bydgoszcz",53.1235,18.0084)],
"Portugal":[("Coimbra",40.2033,-8.4103),("Faro",37.0194,-7.9304),("Braga",41.5454,-8.4265),("Funchal",32.6669,-16.9241)],
"Pays-Bas":[("Groningue",53.2194,6.5665),("Eindhoven",51.4416,5.4697),("Tilburg",51.5555,5.0913)],
"Belgique":[("Liège",50.6326,5.5797),("Bruges",51.2093,3.2247),("Namur",50.4674,4.8720)],
"Suisse":[("Lausanne",46.5197,6.6323),("Lugano",46.0037,8.9511),("Lucerne",47.0502,8.3093)],
"Autriche":[("Klagenfurt",46.6247,14.3079),("Innsbruck",47.2692,11.4041)],
"Roumanie":[("Brasov",45.6580,25.6012),("Constanta",44.1598,28.6348),("Sibiu",45.7983,24.1256)],
"Ukraine":[("Lviv",49.8397,24.0297),("Donetsk",48.0159,37.8028),("Zaporijjia",47.8388,35.1396)],
"Suède":[("Uppsala",59.8586,17.6389),("Linköping",58.4108,15.6214),("Örebro",59.2741,15.2066)],
"Norvège":[("Drammen",59.7440,10.2045),("Kristiansand",58.1599,8.0182),("Tromsø",69.6492,18.9553)],
"Finlande":[("Turku",60.4518,22.2666),("Oulu",65.0121,25.4651),("Jyväskylä",62.2426,25.7473)],
"Danemark":[("Esbjerg",55.4765,8.4594),("Randers",56.4607,10.0364)],
"République Tchèque":[("Prague",50.0755,14.4378),("Brno",49.1951,16.6068),("Ostrava",49.8209,18.2625),("Pilsen",49.7384,13.3736),("Liberec",50.7663,15.0543)],
"Hongrie":[("Pécs",46.0727,18.2323),("Győr",47.6875,17.6504),("Miskolc",48.1035,20.7784)],
"Bulgarie":[("Varna",43.2141,27.9147),("Bourgas",42.5048,27.4626)],
"Grèce":[("Patras",38.2466,21.7346),("Héraklion",35.3387,25.1442),("Larissa",39.6390,22.4191)],
"Croatie":[("Rijeka",45.3271,14.4422),("Osijek",45.5550,18.6955)],
"Serbie":[("Niš",43.3209,21.8958),("Subotica",46.1003,19.6651)],
"Lituanie":[("Klaipeda",55.7033,21.1443),("Siauliai",55.9333,23.3167)],
"Irlande":[("Cork",51.8985,-8.4756),("Galway",53.2707,-9.0568),("Limerick",52.6680,-8.6305)],
"Slovaquie":[("Bratislava",48.1486,17.1077),("Košice",48.7164,21.2611),("Žilina",49.2231,18.7398),("Nitra",48.3061,18.0764)],
"Slovénie":[("Ljubljana",46.0569,14.5058),("Maribor",46.5547,15.6459),("Celje",46.2309,15.2604)],
"Bosnie-H.":[("Sarajevo",43.8563,18.4131),("Banja Luka",44.7722,17.1910),("Mostar",43.3438,17.8078),("Tuzla",44.5384,18.6671)],
"Albanie":[("Tirana",41.3275,19.8187),("Durrës",41.3231,19.4414),("Vlorë",40.4686,19.4894),("Shkodër",42.0683,19.5126)],
"Moldavie":[("Chisinau",47.0105,28.8638),("Bălți",47.7615,27.9292),("Tiraspol",46.8403,29.6433)],
"Biélorussie":[("Minsk",53.9006,27.5590),("Gomel",52.4345,30.9754),("Vitebsk",55.1904,30.2049),("Brest",52.0976,23.7341)],
"Lettonie":[("Riga",56.9496,24.1052),("Daugavpils",55.8714,26.5161),("Liepāja",56.5047,21.0108)],
"Estonie":[("Tallinn",59.4370,24.7536),("Tartu",58.3776,26.7290),("Narva",59.3772,28.1903)],
"Russie":[("Moscou",55.7558,37.6173),("Saint-Pétersbourg",59.9311,30.3609),("Novossibirsk",55.0084,82.9357),("Iekaterinbourg",56.8389,60.6057),("Kazan",55.7963,49.1088),("Rostov-sur-le-Don",47.2357,39.7015)],
"Islande":[("Reykjavik",64.1466,-21.9426),("Akureyri",65.6885,-18.1262),("Kópavogur",64.1126,-21.9127),("Keflavík",64.0049,-22.5657)],
"Malte":[("La Valette",35.8989,14.5146),("Birkirkara",35.8972,14.4611),("Sliema",35.9122,14.5042),("Mosta",35.9094,14.4256)],
"Luxembourg":[("Luxembourg",49.6116,6.1319),("Esch-sur-Alzette",49.4958,5.9806),("Differdange",49.5242,5.8914),("Dudelange",49.4806,6.0875)],
"Macédoine du Nord":[("Skopje",41.9981,21.4254),("Bitola",41.0319,21.3347),("Kumanovo",42.1322,21.7144),("Prilep",41.3464,21.5550)],
"Monténégro":[("Podgorica",42.4304,19.2594),("Nikšić",42.7731,18.9447),("Herceg Novi",42.4531,18.5375),("Bar",42.0931,19.1003)],
"Kosovo":[("Pristina",42.6629,21.1655),("Prizren",42.2139,20.7397),("Peja",42.6593,20.2887),("Gjakova",42.3803,20.4308)],
# ── PROCHE & MOYEN-ORIENT ──
"Turquie":[("Istanbul",41.0082,28.9784),("Ankara",39.9334,32.8597),("Izmir",38.4237,27.1428),("Bursa",40.1885,29.0610),("Antalya",36.8969,30.7133),("Adana",37.0000,35.3213)],
"Géorgie":[("Tbilissi",41.7151,44.8271),("Koutaïssi",42.2679,42.7180),("Batoumi",41.6168,41.6367),("Roustavi",41.5497,44.9930),("Zougdidi",42.5088,41.8709)],
"Arménie":[("Erevan",40.1792,44.4991),("Gyumri",40.7894,43.8475),("Vanadzor",40.8128,44.4883),("Vagharchapat",40.1622,44.2911),("Hrazdan",40.4969,44.7644)],
"Azerbaïdjan":[("Bakou",40.4093,49.8671),("Gandja",40.6828,46.3606),("Sumqayit",40.5897,49.6686),("Mingachevir",40.7700,47.0489),("Lankaran",38.7529,48.8475)],
"Israël":[("Jérusalem",31.7683,35.2137),("Tel-Aviv",32.0853,34.7818),("Haïfa",32.7940,34.9896),("Beer-Sheva",31.2518,34.7913),("Netanya",32.3215,34.8532)],
"Liban":[("Beyrouth",33.8938,35.5018),("Tripoli",34.4367,35.8497),("Saïda",33.5571,35.3729),("Tyr",33.2705,35.2038),("Zahlé",33.8463,35.9019)],
"Jordanie":[("Amman",31.9454,35.9284),("Zarqa",32.0728,36.0876),("Irbid",32.5556,35.8500),("Aqaba",29.5320,35.0063),("Madaba",31.7160,35.7938)],
"Syrie":[("Damas",33.5138,36.2765),("Alep",36.2021,37.1343),("Homs",34.7324,36.7137),("Lattaquié",35.5236,35.7917),("Hama",35.1318,36.7578)],
"Irak":[("Bagdad",33.3152,44.3661),("Bassorah",30.5085,47.7804),("Mossoul",36.3450,43.1189),("Erbil",36.1901,44.0091),("Nadjaf",32.0000,44.3333)],
"Iran":[("Téhéran",35.6892,51.3890),("Machhad",36.2605,59.6168),("Ispahan",32.6539,51.6660),("Chiraz",29.5918,52.5837),("Tabriz",38.0800,46.2919),("Ahvaz",31.3203,48.6693)],
"Arabie Saoudite":[("Riyad",24.7136,46.6753),("Djeddah",21.4858,39.1925),("La Mecque",21.3891,39.8579),("Médine",24.5247,39.5692),("Dammam",26.4207,50.0888),("Tabouk",28.3838,36.5550)],
"Émirats Arabes Unis":[("Dubaï",25.2048,55.2708),("Abou Dabi",24.4539,54.3773),("Charjah",25.3463,55.4209),("Al-Aïn",24.1302,55.8023),("Ajman",25.4052,55.5136)],
"Qatar":[("Doha",25.2854,51.5310),("Al Rayyan",25.2919,51.4244),("Al Wakrah",25.1715,51.6034),("Al Khor",25.6804,51.4969),("Umm Salal",25.4167,51.3833)],
"Koweït":[("Koweït",29.3759,47.9774),("Al Ahmadi",29.0769,48.0838),("Hawalli",29.3328,48.0289),("Jahra",29.3375,47.6581),("Salmiya",29.3394,48.0506)],
"Oman":[("Mascate",23.5880,58.3829),("Salalah",17.0151,54.0924),("Sohar",24.3470,56.7090),("Nizwa",22.9333,57.5333),("Sour",22.5667,59.5289)],
"Bahreïn":[("Manama",26.2285,50.5860),("Riffa",26.1300,50.5550),("Muharraq",26.2572,50.6119),("Hamad",26.1156,50.5069),("Isa",26.1736,50.5478)],
"Yémen":[("Sanaa",15.3694,44.1910),("Aden",12.7855,45.0187),("Taïz",13.5795,44.0209),("Hodeïda",14.7978,42.9545),("Ibb",13.9667,44.1833)],
"Afghanistan":[("Kaboul",34.5553,69.2075),("Kandahar",31.6289,65.7372),("Herat",34.3529,62.2040),("Mazar-e-Charif",36.7090,67.1109),("Jalalabad",34.4265,70.4515)],
# ── ASIE CENTRALE & DU SUD ──
"Kazakhstan":[("Almaty",43.2220,76.8512),("Astana",51.1605,71.4704),("Chymkent",42.3417,69.5901),("Karaganda",49.8047,73.1094),("Aktobe",50.2839,57.1670)],
"Ouzbékistan":[("Tachkent",41.2995,69.2401),("Samarcande",39.6270,66.9750),("Boukhara",39.7747,64.4286),("Namangan",40.9983,71.6726),("Andijan",40.7821,72.3442)],
"Turkménistan":[("Achgabat",37.9601,58.3261),("Türkmenabat",39.0833,63.5833),("Dasoguz",41.8363,59.9666),("Mary",37.6000,61.8333),("Balkanabat",39.5108,54.3672)],
"Kirghizistan":[("Bichkek",42.8746,74.5698),("Och",40.5283,72.7985),("Jalal-Abad",40.9333,73.0000),("Karakol",42.4907,78.3936),("Tokmok",42.8421,75.2900)],
"Tadjikistan":[("Douchanbé",38.5598,68.7870),("Khoudjand",40.2833,69.6222),("Kulob",37.9092,69.7800),("Bokhtar",37.8364,68.7806),("Istaravchan",39.9100,69.0011)],
"Mongolie":[("Oulan-Bator",47.8864,106.9057),("Erdenet",49.0278,104.0444),("Darkhan",49.4861,105.9228),("Tchoïbalsan",48.0667,114.5000),("Mörön",49.6342,100.1625)],
"Inde":[("New Delhi",28.6139,77.2090),("Mumbai",19.0760,72.8777),("Bangalore",12.9716,77.5946),("Calcutta",22.5726,88.3639),("Chennai",13.0827,80.2707),("Hyderabad",17.3850,78.4867),("Ahmedabad",23.0225,72.5714)],
"Pakistan":[("Islamabad",33.6844,73.0479),("Karachi",24.8607,67.0011),("Lahore",31.5204,74.3587),("Faisalabad",31.4504,73.1350),("Rawalpindi",33.5651,73.0169),("Peshawar",34.0151,71.5249)],
"Bangladesh":[("Dacca",23.8103,90.4125),("Chittagong",22.3569,91.7832),("Khulna",22.8456,89.5403),("Rajshahi",24.3745,88.6042),("Sylhet",24.8949,91.8687)],
"Sri Lanka":[("Colombo",6.9271,79.8612),("Kandy",7.2906,80.6337),("Galle",6.0535,80.2210),("Jaffna",9.6615,80.0255),("Negombo",7.2083,79.8358)],
"Népal":[("Katmandou",27.7172,85.3240),("Pokhara",28.2096,83.9856),("Lalitpur",27.6644,85.3188),("Biratnagar",26.4525,87.2718),("Birganj",27.0000,84.8667)],
"Birmanie":[("Rangoun",16.8409,96.1735),("Mandalay",21.9588,96.0891),("Naypyidaw",19.7633,96.0785),("Bago",17.3350,96.4815),("Mawlamyine",16.4909,97.6283)],
# ── ASIE DE L'EST & SUD-EST ──
"Chine":[("Pékin",39.9042,116.4074),("Shanghai",31.2304,121.4737),("Canton",23.1291,113.2644),("Shenzhen",22.5431,114.0579),("Chengdu",30.5728,104.0668),("Wuhan",30.5928,114.3055),("Xi'an",34.3416,108.9398),("Chongqing",29.5630,106.5516)],
"Japon":[("Tokyo",35.6762,139.6503),("Osaka",34.6937,135.5023),("Nagoya",35.1815,136.9066),("Sapporo",43.0618,141.3545),("Fukuoka",33.5904,130.4017),("Kyoto",35.0116,135.7681),("Sendai",38.2682,140.8694)],
"Corée du Sud":[("Séoul",37.5665,126.9780),("Busan",35.1796,129.0756),("Incheon",37.4563,126.7052),("Daegu",35.8714,128.6014),("Daejeon",36.3504,127.3845),("Gwangju",35.1595,126.8526)],
"Corée du Nord":[("Pyongyang",39.0392,125.7625),("Hamhung",39.9183,127.5360),("Chongjin",41.7956,129.7756),("Nampo",38.7375,125.4078),("Wonsan",39.1475,127.4456)],
"Vietnam":[("Hanoï",21.0278,105.8342),("Hô Chi Minh-Ville",10.8231,106.6297),("Da Nang",16.0544,108.2022),("Haïphong",20.8449,106.6881),("Can Tho",10.0452,105.7469),("Hué",16.4637,107.5909)],
"Thaïlande":[("Bangkok",13.7563,100.5018),("Chiang Mai",18.7883,98.9853),("Nonthaburi",13.8591,100.5217),("Pattaya",12.9236,100.8825),("Phuket",7.8804,98.3923),("Khon Kaen",16.4419,102.8360)],
"Indonésie":[("Jakarta",-6.2088,106.8456),("Surabaya",-7.2575,112.7521),("Bandung",-6.9175,107.6191),("Medan",3.5952,98.6722),("Semarang",-6.9667,110.4167),("Makassar",-5.1477,119.4327),("Denpasar",-8.6705,115.2126)],
"Malaisie":[("Kuala Lumpur",3.1390,101.6869),("George Town",5.4141,100.3288),("Ipoh",4.5975,101.0901),("Johor Bahru",1.4927,103.7414),("Kuching",1.5535,110.3593),("Kota Kinabalu",5.9804,116.0735)],
"Philippines":[("Manille",14.5995,120.9842),("Quezon City",14.6760,121.0437),("Davao",7.1907,125.4553),("Cebu",10.3157,123.8854),("Zamboanga",6.9214,122.0790),("Cagayan de Oro",8.4542,124.6319)],
"Singapour":[("Singapour",1.3521,103.8198),("Jurong",1.3329,103.7436),("Tampines",1.3496,103.9568),("Woodlands",1.4382,103.7890),("Changi",1.3644,103.9915)],
"Cambodge":[("Phnom Penh",11.5564,104.9282),("Siem Reap",13.3671,103.8448),("Battambang",13.0957,103.2022),("Sihanoukville",10.6093,103.5296),("Kampong Cham",12.0000,105.4500)],
"Laos":[("Vientiane",17.9757,102.6331),("Luang Prabang",19.8845,102.1348),("Savannakhet",16.5569,104.7510),("Pakse",15.1202,105.7986),("Thakhek",17.4053,104.8003)],
# ── OCÉANIE ──
"Australie":[("Sydney",-33.8688,151.2093),("Melbourne",-37.8136,144.9631),("Brisbane",-27.4698,153.0251),("Perth",-31.9523,115.8613),("Adelaide",-34.9285,138.6007),("Canberra",-35.2809,149.1300),("Darwin",-12.4634,130.8456)],
"Nouvelle-Zélande":[("Auckland",-36.8485,174.7633),("Wellington",-41.2865,174.7762),("Christchurch",-43.5321,172.6362),("Hamilton",-37.7870,175.2793),("Dunedin",-45.8788,170.5028),("Tauranga",-37.6878,176.1651)],
"Papouasie-N.-G.":[("Port Moresby",-9.4438,147.1803),("Lae",-6.7330,146.9970),("Mont Hagen",-5.8576,144.2300),("Madang",-5.2246,145.7848),("Rabaul",-4.1980,152.1632)],
"Fidji":[("Suva",-18.1248,178.4501),("Nadi",-17.7765,177.4356),("Lautoka",-17.6242,177.4677),("Labasa",-16.4333,179.3667),("Ba",-17.5333,177.6667)],
# ── AFRIQUE DU NORD ──
"Maroc":[("Rabat",34.0209,-6.8416),("Casablanca",33.5731,-7.5898),("Marrakech",31.6295,-7.9811),("Fès",34.0181,-5.0078),("Tanger",35.7595,-5.8340),("Agadir",30.4278,-9.5981)],
"Algérie":[("Alger",36.7538,3.0588),("Oran",35.6969,-0.6331),("Constantine",36.3650,6.6147),("Annaba",36.9000,7.7667),("Blida",36.4700,2.8300),("Sétif",36.1900,5.4100)],
"Tunisie":[("Tunis",36.8065,10.1815),("Sfax",34.7406,10.7603),("Sousse",35.8256,10.6369),("Kairouan",35.6781,10.0963),("Bizerte",37.2744,9.8739),("Gabès",33.8814,10.0982)],
"Libye":[("Tripoli",32.8872,13.1913),("Benghazi",32.1167,20.0686),("Misratah",32.3754,15.0925),("Tobrouk",32.0836,23.9764),("Syrte",31.2089,16.5887)],
"Égypte":[("Le Caire",30.0444,31.2357),("Alexandrie",31.2001,29.9187),("Gizeh",30.0131,31.2089),("Port-Saïd",31.2653,32.3019),("Louxor",25.6872,32.6396),("Assouan",24.0889,32.8998)],
"Soudan":[("Khartoum",15.5007,32.5599),("Omdourman",15.6445,32.4777),("Port-Soudan",19.6175,37.2164),("Kassala",15.4510,36.4000),("El-Obeid",13.1842,30.2167)],
"Mauritanie":[("Nouakchott",18.0735,-15.9582),("Nouadhibou",20.9333,-17.0333),("Kiffa",16.6200,-11.4044),("Kaédi",16.1500,-13.5000),("Rosso",16.5138,-15.8050)],
# ── AFRIQUE DE L'OUEST ──
"Nigeria":[("Abuja",9.0765,7.3986),("Lagos",6.5244,3.3792),("Kano",12.0022,8.5919),("Ibadan",7.3776,3.9470),("Port Harcourt",4.8156,7.0498),("Benin City",6.3350,5.6037)],
"Ghana":[("Accra",5.6037,-0.1870),("Kumasi",6.6885,-1.6244),("Tamale",9.4008,-0.8393),("Sekondi-Takoradi",4.9344,-1.7133),("Cape Coast",5.1315,-1.2795)],
"Côte d'Ivoire":[("Abidjan",5.3600,-4.0083),("Yamoussoukro",6.8276,-5.2893),("Bouaké",7.6900,-5.0300),("Daloa",6.8772,-6.4503),("San-Pédro",4.7485,-6.6363)],
"Sénégal":[("Dakar",14.7167,-17.4677),("Thiès",14.7910,-16.9359),("Saint-Louis",16.0179,-16.4896),("Kaolack",14.1652,-16.0726),("Ziguinchor",12.5681,-16.2719)],
"Mali":[("Bamako",12.6392,-8.0029),("Sikasso",11.3176,-5.6666),("Ségou",13.4317,-6.2658),("Mopti",14.4843,-4.1796),("Gao",16.2716,-0.0444)],
"Burkina Faso":[("Ouagadougou",12.3714,-1.5197),("Bobo-Dioulasso",11.1771,-4.2979),("Koudougou",12.2526,-2.3686),("Banfora",10.6333,-4.7667),("Ouahigouya",13.5828,-2.4214)],
"Niger":[("Niamey",13.5117,2.1251),("Zinder",13.8053,8.9881),("Maradi",13.5000,7.1017),("Agadez",16.9737,7.9911),("Tahoua",14.8888,5.2692)],
"Guinée":[("Conakry",9.6412,-13.5784),("Kankan",10.3854,-9.3057),("Nzérékoré",7.7561,-8.8179),("Kindia",10.0569,-12.8658),("Labé",11.3167,-12.2833)],
"Bénin":[("Cotonou",6.3703,2.3912),("Porto-Novo",6.4969,2.6289),("Parakou",9.3370,2.6303),("Abomey",7.1826,1.9912),("Bohicon",7.1782,2.0667)],
"Togo":[("Lomé",6.1725,1.2314),("Sokodé",8.9837,1.1326),("Kara",9.5511,1.1861),("Kpalimé",6.9000,0.6333),("Atakpamé",7.5333,1.1167)],
"Sierra Leone":[("Freetown",8.4657,-13.2317),("Bo",7.9647,-11.7383),("Kenema",7.8767,-11.1900),("Makeni",8.8833,-12.0500),("Koidu",8.6439,-10.9714)],
"Liberia":[("Monrovia",6.3004,-10.7969),("Gbarnga",6.9956,-9.4722),("Buchanan",5.8808,-10.0467),("Kakata",6.5300,-10.3536),("Harper",4.3750,-7.7169)],
# ── AFRIQUE CENTRALE & EST ──
"Cameroun":[("Yaoundé",3.8480,11.5021),("Douala",4.0511,9.7679),("Garoua",9.3017,13.3921),("Bamenda",5.9597,10.1459),("Bafoussam",5.4781,10.4178)],
"Tchad":[("N'Djamena",12.1348,15.0557),("Moundou",8.5667,16.0833),("Sarh",9.1500,18.3833),("Abéché",13.8292,20.8324),("Kelo",9.3092,15.8064)],
"Gabon":[("Libreville",0.4162,9.4673),("Port-Gentil",-0.7193,8.7815),("Franceville",-1.6333,13.5833),("Oyem",1.5993,11.5793),("Lambaréné",-0.7001,10.2418)],
"Congo":[("Brazzaville",-4.2634,15.2429),("Pointe-Noire",-4.7692,11.8664),("Dolisie",-4.1989,12.6730),("Nkayi",-4.1833,13.2833),("Ouesso",1.6136,16.0517)],
"RD Congo":[("Kinshasa",-4.4419,15.2663),("Lubumbashi",-11.6647,27.4794),("Mbuji-Mayi",-6.1360,23.5897),("Kananga",-5.8961,22.4174),("Kisangani",0.5153,25.1910),("Goma",-1.6792,29.2228)],
"Kenya":[("Nairobi",-1.2864,36.8172),("Mombasa",-4.0435,39.6682),("Kisumu",-0.0917,34.7680),("Nakuru",-0.3031,36.0800),("Eldoret",0.5143,35.2698)],
"Tanzanie":[("Dar es Salaam",-6.7924,39.2083),("Dodoma",-6.1630,35.7516),("Mwanza",-2.5164,32.9175),("Arusha",-3.3869,36.6830),("Zanzibar",-6.1659,39.2026)],
"Ouganda":[("Kampala",0.3476,32.5825),("Gulu",2.7724,32.2881),("Lira",2.2350,32.9097),("Mbarara",-0.6072,30.6545),("Jinja",0.4244,33.2042)],
"Rwanda":[("Kigali",-1.9441,30.0619),("Butare",-2.5967,29.7394),("Gisenyi",-1.7028,29.2564),("Ruhengeri",-1.4997,29.6342),("Cyangugu",-2.4846,28.9075)],
"Éthiopie":[("Addis-Abeba",9.0250,38.7469),("Dire Dawa",9.5931,41.8661),("Mekele",13.4967,39.4753),("Gondar",12.6000,37.4667),("Hawassa",7.0621,38.4769)],
"Somalie":[("Mogadiscio",2.0469,45.3182),("Hargeisa",9.5600,44.0650),("Bosaso",11.2842,49.1816),("Kismayo",-0.3582,42.5454),("Berbera",10.4396,45.0143)],
"Angola":[("Luanda",-8.8390,13.2894),("Huambo",-12.7761,15.7392),("Lobito",-12.3644,13.5364),("Benguela",-12.5763,13.4055),("Lubango",-14.9177,13.4925)],
"Zambie":[("Lusaka",-15.3875,28.3228),("Kitwe",-12.8024,28.2132),("Ndola",-12.9587,28.6366),("Kabwe",-14.4469,28.4464),("Livingstone",-17.8419,25.8543)],
"Zimbabwe":[("Harare",-17.8252,31.0335),("Bulawayo",-20.1325,28.6266),("Mutare",-18.9707,32.6709),("Gweru",-19.4500,29.8167),("Kwekwe",-18.9281,29.8149)],
"Mozambique":[("Maputo",-25.9692,32.5732),("Matola",-25.9622,32.4589),("Beira",-19.8436,34.8389),("Nampula",-15.1165,39.2666),("Quelimane",-17.8786,36.8883)],
"Madagascar":[("Antananarivo",-18.8792,47.5079),("Toamasina",-18.1492,49.4023),("Antsirabe",-19.8659,47.0333),("Mahajanga",-15.7167,46.3167),("Fianarantsoa",-21.4536,47.0858)],
"Malawi":[("Lilongwe",-13.9626,33.7741),("Blantyre",-15.7861,35.0058),("Mzuzu",-11.4656,34.0207),("Zomba",-15.3833,35.3333),("Kasungu",-13.0333,33.4833)],
# ── AFRIQUE AUSTRALE ──
"Afrique du Sud":[("Le Cap",-33.9249,18.4241),("Johannesburg",-26.2041,28.0473),("Durban",-29.8587,31.0218),("Pretoria",-25.7479,28.2293),("Port Elizabeth",-33.9608,25.6022),("Bloemfontein",-29.0852,26.1596)],
"Namibie":[("Windhoek",-22.5594,17.0832),("Walvis Bay",-22.9576,14.5053),("Swakopmund",-22.6792,14.5272),("Rundu",-17.9333,19.7667),("Oshakati",-17.7833,15.6833)],
"Botswana":[("Gaborone",-24.6282,25.9231),("Francistown",-21.1700,27.5167),("Maun",-19.9833,23.4167),("Serowe",-22.3875,26.7106),("Selebi-Phikwe",-21.9783,27.8500)],
"Maurice":[("Port-Louis",-20.1609,57.5012),("Vacoas",-20.2981,57.4783),("Curepipe",-20.3188,57.5264),("Quatre Bornes",-20.2654,57.4791),("Rose Hill",-20.2419,57.4674)],
# ── AMÉRIQUE DU NORD ──
"États-Unis":[("New York",40.7128,-74.0060),("Los Angeles",34.0522,-118.2437),("Chicago",41.8781,-87.6298),("Houston",29.7604,-95.3698),("Phoenix",33.4484,-112.0740),("Miami",25.7617,-80.1918),("Seattle",47.6062,-122.3321),("Denver",39.7392,-104.9903)],
"Canada":[("Toronto",43.6532,-79.3832),("Montréal",45.5017,-73.5673),("Vancouver",49.2827,-123.1207),("Ottawa",45.4215,-75.6972),("Calgary",51.0447,-114.0719),("Edmonton",53.5461,-113.4938),("Québec",46.8139,-71.2080)],
"Mexique":[("Mexico",19.4326,-99.1332),("Guadalajara",20.6597,-103.3496),("Monterrey",25.6866,-100.3161),("Puebla",19.0414,-98.2063),("Tijuana",32.5149,-117.0382),("Cancún",21.1619,-86.8515),("Mérida",20.9674,-89.5926)],
"Guatemala":[("Guatemala",14.6349,-90.5069),("Quetzaltenango",14.8333,-91.5167),("Escuintla",14.3050,-90.7850),("Mixco",14.6333,-90.6064),("Villa Nueva",14.5269,-90.5872)],
"Honduras":[("Tegucigalpa",14.0723,-87.1921),("San Pedro Sula",15.5000,-88.0333),("Choloma",15.6144,-87.9528),("La Ceiba",15.7597,-86.7822),("El Progreso",15.4006,-87.8011)],
"Salvador":[("San Salvador",13.6929,-89.2182),("Santa Ana",13.9942,-89.5597),("San Miguel",13.4833,-88.1833),("Soyapango",13.7100,-89.1389),("Santa Tecla",13.6731,-89.2797)],
"Nicaragua":[("Managua",12.1149,-86.2362),("León",12.4347,-86.8780),("Masaya",11.9744,-86.0942),("Chinandega",12.6294,-87.1311),("Granada",11.9344,-85.9560)],
"Costa Rica":[("San José",9.9281,-84.0907),("Alajuela",10.0162,-84.2117),("Cartago",9.8644,-83.9194),("Heredia",10.0023,-84.1165),("Liberia",10.6346,-85.4377)],
"Panama":[("Panama",8.9824,-79.5199),("Colón",9.3592,-79.9014),("David",8.4333,-82.4333),("Santiago",8.1000,-80.9833),("Chitré",7.9667,-80.4333)],
"Cuba":[("La Havane",23.1136,-82.3666),("Santiago de Cuba",20.0247,-75.8219),("Camagüey",21.3808,-77.9169),("Holguín",20.8872,-76.2631),("Santa Clara",22.4069,-79.9647)],
"République Dominicaine":[("Saint-Domingue",18.4861,-69.9312),("Santiago",19.4517,-70.6970),("La Romana",18.4273,-68.9728),("San Pedro de Macorís",18.4539,-69.3086),("Puerto Plata",19.7900,-70.6900)],
"Haïti":[("Port-au-Prince",18.5944,-72.3074),("Cap-Haïtien",19.7592,-72.1989),("Gonaïves",19.4500,-72.6833),("Les Cayes",18.2000,-73.7500),("Jacmel",18.2342,-72.5347)],
"Jamaïque":[("Kingston",17.9714,-76.7920),("Montego Bay",18.4762,-77.8939),("Spanish Town",17.9911,-76.9575),("Portmore",17.9500,-76.8800),("May Pen",17.9656,-77.2456)],
# ── AMÉRIQUE DU SUD ──
"Brésil":[("Brasília",-15.7939,-47.8828),("São Paulo",-23.5505,-46.6333),("Rio de Janeiro",-22.9068,-43.1729),("Salvador",-12.9777,-38.5016),("Fortaleza",-3.7319,-38.5267),("Belo Horizonte",-19.9167,-43.9345),("Manaus",-3.1190,-60.0217),("Curitiba",-25.4284,-49.2733)],
"Argentine":[("Buenos Aires",-34.6037,-58.3816),("Córdoba",-31.4201,-64.1888),("Rosario",-32.9442,-60.6505),("Mendoza",-32.8895,-68.8458),("La Plata",-34.9215,-57.9545),("Mar del Plata",-38.0055,-57.5426)],
"Colombie":[("Bogota",4.7110,-74.0721),("Medellín",6.2442,-75.5812),("Cali",3.4516,-76.5320),("Barranquilla",10.9685,-74.7813),("Cartagena",10.3910,-75.4794),("Bucaramanga",7.1193,-73.1227)],
"Chili":[("Santiago",-33.4489,-70.6693),("Valparaíso",-33.0472,-71.6127),("Concepción",-36.8270,-73.0498),("Antofagasta",-23.6509,-70.3975),("Temuco",-38.7359,-72.5904),("La Serena",-29.9027,-71.2519)],
"Pérou":[("Lima",-12.0464,-77.0428),("Arequipa",-16.4090,-71.5375),("Trujillo",-8.1116,-79.0288),("Chiclayo",-6.7714,-79.8409),("Cuzco",-13.5319,-71.9675),("Piura",-5.1945,-80.6328)],
"Venezuela":[("Caracas",10.4806,-66.9036),("Maracaibo",10.6545,-71.6406),("Valencia",10.1620,-68.0077),("Barquisimeto",10.0647,-69.3570),("Maracay",10.2469,-67.5958)],
"Équateur":[("Quito",-0.1807,-78.4678),("Guayaquil",-2.1894,-79.8891),("Cuenca",-2.9001,-79.0059),("Santo Domingo",-0.2542,-79.1719),("Ambato",-1.2490,-78.6168)],
"Bolivie":[("La Paz",-16.4897,-68.1193),("Santa Cruz",-17.7833,-63.1821),("Cochabamba",-17.3895,-66.1568),("Sucre",-19.0196,-65.2619),("Oruro",-17.9833,-67.1500)],
"Paraguay":[("Asunción",-25.2637,-57.5759),("Ciudad del Este",-25.5097,-54.6111),("San Lorenzo",-25.3397,-57.5089),("Encarnación",-27.3306,-55.8667),("Pedro Juan Caballero",-22.5472,-55.7333)],
"Uruguay":[("Montevideo",-34.9011,-56.1645),("Salto",-31.3833,-57.9667),("Paysandú",-32.3214,-58.0756),("Las Piedras",-34.7286,-56.2206),("Maldonado",-34.9000,-54.9500)],
}

# ── Complément pour les petits pays (atteindre ≥5-6 villes) ──
TOPUP = {
"Chypre":[("Larnaca",34.9182,33.6207),("Paphos",34.7754,32.4245),("Famagouste",35.1264,33.9407),("Kyrenia",35.3414,33.3192)],
"Slovénie":[("Kranj",46.2389,14.3556),("Koper",45.5481,13.7302),("Novo Mesto",45.8010,15.1710)],
"Moldavie":[("Cahul",45.9075,28.1944),("Ungheni",47.2117,27.8053),("Soroca",48.1558,28.2975)],
"Lettonie":[("Jelgava",56.6500,23.7128),("Jūrmala",56.9680,23.7704),("Ventspils",57.3894,21.5606)],
"Estonie":[("Pärnu",58.3859,24.4971),("Kohtla-Järve",59.3986,27.2731),("Viljandi",58.3639,25.5900)],
"Luxembourg":[("Pétange",49.5583,5.8806),("Sanem",49.5489,5.9197),("Diekirch",49.8674,6.1596)],
"Irlande":[("Waterford",52.2593,-7.1101),("Drogheda",53.7189,-6.3478),("Dundalk",54.0019,-6.4058)],
"Slovaquie":[("Banská Bystrica",48.7395,19.1530),("Prešov",48.9984,21.2339),("Trnava",48.3774,17.5877)],
"Bulgarie":[("Plovdiv",42.1354,24.7453),("Roussé",43.8356,25.9657),("Stara Zagora",42.4258,25.6345)],
"Croatie":[("Split",43.5081,16.4402),("Zadar",44.1194,15.2314),("Pula",44.8666,13.8496)],
"Serbie":[("Kragujevac",44.0142,20.9111),("Novi Sad",45.2671,19.8335),("Zrenjanin",45.3836,20.3819)],
"Monténégro":[("Pljevlja",43.3567,19.3586),("Budva",42.2911,18.8403),("Cetinje",42.3911,18.9116)],
"Albanie":[("Elbasan",41.1125,20.0822),("Korçë",40.6186,20.7808),("Fier",40.7239,19.5567)],
"Macédoine du Nord":[("Ohrid",41.1231,20.8016),("Veles",41.7150,21.7753),("Štip",41.7458,22.1958)],
"Kosovo":[("Ferizaj",42.3700,21.1483),("Gjilan",42.4631,21.4694),("Podujevo",42.9106,21.1933)],
"Lituanie":[("Panevėžys",55.7333,24.3500),("Alytus",54.3964,24.0458),("Marijampolė",54.5572,23.3542)],
"Islande":[("Selfoss",63.9333,-21.0000),("Akranes",64.3211,-22.0747),("Vestmannaeyjar",63.4427,-20.2734)],
}
for _k,_v in TOPUP.items():
    DATA.setdefault(_k, []).extend(_v)

# ── Génération ──
def slug(s):
    s = unicodedata.normalize('NFKD', s).encode('ascii','ignore').decode()
    s = re.sub(r"[^a-zA-Z0-9]+","-", s).strip("-").lower()
    return s or "x"

def yml(s):
    """Scalaire YAML : double-guillemets si le texte contient un caractère sensible."""
    if any(ch in s for ch in "'\":#{}[],&*!|>%@`") or s != s.strip():
        return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'
    return s

def main():
    text = open(ASSET, encoding="utf-8").read()
    existing = set(re.findall(r"^  - id: (.+)$", text, re.M))
    used = set(existing)
    blocks, added, per_country = [], 0, {}
    for country, cities in DATA.items():
        for name, lat, lon in cities:
            cid = slug(name)
            if cid in existing:
                continue          # ville déjà présente dans le catalogue → on ne duplique pas
            if cid in used:        # même nom dans deux pays différents → suffixe pays
                cid = f"{cid}-{slug(country)[:3]}"
            if cid in used:        # encore en conflit → suffixe numérique
                k = 2
                while f"{cid}{k}" in used: k += 1
                cid = f"{cid}{k}"
            used.add(cid)
            per_country[country] = per_country.get(country,0)+1
            added += 1
            blocks.append(
                f"  - id: {cid}\n"
                f"    displayName: {yml(name)}\n"
                f"    country: {yml(country)}\n"
                f"    location:\n"
                f"      latitude: {lat}\n"
                f"      longitude: {lon}\n"
                f"    deliveryPointLabels: []\n")
    if not text.endswith("\n"): text += "\n"
    open(ASSET, "w", encoding="utf-8").write(text + "".join(blocks))
    print(f"Villes ajoutées : {added}")
    print(f"Pays touchés    : {len(per_country)}")
    print(f"Total catalogue : {len(existing)+added} villes")

if __name__ == "__main__":
    main()
