# Améliorations repérées pendant la migration Blazor

La migration du front React vers Blazor WebAssembly a gardé la logique métier
et le contrat de l'API **à l'identique**, y compris les comportements qui
ressemblent à des défauts. Ce document liste ce qui mériterait d'être corrigé
ou amélioré ensuite, avec l'endroit concerné. Rien de ce qui suit n'a été
appliqué.

## Défauts apparents conservés

| Écran / zone | Constat | Piste |
|---|---|---|
| Commercial › Société, onglet Principale | Créer un fournisseur depuis la fiche société répond 409. | Vérifier la contrainte côté API (création du fournisseur lié) et le message renvoyé. |
| Gestion documentaire › éditeur | En mode code HTML, « Tableau libre », « Saut de page » et « Numéro de page » écrivent dans la zone visuelle masquée : l'ajout est perdu en sortant du mode code (le textarea fait foi). Les champs de fusion et blocs répétables, eux, sont bien insérés dans le code. | Désactiver ces boutons en mode code, ou les insérer dans le textarea comme les champs. |
| Gestion personnel › Pointage | « H. total » et « H. travaillé » sont des estimations calculées à l'affichage (fin − début, moins la coupure, sans les pauses), pas des valeurs stockées. Le code d'origine le signale comme « à ajuster si la vraie règle métier diffère ». | Faire valider la règle de calcul par le métier. |
| Configuration › Performance | La description du mode « Calcul (CPU) » parle encore de Node.js (« un seul fil d'exécution : plus aucune requête n'est servie »). Avec l'API ASP.NET Core, les autres requêtes continuent d'être servies pendant le calcul. | Mettre le texte à jour. |

## Points d'attention

| Zone | Constat | Piste |
|---|---|---|
| Configuration › Performance, transfert | La durée mesurée inclut l'analyse du JSON par .NET dans le navigateur (≈ 100 ms pour 200 Ko en build Debug, contre ≈ 5 ms avec `JSON.parse` dans React). C'est le coût réel pour une application Blazor, qui exploite les données côté .NET. | Mesurer en build Release (image Docker) ; pour les grosses listes, paginer côté API plutôt que tout charger. |
| Gestion documentaire | L'éditeur s'appuie sur `document.execCommand`, déprécié (mais toujours pris en charge par les navigateurs). | À remplacer si l'éditeur doit évoluer. |
