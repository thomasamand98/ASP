// ===== MODEL (logique métier) =====
// Documents joints à une fiche (onglets « Documents » des fiches Société,
// Personnel, Véhicule — voir frontend/src/features/liens-documents/).
//
// Deux tables : `documents` décrit le fichier, `lien_documents` le rattache
// à un enregistrement — TYPE (voir document-categorie.ts) dit de quelle
// table vient IDReferent. Un même document peut avoir plusieurs liens.
//
// Le contenu du fichier est enregistré sur disque, pas en base
// (Document_physique reste vide) : documents.Emplacement garde son chemin,
// relatif au dossier de stockage (PDF_STORAGE_DIR, voir .env.example).
import { Injectable, NotFoundException } from '@nestjs/common';
import { randomBytes } from 'node:crypto';
import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, extname, isAbsolute, relative, resolve } from 'node:path';
import { PrismaService } from '../prisma/prisma.service.js';
import { serializeBigInt } from '../prisma/serialize-bigint.js';

const SOUS_REPERTOIRE_DOCUMENTS = 'documents';

// Tailles des colonnes VARCHAR (voir schema.prisma).
const MAX_NOM = 100;
const MAX_EXTENSION = 50;

export type FichierRecu = { buffer: Buffer; originalname: string };
export type FichierDocument = { contenu: Buffer; nomFichier: string };

function racineStockage(): string {
  return resolve(process.env.PDF_STORAGE_DIR?.trim() || 'storage');
}

// Chemin absolu d'un Emplacement, ou null s'il sort du dossier de stockage
// (chemin absolu d'un poste WinDev, « .. ») : rien n'est lu ni supprimé
// en dehors de ce dossier.
function cheminFichier(emplacement: string | null): string | null {
  if (!emplacement) return null;
  const racine = racineStockage();
  const chemin = resolve(racine, emplacement);
  const relatif = relative(racine, chemin);
  return relatif && !relatif.startsWith('..') && !isAbsolute(relatif) ? chemin : null;
}

const hex = (id: Uint8Array) => Buffer.from(id).toString('hex');

@Injectable()
export class LienDocumentService {
  constructor(private readonly prisma: PrismaService) {}

  async getLiensDocuments(type: number, referentId: bigint) {
    const liens = await this.prisma.lienDocument.findMany({
      where: { TYPE: type, IDReferent: referentId },
      orderBy: { Date_heure_creation: 'desc' },
      select: { IDLIEN_DOCUMENTS: true, IDDOCUMENTS: true, Nom_lien: true, Date_heure_creation: true },
    });
    // Pas de relation Prisma entre les deux tables : jointure faite ici.
    const ids = liens.flatMap((lien) => (lien.IDDOCUMENTS ? [lien.IDDOCUMENTS] : []));
    const documents = await this.prisma.document.findMany({
      where: { IDDOCUMENTS: { in: ids } },
      select: { IDDOCUMENTS: true, Nom: true, NomFichier: true, Extension_Document: true, Commentaire: true },
    });
    const parId = new Map(documents.map((document) => [hex(document.IDDOCUMENTS), document]));

    return {
      documents: serializeBigInt(
        liens.map((lien) => {
          const document = lien.IDDOCUMENTS ? parId.get(hex(lien.IDDOCUMENTS)) : undefined;
          return {
            IDLIEN_DOCUMENTS: lien.IDLIEN_DOCUMENTS,
            Nom: lien.Nom_lien ?? document?.Nom ?? null,
            NomFichier: document?.NomFichier ?? null,
            Extension: document?.Extension_Document ?? null,
            Commentaire: document?.Commentaire ?? null,
            Date_heure_creation: lien.Date_heure_creation,
          };
        }),
      ),
    };
  }

  // Enregistre le fichier sur disque, puis crée le document et son lien. Le
  // fichier est nommé d'après l'identifiant du document (pas d'après son nom
  // d'origine) : deux fichiers de même nom ne s'écrasent pas.
  async createLienDocument(type: number, referentId: bigint, fichier: FichierRecu) {
    const nomFichier = fichier.originalname.slice(-MAX_NOM);
    // L'extension finit dans un nom de fichier sur disque : lettres et
    // chiffres seulement, sinon aucune.
    const extensionBrute = extname(nomFichier).slice(1).toLowerCase();
    const extension = /^[a-z0-9]+$/.test(extensionBrute) ? extensionBrute.slice(0, MAX_EXTENSION) : '';
    const id = randomBytes(32);
    const emplacement = `${SOUS_REPERTOIRE_DOCUMENTS}/${hex(id)}${extension ? `.${extension}` : ''}`;
    const chemin = resolve(racineStockage(), emplacement);
    await mkdir(dirname(chemin), { recursive: true });
    await writeFile(chemin, fichier.buffer);

    const maintenant = new Date();
    // IDUTILISATEURS_createur/modificateur ont un défaut DB de 0, qui viole
    // leur contrainte de clé étrangère : mis à null explicitement.
    const tracabilite = {
      Date_heure_creation: maintenant,
      Date_heure_modification: maintenant,
      IDUTILISATEURS_createur: null,
      IDUTILISATEURS_modificateur: null,
    };
    try {
      const [, lien] = await this.prisma.$transaction([
        this.prisma.document.create({
          data: {
            ...tracabilite,
            IDDOCUMENTS: id,
            Nom: nomFichier,
            NomFichier: nomFichier,
            Extension_Document: extension,
            Categorie: type,
            Emplacement: emplacement,
            Version: 1,
          },
        }),
        this.prisma.lienDocument.create({
          data: { ...tracabilite, TYPE: type, IDReferent: referentId, IDDOCUMENTS: id, Nom_lien: nomFichier },
          select: { IDLIEN_DOCUMENTS: true },
        }),
      ]);
      return serializeBigInt(lien);
    } catch (error) {
      await rm(chemin, { force: true });
      throw error;
    }
  }

  async getFichier(id: bigint): Promise<FichierDocument> {
    const lien = await this.prisma.lienDocument.findUnique({ where: { IDLIEN_DOCUMENTS: id }, select: { IDDOCUMENTS: true } });
    const document = lien?.IDDOCUMENTS
      ? await this.prisma.document.findUnique({
          where: { IDDOCUMENTS: lien.IDDOCUMENTS },
          select: { Nom: true, NomFichier: true, Emplacement: true },
        })
      : null;
    const chemin = cheminFichier(document?.Emplacement ?? null);
    if (!document || !chemin) throw new NotFoundException('Document introuvable.');
    try {
      return { contenu: await readFile(chemin), nomFichier: document.NomFichier || document.Nom || 'document' };
    } catch {
      throw new NotFoundException('Le fichier de ce document est introuvable sur le serveur.');
    }
  }

  // Supprime le lien ; le document et son fichier ne sont supprimés que s'il
  // n'est plus rattaché à aucune autre fiche.
  async deleteLienDocument(id: bigint) {
    const lien = await this.prisma.lienDocument.findUnique({ where: { IDLIEN_DOCUMENTS: id }, select: { IDDOCUMENTS: true } });
    if (!lien) throw new NotFoundException('Document introuvable.');
    const idDocument = lien.IDDOCUMENTS;

    const emplacement = await this.prisma.$transaction(async (tx) => {
      await tx.lienDocument.delete({ where: { IDLIEN_DOCUMENTS: id } });
      if (!idDocument || (await tx.lienDocument.count({ where: { IDDOCUMENTS: idDocument } })) > 0) return null;
      const document = await tx.document.findUnique({ where: { IDDOCUMENTS: idDocument }, select: { Emplacement: true } });
      if (!document) return null;
      await tx.document.delete({ where: { IDDOCUMENTS: idDocument } });
      return document.Emplacement;
    });

    const chemin = cheminFichier(emplacement);
    if (chemin) await rm(chemin, { force: true });
  }
}
