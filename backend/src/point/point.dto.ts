import { z } from 'zod';
import { id, num, text } from '../common/validation.js';

// Plage horaire d'ouverture du point (table `grilles_horaires`, liée par
// IDLIENS = IDPOINTS). Jour_semaine : 1 = lundi … 7 = dimanche (convention
// WinDev) ; heures en « HH:MM ».
const hhmm = z.string().regex(/^([01]\d|2[0-3]):[0-5]\d$/, 'heure « HH:MM » attendue');
export const HorairePointDto = z
  .object({
    Jour_semaine: z.number().int().min(1).max(7),
    Heure_debut: hhmm,
    Heure_fin: hhmm,
  })
  .refine((h) => h.Heure_fin > h.Heure_debut, { message: 'la fin doit suivre le début', path: ['Heure_fin'] });
export type HorairePointDto = z.infer<typeof HorairePointDto>;

// Champs qu'un client peut envoyer pour créer/modifier un point.
// IDSOCIETES/IDCONTACTS_DEFAUTS sont des strings côté JSON (BigInt non
// sérialisable) — converties dans point.service.ts. IDADRESSES est géré par
// le serveur (voir common/adresse.ts). Archive est un code 0/1 côté vrai
// modèle Prisma (pas un booléen).
export const CreatePointDto = z.object({
  Libelle: text.optional(),
  Nom_societe: text.optional(),
  Telephone: text.optional(),
  IDSOCIETES: id.optional(),
  Archive: num,
  Lien_googleMap: text.optional(),
  Instruction: text.optional(),
  IDCONTACTS_DEFAUTS: id.optional(),
  // Adresse du point (table `adresses`, liée par IDADRESSES) : champs
  // aplatis, enregistrés à part par point.service.ts.
  Adresse1: text.optional(),
  Adresse2: text.optional(),
  Adresse3: text.optional(),
  CP: text.optional(),
  Localite: text.optional(),
  Pays: text.optional(),
  Pays_full_name: text.optional(),
  // Plages horaires : remplacent toutes celles du point quand fournies.
  Horaires: z.array(HorairePointDto).optional(),
});
export type CreatePointDto = z.infer<typeof CreatePointDto>;

export const UpdatePointDto = CreatePointDto.partial();
export type UpdatePointDto = z.infer<typeof UpdatePointDto>;

// Lien point ↔ contact (table point_contacts), modifié depuis les cartes
// de l'onglet Contacts de la fiche Point.
export const UpdatePointContactDto = z.object({
  Recevoir_Mail_Planning: num,
});
export type UpdatePointContactDto = z.infer<typeof UpdatePointContactDto>;
