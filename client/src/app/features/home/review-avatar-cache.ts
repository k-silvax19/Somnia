import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ReviewAvatarCache {
  private readonly http = inject(HttpClient);
  private readonly requests = new Map<string, { until: number; result: Observable<Blob | null> }>();
  private readonly versions = signal<Record<string, number>>({});

  version(username: string): number {
    return this.versions()[username] ?? 0;
  }

  load(username: string): Observable<Blob | null> {
    if (!/^[a-zA-Z0-9._-]{3,30}$/.test(username)) return of(null);
    const cached = this.requests.get(username);
    if (cached && cached.until > Date.now()) return cached.result;
    const request = this.http
      .get(`${environment.apiUrl}/users/${encodeURIComponent(username)}/avatar`, {
        responseType: 'blob',
        timeout: 15_000,
      })
      .pipe(
        map((blob) => (blob.type === 'image/png' && blob.size > 0 ? blob : null)),
        catchError(() => of(null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    if (this.requests.size >= 100) this.requests.delete(this.requests.keys().next().value!);
    this.requests.set(username, { until: Date.now() + 5 * 60_000, result: request });
    return request;
  }

  invalidate(username: string): void {
    this.requests.delete(username);
    this.versions.update((versions) => ({
      ...versions,
      [username]: (versions[username] ?? 0) + 1,
    }));
  }
}
