// ===== CONTROLLER =====
// Reçoit les requêtes HTTP de la View et délègue au Model (LienDocumentService).
import {
  BadRequestException,
  Body,
  Controller,
  Delete,
  Get,
  Param,
  Post,
  Query,
  Res,
  UploadedFile,
  UseInterceptors,
} from '@nestjs/common';
import { FileInterceptor } from '@nestjs/platform-express';
import type { Response } from 'express';
import { extname } from 'node:path';
import { LienDocumentService } from './lien-document.service.js';
import { isDocumentCategorie } from './document-categorie.js';
import { ParseIdPipe } from '../common/params.js';

// Upload multipart (champ "file"), comme POST /mistral/ocr : la limite
// ci-dessous ne concerne que cette route ; au-delà, Multer répond 413.
const MAX_DOCUMENT_FILE_SIZE = 50 * 1024 * 1024;

// Seuls champs lus du fichier fourni par Multer — typés ici plutôt que via
// le type global Express.Multer.File, qui exigerait @types/multer.
type UploadedDocumentFile = { buffer: Buffer; originalname: string };

function parseType(raw: string | undefined): number {
  const type = Number(raw);
  if (!raw || !isDocumentCategorie(type)) throw new BadRequestException('Catégorie de document invalide.');
  return type;
}

@Controller()
export class LienDocumentController {
  constructor(private readonly lienDocumentService: LienDocumentService) {}

  // ?type=6&referentId=X (obligatoires) : documents liés à cet
  // enregistrement — voir document-categorie.ts pour les types.
  @Get('lien-documents')
  async getLiensDocuments(@Query('type') type: string | undefined, @Query('referentId', ParseIdPipe) referentId: bigint) {
    return this.lienDocumentService.getLiensDocuments(parseType(type), referentId);
  }

  // Champs texte "type" et "referentId" envoyés avec le fichier.
  @Post('lien-documents')
  @UseInterceptors(FileInterceptor('file', { limits: { fileSize: MAX_DOCUMENT_FILE_SIZE, files: 1 } }))
  async createLienDocument(
    @UploadedFile() file: UploadedDocumentFile | undefined,
    @Body('type') type: string | undefined,
    @Body('referentId', ParseIdPipe) referentId: bigint,
  ) {
    if (!file) throw new BadRequestException('Fichier requis (champ "file")');
    // Multer décode le nom du fichier en latin1 : les accents (envoyés en
    // UTF-8 par le navigateur) sont rétablis ici.
    const originalname = Buffer.from(file.originalname, 'latin1').toString('utf8');
    return this.lienDocumentService.createLienDocument(parseType(type), referentId, { buffer: file.buffer, originalname });
  }

  // `@Res()` sans `passthrough` : envoie le contenu du fichier tel quel (voir
  // ContratController.getContratPdf). `attachment` + `nosniff` : un fichier
  // déposé (HTML, SVG...) n'est jamais interprété par le navigateur s'il est
  // ouvert directement depuis l'API.
  @Get('lien-documents/:id/fichier')
  async getFichier(@Param('id', ParseIdPipe) id: bigint, @Res() res: Response) {
    const { contenu, nomFichier } = await this.lienDocumentService.getFichier(id);
    res.type(extname(nomFichier) || 'application/octet-stream');
    res.set({
      'Content-Disposition': `attachment; filename*=UTF-8''${encodeURIComponent(nomFichier)}`,
      'X-Content-Type-Options': 'nosniff',
    });
    res.send(contenu);
  }

  @Delete('lien-documents/:id')
  async deleteLienDocument(@Param('id', ParseIdPipe) id: bigint) {
    await this.lienDocumentService.deleteLienDocument(id);
  }
}
