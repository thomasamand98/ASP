import { Module } from '@nestjs/common';
import { LienDocumentController } from './lien-document.controller.js';
import { LienDocumentService } from './lien-document.service.js';

@Module({
  controllers: [LienDocumentController],
  providers: [LienDocumentService],
})
export class LienDocumentModule {}
