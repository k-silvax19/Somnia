import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../environments/environment';
import { apiError } from '../../core/http/api-error';
import { MusicItem, MusicReference, Page, Review, musicKey } from '../home/home.models';

export interface MusicDetails extends MusicItem {
  tracks?: (MusicItem & { trackNumber: number })[];
  album?: MusicItem;
  trackNumber?: number;
}
export interface ItemReviews {
  reviews: Page<Review>;
  summary: { average: number | null; count: number };
  myReview: Review | null;
}
export interface ReviewContent {
  score: number | null;
  review: string | null;
}

export function validReference(item: MusicReference): boolean {
  return (
    ['spotify', 'simulated'].includes(item.provider) &&
    ['album', 'track'].includes(item.type) &&
    /^[a-zA-Z0-9_-]{1,128}$/.test(item.externalId)
  );
}

export const musicRoute = (item: MusicReference) => [
  '/musica',
  item.provider,
  item.type,
  item.externalId,
];

// Catalog failures do not imply that saved reviews are unavailable.
export function musicError(error: unknown, catalog = false) {
  const result = apiError(error);
  if (error instanceof HttpErrorResponse) {
    if (catalog && error.status === 404)
      result.message =
        'Este item não está disponível no catálogo atual. Suas avaliações continuam acessíveis.';
    if (catalog && [502, 503].includes(error.status))
      result.message = 'Não foi possível carregar as informações desta música agora.';
    if (error.status === 503) {
      const seconds = Number(error.headers.get('Retry-After'));
      result.retryAfter = Number.isFinite(seconds) && seconds > 0 ? seconds : 60;
    }
  }
  return result;
}

@Injectable({ providedIn: 'root' })
export class MusicApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;
  private readonly write = { headers: { 'X-Somnia-Client': 'web' }, timeout: 35_000 };

  details(item: MusicReference) {
    return this.http
      .get<MusicDetails>(
        `${this.base}/catalog/${item.type === 'album' ? 'albums' : 'tracks'}/${encodeURIComponent(item.externalId)}`,
        { timeout: 35_000 },
      )
      .pipe(
        map((details) => {
          if (musicKey(details) !== musicKey(item)) throw new HttpErrorResponse({ status: 404 });
          return details;
        }),
      );
  }
  reviews(item: MusicReference, offset = 0) {
    return this.http.get<ItemReviews>(
      `${this.base}/catalog/${encodeURIComponent(item.provider)}/${item.type}/${encodeURIComponent(item.externalId)}/reviews`,
      { params: { limit: 10, offset }, timeout: 15_000 },
    );
  }
  create(item: MusicReference, content: ReviewContent) {
    return this.http.post<Review>(`${this.base}/reviews`, { ...item, ...content }, this.write);
  }
  update(id: string, content: ReviewContent) {
    return this.http.put<Review>(
      `${this.base}/reviews/${encodeURIComponent(id)}`,
      content,
      this.write,
    );
  }
  delete(id: string) {
    return this.http.delete<void>(`${this.base}/reviews/${encodeURIComponent(id)}`, this.write);
  }
}
