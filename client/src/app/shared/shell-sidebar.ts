import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import {
  LucideCompass,
  LucideHouse,
  LucideMenu,
  LucideSearch,
  LucideUserRound,
} from '@lucide/angular';
import { Account } from '../core/auth/auth.models';
import { AuthService } from '../core/auth/auth.service';
import { environment } from '../environments/environment';
import { Brand } from './brand';

@Component({
  selector: 'app-shell-sidebar',
  imports: [
    Brand,
    RouterLink,
    LucideCompass,
    LucideHouse,
    LucideMenu,
    LucideSearch,
    LucideUserRound,
  ],
  template: `
    <header class="mobile-header">
      <a routerLink="/inicio" aria-label="Somnia — início"><app-brand /></a>
      <button
        (click)="menuOpen.set(!menuOpen())"
        [attr.aria-expanded]="menuOpen()"
        aria-controls="shell-navigation"
        aria-label="Abrir navegação"
      >
        <svg lucideMenu class="size-5" aria-hidden="true"></svg>
      </button>
    </header>
    <aside id="shell-navigation" class="sidebar" [class.open]="menuOpen()">
      <a class="brand-link" routerLink="/inicio" aria-label="Somnia — início"><app-brand /></a>
      <nav aria-label="Navegação principal">
        <a routerLink="/inicio"><svg lucideHouse class="size-5" aria-hidden="true"></svg>Início</a>
        <a routerLink="/inicio" [queryParams]="{ view: 'discover' }"
          ><svg lucideCompass class="size-5" aria-hidden="true"></svg>Descobrir</a
        >
        <a routerLink="/inicio" [queryParams]="{ view: 'search' }"
          ><svg lucideSearch class="size-5" aria-hidden="true"></svg>Buscar</a
        >
        <a routerLink="/conta" [class.active]="router.url.startsWith('/conta')"
          ><svg lucideUserRound class="size-5" aria-hidden="true"></svg>Perfil</a
        >
      </nav>
      <a class="current-account" routerLink="/conta">
        <span class="avatar" aria-hidden="true">
          @if (avatarUrl(); as url) {
            <img [src]="url" alt="" />
          } @else {
            {{ initial() }}
          }
        </span>
        <span class="account-name">{{ displayName() }}</span>
      </a>
    </aside>
  `,
  styles: `
    :host {
      display: block;
    }
    .sidebar {
      position: fixed;
      inset: 0 auto 0 0;
      z-index: 5;
      width: 15.25rem;
      background: #171428;
      border-right: 1px solid #302a48;
      padding: 1.8rem 1.25rem 1.2rem;
      display: flex;
      flex-direction: column;
    }
    .brand-link {
      display: block;
      text-decoration: none;
      margin-bottom: 2.2rem;
    }
    nav {
      display: grid;
      gap: 0.3rem;
    }
    nav a {
      display: flex;
      align-items: center;
      gap: 0.85rem;
      text-decoration: none;
      color: #a99fc5;
      border-radius: 0.65rem;
      padding: 0.78rem 0.85rem;
      font-size: 0.87rem;
    }
    nav a:hover,
    nav a.active {
      background: #2d2747;
      color: var(--text);
    }
    .current-account {
      display: flex;
      align-items: center;
      gap: 0.65rem;
      border-top: 1px solid var(--border);
      padding-top: 1rem;
      margin-top: auto;
      color: var(--text);
      text-decoration: none;
      font-size: 0.78rem;
    }
    .avatar {
      display: grid;
      place-items: center;
      flex: none;
      width: 2.2rem;
      height: 2.2rem;
      border-radius: 50%;
      background: #856da8;
      border: 2px solid #574875;
      color: var(--text);
      overflow: hidden;
    }
    .avatar img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .account-name {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .mobile-header {
      display: none;
    }
    @media (max-width: 1100px) {
      .sidebar {
        width: 13rem;
      }
    }
    @media (max-width: 600px) {
      .mobile-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 1rem;
        background: #171428;
        border-bottom: 1px solid var(--border);
      }
      .mobile-header a {
        text-decoration: none;
      }
      .mobile-header button {
        display: grid;
        place-items: center;
        background: none;
        color: var(--accent);
        border: 0;
        width: 2.7rem;
        height: 2.7rem;
      }
      .sidebar {
        position: static;
        display: none;
        width: 100%;
        border-right: 0;
        border-bottom: 1px solid var(--border);
        padding: 0.8rem 1rem 1rem;
      }
      .sidebar.open {
        display: flex;
      }
      .brand-link {
        display: none;
      }
      .current-account {
        margin-top: 1rem;
      }
    }
  `,
})
export class ShellSidebar {
  protected readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly menuOpen = signal(false);
  protected readonly account = signal<Account | null>(null);
  protected readonly avatarUrl = signal<string | null>(null);
  protected readonly displayName = computed(
    () => this.account()?.displayName || this.auth.session()?.nome || 'Seu perfil',
  );
  protected readonly initial = computed(() => this.displayName().slice(0, 1).toUpperCase());

  constructor() {
    this.http
      .get<Account>(`${environment.apiUrl}/me`, { timeout: 15_000 })
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (account) => {
          this.account.set(account);
          if (!account.username || !account.avatarUrl) return;
          this.http
            .get(`${environment.apiUrl}/users/${encodeURIComponent(account.username)}/avatar`, {
              responseType: 'blob',
              timeout: 15_000,
            })
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: (blob) => {
                if (blob.type === 'image/png') this.avatarUrl.set(URL.createObjectURL(blob));
              },
            });
        },
      });
    this.destroyRef.onDestroy(() => {
      const url = this.avatarUrl();
      if (url) URL.revokeObjectURL(url);
    });
  }
}
