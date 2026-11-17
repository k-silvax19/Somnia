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
  const item: MusicReference = { provider: 'simulated', type: 'album', externalId: 'abc' };
  const spotifyItem: MusicReference = {
    provider: 'spotify',
    type: 'album',
    externalId: 'AAAAAAAAAAAAAAAAAAAAAA',
  };
  const metadata: MusicItem = {
    ...item,
    title: 'Álbum',
    artists: ['Artista'],
    coverUrl: null,
    spotifyUrl: 'https://open.spotify.com/album/abc',
  };
  beforeEach(() => {
    for (const key of Object.keys(localStorage))
      if (key.startsWith('somnia:spotify-catalog:')) localStorage.removeItem(key);
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
      .flush({ ...metadata, provider: 'spotify' });
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

  it('uses the Spotify preview for card titles and covers when album details are quota limited', async () => {
    const result = firstValueFrom(api.metadata(spotifyItem));
    http
      .expectOne(
        (request) =>
          request.url === 'https://open.spotify.com/oembed' &&
          request.params.get('url') === 'https://open.spotify.com/album/AAAAAAAAAAAAAAAAAAAAAA',
      )
      .flush({
        provider_name: 'Spotify',
        title: 'Álbum do Spotify',
        thumbnail_url: 'https://i.scdn.co/image/cover',
      });

    expect(await result).toEqual({
      ...spotifyItem,
      title: 'Álbum do Spotify',
      artists: [],
      coverUrl: 'https://i.scdn.co/image/cover',
      spotifyUrl: 'https://open.spotify.com/album/AAAAAAAAAAAAAAAAAAAAAA',
    });
    const afterReload = TestBed.runInInjectionContext(() => new HomeApi());
    expect(await firstValueFrom(afterReload.metadata(spotifyItem))).toEqual(await result);
    http.expectNone(() => true);
  });

  it('reuses a recent Spotify search instead of requesting the same page again', async () => {
    const first = firstValueFrom(api.search('Slipknot', 'album', 0));
    const page = {
      items: [
        {
          ...spotifyItem,
          title: 'Slipknot',
          artists: ['Slipknot'],
          coverUrl: null,
          spotifyUrl: `https://open.spotify.com/album/${spotifyItem.externalId}`,
        },
      ],
      limit: 10,
      offset: 0,
      total: 1,
      hasMore: false,
    };
    http.expectOne((request) => request.url === `${environment.apiUrl}/catalog/search`).flush(page);
    expect(await first).toEqual(page);
    expect(await firstValueFrom(api.search('Slipknot', 'album', 0))).toEqual(page);
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
