# Impression PDF

Comment un contrat (ou n'importe quel autre enregistrement) devient un PDF :
le modèle de document (mis en page à la souris), la fusion (les champs
remplacés par les vraies données) et le rendu (Chromium headless).

## Vue d'ensemble

```
 1. Un utilisateur crée ou modifie un modèle dans l'éditeur
    → le HTML est stocké tel quel en base (3 zones : en-tête / contenu / pied)

 2. Il clique sur le bouton PDF d'une fiche contrat
    → GET /contrats/:id/pdf?templateId=X

 3. API :
    ContratController.GetContratPdf
      ContratService.GetContratMergeContextAsync   → contexte de données
      DocumentMergeService.RenderPdfAsync          → charge les 3 zones du modèle
        MergeHtml.Resolve                          → remplace les jetons par les valeurs
        PdfRendererService.RenderPdfAsync          → HTML fini vers PDF (Chromium)
    ← application/pdf

 4. Front : télécharge le PDF (avec le jeton) et l'ouvre dans un nouvel onglet
```

Trois morceaux qui ne se connaissent pas :

| Morceau | Rôle | Ne connaît pas |
|---|---|---|
| Modèle de document (`Features/DocumentTemplates`) | Stocke le HTML des 3 zones | Les données, ni le type de document |
| Fusion (`Features/DocumentMerge`) | Remplace les jetons, produit le PDF | D'où vient le contexte |
| Module métier (`Features/Contrats`, plus tard factures...) | Construit le contexte, expose la route `/pdf` | Comment le HTML est fusionné ou rendu |

## Le modèle de document

Table `document_templates` (entité `Data/Entities/DocumentTemplate.cs`). Trois
zones HTML indépendantes, résolues avec le même contexte :

| Colonne | Rôle |
|---|---|
| `Contenu_entete` | Répété en haut de chaque page (logo par ex.) |
| `Contenu_html` | Contenu principal, affiché une fois |
| `Contenu_pied` | Répété en bas de chaque page (CGV, numéro de page) |
| `Type_document` | Par ex. `CONTRAT`, filtre les modèles proposés sur la fiche contrat |

CRUD classique dans `src/Api/Features/DocumentTemplates/DocumentTemplates.cs` :
`GET /document-templates` (`?type=CONTRAT` optionnel), `GET /document-templates/:id`
(avec le HTML), `POST`, `PATCH /document-templates/:id`, `DELETE /document-templates/:id`.

## L'éditeur (front)

`src/Web/Features/Documents/DocumentTemplateEditor.razor` : éditeur WYSIWYG
avec trois zones `contentEditable` et une palette de champs à glisser-déposer
(`MergeFieldPalette.razor`). Il insère dans le HTML des éléments avec des
attributs `data-*` que la fusion reconnaît ensuite. Tout ce qui touche au DOM
sous le curseur (sélection, insertion, glisser-déposer) est en JS dans
`wwwroot/js/interop.js` (`horizon.docEditor`), appelé via `DocEditorJs.cs`.

Le catalogue des champs est dans `MergeFields.cs`, un chemin par champ, avec
les noms des entités de l'API. Un groupe « liste » a un `ListPath` (par ex.
`Contrat.Prestations[]`) et peut avoir des sous-listes (`Children`, par ex.
`Contrat.Prestations[].PrestationSupplementaires[]`).

Un champ simple est déposé comme un jeton :

```html
<span class="dte-chip" data-merge-field="Contrat.Num_contrat" data-kind="text">
  N° contrat<button data-chip-remove>×</button>
</span>
```

Un groupe « liste » est déposé comme un tableau répétable. La ligne
`.dte-repeat-row` sera copiée une fois par élément à la fusion :

```html
<div class="dte-repeat" data-repeat-list="Contrat.Prestations[]">
  <table class="dte-table">
    <thead><tr><th>Type</th><th>P.U.</th></tr></thead>
    <tbody>
      <tr class="dte-repeat-row">
        <td><span class="dte-chip" data-merge-field="Contrat.Prestations[].Type" data-kind="text">Type</span></td>
        <td><span class="dte-chip" data-merge-field="Contrat.Prestations[].Prix_unitaire" data-kind="number">P.U.</span></td>
      </tr>
    </tbody>
  </table>
</div>
```

À l'enregistrement, l'éditeur envoie juste le HTML des trois zones par
`PATCH /document-templates/:id`. Le front n'interprète rien, c'est du HTML
brut stocké tel quel. Au chargement dans l'éditeur, il passe par
`TemplateHtmlSanitizer.cs`.

## La fusion (API)

`src/Api/Features/DocumentMerge/MergeHtml.cs`, méthode
`MergeHtml.Resolve(templateHtml, context)`. Pas d'accès à la base : elle prend
le HTML d'un modèle et un contexte déjà construit, et renvoie le HTML fini
(parsing avec AngleSharp).

1. Les blocs `[data-repeat-list]` sont développés : une copie de
   `.dte-repeat-row` par élément de la liste pointée, chaque copie résolue
   contre son élément. Les blocs imbriqués (suppléments d'une prestation)
   sont gérés.
2. Chaque `[data-merge-field="Chemin"]` est remplacé par sa valeur formatée.
3. Les restes de l'éditeur (`contenteditable`, boutons de suppression des
   jetons...) sont retirés.

Exemple, avec ce contexte :

```json
{
  "Contrat": {
    "Num_contrat": "C-2026-042",
    "Prestations": [
      { "Type": "Transport", "Prix_unitaire": 150 },
      { "Type": "Stockage", "Prix_unitaire": 80 }
    ]
  }
}
```

le jeton `Contrat.Num_contrat` devient `C-2026-042`, et la ligne du tableau
`Contrat.Prestations[]` est copiée deux fois, une par prestation.

`data-kind` règle le format, à la belge : `date` en `dd/MM/yyyy` (jour dans le
fuseau du serveur), `number` avec deux décimales, virgule et espace fine
comme séparateur de milliers, `image` remplace le jeton par une vraie balise
`<img>` (data URI, pour le logo).

`DocumentMergeService` (dans `PdfRendererService.cs`) charge le modèle, résout
les trois zones avec le même contexte et passe le résultat au rendu.

## Le rendu (PuppeteerSharp)

`PdfRendererService` transforme le HTML fini en PDF avec Chromium headless
(PuppeteerSharp). On a le vrai moteur de mise en page d'un navigateur (sauts
de page, tableaux sur plusieurs pages...) au lieu d'une lib qui approxime.

- Un seul Chromium, lancé à la première demande puis réutilisé. S'il s'arrête,
  il est relancé et le rendu retenté une fois. En cas d'échec : `503`.
- A4, marges de 16 mm à gauche et à droite, 32 mm en haut s'il y a un en-tête
  (18 mm sinon), 20 mm en bas s'il y a un pied (16 mm sinon).
- Le HTML vient d'un modèle saisi par un utilisateur : JavaScript désactivé et
  aucune ressource externe chargée (seules les images en `data:` passent).
- En-tête et pied passent par `HeaderTemplate` / `FooterTemplate`, que Chromium
  rend dans un cadre isolé sans accès aux styles de la page. D'où deux
  feuilles de style séparées, `BodyStylesheet` et `ChromeStylesheet`.

Chromium utilisé : `PUPPETEER_EXECUTABLE_PATH` s'il est défini (l'image Docker
utilise le Chromium de Debian), sinon un Chrome déjà téléchargé par puppeteer
dans `~/.cache/puppeteer`, sinon PuppeteerSharp en télécharge un à la première
génération. Chromium est lancé avec `--disable-dev-shm-usage` parce que Docker
limite `/dev/shm` à 64 Mo, ce qui faisait planter les gros lots.

## La route d'impression d'un contrat

`ContratController.GetContratPdf` dans `src/Api/Features/Contrats/Contrats.cs` :
lit `id` et `templateId`, construit le contexte, appelle
`DocumentMergeService.RenderPdfAsync` et renvoie le PDF en `inline`
(`contrat-<id>.pdf`).

Le contexte vient de `ContratService.GetContratMergeContextAsync`. Il a les
mêmes clés racine que `MergeFields.cs` côté front, pour que tout champ posé
dans l'éditeur se résolve :

- `Entite` : notre société (table `entites`) ;
- `Divers` : `Date_edition` et `CGV_URL` ;
- `Contrat` : le contrat avec société, type de facture, prestations et
  suppléments. Les codes d'unité sont traduits en libellés via la catégorie
  d'énumération `unite_prestation`.

## Le bouton PDF (front)

`src/Web/Features/Contrats/ContratPdfButton.razor`. Un `<a href>` ne peut pas
envoyer `Authorization` et la route est protégée par JWT (voir
[securite-authentification.md](securite-authentification.md)). Le bouton
ouvre donc d'abord un onglet vide (pendant le clic, sinon le navigateur bloque
le popup), télécharge le PDF via `ApiClient`, puis affiche le blob dans cet
onglet. La liste des modèles proposés est celle de type `CONTRAT`.

## Ajouter l'impression à un autre type de document

Par exemple une facture, sans toucher à la fusion :

1. Contexte : une méthode `GetFactureMergeContextAsync(id)` dans le service
   facture, qui renvoie `{ Entite, Divers, Facture: {...} }` (même forme que
   pour les contrats).
2. Champs : un groupe `facture` dans `MergeFields.cs` (`Facture.Num_facture`,
   `Facture.Lignes[].Prix_unitaire`...), avec des chemins qui collent
   exactement aux clés du contexte.
3. Route : `GET factures/{id}/pdf` sur le même modèle que `GetContratPdf`
   (contexte, puis `documentMerge.RenderPdfAsync(templateId, context, ct)`,
   puis `File(pdf, "application/pdf")`).
4. Modèle : en créer un avec `Type_document` = `FACTURE` dans l'éditeur. Il
   apparaîtra dans le sélecteur si la fiche facture a un bouton du même genre
   que `ContratPdfButton`, filtré sur ce type.

`DocumentMergeService` et `PdfRendererService` ne bougent pas, ils ne
connaissent que le contexte.

## Tester

```bash
# 1. Modèles disponibles pour les contrats
curl "<API>/document-templates?type=CONTRAT" -H "Authorization: Bearer <token>"

# 2. PDF d'un contrat avec un modèle existant
curl -o contrat.pdf "<API>/contrats/8/pdf?templateId=6" -H "Authorization: Bearer <token>"

# 3. Modèle inexistant : 404, pas 500
curl "<API>/contrats/8/pdf?templateId=999999" -H "Authorization: Bearer <token>"
# {"statusCode":404,"message":"Modèle de document introuvable"}
```

## Limites connues

- Sur un serveur sans Docker, prévoir les dépendances système de Chromium
  headless (ou pointer `PUPPETEER_EXECUTABLE_PATH` sur un Chromium installé).
- `CGV_URL` (lien des conditions générales en pied de page) est une variable
  d'environnement de l'API, à part du reste de la fusion. Voir
  `src/Api/.env.example`.
