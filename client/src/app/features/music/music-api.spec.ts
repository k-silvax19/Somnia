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
    for (const key of Object.keys(localStorage))
      if (key.startsWith('somnia:spotify-catalog:')) localStorage.removeItem(key);
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
  it('restores a Spotify album and its tracks from temporary browser storage', async () => {
    const spotifyId = 'AAAAAAAAAAAAAAAAAAAAAA';
    const reference: MusicReference = { provider: 'spotify', type: 'album', externalId: spotifyId };
    const album = {
      ...reference,
      title: 'Album',
      artists: ['Artist'],
      coverUrl: null,
      spotifyUrl: `https://open.spotify.com/album/${spotifyId}`,
      tracks: [
        {
          provider: 'spotify',
          type: 'track',
          externalId: 'BBBBBBBBBBBBBBBBBBBBBB',
          title: 'Track',
          artists: ['Artist'],
          coverUrl: null,
          spotifyUrl: 'https://open.spotify.com/track/BBBBBBBBBBBBBBBBBBBBBB',
          trackNumber: 1,
        },
      ],
    };
    const first = firstValueFrom(api.details(reference));
    http.expectOne(`${environment.apiUrl}/catalog/albums/${spotifyId}`).flush(album);
    expect(await first).toEqual(album);
    expect(await firstValueFrom(api.details(reference))).toEqual(album);
    http.expectNone(() => true);

    const key = Object.keys(localStorage).find((name) =>
      name.includes(`details:spotify:album:${spotifyId}`),
    );
    expect(key).toBeDefined();
    const stored = JSON.parse(localStorage.getItem(key!)!) as { expiresAt: number };
    localStorage.setItem(key!, JSON.stringify({ ...stored, expiresAt: Date.now() - 1 }));
    const refreshed = firstValueFrom(api.details(reference));
    http.expectOne(`${environment.apiUrl}/catalog/albums/${spotifyId}`).flush(album);
    expect(await refreshed).toEqual(album);
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
    expect(problem.message).toContain('informações desta música');
    expect(problem.message).not.toContain('secret');
  });
});
