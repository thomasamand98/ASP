// Catégories de document (constantes DOCUMENT_CATEGORIE_* de WinDev) :
// valeur de lien_documents.TYPE et de documents.Categorie. Le TYPE dit à quel
// genre d'enregistrement se rapporte lien_documents.IDReferent (TYPE 6 :
// IDReferent = IDSOCIETES, TYPE 3 : IDPERSONNELS, TYPE 4 : IDVEHICULES...).
// Mêmes valeurs côté front : frontend/src/features/liens-documents/documentCategories.ts.
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
} as const;

const CATEGORIES: ReadonlySet<number> = new Set(Object.values(DOCUMENT_CATEGORIE));

export function isDocumentCategorie(value: number): boolean {
  return CATEGORIES.has(value);
}
