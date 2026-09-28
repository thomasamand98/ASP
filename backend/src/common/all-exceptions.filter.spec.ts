import { ArgumentsHost, Logger, NotFoundException } from '@nestjs/common';
import { Prisma } from '../generated/prisma/client.js';
import { AllExceptionsFilter } from './all-exceptions.filter.js';

function run(exception: unknown) {
  const json = vi.fn((_body: { statusCode: number; message: string }) => undefined);
  const status = vi.fn((_code: number) => ({ json }));
  const host = { switchToHttp: () => ({ getResponse: () => ({ status }) }) } as unknown as ArgumentsHost;
  new AllExceptionsFilter().catch(exception, host);
  return { status: status.mock.calls[0]?.[0], body: json.mock.calls[0]?.[0] };
}

const prismaError = (code: string) =>
  new Prisma.PrismaClientKnownRequestError('détail interne', { code, clientVersion: 'test' });

describe('AllExceptionsFilter', () => {
  beforeAll(() => {
    vi.spyOn(Logger.prototype, 'warn').mockImplementation(() => undefined);
    vi.spyOn(Logger.prototype, 'error').mockImplementation(() => undefined);
  });

  it('garde le statut et le message des HttpException', () => {
    expect(run(new NotFoundException('Société introuvable'))).toEqual({
      status: 404,
      body: { statusCode: 404, message: 'Société introuvable' },
    });
  });

  it.each([
    ['P2025', 404],
    ['P2002', 409],
    ['P2003', 409],
    ['P2000', 400],
  ])('traduit l’erreur Prisma %s en %i sans exposer de détail', (code, expected) => {
    const { status, body } = run(prismaError(code));
    expect(status).toBe(expected);
    expect(body.message).not.toContain('détail interne');
  });

  it.each(['P1000', 'P1001', 'P1003', 'P2037'])('renvoie 503 quand la base est indisponible (%s)', (code) => {
    expect(run(prismaError(code))).toEqual({
      status: 503,
      body: { statusCode: 503, message: 'Base de données indisponible, réessayez dans quelques instants.' },
    });
  });

  it('renvoie 503 quand le pool ne parvient pas à ouvrir de connexion', () => {
    const poolTimeout = new Prisma.PrismaClientKnownRequestError(
      'Database error. Code: `45028`. Message: `pool failed to retrieve a connection from pool`',
      { code: 'P2039', clientVersion: 'test' },
    );
    expect(run(poolTimeout).status).toBe(503);
    expect(run(new Prisma.PrismaClientInitializationError('connexion impossible', 'test')).status).toBe(503);
  });

  it('renvoie 500 générique pour une erreur Prisma non répertoriée ou un bug', () => {
    expect(run(prismaError('P2021')).status).toBe(500);
    expect(run(prismaError('P2039')).status).toBe(500);
    expect(run(new TypeError('boom'))).toEqual({ status: 500, body: { statusCode: 500, message: 'Erreur interne du serveur' } });
  });
});
