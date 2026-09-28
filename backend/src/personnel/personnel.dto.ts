import { z } from 'zod';
import { date, num, text } from '../common/validation.js';

// Champs qu'un client peut envoyer pour créer/modifier un personnel —
// le sous-ensemble scripturable de personnelDetailSelect
// (personnel.service.ts). Les Date_* sont des strings ISO — converties dans
// personnel.service.ts ; IDADRESSES est géré par le serveur (voir
// common/adresse.ts). Routier/Manutention/Atelier sont des codes 0/1 côté
// vrai modèle Prisma (pas des booléens).
export const CreatePersonnelDto = z.object({
  Civilite_Personnel: text.optional(),
  Nom_Personnel: text.optional(),
  Prenom_Personnel: text.optional(),
  Telephone_portable: text.optional(),
  Telephone_fixe: text.optional(),
  Telephone_autre: text.optional(),
  Telephone_professionnel: text.optional(),
  Description_telephone: text.optional(),
  E_mail: text.optional(),
  E_mail_professionnel: text.optional(),
  Num_service_social: text.optional(),
  Num_registre_national: text.optional(),
  Date_naissance: date.optional(),
  Lieu_naissance: text.optional(),
  Pays_Naissance: text.optional(),
  Etat_civil: text.optional(),
  Nbr_personne_charge: num,
  Iban: text.optional(),
  Bic: text.optional(),
  Nom_Banque: text.optional(),
  Qualification: text.optional(),
  Routier: num,
  Manutention: num,
  Atelier: num,
  Commentaire_Personnel: text.optional(),
  Date_validite_selection_medicale: date.optional(),
  Date_validite_carte_chauffeur: date.optional(),
  Date_validite_CAP: date.optional(),
  Date_validite_carte_identite: date.optional(),
  Date_validite_A1: date.optional(),
  Date_validite_SIPSI: date.optional(),
  // Adresse (table `adresses`, liée par IDADRESSES) : champs aplatis,
  // enregistrés à part par personnel.service.ts — comme pour l'Entité.
  Adresse1: text.optional(),
  Adresse2: text.optional(),
  Adresse3: text.optional(),
  CP: text.optional(),
  Localite: text.optional(),
  Pays: text.optional(),
  Pays_full_name: text.optional(),
});
export type CreatePersonnelDto = z.infer<typeof CreatePersonnelDto>;

export const UpdatePersonnelDto = CreatePersonnelDto.partial();
export type UpdatePersonnelDto = z.infer<typeof UpdatePersonnelDto>;
