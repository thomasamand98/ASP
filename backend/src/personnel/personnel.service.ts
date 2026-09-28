// ===== MODEL (logique métier) =====
// Va chercher tous les personnels via Prisma. Appelé uniquement par
// PersonnelController — jamais par la View directement.
import { BadRequestException, Injectable } from '@nestjs/common';
import { Prisma } from '../generated/prisma/client.js';
import { PrismaService } from '../prisma/prisma.service.js';
import { buildSearchWhere } from '../common/search.js';
import { andWhere, projectionWhere, type Projection, type ProjectionMap } from '../common/projection.js';
import { serializeBigInt } from '../prisma/serialize-bigint.js';
import { adresseSelect, pickAdresse, saveAdresse } from '../common/adresse.js';
import { CreatePersonnelDto, UpdatePersonnelDto } from './personnel.dto.js';

export const personnelSelect = {
  IDPERSONNELS: true,
  Civilite_Personnel: true,
  Prenom_Personnel: true,
  Nom_Personnel: true,
  Telephone_professionnel: true,
  Telephone_fixe: true,
  Date_validite_CAP: true,
  Date_validite_selection_medicale: true,
  Date_validite_carte_chauffeur: true,
  Num_service_social: true,
  // Adresse « Email planning » affichée sur la fiche chauffeur liée.
  E_mail_professionnel: true,
} satisfies Prisma.PersonnelSelect;

// Select complet pour GET /personnel/:id — ajoute les champs scripturables
// absents de personnelSelect (celui-ci contient déjà toutes les colonnes du
// tableau). L'adresse liée (IDADRESSES) est ajoutée à part par
// getPersonnel, faute de relation Prisma vers Adresse sur ce modèle.
export const personnelDetailSelect = {
  ...personnelSelect,
  Telephone_portable: true,
  Telephone_autre: true,
  Description_telephone: true,
  E_mail: true,
  Num_registre_national: true,
  Date_naissance: true,
  Lieu_naissance: true,
  Pays_Naissance: true,
  Etat_civil: true,
  Nbr_personne_charge: true,
  Iban: true,
  Bic: true,
  Nom_Banque: true,
  Qualification: true,
  Routier: true,
  Manutention: true,
  Atelier: true,
  Commentaire_Personnel: true,
  IDADRESSES: true,
  Date_validite_carte_identite: true,
  Date_validite_A1: true,
  Date_validite_SIPSI: true,
} satisfies Prisma.PersonnelSelect;

const dateFieldNames = [
  'Date_naissance',
  'Date_validite_selection_medicale',
  'Date_validite_carte_chauffeur',
  'Date_validite_CAP',
  'Date_validite_carte_identite',
  'Date_validite_A1',
  'Date_validite_SIPSI',
] as const;

// Projection (voir common/projection.ts) : pour chaque table source, les
// lignes de cette table liées aux ids sélectionnés.
const personnelProjections: ProjectionMap<Prisma.PersonnelWhereInput> = {
  chauffeurs: (ids) => ({ Chauffeurs: { some: { IDCHAUFFEURS: { in: ids } } } }),
  pointage: (ids) => ({ Pointages: { some: { IDPOINTAGES: { in: ids } } } }),
  // Attelages de référence : liés au salarié via sa fiche chauffeur.
  attelages: (ids) => ({ Chauffeurs: { some: { AttelageReferences: { some: { IDATTELAGE_REFERENCE: { in: ids } } } } } }),
};

@Injectable()
export class PersonnelService {
  constructor(private readonly prisma: PrismaService) {}

  // page/pageSize optionnels : omis, le comportement est inchangé (toute la
  // table). Fournis, la requête est découpée avec skip/take et `total`
  // (nombre total de lignes, pas juste celles de la page) est renvoyé à
  // côté pour que le frontend puisse calculer le nombre de pages.
  async getPersonnels(page?: number, pageSize?: number, search?: string, projection?: Projection) {
    const paginate = page !== undefined && pageSize !== undefined && pageSize > 0;
    const filterWhere = buildSearchWhere<Prisma.PersonnelWhereInput>(search, (c) => [
      { Civilite_Personnel: c },
      { Prenom_Personnel: c },
      { Nom_Personnel: c },
      { Telephone_professionnel: c },
      { Telephone_fixe: c },
      { Num_service_social: c },
    ]);
    const where = andWhere<Prisma.PersonnelWhereInput>(filterWhere, projectionWhere(personnelProjections, projection));
    const [personnels, total] = await Promise.all([
      this.prisma.personnel.findMany({
        where,
        orderBy: { IDPERSONNELS: 'asc' },
        select: personnelSelect,
        ...(paginate ? { skip: (page - 1) * pageSize, take: pageSize } : {}),
      }),
      this.prisma.personnel.count({ where }),
    ]);
    return { personnel: serializeBigInt(personnels), total };
  }

  async getPersonnel(id: bigint) {
    const personnel = await this.prisma.personnel.findUniqueOrThrow({
      where: { IDPERSONNELS: id },
      select: personnelDetailSelect,
    });
    // Pas de relation Prisma entre Personnel et Adresse (colonne IDADRESSES
    // sans clé déclarée) : l'adresse est lue à part.
    const adresse = personnel.IDADRESSES
      ? await this.prisma.adresse.findUnique({ where: { IDADRESSES: personnel.IDADRESSES }, select: adresseSelect })
      : null;
    return serializeBigInt({ ...personnel, Adresse: adresse });
  }

  // L'adresse éventuellement saisie est créée dans `adresses` puis liée
  // (voir common/adresse.ts).
  async createPersonnel(dto: CreatePersonnelDto) {
    const { address, data } = toPersonnelData(dto);
    const personnel = await this.prisma.$transaction(async (tx) => {
      const idAdresses = await saveAdresse(tx, null, address);
      return tx.personnel.create({
        // IDADRESSES/IDUTILISATEURS_* ont un défaut DB de 0, qui viole leur
        // contrainte de clé étrangère (aucune ligne d'id 0) quand ils sont
        // omis — mis explicitement à NULL.
        data: { ...data, IDADRESSES: idAdresses, IDUTILISATEURS_createur: null, IDUTILISATEURS_modificateur: null },
        select: personnelSelect,
      });
    });
    return serializeBigInt(personnel);
  }

  // L'adresse liée est mise à jour, ou créée si le personnel n'en avait pas.
  async updatePersonnel(id: bigint, dto: UpdatePersonnelDto) {
    const { address, data } = toPersonnelData(dto);
    const personnel = await this.prisma.$transaction(async (tx) => {
      const current = await tx.personnel.findUniqueOrThrow({ where: { IDPERSONNELS: id }, select: { IDADRESSES: true } });
      const idAdresses = await saveAdresse(tx, current.IDADRESSES, address);
      return tx.personnel.update({
        where: { IDPERSONNELS: id },
        data: { ...data, IDADRESSES: idAdresses, Date_heure_modification: new Date() },
        select: personnelSelect,
      });
    });
    return serializeBigInt(personnel);
  }

  async deletePersonnel(id: bigint) {
    await this.prisma.personnel.delete({ where: { IDPERSONNELS: id } });
  }
}

// Le DTO a les mêmes noms de champs que Prisma. Sont convertis ici :
//   - les Date_* : le formulaire envoie un jour « AAAA-MM-JJ », que Prisma
//     refuse tel quel (colonne DATE → minuit UTC) ; vide = effacé (NULL) ;
//   - les champs d'adresse, séparés du reste pour la table `adresses` ;
//   - les textes vides : le formulaire envoie "" pour un champ non rempli,
//     enregistré NULL comme le faisait WinDev (pas de "" parasites).
function toPersonnelData(dto: CreatePersonnelDto | UpdatePersonnelDto) {
  const { Adresse1, Adresse2, Adresse3, CP, Localite, Pays, Pays_full_name, ...rawFields } = dto;
  const fields = emptyToNull(rawFields);
  const dates: Partial<Record<(typeof dateFieldNames)[number], Date | null>> = Object.fromEntries(
    dateFieldNames.filter((field) => fields[field] !== undefined).map((field) => [field, toDay(fields[field], field)]),
  );
  return { address: pickAdresse({ Adresse1, Adresse2, Adresse3, CP, Localite, Pays, Pays_full_name }), data: { ...fields, ...dates } };
}

function toDay(raw: string | null | undefined, field: string): Date | null {
  if (!raw) return null;
  const day = raw.slice(0, 10);
  const date = new Date(`${day}T00:00:00Z`);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day) || Number.isNaN(date.getTime())) {
    throw new BadRequestException(`Date invalide pour « ${field} ».`);
  }
  return date;
}

function emptyToNull<T extends object>(values: T): { [K in keyof T]: T[K] | null } {
  return Object.fromEntries(Object.entries(values).map(([key, value]) => [key, typeof value === 'string' && !value.trim() ? null : value])) as {
    [K in keyof T]: T[K] | null;
  };
}
