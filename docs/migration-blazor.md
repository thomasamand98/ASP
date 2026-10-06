# Migration du front React vers Blazor WebAssembly

Inventaire de `frontend/src` (≈ 25 000 lignes, 190 fichiers) et équivalent
Blazor prévu. La colonne « Vérifié » est cochée après comparaison réelle dans
le navigateur avec l'ancien front React lancé en parallèle, sur la même API.

## 1. Décisions

Validées le 2026-10-05 (« fais au mieux », types partagés entre front et
back au besoin) : D1 WebAssembly standalone ; D2 éditeur en JS ; D3 types
partagés dans `Shared`, adoptés par l'API quand le JSON reste identique ;
D4 ping + impression + transfert + traitement long, `calcul` supprimé ;
D5 les 8 modes portés, sélecteur désactivé.

| # | Sujet | Proposition |
|---|---|---|
| D1 | Modèle d'hébergement | **Blazor WebAssembly standalone**, comme demandé. Rien ne justifie Interactive Server : l'appli est déjà une SPA avec JWT en mémoire + cookie de renouvellement, le front reste un site statique (nginx), et l'API ne change pas. Seul coût : premier chargement plus lourd (≈ 3-5 Mo compressés, mis en cache ensuite). |
| D2 | Éditeur de modèles de document | L'éditeur (`DocumentTemplateEditor`, `domInsert.ts`, ≈ 600 lignes) repose sur `contentEditable`, `selectionchange`, `caretRangeFromPoint` et l'insertion de nœuds DOM sans perdre le curseur. **Blazor n'a aucun équivalent** : le moteur de rendu réécrirait le DOM sous le curseur. Proposition : porter cette partie en JS dans `interop.js` (≈ 300 lignes, module `templateEditor`), le reste (palette, barre d'outils, liste, enregistrement, garde des modifications) en Razor. **C'est une exception à la règle 5 qui demande ton accord.** |
| D3 | Périmètre de `Shared` | Les DTO d'**entrée** de l'API (`Create*Dto`/`Update*Dto`, `Opt<T>`, attributs de validation `[Id]`, `[Text]`…) passent dans `Shared` et sont utilisés tels quels par le front pour construire les requêtes. Les **réponses** de l'API sont aujourd'hui des objets anonymes (`new { attelages = …, total }`) : je crée dans `Shared` des records de réponse que le front désérialise, et je fais retourner ces records par l'API **module par module**, en vérifiant par des tests d'instantané JSON (réponse avant = réponse après) que le contrat ne bouge pas. À valider : ce remplacement côté API, ou seulement côté front (moins risqué, mais deux définitions de fait). |
| D4 | Page Performance | L'arbre de travail actuel (non commité) remplace le traitement long par le « calcul lourd » de comparaison (`POST /performance/calcul`, `calcul-lourd.ts`, `CalculLourd.cs`), que la mission demande de supprimer. Proposition : garder **ping/latence pendant un traitement**, **impression de tous les contrats** et **test de transfert/compression** ; remettre le **traitement long** (`POST /performance/traitement-long`, modes attente/cpu, version commitée de la page) comme test de réactivité ; supprimer `/performance/calcul`. Sinon : ne garder aucun test de traitement long et supprimer aussi `traitement-long`. |
| D5 | Modes de vue | Seul le mode « Onglet » est actif (`VIEW_MODE_SELECTION_ENABLED = false`) ; les 7 autres sont du code présent mais inaccessible. Proposition : les porter tous (≈ 1 composant + CSS chacun) avec le sélecteur désactivé de la même façon, pour une parité stricte. Alternative : ne porter que « Onglet » et lister les autres dans `docs/ameliorations.md`. |

## 2. Socle de l'application

| React | Rôle | Blazor prévu | Vérifié |
|---|---|---|---|
| `main.tsx`, `App.tsx`, `index.html` (script anti-flash du thème) | Démarrage, `AuthProvider` > `UnsavedChangesProvider` > `AppLayout` | `Program.cs`, `App.razor`, `wwwroot/index.html` (même script de thème, inline) | ☐ |
| `lib/config.ts` (`VITE_API_URL`, `VITE_LOGIN_SCREEN`) | Configuration | `wwwroot/appsettings.json` (`ApiUrl`, `LoginScreen`) + `appsettings.Development.json` ; repli sur `http://<hôte>:3000` si `ApiUrl` vide | ☐ |
| `lib/auth.ts` | Jeton en mémoire, `refresh` mutualisé, connexion automatique mutualisée, `logout` | `Services/AuthSession.cs` (singleton : jeton en mémoire, `SemaphoreSlim` pour mutualiser refresh/login auto) ; requêtes `/auth/*` avec `credentials: include` via `SetBrowserRequestCredentials` | ☐ |
| `lib/api.ts`, `lib/errors.ts` | Appel unique : jeton, 401 → refresh → rejouer, sinon session perdue ; `ApiError` avec message serveur | `Services/ApiClient.cs` (`DelegatingHandler` pour le jeton et le 401) + `ApiException` ; lecture de `{ statusCode, message }` | ☐ |
| `lib/useApiList.ts`, `lib/useApiMutation.ts` | Liste (pagination, recherche, filtres, réponse la plus récente seulement), CRUD | `Services/ApiList<T>` (compteur de requête pour ignorer les réponses périmées) + `CrudApi<TDto,T,TDetail>` | ☐ |
| `lib/useEnumerationLabels.ts` | Libellés d'une catégorie d'énumération | `EnumerationLabels` (service avec cache par catégorie) | ☐ |
| `lib/theme.ts` | Thème clair/sombre/système, `localStorage`, suivi du système | `ThemeService` + interop (`localStorage`, `matchMedia`) | ☐ |
| `lib/windevColor.ts` (+ test) | Couleurs WinDev ↔ `#rrggbb` | `Shared/WindevColor.cs` (déjà côté API dans `Common/WindevColor.cs` : fusionnés) ; test porté en xUnit | ☐ |
| `lib/adresse.ts`, `lib/period.ts` | Adresse aplatie, période `from/to` | `Web/Models/Adresse.cs`, `Period.cs` | ☐ |
| `features/planning/dateUtils.ts` (+ test) | Dates locales, semaines, voies de chevauchement | `Web/Common/DateUtils.cs` ; test porté | ☐ |
| `auth/AuthProvider.tsx`, `AuthContext.ts` | États checking/anonymous/authenticated/error, session automatique si écran de connexion désactivé, retour à la connexion si session perdue | `Auth/AuthGate.razor` + service en cascade (`CascadingValue`) | ☐ |
| `auth/LoginPage.tsx` + CSS | Écran de connexion | `Auth/LoginPage.razor` + `.razor.css` | ☐ |
| `layout/AppLayout.tsx` + CSS | Menu, onglets persistés (`localStorage`), pages montées en arrière-plan (masquées), onglets dynamiques, chargement différé | `Layout/AppLayout.razor` ; pages masquées par `hidden` (état conservé) ; chargement différé des pages lourdes via assemblies paresseuses **non retenu** (gain faible en WASM, complexité) | ☐ |
| `layout/Sidebar.tsx` + CSS | Groupes déroulants, tiroir mobile < 768 px, thème, déconnexion (avec garde des modifications) | `Layout/Sidebar.razor` | ☐ |
| `layout/TabBar.tsx` + CSS, `TabsContext.tsx` | Onglets façon navigateur, fermeture avec garde, réordonnancement par glisser-déposer natif | `Layout/TabBar.razor` (événements `ondragstart/over/drop` de Blazor), `TabsService` | ☐ |
| `layout/TasksPanel.tsx` + CSS | Bandeau « Tâches à faire » : compteur, rafraîchissement périodique, traiter / reprogrammer, Échap | `Layout/TasksPanel.razor` (`PeriodicTimer`) | ☐ |
| `layout/icons.tsx`, `components/HorizonLogo.tsx`, `*/icons.tsx` | Icônes SVG | Composants `Icons/*.razor` (SVG inline identiques) | ☐ |
| `styles/*.css`, `index.css` | Jetons, base, contrôles, utilitaires | Copiés tels quels dans `wwwroot/css/` (globaux) | ☐ |
| `assets/` (`hero.png`, `react.svg`, `vite.svg`) | Restes du gabarit | Non repris s'ils sont inutilisés (à vérifier) | ☐ |

## 3. Composants génériques

| React | Comportement à reproduire | Blazor prévu | Vérifié |
|---|---|---|---|
| `CrudPage.tsx` | En-tête « Titre (n) », Nouveau/Modifier/Supprimer, multisuppression (`Promise.allSettled`, lignes en échec restent sélectionnées), fiche en onglet (mode par défaut) ou selon le mode de vue, pagination serveur (10/25/50/100/Tous), recherche, `toolbarExtra`, projection, garde des modifications, page gardée affichée après le 1er chargement | `Components/CrudPage.razor` générique (`TItem`, `TDto`, `TDetail`), `RenderFragment<FormContext>` pour le formulaire | ☐ |
| `DataTable.tsx` (TanStack Table) | Tri au clic (▲/▼, 3 états), filtre par colonne (entonnoir, `includesString` insensible à la casse), sélection simple / Ctrl / Maj / Maj+Ctrl / case d'en-tête indéterminée, double clic, ligne dépliée, « Aucun résultat », cartes empilées < 768 px, export Excel des lignes affichées | `Components/DataTable.razor` + `ColumnDef<T>` (en-tête, accesseur, rendu, valeur de tri/filtre). Tri et filtre réimplémentés (mêmes règles que `sortFn_text` / `filterFn_includesString`) | ☐ |
| `TableSearch.tsx` | Anti-rebond 300 ms, Échap / croix efface | `TableSearch.razor` | ☐ |
| `ArchiveFilter.tsx`, `PeriodFilter.tsx` | Filtres `?archive=` et `?from=&to=` (flèches, Aujourd'hui, Toutes, bornes réordonnées) | `ArchiveFilter.razor`, `PeriodFilter.razor` | ☐ |
| `PageActions.tsx` | Boutons et états désactivés, compteur sur Supprimer | `PageActions.razor` | ☐ |
| `Modal.tsx` | Overlay, croix, fermeture avec garde | `Modal.razor` | ☐ |
| `FicheLayout.tsx` | `FicheHero`, `FicheSection`, `FicheField`, `FicheSwitch`, icônes | `Fiche/*.razor` | ☐ |
| `FicheTabs.tsx` | Onglets de fiche, flèches/Début/Fin, compteur, onglet monté à la 1re visite puis masqué (`FichePanel`) | `FicheTabs.razor`, `FichePanel.razor` | ☐ |
| `AdresseFields.tsx` | Pays (liste), villes selon le code postal (anti-rebond, `datalist`), pays inconnu conservé | `AdresseFields.razor` | ☐ |
| `ColorField.tsx` | Pastille + `<input type="color">`, effacement, pastille en lecture seule | `ColorField.razor`, `ColorSwatch.razor` | ☐ |
| `projection/*` | Bouton Projection (menu, clic extérieur, Échap), relations, onglet résultat « Contrats de 3 sociétés », valeurs par défaut | `Projection/*.razor`, `Relations.cs` | ☐ |
| `unsaved-changes/*` | Zones imbriquées (application > onglet > fiche), gardes, dialogue Enregistrer / Annuler les modifications (Échap, clic fond, Entrée = enregistrer), validation navigateur avant enregistrement, `beforeunload`, « touché » à la 1re interaction | `UnsavedChanges/GuardScope.cs`, `GuardBoundary.razor` (`CascadingValue`), `UnsavedChangesDialog.razor`, interop `beforeunload` et `reportValidity` | ☐ |
| `view-modes/*` (8 modes + sélecteur + préférence) | Voir D5 | `ViewModes/*.razor` | ☐ |

## 4. Écrans

Endpoints : ceux appelés par l'écran (hors `/auth`). « CRUD » = `GET` liste,
`POST`, `PATCH /:id`, `DELETE /:id` ; « +détail » = `GET /:id` à l'ouverture
de la fiche.

| Menu | Écran (React) | Particularités | Endpoints | Vérifié |
|---|---|---|---|---|
| Tableau de bord | `dashboard/DashboardPage`, `ChiffreAffairesCard` | Période (année en cours), N-1, granularité mois/jour, bascule Graphique/Tableau, évolution sur la partie écoulée, infobulle | `statistiques/chiffre-affaires` | ✔ |
| Commercial › Société | `societes/*` | Fiche à onglets : Principale (client / fournisseur), Contacts (CrudPage imbriqué), Contrats / Offres, Documents, Msg envoyés ; pagination, projection | `societes` CRUD+détail, `contacts`, `contrats`, `messages`, `lien-documents`, `pays`, `villes` | ✔ |
| Commercial › Contrats / Offre de prix | `contrats/*` | Onglets Détail / Prestations / Factures, conditions CMR, numéro, bouton PDF (modèle choisi, ouverture dans un nouvel onglet), types de facture | `contrats` CRUD+détail, `contrats/:id/prestations`, `contrats/:id/pdf`, `conditions-cmr` (POST/PATCH/DELETE), `types-facture`, `document-templates?type=`, `societes/:id`, `enumerations?categorie=` | ✔ |
| Commercial › Contacts | `contacts/*` | Fiche contact, adresse reprise de la société | `contacts` CRUD+détail, `societes/:id` | ✔ |
| Commercial › Point | `points/*` | Onglets Principale / Contacts / Carte (iframe Google Maps), horaires | `points` CRUD+détail, `points/:id/contacts/:contactId` (PATCH/DELETE), `contacts` | ✔ |
| Commercial › Conditions d'exécution | `conditions/*` | Libellés d'énumération | `conditions` CRUD, `enumerations?categorie=` | ✔ |
| Commercial › Marchandises | `marchandises/*` | Recherche de déchets, couleur planning | `marchandises` CRUD+détail, `dechets?search=` | ✔ |
| Gestion attelage › Chauffeurs | `chauffeurs/*` | Filtre d'archive | `chauffeurs` CRUD | ✔ |
| Gestion attelage › Véhicules | `vehicules/*` | Onglets Général / Documents | `vehicules` CRUD+détail, `lien-documents` | ✔ |
| Gestion attelage › Attelages | `attelages/*` | — | `attelages` CRUD | ✔ |
| Production › Commandes | `commandes/*` | Filtre de période, prestations du contrat choisi | `commandes` CRUD, `contrats/:id/prestations` | ✔ |
| Production › Planning | `planning/*` | Vues Colonne / Ligne, regroupement, panneau des commandes en attente, glisser-déposer (déplacer, redimensionner, déposer une commande), voies de chevauchement, préférences `localStorage`, avis « enregistré » | `planning`, `planning/executions` (POST/PATCH/DELETE) | ✔ |
| Gestion personnel › Personnel | `personnel/*` | Onglets Principal / Contrat (contrats de travail : liste + fiche) / Contact / Documents | `personnel` CRUD+détail, `personnel/:id/contrats-travail` (GET/POST), `contrats-travail/:id` (PATCH/DELETE), `lien-documents`, `pays`, `villes` | ✔ |
| Gestion personnel › Pointage | `pointage/*` | Liste + feuille de pointage (salarié, mois/période, totaux, montants, statuts colorés), saisie d'un jour | `pointage` CRUD+détail, `pointage/feuille`, `pointage/statuts`, `personnel` | ✔ |
| Gestion documentaire | `documents/*` | Liste des modèles, création, éditeur 3 zones (en-tête / contenu / pied), champs de fusion par glisser-déposer, blocs répétables, mode code HTML, assainissement | `document-templates` CRUD+détail | ✔ |
| Configuration › Société | `entite/*` | Fiche de l'entité, SMTP, pays/villes | `entite` (GET/PATCH), `pays`, `villes` | ✔ |
| Configuration › Enumérations | `enumerations/*` | Catégories (système non supprimables), valeurs, recherche, ordre | `categories-enumeration` (GET/POST/PATCH/DELETE), `enumerations` (GET/POST/PATCH/DELETE) | ✔ |
| Configuration › Test OCR | `ocr/*` | Dépôt PDF/image (glisser ou choisir), aperçu, préréglages (prompt + JSON Schema), résultat JSON + Markdown | `mistral/ocr` | ✔ |
| Configuration › Performance | `performance/*` | Voir D4 | `performance/ping`, `performance/impression-contrats`, `performance/transfert`, (`performance/traitement-long`) | ✔ |
| Exécutions, Général, Base de données, Comptabilité, Outils, Données brutes, Import/Export | `enDeveloppement/Dev.tsx` | Page « en développement » | — | ✔ |

## 5. Bibliothèques et équivalents

| React | Équivalent Blazor | Dépendance |
|---|---|---|
| React 19, Vite | Blazor WebAssembly (.NET 10) | `Microsoft.AspNetCore.Components.WebAssembly` (+ `DevServer`) : le framework |
| TanStack Table | `DataTable.razor` fait maison (tri, filtres, sélection) | — |
| Recharts (`BarChart` groupé, axes, grille, infobulle, `ResponsiveContainer`) | `BarChart.razor` en SVG fait maison ; largeur mesurée par `ResizeObserver` (interop) | — |
| xlsx (SheetJS) | Écriture `.xlsx` minimale faite maison (SpreadsheetML + `System.IO.Compression`, ≈ 150 lignes), téléchargement par interop. ClosedXML/OpenXML ajouteraient plusieurs Mo au téléchargement WASM | — |
| DOMPurify | `HtmlSanitizer` | `HtmlSanitizer` (Ganss.Xss) : seul assainisseur HTML .NET maintenu, s'exécute dans le navigateur |
| Vitest | xUnit + bUnit | `bunit` : tests de composants Razor demandés ; xUnit déjà utilisé par `Api.Tests` |

## 6. Interop JavaScript (`wwwroot/js/interop.js`)

Uniquement là où Blazor n'a pas d'équivalent :

- fichiers : télécharger un blob (export Excel, document joint), ouvrir un blob
  dans un onglet ouvert **avant** l'attente réseau (PDF de contrat, documents) ;
- `localStorage` (onglets, thème, préférences du planning et du pointage),
  `matchMedia` (thème système), `beforeunload` ;
- `confirm` / `alert` (mêmes boîtes natives que React) ;
- focus, `checkbox.indeterminate`, `form.reportValidity()` ;
- clic hors d'un menu et Échap au niveau du document (menu Projection,
  panneau des tâches) ;
- `getBoundingClientRect` pour la position de dépose du planning ;
- `ResizeObserver` du graphique ;
- éditeur de modèles de document (voir D2).

## 7. Tests

- `tests/Web.Tests` (bUnit + xUnit) : `DataTable` (tri, filtre, sélection
  Maj/Ctrl), `CrudPage` (création, suppression multiple avec échec partiel),
  garde des modifications (dialogue, enregistrer / abandonner), `AuthGate`
  (session automatique, écran de connexion), `TabBar` ; portage des tests
  Vitest `windevColor.test.ts`, `dateUtils.test.ts`, `feuille.test.ts`.
- `tests/Api.Tests` : repris tel quel, plus les instantanés JSON de D3.

## 8. Code à supprimer après vérification

Endpoints que le front n'appelle plus (relevé par recherche dans
`frontend/src`, à confirmer écran par écran pendant la migration) :

| Endpoint | Constat |
|---|---|
| `GET /auth/me` | Jamais appelé : la session est rétablie par `POST /auth/refresh` |
| `GET /clients`, `GET /fournisseurs` | Seuls appelants : `useClients.ts`, `useFournisseurs.ts`, eux-mêmes importés nulle part |
| `GET /attelages/:id`, `/chauffeurs/:id`, `/commandes/:id`, `/conditions/:id` | Pas de `getDetail` sur ces pages : la fiche s'ouvre avec la ligne du tableau |
| `POST /performance/calcul` | Comparaison des deux backends (voir D4) |
| `POST /performance/traitement-long` | Seulement si D4 l'écarte |

Plus : `backend/`, `frontend/`, les outils de comparaison de
`backend-dotnet/tools` (sauf, à décider, `prisma-to-ef.mjs` : il dépend de
`schema.prisma`, qui disparaît ; les entités deviennent la référence),
`package-lock.json`, le repli sur `backend/.env`, le profil `Comparaison`,
`VITE_DOTNET_API_URL`, `.vscode/settings.json` (réglages TypeScript/Prettier).

## 9. Ordre de travail et commits

1. Restructuration `src/Api`, `src/Shared`, `tests/Api.Tests` — build + tests.
2. `src/Web` : socle (configuration, authentification, client HTTP, mise en
   page, onglets, tâches, garde des modifications), composants génériques.
3. Écrans, un commit par groupe : Commercial (Société, Contrats, Contacts,
   Point, Conditions, Marchandises) ; Gestion attelage ; Production
   (Commandes, Planning) ; Personnel (Personnel, Pointage) ; Documentaire ;
   Configuration ; Tableau de bord.
4. Suppressions (section 8), nettoyage.
5. Docker (API, front nginx, `docker-compose.yml` avec MySQL).
6. Documentation.
