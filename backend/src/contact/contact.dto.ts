import { z } from 'zod';
import { id, num, text } from '../common/validation.js';

// Champs qu'un client peut envoyer pour créer/modifier un contact —
// le sous-ensemble scripturable de contactDetailSelect (contact.service.ts).
// IDADRESSES n'en fait pas partie : il est géré par le serveur (voir
// common/adresse.ts). Personne_physique/Adresse_entreprise sont des codes
// 0/1 côté vrai modèle Prisma (pas des booléens).
export const CreateContactDto = z.object({
  Civilite: text.optional(),
  Nom_contact: text.optional(),
  Prenom_contact: text.optional(),
  Telephone_portable: text.optional(),
  Telephone_fixe: text.optional(),
  Telephone_autre: text.optional(),
  E_mail: text.optional(),
  Remarque: text.optional(),
  Personne_physique: num,
  Adresse_entreprise: num,
  description_telephone: text.optional(),
  // Société principale du contact (liste « Société » de la fiche) : lien
  // SocieteContacts avec sa fonction/service, géré à part du contact
  // lui-même (voir createContact/updateContact dans contact.service.ts).
  // Vide en modification : retire la société principale.
  IDSOCIETES: id.optional(),
  Fonction_contact: text.optional(),
  Service_bureau: text.optional(),
  // Création depuis la fiche Point : le contact est créé lié à ce point
  // (PointContacts). Ignoré en modification.
  IDPOINTS: id.optional(),
  // Adresse propre du contact (table `adresses`, liée par IDADRESSES) :
  // champs aplatis, enregistrés à part par contact.service.ts.
  Adresse1: text.optional(),
  Adresse2: text.optional(),
  Adresse3: text.optional(),
  CP: text.optional(),
  Localite: text.optional(),
  Pays: text.optional(),
  Pays_full_name: text.optional(),
});
export type CreateContactDto = z.infer<typeof CreateContactDto>;

export const UpdateContactDto = CreateContactDto.partial();
export type UpdateContactDto = z.infer<typeof UpdateContactDto>;
