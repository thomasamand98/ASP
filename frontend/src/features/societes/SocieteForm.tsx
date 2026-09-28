import { useState, type FormEvent } from 'react'
import type { SocieteDetail } from './useSocietes.js'
import type { Contact } from '../contacts/useContacts.js'
import { Dev } from '../enDeveloppement/Dev.js'
import { SocieteContactsTab } from './SocieteContactsTab.js'
import { SocieteContratsTab } from './SocieteContratsTab.js'
import { SocieteMessagesTab } from './SocieteMessagesTab.js'
import { FichePanel, FicheTabs } from '../../components/FicheTabs.js'
import './SocieteForm.css'
import { useUnsavedForm } from '../../components/unsaved-changes/UnsavedChangesContext.js'
import { AdresseFields } from '../../components/AdresseFields.js'
import { adresseValues, type AdresseValues } from '../../lib/adresse'

// Champs scripturables d'une société, mêmes clés que le CreateSocieteDto
// côté backend (backend/src/societe/societe.dto.ts). Prospect/Archive/
// Client_Facture_mail/Client_Adresse_facturation_societe/Fournisseur_* (les
// mêmes) sont des codes 0/1 côté vrai modèle Prisma (pas des booléens).
// Les adresses sont saisies champ par champ (adresse de la société aplatie,
// adresses de facturation en objets) ; leurs IDADRESSES restent côté serveur.
export type SocieteDto = AdresseValues & {
  Nom_societe: string
  Denomination: string
  TVA: string
  Activite: string
  Site_web: string
  Note: string
  Prospect: number
  Archive: number

  Client_Numero_client: string
  Client_Delai_paiement: string
  Client_Taux_tva: string
  Client_E_mail_comptabilite: string
  Client_Facture_mail: number
  Client_Adresse_facturation_societe: number
  Client_Adresse_facturation: AdresseValues
  Client_IDCONTACTS_Comptabilite: string

  Fournisseur_Numero_fournisseur: string
  Fournisseur_Delai_paiement: string
  Fournisseur_Taux_tva: string
  Fournisseur_E_mail_comptabilite: string
  Fournisseur_Facture_mail: number
  Fournisseur_Adresse_facturation_societe: number
  Fournisseur_Adresse_facturation: AdresseValues
  Fournisseur_IDCONTACTS_Comptabilite: string
  Fournisseur_Iban: string
  Fournisseur_Bic: string
}

type Props = {
  initial: SocieteDetail | null
  // Liste des contacts pour les sélecteurs « Contact comptabilité »/
  // « Contact planning » — chargée par SocietesPage et passée en prop,
  // même logique que `societes` dans ContratForm.tsx.
  contacts: Contact[]
  onSubmit: (dto: SocieteDto) => Promise<void>
  onCancel: () => void
}

const TOP_TABS = [
  { id: 'principale', label: 'Principale' },
  { id: 'contacts', label: 'Contacts' },
  { id: 'contrats', label: 'Contrats / Offres' },
  { id: 'documents', label: 'Documents' },
  { id: 'messages', label: 'Msg envoyés' },
] as const

type TopTabId = (typeof TOP_TABS)[number]['id']

function contactLabel(c: { Civilite?: string | null; Nom_contact: string | null; Prenom_contact: string | null }): string {
  return [c.Nom_contact, c.Prenom_contact].filter(Boolean).join(' ') || '—'
}

// Formulaire de saisie utilisé par la modale de création/modification (voir
// SocietesPage.tsx). `initial` vaut null en création, sinon la fiche
// complète de la société (GET /societes/:id, voir useSocietes.ts) chargée
// par CrudPage avant l'ouverture de la modale. Les onglets Contacts/
// Contrats/Documents/Messages ne portent que de la consultation liée à un
// enregistrement existant — désactivés tant que la société n'est pas créée.
export function SocieteForm({ initial, contacts, onSubmit, onCancel }: Props) {
  const [topTab, setTopTab] = useState<TopTabId>('principale')
  const [billingTab, setBillingTab] = useState<'client' | 'fournisseur'>('client')
  const [form, setForm] = useState<SocieteDto>({
    Nom_societe: initial?.Nom_societe ?? '',
    Denomination: initial?.Denomination ?? '',
    TVA: initial?.TVA ?? '',
    Activite: initial?.Activite ?? '',
    Site_web: initial?.Site_web ?? '',
    Note: initial?.Note ?? '',
    Prospect: initial?.Prospect ?? 0,
    Archive: initial?.Archive ?? 0,
    ...adresseValues(initial?.Adresse),

    Client_Numero_client: initial?.Client?.Numero_client ?? '',
    Client_Delai_paiement: initial?.Client?.Delai_paiement ?? '',
    Client_Taux_tva: initial?.Client?.Taux_tva ?? '',
    Client_E_mail_comptabilite: initial?.Client?.E_mail_comptabilite ?? '',
    Client_Facture_mail: initial?.Client?.Facture_mail ?? 0,
    Client_Adresse_facturation_societe: initial?.Client?.Adresse_facturation_societe ?? 1,
    Client_Adresse_facturation: adresseValues(initial?.Client?.Adresse_facturation),
    Client_IDCONTACTS_Comptabilite: initial?.Client?.IDCONTACTS_Comptabilite ?? '',

    Fournisseur_Numero_fournisseur: initial?.Fournisseur?.Numero_fournisseur ?? '',
    Fournisseur_Delai_paiement: initial?.Fournisseur?.Delai_paiement ?? '',
    Fournisseur_Taux_tva: initial?.Fournisseur?.Taux_tva ?? '',
    Fournisseur_E_mail_comptabilite: initial?.Fournisseur?.E_mail_comptabilite ?? '',
    Fournisseur_Facture_mail: initial?.Fournisseur?.Facture_mail ?? 0,
    Fournisseur_Adresse_facturation_societe: initial?.Fournisseur?.Adresse_facturation_societe ?? 1,
    Fournisseur_Adresse_facturation: adresseValues(initial?.Fournisseur?.Adresse_facturation),
    Fournisseur_IDCONTACTS_Comptabilite: initial?.Fournisseur?.IDCONTACTS_Comptabilite ?? '',
    Fournisseur_Iban: initial?.Fournisseur?.Iban ?? '',
    Fournisseur_Bic: initial?.Fournisseur?.Bic ?? '',
  })
  const [submitting, setSubmitting] = useState(false)

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

  return (
    <div className="societe-form">
      <FicheTabs
        ariaLabel="Sections de la fiche société"
        value={topTab}
        onChange={setTopTab}
        tabs={TOP_TABS.map((tab) => {
          const locked = tab.id !== 'principale' && !initial
          return { ...tab, disabled: locked, title: locked ? 'Enregistrez d’abord la société pour accéder à cet onglet' : undefined }
        })}
      />

      {topTab === 'principale' && (
        <form ref={formRef} onSubmit={handleSubmit} className="societe-form-principale">
          <div className="form-actions">
            <button type="button" className="btn" onClick={() => void confirmLeave(onCancel)}>Annuler</button>
            <button type="submit" className="btn primary" disabled={submitting}>
              {submitting ? 'Enregistrement...' : 'Enregistrer'}
            </button>
          </div>

          <div className="societe-form-columns">
            <section className="societe-form-section">
              <h4 className="societe-form-section-title">Coordonnées</h4>
              <label className="field">
                Nom
                <input value={form.Nom_societe} onChange={(e) => setForm({ ...form, Nom_societe: e.target.value })} />
              </label>
              <label className="field">
                Dénomination
                <input value={form.Denomination} onChange={(e) => setForm({ ...form, Denomination: e.target.value })} />
              </label>
              <label className="field">
                N° TVA
                <input value={form.TVA} onChange={(e) => setForm({ ...form, TVA: e.target.value })} />
              </label>
              <label className="field">
                Activité
                <input value={form.Activite} onChange={(e) => setForm({ ...form, Activite: e.target.value })} />
              </label>
              <label className="field">
                Site web
                <input value={form.Site_web} onChange={(e) => setForm({ ...form, Site_web: e.target.value })} />
              </label>
              <div className="societe-field-row">
                <label className="field">
                  Prospect
                  <select value={form.Prospect} onChange={(e) => setForm({ ...form, Prospect: Number(e.target.value) })}>
                    <option value={0}>Non</option>
                    <option value={1}>Oui</option>
                  </select>
                </label>
                <label className="field">
                  Archivée
                  <select value={form.Archive} onChange={(e) => setForm({ ...form, Archive: Number(e.target.value) })}>
                    <option value={0}>Non</option>
                    <option value={1}>Oui</option>
                  </select>
                </label>
              </div>
              <AdresseFields value={form} onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))} />
              <label className="field">
                Note
                <textarea rows={4} value={form.Note} onChange={(e) => setForm({ ...form, Note: e.target.value })} />
              </label>
            </section>

            <section className="societe-form-section societe-form-billing">
              <FicheTabs
                ariaLabel="Facturation"
                value={billingTab}
                onChange={setBillingTab}
                tabs={[
                  { id: 'client', label: 'Client' },
                  { id: 'fournisseur', label: 'Fournisseur' },
                ]}
              />

              {billingTab === 'client' ? (
                <div className="societe-billing-panel">
                  <h4 className="societe-form-section-title">Données pour la facturation client</h4>
                  <div className="societe-field-row">
                    <label className="field">
                      Numéro client
                      <input value={form.Client_Numero_client} onChange={(e) => setForm({ ...form, Client_Numero_client: e.target.value })} />
                    </label>
                    <label className="field">
                      Délai de paiement
                      <input value={form.Client_Delai_paiement} onChange={(e) => setForm({ ...form, Client_Delai_paiement: e.target.value })} />
                    </label>
                  </div>
                  <div className="societe-field-row">
                    <label className="field">
                      Taux de TVA (%)
                      <input value={form.Client_Taux_tva} onChange={(e) => setForm({ ...form, Client_Taux_tva: e.target.value })} />
                    </label>
                    <label className="field">
                      E-mail comptabilité
                      <input type="email" value={form.Client_E_mail_comptabilite} onChange={(e) => setForm({ ...form, Client_E_mail_comptabilite: e.target.value })} />
                    </label>
                  </div>
                  <label className="field">
                    Contact pour la comptabilité
                    <select
                      value={form.Client_IDCONTACTS_Comptabilite}
                      onChange={(e) => setForm({ ...form, Client_IDCONTACTS_Comptabilite: e.target.value })}
                    >
                      <option value="">—</option>
                      {contacts.map((c) => (
                        <option key={c.IDCONTACTS} value={c.IDCONTACTS}>{contactLabel(c)}</option>
                      ))}
                    </select>
                  </label>
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={form.Client_Facture_mail === 1}
                      onChange={(e) => setForm({ ...form, Client_Facture_mail: e.target.checked ? 1 : 0 })}
                    />
                    Facture e-mail
                  </label>
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={form.Client_Adresse_facturation_societe === 1}
                      onChange={(e) => setForm({ ...form, Client_Adresse_facturation_societe: e.target.checked ? 1 : 0 })}
                    />
                    Adresse de facturation = adresse de la société
                  </label>
                  {form.Client_Adresse_facturation_societe !== 1 && (
                    <AdresseFields
                      value={form.Client_Adresse_facturation}
                      onChange={(patch) => setForm((prev) => ({ ...prev, Client_Adresse_facturation: { ...prev.Client_Adresse_facturation, ...patch } }))}
                    />
                  )}
                </div>
              ) : (
                <div className="societe-billing-panel">
                  <h4 className="societe-form-section-title">Données pour la facturation fournisseur</h4>
                  <div className="societe-field-row">
                    <label className="field">
                      Numéro fournisseur
                      <input
                        value={form.Fournisseur_Numero_fournisseur}
                        onChange={(e) => setForm({ ...form, Fournisseur_Numero_fournisseur: e.target.value })}
                      />
                    </label>
                    <label className="field">
                      Délai de paiement
                      <input
                        value={form.Fournisseur_Delai_paiement}
                        onChange={(e) => setForm({ ...form, Fournisseur_Delai_paiement: e.target.value })}
                      />
                    </label>
                  </div>
                  <div className="societe-field-row">
                    <label className="field">
                      Taux de TVA (%)
                      <input value={form.Fournisseur_Taux_tva} onChange={(e) => setForm({ ...form, Fournisseur_Taux_tva: e.target.value })} />
                    </label>
                    <label className="field">
                      E-mail comptabilité
                      <input
                        type="email"
                        value={form.Fournisseur_E_mail_comptabilite}
                        onChange={(e) => setForm({ ...form, Fournisseur_E_mail_comptabilite: e.target.value })}
                      />
                    </label>
                  </div>
                  <div className="societe-field-row">
                    <label className="field">
                      IBAN
                      <input value={form.Fournisseur_Iban} onChange={(e) => setForm({ ...form, Fournisseur_Iban: e.target.value })} />
                    </label>
                    <label className="field">
                      BIC
                      <input value={form.Fournisseur_Bic} onChange={(e) => setForm({ ...form, Fournisseur_Bic: e.target.value })} />
                    </label>
                  </div>
                  <label className="field">
                    Contact pour la comptabilité
                    <select
                      value={form.Fournisseur_IDCONTACTS_Comptabilite}
                      onChange={(e) => setForm({ ...form, Fournisseur_IDCONTACTS_Comptabilite: e.target.value })}
                    >
                      <option value="">—</option>
                      {contacts.map((c) => (
                        <option key={c.IDCONTACTS} value={c.IDCONTACTS}>{contactLabel(c)}</option>
                      ))}
                    </select>
                  </label>
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={form.Fournisseur_Facture_mail === 1}
                      onChange={(e) => setForm({ ...form, Fournisseur_Facture_mail: e.target.checked ? 1 : 0 })}
                    />
                    Facture e-mail
                  </label>
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={form.Fournisseur_Adresse_facturation_societe === 1}
                      onChange={(e) => setForm({ ...form, Fournisseur_Adresse_facturation_societe: e.target.checked ? 1 : 0 })}
                    />
                    Adresse de facturation = adresse de la société
                  </label>
                  {form.Fournisseur_Adresse_facturation_societe !== 1 && (
                    <AdresseFields
                      value={form.Fournisseur_Adresse_facturation}
                      onChange={(patch) => setForm((prev) => ({ ...prev, Fournisseur_Adresse_facturation: { ...prev.Fournisseur_Adresse_facturation, ...patch } }))}
                    />
                  )}
                </div>
              )}
            </section>
          </div>
        </form>
      )}

      {/* Onglets tableau gardés montés : une fiche contact ou contrat en
          cours de saisie survit au changement d'onglet. */}
      {initial && (
        <FichePanel active={topTab === 'contacts'}>
          <SocieteContactsTab societeId={initial.IDSOCIETES} />
        </FichePanel>
      )}

      {initial && (
        <FichePanel active={topTab === 'contrats'}>
          <SocieteContratsTab societeId={initial.IDSOCIETES} />
        </FichePanel>
      )}
      {topTab === 'documents' && <Dev />}
      {topTab === 'messages' && initial && <SocieteMessagesTab societeId={initial.IDSOCIETES} />}
    </div>
  )
}
