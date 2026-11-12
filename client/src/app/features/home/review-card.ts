import { Component, computed, input, signal } from '@angular/core';
import { Icon } from '../../shared/icon';
import { MusicItem, Review, coverUrl, spotifyUrl } from './home.models';

@Component({
  selector: 'app-review-card',
  imports: [Icon],
  template: `
    <article>
      <header>
        <span class="avatar" aria-hidden="true">{{
          review().author.displayName.slice(0, 1).toUpperCase()
        }}</span>
        <div class="author">
          <strong>{{ review().author.displayName }}</strong><span>{{
            '@' + review().author.username
          }}</span>
        </div>
        <time [attr.datetime]="review().createdAt">{{ date() }}</time>
      </header>
      <div class="body">
        @if (cover(); as url) {
          @if (!imageFailed()) {
            <img
              class="cover"
              [src]="url"
              alt=""
              loading="lazy"
              referrerpolicy="no-referrer"
              (error)="imageFailed.set(true)"
            />
          } @else {
            <div class="cover fallback" aria-hidden="true"><app-icon name="music" /></div>
          }
        } @else {
          <div class="cover fallback" aria-hidden="true"><app-icon name="music" /></div>
        }
        <div class="content">
          <div class="heading">
            <div>
              <p class="type">{{ review().item.type === 'album' ? 'Álbum' : 'Música' }}</p>
              <h2>
                {{ item()?.title || (pending() ? 'Carregando título…' : 'Título indisponível') }}
              </h2>
              @if (item(); as music) {
                <p class="artist">{{ music.artists.join(', ') || 'Artista não informado' }}</p>
              }
            </div>
            @if (review().score !== null) {
              <p class="score" [attr.aria-label]="'Nota ' + review().score + ' de 100'">
                <strong>{{ review().score }}</strong><span>/ 100</span>
              </p>
            } @else {
              <span class="no-score">Sem nota</span>
            }
          </div>
          @if (review().review; as text) {
            <p class="review" [class.collapsed]="!expanded() && text.length > 320">{{ text }}</p>
            @if (text.length > 320) {
              <button
                class="expand"
                (click)="expanded.set(!expanded())"
                [attr.aria-expanded]="expanded()"
              >
                {{ expanded() ? 'Ler menos' : 'Ler resenha completa' }}
              </button>
            }
          }
          <footer>
            @if (spotify(); as url) {
              <a [href]="url" target="_blank" rel="noopener noreferrer"
                >Abrir no Spotify <span aria-hidden="true">↗</span
                ><span class="sr-only"> (nova aba)</span></a
              >
            } @else if (!item() && !pending()) {
              <span>Dados musicais indisponíveis no momento.</span>
            }
            @if (review().updatedAt) {
              <span class="edited">Editada</span>
            }
          </footer>
        </div>
      </div>
    </article>
  `,
  styleUrl: './review-card.scss',
})
export class ReviewCard {
  readonly review = input.required<Review>();
  readonly item = input<MusicItem | null>(null);
  readonly pending = input(false);
  protected readonly expanded = signal(false);
  protected readonly imageFailed = signal(false);
  protected readonly cover = computed(() => coverUrl(this.item()?.coverUrl ?? null));
  protected readonly spotify = computed(() => (this.item() ? spotifyUrl(this.item()!) : null));
  protected readonly date = computed(() =>
    new Intl.DateTimeFormat('pt-BR', { day: 'numeric', month: 'short', year: 'numeric' }).format(
      new Date(this.review().createdAt),
    ),
  );
}
