import { useEffect, useRef, useState } from 'react'
import { apiFetch, apiJson } from '../../lib/api.js'
import { useDocumentTemplates } from '../documents/useDocumentTemplates.js'
import './PerformancePage.css'

// Réponse de POST /performance/traitement-long (voir backend/src/performance/).
type ModeTraitement = 'attente' | 'cpu'
type Resultat = {
  mode: ModeTraitement
  dureeDemandeeMs: number
  dureeReelleMs: number
  debut: string
  fin: string
  iterations?: number
}

// Réponse de POST /performance/impression-contrats.
type ResultatImpression = {
  dossier: string
  total: number
  exemplaires: number
  impressions: number
  dureeTotaleMs: number
  dureeMoyenneMs: number | null
  dureeMinMs: number | null
  dureeMaxMs: number | null
  octetsTotal: number
  debut: string
  fin: string
  contrats: { id: string; numero: string | null; exemplaires: number; dureeMs: number; octets: number }[]
  echecs: { id: string; numero: string | null; message: string }[]
}

// Réponse de GET /performance/ping.
type Avancement = { faits: number; total: number }
type ReponsePing = { heure: string; impression: Avancement | null }

type Ping = { heure: Date; latenceMs: number | null }
type Tache = { type: 'traitement' | 'impression'; debut: number }

// Une ligne du tableau des mesures de transfert (GET /performance/transfert,
// répété) : durées en ms, médianes des répétitions sauf min/max.
type MesureTransfert = {
  cle: number
  tailleKo: number
  compression: boolean
  octetsJson: number
  octetsEnvoyes: number
  totalMs: number
  minMs: number
  maxMs: number
  jsonMs: number
  compressionMs: number
}

// Même plafond que le backend (performance.controller.ts).
const TAILLE_MAX_KO = 51_200
const REPETITIONS_MAX = 50

const DUREE_S = 60
// Même plafond que le backend (performance.controller.ts).
const EXEMPLAIRES_MAX = 20
const INTERVALLE_PING_MS = 2000
// Au-delà, le serveur est considéré comme bloqué pour ce ping.
const LATENCE_LENTE_MS = 1000

const MODES: { id: ModeTraitement; label: string; description: string }[] = [
  {
    id: 'attente',
    label: 'Attente (I/O)',
    description: 'Le serveur attend 60 s sans calculer, comme une requête SQL ou un appel d’API lent. Il reste disponible pour les autres requêtes.',
  },
  {
    id: 'cpu',
    label: 'Calcul (CPU)',
    description: 'Le serveur calcule sans s’arrêter pendant 60 s. Node.js n’a qu’un seul fil d’exécution : plus aucune requête n’est servie, pour aucun utilisateur.',
  },
]

const heureFormat = new Intl.DateTimeFormat('fr-BE', { hour: '2-digit', minute: '2-digit', second: '2-digit' })

function secondes(ms: number, decimales = 1): string {
  return `${(ms / 1000).toLocaleString('fr-BE', { minimumFractionDigits: decimales, maximumFractionDigits: decimales })} s`
}

function millisecondes(ms: number): string {
  return `${ms.toLocaleString('fr-BE', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} ms`
}

function ko(octets: number): string {
  return `${(octets / 1024).toLocaleString('fr-BE', { maximumFractionDigits: 1 })} Ko`
}

function mediane(valeurs: number[]): number {
  const triees = [...valeurs].sort((a, b) => a - b)
  const milieu = Math.floor(triees.length / 2)
  return triees.length % 2 ? triees[milieu] : (triees[milieu - 1] + triees[milieu]) / 2
}

// Page de test (Configuration > Performance) : lance un traitement long côté
// backend — fictif (60 s) ou réel (impression de tous les contrats en PDF,
// enregistrés sur le serveur) — et vérifie qu'on peut continuer à travailler
// pendant ce temps. Le chronomètre prouve que le navigateur reste libre,
// les pings mesurent si le serveur répond encore. La page reste montée
// quand on change d'onglet : le traitement continue en arrière-plan.
export function PerformancePage() {
  const [mode, setMode] = useState<ModeTraitement>('attente')
  const [tache, setTache] = useState<Tache | null>(null)
  const [maintenant, setMaintenant] = useState(() => Date.now())
  const [resultat, setResultat] = useState<Resultat | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [impression, setImpression] = useState<ResultatImpression | null>(null)
  const [erreurImpression, setErreurImpression] = useState<string | null>(null)
  const [avancement, setAvancement] = useState<Avancement | null>(null)
  const [templateId, setTemplateId] = useState('')
  const [exemplaires, setExemplaires] = useState(1)
  const [pings, setPings] = useState<Ping[]>([])
  // Sur tout le traitement, pas seulement les derniers pings affichés.
  const [latenceMax, setLatenceMax] = useState<number | null>(null)
  const [tailleKo, setTailleKo] = useState(100)
  const [repetitions, setRepetitions] = useState(5)
  // Mode de la mesure de transfert en cours (true : avec compression).
  const [transfertEnCours, setTransfertEnCours] = useState<boolean | null>(null)
  const [mesures, setMesures] = useState<MesureTransfert[]>([])
  const [erreurTransfert, setErreurTransfert] = useState<string | null>(null)
  const monteRef = useRef(true)
  const { templates, loading: chargementModeles, error: erreurModeles } = useDocumentTemplates('CONTRAT')

  const enCours = tache !== null
  const modeleChoisi = templateId || templates[0]?.IDDOCUMENT_TEMPLATES || ''

  useEffect(() => {
    monteRef.current = true
    return () => {
      monteRef.current = false
    }
  }, [])

  // Chronomètre : se met à jour tant que le navigateur n'est pas bloqué.
  useEffect(() => {
    if (!enCours) return
    const timer = window.setInterval(() => setMaintenant(Date.now()), 200)
    return () => window.clearInterval(timer)
  }, [enCours])

  // Ping du serveur pendant le traitement. Un seul ping à la fois : s'ils
  // s'accumulaient sans réponse, ils occuperaient les quelques connexions
  // que le navigateur ouvre vers le serveur et bloqueraient le reste de
  // l'application.
  useEffect(() => {
    if (!enCours) return
    let pingEnAttente = false
    const envoyer = () => {
      if (pingEnAttente) return
      pingEnAttente = true
      const envoi = performance.now()
      const ping: Ping = { heure: new Date(), latenceMs: null }
      setPings((prev) => [ping, ...prev].slice(0, 30))
      apiJson<ReponsePing>('performance/ping')
        .then((reponse) => {
          const latenceMs = Math.round(performance.now() - envoi)
          if (!monteRef.current) return
          setPings((prev) => prev.map((p) => (p === ping ? { ...p, latenceMs } : p)))
          setLatenceMax((prev) => Math.max(prev ?? 0, latenceMs))
          if (reponse.impression) setAvancement(reponse.impression)
        })
        .catch(() => undefined)
        .finally(() => {
          pingEnAttente = false
        })
    }
    envoyer()
    const timer = window.setInterval(envoyer, INTERVALLE_PING_MS)
    return () => window.clearInterval(timer)
  }, [enCours])

  function demarrer(type: Tache['type']) {
    const maintenantMs = Date.now()
    setTache({ type, debut: maintenantMs })
    setMaintenant(maintenantMs)
    setPings([])
    setLatenceMax(null)
    setAvancement(null)
  }

  async function lancer() {
    demarrer('traitement')
    setResultat(null)
    setError(null)
    try {
      const reponse = await apiJson<Resultat>(`performance/traitement-long?mode=${mode}&duree=${DUREE_S}`, { method: 'POST' })
      if (monteRef.current) setResultat(reponse)
    } catch (e: unknown) {
      if (monteRef.current) setError(e instanceof Error ? e.message : 'Le traitement a échoué.')
    } finally {
      if (monteRef.current) setTache(null)
    }
  }

  async function lancerImpression() {
    if (!modeleChoisi) return
    demarrer('impression')
    setImpression(null)
    setErreurImpression(null)
    try {
      const reponse = await apiJson<ResultatImpression>(`performance/impression-contrats?templateId=${encodeURIComponent(modeleChoisi)}&exemplaires=${exemplaires}`, { method: 'POST' })
      if (monteRef.current) setImpression(reponse)
    } catch (e: unknown) {
      if (monteRef.current) setErreurImpression(e instanceof Error ? e.message : 'L’impression a échoué.')
    } finally {
      if (monteRef.current) setTache(null)
    }
  }

  // Demandes envoyées l'une après l'autre : en parallèle, elles se
  // partageraient le débit et le serveur.
  async function mesurerTransfert(compression: boolean) {
    setTransfertEnCours(compression)
    setErreurTransfert(null)
    try {
      const essais: { totalMs: number; jsonMs: number; compressionMs: number }[] = []
      let octetsJson = 0
      let octetsEnvoyes = 0
      for (let i = 0; i < repetitions; i++) {
        const envoi = performance.now()
        const res = await apiFetch(`performance/transfert?taille=${tailleKo}&compression=${compression ? 1 : 0}`, { cache: 'no-store' })
        await res.json()
        const dureeMs = performance.now() - envoi
        const entete = (nom: string) => Number(res.headers.get(nom) ?? 0)
        essais.push({
          totalMs: Math.max(0, dureeMs - entete('X-Perf-Generation-Ms')),
          jsonMs: entete('X-Perf-Json-Ms'),
          compressionMs: entete('X-Perf-Compression-Ms'),
        })
        octetsJson = entete('X-Perf-Octets-Json')
        octetsEnvoyes = entete('X-Perf-Octets-Envoyes')
      }
      if (!monteRef.current) return
      const totaux = essais.map((essai) => essai.totalMs)
      const mesure: MesureTransfert = {
        cle: Date.now(),
        tailleKo,
        compression,
        octetsJson,
        octetsEnvoyes,
        totalMs: mediane(totaux),
        minMs: Math.min(...totaux),
        maxMs: Math.max(...totaux),
        jsonMs: mediane(essais.map((essai) => essai.jsonMs)),
        compressionMs: mediane(essais.map((essai) => essai.compressionMs)),
      }
      setMesures((prev) => [mesure, ...prev])
    } catch (e: unknown) {
      if (monteRef.current) setErreurTransfert(e instanceof Error ? e.message : 'La mesure a échoué.')
    } finally {
      if (monteRef.current) setTransfertEnCours(null)
    }
  }

  const traitementEnCours = tache?.type === 'traitement'
  const impressionEnCours = tache?.type === 'impression'
  const ecouleMs = tache ? maintenant - tache.debut : 0
  const ecouleS = traitementEnCours ? Math.min(DUREE_S, ecouleMs / 1000) : 0

  return (
    <div className="perf-page">
      <div className="page-header">
        <h2>Performance</h2>
      </div>

      <section className="perf-card" aria-labelledby="perf-title">
        <h3 id="perf-title" className="perf-card-title">Traitement long ({DUREE_S} s)</h3>
        <p className="perf-help">
          Lance le traitement, puis continue à utiliser l’application (autres onglets, fiches, recherches) pour vérifier qu’elle reste utilisable.
        </p>

        <fieldset className="perf-modes" disabled={enCours}>
          <legend className="perf-legend">Type de traitement</legend>
          {MODES.map((option) => (
            <label key={option.id} className={`perf-mode${mode === option.id ? ' active' : ''}`}>
              <input type="radio" name="perf-mode" value={option.id} checked={mode === option.id} onChange={() => setMode(option.id)} />
              <span>
                <strong>{option.label}</strong>
                <span className="perf-mode-description">{option.description}</span>
              </span>
            </label>
          ))}
        </fieldset>

        <div className="perf-actions">
          <button type="button" className="btn primary" onClick={() => void lancer()} disabled={enCours}>
            {traitementEnCours ? 'Traitement en cours...' : 'Lancer le traitement'}
          </button>
          {traitementEnCours && (
            <span className="perf-chrono" aria-live="off">
              {ecouleS.toFixed(1)} s / {DUREE_S} s
            </span>
          )}
        </div>

        {traitementEnCours && (
          <div
            className="perf-progress"
            role="progressbar"
            aria-label="Avancement du traitement"
            aria-valuemin={0}
            aria-valuemax={DUREE_S}
            aria-valuenow={Math.round(ecouleS)}
          >
            <div className="perf-progress-bar" style={{ width: `${(ecouleS / DUREE_S) * 100}%` }} />
          </div>
        )}

        {error && <p className="perf-message perf-message-error">{error}</p>}

        {resultat && (
          <dl className="perf-result">
            <div>
              <dt>Mode</dt>
              <dd>{MODES.find((m) => m.id === resultat.mode)?.label}</dd>
            </div>
            <div>
              <dt>Durée réelle</dt>
              <dd>{(resultat.dureeReelleMs / 1000).toFixed(1)} s</dd>
            </div>
            <div>
              <dt>Début / fin (serveur)</dt>
              <dd>
                {heureFormat.format(new Date(resultat.debut))} → {heureFormat.format(new Date(resultat.fin))}
              </dd>
            </div>
            {resultat.iterations !== undefined && (
              <div>
                <dt>Itérations de calcul</dt>
                <dd>{resultat.iterations.toLocaleString('fr-BE')}</dd>
              </div>
            )}
          </dl>
        )}
      </section>

      <section className="perf-card" aria-labelledby="perf-impression-title">
        <h3 id="perf-impression-title" className="perf-card-title">Impression de tous les contrats</h3>
        <p className="perf-help">
          Génère le PDF de chaque contrat, l’un après l’autre, autant de fois que d’exemplaires demandés, et l’enregistre sur le serveur. La demande ne répond qu’une fois tous les PDF
          enregistrés : la durée totale est le temps pendant lequel vous attendez le résultat.
        </p>

        <div className="perf-actions">
          {templates.length > 1 && (
            <select className="control perf-select" value={modeleChoisi} onChange={(e) => setTemplateId(e.target.value)} disabled={enCours} aria-label="Modèle de document">
              {templates.map((t) => (
                <option key={t.IDDOCUMENT_TEMPLATES} value={t.IDDOCUMENT_TEMPLATES}>{t.Nom}</option>
              ))}
            </select>
          )}
          <label className="perf-field">
            Exemplaires par contrat
            <input
              type="number"
              className="control perf-exemplaires"
              min={1}
              max={EXEMPLAIRES_MAX}
              step={1}
              value={exemplaires}
              onChange={(e) => setExemplaires(Math.min(EXEMPLAIRES_MAX, Math.max(1, Math.trunc(e.target.valueAsNumber) || 1)))}
              disabled={enCours}
            />
          </label>
          <button type="button" className="btn primary" onClick={() => void lancerImpression()} disabled={enCours || !modeleChoisi}>
            {impressionEnCours ? 'Impression en cours...' : 'Imprimer tous les contrats'}
          </button>
          {impressionEnCours && (
            <span className="perf-chrono" aria-live="off">
              {secondes(ecouleMs)}
              {avancement && ` — ${avancement.faits} / ${avancement.total} PDF`}
            </span>
          )}
        </div>

        {!chargementModeles && !erreurModeles && templates.length === 0 && (
          <p className="perf-help">Créez d’abord un modèle de contrat dans Gestion documentaire.</p>
        )}
        {erreurModeles && <p className="perf-message perf-message-error">{erreurModeles}</p>}

        {impressionEnCours && avancement && (
          <div
            className="perf-progress"
            role="progressbar"
            aria-label="Avancement de l’impression"
            aria-valuemin={0}
            aria-valuemax={avancement.total}
            aria-valuenow={avancement.faits}
          >
            <div className="perf-progress-bar" style={{ width: `${avancement.total ? (avancement.faits / avancement.total) * 100 : 0}%` }} />
          </div>
        )}

        {erreurImpression && <p className="perf-message perf-message-error">{erreurImpression}</p>}

        {impression && (
          <>
            <dl className="perf-result">
              <div>
                <dt>Durée totale (attente)</dt>
                <dd>{secondes(impression.dureeTotaleMs)}</dd>
              </div>
              <div>
                <dt>Contrats imprimés</dt>
                <dd>
                  {impression.contrats.length} / {impression.total}
                </dd>
              </div>
              <div>
                <dt>PDF enregistrés</dt>
                <dd>
                  {impression.impressions} / {impression.total * impression.exemplaires}
                </dd>
              </div>
              <div>
                <dt>Durée moyenne par PDF</dt>
                <dd>{impression.dureeMoyenneMs === null ? '—' : secondes(impression.dureeMoyenneMs, 2)}</dd>
              </div>
              <div>
                <dt>Plus rapide / plus lent</dt>
                <dd>
                  {impression.dureeMinMs === null || impression.dureeMaxMs === null
                    ? '—'
                    : `${secondes(impression.dureeMinMs, 2)} / ${secondes(impression.dureeMaxMs, 2)}`}
                </dd>
              </div>
              <div>
                <dt>Latence max. du serveur</dt>
                <dd>{latenceMax === null ? '—' : `${latenceMax} ms`}</dd>
              </div>
              <div>
                <dt>Taille totale</dt>
                <dd>{(impression.octetsTotal / 1_048_576).toLocaleString('fr-BE', { maximumFractionDigits: 1 })} Mo</dd>
              </div>
            </dl>
            <p className="perf-help">
              PDF enregistrés sur le serveur dans <code className="perf-path">{impression.dossier}</code>
            </p>

            {impression.echecs.length > 0 && (
              <div className="perf-message perf-message-error">
                {impression.echecs.length} contrat(s) non imprimé(s) :
                <ul className="perf-echecs">
                  {impression.echecs.map((echec) => (
                    <li key={echec.id}>
                      Contrat {echec.numero ?? echec.id} : {echec.message}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {impression.contrats.length > 0 && (
              <details className="perf-details">
                <summary>Durée par contrat{impression.exemplaires > 1 && ` (cumul de ${impression.exemplaires} exemplaires)`}</summary>
                <table className="perf-table">
                  <thead>
                    <tr>
                      <th>N° contrat</th>
                      <th>Durée</th>
                      <th>Taille</th>
                    </tr>
                  </thead>
                  <tbody>
                    {impression.contrats.map((contrat) => (
                      <tr key={contrat.id}>
                        <td>{contrat.numero ?? contrat.id}</td>
                        <td>{secondes(contrat.dureeMs, 2)}</td>
                        <td>{Math.round(contrat.octets / 1024).toLocaleString('fr-BE')} Ko</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </details>
            )}
          </>
        )}
      </section>

      <section className="perf-card" aria-labelledby="perf-transfert-title">
        <h3 id="perf-transfert-title" className="perf-card-title">Transfert de données : avec ou sans compression</h3>
        <p className="perf-help">
          Le serveur renvoie un JSON de la taille choisie, tel quel ou compressé (gzip). La durée est celle constatée par le navigateur, de l’envoi de la demande aux données
          prêtes à l’emploi (préparation, transfert, décompression, lecture du JSON), hors génération des données de test. Chaque mesure est la médiane des répétitions.
          Le résultat dépend du débit : à mesurer depuis le réseau des utilisateurs, pas seulement en local.
        </p>

        <div className="perf-actions">
          <label className="perf-field">
            Taille (Ko)
            <input
              type="number"
              className="control perf-taille"
              min={1}
              max={TAILLE_MAX_KO}
              step={1}
              value={tailleKo}
              onChange={(e) => setTailleKo(Math.min(TAILLE_MAX_KO, Math.max(1, Math.trunc(e.target.valueAsNumber) || 1)))}
              disabled={transfertEnCours !== null}
            />
          </label>
          <label className="perf-field">
            Répétitions
            <input
              type="number"
              className="control perf-exemplaires"
              min={1}
              max={REPETITIONS_MAX}
              step={1}
              value={repetitions}
              onChange={(e) => setRepetitions(Math.min(REPETITIONS_MAX, Math.max(1, Math.trunc(e.target.valueAsNumber) || 1)))}
              disabled={transfertEnCours !== null}
            />
          </label>
          <button type="button" className="btn primary" onClick={() => void mesurerTransfert(false)} disabled={enCours || transfertEnCours !== null}>
            {transfertEnCours === false ? 'Mesure en cours...' : 'Sans compression'}
          </button>
          <button type="button" className="btn primary" onClick={() => void mesurerTransfert(true)} disabled={enCours || transfertEnCours !== null}>
            {transfertEnCours === true ? 'Mesure en cours...' : 'Avec compression'}
          </button>
          {mesures.length > 0 && (
            <button type="button" className="btn" onClick={() => setMesures([])} disabled={transfertEnCours !== null}>
              Effacer
            </button>
          )}
        </div>

        {erreurTransfert && <p className="perf-message perf-message-error">{erreurTransfert}</p>}

        {mesures.length > 0 && (
          <div className="perf-table-scroll">
          <table className="perf-table">
            <thead>
              <tr>
                <th>Taille</th>
                <th>Mode</th>
                <th>Octets envoyés</th>
                <th>Durée totale</th>
                <th>dont JSON (serveur)</th>
                <th>dont compression (serveur)</th>
                <th>Plus rapide / plus lent</th>
              </tr>
            </thead>
            <tbody>
              {mesures.map((mesure) => (
                <tr key={mesure.cle}>
                  <td>{mesure.tailleKo.toLocaleString('fr-BE')} Ko</td>
                  <td>{mesure.compression ? 'Avec compression' : 'Sans compression'}</td>
                  <td>
                    {ko(mesure.octetsEnvoyes)}
                    {mesure.compression && ` (${Math.round((mesure.octetsEnvoyes / mesure.octetsJson) * 100)} %)`}
                  </td>
                  <td>
                    <strong>{millisecondes(mesure.totalMs)}</strong>
                  </td>
                  <td>{millisecondes(mesure.jsonMs)}</td>
                  <td>{mesure.compression ? millisecondes(mesure.compressionMs) : '—'}</td>
                  <td>
                    {millisecondes(mesure.minMs)} / {millisecondes(mesure.maxMs)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        )}
      </section>

      {pings.length > 0 && (
        <section className="perf-card" aria-labelledby="perf-pings-title">
          <h3 id="perf-pings-title" className="perf-card-title">Réactivité du serveur</h3>
          <p className="perf-help">
            Un ping est envoyé toutes les {INTERVALLE_PING_MS / 1000} s (dès que le précédent a répondu). S’il reste « en attente », le serveur ne répond plus.
            {latenceMax !== null && (
              <>
                {' '}Latence maximale : <strong>{latenceMax} ms</strong>.
              </>
            )}
          </p>
          <ul className="perf-pings">
            {pings.map((ping) => (
              <li key={ping.heure.getTime()} className="perf-ping">
                <span className="perf-ping-heure">{heureFormat.format(ping.heure)}</span>
                {ping.latenceMs === null ? (
                  <span className="perf-badge perf-badge-attente">En attente</span>
                ) : (
                  <span className={`perf-badge ${ping.latenceMs > LATENCE_LENTE_MS ? 'perf-badge-lent' : 'perf-badge-ok'}`}>{ping.latenceMs} ms</span>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}
