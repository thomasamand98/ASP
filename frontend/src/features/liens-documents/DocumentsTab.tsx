import { useCallback, useEffect, useRef, useState, type ChangeEvent } from 'react'
import { createColumnHelper } from '@tanstack/react-table'
import { apiFetch, apiJson } from '../../lib/api.js'
import { features } from '../../lib/tableFeatures.js'
import { DataTable } from '../../components/DataTable.js'
import type { DocumentCategorie } from './documentCategories.js'
import './DocumentsTab.css'

// Même limite que MAX_DOCUMENT_FILE_SIZE côté backend (voir
// backend/src/lien-document/lien-document.controller.ts) : vérifiée ici aussi
// pour éviter d'envoyer le fichier pour rien avant d'obtenir un 413.
const MAX_DOCUMENT_FILE_SIZE = 50 * 1024 * 1024

// Formats affichés dans un nouvel onglet ; les autres sont téléchargés. Liste
// fermée : un fichier déposé en HTML ou SVG, ouvert depuis un blob, exécuterait
// ses scripts avec les droits de l'application.
const AFFICHABLES: Record<string, string> = {
  pdf: 'application/pdf',
  png: 'image/png',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  gif: 'image/gif',
  webp: 'image/webp',
}

type DocumentRow = {
  IDLIEN_DOCUMENTS: string
  Nom: string | null
  NomFichier: string | null
  Extension: string | null
  Commentaire: string | null
  Date_heure_creation: string | null
}

type Props = {
  // Catégorie des liens affichés et identifiant de l'enregistrement auquel
  // ils se rapportent (ex. DOCUMENT_CATEGORIE.SOCIETE + IDSOCIETES).
  type: DocumentCategorie
  referentId: string
}

function errorMessage(err: unknown): string {
  return err instanceof Error ? err.message : 'erreur inconnue'
}

// Date locale « AAAA-MM-JJ HH:MM » : triable telle quelle comme du texte.
function formatDateHeure(row: DocumentRow): string {
  if (!row.Date_heure_creation) return ''
  const d = new Date(row.Date_heure_creation)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

const helper = createColumnHelper<typeof features, DocumentRow>()

const columns = helper.columns([
  helper.accessor('Nom', { header: 'Nom', filterFn: 'includesString' }),
  helper.accessor((row) => row.Extension?.toUpperCase() ?? '', { id: 'type', header: 'Type', filterFn: 'includesString' }),
  helper.accessor(formatDateHeure, { id: 'date', header: 'Ajouté le', filterFn: 'includesString' }),
  helper.accessor('Commentaire', { header: 'Commentaire', filterFn: 'includesString' }),
])

// Onglet « Documents » d'une fiche (Société, Personnel, Véhicule) : les
// documents liés à l'enregistrement par la table lien_documents (voir
// backend/src/lien-document/). Ajout d'un ou plusieurs fichiers, ouverture
// (double clic ou bouton) et suppression des lignes sélectionnées. Tout est
// enregistré immédiatement, hors du formulaire de la fiche.
export function DocumentsTab({ type, referentId }: Props) {
  const [documents, setDocuments] = useState<DocumentRow[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [busy, setBusy] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const load = useCallback(async () => {
    const params = new URLSearchParams({ type: String(type), referentId })
    const json = await apiJson<{ documents: DocumentRow[] }>(`lien-documents?${params.toString()}`)
    setDocuments(json.documents)
    setError(null)
  }, [type, referentId])

  useEffect(() => {
    let cancelled = false
    load().catch((err: unknown) => {
      if (!cancelled) setError(errorMessage(err))
    })
    return () => {
      cancelled = true
    }
  }, [load])

  async function handleFiles(e: ChangeEvent<HTMLInputElement>) {
    const files = [...(e.target.files ?? [])]
    // Permet de re-sélectionner le même fichier après une erreur.
    e.target.value = ''
    if (files.length === 0) return
    setBusy(true)
    const echecs: string[] = []
    for (const file of files) {
      if (file.size > MAX_DOCUMENT_FILE_SIZE) {
        echecs.push(`${file.name} : fichier trop volumineux (maximum 50 Mo)`)
        continue
      }
      // Pas de Content-Type explicite : le navigateur le fixe lui-même avec
      // le séparateur multipart.
      const body = new FormData()
      body.append('type', String(type))
      body.append('referentId', referentId)
      body.append('file', file)
      try {
        await apiFetch('lien-documents', { method: 'POST', body })
      } catch (err) {
        echecs.push(`${file.name} : ${errorMessage(err)}`)
      }
    }
    await load().catch((err: unknown) => setError(errorMessage(err)))
    setBusy(false)
    if (echecs.length > 0) alert(`Échec de l'ajout :\n${echecs.join('\n')}`)
  }

  // Le fichier est récupéré via apiFetch (la route exige le jeton
  // d'authentification, qu'un lien classique n'enverrait pas) puis affiché
  // ou téléchargé depuis un blob. L'onglet est ouvert avant l'attente réseau,
  // sinon le navigateur bloquerait le popup (voir ContratPdfButton.tsx).
  async function open(row: DocumentRow) {
    const mime = AFFICHABLES[row.Extension?.toLowerCase() ?? '']
    const preview = mime ? window.open('', '_blank') : null
    try {
      const res = await apiFetch(`lien-documents/${row.IDLIEN_DOCUMENTS}/fichier`)
      const blob = await res.blob()
      if (mime) {
        const url = URL.createObjectURL(new Blob([blob], { type: mime }))
        if (preview) preview.location.href = url
        else window.open(url, '_blank')
        return
      }
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = row.NomFichier ?? row.Nom ?? 'document'
      link.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      preview?.close()
      alert(`Ouverture impossible : ${errorMessage(err)}`)
    }
  }

  async function removeSelected(rows: DocumentRow[]) {
    const message = rows.length === 1 ? `Supprimer le document « ${rows[0].Nom ?? ''} » ?` : `Supprimer ces ${rows.length} documents ?`
    if (!confirm(message)) return
    setBusy(true)
    try {
      for (const row of rows) await apiFetch(`lien-documents/${row.IDLIEN_DOCUMENTS}`, { method: 'DELETE' })
    } catch (err) {
      alert(`Échec de la suppression : ${errorMessage(err)}`)
    }
    setSelected(new Set())
    await load().catch((err: unknown) => setError(errorMessage(err)))
    setBusy(false)
  }

  if (documents === null) return error ? <p className="crud-page-error">Erreur : {error}</p> : <p className="fiche-empty">Chargement des documents...</p>

  const selectedRows = documents.filter((d) => selected.has(d.IDLIEN_DOCUMENTS))

  return (
    <div>
      <div className="page-header">
        <h2>Documents ({documents.length})</h2>
        <div className="documents-tab-actions">
          <button type="button" className="btn" disabled={selectedRows.length !== 1} onClick={() => void open(selectedRows[0])}>
            Ouvrir
          </button>
          <button type="button" className="btn" disabled={selectedRows.length === 0 || busy} onClick={() => void removeSelected(selectedRows)}>
            Supprimer
          </button>
          <button type="button" className="btn primary" disabled={busy} onClick={() => fileInputRef.current?.click()}>
            {busy ? 'En cours...' : 'Ajouter un document'}
          </button>
          <input ref={fileInputRef} type="file" multiple hidden onChange={(e) => void handleFiles(e)} />
        </div>
      </div>
      {error && <p className="crud-page-error">Erreur : {error}</p>}
      <DataTable
        data={documents}
        columns={columns}
        getRowId={(d) => d.IDLIEN_DOCUMENTS}
        selectedRowIds={selected}
        onSelectionChange={setSelected}
        onRowDoubleClick={(row) => void open(row)}
        exportFileName="Documents"
      />
    </div>
  )
}
