// ===== MODEL (logique métier) =====
// Chiffre d'affaires du tableau de bord (frontend/src/features/dashboard/).
// Règles provisoires, à affiner avec le métier :
//   - Facturé : Montant_Facture_HT des factures à leur Date_Facture, hors
//     proformas ; une note de crédit est déduite.
//   - Production : valeur des exécutions à leur Date_Execution, hors
//     déchargements (la course est déjà comptée à son chargement) :
//     Prix_unitaire de la prestation × quantité arrivée (sinon de départ)
//     si la prestation est facturée à la quantité (Qt_client_facturation),
//     × 1 sinon ; plus les prestations supplémentaires (Quantite ×
//     Prix_unitaire_ES).
// Chaque série est accompagnée de la même période un an plus tôt (N-1).
import { Injectable } from '@nestjs/common';
import { PrismaService } from '../prisma/prisma.service.js';
import { arrondiCentimes, cumulerParIntervalle, decalerAnnee, granulariteDe, intervalles } from './chiffre-affaires.js';

type Periode = { from: Date; to: Date };

const JOUR_MS = 86_400_000;

@Injectable()
export class StatistiqueService {
  constructor(private readonly prisma: PrismaService) {}

  async getChiffreAffaires(from: Date, to: Date) {
    const granularite = granulariteDe(from, to);
    const precedente = { from: decalerAnnee(from, -1), to: decalerAnnee(to, -1) };
    const [facture, factureN1, production, productionN1] = await Promise.all([
      this.lignesFacturees({ from, to }),
      this.lignesFacturees(precedente),
      this.lignesProduction({ from, to }),
      this.lignesProduction(precedente),
    ]);
    const series = {
      facture: cumulerParIntervalle(facture, granularite),
      factureN1: cumulerParIntervalle(factureN1, granularite, 1),
      production: cumulerParIntervalle(production, granularite),
      productionN1: cumulerParIntervalle(productionN1, granularite, 1),
    };
    const points = intervalles(from, to, granularite).map((cle) => ({
      cle,
      facture: arrondiCentimes(series.facture.get(cle) ?? 0),
      factureN1: arrondiCentimes(series.factureN1.get(cle) ?? 0),
      production: arrondiCentimes(series.production.get(cle) ?? 0),
      productionN1: arrondiCentimes(series.productionN1.get(cle) ?? 0),
    }));
    return { granularite, points };
  }

  private async lignesFacturees({ from, to }: Periode) {
    const factures = await this.prisma.facture.findMany({
      where: { Date_Facture: { gte: from, lte: to }, OR: [{ Proformat: 0 }, { Proformat: null }] },
      select: { Date_Facture: true, Montant_Facture_HT: true, Note_de_Credit: true },
    });
    return factures.map((f) => {
      const montant = Math.abs(Number(f.Montant_Facture_HT ?? 0));
      return { date: f.Date_Facture, montant: f.Note_de_Credit ? -montant : montant };
    });
  }

  private async lignesProduction({ from, to }: Periode) {
    const executions = await this.prisma.execution.findMany({
      where: {
        Date_Execution: { gte: from, lt: new Date(to.getTime() + JOUR_MS) },
        OR: [{ Execution_dechargement: 0 }, { Execution_dechargement: null }],
      },
      select: {
        Date_Execution: true,
        Qt_depart: true,
        Qt_arrivee: true,
        Prestation: { select: { Prix_unitaire: true, Qt_client_facturation: true } },
        ExecutionSupplementaires: { select: { Quantite: true, Prix_unitaire_ES: true } },
      },
    });
    return executions.map((e) => {
      const prix = Number(e.Prestation?.Prix_unitaire ?? 0);
      const quantite = e.Prestation?.Qt_client_facturation ? e.Qt_arrivee || e.Qt_depart || 0 : 1;
      const supplements = e.ExecutionSupplementaires.reduce((total, s) => total + (s.Quantite ?? 0) * Number(s.Prix_unitaire_ES ?? 0), 0);
      return { date: e.Date_Execution, montant: prix * quantite + supplements };
    });
  }
}
