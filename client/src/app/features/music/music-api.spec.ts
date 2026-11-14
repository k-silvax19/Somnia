import { HttpErrorResponse, HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { MusicReference } from '../home/home.models';
import { MusicApi, musicError, validReference } from './music-api';

describe('music details contract', () => {
  let api: MusicApi;
  let http: HttpTestingController;
  const item: MusicReference = { provider: 'spotify', type: 'album', externalId: 'abc' };
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(MusicApi);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('rejects metadata for a different provider instead of linking the wrong item', async () => {
    const result = firstValueFrom(api.details(item));
    const rejected = expect(result).rejects.toMatchObject({ status: 404 });
    http
      .expectOne(`${environment.apiUrl}/catalog/albums/abc`)
      .flush({ ...item, provider: 'simulated' });
    await rejected;
  });
  it('preserves zero, sends origin protection, and replaces both fields on edit', async () => {
    const created = firstValueFrom(api.create(item, { score: 0, review: null }));
    const post = http.expectOne(`${environment.apiUrl}/reviews`);
    expect(post.request.headers.get('X-Somnia-Client')).toBe('web');
    expect(post.request.body).toEqual({ ...item, score: 0, review: null });
    post.flush({ id: 'own' });
    await created;
    const updated = firstValueFrom(api.update('own', { score: null, review: 'Somente resenha.' }));
    const put = http.expectOne(`${environment.apiUrl}/reviews/own`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.headers.get('X-Somnia-Client')).toBe('web');
    expect(put.request.body).toEqual({ score: null, review: 'Somente resenha.' });
    put.flush({ id: 'own' });
    await updated;
  });
  it('does not retry a mutation after a network failure', async () => {
    const result = firstValueFrom(api.delete('own'));
    const rejected = expect(result).rejects.toBeInstanceOf(HttpErrorResponse);
    const request = http.expectOne(`${environment.apiUrl}/reviews/own`);
    expect(request.request.method).toBe('DELETE');
    expect(request.request.headers.get('X-Somnia-Client')).toBe('web');
    request.error(new ProgressEvent('error'));
    await rejected;
    http.expectNone(() => true);
  });
  it('rejects unsupported references and path traversal before rendering a detail view', () => {
    expect(validReference(item)).toBe(true);
    for (const externalId of ['..', 'a/b', '%2f', '', 'a'.repeat(129)])
      expect(validReference({ ...item, externalId })).toBe(false);
    expect(validReference({ ...item, provider: 'other' })).toBe(false);
  });
  it('distinguishes catalog failures without exposing backend diagnostics', () => {
    const problem = musicError(
      new HttpErrorResponse({
        status: 503,
        headers: new HttpHeaders({ 'Retry-After': '30' }),
        error: { detail: 'secret' },
      }),
      true,
    );
    expect(problem.retryAfter).toBe(30);
    expect(problem.message).toContain('catálogo musical');
    expect(problem.message).not.toContain('secret');
  });
});
