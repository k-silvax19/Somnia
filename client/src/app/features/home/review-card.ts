import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { LucideExternalLink, LucideHeart, LucideShare2 } from '@lucide/angular';
import { Icon } from '../../shared/icon';
import { MusicApi } from '../music/music-api';
import { ReviewStoryShare } from './review-story-share';
import { MusicItem, Review, coverUrl, spotifyUrl } from './home.models';
import { ReviewAvatarCache } from './review-avatar-cache';

@Component({
  selector: 'app-review-card',
  imports: [Icon, RouterLink, LucideExternalLink, LucideHeart, LucideShare2],
  template: `
    <article [class.clickable]="linkToItem()">
      @if (linkToItem()) {
        <a
          class="card-link"
          [routerLink]="[
            '/musica',
            review().item.provider,
            review().item.type,
            review().item.externalId,
          ]"
          [attr.aria-label]="
            'Ver detalhes de ' +
            (item()?.title || (review().item.type === 'album' ? 'o álbum' : 'a música'))
          "
        ></a>
      }
      <header>
        <span class="avatar" aria-hidden="true">
          @if (avatarUrl() && !avatarImageFailed()) {
            <img [src]="avatarUrl()" alt="" (error)="avatarImageFailed.set(true)" />
          } @else {
            {{ review().author.displayName.slice(0, 1).toUpperCase() }}
          }
        </span>
        <div class="author">
          <strong
            ><a [routerLink]="['/perfil', review().author.username]">{{
              review().author.displayName
            }}</a></strong
          ><span>{{ '@' + review().author.username }}</span>
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
                @if (music.artists.length) {
                  <p class="artist">{{ music.artists.join(', ') }}</p>
                }
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
            <button
              class="like-button"
              [class.liked]="likedByMe()"
              [disabled]="liking()"
              [attr.aria-pressed]="likedByMe()"
              [attr.aria-label]="likedByMe() ? 'Remover curtida' : 'Curtir avaliação'"
              (click)="toggleLike($event)"
            >
              <svg lucideHeart aria-hidden="true"></svg>
              <span>{{ likedByMe() ? 'Curtida' : 'Curtir' }}</span>
              <span>{{ likesCount() }}</span>
            </button>
            @if (likeError()) {
              <span class="like-error" role="status">{{ likeError() }}</span>
            }
            @if (canShare()) {
              <button
                class="share-button"
                [disabled]="sharing()"
                (click)="shareStory($event)"
                aria-label="Compartilhar avaliação"
              >
                <svg lucideShare2 aria-hidden="true"></svg>
                <span>Compartilhar</span>
              </button>
            }
            @if (shareStatus()) {
              <span role="status">{{ shareStatus() }}</span>
            }
            @if (shareError()) {
              <span class="share-error" role="alert">{{ shareError() }}</span>
            }
            @if (spotify(); as url) {
              <a [href]="url" target="_blank" rel="noopener noreferrer"
                >Abrir no Spotify <svg lucideExternalLink class="spotify-link-icon" aria-hidden="true"></svg
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
  private readonly avatars = inject(ReviewAvatarCache);
  readonly review = input.required<Review>();
  readonly item = input<MusicItem | null>(null);
  readonly pending = input(false);
  readonly linkToItem = input(true);
  readonly canShare = input(false);
  private readonly api = inject(MusicApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly storyShare = inject(ReviewStoryShare);
  protected readonly expanded = signal(false);
  protected readonly imageFailed = signal(false);
  protected readonly avatarUrl = signal<string | null>(null);
  protected readonly avatarImageFailed = signal(false);
  protected readonly likesCount = signal(0);
  protected readonly likedByMe = signal(false);
  protected readonly liking = signal(false);
  protected readonly likeError = signal('');
  protected readonly sharing = signal(false);
  protected readonly shareStatus = signal('');
  protected readonly shareError = signal('');
  protected readonly cover = computed(() => coverUrl(this.item()?.coverUrl ?? null));
  protected readonly spotify = computed(() => (this.item() ? spotifyUrl(this.item()!) : null));
  protected readonly date = computed(() =>
    new Intl.DateTimeFormat('pt-BR', { day: 'numeric', month: 'short', year: 'numeric' }).format(
      new Date(this.review().createdAt),
    ),
  );

  constructor() {
    effect(() => {
      this.likesCount.set(this.review().likesCount ?? 0);
      this.likedByMe.set(this.review().likedByMe ?? false);
      this.likeError.set('');
    });
    effect((onCleanup) => {
      const author = this.review().author;
      const avatarVersion = this.avatars.version(author.username);
      this.avatarUrl.set(null);
      this.avatarImageFailed.set(false);
      let objectUrl: string | null = null;
      const request = this.avatars.load(author.username).subscribe((blob) => {
        if (objectUrl) URL.revokeObjectURL(objectUrl);
        objectUrl = blob ? URL.createObjectURL(blob) : null;
        this.avatarUrl.set(objectUrl);
        this.avatarImageFailed.set(false);
      });
      onCleanup(() => {
        request.unsubscribe();
        if (objectUrl) URL.revokeObjectURL(objectUrl);
      });
    });
  }

  protected toggleLike(event: MouseEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (this.liking()) return;

    const previousLiked = this.likedByMe();
    const previousCount = this.likesCount();
    const nextLiked = !previousLiked;
    this.likedByMe.set(nextLiked);
    this.likesCount.set(Math.max(0, previousCount + (nextLiked ? 1 : -1)));
    this.likeError.set('');
    this.liking.set(true);
    this.api
      .setLiked(this.review().id, nextLiked)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        error: () => {
          this.likedByMe.set(previousLiked);
          this.likesCount.set(previousCount);
          this.likeError.set('Não foi possível atualizar a curtida. Tente novamente.');
          this.liking.set(false);
        },
        complete: () => this.liking.set(false),
      });
  }

  protected shareStory(event: MouseEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (!this.canShare() || this.sharing()) return;

    this.sharing.set(true);
    this.shareStatus.set('');
    this.shareError.set('');
    this.storyShare.share(this.review(), this.item()).then((result) => {
      if (result === 'shared') this.shareStatus.set('Imagem enviada ao compartilhador do dispositivo.');
      if (result === 'downloaded') this.shareStatus.set('Imagem baixada; abra o Instagram para publicar no Story.');
      if (result === 'cancelled') this.shareStatus.set('Compartilhamento cancelado.');
    }).catch(() => {
      this.shareError.set('Não foi possível compartilhar a imagem. Tente novamente.');
    }).finally(() => this.sharing.set(false));
  }
}
