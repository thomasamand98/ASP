import { useEffect, useState } from 'react'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis, type TooltipContentProps } from 'recharts'
import { apiJson } from '../../lib/api.js'
import { ChevronLeftIcon, ChevronRightIcon } from '../planning/icons.js'
import { toInputValue } from '../planning/dateUtils.js'
import './ChiffreAffairesCard.css'

// Réponse de GET /statistiques/chiffre-affaires (voir
// backend/src/statistique/) : un point par mois, ou par jour sur une
// période de deux mois au plus.
type Point = { cle: string; facture: number; factureN1: number; production: number; productionN1: number }
type Reponse = { granularite: 'jour' | 'mois'; points: Point[] }
type SerieKey = Exclude<keyof Point, 'cle'>

// Ordre fixe = ordre des couleurs --series-N (validé daltonisme, voir
// styles/tokens.css) : ne pas réordonner.
const SERIES: { key: SerieKey; label: string; color: string }[] = [
  { key: 'facture', label: 'Facturé', color: 'var(--series-1)' },
  { key: 'factureN1', label: 'Facturé N-1', color: 'var(--series-2)' },
  { key: 'production', label: 'Production', color: 'var(--series-3)' },
  { key: 'productionN1', label: 'Production N-1', color: 'var(--series-4)' },
]

const euro = new Intl.NumberFormat('fr-BE', { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 })
const euroCompact = new Intl.NumberFormat('fr-BE', { style: 'currency', currency: 'EUR', notation: 'compact', maximumFractionDigits: 1 })
const pourcent = new Intl.NumberFormat('fr-BE', { style: 'percent', maximumFractionDigits: 1, signDisplay: 'exceptZero' })
const moisCourt = new Intl.DateTimeFormat('fr-BE', { month: 'short', timeZone: 'UTC' })
const moisLong = new Intl.DateTimeFormat('fr-BE', { month: 'long', year: 'numeric', timeZone: 'UTC' })

type Periode = { from: string; to: string }

function anneeComplete(annee: number): Periode {
  return { from: `${annee}-01-01`, to: `${annee}-12-31` }
}

// Période décalée d'un an (un 29 février devient le 28).
function decalerAnnee(jour: string, annees: number): string {
  const [y, m, d] = jour.split('-').map(Number)
  const dernierJour = new Date(Date.UTC(y + annees, m, 0)).getUTCDate()
  return `${y + annees}-${String(m).padStart(2, '0')}-${String(Math.min(d, dernierJour)).padStart(2, '0')}`
}

function libelleCourt(cle: string, granularite: Reponse['granularite']): string {
  if (granularite === 'jour') return `${cle.slice(8, 10)}/${cle.slice(5, 7)}`
  const mois = moisCourt.format(new Date(`${cle}-01T00:00:00Z`))
  return mois.charAt(0).toUpperCase() + mois.slice(1)
}

function libelleLong(cle: string, granularite: Reponse['granularite']): string {
  if (granularite === 'jour') return `${cle.slice(8, 10)}/${cle.slice(5, 7)}/${cle.slice(0, 4)}`
  const mois = moisLong.format(new Date(`${cle}-01T00:00:00Z`))
  return mois.charAt(0).toUpperCase() + mois.slice(1)
}

function evolution(courant: number, precedent: number, aDate: boolean): string | null {
  return precedent ? `${pourcent.format((courant - precedent) / Math.abs(precedent))} vs N-1${aDate ? ' à date' : ''}` : null
}

function somme(points: Point[], key: SerieKey): number {
  return points.reduce((total, p) => total + p[key], 0)
}

// Carte « Chiffre d'affaires » du tableau de bord : facturé et production
// de la période choisie (année en cours par défaut), comparés à la même
// période un an plus tôt. Bascule Graphique / Tableau : le tableau donne
// les valeurs exactes (et remplace le graphique pour qui ne distingue pas
// les couleurs).
export function ChiffreAffairesCard() {
  const [periode, setPeriode] = useState<Periode>(() => anneeComplete(new Date().getFullYear()))
  const [vue, setVue] = useState<'graphique' | 'tableau'>('graphique')
  const [data, setData] = useState<Reponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let actif = true
    setLoading(true)
    apiJson<Reponse>(`statistiques/chiffre-affaires?from=${periode.from}&to=${periode.to}`)
      .then((reponse) => {
        if (!actif) return
        setData(reponse)
        setError(null)
      })
      .catch((e: unknown) => {
        if (actif) setError(e instanceof Error ? e.message : 'Impossible de charger le chiffre d’affaires.')
      })
      .finally(() => {
        if (actif) setLoading(false)
      })
    return () => {
      actif = false
    }
  }, [periode])

  const annee = periode.from.slice(0, 4)
  const estAnneeComplete = periode.from === `${annee}-01-01` && periode.to === `${annee}-12-31`
  const granularite = data?.granularite ?? 'mois'
  const points = data?.points ?? []
  const totaux = Object.fromEntries(SERIES.map(({ key }) => [key, somme(points, key)])) as Record<SerieKey, number>
  // Période en cours : l'évolution ne compare que ce qui est écoulé (jusqu'au
  // mois ou jour courant), pas 9 mois de cette année à 12 de l'an dernier.
  const aujourdhui = toInputValue(new Date())
  const enCours = periode.to > aujourdhui
  const cleCourante = granularite === 'mois' ? aujourdhui.slice(0, 7) : aujourdhui
  const pointsEcoules = enCours ? points.filter((p) => p.cle <= cleCourante) : points

  function decaler(annees: number) {
    setPeriode((p) => ({ from: decalerAnnee(p.from, annees), to: decalerAnnee(p.to, annees) }))
  }

  function changerBorne(borne: keyof Periode, valeur: string) {
    if (!valeur) return
    setPeriode((p) => {
      const suivante = { ...p, [borne]: valeur }
      // Une période inversée devient une période d'un jour sur la borne modifiée.
      return suivante.from <= suivante.to ? suivante : { from: valeur, to: valeur }
    })
  }

  return (
    <section className="ca-card" aria-labelledby="ca-card-title">
      <header className="ca-card-header">
        <h3 id="ca-card-title" className="ca-card-title">
          Chiffre d’affaires {estAnneeComplete && <span className="ca-card-year">{annee}</span>}
        </h3>
        <div className="ca-card-filters">
          <div className="ca-period" role="group" aria-label="Période">
            <button type="button" className="icon-btn outlined" onClick={() => decaler(-1)} aria-label="Année précédente">
              <ChevronLeftIcon />
            </button>
            <label className="ca-period-date">
              <span>Du</span>
              <input type="date" className="control" value={periode.from} onChange={(e) => changerBorne('from', e.target.value)} />
            </label>
            <label className="ca-period-date">
              <span>au</span>
              <input type="date" className="control" value={periode.to} onChange={(e) => changerBorne('to', e.target.value)} />
            </label>
            <button type="button" className="icon-btn outlined" onClick={() => decaler(1)} aria-label="Année suivante">
              <ChevronRightIcon />
            </button>
            {!estAnneeComplete || annee !== String(new Date().getFullYear()) ? (
              <button type="button" className="btn sm" onClick={() => setPeriode(anneeComplete(new Date().getFullYear()))}>
                Année en cours
              </button>
            ) : null}
          </div>
          <div className="ca-view-toggle" role="group" aria-label="Affichage">
            {(['graphique', 'tableau'] as const).map((option) => (
              <button key={option} type="button" className={vue === option ? 'active' : ''} aria-pressed={vue === option} onClick={() => setVue(option)}>
                {option === 'graphique' ? 'Graphique' : 'Tableau'}
              </button>
            ))}
          </div>
        </div>
      </header>

      {/* Légende + totaux de la période : l'identité de chaque série ne
          repose jamais sur la seule couleur. */}
      <ul className="ca-totals">
        {SERIES.map(({ key, label, color }, index) => {
          const comparaison = index % 2 === 0 ? evolution(somme(pointsEcoules, key), somme(pointsEcoules, SERIES[index + 1].key), enCours) : null
          return (
            <li key={key} className="ca-total">
              <span className="ca-total-label">
                <span className="ca-swatch" style={{ background: color }} aria-hidden="true" />
                {label}
              </span>
              <strong className="ca-total-value">{euro.format(totaux[key])}</strong>
              {comparaison && <span className="ca-total-delta">{comparaison}</span>}
            </li>
          )
        })}
      </ul>

      {error ? (
        <p className="ca-message ca-message-error">{error}</p>
      ) : loading && !data ? (
        <p className="ca-message">Chargement...</p>
      ) : vue === 'graphique' ? (
        <div className={`ca-chart${loading ? ' is-loading' : ''}`}>
          <ResponsiveContainer width="100%" height={340}>
            <BarChart data={points} margin={{ top: 8, right: 8, bottom: 0, left: 8 }} barGap={2} barCategoryGap="18%">
              <CartesianGrid vertical={false} stroke="var(--line)" />
              <XAxis
                dataKey="cle"
                tickFormatter={(cle: string) => libelleCourt(cle, granularite)}
                tickLine={false}
                axisLine={{ stroke: 'var(--border)' }}
                tick={{ fill: 'var(--text)', fontSize: 12 }}
                interval="preserveStartEnd"
                minTickGap={8}
              />
              <YAxis
                tickFormatter={(value: number) => euroCompact.format(value)}
                tickLine={false}
                axisLine={false}
                tick={{ fill: 'var(--text)', fontSize: 12 }}
                width={72}
              />
              <Tooltip cursor={{ fill: 'var(--surface)' }} content={({ active, payload, label }) => <CaTooltip active={active} payload={payload} label={label} granularite={granularite} />} />
              {SERIES.map(({ key, label, color }) => (
                <Bar key={key} dataKey={key} name={label} fill={color} radius={[4, 4, 0, 0]} maxBarSize={28} isAnimationActive={false} />
              ))}
            </BarChart>
          </ResponsiveContainer>
        </div>
      ) : (
        <div className="ca-table-wrap">
          <table className="ca-table">
            <thead>
              <tr>
                <th scope="col">{granularite === 'mois' ? 'Mois' : 'Jour'}</th>
                {SERIES.map(({ key, label }) => (
                  <th key={key} scope="col">
                    {label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {points.map((point) => (
                <tr key={point.cle}>
                  <th scope="row">{libelleLong(point.cle, granularite)}</th>
                  {SERIES.map(({ key }) => (
                    <td key={key}>{euro.format(point[key])}</td>
                  ))}
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <th scope="row">Total</th>
                {SERIES.map(({ key }) => (
                  <td key={key}>{euro.format(totaux[key])}</td>
                ))}
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </section>
  )
}

type CaTooltipProps = Pick<TooltipContentProps<number, string>, 'active' | 'label'> & {
  payload?: readonly { dataKey?: unknown; value?: unknown }[]
  granularite: Reponse['granularite']
}

function CaTooltip({ active, payload, label, granularite }: CaTooltipProps) {
  if (!active || !payload?.length) return null
  return (
    <div className="ca-tooltip">
      <p className="ca-tooltip-title">{libelleLong(String(label), granularite)}</p>
      {SERIES.map(({ key, label: serie, color }) => {
        const entry = payload.find((item) => item.dataKey === key)
        return (
          <p key={key} className="ca-tooltip-row">
            <span className="ca-swatch" style={{ background: color }} aria-hidden="true" />
            <span className="ca-tooltip-label">{serie}</span>
            <span className="ca-tooltip-value">{euro.format(Number(entry?.value ?? 0))}</span>
          </p>
        )
      })}
    </div>
  )
}
