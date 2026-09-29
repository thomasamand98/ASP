import { cleIntervalle, cumulerParIntervalle, decalerAnnee, granulariteDe, intervalles } from './chiffre-affaires.js';

const d = (iso: string) => new Date(`${iso}T00:00:00Z`);

describe('granulariteDe', () => {
  it('passe au mois au-delà de 62 jours', () => {
    expect(granulariteDe(d('2026-01-01'), d('2026-03-03'))).toBe('jour');
    expect(granulariteDe(d('2026-01-01'), d('2026-03-04'))).toBe('mois');
  });
});

describe('intervalles', () => {
  it("liste les mois de la période, même partiels", () => {
    expect(intervalles(d('2026-01-15'), d('2026-12-31'), 'mois')).toHaveLength(12);
    expect(intervalles(d('2026-01-31'), d('2026-03-01'), 'mois')).toEqual(['2026-01', '2026-02', '2026-03']);
  });

  it('liste les jours, bornes incluses', () => {
    expect(intervalles(d('2026-02-27'), d('2026-03-02'), 'jour')).toEqual(['2026-02-27', '2026-02-28', '2026-03-01', '2026-03-02']);
  });
});

describe('decalerAnnee', () => {
  it('ramène le 29 février au 28', () => {
    expect(decalerAnnee(d('2024-02-29'), 1).toISOString().slice(0, 10)).toBe('2025-02-28');
    expect(decalerAnnee(d('2026-09-29'), -1).toISOString().slice(0, 10)).toBe('2025-09-29');
  });
});

describe('cumulerParIntervalle', () => {
  it('additionne par mois et range N-1 dans le mois correspondant', () => {
    const lignes = [
      { date: d('2025-03-02'), montant: 100 },
      { date: d('2025-03-20'), montant: 50 },
      { date: null, montant: 10 },
    ];
    expect(cumulerParIntervalle(lignes, 'mois', 1)).toEqual(new Map([['2026-03', 150]]));
    expect(cleIntervalle(d('2026-03-20'), 'jour')).toBe('2026-03-20');
  });
});
