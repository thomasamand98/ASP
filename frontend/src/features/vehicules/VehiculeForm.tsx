import { useMemo, useState, type FormEvent } from 'react'
import type { VehiculeDetail } from './useVehicules.js'
import type { Societe } from '../societes/useSocietes.js'
import { useUnsavedForm } from '../../components/unsaved-changes/UnsavedChangesContext.js'
import { FicheField, FicheSection } from '../../components/FicheLayout.js'
import { FichePanel, FicheTabs } from '../../components/FicheTabs.js'
import { useEnumerationLabels } from '../../lib/useEnumerationLabels.js'
import './VehiculeForm.css'
import { DocumentsTab } from '../liens-documents/DocumentsTab.js'
import { DOCUMENT_CATEGORIE } from '../liens-documents/documentCategories.js'

// Champs scripturables d'un véhicule, mêmes clés que le CreateVehiculeDto
// côté backend (backend/src/vehicule/vehicule.dto.ts) : IDSOCIETES en string
// (BigInt non sérialisable côté JSON, '' = aucune société), dates en ISO
// string. Type est un code de l'énumération « type_vehicule » (1 Tracteur,
// 2 Remorque...). Avec_compresseur est un code 0/1 (pas un booléen).
export type VehiculeDto = {
  Type: number
  Marque: string
  Modele: string
  Num_police_assurance: string
  Num_immat: string
  Num_chassis: string
  Date_validite_assurance: string
  Num_licence_transport: string
  Date_validite_licence: string
  Date_modification_licence: string
  Date_inspection_auto: string
  Date_radiation_immatriculation: string
  Date_vente: string
  IDSOCIETES: string
  Date_premiere_mise_en_circulation: string
  Date_validite_tachygeaphe: string
  Avec_compresseur: number
}

type Props = {
  initial: VehiculeDetail | null
  // Valeurs préremplies en création (projection depuis une seule ligne,
  // voir components/projection/relations.ts) — ignorées en modification.
  defaults?: Partial<VehiculeDto>
  // Liste des sociétés pour le sélecteur — chargée par VehiculesPage et
  // passée en prop plutôt que rechargée ici, pour ne pas refaire un GET
  // /societes à chaque ouverture de la modale.
  societes: Societe[]
  onSubmit: (dto: VehiculeDto) => Promise<void>
  onCancel: () => void
}

type Tab = 'general' | 'documents'

// Convertit une date ISO (renvoyée par l'API) en "AAAA-MM-JJ", format attendu
// par <input type="date">.
function toDateInput(value: string | null | undefined): string {
  if (!value) return ''
  return value.slice(0, 10)
}

// Champs date de la fiche (clés de VehiculeDto dont la valeur est une date).
type DateKey = {
  [K in keyof VehiculeDto]: K extends `Date_${string}` ? K : never
}[keyof VehiculeDto]

// Formulaire de saisie utilisé par la modale de création/modification (voir
// VehiculesPage.tsx). `initial` vaut null en création, sinon la fiche
// complète du véhicule (GET /vehicules/:id, voir useVehicules.ts) chargée
// par CrudPage avant l'ouverture de la modale. Mise en page reprise de
// l'écran WinDev : Entreprise, puis cartes Identification et Documents.
// Date_validite_assurance n'est pas affichée (absente de l'écran WinDev,
// vide en base) mais reste conservée telle quelle.
export function VehiculeForm({ initial, defaults, societes, onSubmit, onCancel }: Props) {
  const [form, setForm] = useState<VehiculeDto>({
    Type: initial?.Type ?? 1,
    Marque: initial?.Marque ?? '',
    Modele: initial?.Modele ?? '',
    Num_police_assurance: initial?.Num_police_assurance ?? '',
    Num_immat: initial?.Num_immat ?? '',
    Num_chassis: initial?.Num_chassis ?? '',
    Date_validite_assurance: toDateInput(initial?.Date_validite_assurance),
    Num_licence_transport: initial?.Num_licence_transport ?? '',
    Date_validite_licence: toDateInput(initial?.Date_validite_licence),
    Date_modification_licence: toDateInput(initial?.Date_modification_licence),
    Date_inspection_auto: toDateInput(initial?.Date_inspection_auto),
    Date_radiation_immatriculation: toDateInput(initial?.Date_radiation_immatriculation),
    Date_vente: toDateInput(initial?.Date_vente),
    IDSOCIETES: initial?.IDSOCIETES && initial.IDSOCIETES !== '0' ? initial.IDSOCIETES : '',
    Date_premiere_mise_en_circulation: toDateInput(initial?.Date_premiere_mise_en_circulation),
    Date_validite_tachygeaphe: toDateInput(initial?.Date_validite_tachygeaphe),
    Avec_compresseur: initial?.Avec_compresseur ?? 0,
    ...(initial ? {} : defaults),
  })
  const [tab, setTab] = useState<Tab>('general')
  const [submitting, setSubmitting] = useState(false)
  const types = useEnumerationLabels('type_vehicule')

  const sortedSocietes = useMemo(
    () => [...societes].sort((a, b) => (a.Nom_societe ?? '').localeCompare(b.Nom_societe ?? '', 'fr')),
    [societes],
  )

  async function save() {
    setSubmitting(true)
    try {
      await onSubmit(form)
    } finally {
      setSubmitting(false)
    }
  }

  // Quitter la fiche modifiée demande « Enregistrer / Annuler les
  // modifications » (voir components/unsaved-changes/).
  const { formRef, confirmLeave } = useUnsavedForm(form, save)

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    void save()
  }

  const set = <K extends keyof VehiculeDto>(key: K, value: VehiculeDto[K]) => setForm({ ...form, [key]: value })
  const text = (key: Exclude<keyof VehiculeDto, DateKey | 'Type' | 'Avec_compresseur'>) => ({
    value: form[key],
    maxLength: 50,
    onChange: (e: { target: { value: string } }) => set(key, e.target.value),
  })
  const date = (key: DateKey) => ({
    type: 'date',
    value: form[key],
    onChange: (e: { target: { value: string } }) => set(key, e.target.value),
  })

  return (
    <form ref={formRef} onSubmit={handleSubmit} className="form fiche">
      <div className="form-actions">
        <button type="button" className="btn" onClick={() => void confirmLeave(onCancel)}>Annuler</button>
        <button type="submit" className="btn primary" disabled={submitting}>{submitting ? 'Enregistrement...' : 'Enregistrer'}</button>
      </div>

      <FicheTabs
        ariaLabel="Sections de la fiche véhicule"
        value={tab}
        onChange={setTab}
        tabs={[
          { id: 'general', label: 'Général' },
          { id: 'documents', label: 'Documents' },
        ]}
      />

      <FichePanel active={tab === 'general'}>
        <div className="veh-general">
          <section className="fiche-section">
            <FicheField label="Entreprise">
              <select className="veh-societe" value={form.IDSOCIETES} onChange={(e) => set('IDSOCIETES', e.target.value)}>
                <option value="" />
                {/* Société liée pas encore dans la liste chargée. */}
                {form.IDSOCIETES && !sortedSocietes.some((s) => s.IDSOCIETES === form.IDSOCIETES) && (
                  <option value={form.IDSOCIETES}>{initial?.Societe?.Nom_societe ?? form.IDSOCIETES}</option>
                )}
                {sortedSocietes.map((s) => (
                  <option key={s.IDSOCIETES} value={s.IDSOCIETES}>{s.Nom_societe}</option>
                ))}
              </select>
            </FicheField>
          </section>

          <FicheSection title="Identification" className="fiche-section--center">
            <div className="veh-grid">
              <FicheField label="Type">
                <select value={form.Type} onChange={(e) => set('Type', Number(e.target.value))}>
                  {!(String(form.Type) in types) && <option value={form.Type}>{form.Type || ''}</option>}
                  {Object.entries(types).map(([valeur, libelle]) => (
                    <option key={valeur} value={valeur}>{libelle}</option>
                  ))}
                </select>
              </FicheField>
              <FicheField label="Marque"><input {...text('Marque')} /></FicheField>
              <FicheField label="Modèle"><input {...text('Modele')} /></FicheField>
              <FicheField label="N° de châssis"><input {...text('Num_chassis')} /></FicheField>
              <FicheField label="N° d'immatriculation"><input {...text('Num_immat')} /></FicheField>
              <FicheField label="Radiation de l'immatriculation"><input {...date('Date_radiation_immatriculation')} /></FicheField>
              <FicheField label="Vente"><input {...date('Date_vente')} /></FicheField>
              <label className="fiche-check veh-check">
                <span className="field-label">Avec compresseur</span>
                <input
                  type="checkbox"
                  checked={form.Avec_compresseur === 1}
                  onChange={(e) => set('Avec_compresseur', e.target.checked ? 1 : 0)}
                />
              </label>
            </div>
          </FicheSection>

          <FicheSection title="Documents" className="fiche-section--center">
            <div className="veh-grid">
              <FicheField label="N° licence transport"><input {...text('Num_licence_transport')} /></FicheField>
              <FicheField label="Validité de la licence"><input {...date('Date_validite_licence')} /></FicheField>
              <FicheField label="Modification de licence"><input {...date('Date_modification_licence')} /></FicheField>
              <FicheField label="N° police d'assurance"><input {...text('Num_police_assurance')} /></FicheField>
              <FicheField label="Première mise en circulation"><input {...date('Date_premiere_mise_en_circulation')} /></FicheField>
              <FicheField label="Validité tachygraphe"><input {...date('Date_validite_tachygeaphe')} /></FicheField>
              <FicheField label="Inspection auto"><input {...date('Date_inspection_auto')} /></FicheField>
            </div>
          </FicheSection>
        </div>
      </FichePanel>

      <FichePanel active={tab === 'documents'}>
        {initial ? (
          <DocumentsTab type={DOCUMENT_CATEGORIE.VEHICULE} referentId={initial.IDVEHICULES} />
        ) : (
          <p className="fiche-empty">Enregistrez d’abord le véhicule pour y joindre des documents.</p>
        )}
      </FichePanel>
    </form>
  )
}
