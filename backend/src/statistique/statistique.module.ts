import { Module } from '@nestjs/common';
import { StatistiqueController } from './statistique.controller.js';
import { StatistiqueService } from './statistique.service.js';

@Module({
  controllers: [StatistiqueController],
  providers: [StatistiqueService],
})
export class StatistiqueModule {}
