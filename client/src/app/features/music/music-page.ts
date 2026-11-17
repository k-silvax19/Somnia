import { Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ShellSidebar } from '../../shared/shell-sidebar';
import { MusicReference, musicKey } from '../home/home.models';
import { validReference } from './music-api';
import { MusicDetailsView } from './music-details';

@Component({
  selector: 'app-music-page',
  imports: [RouterLink, ShellSidebar, MusicDetailsView],
  template: `
    @if (auth.session()) {
      <app-shell-sidebar />
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
    main {
      max-width: 1180px;
      margin-left: 15.25rem;
      padding: 2.5rem 2.2rem 4rem;
    }
    .back {
      font-size: 0.85rem;
      text-decoration: none;
      color: var(--muted);
    }
    @media (max-width: 1100px) {
      main {
        margin-left: 13rem;
      }
    }
    @media (max-width: 600px) {
      main {
        margin-left: 0;
        padding: 1.5rem 1rem 4rem;
      }
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
