// Catégories de document (constantes DOCUMENT_CATEGORIE_* de WinDev) : le
// TYPE d'un lien dit à quel genre d'enregistrement il rattache le document.
// Mêmes valeurs côté backend : backend/src/lien-document/document-categorie.ts.
export const DOCUMENT_CATEGORIE = {
  CONTRAT: 1,
  FACTURE: 2,
  PERSONNEL: 3,
  VEHICULE: 4,
  CLIENT: 5,
  SOCIETE: 6,
  DIVERS: 7,
  NOTEDECREDIT: 8,
  PIECE_JOINTE_MAIL: 9,
  EXECUTION: 10,
} as const

export type DocumentCategorie = (typeof DOCUMENT_CATEGORIE)[keyof typeof DOCUMENT_CATEGORIE]
