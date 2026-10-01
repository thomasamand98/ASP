// ===== CONTROLLER =====
// Reçoit les requêtes HTTP de la View et délègue au Model (PerformanceService).
import { BadRequestException, Controller, Get, Post, Query, Res } from '@nestjs/common';
import type { Response } from 'express';
import { ParseIdPipe } from '../common/params.js';
import { PerformanceService, type ModeTraitement } from './performance.service.js';

const DUREE_PAR_DEFAUT_S = 60;
const DUREE_MAX_S = 300;
const EXEMPLAIRES_MAX = 20;
// 50 Mo de JSON : au-delà, la génération occuperait trop le serveur.
const TAILLE_MAX_KO = 51_200;

@Controller('performance')
export class PerformanceController {
  constructor(private readonly performanceService: PerformanceService) {}

  // ?mode=attente|cpu (défaut : attente) &duree=secondes (défaut : 60, max 300).
  @Post('traitement-long')
  async lancerTraitementLong(@Query('mode') mode?: string, @Query('duree') duree?: string) {
    const modeTraitement: ModeTraitement = mode === undefined ? 'attente' : parseMode(mode);
    const dureeS = duree === undefined ? DUREE_PAR_DEFAUT_S : Number(duree);
    if (!Number.isInteger(dureeS) || dureeS < 1 || dureeS > DUREE_MAX_S) {
      throw new BadRequestException(`Paramètre « duree » invalide (entier de 1 à ${DUREE_MAX_S} secondes attendu).`);
    }
    return this.performanceService.lancerTraitementLong(modeTraitement, dureeS * 1000);
  }

  // ?templateId= (obligatoire) : modèle de document de type CONTRAT.
  // &exemplaires= (défaut : 1, max 20) : nombre d'impressions par contrat.
  // Imprime tous les contrats et enregistre les PDF sur le serveur (voir
  // PerformanceService.imprimerTousLesContrats) ; ne répond qu'à la fin.
  @Post('impression-contrats')
  async imprimerTousLesContrats(@Query('templateId', ParseIdPipe) templateId: bigint, @Query('exemplaires') exemplaires?: string) {
    const nombre = exemplaires === undefined ? 1 : Number(exemplaires);
    if (!Number.isInteger(nombre) || nombre < 1 || nombre > EXEMPLAIRES_MAX) {
      throw new BadRequestException(`Paramètre « exemplaires » invalide (entier de 1 à ${EXEMPLAIRES_MAX} attendu).`);
    }
    return this.performanceService.imprimerTousLesContrats(templateId, nombre);
  }

  // ?taille= (Ko de JSON, 1 à 51 200) &compression=1 (gzip) ou 0 (défaut).
  // Renvoie le JSON de test ; les mesures du serveur sont dans les en-têtes
  // X-Perf-* (durées en ms, tailles en octets), exposés au front par
  // Access-Control-Expose-Headers. `@Res()` sans `passthrough` : le corps,
  // déjà sérialisé (et compressé), est envoyé tel quel. Avec
  // Content-Encoding: gzip, le navigateur décompresse lui-même.
  @Get('transfert')
  async transfert(@Query('taille') taille: string | undefined, @Query('compression') compression: string | undefined, @Res() res: Response) {
    const tailleKo = Number(taille);
    if (!Number.isInteger(tailleKo) || tailleKo < 1 || tailleKo > TAILLE_MAX_KO) {
      throw new BadRequestException(`Paramètre « taille » invalide (entier de 1 à ${TAILLE_MAX_KO} Ko attendu).`);
    }
    const compresse = compression === '1';
    const transfert = await this.performanceService.preparerTransfert(tailleKo, compresse);
    const entetes: Record<string, string> = {
      'Content-Type': 'application/json; charset=utf-8',
      'Cache-Control': 'no-store',
      'X-Perf-Generation-Ms': transfert.generationMs.toFixed(2),
      'X-Perf-Json-Ms': transfert.jsonMs.toFixed(2),
      'X-Perf-Compression-Ms': transfert.compressionMs.toFixed(2),
      'X-Perf-Octets-Json': String(transfert.octetsJson),
      'X-Perf-Octets-Envoyes': String(transfert.corps.length),
    };
    if (compresse) entetes['Content-Encoding'] = 'gzip';
    entetes['Access-Control-Expose-Headers'] = Object.keys(entetes).filter((nom) => nom.startsWith('X-Perf-')).join(', ');
    res.set(entetes);
    res.end(transfert.corps);
  }

  // Réponse immédiate : sert à mesurer si le serveur répond encore pendant
  // un traitement long. `impression` : avancement de l'impression de tous
  // les contrats en cours, sinon null.
  @Get('ping')
  ping() {
    return { heure: new Date().toISOString(), impression: this.performanceService.getAvancementImpression() };
  }
}

function parseMode(mode: string): ModeTraitement {
  if (mode === 'attente' || mode === 'cpu') return mode;
  throw new BadRequestException('Paramètre « mode » invalide (« attente » ou « cpu » attendu).');
}
