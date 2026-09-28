// ===== CONTROLLER =====
// Reçoit les requêtes HTTP de la View et délègue au Model (TacheService).
import { BadRequestException, Body, Controller, Get, Param, Patch, Query } from '@nestjs/common';
import { TacheService } from './tache.service.js';
import { ReprogrammerTacheDto, TraiterTacheDto } from './tache.dto.js';
import { ParseIdPipe } from '../common/params.js';
import { ZodBodyPipe } from '../common/validation.js';

@Controller()
export class TacheController {
  constructor(private readonly tacheService: TacheService) {}

  // ?jusquAu=AAAA-MM-JJ (obligatoire) : date du jour côté navigateur.
  @Get('taches/a-faire')
  async getTachesAFaire(@Query('jusquAu') jusquAu?: string) {
    if (!jusquAu || !/^\d{4}-\d{2}-\d{2}$/.test(jusquAu) || Number.isNaN(Date.parse(jusquAu))) {
      throw new BadRequestException('Paramètre « jusquAu » invalide (AAAA-MM-JJ attendu).');
    }
    return this.tacheService.getTachesAFaire(jusquAu);
  }

  @Patch('taches/:id/traiter')
  async traiterTache(@Param('id', ParseIdPipe) id: bigint, @Body(new ZodBodyPipe(TraiterTacheDto)) dto: TraiterTacheDto) {
    return this.tacheService.traiterTache(id, dto);
  }

  @Patch('taches/:id/reprogrammer')
  async reprogrammerTache(@Param('id', ParseIdPipe) id: bigint, @Body(new ZodBodyPipe(ReprogrammerTacheDto)) dto: ReprogrammerTacheDto) {
    return this.tacheService.reprogrammerTache(id, dto);
  }
}
