# Améliorations à faire

Pendant le passage de React à Blazor, on a gardé la logique métier et le
contrat de l'API tels quels, y compris ce qui ressemble à des bugs. Ce qui
suit est à corriger ou améliorer plus tard. Rien n'a été fait.

## Défauts gardés tels quels

| Écran / zone | Problème | Piste |
|---|---|---|
| Commercial › Société, onglet Principale | Créer un fournisseur depuis la fiche société renvoie 409. | Vérifier la contrainte côté API (création du fournisseur lié) et le message. |
| Gestion documentaire › éditeur | En mode code HTML, « Tableau libre », « Saut de page » et « Numéro de page » écrivent dans la zone visuelle cachée, et c'est perdu en sortant du mode code (c'est le textarea qui compte). Les champs de fusion et blocs répétables, eux, vont bien dans le code. | Désactiver ces boutons en mode code, ou les insérer dans le textarea comme les champs. |
| Gestion personnel › Pointage | « H. total » et « H. travaillé » sont calculés à l'affichage (fin moins début, moins la coupure, sans les pauses), pas stockés. Le code d'origine dit « à ajuster si la vraie règle métier diffère ». | Faire valider la règle par le métier. |
| Configuration › Performance | La description du mode « Calcul (CPU) » parle encore de Node.js (« un seul fil d'exécution : plus aucune requête n'est servie »). Avec ASP.NET Core les autres requêtes continuent d'être servies pendant le calcul. | Mettre le texte à jour. |

## À surveiller

| Zone | Problème | Piste |
|---|---|---|
| Configuration › Performance, transfert | Le temps mesuré inclut le parsing JSON par .NET dans le navigateur : env. 20 ms pour 200 Ko en Release (130 ms en Debug), contre 5 ms avec `JSON.parse` sous React. C'est le vrai coût côté Blazor, qui travaille ensuite sur les données en .NET. | Pour les grosses listes, paginer côté API au lieu de tout charger. |
| Pages tableau, sur mobile | À 390 px, l'en-tête (titre + Nouveau / Modifier / Supprimer / Projection) déborde et la page défile en horizontal (env. 490 px). Pareil dans l'ancien front React. | Passer les boutons sous le titre en dessous de 480 px. |
| Gestion documentaire | L'éditeur utilise `document.execCommand`, déprécié (mais encore supporté). | À remplacer si l'éditeur doit évoluer. |
