// ===== MODEL (logique métier) =====
// Alimente le bandeau « Tâches à faire » (frontend/src/layout/TasksPanel.tsx).
// Les tâches sont créées par l'ancienne application WinDev :
//   - Type 0 : tâche saisie à la main (pas de lien) ;
//   - Type 1 : échéance d'un document du personnel (Idlien = IDPERSONNELS) ;
//   - Type 2 : échéance d'un véhicule (Idlien = IDVEHICULES).
// Date_tache est la date d'alerte (quelques semaines avant l'échéance citée
// dans le libellé).
import { Injectable } from '@nestjs/common';
import { Prisma } from '../generated/prisma/client.js';
import { PrismaService } from '../prisma/prisma.service.js';
import { serializeBigInt } from '../prisma/serialize-bigint.js';
import type { ReprogrammerTacheDto, TraiterTacheDto } from './tache.dto.js';

export const tacheSelect = {
  IDTACHES: true,
  Type: true,
  Libelle: true,
  Date_tache: true,
  Idlien: true,
} satisfies Prisma.TacheSelect;

@Injectable()
export class TacheService {
  constructor(private readonly prisma: PrismaService) {}

  // Tâches non traitées dont la date d'alerte est atteinte au jour `jusquAu`
  // (inclus), pour tous les utilisateurs, la plus ancienne en premier.
  async getTachesAFaire(jusquAu: string) {
    const taches = await this.prisma.tache.findMany({
      where: {
        OR: [{ Traite: 0 }, { Traite: null }],
        Date_tache: { lte: new Date(jusquAu) },
      },
      orderBy: [{ Date_tache: 'asc' }, { IDTACHES: 'asc' }],
      select: tacheSelect,
    });
    return { taches: serializeBigInt(taches) };
  }

  async traiterTache(id: bigint, dto: TraiterTacheDto) {
    const tache = await this.prisma.tache.update({
      where: { IDTACHES: id },
      data: { Traite: 1, Date_traitement: new Date(dto.Date_traitement), Date_heure_modification: new Date() },
      select: tacheSelect,
    });
    return serializeBigInt(tache);
  }

  // Décale la date d'alerte sans clôturer la tâche : elle sort du bandeau
  // jusqu'à cette date.
  async reprogrammerTache(id: bigint, dto: ReprogrammerTacheDto) {
    const tache = await this.prisma.tache.update({
      where: { IDTACHES: id },
      data: { Date_tache: new Date(dto.Date_tache), Date_heure_modification: new Date() },
      select: tacheSelect,
    });
    return serializeBigInt(tache);
  }
}
