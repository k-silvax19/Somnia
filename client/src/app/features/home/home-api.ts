import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, defer, map, of, shareReplay, tap, throwError } from 'rxjs';
import { Account } from '../../core/auth/auth.models';
import { apiError } from '../../core/http/api-error';
import { environment } from '../../environments/environment';
import { Connections, MusicItem, MusicReference, Page, Review, musicKey } from './home.models';

// Provided by the home page: metadata cache is released when leaving the account/session.
@Injectable()
export class HomeApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;
  private readonly metadataCache = new Map<
    string,
    { until: number; result: Observable<MusicItem | null> }
  >();
  private catalogBlockedUntil = 0;

  account() {
    return this.http.get<Account>(`${this.base}/me`, { timeout: 15_000 });
  }
  connections(username: string) {
    return this.http.get<Connections>(
      `${this.base}/users/${encodeURIComponent(username)}/connections`,
      { timeout: 15_000 },
    );
  }
  reviewCount(username: string) {
    return this.http
      .get<Page<Review>>(`${this.base}/users/${encodeURIComponent(username)}/reviews`, {
        params: { limit: 1, offset: 0 },
        timeout: 15_000,
      })
      .pipe(map((page) => page.total));
  }
  avatar(username: string) {
    return this.http.get(`${this.base}/users/${encodeURIComponent(username)}/avatar`, {
      responseType: 'blob',
      timeout: 15_000,
    });
  }
  reviews(tab: 'following' | 'discover', offset: number) {
    return this.http.get<Page<Review>>(
      `${this.base}/${tab === 'following' ? 'feed' : 'discover'}`,
      {
        params: { limit: 10, offset },
        timeout: 15_000,
      },
    );
  }
  search(query: string, type: 'album' | 'track', offset: number) {
    return this.http.get<Page<MusicItem>>(`${this.base}/catalog/search`, {
      params: { q: query, type, limit: 10, offset },
      timeout: 35_000,
    });
  }
  metadata(item: MusicReference): Observable<MusicItem | null> {
    return defer(() => {
      if (
        !['spotify', 'simulated'].includes(item.provider) ||
        Date.now() < this.catalogBlockedUntil
      )
        return of(null);
      const key = musicKey(item);
      const cached = this.metadataCache.get(key);
      if (cached && cached.until > Date.now()) return cached.result;
      if (this.metadataCache.size >= 100)
        this.metadataCache.delete(this.metadataCache.keys().next().value!);
      const result = this.http
        .get<MusicItem>(
          `${this.base}/catalog/${item.type === 'album' ? 'albums' : 'tracks'}/${encodeURIComponent(item.externalId)}`,
          { timeout: 35_000 },
        )
        .pipe(
          map((metadata): MusicItem | null => {
            if (musicKey(metadata) !== key) return null;
            // Album details may include hundreds of tracks; keep only the card's fields.
            const { provider, type, externalId, title, artists, coverUrl, spotifyUrl } = metadata;
            return { provider, type, externalId, title, artists, coverUrl, spotifyUrl };
          }),
          tap((metadata) => {
            if (!metadata) this.metadataCache.delete(key);
            else this.metadataCache.set(key, { until: Date.now() + 120_000, result: of(metadata) });
          }),
          catchError((error: unknown) => {
            this.metadataCache.delete(key);
            if (
              error instanceof HttpErrorResponse &&
              (error.status === 429 || error.status === 503)
            ) {
              this.catalogBlockedUntil = Date.now() + (apiError(error).retryAfter || 60) * 1000;
            }
            return throwError(() => error);
          }),
          shareReplay({ bufferSize: 1, refCount: true }),
        );
      // Only successful metadata is cached; an unavailable catalog never hides a review.
      this.metadataCache.set(key, { until: Date.now() + 120_000, result });
      return result;
    }).pipe(catchError(() => of(null)));
  }
}
