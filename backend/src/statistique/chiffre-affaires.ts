// Découpage d'une période en intervalles (jours ou mois) pour le graphe du
// chiffre d'affaires du tableau de bord — logique pure, sans base de
// données (voir chiffre-affaires.spec.ts). Toutes les dates sont des jours
// à minuit UTC, comme les colonnes DATE renvoyées par Prisma.

export type Granularite = 'jour' | 'mois';

// Au-delà de deux mois, un point par jour devient illisible : on passe au mois.
const MAX_JOURS_PAR_JOUR = 62;

const JOUR_MS = 86_400_000;

export function granulariteDe(from: Date, to: Date): Granularite {
  return (to.getTime() - from.getTime()) / JOUR_MS + 1 > MAX_JOURS_PAR_JOUR ? 'mois' : 'jour';
}

// Clé de l'intervalle contenant `date` : « AAAA-MM » ou « AAAA-MM-JJ ».
export function cleIntervalle(date: Date, granularite: Granularite): string {
  const iso = date.toISOString();
  return granularite === 'mois' ? iso.slice(0, 7) : iso.slice(0, 10);
}

// Clés de tous les intervalles de la période, dans l'ordre (même vides).
export function intervalles(from: Date, to: Date, granularite: Granularite): string[] {
  const cles: string[] = [];
  const courant = new Date(from);
  if (granularite === 'mois') courant.setUTCDate(1);
  while (courant <= to) {
    cles.push(cleIntervalle(courant, granularite));
    if (granularite === 'mois') courant.setUTCMonth(courant.getUTCMonth() + 1);
    else courant.setUTCDate(courant.getUTCDate() + 1);
  }
  return cles;
}

// Même jour décalé de `annees` ans ; un 29 février devient le 28.
export function decalerAnnee(date: Date, annees: number): Date {
  const decale = new Date(Date.UTC(date.getUTCFullYear() + annees, date.getUTCMonth(), 1));
  const dernierJour = new Date(Date.UTC(decale.getUTCFullYear(), decale.getUTCMonth() + 1, 0)).getUTCDate();
  decale.setUTCDate(Math.min(date.getUTCDate(), dernierJour));
  return decale;
}

// Additionne des montants par intervalle. Les montants de N-1 (`decalage` =
// 1) sont rangés dans l'intervalle de même rang de la période affichée.
export function cumulerParIntervalle(
  lignes: { date: Date | null; montant: number }[],
  granularite: Granularite,
  decalage = 0,
): Map<string, number> {
  const totaux = new Map<string, number>();
  for (const { date, montant } of lignes) {
    if (!date || !montant) continue;
    const cle = cleIntervalle(decalage ? decalerAnnee(date, decalage) : date, granularite);
    totaux.set(cle, (totaux.get(cle) ?? 0) + montant);
  }
  return totaux;
}

export const arrondiCentimes = (value: number) => Math.round(value * 100) / 100;
