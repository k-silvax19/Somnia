import { Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Brand } from '../../shared/brand';
import { MusicReference, musicKey } from '../home/home.models';
import { validReference } from './music-api';
import { MusicDetailsView } from './music-details';

@Component({
  selector: 'app-music-page',
  imports: [RouterLink, Brand, MusicDetailsView],
  template: `
    @if (auth.session()) {
      <header>
        <a routerLink="/inicio" aria-label="Somnia — início"><app-brand /></a>
        <nav aria-label="Navegação principal">
          <a routerLink="/inicio">Início</a><a routerLink="/conta">Minha conta</a>
        </nav>
      </header>
      <main>
        <a class="back" routerLink="/inicio">← Voltar ao início</a>
        @for (item of selection(); track musicKey(item)) {
          <app-music-details [reference]="item" />
        } @empty {
          <h1>Endereço musical inválido</h1>
          <p>Volte ao início e selecione um álbum ou uma música na busca.</p>
        }
      </main>
    }
  `,
  styles: `
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 1.2rem max(1rem, calc((100vw - 1200px) / 2));
      border-bottom: 1px solid var(--border);
      background: #191d21;
    }
    header > a {
      text-decoration: none;
    }
    nav {
      display: flex;
      gap: 1.5rem;
      font-size: 0.85rem;
    }
    nav a {
      color: var(--text);
      text-decoration: none;
    }
    main {
      max-width: 1232px;
      padding: 1.5rem 1rem 4rem;
      margin: auto;
    }
    .back {
      font-size: 0.85rem;
      text-decoration: none;
      color: var(--muted);
    }
  `,
})
export class MusicPage {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly params = toSignal(inject(ActivatedRoute).paramMap);
  protected readonly musicKey = musicKey;
  protected readonly selection = computed(() => {
    const params = this.params();
    const item: MusicReference = {
      provider: params?.get('provider') ?? '',
      type: params?.get('type') as MusicReference['type'],
      externalId: params?.get('externalId') ?? '',
    };
    return validReference(item) ? [item] : [];
  });
  constructor() {
    effect(() => {
      if (!this.auth.session()) void this.router.navigate(['/entrar']);
    });
  }
}
