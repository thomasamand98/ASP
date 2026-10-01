import { Logger, ServiceUnavailableException } from '@nestjs/common';
import { PdfRendererService } from './pdf-renderer.service.js';

const launch = vi.hoisted(() => vi.fn());
vi.mock('puppeteer', () => ({ default: { launch } }));

const input = { headerHtml: '', bodyHtml: '<p>Contrat</p>', footerHtml: '' };

// Faux Chromium : `pdf` décide du résultat de chaque génération, `connected`
// indique s'il tourne encore au moment de l'échec.
function fakeBrowser(pdf: () => Promise<Uint8Array>, connected = true) {
  const page = {
    setJavaScriptEnabled: vi.fn(),
    setRequestInterception: vi.fn(),
    on: vi.fn(),
    setContent: vi.fn(),
    pdf: vi.fn(pdf),
    close: vi.fn(() => Promise.resolve()),
  };
  return { connected, once: vi.fn(), newPage: vi.fn(() => Promise.resolve(page)), close: vi.fn(() => Promise.resolve()) };
}

const ok = () => Promise.resolve(new Uint8Array([1, 2, 3]));
const fail = () => Promise.reject(new Error('Protocol error: Target closed'));

describe('PdfRendererService', () => {
  beforeAll(() => {
    vi.spyOn(Logger.prototype, 'warn').mockImplementation(() => undefined);
    vi.spyOn(Logger.prototype, 'error').mockImplementation(() => undefined);
  });
  beforeEach(() => launch.mockReset());

  it('renvoie le PDF et réutilise la même instance Chromium', async () => {
    launch.mockResolvedValue(fakeBrowser(ok) as never);
    const service = new PdfRendererService();
    expect([...(await service.renderPdf(input))]).toEqual([1, 2, 3]);
    await service.renderPdf(input);
    expect(launch).toHaveBeenCalledTimes(1);
  });

  it('renvoie 503 sans détail interne quand Chromium ne démarre pas, puis relance à la demande suivante', async () => {
    launch.mockRejectedValueOnce(new Error('Failed to launch the browser process')).mockResolvedValue(fakeBrowser(ok) as never);
    const service = new PdfRendererService();
    const error = await service.renderPdf(input).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ServiceUnavailableException);
    expect((error as Error).message).not.toContain('launch');
    expect([...(await service.renderPdf(input))]).toEqual([1, 2, 3]);
    expect(launch).toHaveBeenCalledTimes(2);
  });

  it('relance Chromium et retente une fois quand l’instance s’est arrêtée', async () => {
    launch.mockResolvedValueOnce(fakeBrowser(fail, false) as never).mockResolvedValue(fakeBrowser(ok) as never);
    const service = new PdfRendererService();
    expect([...(await service.renderPdf(input))]).toEqual([1, 2, 3]);
    expect(launch).toHaveBeenCalledTimes(2);
  });

  it('renvoie 503 sans retenter quand le rendu échoue sur une instance vivante', async () => {
    launch.mockResolvedValue(fakeBrowser(fail) as never);
    const service = new PdfRendererService();
    await expect(service.renderPdf(input)).rejects.toBeInstanceOf(ServiceUnavailableException);
    expect(launch).toHaveBeenCalledTimes(1);
  });
});
