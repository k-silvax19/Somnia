import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { Account } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { apiError } from '../../core/http/api-error';
import { Brand } from '../../shared/brand';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-account-page',
  imports: [Brand],
  template: `
    <header>
      <app-brand /><span class="status"><i></i> Conta conectada</span>
    </header>
    <main>
      @if (auth.session()) {
        <section aria-labelledby="account-title">
          <div class="avatar" aria-hidden="true">{{ initial() }}</div>
          <p class="eyebrow">BEM-VINDO AO SOMNIA</p>
          <h1 id="account-title">Olá, {{ account()?.displayName || auth.session()?.nome }}.</h1>
          <p class="description">Sua conta está pronta para o próximo play.</p>
          @if (loading()) {
            <p role="status">Carregando sua conta…</p>
          }
          @if (error()) {
            <p class="message error" role="alert">{{ error() }}</p>
            @if (!account()) {
              <button class="button secondary" (click)="load()" [disabled]="loading()">
                Tentar novamente
              </button>
            }
          }
          @if (account(); as user) {
            <dl>
              <div>
                <dt>Nome de usuário</dt>
                <dd>{{ user.username ? '@' + user.username : 'Não informado' }}</dd>
              </div>
              <div>
                <dt>E-mail</dt>
                <dd>{{ user.email }}</dd>
              </div>
            </dl>
          }
          <div class="logout">
            <button class="button secondary" (click)="logout()" [disabled]="pending()">
              {{ pending() ? 'Saindo…' : 'Sair de todos os dispositivos' }}
            </button>
            <p>Ao sair, todas as suas sessões serão encerradas.</p>
          </div>
        </section>
      }
    </main>
  `,
  styles: `
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 1.6rem 3rem;
      border-bottom: 1px solid var(--border);
    }
    .status {
      font-size: 0.78rem;
      color: var(--muted);
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .status i {
      width: 0.4rem;
      height: 0.4rem;
      border-radius: 50%;
      background: var(--accent);
    }
    main {
      padding: 4rem 1.5rem;
    }
    section {
      max-width: 36rem;
      margin: auto;
    }
    .avatar {
      width: 4rem;
      height: 4rem;
      background: #d99a3d18;
      color: var(--accent);
      border: 1px solid #d99a3d40;
      border-radius: 50%;
      display: grid;
      place-items: center;
      font-size: 1.6rem;
      margin-bottom: 2rem;
    }
    h1 {
      font-size: clamp(2rem, 6vw, 3rem);
      letter-spacing: -0.04em;
      margin: 0.75rem 0;
      overflow-wrap: anywhere;
    }
    .description {
      color: var(--muted);
      line-height: 1.6;
    }
    dl {
      padding: 0.25rem 1.5rem;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 0.75rem;
      margin: 2.5rem 0;
    }
    dl > div {
      padding: 1.2rem 0;
    }
    dl > div + div {
      border-top: 1px solid var(--border);
    }
    dt {
      color: var(--muted);
      font-size: 0.8rem;
      margin-bottom: 0.5rem;
    }
    dd {
      margin: 0;
      overflow-wrap: anywhere;
    }
    .logout p {
      font-size: 0.75rem;
      color: var(--muted);
      line-height: 1.6;
    }
    @media (max-width: 600px) {
      header {
        padding: 1.4rem;
      }
      main {
        padding-top: 2rem;
      }
      .status {
        max-width: 6.5rem;
      }
    }
  `,
})
export class AccountPage {
  protected readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly account = signal<Account | null>(null);
  protected readonly loading = signal(false);
  protected readonly pending = signal(false);
  protected readonly error = signal('');
  protected readonly initial = () =>
    (this.account()?.displayName || this.auth.session()?.nome || 'S').slice(0, 1).toUpperCase();

  constructor() {
    effect(() => {
      if (!this.auth.session()) {
        this.account.set(null);
        void this.router.navigate(['/entrar']);
      }
    });
    this.load();
  }

  protected load(): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set('');
    this.http
      .get<Account>(`${environment.apiUrl}/me`, { timeout: 15_000 })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (account) => {
          this.account.set(account);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.error.set(apiError(error).message);
          this.loading.set(false);
        },
      });
  }

  protected async logout(): Promise<void> {
    if (this.pending()) return;
    this.pending.set(true);
    this.error.set('');
    try {
      await this.auth.logout();
      await this.router.navigate(['/entrar']);
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.auth.invalidate();
      } else {
        this.error.set(apiError(error).message);
      }
    } finally {
      this.pending.set(false);
    }
  }
}
