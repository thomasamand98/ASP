import { z } from 'zod';
import { decimal, id, num, text } from '../common/validation.js';
import { AdresseDto } from '../common/adresse.js';

// Champs qu'un client peut envoyer pour créer/modifier une société —
// le sous-ensemble scripturable de societeDetailSelect (societe.service.ts).
// Les adresses (société, facturation client/fournisseur) sont saisies champ
// par champ ; leurs IDADRESSES sont gérés par le serveur (voir
// common/adresse.ts). Prospect/Archive sont des codes 0/1 côté vrai modèle
// Prisma (pas des booléens).
export const CreateSocieteDto = AdresseDto.extend({
  Nom_societe: text.optional(),
  Denomination: text.optional(),
  TVA: text.optional(),
  Activite: text.optional(),
  Site_web: text.optional(),
  Note: text.optional(),
  Prospect: num,
  Archive: num,

  // Données de facturation du Client lié, éditées directement depuis la
  // fiche Société (voir SocieteForm.tsx, sous-onglet « Client ») plutôt que
  // via un sélecteur d'enregistrement existant. Si la société n'a pas
  // encore de Client lié et que `Client_Numero_client` est fourni,
  // societe.service.ts en crée un et le lie automatiquement ; sinon le
  // Client déjà lié est mis à jour avec ces champs.
  Client_Numero_client: text.optional(),
  Client_Delai_paiement: text.optional(),
  Client_Taux_tva: decimal.optional(),
  Client_E_mail_comptabilite: text.optional(),
  Client_Facture_mail: num,
  Client_Adresse_facturation_societe: num,
  Client_Adresse_facturation: AdresseDto.optional(),
  Client_IDCONTACTS_Comptabilite: id.optional(),

  // Idem pour le Fournisseur lié (sous-onglet « Fournisseur »).
  Fournisseur_Numero_fournisseur: text.optional(),
  Fournisseur_Delai_paiement: text.optional(),
  Fournisseur_Taux_tva: decimal.optional(),
  Fournisseur_E_mail_comptabilite: text.optional(),
  Fournisseur_Facture_mail: num,
  Fournisseur_Adresse_facturation_societe: num,
  Fournisseur_Adresse_facturation: AdresseDto.optional(),
  Fournisseur_IDCONTACTS_Comptabilite: id.optional(),
  Fournisseur_Iban: text.optional(),
  Fournisseur_Bic: text.optional(),
});
export type CreateSocieteDto = z.infer<typeof CreateSocieteDto>;

export const UpdateSocieteDto = CreateSocieteDto.partial();
export type UpdateSocieteDto = z.infer<typeof UpdateSocieteDto>;
