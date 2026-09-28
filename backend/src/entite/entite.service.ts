// ===== MODEL (logique métier) =====
// Contrairement aux autres features sous src/, `entites` ne contient
// qu'une seule ligne (les coordonnées de la société courante) — pas de
// liste, pas de création/suppression, uniquement lecture et modification
// de cet unique enregistrement.
import { Injectable } from '@nestjs/common';
import { Prisma } from '../generated/prisma/client.js';
import { PrismaService } from '../prisma/prisma.service.js';
import { serializeBigInt } from '../prisma/serialize-bigint.js';
import { adresseFieldNames, adresseSelect, pickAdresse, saveAdresse } from '../common/adresse.js';
import { UpdateEntiteDto } from './entite.dto.js';

export const entiteSelect = {
  IDSignaletique: true,
  Nom_societe: true,
  Nom_court: true,
  Num_Telephone: true,
  Email_contact: true,
  Num_TVA: true,
  Nom_Banque: true,
  Iban: true,
  Bic: true,
  Signataire: true,
  numero_ucm: true,
  Num_licence: true,
  Valeur_facial_cheque_repas: true,
  Seveur_SMTP: true,
  Port_SMTP: true,
  Utilisateur_SMTP: true,
  TypeConnexion_SMTP: true,
  Utilisateur_smtp_planning: true,
  IDADRESSES: true,
} satisfies Prisma.EntiteSelect;

// Mots de passe SMTP : jamais renvoyés au client. getEntite() indique
// seulement s'ils sont renseignés, et updateEntite() ne les remplace que si
// une nouvelle valeur non vide est envoyée.
const secretFieldNames = ['MDP_SMTP', 'MDP_SMTP_Planning'] as const;

@Injectable()
export class EntiteService {
  constructor(private readonly prisma: PrismaService) {}

  // Pas de relation Prisma déclarée entre Entite et Adresse (contrairement
  // à Societe/Point) — l'adresse est rechargée séparément via IDADRESSES.
  //
  // Logo : stocké tel quel comme les octets UTF-8 de sa data URI complète
  // ("data:image/png;base64,...", voir EntiteForm.tsx) dans la colonne
  // binaire — pas de colonne séparée pour le type MIME, superflue pour une
  // simple image de logo. Reconverti en chaîne ici, prête à servir
  // directement de `src` d'une <img>, côté formulaire comme dans l'éditeur
  // de modèles (voir mergeFields.ts, Entite.Logo).
  async getEntite() {
    const { MDP_SMTP, MDP_SMTP_Planning, Logo, ...entite } = await this.prisma.entite.findFirstOrThrow({
      select: { ...entiteSelect, MDP_SMTP: true, MDP_SMTP_Planning: true, Logo: true },
    });
    const adresse = entite.IDADRESSES
      ? await this.prisma.adresse.findUnique({ where: { IDADRESSES: entite.IDADRESSES }, select: adresseSelect })
      : null;
    return serializeBigInt({
      ...entite,
      MDP_SMTP_defini: Boolean(MDP_SMTP),
      MDP_SMTP_Planning_defini: Boolean(MDP_SMTP_Planning),
      Logo: Logo ? Buffer.from(Logo).toString('utf-8') : null,
      Adresse: adresse,
    });
  }

  // Sépare les champs d'adresse (aplatis côté DTO) du reste, et les
  // persiste dans la table `adresses` liée via IDADRESSES — créée si
  // l'entité n'en a pas encore (cas normalement jamais rencontré en
  // pratique, la ligne existante en a toujours une). Voir common/adresse.ts.
  async updateEntite(dto: UpdateEntiteDto) {
    const current = await this.prisma.entite.findFirstOrThrow();

    const addressData = pickAdresse(dto);
    // Logo traité séparément (converti en Buffer, voir plus bas) : exclu ici
    // par déstructuration plutôt que `delete`, pour que son absence soit
    // aussi reflétée dans le type de `entiteFields` (sinon TS le voit encore
    // comme `string | undefined`, incompatible avec le Buffer attendu par
    // Prisma pour ce champ).
    const { Logo: logoDataUri, ...dtoWithoutLogo } = dto;
    const entiteFields = { ...dtoWithoutLogo };
    for (const field of adresseFieldNames) delete entiteFields[field];
    // Champ mot de passe vide = « inchangé » (le formulaire ne connaît pas
    // la valeur actuelle et l'envoie vide s'il n'est pas modifié).
    for (const field of secretFieldNames) {
      if (!entiteFields[field]) delete entiteFields[field];
    }
    // Logo : contrairement aux mots de passe, une valeur vide l'efface — le
    // formulaire connaît toujours le logo actuel (affiché en aperçu), donc
    // vide signifie ici une vraie suppression voulue, pas "inchangé".
    const logoData = logoDataUri !== undefined ? { Logo: logoDataUri ? Buffer.from(logoDataUri, 'utf-8') : null } : {};

    await this.prisma.$transaction(async (tx) => {
      const idAdresses = await saveAdresse(tx, current.IDADRESSES, addressData);
      await tx.entite.update({
        where: { IDSignaletique: current.IDSignaletique },
        data: { ...entiteFields, ...logoData, IDADRESSES: idAdresses !== current.IDADRESSES ? idAdresses : undefined },
      });
    });

    return this.getEntite();
  }
}
