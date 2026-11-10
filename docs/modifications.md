# Journal des modifications (21 septembre 2026)

Ce qui a changé depuis le commit `f9c590d`, à l'époque où l'appli tournait
encore sur NestJS (`backend/`) et React (`frontend/`). Les chemins ci-dessous
sont ceux de ce code-là. Quatre lots :

1. [Énumérations](#1-énumérations-configuration)
2. [Fiche contrat](#2-fiche-contrat)
3. [PDF du contrat](#3-impression-pdf-du-contrat)
4. [Sécurité et authentification](#4-sécurité-et-authentification)

Puis [dépendances](#dépendances-ajoutées), [variables d'environnement](#variables-denvironnement-ajoutées),
[nouvelles routes](#nouvelles-routes-de-lapi) et [points d'attention](#points-dattention).

## 1. Énumérations (Configuration)

Objectif : dans Configuration › Enumérations, choisir une catégorie et gérer
ses valeurs.

Backend :
- `src/categorie-enumeration/` : liste (avec le nombre de valeurs par
  catégorie), création, modif, suppression. Suppression refusée (`409`) pour
  une catégorie système ou qui a encore des valeurs.
- `src/enumeration/` : liste (filtre `?categorieId=` ou nom technique
  `?categorie=`), création, modif, suppression. Suppression refusée (`409`)
  pour une valeur système. Tri par `Ordre` puis libellé.
- À la création, `IDUTILISATEURS_createur/modificateur` sont forcés à `NULL` :
  leur défaut en base (`0`) viole la clé étrangère (pas d'utilisateur 0).

Frontend (`src/features/enumerations/`) :
- Maître/détail : catégories à gauche (recherche sans accents, compteur,
  cadenas pour les catégories système), valeurs à droite (recherche, tableau,
  actions).
- Formulaires en modale, avec la case « Valeur système » et une colonne
  « Système ». Le nom technique d'une catégorie système et la `Valeur` d'une
  valeur système sont verrouillés.
- Responsive : cartes empilées sous 900 px, lignes en cartes sous 640 px.
- `layout/AppLayout.tsx` : l'entrée de menu « Enumérations » ouvre cette page.

## 2. Fiche contrat

Objectif : remplacer le formulaire simple par une fiche à trois onglets,
d'après la capture de l'ancienne appli.

Backend :
- `GET /contrats/:id` renvoie en plus : numéro client et taux de TVA du
  client, conditions CMR, prestations (avec marchandise), factures, et
  `Chiffre_affaires` (somme HT des factures du contrat, sans les proformas).
- `src/condition-cmr/` : `POST`, `PATCH`, `DELETE /conditions-cmr`.
- `src/type-facture/` : `GET /types-facture` (pour le sélecteur « Type facture »).
- `GET /enumerations?categorie=<nom>` : libellés d'une catégorie par son nom technique.
- Sur `POST/PATCH /contrats`, une chaîne vide efface maintenant le champ (date
  de fin, société, type de facture, taux de TVA). Avant elle était ignorée, et
  pour `Taux_tva` MySQL la refusait.

Frontend (`src/features/contrats/`) :
- `ContratForm.tsx` : onglets Détail / Prestations / Factures.
  - Détail : cartes Contrat, Client, Archivage, Conditions CMR, Chiffres clés.
    Changer de société met à jour le numéro client et reprend son taux de TVA.
  - Prestations et Factures : lecture seule, libellés d'unité et d'état tirés
    des énumérations `unite_prestation` et `etat_facture`.
  - Onglets et conditions CMR grisés tant que le contrat n'est pas créé.
- `ContratConditionsCmr.tsx` : ajout (bouton ou Entrée), case à cocher,
  suppression, envoyés direct à l'API sans attendre « Enregistrer ».
- Numéro affiché avec sa version (`Num_contrat.Version_contrat`, ex.
  `1680.24/07`), en lecture seule pour un contrat existant.
- `ContratForm.css` : modale élargie à 1100 px pour cette fiche.
- Champs retirés du formulaire (l'API les accepte toujours, ils ne sont plus
  écrasés) : Type de contrat, Version, Suivant, Qt client facturation, ID
  marchandise.

## 3. Impression PDF du contrat

Objectif : produire l'« état de contrat » d'après le PDF d'exemple. (Cette
version PDFKit a été remplacée depuis par les modèles de document, voir
[impression-pdf.md](impression-pdf.md).)

Backend (`src/contrat/`) :
- `GET /contrats/:id/pdf` : PDF `inline`, nommé `Contrat-<numéro>.pdf`.
- `contrat-pdf.service.ts` : récupère les données (contrat, client et adresse
  de facturation, entité émettrice, prestations et suppléments, libellés
  d'énumérations).
- `contrat-pdf.renderer.ts` : mise en page PDFKit (en-tête, destinataire,
  références, tableau des prestations avec suppléments, TVA, clauses,
  signatures, pied de page, numérotation « n/N », sauts de page). C'est là
  qu'on change la structure ou les textes fixes.
- Coordonnées de l'entreprise (nom, adresse, TVA, banque, IBAN, e-mail) lues
  dans `entites`, rien en dur.
- Logo lu dans `entites.Logo` (PNG ou JPEG). Vide en base pour l'instant, donc
  l'en-tête n'a que du texte.
- Le lien « ICI » (conditions générales) utilise `CGV_URL`.

Frontend :
- Bouton Imprimer dans la fiche contrat (voir la partie impression dans
  [securite-authentification.md](securite-authentification.md#impression-du-pdf)).

## 4. Sécurité et authentification

Détails dans [securite-authentification.md](securite-authentification.md).

Backend :
- `src/auth/` : JWT (HS256), routes `/auth/login|refresh|logout|me`, guard
  global (`@Public()` pour les exceptions). Connexion factice, accepte tout.
- Rate limit (`@nestjs/throttler`) : 300 req/min/IP, 5 tentatives de
  connexion/min/IP.
- `src/main.ts` : helmet, cookie-parser, CORS resserré (plus d'IP du réseau
  local en dur), confiance dans le proxy (détection Railway).
- `src/common/all-exceptions.filter.ts` : un seul format d'erreur, sans détail interne.
- `src/entite/entite.service.ts` : mots de passe SMTP plus renvoyés, vides à
  l'envoi = inchangés.

Frontend :
- `src/lib/api.ts`, `auth.ts`, `errors.ts` : point d'appel unique, jeton en
  mémoire, renouvellement automatique, messages d'erreur du serveur.
- `src/auth/` : `AuthProvider`, écran de connexion gardé mais désactivé (session
  automatique au chargement, réactivable avec `VITE_LOGIN_SCREEN=true`).
- `layout/Sidebar.*` : utilisateur connecté et déconnexion, seulement si
  l'écran de connexion est activé.
- Tous les `fetch` directs des features passent maintenant par le point d'appel unique.
- `features/entite/*` : champs mot de passe SMTP vides avec « (inchangé) ».

## Dépendances ajoutées

Backend (`backend/package.json`) :

| Paquet | Pour |
|---|---|
| `pdfkit`, `@types/pdfkit` | PDF de contrat |
| `@nestjs/jwt` | Jetons JWT |
| `@nestjs/throttler` | Limitation de débit |
| `helmet` | En-têtes de sécurité |
| `cookie-parser`, `@types/cookie-parser` | Lecture du cookie de renouvellement |

Rien côté frontend.

## Variables d'environnement ajoutées

Dans `backend/.env.example` : `JWT_SECRET`, `ACCESS_TOKEN_TTL_SECONDS`,
`COOKIE_SAMESITE`, `COOKIE_SECURE`, `TRUST_PROXY`, `THROTTLE_LIMIT`,
`LOGIN_THROTTLE_LIMIT`, `CGV_URL`.

Un `JWT_SECRET` généré a été mis dans le `backend/.env` local (pas versionné).

Frontend (`frontend/.env.example`) : `VITE_LOGIN_SCREEN` (`false` par défaut).

## Nouvelles routes de l'API

Toutes demandent un jeton sauf `/auth/login`, `/auth/refresh` et `/auth/logout`.

| Route | Pour |
|---|---|
| `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me` | Session |
| `GET/POST /categories-enumeration`, `PATCH/DELETE /categories-enumeration/:id` | Catégories d'énumération |
| `GET/POST /enumerations`, `PATCH/DELETE /enumerations/:id` | Valeurs d'énumération (`?categorieId=`, `?categorie=`) |
| `POST /conditions-cmr`, `PATCH/DELETE /conditions-cmr/:id` | Conditions CMR d'un contrat |
| `GET /types-facture` | Types de facture |
| `GET /contrats/:id/pdf` | PDF du contrat |

Routes modifiées : `GET /contrats/:id` (plus de données), `POST/PATCH /contrats`
(chaîne vide = effacer), `GET/PATCH /entite` (mots de passe SMTP masqués).

## Points d'attention

- La connexion est factice, l'API n'est pas encore vraiment protégée.
- Testé à chaque lot avec de vraies requêtes et dans le navigateur, sur des
  données de test supprimées ensuite (base remise dans son état d'origine).
- Hypothèses métier à confirmer : le CA exclut les proformas ; le PDF n'imprime
  que le numéro de contrat (sans la version) et toutes les prestations
  supplémentaires (sans filtre sur les dates de validité) ; notes de crédit pas
  vérifiées (aucune facture en base).
- Manque en base : logo d'entité, types de facture, prestations, factures.
- Tout appel sans jeton prend maintenant un `401` (scripts, `curl`).
- Railway : définir `JWT_SECRET`, vérifier `CORS_ORIGIN`, et
  `COOKIE_SAMESITE=none` si front et API sont sur des domaines différents.
- Un backend lancé avant ces changements (`node dist/main`) ne les voit pas :
  `npm run start:dev` (rechargement auto) ou recompiler puis relancer.
