import { useEffect, useRef, useState, type ChangeEvent, type DragEvent, type FormEvent } from 'react'
import { apiJson } from '../../lib/api.js'
import type { EntiteDto } from './useEntite.js'
import './EntiteForm.css'

type VilleOption = { IDVILLES: string; Nom_ville: string | null }
type PaysOption = { IDPAYS: string; ISO: string | null; Nom: string | null }

// Options de sécurisation de la connexion SMTP — non documentées côté DB
// (pas de table d'énumération pour ce champ, juste un Int côté Entite),
// déduites de la seule donnée existante (TypeConnexion_SMTP=2 ↔ "Option
// Sécurisé TLS" dans la maquette fournie).
const CONNEXION_SMTP_OPTIONS = [
  { value: 0, label: 'Aucune' },
  { value: 1, label: 'SSL' },
  { value: 2, label: 'Option Sécurisé TLS' },
]

type Props = {
  formId: string
  form: EntiteDto
  // Mots de passe SMTP déjà enregistrés (jamais renvoyés par l'API, voir
  // useEntite.ts) : sert seulement à indiquer « inchangé » dans le champ.
  smtpPasswordSet: boolean
  smtpPlanningPasswordSet: boolean
  onChange: (form: EntiteDto) => void
  onSubmit: (e: FormEvent) => void
}

const UNCHANGED_PLACEHOLDER = '•••••••• (inchangé)'

// Convertit un fichier image en data URI base64 ("data:image/png;base64,..."),
// le format attendu par EntiteDto.Logo (voir useEntite.ts) — envoyé tel quel
// au backend, qui le stocke tel quel (voir entite.service.ts) : prêt à
// resservir directement de `src` d'une <img>, sans aller-retour supplémentaire.
function readImageAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = () => reject(reader.error ?? new Error('Lecture du fichier impossible'))
    reader.readAsDataURL(file)
  })
}

// Formulaire "Coordonnée entreprise" — pas de bouton de soumission ici, il
// vit dans l'en-tête de EntitePage.tsx (attribut `form`, voir formId) pour
// rester visible en haut de page comme dans la maquette fournie par
// l'utilisateur.
export function EntiteForm({ formId, form, smtpPasswordSet, smtpPlanningPasswordSet, onChange, onSubmit }: Props) {
  const [villeOptions, setVilleOptions] = useState<VilleOption[]>([])
  const [paysOptions, setPaysOptions] = useState<PaysOption[]>([])
  const [logoDragOver, setLogoDragOver] = useState(false)
  const [logoError, setLogoError] = useState<string | null>(null)
  const logoFileInputRef = useRef<HTMLInputElement>(null)

  // Le logo n'est enregistré qu'au clic sur "Enregistrer" comme le reste du
  // formulaire (voir EntitePage.tsx) — pas d'envoi immédiat au dépôt, pour
  // rester cohérent avec tous les autres champs de cette fiche.
  async function applyLogoFile(file: File | undefined) {
    if (!file) return
    if (!file.type.startsWith('image/')) {
      setLogoError('Le logo doit être une image (PNG, JPG...)')
      return
    }
    try {
      const dataUrl = await readImageAsDataUrl(file)
      setLogoError(null)
      onChange({ ...form, Logo: dataUrl })
    } catch {
      setLogoError('Lecture du fichier impossible')
    }
  }

  function handleLogoDrop(e: DragEvent<HTMLDivElement>) {
    e.preventDefault()
    setLogoDragOver(false)
    applyLogoFile(e.dataTransfer.files[0])
  }

  function handleLogoFileInput(e: ChangeEvent<HTMLInputElement>) {
    applyLogoFile(e.target.files?.[0])
    e.target.value = ''
  }

  // Liste des pays : ~240 lignes, chargée une seule fois (voir
  // backend/src/pays/pays.service.ts).
  useEffect(() => {
    apiJson<{ pays: PaysOption[] }>('pays')
      .then((json) => setPaysOptions(json.pays))
      .catch(() => {})
  }, [])

  // Recherche des villes par code postal, avec un léger anti-rebond — la
  // table en compte ~39 000, impossible à charger en une fois (voir
  // backend/src/ville/ville.service.ts).
  useEffect(() => {
    if (!form.CP) {
      setVilleOptions([])
      return
    }
    const timeout = setTimeout(() => {
      apiJson<{ villes: VilleOption[] }>(`villes?cp=${encodeURIComponent(form.CP)}`)
        .then((json) => setVilleOptions(json.villes))
        .catch(() => {})
    }, 300)
    return () => clearTimeout(timeout)
  }, [form.CP])

  function handlePaysChange(nom: string) {
    const match = paysOptions.find((p) => p.Nom === nom)
    onChange({ ...form, Pays_full_name: nom, Pays: match?.ISO ?? '' })
  }

  // La ville actuellement enregistrée peut ne pas (ou plus) figurer dans
  // les résultats de recherche (CP pas encore recherché, ville renommée...)
  // — ajoutée à la liste pour ne pas perdre la valeur affichée.
  const villeChoices =
    form.Localite && !villeOptions.some((v) => v.Nom_ville === form.Localite)
      ? [{ IDVILLES: 'current', Nom_ville: form.Localite }, ...villeOptions]
      : villeOptions

  return (
    <form id={formId} className="entite-form" onSubmit={onSubmit}>
      <div
        className={`entite-logo-drop${logoDragOver ? ' drag-over' : ''}`}
        onDragOver={(e) => {
          e.preventDefault()
          setLogoDragOver(true)
        }}
        onDragLeave={() => setLogoDragOver(false)}
        onDrop={handleLogoDrop}
        onClick={() => logoFileInputRef.current?.click()}
        role="button"
        tabIndex={0}
      >
        {form.Logo ? (
          <img src={form.Logo} alt="Logo de l’entreprise" className="entite-logo-preview" />
        ) : (
          <span className="entite-logo-placeholder">Glissez une image ici, ou cliquez pour choisir un logo</span>
        )}
        <input ref={logoFileInputRef} type="file" accept="image/*" hidden onChange={handleLogoFileInput} />
        {form.Logo && (
          <button
            type="button"
            className="entite-logo-remove"
            onClick={(e) => {
              e.stopPropagation()
              onChange({ ...form, Logo: '' })
            }}
          >
            Retirer le logo
          </button>
        )}
      </div>
      {logoError && <p className="entite-logo-error">{logoError}</p>}

      <div className="entite-form-grid">
        <label className="field">
          Nom
          <input value={form.Nom_societe} onChange={(e) => onChange({ ...form, Nom_societe: e.target.value })} />
        </label>
        <label className="field">
          Téléphone
          <input value={form.Num_Telephone} onChange={(e) => onChange({ ...form, Num_Telephone: e.target.value })} />
        </label>
        <label className="field">
          Nom de la Banque
          <input value={form.Nom_Banque} onChange={(e) => onChange({ ...form, Nom_Banque: e.target.value })} />
        </label>

        <label className="field">
          Nom court
          <input value={form.Nom_court} onChange={(e) => onChange({ ...form, Nom_court: e.target.value })} />
        </label>
        <label className="field">
          Email
          <input type="email" value={form.Email_contact} onChange={(e) => onChange({ ...form, Email_contact: e.target.value })} />
        </label>
        <label className="field">
          IBAN
          <input value={form.Iban} onChange={(e) => onChange({ ...form, Iban: e.target.value })} />
        </label>

        <label className="field">
          Adresse 1
          <input value={form.Adresse1} onChange={(e) => onChange({ ...form, Adresse1: e.target.value })} />
        </label>
        <label className="field">
          N° TVA
          <input value={form.Num_TVA} onChange={(e) => onChange({ ...form, Num_TVA: e.target.value })} />
        </label>
        <label className="field">
          BIC
          <input value={form.Bic} onChange={(e) => onChange({ ...form, Bic: e.target.value })} />
        </label>

        <label className="field">
          Adresse 2
          <input value={form.Adresse2} onChange={(e) => onChange({ ...form, Adresse2: e.target.value })} />
        </label>
        <label className="field">
          Nom du signataire
          <input value={form.Signataire} onChange={(e) => onChange({ ...form, Signataire: e.target.value })} />
        </label>
        <label className="field">
          Numéro UCM
          <input value={form.numero_ucm} onChange={(e) => onChange({ ...form, numero_ucm: e.target.value })} />
        </label>

        <label className="field">
          Adresse 3
          <input value={form.Adresse3} onChange={(e) => onChange({ ...form, Adresse3: e.target.value })} />
        </label>
        <label className="field">
          Numéro de licence
          <input value={form.Num_licence} onChange={(e) => onChange({ ...form, Num_licence: e.target.value })} />
        </label>
        <label className="field">
          Valeur faciale chèque repas
          <input
            type="number"
            step="0.01"
            value={form.Valeur_facial_cheque_repas}
            onChange={(e) => onChange({ ...form, Valeur_facial_cheque_repas: Number(e.target.value) })}
          />
        </label>

        <div className="entite-field-group">
          <label className="field">
            Code postal
            <input value={form.CP} onChange={(e) => onChange({ ...form, CP: e.target.value })} />
          </label>
          <label className="field">
            Villes
            <select value={form.Localite} onChange={(e) => onChange({ ...form, Localite: e.target.value })}>
              <option value="">—</option>
              {villeChoices.map((v) => (
                <option key={v.IDVILLES} value={v.Nom_ville ?? ''}>
                  {v.Nom_ville}
                </option>
              ))}
            </select>
          </label>
        </div>

        <label className="field">
          Pays
          <select value={form.Pays_full_name} onChange={(e) => handlePaysChange(e.target.value)}>
            <option value="">—</option>
            {paysOptions.map((p) => (
              <option key={p.IDPAYS} value={p.Nom ?? ''}>
                {p.Nom}
              </option>
            ))}
          </select>
        </label>
      </div>

      <h3 className="entite-section-title">Configuration des paramètres e-mail</h3>

      {/* Une colonne par bloc : serveur, puis compte compta et compte
          planning (e-mail suivi de son mot de passe). */}
      <div className="entite-form-grid entite-form-grid-email">
        <div className="entite-email-column">
          <label className="field">
            Serveur :
            <input value={form.Seveur_SMTP} onChange={(e) => onChange({ ...form, Seveur_SMTP: e.target.value })} />
          </label>
          <div className="entite-field-group">
            <label className="field">
              Connexion SMTP :
              <select
                value={form.TypeConnexion_SMTP}
                onChange={(e) => onChange({ ...form, TypeConnexion_SMTP: Number(e.target.value) })}
              >
                {CONNEXION_SMTP_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="field entite-field-narrow">
              Port SMTP :
              <input type="number" value={form.Port_SMTP} onChange={(e) => onChange({ ...form, Port_SMTP: Number(e.target.value) })} />
            </label>
          </div>
        </div>

        <div className="entite-email-column">
          <label className="field">
            E-mail compta :
            <input value={form.Utilisateur_SMTP} onChange={(e) => onChange({ ...form, Utilisateur_SMTP: e.target.value })} />
          </label>
          <label className="field">
            Mot de passe compta :
            <input
              type="password"
              value={form.MDP_SMTP}
              placeholder={smtpPasswordSet ? UNCHANGED_PLACEHOLDER : undefined}
              autoComplete="new-password"
              onChange={(e) => onChange({ ...form, MDP_SMTP: e.target.value })}
            />
          </label>
        </div>

        <div className="entite-email-column">
          <label className="field">
            E-mail planning :
            <input value={form.Utilisateur_smtp_planning} onChange={(e) => onChange({ ...form, Utilisateur_smtp_planning: e.target.value })} />
          </label>
          <label className="field">
            Mot de passe planning :
            <input
              type="password"
              value={form.MDP_SMTP_Planning}
              placeholder={smtpPlanningPasswordSet ? UNCHANGED_PLACEHOLDER : undefined}
              autoComplete="new-password"
              onChange={(e) => onChange({ ...form, MDP_SMTP_Planning: e.target.value })}
            />
          </label>
        </div>
      </div>
    </form>
  )
}
