import { useEffect, useState, type FormEvent } from 'react'
import { apiJson } from '../../lib/api'
import type { ContactDetail } from './useContacts.js'
import type { Societe } from '../societes/useSocietes.js'
import { useUnsavedForm } from '../../components/unsaved-changes/UnsavedChangesContext.js'
import { AdresseFields } from '../../components/AdresseFields.js'
import { adresseValues, type AdresseValues } from '../../lib/adresse'
import { BuildingIcon, FicheField, FicheHero, FicheSection, FicheSwitch, MailIcon, PhoneIcon } from '../../components/FicheLayout.js'
import './ContactForm.css'

// Champs scripturables d'un contact, mêmes clés que le CreateContactDto
// côté backend (backend/src/contact/contact.dto.ts). Personne_physique/
// Adresse_entreprise sont des codes 0/1 côté vrai modèle Prisma (pas des
// booléens) — voir les interrupteurs ci-dessous. L'adresse propre du
// contact est aplatie (AdresseValues), enregistrée à part par le backend.
export type ContactDto = AdresseValues & {
  Civilite: string
  Nom_contact: string
  Prenom_contact: string
  Telephone_portable: string
  Telephone_fixe: string
  Telephone_autre: string
  E_mail: string
  Remarque: string
  Personne_physique: number
  Adresse_entreprise: number
  description_telephone: string
  // Société principale (liste « Entreprise ») et fonction/service dans cette
  // société — lien SocieteContacts géré par le backend (createContact/
  // updateContact). Vide : aucune société principale.
  IDSOCIETES: string
  Fonction_contact: string
  Service_bureau: string
  // Création depuis la fiche Point : lie le nouveau contact à ce point
  // (ajouté par PointContactsTab, pas saisi dans la fiche).
  IDPOINTS?: string
}

type Props = {
  initial: ContactDetail | null
  // Sociétés pour la liste « Entreprise » — chargées par la page et passées
  // en prop (même principe que ContratForm).
  societes: Societe[]
  // Société présélectionnée en création (onglet Contacts de la fiche
  // Société, voir SocieteContactsTab.tsx) — ignorée en modification.
  defaultSocieteId?: string
  onSubmit: (dto: ContactDto) => Promise<void>
  onCancel: () => void
}

type SocieteAdresse = { Adresse: { Adresse1: string | null; CP: string | null; Localite: string | null } | null }

const CIVILITES = ['Monsieur', 'Madame', 'Mademoiselle']
// Suggestions seulement : la fonction reste en texte libre.
const FONCTIONS = ['Directeur Général', 'Gérant', 'Directeur', 'Responsable logistique', 'Responsable exploitation', 'Comptable', 'Commercial', 'Assistant(e)', 'Secrétaire']

function initialsOf(prenom: string, nom: string): string {
  return `${prenom.trim().charAt(0)}${nom.trim().charAt(0)}`.toUpperCase() || '?'
}

// Fiche contact, utilisée par la modale (ou tout autre mode d'affichage,
// voir components/view-modes/) de création/modification de ContactsPage.
// `initial` vaut null en création, sinon la fiche complète du contact (GET
// /contacts/:id, voir useContacts.ts) chargée par CrudPage.
export function ContactForm({ initial, societes, defaultSocieteId, onSubmit, onCancel }: Props) {
  const [principal, ...autresSocietes] = initial?.SocieteContacts ?? []
  const [form, setForm] = useState<ContactDto>({
    Civilite: initial?.Civilite ?? '',
    Nom_contact: initial?.Nom_contact ?? '',
    Prenom_contact: initial?.Prenom_contact ?? '',
    Telephone_portable: initial?.Telephone_portable ?? '',
    Telephone_fixe: initial?.Telephone_fixe ?? '',
    Telephone_autre: initial?.Telephone_autre ?? '',
    E_mail: initial?.E_mail ?? '',
    Remarque: initial?.Remarque ?? '',
    Personne_physique: initial?.Personne_physique ?? 1,
    Adresse_entreprise: initial?.Adresse_entreprise ?? 0,
    description_telephone: initial?.description_telephone ?? '',
    IDSOCIETES: initial ? (principal?.IDSOCIETES ?? '') : (defaultSocieteId ?? ''),
    Fonction_contact: principal?.Fonction_contact ?? '',
    Service_bureau: principal?.Service_bureau ?? '',
    ...adresseValues(initial?.Adresse),
  })
  const [submitting, setSubmitting] = useState(false)
  // Adresse chargée, avec l'id de sa société : une adresse d'une autre
  // société que celle choisie compte comme « en cours de chargement ».
  const [loadedAdresse, setLoadedAdresse] = useState<{ id: string; adresse: SocieteAdresse['Adresse'] } | null>(null)

  // Adresse de la société principale, affichée à la place de l'adresse du
  // contact quand « Adresse de l'entreprise » est activé.
  const useCompanyAddress = form.Adresse_entreprise === 1 && !!form.IDSOCIETES
  useEffect(() => {
    if (!useCompanyAddress) return
    const id = form.IDSOCIETES
    let cancelled = false
    apiJson<SocieteAdresse>(`societes/${id}`)
      .then((json) => { if (!cancelled) setLoadedAdresse({ id, adresse: json.Adresse }) })
      .catch(() => { if (!cancelled) setLoadedAdresse({ id, adresse: null }) })
    return () => { cancelled = true }
  }, [useCompanyAddress, form.IDSOCIETES])
  const societeAdresse = loadedAdresse?.id === form.IDSOCIETES ? loadedAdresse.adresse : undefined

  const set = <K extends keyof ContactDto>(key: K, value: ContactDto[K]) => setForm((prev) => ({ ...prev, [key]: value }))
  const text = (key: keyof ContactDto, maxLength = 50) => ({
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

  // Une civilité enregistrée mais absente de la liste reste proposée, pour
  // ne pas être perdue à l'enregistrement.
  const civilites = form.Civilite && !CIVILITES.includes(form.Civilite) ? [form.Civilite, ...CIVILITES] : CIVILITES
  const societe = societes.find((s) => s.IDSOCIETES === form.IDSOCIETES)
  const fullName = [form.Prenom_contact, form.Nom_contact].filter((v) => v.trim()).join(' ')
  const subtitle = [form.Fonction_contact, societe?.Nom_societe].filter(Boolean).join(' · ')
  const phone = form.Telephone_portable || form.Telephone_fixe

  return (
    <form ref={formRef} onSubmit={handleSubmit} className="fiche">
      <div className="form-actions">
        <button type="button" className="btn" onClick={() => void confirmLeave(onCancel)}>Annuler</button>
        <button type="submit" className="btn primary" disabled={submitting}>
          {submitting ? 'Enregistrement...' : 'Enregistrer'}
        </button>
      </div>

      <FicheHero
        badge={
          <span className={`fiche-avatar${form.Personne_physique === 1 ? '' : ' fiche-avatar--icon'}`} aria-hidden="true">
            {form.Personne_physique === 1 ? initialsOf(form.Prenom_contact, form.Nom_contact) : <BuildingIcon />}
          </span>
        }
        title={fullName || (initial ? 'Sans nom' : 'Nouveau contact')}
        subtitle={subtitle || 'Renseignez l’identité et l’entreprise ci-dessous'}
        actions={
          <>
            {form.E_mail.trim() && (
              <a className="btn sm" href={`mailto:${form.E_mail.trim()}`} title={`Écrire à ${form.E_mail.trim()}`}>
                <MailIcon /> Email
              </a>
            )}
            {phone.trim() && (
              <a className="btn sm" href={`tel:${phone.replace(/[^\d+]/g, '')}`} title={`Appeler le ${phone}`}>
                <PhoneIcon /> Appeler
              </a>
            )}
          </>
        }
      />

      <div className="fiche-columns">
        <div className="fiche-column">
          <FicheSection
            title="Renseignements"
            aside={
              <FicheSwitch
                label="Personne physique"
                checked={form.Personne_physique === 1}
                onChange={(on) => set('Personne_physique', on ? 1 : 0)}
              />
            }
          >
            <div className="fiche-grid cf-grid--identity">
              <FicheField label="Civilité">
                <select value={form.Civilite} onChange={(e) => set('Civilite', e.target.value)}>
                  <option value="">—</option>
                  {civilites.map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
              </FicheField>
              <FicheField label="Nom"><input {...text('Nom_contact')} autoComplete="off" /></FicheField>
              <FicheField label="Prénom"><input {...text('Prenom_contact')} autoComplete="off" /></FicheField>
            </div>
          </FicheSection>

          <FicheSection title="Entreprise">
            <div className="fiche-grid">
              <FicheField label="Société" wide>
                <select value={form.IDSOCIETES} onChange={(e) => set('IDSOCIETES', e.target.value)}>
                  <option value="">— Aucune —</option>
                  {societes.map((s) => <option key={s.IDSOCIETES} value={s.IDSOCIETES}>{s.Nom_societe}</option>)}
                </select>
              </FicheField>
              {form.IDSOCIETES && (
                <>
                  <FicheField label="Fonction">
                    <input {...text('Fonction_contact')} list="cf-fonctions" placeholder="Ex. Directeur Général" />
                    <datalist id="cf-fonctions">{FONCTIONS.map((f) => <option key={f} value={f} />)}</datalist>
                  </FicheField>
                  <FicheField label="Service"><input {...text('Service_bureau')} placeholder="Ex. Comptabilité" /></FicheField>
                </>
              )}
            </div>
            {autresSocietes.length > 0 && (
              <div className="cf-links">
                <span className="cf-links-label">Autres sociétés</span>
                <ul className="fiche-chips">
                  {autresSocietes.map((sc, i) => (
                    <li key={i} className="fiche-chip">
                      {sc.Societe?.Nom_societe ?? '—'}
                      {sc.Fonction_contact && <small>{sc.Fonction_contact}</small>}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </FicheSection>

          <FicheSection title="Commentaires">
            <textarea {...text('Remarque', 200)} className="control" rows={5} aria-label="Commentaires" placeholder="Notes internes sur ce contact…" />
            <span className="fiche-counter">{form.Remarque.length} / 200</span>
          </FicheSection>
        </div>

        <div className="fiche-column">
          <FicheSection title="Coordonnées">
            <div className="fiche-grid">
              <FicheField label="Téléphone fixe"><input type="tel" {...text('Telephone_fixe')} placeholder="+32 …" /></FicheField>
              <FicheField label="Téléphone portable"><input type="tel" {...text('Telephone_portable')} placeholder="+32 …" /></FicheField>
              <FicheField label="Téléphone autre"><input type="tel" {...text('Telephone_autre')} /></FicheField>
              <FicheField label="Description"><input {...text('description_telephone')} placeholder="Ex. Standard, domicile…" /></FicheField>
              <FicheField label="Email" wide><input type="email" {...text('E_mail', 100)} placeholder="nom@entreprise.com" /></FicheField>
            </div>
          </FicheSection>

          <FicheSection
            title="Adresse"
            aside={
              <FicheSwitch
                label="Adresse de l’entreprise"
                checked={form.Adresse_entreprise === 1}
                onChange={(on) => set('Adresse_entreprise', on ? 1 : 0)}
              />
            }
          >
            {form.Adresse_entreprise === 1 ? (
              <CompanyAddress societe={societe?.Nom_societe ?? null} adresse={form.IDSOCIETES ? societeAdresse : null} />
            ) : (
              <AdresseFields value={form} onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))} />
            )}
          </FicheSection>

          {initial && initial.PointContacts.length > 0 && (
            <FicheSection title="Points liés">
              <ul className="fiche-chips">
                {initial.PointContacts.map((pc, i) => (
                  <li key={i} className="fiche-chip">
                    {pc.Point?.Libelle ?? '—'}
                    {pc.Lien && <small>{pc.Lien}</small>}
                  </li>
                ))}
              </ul>
            </FicheSection>
          )}
        </div>
      </div>
    </form>
  )
}

// `adresse` : undefined pendant le chargement, null si aucune.
function CompanyAddress({ societe, adresse }: { societe: string | null; adresse: SocieteAdresse['Adresse'] | undefined }) {
  if (!societe) return <p className="fiche-empty">Choisissez une société dans « Entreprise » pour reprendre son adresse.</p>
  if (adresse === undefined) return <p className="fiche-empty">Chargement de l’adresse…</p>
  const lines = [adresse?.Adresse1, [adresse?.CP, adresse?.Localite].filter(Boolean).join(' ')].filter(Boolean)
  return (
    <div className="cf-company-address">
      <strong>{societe}</strong>
      {lines.length > 0 ? lines.map((l) => <span key={l}>{l}</span>) : <span className="fiche-empty">Aucune adresse renseignée pour cette société.</span>}
    </div>
  )
}
