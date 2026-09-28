// ===== MODEL (logique métier) =====
// Va chercher tous les points via Prisma. Appelé uniquement par
// PointController — jamais par la View directement.
import { Injectable, NotFoundException } from '@nestjs/common';
import { Prisma } from '../generated/prisma/client.js';
import { PrismaService } from '../prisma/prisma.service.js';
import { buildSearchWhere } from '../common/search.js';
import { andWhere, projectionWhere, type Projection, type ProjectionMap } from '../common/projection.js';
import { serializeBigInt } from '../prisma/serialize-bigint.js';
import { adresseSelect, pickAdresse, saveAdresse } from '../common/adresse.js';
import { CreatePointDto, UpdatePointContactDto, UpdatePointDto, HorairePointDto } from './point.dto.js';

export const pointSelect = {
  IDPOINTS: true,
  Libelle: true,
  Nom_societe: true,
  Telephone: true,
  IDADRESSES: true,
  IDSOCIETES: true,
  Adresse: { select: { Adresse1: true, CP: true, Localite: true } },
} satisfies Prisma.PointSelect;

// Select complet pour GET /points/:id — tous les champs scripturables du
// point, plus le contact par défaut résolu et la totalité des contacts
// rattachés (table de jointure PointContacts).
export const pointDetailSelect = {
  ...pointSelect,
  Archive: true,
  Lien_googleMap: true,
  Instruction: true,
  IDCONTACTS_DEFAUTS: true,
  Adresse: { select: adresseSelect },
  ContactDefauts: { select: { Nom_contact: true, Prenom_contact: true } },
  // IDCONTACTS et coordonnées : onglet Contacts de la fiche (liste, et
  // choix du contact par défaut parmi les contacts liés).
  PointContacts: {
    select: {
      IDCONTACTS: true,
      Lien: true,
      Recevoir_Mail_Planning: true,
      Contact: { select: { Civilite: true, Nom_contact: true, Prenom_contact: true, Telephone_portable: true, Telephone_fixe: true, E_mail: true } },
    },
  },
} satisfies Prisma.PointSelect;

// Projection (voir common/projection.ts) : pour chaque table source, les
// lignes de cette table liées aux ids sélectionnés.
const pointProjections: ProjectionMap<Prisma.PointWhereInput> = {
  societes: (ids) => ({ IDSOCIETES: { in: ids } }),
  contacts: (ids) => ({ PointContacts: { some: { IDCONTACTS: { in: ids } } } }),
};

@Injectable()
export class PointService {
  constructor(private readonly prisma: PrismaService) {}

  // page/pageSize optionnels : omis, le comportement est inchangé (toute la
  // table). Fournis, la requête est découpée avec skip/take et `total`
  // (nombre total de lignes, pas juste celles de la page) est renvoyé à
  // côté pour que le frontend puisse calculer le nombre de pages.
  async getPoints(page?: number, pageSize?: number, search?: string, projection?: Projection) {
    const paginate = page !== undefined && pageSize !== undefined && pageSize > 0;
    const filterWhere = buildSearchWhere<Prisma.PointWhereInput>(search, (c) => [
      { Libelle: c },
      { Nom_societe: c },
      { Telephone: c },
      { Adresse: { Adresse1: c } },
      { Adresse: { CP: c } },
      { Adresse: { Localite: c } },
    ]);
    const where = andWhere<Prisma.PointWhereInput>(filterWhere, projectionWhere(pointProjections, projection));
    const [points, total] = await Promise.all([
      this.prisma.point.findMany({
        where,
        orderBy: { IDPOINTS: 'asc' },
        select: pointSelect,
        ...(paginate ? { skip: (page - 1) * pageSize, take: pageSize } : {}),
      }),
      this.prisma.point.count({ where }),
    ]);
    return { points: serializeBigInt(points), total };
  }

  // Plages horaires : pas de relation Prisma (IDLIENS générique), lues à
  // part et renvoyées en « HH:MM ».
  async getPoint(id: bigint) {
    const [point, horaires] = await Promise.all([
      this.prisma.point.findUniqueOrThrow({ where: { IDPOINTS: id }, select: pointDetailSelect }),
      this.prisma.grilleHoraire.findMany({
        where: { IDLIENS: id },
        orderBy: [{ Jour_semaine: 'asc' }, { Heure_debut: 'asc' }],
        select: { Jour_semaine: true, Heure_debut: true, Heure_fin: true },
      }),
    ]);
    return serializeBigInt({
      ...point,
      Horaires: horaires.map((h) => ({ Jour_semaine: h.Jour_semaine, Heure_debut: toHhmm(h.Heure_debut), Heure_fin: toHhmm(h.Heure_fin) })),
    });
  }

  // L'adresse éventuellement saisie est créée dans `adresses` puis liée
  // (voir common/adresse.ts).
  async createPoint(dto: CreatePointDto) {
    const { address, horaires, data } = toPointData(dto);
    const point = await this.prisma.$transaction(async (tx) => {
      const idAdresses = await saveAdresse(tx, null, address);
      const created = await tx.point.create({
        // IDADRESSES/IDSOCIETES/IDCONTACTS_DEFAUTS ont un défaut DB de 0, qui
        // viole leur contrainte de clé étrangère (aucune ligne d'id 0) quand
        // ils sont omis — mis explicitement à NULL.
        data: {
          ...data,
          IDADRESSES: idAdresses,
          IDSOCIETES: data.IDSOCIETES ?? null,
          IDCONTACTS_DEFAUTS: data.IDCONTACTS_DEFAUTS ?? null,
          Date_heure_creation: new Date(),
        },
        select: pointSelect,
      });
      if (horaires) await replaceHoraires(tx, created.IDPOINTS, horaires);
      return created;
    });
    return serializeBigInt(point);
  }

  // L'adresse liée est mise à jour, ou créée si le point n'en avait pas.
  async updatePoint(id: bigint, dto: UpdatePointDto) {
    const { address, horaires, data } = toPointData(dto);
    const point = await this.prisma.$transaction(async (tx) => {
      const current = await tx.point.findUniqueOrThrow({ where: { IDPOINTS: id }, select: { IDADRESSES: true } });
      const idAdresses = await saveAdresse(tx, current.IDADRESSES, address);
      if (horaires) await replaceHoraires(tx, id, horaires);
      return tx.point.update({
        where: { IDPOINTS: id },
        data: { ...data, IDADRESSES: idAdresses, Date_heure_modification: new Date() },
        select: pointSelect,
      });
    });
    return serializeBigInt(point);
  }

  async deletePoint(id: bigint) {
    await this.prisma.point.delete({ where: { IDPOINTS: id } });
  }

  // Un contact peut en théorie être lié plusieurs fois au même point
  // (aucune contrainte d'unicité) : tous ses liens sont mis à jour.
  async updatePointContact(pointId: bigint, contactId: bigint, dto: UpdatePointContactDto) {
    const { count } = await this.prisma.pointContact.updateMany({
      where: { IDPOINTS: pointId, IDCONTACTS: contactId },
      data: { Recevoir_Mail_Planning: dto.Recevoir_Mail_Planning },
    });
    if (count === 0) throw new NotFoundException('Ce contact n’est pas lié à ce point.');
  }

  async removePointContact(pointId: bigint, contactId: bigint) {
    await this.prisma.pointContact.deleteMany({ where: { IDPOINTS: pointId, IDCONTACTS: contactId } });
  }
}

// Le DTO a les mêmes noms de champs que Prisma — seuls les IDs (BigInt côté
// Prisma, string côté JSON) ont besoin d'être convertis, le reste passe tel
// quel via le spread.
// Les champs d'adresse vont dans la table `adresses` (texte vide enregistré
// NULL), les plages horaires dans `grilles_horaires`.
function toPointData(dto: CreatePointDto | UpdatePointDto) {
  const { Adresse1, Adresse2, Adresse3, CP, Localite, Pays, Pays_full_name, Horaires, ...point } = dto;
  return {
    address: pickAdresse({ Adresse1, Adresse2, Adresse3, CP, Localite, Pays, Pays_full_name }),
    horaires: Horaires,
    data: {
      ...point,
      IDSOCIETES: dto.IDSOCIETES ? BigInt(dto.IDSOCIETES) : undefined,
      IDCONTACTS_DEFAUTS: dto.IDCONTACTS_DEFAUTS ? BigInt(dto.IDCONTACTS_DEFAUTS) : undefined,
    },
  };
}

// Remplace toutes les plages horaires du point. Colonnes TIME : Prisma les
// lit/écrit comme un datetime du 01/01/1970 (UTC).
async function replaceHoraires(tx: Prisma.TransactionClient, pointId: bigint, horaires: HorairePointDto[]) {
  await tx.grilleHoraire.deleteMany({ where: { IDLIENS: pointId } });
  if (horaires.length === 0) return;
  const now = new Date();
  await tx.grilleHoraire.createMany({
    data: horaires.map((h) => ({
      IDLIENS: pointId,
      Jour_semaine: h.Jour_semaine,
      Heure_debut: new Date(`1970-01-01T${h.Heure_debut}:00Z`),
      Heure_fin: new Date(`1970-01-01T${h.Heure_fin}:00Z`),
      Horaire: `${h.Heure_debut} - ${h.Heure_fin}`,
      Date_heure_creation: now,
    })),
  });
}

function toHhmm(value: Date | null): string {
  return value ? value.toISOString().slice(11, 16) : '';
}
