import { useCallback, useEffect, useState } from 'react'
import { apiFetch, apiJson } from '../lib/api.js'
import { toInputValue } from '../features/planning/dateUtils.js'
import { CloseIcon, TasksIcon } from './icons.js'
import './TasksPanel.css'

// Tâche renvoyée par GET /taches/a-faire (voir backend/src/tache/).
type Tache = {
  IDTACHES: string
  Type: number | null
  Libelle: string | null
  Date_tache: string | null
  Idlien: string | null
}

const TYPE_LABELS: Record<number, string> = { 1: 'Personnel', 2: 'Véhicule' }

const REFRESH_INTERVAL_MS = 60_000

// Date DATE renvoyée en minuit UTC : on garde le jour tel quel, sans
// conversion de fuseau (qui décalerait d'un jour à l'ouest de Greenwich).
function formatDay(iso: string | null): string {
  if (!iso) return ''
  const [y, m, d] = iso.slice(0, 10).split('-')
  return `${d}/${m}/${y}`
}

function patchTache(id: string, action: 'traiter' | 'reprogrammer', body: Record<string, string>) {
  return apiFetch(`taches/${id}/${action}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}

// Bandeau latéral droit, présent sur tous les écrans : replié en une fine
// barre avec le nombre de tâches échues (non traitées, date d'alerte
// atteinte), il se déplie par-dessus le contenu pour les lister. Chaque
// tâche peut être marquée comme traitée ou reprogrammée à une autre date
// (elle disparaît alors jusqu'à cette date).
export function TasksPanel() {
  const [open, setOpen] = useState(false)
  const [taches, setTaches] = useState<Tache[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [reprogramming, setReprogramming] = useState<{ id: string; date: string } | null>(null)

  const load = useCallback(async () => {
    try {
      const data = await apiJson<{ taches: Tache[] }>(`taches/a-faire?jusquAu=${toInputValue(new Date())}`)
      setTaches(data.taches)
      setError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Impossible de charger les tâches.')
    }
  }, [])

  useEffect(() => {
    void load()
    const timer = window.setInterval(() => void load(), REFRESH_INTERVAL_MS)
    return () => window.clearInterval(timer)
  }, [load])

  useEffect(() => {
    if (!open) return
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open])

  async function run(id: string, action: () => Promise<unknown>) {
    setBusyId(id)
    try {
      await action()
      setReprogramming(null)
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Action impossible.')
    } finally {
      setBusyId(null)
    }
  }

  const count = taches.length

  return (
    <>
      <div className={`tasks-backdrop${open ? ' open' : ''}`} onClick={() => setOpen(false)} />
      <aside className={`tasks-panel${open ? ' open' : ''}`} aria-label="Tâches à faire" inert={!open}>
        <header className="tasks-panel-header">
          <h3 className="tasks-panel-title">
            Tâches à faire <span className="tasks-panel-count">{count}</span>
          </h3>
          <button type="button" className="tasks-panel-close" onClick={() => setOpen(false)} aria-label="Fermer">
            <CloseIcon />
          </button>
        </header>
        {error && <p className="tasks-panel-error">{error}</p>}
        {count === 0 && !error ? (
          <p className="tasks-panel-empty">Aucune tâche à faire.</p>
        ) : (
          <ul className="tasks-list">
            {taches.map((tache) => {
              const busy = busyId === tache.IDTACHES
              const editing = reprogramming?.id === tache.IDTACHES
              return (
                <li key={tache.IDTACHES} className="tasks-item">
                  <div className="tasks-item-meta">
                    <span className="tasks-item-date">{formatDay(tache.Date_tache)}</span>
                    <span className="tasks-item-type">{TYPE_LABELS[tache.Type ?? 0] ?? 'Autre'}</span>
                  </div>
                  <p className="tasks-item-label">{tache.Libelle?.replaceAll('\t', ' ')}</p>
                  {editing ? (
                    <form
                      className="tasks-item-actions"
                      onSubmit={(e) => {
                        e.preventDefault()
                        void run(tache.IDTACHES, () => patchTache(tache.IDTACHES, 'reprogrammer', { Date_tache: reprogramming.date }))
                      }}
                    >
                      <input
                        type="date"
                        className="control tasks-item-date-input"
                        value={reprogramming.date}
                        onChange={(e) => setReprogramming({ id: tache.IDTACHES, date: e.target.value })}
                        required
                        aria-label="Nouvelle date d'alerte"
                      />
                      <button type="submit" className="btn sm primary" disabled={busy || !reprogramming.date}>
                        OK
                      </button>
                      <button type="button" className="btn sm ghost" onClick={() => setReprogramming(null)} disabled={busy}>
                        Annuler
                      </button>
                    </form>
                  ) : (
                    <div className="tasks-item-actions">
                      <button
                        type="button"
                        className="btn sm"
                        disabled={busy}
                        onClick={() => {
                          const libelle = tache.Libelle?.replaceAll('\t', ' ').replaceAll('\n', ' — ') ?? ''
                          if (!window.confirm(`Marquer cette tâche comme traitée ?\n\n${libelle}`)) return
                          void run(tache.IDTACHES, () => patchTache(tache.IDTACHES, 'traiter', { Date_traitement: toInputValue(new Date()) }))
                        }}
                      >
                        Traitée
                      </button>
                      <button
                        type="button"
                        className="btn sm ghost"
                        disabled={busy}
                        onClick={() => setReprogramming({ id: tache.IDTACHES, date: '' })}
                      >
                        Reprogrammer
                      </button>
                    </div>
                  )}
                </li>
              )
            })}
          </ul>
        )}
      </aside>
      <button
        type="button"
        className="tasks-rail"
        onClick={() => setOpen((prev) => !prev)}
        aria-expanded={open}
        aria-label={`${open ? 'Fermer' : 'Ouvrir'} les tâches à faire (${count})`}
        title="Tâches à faire"
      >
        <span className="tasks-rail-icon">
          <TasksIcon />
          {count > 0 && <span className="tasks-rail-badge">{count > 99 ? '99+' : count}</span>}
        </span>
      </button>
    </>
  )
}
