namespace Horizon.Web.Features.Ocr;

// schéma envoyé en mode strict : tout dans required, pas de propriétés en plus
public sealed record OcrPreset(string Id, string Label, string Prompt, string Schema)
{
    private const string TransportPrompt = """"
Extrait du document :

"immatriculation_tracteur" : l'immatriculation du tracteur et "immatriculation_remorque" l'immatriculation de la remorque (exemple AA123AA) sans espace,
"cmr" : le numéro de cmr (numéro document de type CMR, lettre de voiture), pas le numero de bon de livraison ou autres documents
Pour le chargement (prise en charge,CHGT), extrait la date et l'heure de début (DATE HEURE IN, Date d'entrée) et de fin (DATE HEURE OUT, Date de sortie) ainsi que la durée et la quantité "quantite_chargement"  dans le document de chargement (prendre le poids net, KG NET ou Quantité) et le numéro de bon (exemple : 907703).
Pour le déchargement (livraison,DECHGT), extrait la date et l'heure de début (DATE HEURE IN, Date d'entrée) et de fin (DATE HEURE OUT, Date de sortie) ainsi que la durée et la quantité "quantite_dechargement" dans le document de déhargement  (prendre le poids net, KG NET ou quantité) et le numéro de bon (exemple : 907703).
différencie bien la quantité du chargement, du déchargement.

IMPORTANT : Un transport commence par le chargement, puis on effectue le déchargement (de facon chronologique).
Le chargement doit etre avant le déchargement au niveau des dates.
Le chargement et le déchargement ne sont jamais sur le même document, ne mélange pas les documents.
Tu devrais avoir 3 documents, 1 cmr, 1 premier ticket de pesée pour le chargement et 1 deuxième ticket de pesée pour le déchargement.
Si tu une remarque un soucis pendant le transport, merci de remplir l'attribut remarques.

Le "numero_bon" est souvent un numéro de Bon de livraison ou un numéro de ticket de pesée ou numéro de Bordereau de livraison ou une reference ou "No Suite" ou weegticket NR, si sur le document tu as "Order" avec l'une des valeurs précédentes tu prends "Order". Ne pas prendre le "No badge" ou le numero de CMR.
Si tu ne trouve pas de numero de bon, ne remplit pas l'attribut "numero_bon".

les dates doivent être formattées sous format : "JJ/MM/AAAA", les heures : "HH:MM"
Si le cmr est illisible ou manquant, dis le moi dans l'attribut debug.

Si tu ne trouve pas le document du chargement , alors dis le moi dans l'attribut debug.
Si tu ne trouve pas le document du déchargement, alors dis le moi dans l'attribut debug.

une quantité négative est normale.

essaye de faire la relation avec la date et l'heure de chargement trouvée sur le cmr et la date sur le ticket pesée ou autres document.
essaye de faire la relation avec la date de l'heure du déchargement trouvée sur le cmr et la date sur le ticket pesée ou autres document.

Si tu ne trouve que 2 documents : lettre de voiture (cmr) ET (Ticket de pesée ou Bordereau de pesée), alors le document qui n'est pas une lettre de voiture (CMR) est un déchargement.
si tu trouve 2 documents dont aucun document CMR lettre de voyage, alors c'est un chargement puis un déchargement de façon chronologique.

donne moi un indice de confiance en pourcentage pour le résultat  dans l'attribut "confiance"  dans informations

le numéro de cmr est composé de 8 chiffres
"""";

    private const string TransportSchema = """"
{
  "type": "object",
  "properties": {
    "chargement": {
      "type": "object",
      "properties": {
        "date_debut_chargement": {
          "type": "string",
          "description": "JJ/MM/AAAA"
        },
        "heure_debut_chargement": {
          "type": "string",
          "description": "HH:MM"
        },
        "date_fin_chargement": {
          "type": "string",
          "description": "JJ/MM/AAAA"
        },
        "heure_fin_chargement": {
          "type": "string",
          "description": "HH:MM"
        },
        "quantite_chargement": {
          "type": "number",
          "description": "Poids net (KG NET ou Quantité)"
        },
        "numero_bon_chargement": {
          "type": "string"
        }
      },
      "required": [
        "date_debut_chargement",
        "heure_debut_chargement",
        "date_fin_chargement",
        "heure_fin_chargement",
        "quantite_chargement",
        "numero_bon_chargement"
      ],
      "additionalProperties": false
    },
    "dechargement": {
      "type": "object",
      "properties": {
        "date_debut_dechargement": {
          "type": "string",
          "description": "JJ/MM/AAAA"
        },
        "heure_debut_dechargement": {
          "type": "string",
          "description": "HH:MM"
        },
        "date_fin_dechargement": {
          "type": "string",
          "description": "JJ/MM/AAAA"
        },
        "heure_fin_dechargement": {
          "type": "string",
          "description": "HH:MM"
        },
        "quantite_dechargement": {
          "type": "number",
          "description": "Poids net (KG NET ou Quantité)"
        },
        "numero_bon_dechargement": {
          "type": "string"
        }
      },
      "required": [
        "date_debut_dechargement",
        "heure_debut_dechargement",
        "date_fin_dechargement",
        "heure_fin_dechargement",
        "quantite_dechargement",
        "numero_bon_dechargement"
      ],
      "additionalProperties": false
    },
    "vehicules": {
      "type": "object",
      "properties": {
        "immatriculation_tracteur": {
          "type": "string",
          "description": "Sans espace, ex. AA123AA"
        },
        "immatriculation_remorque": {
          "type": "string",
          "description": "Sans espace, ex. AA123AA"
        }
      },
      "required": [
        "immatriculation_tracteur",
        "immatriculation_remorque"
      ],
      "additionalProperties": false
    },
    "informations": {
      "type": "object",
      "properties": {
        "cmr": {
          "type": "string",
          "description": "Numéro de CMR, 8 chiffres"
        },
        "remarques": {
          "type": "string"
        },
        "debug": {
          "type": "string"
        },
        "confiance": {
          "type": "number",
          "description": "Indice de confiance en pourcentage (0-100)"
        }
      },
      "required": [
        "cmr",
        "remarques",
        "debug",
        "confiance"
      ],
      "additionalProperties": false
    }
  },
  "required": [
    "chargement",
    "dechargement",
    "vehicules",
    "informations"
  ],
  "additionalProperties": false
}
"""";

    public static readonly IReadOnlyList<OcrPreset> All =
    [
        new("aucun", "Aucune extraction (OCR seul)", "", ""),
        new("transport", "Transport : CMR + tickets de pesée", TransportPrompt, TransportSchema),
    ];

    public static OcrPreset Default => All.First(p => p.Id == "transport");
}
