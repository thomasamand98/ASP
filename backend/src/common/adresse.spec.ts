import { Prisma } from '../generated/prisma/client.js';
import { pickAdresse, saveAdresse } from './adresse.js';

function fakeTx(createdId = 42n) {
  const adresse = {
    create: vi.fn().mockResolvedValue({ IDADRESSES: createdId }),
    update: vi.fn().mockResolvedValue({}),
  };
  return { tx: { adresse } as unknown as Prisma.TransactionClient, adresse };
}

describe('pickAdresse', () => {
  it('ne garde que les champs envoyés, texte vide → NULL', () => {
    expect(pickAdresse({ Adresse1: 'Rue 1', CP: '  ', Localite: undefined })).toEqual({ Adresse1: 'Rue 1', CP: null });
  });
});

describe('saveAdresse', () => {
  it('création de fiche : crée l’adresse et renvoie son id', async () => {
    const { tx, adresse } = fakeTx(42n);
    await expect(saveAdresse(tx, null, { Adresse1: 'Rue 1' })).resolves.toBe(42n);
    expect(adresse.create).toHaveBeenCalledWith({ data: expect.objectContaining({ Adresse1: 'Rue 1' }) });
    expect(adresse.update).not.toHaveBeenCalled();
  });

  it('modification : met à jour l’adresse pointée par l’IDADRESSES de la fiche', async () => {
    const { tx, adresse } = fakeTx();
    await expect(saveAdresse(tx, 7n, { Localite: 'Liège' })).resolves.toBe(7n);
    expect(adresse.update).toHaveBeenCalledWith({ where: { IDADRESSES: 7n }, data: expect.objectContaining({ Localite: 'Liège' }) });
    expect(adresse.create).not.toHaveBeenCalled();
  });

  it('modification d’une fiche sans adresse (NULL ou 0) : crée puis renvoie le nouvel id', async () => {
    const { tx, adresse } = fakeTx(9n);
    await expect(saveAdresse(tx, 0n, { CP: '4000' })).resolves.toBe(9n);
    expect(adresse.create).toHaveBeenCalledOnce();
  });

  it('adresse entièrement vide : rien n’est créé', async () => {
    const { tx, adresse } = fakeTx();
    await expect(saveAdresse(tx, null, { Adresse1: null, CP: null })).resolves.toBeNull();
    expect(adresse.create).not.toHaveBeenCalled();
  });

  it('aucun champ d’adresse envoyé : IDADRESSES inchangé', async () => {
    const { tx, adresse } = fakeTx();
    await expect(saveAdresse(tx, 7n, {})).resolves.toBe(7n);
    expect(adresse.update).not.toHaveBeenCalled();
  });
});
