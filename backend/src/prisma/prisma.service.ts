// ===== MODEL (accès aux données) =====
// Fondation du Model : encapsule la connexion à MySQL via Prisma.
// PrismaClient est généré automatiquement à partir de
// backend/prisma/schema.prisma (commande `npx prisma generate`,
// sortie dans backend/src/generated/prisma).
// Cette classe est injectée dans les Services (ex: hello.service.ts) qui
// ont besoin de lire/écrire des données — jamais directement dans un
// Controller.
import { Injectable, Logger, OnModuleInit, OnModuleDestroy } from '@nestjs/common';
import { PrismaMariaDb } from '@prisma/adapter-mariadb';
import mariadb from 'mariadb';
import { PrismaClient } from '../generated/prisma/client.js';

// Ouvre (puis referme) une connexion directe pour journaliser la cause
// exacte d'un échec : via Prisma, elle est masquée derrière un message
// générique (« pool failed to retrieve a connection from pool »). Ne bloque
// jamais le démarrage.
async function logDatabaseConnectionProblem(url: string | undefined): Promise<void> {
  const logger = new Logger('Database');
  if (!url) {
    logger.error('DATABASE_URL absente : aucune requête ne pourra aboutir.');
    return;
  }
  try {
    const connection = await mariadb.createConnection(url.replace(/^mysql:/, 'mariadb:'));
    await connection.end();
    logger.log('Connexion à la base de données réussie.');
  } catch (error) {
    logger.error(`Connexion à la base de données impossible : ${error instanceof Error ? error.message : String(error)}`);
  }
}

@Injectable()
export class PrismaService
  extends PrismaClient
  implements OnModuleInit, OnModuleDestroy
{
  constructor() {
    // L'adaptateur lit l'URL de connexion (utilisateur/mot de passe/DB)
    // dans backend/.env → variable DATABASE_URL.
    super({ adapter: new PrismaMariaDb(process.env.DATABASE_URL as string) });
  }

  // Ouvre la connexion MySQL quand NestJS démarre le module.
  async onModuleInit() {
    await logDatabaseConnectionProblem(process.env.DATABASE_URL);
    await this.$connect();
  }

  // Ferme proprement la connexion quand l'application s'arrête.
  async onModuleDestroy() {
    await this.$disconnect();
  }
}
