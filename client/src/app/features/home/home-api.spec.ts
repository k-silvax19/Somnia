import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { HomeApi } from './home-api';
import { MusicItem, MusicReference, coverUrl, spotifyUrl } from './home.models';

describe('home catalog integration', () => {
  let api: HomeApi;
  let http: HttpTestingController;
  const item: MusicReference = { provider: 'spotify', type: 'album', externalId: 'abc' };
  const metadata: MusicItem = {
    ...item,
    title: 'Álbum',
    artists: ['Artista'],
    coverUrl: null,
    spotifyUrl: 'https://open.spotify.com/album/abc',
  };
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [HomeApi, provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(HomeApi);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('deduplicates concurrent metadata requests and caches a successful response', async () => {
    const first = firstValueFrom(api.metadata(item));
    const second = firstValueFrom(api.metadata(item));
    http.expectOne(`${environment.apiUrl}/catalog/albums/abc`).flush(metadata);
    expect(await first).toEqual(await second);
    expect(await firstValueFrom(api.metadata(item))).toEqual(metadata);
    http.expectNone(`${environment.apiUrl}/catalog/albums/abc`);
  });

  it('does not use metadata from a different provider', async () => {
    const result = firstValueFrom(api.metadata(item));
    http
      .expectOne(`${environment.apiUrl}/catalog/albums/abc`)
      .flush({ ...metadata, provider: 'simulated' });
    expect(await result).toBeNull();
  });

  it('does not request unsupported providers', async () => {
    expect(await firstValueFrom(api.metadata({ ...item, provider: 'other' }))).toBeNull();
    http.expectNone(() => true);
  });

  it('pauses catalog hydration after a rate limit instead of flooding the provider', async () => {
    const result = firstValueFrom(api.metadata(item));
    http
      .expectOne(`${environment.apiUrl}/catalog/albums/abc`)
      .flush({}, { status: 429, statusText: 'Rate limit', headers: { 'Retry-After': '60' } });
    expect(await result).toBeNull();
    expect(await firstValueFrom(api.metadata({ ...item, externalId: 'another' }))).toBeNull();
    http.expectNone(() => true);
  });

  it('rejects unsafe covers and spoofed Spotify links', () => {
    expect(coverUrl('javascript:alert(1)')).toBeNull();
    expect(coverUrl('http://images.test/cover.png')).toBeNull();
    expect(
      spotifyUrl({ ...metadata, spotifyUrl: 'https://open.spotify.com.evil.test/album/abc' }),
    ).toBeNull();
    expect(
      spotifyUrl({ ...metadata, spotifyUrl: 'https://open.spotify.com/album/other' }),
    ).toBeNull();
    expect(spotifyUrl(metadata)).toBe(metadata.spotifyUrl);
  });
});
