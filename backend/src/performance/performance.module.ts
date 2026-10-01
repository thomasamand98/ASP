import { Module } from '@nestjs/common';
import { ContratModule } from '../contrat/contrat.module.js';
import { DocumentMergeModule } from '../document-merge/document-merge.module.js';
import { DocumentTemplateModule } from '../document-template/document-template.module.js';
import { PerformanceController } from './performance.controller.js';
import { PerformanceService } from './performance.service.js';

@Module({
  // Impression de tous les contrats : mêmes services que le « Voir le PDF »
  // de la fiche contrat (voir contrat.controller.ts).
  imports: [ContratModule, DocumentTemplateModule, DocumentMergeModule],
  controllers: [PerformanceController],
  providers: [PerformanceService],
})
export class PerformanceModule {}
