import { DOCUMENT } from '@angular/common';
import { HttpBackend, HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Account, Credentials, Registration, Session } from './auth.models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = environment.apiUrl;
  // Auth calls bypass the interceptor to avoid recursive refresh requests.
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly document = inject(DOCUMENT);
  private readonly current = signal<Session | null>(null);
  readonly session = this.current.asReadonly();
  readonly notice = signal('');
  private refreshFlight: Promise<Session | null> | null = null;
  private refreshBlocked = false;
  private revision = 0;
  private readonly channel =
    typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel('somnia-auth');

  constructor() {
    if (this.channel) {
      this.channel.onmessage = (event: MessageEvent<unknown>) => {
        if (event.data === 'logout') this.invalidate('Sua sessão foi encerrada em outra aba.');
        if (event.data === 'login') {
          // Another login replaces shared cookies; don't keep a different account's JWT.
          this.invalidate('A conta conectada mudou em outra aba. Entre novamente para continuar.');
        }
      };
      inject(DestroyRef).onDestroy(() => this.channel?.close());
    }
  }

  register(data: Registration): Promise<Account> {
    return firstValueFrom(
      this.http.post<Account>(`${this.apiUrl}/auth/register`, data, this.options()),
    );
  }

  async login(data: Credentials): Promise<void> {
    await this.withSessionLock(async () => {
      const session = await firstValueFrom(
        this.http.post<Session>(`${this.apiUrl}/auth/login`, data, this.options()),
      );
      this.revision++;
      this.current.set(session);
      this.refreshBlocked = false;
      this.notice.set('');
      this.channel?.postMessage('login');
    });
  }

  ensureSession(): Promise<Session | null> {
    const session = this.current();
    if (session && Date.parse(session.dataExpiracaoEmUtc) > Date.now() + 30_000) {
      return Promise.resolve(session);
    }
    if (this.refreshFlight) return this.refreshFlight;
    if (this.refreshBlocked || !this.csrfCookie()) {
      if (session) this.invalidate('Sua sessão expirou. Entre novamente.');
      return Promise.resolve(null);
    }

    const revision = this.revision;
    this.refreshFlight = this.withSessionLock(async () => {
      if (revision !== this.revision) return this.current();
      // Read AFTER acquiring the cross-tab lock: refresh rotates both cookies.
      const csrf = this.csrfCookie();
      if (!csrf) {
        this.invalidate('Sua sessão expirou. Entre novamente.');
        return null;
      }
      try {
        const renewed = await firstValueFrom(
          this.http.post<Session>(`${this.apiUrl}/auth/refresh`, {}, this.options(csrf)),
        );
        if (revision !== this.revision) return this.current();
        this.current.set(renewed);
        this.notice.set('');
        return renewed;
      } catch (error: unknown) {
        if (revision === this.revision) {
          this.invalidate(
            error instanceof HttpErrorResponse && error.status === 0
              ? 'Não foi possível restaurar sua sessão. Verifique a conexão e entre novamente.'
              : 'Sua sessão não pôde ser renovada. Entre novamente.',
          );
        }
        return null;
      }
    }).finally(() => {
      this.refreshFlight = null;
    });
    return this.refreshFlight;
  }

  async logout(): Promise<void> {
    const session = await this.ensureSession();
    if (!session) return;
    const revision = this.revision;
    await this.withSessionLock(async () => {
      if (revision !== this.revision) return;
      const csrf = this.csrfCookie();
      if (!csrf) {
        this.invalidate('Sua sessão expirou. Entre novamente.');
        return;
      }
      // Failed logout must not be shown as successful server-side revocation.
      const options = this.options(csrf);
      await firstValueFrom(
        this.http.post<void>(
          `${this.apiUrl}/auth/logout`,
          {},
          {
            ...options,
            headers: options.headers.set('Authorization', `Bearer ${session.accessToken}`),
          },
        ),
      );
      if (revision !== this.revision) return;
      this.invalidate('Você saiu de todos os dispositivos.');
      this.channel?.postMessage('logout');
    });
  }

  invalidate(message = 'Sua sessão expirou. Entre novamente.'): void {
    this.revision++;
    this.current.set(null);
    this.refreshBlocked = true;
    this.notice.set(message);
  }

  private csrfCookie(): string | null {
    const value = this.document.cookie
      .split('; ')
      .find((cookie) => cookie.startsWith('__Host-somnia-csrf='))
      ?.split('=')
      .slice(1)
      .join('=');
    return value || null;
  }

  private options(csrf?: string) {
    let headers = new HttpHeaders({ 'X-Somnia-Client': 'web' });
    if (csrf) headers = headers.set('X-CSRF-Token', csrf);
    return { headers, withCredentials: true, timeout: 15_000 };
  }

  private withSessionLock<T>(operation: () => Promise<T>): Promise<T> {
    // HTTPS Web Locks serialize rotation between tabs sharing the refresh cookie.
    const locks = this.document.defaultView?.navigator.locks;
    return locks ? locks.request('somnia-session', operation) : operation();
  }
}
