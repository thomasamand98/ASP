import { useApiList, type ApiListPagination } from '../../lib/useApiList.js'
import type { AdresseValues } from '../../lib/adresse'

// Adresse résolue telle que renvoyée par l'API (champs NULL possibles).
type Adresse = { [K in keyof AdresseValues]: string | null }

// Forme d'une société telle que renvoyée par le Controller backend
// (backend/src/societe/societe.controller.ts → GET /societes), qui reflète
// directement le `societeSelect` de societe.service.ts. IDSOCIETES est un
// BigInt côté Prisma, converti en string côté service pour rester
// sérialisable en JSON.
export type Societe = {
  IDSOCIETES: string
  Nom_societe: string | null
  Denomination: string | null
  TVA: string | null
  Activite: string | null
  Site_web: string | null
}

// Données de facturation d'un Client/Fournisseur, telles que résolues dans
// la fiche Société (au lieu des seuls IDCLIENTS/IDFOURNISSEURS) — voir
// clientBillingSelect/fournisseurBillingSelect et getSociete dans
// societe.service.ts (adresse de facturation résolue, sans son id).
export type ClientBilling = {
  IDCLIENTS: string
  Numero_client: string | null
  Delai_paiement: string | null
  Taux_tva: string | null
  E_mail_comptabilite: string | null
  Facture_mail: number | null
  Adresse_facturation_societe: number | null
  Adresse_facturation: Adresse | null
  IDCONTACTS_Comptabilite: string | null
}

export type FournisseurBilling = {
  IDFOURNISSEURS: string
  Numero_fournisseur: string | null
  Delai_paiement: string | null
  Taux_tva: string | null
  E_mail_comptabilite: string | null
  Facture_mail: number | null
  Adresse_facturation_societe: number | null
  Adresse_facturation: Adresse | null
  IDCONTACTS_Comptabilite: string | null
  Iban: string | null
  Bic: string | null
}

// Forme complète d'une société telle que renvoyée par GET /societes/:id
// (backend/src/societe/societe.controller.ts), qui reflète directement le
// `societeDetailSelect` de societe.service.ts — tous les champs, plus
// l'adresse résolue, les données de facturation Client/Fournisseur
// résolues et la totalité des contacts rattachés.
export type SocieteDetail = {
  IDSOCIETES: string
  Nom_societe: string | null
  Denomination: string | null
  TVA: string | null
  Activite: string | null
  Site_web: string | null
  Note: string | null
  Prospect: number | null
  Archive: number | null
  Adresse: Adresse | null
  Client: ClientBilling | null
  Fournisseur: FournisseurBilling | null
  SocieteContacts: {
    Type_lien: string | null
    Fonction_contact: string | null
    Service_bureau: string | null
    Contact: { Nom_contact: string | null; Prenom_contact: string | null } | null
  }[]
}

// Récupère la liste des sociétés depuis l'API au montage du composant
// qui l'utilise (voir lib/useApiList.ts pour la logique fetch partagée).
// `pagination` optionnel : voir ApiListPagination (useApiList.ts). Omis
// (ex. pour les sélecteurs Société de ContratForm/PointForm/...), la liste
// complète est chargée comme avant.
export function useSocietes(pagination?: ApiListPagination) {
  const { data: societes, setData: setSocietes, loading, error, refetch, total } = useApiList<Societe>('societes', pagination)
  return { societes, setSocietes, loading, error, refetch, total }
}
