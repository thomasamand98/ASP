import { z } from 'zod';
import { date } from '../common/validation.js';

// Les dates « du jour » sont envoyées par le navigateur (AAAA-MM-JJ, date
// locale de l'utilisateur) plutôt que calculées ici : le serveur peut
// tourner dans un autre fuseau horaire (Cloud Run = UTC).
const requiredDay = date.refine((value) => value !== '', 'champ obligatoire');

export const TraiterTacheDto = z.object({
  Date_traitement: requiredDay,
});
export type TraiterTacheDto = z.infer<typeof TraiterTacheDto>;

export const ReprogrammerTacheDto = z.object({
  Date_tache: requiredDay,
});
export type ReprogrammerTacheDto = z.infer<typeof ReprogrammerTacheDto>;
