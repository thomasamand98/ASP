import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { apiJson } from '../../lib/api'
import { Dev } from '../enDeveloppement/Dev.js'
import { DocumentsTab } from '../liens-documents/DocumentsTab.js'
import { DOCUMENT_CATEGORIE } from '../liens-documents/documentCategories.js'
import { FichePanel, FicheTabs } from '../../components/FicheTabs.js'
import { PersonnelContratsTab } from './PersonnelContratsTab.js'
import type { PersonnelDetail } from './usePersonnel.js'
import './PersonnelForm.css'
import { useUnsavedForm } from '../../components/unsaved-changes/UnsavedChangesContext.js'

// Champs scripturables d'un personnel, mêmes clés que le
// CreatePersonnelDto côté backend (backend/src/personnel/personnel.dto.ts) :
// dates en "AAAA-MM-JJ", adresse aplatie (enregistrée dans la table
// `adresses` par le backend). Routier/Manutention/Atelier sont des codes 0/1
// côté vrai modèle Prisma (pas des booléens).
export type PersonnelDto = {
  Civilite_Personnel: string
  Nom_Personnel: string
  Prenom_Personnel: string
  Telephone_portable: string
  Telephone_fixe: string
  Telephone_autre: string
  Telephone_professionnel: string
  Description_telephone: string
  E_mail: string
  E_mail_professionnel: string
  Num_service_social: string
  Num_registre_national: string
  Date_naissance: string
  Lieu_naissance: string
  Pays_Naissance: string
  Etat_civil: string
  Nbr_personne_charge: number
  Iban: string
  Bic: string
  Nom_Banque: string
  Qualification: string
  Routier: number
  Manutention: number
  Atelier: number
  Commentaire_Personnel: string
  Date_validite_selection_medicale: string
  Date_validite_carte_chauffeur: string
  Date_validite_CAP: string
  Date_validite_carte_identite: string
  Date_validite_A1: string
  Date_validite_SIPSI: string
  Adresse1: string
  Adresse2: string
  Adresse3: string
  CP: string
  Localite: string
  Pays: string
  Pays_full_name: string
}

type Props = {
  initial: PersonnelDetail | null
  onSubmit: (dto: PersonnelDto) => Promise<void>
  onCancel: () => void
}

type PaysOption = { IDPAYS: string; ISO: string | null; Nom: string | null }
type VilleOption = { IDVILLES: string; Nom_ville: string | null }

const TABS = [
  { id: 'principal', label: 'Principal' },
  { id: 'contrat', label: 'Contrat' },
  { id: 'contact', label: 'Contact' },
  { id: 'documents', label: 'Documents' },
] as const
type TabId = (typeof TABS)[number]['id']

const CIVILITES = ['Monsieur', 'Madame', 'Mademoiselle']
// Suggestions seulement : la colonne est en texte libre et contient déjà
// d'autres graphies (MARIE, CELIB...), conservées telles quelles.
const ETATS_CIVILS = ['Célibataire', 'Marié(e)', 'Cohabitant(e)', 'Divorcé(e)', 'Séparé(e)', 'Veuf / Veuve', 'Isolé(e)']

// Validités (documents du chauffeur), affichées avec leur état d'expiration.
const VALIDITES = [
  { field: 'Date_validite_CAP', label: 'C.A.P.' },
  { field: 'Date_validite_selection_medicale', label: 'Sélection médicale' },
  { field: 'Date_validite_carte_chauffeur', label: 'Carte chauffeur' },
  { field: 'Date_validite_carte_identite', label: "Carte d'identité" },
  { field: 'Date_validite_A1', label: 'A1' },
  { field: 'Date_validite_SIPSI', label: 'SIPSI' },
] as const

// En deçà, la validité est signalée comme « bientôt expirée ».
const EXPIRY_WARNING_DAYS = 60

// Convertit une date ISO (renvoyée par l'API) en "AAAA-MM-JJ", format attendu
// par <input type="date">.
function toDateInput(value: string | null | undefined): string {
  if (!value) return ''
  return value.slice(0, 10)
}

type Validity = { tone: 'none' | 'ok' | 'soon' | 'expired'; label: string }

// État d'une date de validité "AAAA-MM-JJ" par rapport à aujourd'hui.
// Les dates en 2099 ou après sont une convention WinDev pour « sans limite ».
function validityOf(day: string): Validity {
  if (!day) return { tone: 'none', label: 'Non renseignée' }
  if (Number(day.slice(0, 4)) >= 2099) return { tone: 'ok', label: 'Sans limite' }
  const [y, m, d] = day.split('-').map(Number)
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const days = Math.round((new Date(y, m - 1, d).getTime() - today.getTime()) / 86_400_000)
  if (days < 0) return { tone: 'expired', label: `Expirée depuis ${-days} j` }
  if (days <= EXPIRY_WARNING_DAYS) return { tone: 'soon', label: days === 0 ? "Expire aujourd'hui" : `Expire dans ${days} j` }
  return { tone: 'ok', label: 'Valide' }
}

function initialsOf(prenom: string, nom: string): string {
  return `${prenom.trim().charAt(0)}${nom.trim().charAt(0)}`.toUpperCase() || '?'
}

// Fiche personnel, utilisée par la modale (ou tout autre mode d'affichage,
// voir components/view-modes/) de création/modification de PersonnelPage.
// `initial` vaut null en création, sinon la fiche complète (GET
// /personnel/:id, adresse comprise). Les onglets Contrat / Contact /
// Documents ne concernent qu'un personnel existant — désactivés en création.
export function PersonnelForm({ initial, onSubmit, onCancel }: Props) {
  const [tab, setTab] = useState<TabId>('principal')
  const [form, setForm] = useState<PersonnelDto>({
    Civilite_Personnel: initial?.Civilite_Personnel ?? '',
    Nom_Personnel: initial?.Nom_Personnel ?? '',
    Prenom_Personnel: initial?.Prenom_Personnel ?? '',
    Telephone_portable: initial?.Telephone_portable ?? '',
    Telephone_fixe: initial?.Telephone_fixe ?? '',
    Telephone_autre: initial?.Telephone_autre ?? '',
    Telephone_professionnel: initial?.Telephone_professionnel ?? '',
    Description_telephone: initial?.Description_telephone ?? '',
    E_mail: initial?.E_mail ?? '',
    E_mail_professionnel: initial?.E_mail_professionnel ?? '',
    Num_service_social: initial?.Num_service_social ?? '',
    Num_registre_national: initial?.Num_registre_national ?? '',
    Date_naissance: toDateInput(initial?.Date_naissance),
    Lieu_naissance: initial?.Lieu_naissance ?? '',
    Pays_Naissance: initial?.Pays_Naissance ?? '',
    Etat_civil: initial?.Etat_civil ?? '',
    Nbr_personne_charge: initial?.Nbr_personne_charge ?? 0,
    Iban: initial?.Iban ?? '',
    Bic: initial?.Bic ?? '',
    Nom_Banque: initial?.Nom_Banque ?? '',
    Qualification: initial?.Qualification ?? '',
    Routier: initial?.Routier ?? 0,
    Manutention: initial?.Manutention ?? 0,
    Atelier: initial?.Atelier ?? 0,
    Commentaire_Personnel: initial?.Commentaire_Personnel ?? '',
    Date_validite_selection_medicale: toDateInput(initial?.Date_validite_selection_medicale),
    Date_validite_carte_chauffeur: toDateInput(initial?.Date_validite_carte_chauffeur),
    Date_validite_CAP: toDateInput(initial?.Date_validite_CAP),
    Date_validite_carte_identite: toDateInput(initial?.Date_validite_carte_identite),
    Date_validite_A1: toDateInput(initial?.Date_validite_A1),
    Date_validite_SIPSI: toDateInput(initial?.Date_validite_SIPSI),
    Adresse1: initial?.Adresse?.Adresse1 ?? '',
    Adresse2: initial?.Adresse?.Adresse2 ?? '',
    Adresse3: initial?.Adresse?.Adresse3 ?? '',
    CP: initial?.Adresse?.CP ?? '',
    Localite: initial?.Adresse?.Localite ?? '',
    Pays: initial?.Adresse?.Pays ?? '',
    Pays_full_name: initial?.Adresse?.Pays_full_name ?? '',
  })
  const [submitting, setSubmitting] = useState(false)
  const [paysOptions, setPaysOptions] = useState<PaysOption[]>([])
  const [villeOptions, setVilleOptions] = useState<VilleOption[]>([])

  // Liste des pays : ~240 lignes, chargée une seule fois (comme EntiteForm).
  useEffect(() => {
    apiJson<{ pays: PaysOption[] }>('pays')
      .then((json) => setPaysOptions(json.pays))
      .catch(() => {})
  }, [])

  // Villes du code postal saisi, avec un léger anti-rebond — la table en
  // compte ~39 000, impossible à charger en une fois.
  useEffect(() => {
    if (!form.CP.trim()) return
    const timeout = setTimeout(() => {
      apiJson<{ villes: VilleOption[] }>(`villes?cp=${encodeURIComponent(form.CP.trim())}`)
        .then((json) => setVilleOptions(json.villes))
        .catch(() => {})
    }, 300)
    return () => clearTimeout(timeout)
  }, [form.CP])

  const set = <K extends keyof PersonnelDto>(key: K, value: PersonnelDto[K]) => setForm((prev) => ({ ...prev, [key]: value }))
  const text = (key: keyof PersonnelDto, maxLength = 50) => ({
    value: String(form[key] ?? ''),
    maxLength,
    onChange: (e: { target: { value: string } }) => set(key, e.target.value as never),
  })

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

  // Un pays (ou une civilité) enregistré mais absent des listes reste
  // proposé, pour ne pas être perdu à l'enregistrement.
  const paysChoices = (current: string) =>
    current && !paysOptions.some((p) => p.ISO === current) ? [{ IDPAYS: 'current', ISO: current, Nom: current }, ...paysOptions] : paysOptions
  const civilites = form.Civilite_Personnel && !CIVILITES.includes(form.Civilite_Personnel) ? [form.Civilite_Personnel, ...CIVILITES] : CIVILITES
  const villes = form.CP.trim() ? villeOptions : []
  const fullName = [form.Prenom_Personnel, form.Nom_Personnel].filter((v) => v.trim()).join(' ')
  const activites = [
    { field: 'Routier', label: 'Routier' },
    { field: 'Manutention', label: 'Manutention' },
    { field: 'Atelier', label: 'Atelier' },
  ] as const

  return (
    <div className="pf">
      <FicheTabs
        ariaLabel="Sections de la fiche personnel"
        value={tab}
        onChange={setTab}
        tabs={TABS.map((t) => {
          const locked = t.id !== 'principal' && !initial
          return { ...t, disabled: locked, title: locked ? 'Enregistrez d’abord le personnel pour accéder à cet onglet' : undefined }
        })}
      />

      {/* Gardé monté : un contrat en cours de saisie survit au changement
          d'onglet. */}
      {initial && (
        <FichePanel active={tab === 'contrat'}>
          <PersonnelContratsTab
            personnelId={initial.IDPERSONNELS}
            defaults={{ Qualification: form.Qualification, Routier: form.Routier, Manutention: form.Manutention, Atelier: form.Atelier }}
          />
        </FichePanel>
      )}

      {tab === 'contact' && <Dev />}
      {tab === 'documents' && initial && <DocumentsTab type={DOCUMENT_CATEGORIE.PERSONNEL} referentId={initial.IDPERSONNELS} />}

      {tab === 'principal' && (
        <form ref={formRef} onSubmit={handleSubmit} className="pf-form">
          <div className="form-actions">
            <button type="button" className="btn" onClick={() => void confirmLeave(onCancel)}>Annuler</button>
            <button type="submit" className="btn primary" disabled={submitting}>
              {submitting ? 'Enregistrement...' : 'Enregistrer'}
            </button>
          </div>
          <header className="pf-hero">
            <span className="pf-avatar" aria-hidden="true">{initialsOf(form.Prenom_Personnel, form.Nom_Personnel)}</span>
            <div className="pf-hero-text">
              <strong>{fullName || (initial ? 'Sans nom' : 'Nouveau personnel')}</strong>
              <span>{[form.Civilite_Personnel, form.Qualification].filter(Boolean).join(' · ') || 'Renseignez l’identité ci-dessous'}</span>
            </div>
            <div className="pf-activities" role="group" aria-label="Activités">
              {activites.map((a) => (
                <button
                  key={a.field}
                  type="button"
                  className={`pf-chip${form[a.field] === 1 ? ' is-on' : ''}`}
                  aria-pressed={form[a.field] === 1}
                  onClick={() => set(a.field, form[a.field] === 1 ? 0 : 1)}
                >
                  {a.label}
                </button>
              ))}
            </div>
          </header>

          <div className="pf-columns">
            <div className="pf-column">
              <Section title="Identification">
                <div className="pf-grid">
                  <Field label="Civilité">
                    <select value={form.Civilite_Personnel} onChange={(e) => set('Civilite_Personnel', e.target.value)}>
                      <option value="">—</option>
                      {civilites.map((c) => <option key={c} value={c}>{c}</option>)}
                    </select>
                  </Field>
                  <Field label="Nom"><input {...text('Nom_Personnel')} required autoComplete="off" /></Field>
                  <Field label="Prénom"><input {...text('Prenom_Personnel')} autoComplete="off" /></Field>
                  <Field label="Date de naissance"><input type="date" value={form.Date_naissance} onChange={(e) => set('Date_naissance', e.target.value)} /></Field>
                  <Field label="Lieu de naissance"><input {...text('Lieu_naissance')} /></Field>
                  <Field label="Pays de naissance">
                    <select value={form.Pays_Naissance} onChange={(e) => set('Pays_Naissance', e.target.value)}>
                      <option value="">—</option>
                      {paysChoices(form.Pays_Naissance).map((p) => <option key={p.IDPAYS} value={p.ISO ?? ''}>{p.Nom}</option>)}
                    </select>
                  </Field>
                  <Field label="N° service social"><input {...text('Num_service_social')} /></Field>
                  <Field label="N° registre national"><input {...text('Num_registre_national')} /></Field>
                  <Field label="Personnes à charge">
                    <input type="number" min={0} max={127} value={form.Nbr_personne_charge} onChange={(e) => set('Nbr_personne_charge', Number(e.target.value))} />
                  </Field>
                  <Field label="Statut marital">
                    <input {...text('Etat_civil')} list="pf-etats-civils" />
                    <datalist id="pf-etats-civils">{ETATS_CIVILS.map((v) => <option key={v} value={v} />)}</datalist>
                  </Field>
                </div>
              </Section>

              <Section title="Validités">
                <div className="pf-validities">
                  {VALIDITES.map((v) => {
                    const state = validityOf(form[v.field])
                    return (
                      <label key={v.field} className="field pf-validity">
                        <span className="pf-validity-head">
                          <span>{v.label}</span>
                          <span className={`pf-badge pf-badge--${state.tone}`}>{state.label}</span>
                        </span>
                        <input type="date" value={form[v.field]} onChange={(e) => set(v.field, e.target.value)} />
                      </label>
                    )
                  })}
                </div>
              </Section>
            </div>

            <div className="pf-column">
              <Section title="Coordonnées">
                <div className="pf-grid">
                  <Field label="Téléphone fixe"><input type="tel" {...text('Telephone_fixe')} /></Field>
                  <Field label="Téléphone portable"><input type="tel" {...text('Telephone_portable')} /></Field>
                  <Field label="Téléphone autre"><input type="tel" {...text('Telephone_autre')} /></Field>
                  <Field label="Description"><input {...text('Description_telephone')} /></Field>
                  <Field label="Email" wide><input type="email" {...text('E_mail', 100)} /></Field>
                </div>
              </Section>

              <Section title="Adresse">
                <div className="pf-grid">
                  <Field label="Adresse" wide><input {...text('Adresse1', 250)} autoComplete="address-line1" /></Field>
                  <Field label="Complément" wide><input {...text('Adresse2', 250)} autoComplete="address-line2" /></Field>
                  <Field label="Complément 2" wide><input {...text('Adresse3', 250)} autoComplete="address-line3" /></Field>
                  <Field label="Code postal"><input {...text('CP')} autoComplete="postal-code" /></Field>
                  <Field label="Ville">
                    <input {...text('Localite')} list="pf-villes" autoComplete="address-level2" />
                    <datalist id="pf-villes">{villes.map((v) => <option key={v.IDVILLES} value={v.Nom_ville ?? ''} />)}</datalist>
                  </Field>
                  <Field label="Pays" wide>
                    <select
                      value={form.Pays}
                      onChange={(e) => {
                        const match = paysOptions.find((p) => p.ISO === e.target.value)
                        setForm((prev) => ({ ...prev, Pays: e.target.value, Pays_full_name: match?.Nom ?? '' }))
                      }}
                    >
                      <option value="">—</option>
                      {paysChoices(form.Pays).map((p) => <option key={p.IDPAYS} value={p.ISO ?? ''}>{p.Nom}</option>)}
                    </select>
                  </Field>
                </div>
              </Section>
            </div>

            <div className="pf-column">
              <Section title="Qualifications">
                <Field label="Qualification">
                  <input {...text('Qualification')} placeholder="Ex. Chauffeur, Mécanicien…" />
                </Field>
              </Section>

              <Section title="Coordonnées professionnelles">
                <div className="pf-grid">
                  <Field label="Téléphone professionnel" wide><input type="tel" {...text('Telephone_professionnel')} /></Field>
                  <Field label="Email professionnel" wide><input type="email" {...text('E_mail_professionnel')} /></Field>
                </div>
              </Section>

              <Section title="Coordonnées bancaires">
                <div className="pf-grid">
                  <Field label="Nom de la banque" wide><input {...text('Nom_Banque')} /></Field>
                  <Field label="IBAN"><input {...text('Iban')} className="pf-mono" autoComplete="off" /></Field>
                  <Field label="BIC"><input {...text('Bic')} className="pf-mono" autoComplete="off" /></Field>
                </div>
              </Section>

              <Section title="Commentaires">
                <textarea {...text('Commentaire_Personnel')} className="control" rows={3} aria-label="Commentaires" />
                <span className="pf-counter">{form.Commentaire_Personnel.length} / 50</span>
              </Section>
            </div>
          </div>

        </form>
      )}
    </div>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="pf-section">
      <h4 className="pf-section-title">{title}</h4>
      {children}
    </section>
  )
}

function Field({ label, wide, children }: { label: string; wide?: boolean; children: ReactNode }) {
  return (
    <label className={`field${wide ? ' pf-field--wide' : ''}`}>
      <span className="field-label">{label}</span>
      {children}
    </label>
  )
}
