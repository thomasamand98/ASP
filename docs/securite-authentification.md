# Sécurité et authentification

Comment l'API est protégée (JWT, limitation de débit, CORS, en-têtes, format
des erreurs) et comment remplacer la connexion factice actuelle.

**La connexion est factice.** `POST /auth/login` accepte n'importe quel
identifiant et mot de passe sans regarder la base. N'importe qui qui atteint
cette route a un jeton valide, donc l'API n'est pas vraiment protégée. Rate
limit, CORS et en-têtes restent utiles, mais ça ne sera sérieux qu'une fois
la [vraie authentification](#passer-à-une-vraie-authentification) en place.

L'écran de connexion est aussi désactivé : il existe
(`src/Web/Auth/LoginPage.razor`) mais le front ouvre une session automatique
au chargement. Les jetons, le renouvellement et le rate limit marchent
normalement, il n'y a juste pas de saisie. Voir
[Écran de connexion désactivé](#écran-de-connexion-désactivé).

## Vue d'ensemble

| Protection | Rôle | Où |
|---|---|---|
| JWT | Jeton d'accès exigé partout sauf connexion / renouvellement / déconnexion | `src/Api/Auth/` |
| Rate limit | 300 req/min/IP, 5 tentatives de connexion/min/IP | `Auth/ThrottleMiddleware.cs` |
| CORS | Seules les origines autorisées lisent les réponses depuis un navigateur | `Program.cs`, `Configuration/AppOptions.cs` |
| En-têtes de sécurité | Mêmes en-têtes que helmet côté NestJS, pas d'en-tête `Server` | `Common/SecurityHeadersMiddleware.cs` |
| Erreurs | Format unique `{ statusCode, message }`, aucun détail interne | `Common/ExceptionMiddleware.cs` |
| Mots de passe SMTP | Plus renvoyés par `GET /entite` | `Features/Entites/Entites.cs` |

C'est l'API qui décide. Le front affiche l'écran de connexion et joint le
jeton, il ne valide rien lui-même.

## Déroulement d'une session

```
 Navigateur (front)                                  API
 ──────────────────                                  ───
 1. Ouverture de la page
    POST /auth/refresh  (cookie httpOnly)  ───────►  cookie valide ?
                                           ◄───────  200 { accessToken, login }   → app affichée
                                           ◄───────  401                          → connexion

 2. Connexion
    POST /auth/login { login, password }   ───────►  AuthenticateAsync()
                                           ◄───────  200 { accessToken, login }
                                                     + Set-Cookie: refresh_token (httpOnly, 8 h)

 3. Appels métier
    GET /societes                          ───────►  ThrottleMiddleware puis authentification
    Authorization: Bearer <accessToken>    ◄───────  200 { ... }

 4. Jeton d'accès expiré (15 min)
    GET /societes                          ───────►  401
    POST /auth/refresh (cookie)            ───────►  nouveau jeton d'accès
    GET /societes (rejoué)                 ───────►  200

 5. Déconnexion
    POST /auth/logout                      ───────►  cookie effacé
```

Deux jetons :

| | Jeton d'accès | Jeton de renouvellement |
|---|---|---|
| Durée | 15 min (`ACCESS_TOKEN_TTL_SECONDS`) | 8 h |
| Stockage | En mémoire dans le front (pas de `localStorage`) | Cookie `httpOnly`, illisible en JS |
| Envoi | En-tête `Authorization: Bearer ...` | Automatique, seulement vers `/auth/*` |
| Audience (`aud`) | `mvc-template-app` | `mvc-template-refresh` |

Les audiences sont différentes, donc un jeton de renouvellement ne passe pas
comme jeton d'accès et inversement (il y a un test).

Le jeton ne contient que `sub` (l'identifiant), `iat`, `exp`, `iss`
(`mvc-template-api`) et `aud`.

Au rechargement de la page, le jeton d'accès (en mémoire) est perdu. Le front
appelle `POST /auth/refresh` au chargement et le cookie suffit à reprendre la
session.

## API

### Fichiers (`src/Api`)

| Fichier | Rôle |
|---|---|
| `Auth/AuthController.cs` | Routes `/auth/login`, `/auth/refresh`, `/auth/logout`, options du cookie |
| `Auth/TokenService.cs` | `AuthenticateAsync()` (factice), émission et vérification des jetons (HS256) |
| `Auth/BearerAuthenticationHandler.cs` | Lit `Authorization: Bearer <jeton>` |
| `Auth/ThrottleMiddleware.cs` | Limitation de débit, attributs `[LoginThrottle]` et `[SkipThrottle]` |
| `Configuration/AppOptions.cs` | `AppOptions` (CORS, proxy...) et `AuthOptions` (durées, audiences, secret, cookie) |
| `Common/ExceptionMiddleware.cs` | Format d'erreur unique |
| `Common/SecurityHeadersMiddleware.cs` | En-têtes de sécurité |
| `Program.cs` | Branche le tout : CORS, proxy, ordre des middlewares, politique par défaut |

### Routes d'authentification

| Route | Accès | Corps | Réponse |
|---|---|---|---|
| `POST /auth/login` | Public, 5/min/IP | `{ login, password }` | `200 { accessToken, login }` + cookie. `400` si champ vide ou trop long, `401` si refusé |
| `POST /auth/refresh` | Public | (cookie) | `200 { accessToken, login }` + cookie prolongé. `401` sans cookie valide |
| `POST /auth/logout` | Public | aucun | `200 {}`, cookie effacé |

Toutes les autres routes répondent `401` sans jeton valide, `GET /contrats/:id/pdf`
compris.

### Ordre et exceptions

La politique par défaut (`SetFallbackPolicy`) exige un utilisateur
authentifié sur toutes les routes. Pour ouvrir une route, mettre
`[AllowAnonymous]`, à garder pour la connexion et le renouvellement.

`ThrottleMiddleware` passe avant l'authentification, donc il freine aussi les
requêtes sans jeton.

### Rate limit

- Global : `THROTTLE_LIMIT` requêtes par minute, par route et par IP (300 par défaut).
- Connexion : `LOGIN_THROTTLE_LIMIT` (5 par défaut).
- Au-delà : `429 { message: "Trop de requêtes, veuillez réessayer dans quelques instants" }`.
- L'IP est celle du visiteur si `TRUST_PROXY` est bon (voir [Déploiement](#déploiement)).
- Compteurs en mémoire, donc remis à zéro au redémarrage et pas partagés entre instances.

### CORS

- `CORS_ORIGIN` renseigné : seulement ces origines exactes (séparées par des virgules).
- `CORS_ORIGIN` vide : seulement `http://localhost:<port>` et
  `http://127.0.0.1:<port>`. Pour ouvrir le front depuis un autre appareil,
  ajouter son adresse exacte dans `CORS_ORIGIN`.
- Credentials autorisés (nécessaire pour le cookie), méthodes
  `GET/POST/PATCH/DELETE`, en-têtes `Content-Type` et `Authorization`,
  préflight en cache 10 minutes.

Le CORS n'est appliqué que par les navigateurs. `curl` l'ignore : ce qui
protège l'API, c'est le JWT.

### En-têtes de sécurité

Les mêmes que `helmet()` avec `crossOriginResourcePolicy: cross-origin` (le
front est sur une autre origine et doit pouvoir lire les réponses) :
`X-Content-Type-Options`, `Strict-Transport-Security`, une CSP (sauf sur
`/swagger`), etc. Kestrel n'envoie pas d'en-tête `Server`.

### Format des erreurs

Toutes les erreurs renvoient `{ "statusCode": <n>, "message": "<texte>" }` :

- les erreurs prévues (`400`, `401`, `404`, `409`, `429`...) gardent leur message ;
- les erreurs MySQL courantes (doublon, référence invalide, valeur trop
  longue...) sont traduites en statut et message lisibles ;
- le reste est loggé côté serveur et le client reçoit
  `500 { "message": "Erreur interne du serveur" }`, sans trace.

### Mots de passe SMTP

`GET /entite` ne renvoie plus `MDP_SMTP` ni `MDP_SMTP_Planning`, seulement
`MDP_SMTP_defini` et `MDP_SMTP_Planning_defini`. `PATCH /entite` ignore un mot
de passe vide (inchangé) et ne remplace la valeur que si on en envoie une
nouvelle.

## Front

### Fichiers (`src/Web`)

| Fichier | Rôle |
|---|---|
| `Services/ApiClient.cs` | Point d'appel unique : préfixe l'URL, joint le jeton, sur un 401 renouvelle la session et rejoue, lève une `ApiException` avec le message du serveur |
| `Services/AuthSession.cs` | Jeton en mémoire, connexion, renouvellement (une seule requête même si plusieurs appels arrivent en même temps), connexion automatique, déconnexion |
| `Services/ApiException.cs` | Exception et lecture du message d'erreur du serveur |
| `Services/AppConfig.cs` | `ApiUrl` et l'interrupteur `LoginScreen` |
| `Auth/AuthGate.razor` | N'affiche l'appli qu'une fois la session ouverte (automatiquement, ou via l'écran de connexion s'il est activé) |
| `Auth/AuthState.cs` | Ce qui est transmis aux pages (`CascadingValue`) : login, `CanSignOut`, `SignOutAsync` |
| `Auth/LoginPage.razor` | Écran de connexion (pas affiché tant que `LoginScreen` est à `false`) |
| `Layout/Sidebar.razor` | Utilisateur et bouton « Se déconnecter », seulement si l'écran de connexion est activé |

### Règle

Tous les appels à l'API passent par `ApiClient`. Un `HttpClient` utilisé
directement partirait sans jeton et prendrait un `401`.

### Erreurs

Les messages du serveur (« Trop de requêtes... », « Identifiant ou mot de
passe incorrect », « Vous devez renseigner... ») arrivent tels quels dans
`ApiException.Message` et sont affichés à l'utilisateur.

### Impression du PDF

Un lien `<a href>` ne peut pas envoyer `Authorization`. Le bouton de la fiche
contrat (`Features/Contrats/ContratPdfButton.razor`) télécharge donc le PDF
via `ApiClient`, en fait un blob et l'ouvre dans un nouvel onglet. L'onglet
est ouvert pendant le clic, avant l'attente réseau, sinon le navigateur le
bloque comme popup.

## Écran de connexion désactivé

Tant que la connexion est factice, demander un identifiant ne sert à rien.
L'écran est donc gardé mais pas utilisé, et le front ouvre la session lui-même
(`AuthGate.razor`, `AuthSession.cs`) :

1. Au chargement, le front tente `POST /auth/refresh`.
2. Sans session valide, il appelle `LoginAutomaticallyAsync()`, qui se
   connecte avec un identifiant bidon (`utilisateur` /
   `session-automatique`, pas de vrais identifiants). Les appels simultanés
   partagent une seule connexion pour ne pas griller la limite de 5
   tentatives par minute.
3. Ensuite tout est normal : jeton en mémoire, envoyé dans `Authorization`,
   renouvelé à l'expiration. Le cookie évite de rouvrir une session à chaque
   rechargement (8 h).

À l'écran : pas d'écran de connexion, pas de nom d'utilisateur ni de bouton
« Se déconnecter » (se déconnecter rouvrirait une session tout de suite).

Si le serveur ne répond pas ou que la limite de connexion est atteinte, on
affiche « Impossible d'ouvrir la session : ... » avec un bouton « Réessayer ».

Côté API rien ne change : une requête sans jeton prend toujours `401`, rate
limit et renouvellement fonctionnent pareil.

Pour réactiver l'écran : `"LoginScreen": true` dans
`src/Web/wwwroot/appsettings.json` (ou `LOGIN_SCREEN=true` pour l'image
Docker du front). `LoginPage` remplace alors la session automatique.

## Variables d'environnement

API (documentées dans `src/Api/.env.example`) :

| Variable | Défaut | Rôle |
|---|---|---|
| `JWT_SECRET` | secret temporaire aléatoire | Secret de signature, 32 caractères minimum (`openssl rand -base64 48`). Sans lui, un secret temporaire est généré avec un warning et les sessions sont perdues à chaque redémarrage |
| `ACCESS_TOKEN_TTL_SECONDS` | `900` (15 min) | Durée du jeton d'accès |
| `COOKIE_SAMESITE` | `lax` | `lax`, `strict` ou `none`. `none` impose HTTPS (`Secure` forcé) |
| `COOKIE_SECURE` | `false` | `true` pour n'envoyer le cookie qu'en HTTPS |
| `CORS_ORIGIN` | localhost | Origines autorisées, séparées par des virgules |
| `TRUST_PROXY` | auto | Nombre de proxys devant l'API. `1` si `RAILWAY_ENVIRONMENT` est défini (Railway), sinon `0` |
| `THROTTLE_LIMIT` | `300` | Requêtes/min/IP |
| `LOGIN_THROTTLE_LIMIT` | `5` | Tentatives de connexion/min/IP |
| `CGV_URL` | vide | Lien « ICI » du pied de page des PDF de contrat (rien à voir avec l'auth) |

Front (`src/Web/wwwroot/appsettings.json`, ou `API_URL` / `LOGIN_SCREEN` pour
l'image Docker) :

| Clé | Défaut | Rôle |
|---|---|---|
| `ApiUrl` | `http(s)://<hôte>:3000` | Adresse de l'API |
| `LoginScreen` | `false` | `true` affiche l'écran de connexion, sinon session automatique |

## Déploiement

### Railway

1. Définir `JWT_SECRET` (32 caractères ou plus) sur le service API.
2. Mettre dans `CORS_ORIGIN` l'adresse exacte du front.
3. Si front et API sont sur des domaines différents (deux sous-domaines
   Railway par exemple) : `COOKIE_SAMESITE=none`. S'ils sont sur le même
   site (`app.exemple.be` et `api.exemple.be`), garder `lax`.
4. `TRUST_PROXY` est détecté tout seul. Sinon tous les visiteurs auraient
   l'IP du proxy et partageraient la même limite.
5. Côté front, `API_URL` doit pointer vers l'API.

Un cookie mal réglé ne casse rien : le renouvellement silencieux échoue et on
se reconnecte à chaque rechargement de page.

### Serveur dédié

- Front et API en HTTPS, si possible sur le même site (sous-domaines d'un
  même domaine) pour garder `COOKIE_SAMESITE=lax`.
- Derrière Nginx ou un autre proxy : `TRUST_PROXY=1` (ou le nombre de proxys).
- `COOKIE_SECURE=true`.

## Passer à une vraie authentification

Il n'y a qu'une méthode à changer : `TokenService.AuthenticateAsync()` dans
`src/Api/Auth/TokenService.cs`. Les jetons, le cookie et le front restent
comme ils sont.

Aujourd'hui :

```csharp
public Task<AuthUser?> AuthenticateAsync(string login, string password) => Task.FromResult<AuthUser?>(new AuthUser(login));
```

À faire :

1. Regarder le format de `utilisateurs.Password_hash`. Si c'est un format
   WinDev et pas un hachage standard (argon2, bcrypt), il faudra réinitialiser
   les mots de passe (par exemple avec une commande qui en définit un par
   utilisateur).
2. Chercher l'utilisateur (`Login`, `Actif = 1`), vérifier le mot de passe avec
   la lib de hachage choisie, renvoyer `null` si c'est faux. Même message pour
   « login inconnu » et « mauvais mot de passe », pour ne pas révéler quels
   logins existent.
3. Renvoyer `new AuthUser(user.IDUTILISATEURS.ToString())` : `sub` devient
   l'id numérique, utilisable pour « créé par / modifié par ».
4. Dans `POST /auth/refresh`, revérifier `Actif` en base pour qu'un utilisateur
   désactivé ne puisse plus renouveler.
5. Si on veut afficher le nom, l'exposer par une route dédiée (le jeton ne
   porte que `sub`, exprès).
6. Côté front : `LoginScreen` à `true`, puis supprimer
   `LoginAutomaticallyAsync()` et l'identifiant bidon (`AutoLogin`,
   `AutoPassword`) dans `AuthSession.cs`, et la branche session automatique
   d'`AuthGate.razor`.

## Tester

Remplacer `<API>` par l'adresse de l'API (`http://localhost:3000` en local).

```bash
# 1. Sans jeton : 401
curl -i <API>/contrats

# 2. Connexion : renvoie accessToken et pose le cookie
curl -i -c cookies.txt -X POST <API>/auth/login \
  -H "Content-Type: application/json" -d '{"login":"test","password":"test"}'

# 3. Avec jeton : 200
curl <API>/contrats -H "Authorization: Bearer <accessToken>"

# 4. Renouvellement avec le cookie
curl -b cookies.txt -X POST <API>/auth/refresh

# 5. Limitation : la 6e tentative de connexion dans la minute répond 429
for i in 1 2 3 4 5 6; do curl -s -o /dev/null -w "%{http_code}\n" -X POST <API>/auth/login \
  -H "Content-Type: application/json" -d '{"login":"a","password":"b"}'; done

# 6. CORS : origine refusée = pas d'en-tête Access-Control-Allow-Origin
curl -i -X OPTIONS <API>/contrats -H "Origin: https://autre-site.example" \
  -H "Access-Control-Request-Method: GET"
```

Pour tester le renouvellement sans attendre 15 minutes, lancer l'API avec
`ACCESS_TOKEN_TTL_SECONDS=4`, se connecter, attendre quelques secondes puis
ouvrir une page : la requête prend un vrai `401`, le front renouvelle et
rejoue.

## Limites connues

- Connexion factice (voir plus haut).
- Pas de rôles ni de permissions : un utilisateur connecté a accès à toute l'API.
- Pas de révocation : un jeton de renouvellement reste valable jusqu'à son
  expiration (8 h). « Se déconnecter » efface le cookie dans le navigateur
  mais ne l'invalide pas côté serveur. Revérifier `Actif` au renouvellement
  limiterait le risque, une vraie révocation demanderait une table de jetons.
- Les champs « créé par / modifié par » restent à `NULL` : à remplir avec
  `sub` une fois la vraie authentification en place.
- `COOKIE_SAMESITE=none` pas testé ailleurs qu'en local.
