// Adresses liées par IDADRESSES (table `adresses`). Règle commune à toutes
// les fiches : l'utilisateur saisit les champs d'adresse, jamais l'id.
//   - création de la fiche : une ligne `adresses` est créée avec les valeurs
//     saisies (sauf adresse entièrement vide) et son id est affecté à la fiche ;
//   - modification : la ligne pointée par l'IDADRESSES de la fiche est mise à
//     jour (créée puis affectée si la fiche n'en avait pas encore).
import { z } from 'zod';
import { Prisma } from '../generated/prisma/client.js';
import { text } from './validation.js';

export const adresseFieldNames = ['Adresse1', 'Adresse2', 'Adresse3', 'CP', 'Localite', 'Pays', 'Pays_full_name'] as const;
type AdresseField = (typeof adresseFieldNames)[number];
export type AdresseData = Partial<Record<AdresseField, string | null>>;

export const adresseSelect = {
  Adresse1: true,
  Adresse2: true,
  Adresse3: true,
  CP: true,
  Localite: true,
  Pays: true,
  Pays_full_name: true,
} satisfies Prisma.AdresseSelect;

// Champs d'adresse d'un DTO (tous optionnels, comme en modification partielle).
export const AdresseDto = z.object({
  Adresse1: text.optional(),
  Adresse2: text.optional(),
  Adresse3: text.optional(),
  CP: text.optional(),
  Localite: text.optional(),
  Pays: text.optional(),
  Pays_full_name: text.optional(),
});
export type AdresseDto = z.infer<typeof AdresseDto>;

// Seuls les champs envoyés ; texte vide enregistré NULL.
export function pickAdresse(source: Partial<Record<AdresseField, string | null | undefined>>): AdresseData {
  return Object.fromEntries(
    adresseFieldNames
      .filter((field) => source[field] !== undefined)
      .map((field) => [field, source[field]?.trim() ? source[field] : null]),
  );
}

// Crée ou met à jour l'adresse d'une fiche et renvoie l'IDADRESSES à lui
// affecter. `currentId` : IDADRESSES actuel de la fiche (null/0 = aucun —
// 0 est le défaut DB des colonnes IDADRESSES). Aucun champ envoyé : inchangé.
export async function saveAdresse(
  tx: Prisma.TransactionClient,
  currentId: bigint | null | undefined,
  data: AdresseData,
): Promise<bigint | null> {
  const id = currentId || null;
  if (Object.keys(data).length === 0) return id;
  if (id) {
    await tx.adresse.update({ where: { IDADRESSES: id }, data: { ...data, Date_heure_modification: new Date() } });
    return id;
  }
  // Une adresse entièrement vide n'est pas créée.
  if (!Object.values(data).some((value) => value?.trim())) return null;
  const created = await tx.adresse.create({
    // IDUTILISATEURS_* ont un défaut DB de 0 (clé étrangère) : NULL.
    data: { ...data, Date_heure_creation: new Date(), IDUTILISATEURS_createur: null, IDUTILISATEURS_modificateur: null },
  });
  return created.IDADRESSES;
}
