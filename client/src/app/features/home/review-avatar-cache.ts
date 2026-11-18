import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ReviewAvatarCache {
  private readonly successLifetime = 30 * 60_000;
  private readonly retryLifetime = 30_000;
  private readonly http = inject(HttpClient);
  private readonly requests = new Map<string, { until: number; result: Observable<Blob | null> }>();
  private readonly urls = new Map<
    string,
    { until: number; result: Observable<string | null>; url?: string }
  >();
  private readonly versions = signal<Record<string, number>>({});

  version(username: string): number {
    return this.versions()[username] ?? 0;
  }

  load(username: string): Observable<Blob | null> {
    if (!/^[a-zA-Z0-9._-]{3,30}$/.test(username)) return of(null);
    const cached = this.requests.get(username);
    if (cached && cached.until > Date.now()) return cached.result;
    const entry: { until: number; result: Observable<Blob | null> } = {
      until: Date.now() + this.retryLifetime,
      result: of(null),
    };
    entry.result = this.http
      .get(`${environment.apiUrl}/users/${encodeURIComponent(username)}/avatar`, {
        responseType: 'blob',
        timeout: 15_000,
      })
      .pipe(
        map((blob) => (blob.type === 'image/png' && blob.size > 0 ? blob : null)),
        tap((blob) => {
          entry.until = Date.now() + (blob ? this.successLifetime : this.retryLifetime);
        }),
        catchError(() => {
          entry.until = Date.now() + this.retryLifetime;
          return of(null);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    if (this.requests.size >= 100) this.requests.delete(this.requests.keys().next().value!);
    this.requests.set(username, entry);
    return entry.result;
  }

  loadUrl(username: string): Observable<string | null> {
    if (!/^[a-zA-Z0-9._-]{3,30}$/.test(username)) return of(null);
    const cached = this.urls.get(username);
    if (cached && cached.until > Date.now()) return cached.result;
    if (cached?.url) URL.revokeObjectURL(cached.url);
    if (this.urls.size >= 100) {
      const oldest = this.urls.keys().next().value;
      if (oldest) this.removeUrl(oldest);
    }

    const entry: { until: number; result: Observable<string | null>; url?: string } = {
      until: Date.now() + this.successLifetime,
      result: of(null),
    };
    entry.result = this.load(username).pipe(
      map((blob) => {
        if (!blob) {
          entry.until = Date.now() + this.retryLifetime;
          return null;
        }
        entry.url = URL.createObjectURL(blob);
        entry.until = Date.now() + this.successLifetime;
        return entry.url;
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    this.urls.set(username, entry);
    return entry.result;
  }

  invalidate(username: string): void {
    this.requests.delete(username);
    this.removeUrl(username);
    this.versions.update((versions) => ({
      ...versions,
      [username]: (versions[username] ?? 0) + 1,
    }));
  }

  private removeUrl(username: string): void {
    const entry = this.urls.get(username);
    if (entry?.url) URL.revokeObjectURL(entry.url);
    this.urls.delete(username);
  }
}
