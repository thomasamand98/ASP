// ===== CONTROLLER =====
// Reçoit les requêtes HTTP de la View et délègue au Model (StatistiqueService).
import { BadRequestException, Controller, Get, Query } from '@nestjs/common';
import { StatistiqueService } from './statistique.service.js';
import { parsePeriod } from '../common/params.js';

@Controller()
export class StatistiqueController {
  constructor(private readonly statistiqueService: StatistiqueService) {}

  // ?from=AAAA-MM-JJ&to=AAAA-MM-JJ (obligatoires, bornes incluses).
  @Get('statistiques/chiffre-affaires')
  async getChiffreAffaires(@Query('from') from?: string, @Query('to') to?: string) {
    const period = parsePeriod(from, to);
    if (!period?.gte || !period.lte) throw new BadRequestException('Les paramètres « from » et « to » sont obligatoires.');
    return this.statistiqueService.getChiffreAffaires(period.gte, period.lte);
  }
}
