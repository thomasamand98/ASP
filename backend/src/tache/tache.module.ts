import { Module } from '@nestjs/common';
import { TacheController } from './tache.controller.js';
import { TacheService } from './tache.service.js';

@Module({
  controllers: [TacheController],
  providers: [TacheService],
})
export class TacheModule {}
