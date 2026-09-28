// Format d'erreur unique pour toute l'API : { statusCode, message }. Les
// erreurs volontaires (HttpException : 400, 401, 404, 409, 429...) gardent
// leur message ; les erreurs Prisma prévisibles (enregistrement introuvable,
// doublon, référence invalide...) sont traduites en statut et message
// clairs ; toute autre erreur (bug...) est journalisée côté serveur et
// renvoyée au client sans le moindre détail interne.
import { ArgumentsHost, Catch, ExceptionFilter, HttpException, HttpStatus, Logger } from '@nestjs/common';
import type { Response } from 'express';
import { Prisma } from '../generated/prisma/client.js';

// Codes : https://www.prisma.io/docs/orm/reference/error-reference
const prismaErrors: Record<string, { status: HttpStatus; message: string }> = {
  P2000: { status: HttpStatus.BAD_REQUEST, message: 'Une valeur est trop longue pour son champ.' },
  P2002: { status: HttpStatus.CONFLICT, message: 'Un enregistrement avec cette valeur existe déjà.' },
  P2003: {
    status: HttpStatus.CONFLICT,
    message: 'Opération impossible : un enregistrement lié est introuvable ou encore utilisé ailleurs.',
  },
  P2014: { status: HttpStatus.CONFLICT, message: 'Opération impossible : cet enregistrement est lié à d’autres données.' },
  P2025: { status: HttpStatus.NOT_FOUND, message: 'Enregistrement introuvable.' },
};

// Base injoignable ou qui refuse la connexion (identifiants, base absente,
// serveur arrêté, trop de connexions...) : ce n'est pas un bug de l'API mais
// une indisponibilité, d'où un 503 plutôt qu'un 500.
const databaseUnavailableCodes = new Set(['P1000', 'P1001', 'P1003', 'P1008', 'P1010', 'P1011', 'P1017', 'P2037']);
// L'adaptateur MariaDB remonte l'échec d'obtention d'une connexion du pool
// (erreur 45028 du pilote, ex. socket Cloud SQL absent ou mot de passe refusé)
// sous le code générique P2039 : seul le message permet de le reconnaître.
const POOL_TIMEOUT = /Code: `45028`/;
const DATABASE_UNAVAILABLE_MESSAGE = 'Base de données indisponible, réessayez dans quelques instants.';

function isDatabaseUnavailable(exception: unknown): exception is Error {
  if (exception instanceof Prisma.PrismaClientInitializationError) return true;
  if (!(exception instanceof Prisma.PrismaClientKnownRequestError)) return false;
  return databaseUnavailableCodes.has(exception.code) || (exception.code === 'P2039' && POOL_TIMEOUT.test(exception.message));
}

@Catch()
export class AllExceptionsFilter implements ExceptionFilter {
  private readonly logger = new Logger('Exceptions');

  catch(exception: unknown, host: ArgumentsHost) {
    const response = host.switchToHttp().getResponse<Response>();

    if (exception instanceof HttpException) {
      const status = exception.getStatus();
      const body = exception.getResponse();
      const raw = typeof body === 'string' ? body : (body as { message?: string | string[] }).message;
      const message = Array.isArray(raw) ? raw.join(', ') : (raw ?? exception.message);
      response.status(status).json({ statusCode: status, message });
      return;
    }

    if (isDatabaseUnavailable(exception)) {
      // Journalisé en erreur avec le détail : c'est la seule trace de la
      // cause réelle (mot de passe, socket, base...).
      this.logger.error(`Base de données indisponible : ${exception.message}`);
      const status = HttpStatus.SERVICE_UNAVAILABLE;
      response.status(status).json({ statusCode: status, message: DATABASE_UNAVAILABLE_MESSAGE });
      return;
    }

    if (exception instanceof Prisma.PrismaClientKnownRequestError) {
      const known = prismaErrors[exception.code];
      if (known) {
        // Journalisé en avertissement : utile pour repérer un appel du front
        // qui vise un id disparu ou une contrainte non anticipée.
        this.logger.warn(`${exception.code} ${exception.message.split('\n').pop()}`);
        response.status(known.status).json({ statusCode: known.status, message: known.message });
        return;
      }
    }

    this.logger.error(exception instanceof Error ? (exception.stack ?? exception.message) : String(exception));
    response.status(500).json({ statusCode: 500, message: 'Erreur interne du serveur' });
  }
}
