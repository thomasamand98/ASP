namespace Horizon.Web.Features.Documents;

// HTML identique à celui de docEditor (interop.js), sinon jetons non reconnus
public static class StarterTemplate
{
    private static string Chip(string path, string label, string kind = "text") => TemplateChipHtml.Chip(new MergeField(path, label, kind));

    private static string RepeatTag(string label) => TemplateChipHtml.RepeatTag(label);

    // champ image retiré à la fusion si pas de logo
    public static readonly string Header = $"""
        <div class="dte-header-row">
          {Chip("Entite.Logo", "Logo", "image")}
          <p class="dte-address-block">
            {Chip("Entite.Nom_societe", "Raison sociale")}<br>
            {Chip("Entite.Adresse.Adresse1", "Adresse")}<br>
            {Chip("Entite.Adresse.CP", "Code postal")} {Chip("Entite.Adresse.Localite", "Localité")}
          </p>
        </div>
        """;

    public static readonly string Content = $$"""
        <p class="dte-address-block dte-address-right">
          {{Chip("Contrat.Societe.Nom_societe", "Nom société")}}<br>
          {{Chip("Contrat.Societe.Adresse.Adresse1", "Adresse")}}<br>
          {{Chip("Contrat.Societe.Adresse.CP", "Code postal")}} {{Chip("Contrat.Societe.Adresse.Localite", "Localité")}}<br>
          {{Chip("Contrat.Societe.Adresse.Pays_full_name", "Pays")}}
        </p>

        <p>Fait à {{Chip("Entite.Adresse.Localite", "Localité")}}, le {{Chip("Divers.Date_edition", "Date d’édition", "date")}}</p>

        <div class="dte-header-row">
          <p><strong>N° Contrat :</strong> {{Chip("Contrat.Num_contrat", "N° contrat")}}</p>
          <p><strong>N° Client :</strong> {{Chip("Contrat.Societe.Client.Numero_client", "N° client")}} &nbsp; <strong>N° T.V.A. :</strong> {{Chip("Contrat.Societe.TVA", "N° TVA")}}</p>
        </div>

        <p><strong>Période du</strong> {{Chip("Contrat.Date_debut", "Date de début", "date")}} <strong>au</strong> {{Chip("Contrat.Date_fin", "Date de fin", "date")}} &nbsp; <strong>Délai paiement :</strong> {{Chip("Contrat.Societe.Client.Delai_paiement", "Délai de paiement")}}</p>

        <h2>Prestations</h2>

        <div class="dte-repeat" data-repeat-list="Contrat.Prestations[]" data-repeat-label="Prestations">
          {{RepeatTag("Prestations")}}
          <table class="dte-table">
            <thead>
              <tr>
                <th>Type</th>
                <th>Description</th>
                <th>Unité</th>
                <th>P.U.</th>
              </tr>
            </thead>
            <tbody class="dte-repeat-row">
              <tr>
                <td>{{Chip("Contrat.Prestations[].Type", "Type")}}</td>
                <td>{{Chip("Contrat.Prestations[].Description_prestation", "Description")}}</td>
                <td>{{Chip("Contrat.Prestations[].Unite", "Unité")}}</td>
                <td>{{Chip("Contrat.Prestations[].Prix_unitaire", "P.U.", "number")}}</td>
              </tr>
              <tr>
                <td class="dte-repeat-subcell" colspan="4">
                  <div class="dte-repeat dte-repeat-nested" data-repeat-list="Contrat.Prestations[].PrestationSupplementaires[]" data-repeat-label="Prestations supplémentaires">
                    {{RepeatTag("Prestations supplémentaires")}}
                    <table class="dte-table dte-table-nested">
                      <thead>
                        <tr>
                          <th>Description</th>
                          <th>Unité</th>
                          <th>P.U.</th>
                        </tr>
                      </thead>
                      <tbody class="dte-repeat-row">
                        <tr>
                          <td>{{Chip("Contrat.Prestations[].PrestationSupplementaires[].Description_prestation", "Description")}}</td>
                          <td>{{Chip("Contrat.Prestations[].PrestationSupplementaires[].Unite", "Unité")}}</td>
                          <td>{{Chip("Contrat.Prestations[].PrestationSupplementaires[].Prix_unitaire", "P.U.", "number")}}</td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <p class="dte-muted">La signature de la présente offre sous-entend la bonne connaissance et l'acceptation de nos conditions générales de vente co-annexées.</p>

        <div class="dte-header-row dte-signature-row">
          <p>Le client<br>Pour accord</p>
          <p>Pour {{Chip("Entite.Nom_societe", "Raison sociale")}}<br>{{Chip("Entite.Signataire", "Signataire")}}</p>
        </div>
        """;

    // pageNumber / totalPages remplis par Chromium
    public static readonly string Footer = $"""
        <div class="dte-header-row">
          <p>Nos conditions générales de vente sont consultables : ICI &nbsp;—&nbsp; {Chip("Entite.Email_contact", "E-mail")}</p>
          <p><span class="dte-chip dte-chip-pagenum" contenteditable="false">Page <span class="pageNumber"></span> / <span class="totalPages"></span></span></p>
          <p><strong>T.V.A. :</strong> {Chip("Entite.Num_TVA", "N° TVA")} &nbsp; {Chip("Entite.Nom_Banque", "Banque")} : {Chip("Entite.Iban", "IBAN")}</p>
        </div>
        """;
}
