import { useId, useState, type FormEvent } from 'react'
import type { PointDetail } from './usePoints.js'
import type { Societe } from '../societes/useSocietes.js'
import { useUnsavedForm } from '../../components/unsaved-changes/UnsavedChangesContext.js'
import { FichePanel, FicheTabs } from '../../components/FicheTabs.js'
import { FicheField, FicheHero, FicheSection, FicheSwitch, MapPinIcon, PhoneIcon } from '../../components/FicheLayout.js'
import { AdresseFields } from '../../components/AdresseFields.js'
import { adresseValues, formatAdresse, type AdresseValues } from '../../lib/adresse'
import { PointHoraires, type Horaire } from './PointHoraires.js'
import { PointContactsTab } from './PointContactsTab.js'
import './PointForm.css'

// Champs scripturables d'un point, mêmes clés que le CreatePointDto côté
// backend (backend/src/point/point.dto.ts) : IDSOCIETES/IDCONTACTS_DEFAUTS
// en string (BigInt non sérialisable côté JSON). Archive est un code 0/1
// côté vrai modèle Prisma (pas un booléen). L'adresse est aplatie
// (AdresseValues) et les plages horaires envoyées en entier : le backend
// enregistre les deux à part (l'IDADRESSES est géré par le serveur).
export type PointDto = AdresseValues & {
  Libelle: string
  Nom_societe: string
  Telephone: string
  IDSOCIETES: string
  Archive: number
  Lien_googleMap: string
  Instruction: string
  IDCONTACTS_DEFAUTS: string
  Horaires: Horaire[]
}

type Props = {
  initial: PointDetail | null
  // Valeurs préremplies en création (projection depuis une seule ligne,
  // voir components/projection/relations.ts) — ignorées en modification.
  defaults?: Partial<PointDto>
  // Liste des sociétés pour le sélecteur — chargée par PointsPage et
  // passée en prop plutôt que rechargée ici, pour ne pas refaire un GET
  // /societes à chaque ouverture de la modale.
  societes: Societe[]
  onSubmit: (dto: PointDto) => Promise<void>
  onCancel: () => void
}

type TabId = 'principal' | 'contacts' | 'carte'

// Libellé du point, calculé comme dans WinDev : « VILLE, NOM SOCIÉTÉ ».
function libelleOf(form: Pick<PointDto, 'Localite' | 'Nom_societe'>): string {
  return [form.Localite, form.Nom_societe].map((v) => v.trim()).filter(Boolean).join(', ')
}

const fullName = (c: { Nom_contact: string | null; Prenom_contact: string | null } | null | undefined) =>
  [c?.Prenom_contact, c?.Nom_contact].filter(Boolean).join(' ') || 'Sans nom'

// Fiche point, utilisée par la modale (ou tout autre mode d'affichage, voir
// components/view-modes/) de création/modification de PointsPage. `initial`
// vaut null en création, sinon la fiche complète du point (GET /points/:id,
// voir usePoints.ts) chargée par CrudPage. Le formulaire couvre les onglets
// Principale et Carte ; l'onglet Contacts (fiches contact complètes, avec
// leur propre enregistrement) est hors du <form> — pas de formulaires
// imbriqués — et réservé à un point déjà enregistré.
export function PointForm({ initial, defaults, societes, onSubmit, onCancel }: Props) {
  const formId = useId()
  const [tab, setTab] = useState<TabId>('principal')
  const [form, setForm] = useState<PointDto>({
    Libelle: initial?.Libelle ?? '',
    Nom_societe: initial?.Nom_societe ?? '',
    Telephone: initial?.Telephone ?? '',
    IDSOCIETES: initial?.IDSOCIETES ?? '',
    Archive: initial?.Archive ?? 0,
    Lien_googleMap: initial?.Lien_googleMap ?? '',
    Instruction: initial?.Instruction ?? '',
    IDCONTACTS_DEFAUTS: initial?.IDCONTACTS_DEFAUTS ?? '',
    ...adresseValues(initial?.Adresse),
    Horaires: initial?.Horaires ?? [],
    ...(initial ? {} : defaults),
  })
  const [submitting, setSubmitting] = useState(false)

  const set = <K extends keyof PointDto>(key: K, value: PointDto[K]) => setForm((prev) => ({ ...prev, [key]: value }))
  const text = (key: keyof PointDto, maxLength = 50) => ({
    value: String(form[key] ?? ''),
    maxLength,
    onChange: (e: { target: { value: string } }) => set(key, e.target.value as never),
  })

  // Le libellé suit la ville et le nom de société ; s'ils sont vides, le
  // libellé existant est gardé tel quel.
  const libelle = libelleOf(form) || form.Libelle

  async function save() {
    setSubmitting(true)
    try {
      await onSubmit({ ...form, Libelle: libelle })
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

  // Choisir une société reprend son nom (modifiable ensuite).
  function selectSociete(id: string) {
    const societe = societes.find((s) => s.IDSOCIETES === id)
    setForm((prev) => ({ ...prev, IDSOCIETES: id, Nom_societe: societe?.Nom_societe?.slice(0, 50) ?? prev.Nom_societe }))
  }

  const adresse = formatAdresse(form)
  const mapsUrl = form.Lien_googleMap.trim() || (adresse ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(adresse)}` : '')
  const contacts = initial?.PointContacts ?? []
  // Le contact par défaut se choisit parmi les contacts liés (à
  // l'ouverture de la fiche) ; un contact par défaut enregistré mais non lié
  // reste proposé.
  const defaultChoices = [
    ...(form.IDCONTACTS_DEFAUTS && !contacts.some((pc) => pc.IDCONTACTS === form.IDCONTACTS_DEFAUTS)
      ? [{ id: form.IDCONTACTS_DEFAUTS, label: fullName(initial?.ContactDefauts) }]
      : []),
    ...contacts.filter((pc) => pc.IDCONTACTS).map((pc) => ({ id: pc.IDCONTACTS as string, label: fullName(pc.Contact) })),
  ]

  return (
    <div className="fiche">
      <div className="form-actions">
        <button type="button" className="btn" onClick={() => void confirmLeave(onCancel)}>Annuler</button>
        <button type="submit" form={formId} className="btn primary" disabled={submitting}>
          {submitting ? 'Enregistrement...' : 'Enregistrer'}
        </button>
      </div>

      <FicheHero
        badge={<span className="fiche-avatar fiche-avatar--icon" aria-hidden="true"><MapPinIcon /></span>}
        title={libelle || (initial ? 'Sans libellé' : 'Nouveau point')}
        subtitle={adresse || 'Renseignez la société et l’adresse ci-dessous'}
        actions={
          <>
            {form.Archive === 1 && <span className="pt-archived">Archivé</span>}
            {form.Telephone.trim() && (
              <a className="btn sm" href={`tel:${form.Telephone.replace(/[^\d+]/g, '')}`} title={`Appeler le ${form.Telephone}`}>
                <PhoneIcon /> Appeler
              </a>
            )}
            {mapsUrl && (
              <a className="btn sm" href={mapsUrl} target="_blank" rel="noreferrer">
                <MapPinIcon size={14} /> Google Maps
              </a>
            )}
          </>
        }
      />

      <FicheTabs
        ariaLabel="Sections de la fiche point"
        value={tab}
        onChange={setTab}
        tabs={[
          { id: 'principal', label: 'Principale' },
          {
            id: 'contacts',
            label: 'Contacts',
            count: contacts.length,
            disabled: !initial,
            title: initial ? undefined : 'Enregistrez d’abord le point pour lui lier des contacts',
          },
          { id: 'carte', label: 'Carte' },
        ]}
      />

      <form id={formId} ref={formRef} onSubmit={handleSubmit} className="pt-form">
        <FichePanel active={tab === 'principal'}>
          <div className="pt-principal">
            <div className="fiche-columns">
              <div className="fiche-column">
                <FicheSection title="Identification" aside={<FicheSwitch label="Archivé" checked={form.Archive === 1} onChange={(on) => set('Archive', on ? 1 : 0)} />}>
                  <div className="fiche-grid">
                    <FicheField label="Libellé" wide>
                      <input value={libelle} readOnly tabIndex={-1} title="Calculé à partir de la ville et du nom de société" />
                    </FicheField>
                    <FicheField label="Société liée" wide>
                      <select value={form.IDSOCIETES} onChange={(e) => selectSociete(e.target.value)}>
                        <option value="">— Aucune —</option>
                        {societes.map((s) => <option key={s.IDSOCIETES} value={s.IDSOCIETES}>{s.Nom_societe}</option>)}
                      </select>
                    </FicheField>
                    <FicheField label="Nom société"><input {...text('Nom_societe')} /></FicheField>
                    <FicheField label="Téléphone"><input type="tel" {...text('Telephone')} placeholder="+32 …" /></FicheField>
                    <FicheField label="Contact par défaut" wide>
                      <select value={form.IDCONTACTS_DEFAUTS} onChange={(e) => set('IDCONTACTS_DEFAUTS', e.target.value)} disabled={defaultChoices.length === 0}>
                        <option value="">{defaultChoices.length === 0 ? '— Aucun contact lié —' : '— Aucun —'}</option>
                        {defaultChoices.map((c) => <option key={c.id} value={c.id}>{c.label}</option>)}
                      </select>
                    </FicheField>
                  </div>
                </FicheSection>

                <FicheSection title="Instruction">
                  <textarea {...text('Instruction', 300)} className="control" rows={4} aria-label="Instruction" placeholder="Consignes d’accès, quai, horaires particuliers…" />
                  <span className="fiche-counter">{form.Instruction.length} / 300</span>
                </FicheSection>
              </div>

              <div className="fiche-column">
                <FicheSection title="Adresse">
                  <AdresseFields value={form} onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))} />
                </FicheSection>
              </div>
            </div>

            <FicheSection title="Plage horaire" className="pt-hours">
              <PointHoraires value={form.Horaires} onChange={(horaires) => set('Horaires', horaires)} />
            </FicheSection>
          </div>
        </FichePanel>

        <FichePanel active={tab === 'carte'}>
          <FicheSection title="Localisation">
            <FicheField label="Lien Google Maps">
              <input type="url" {...text('Lien_googleMap', 300)} placeholder="https://maps.app.goo.gl/… (sinon, l’adresse est utilisée)" />
            </FicheField>
            {adresse ? (
              <iframe
                className="pt-map"
                title={`Carte : ${adresse}`}
                src={`https://maps.google.com/maps?q=${encodeURIComponent(adresse)}&z=15&output=embed`}
                loading="lazy"
                referrerPolicy="no-referrer-when-downgrade"
              />
            ) : (
              <p className="fiche-empty">Renseignez l’adresse du point pour afficher la carte.</p>
            )}
          </FicheSection>
        </FichePanel>
      </form>

      {/* Gardé monté : une fiche contact en cours de saisie survit au
          changement d'onglet. */}
      {initial && (
        <FichePanel active={tab === 'contacts'}>
          <PointContactsTab pointId={initial.IDPOINTS} />
        </FichePanel>
      )}
    </div>
  )
}
