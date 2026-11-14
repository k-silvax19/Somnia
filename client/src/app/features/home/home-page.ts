import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subject, Subscription, catchError, forkJoin, map, mergeMap, of } from 'rxjs';
import { Account } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { apiError } from '../../core/http/api-error';
import { Brand } from '../../shared/brand';
import { Icon } from '../../shared/icon';
import { HomeApi } from './home-api';
import {
  Connections,
  MusicItem,
  MusicReference,
  Review,
  coverUrl,
  musicKey,
  spotifyUrl,
} from './home.models';
import { ReviewCard } from './review-card';

@Component({
  selector: 'app-home-page',
  imports: [RouterLink, ReactiveFormsModule, Brand, Icon, ReviewCard],
  providers: [HomeApi],
  templateUrl: './home-page.html',
  styleUrl: './home-page.scss',
})
export class HomePage {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(HomeApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly account = signal<Account | null>(null);
  protected readonly connections = signal<Connections | null>(null);
  protected readonly reviewCount = signal<number | null>(null);
  protected readonly avatarUrl = signal<string | null>(null);
  protected readonly profileLoading = signal(false);
  protected readonly profileError = signal('');
  protected readonly logoutError = signal('');
  protected readonly loggingOut = signal(false);
  protected readonly menuOpen = signal(false);
  protected readonly tab = signal<'following' | 'discover'>('following');
  protected readonly reviews = signal<Review[]>([]);
  protected readonly loading = signal(false);
  protected readonly feedError = signal('');
  protected readonly hasMore = signal(false);
  protected readonly metadata = signal<Record<string, MusicItem | null>>({});
  protected readonly searchActive = signal(false);
  protected readonly searchLoading = signal(false);
  protected readonly searchError = signal('');
  protected readonly results = signal<MusicItem[]>([]);
  protected readonly searched = signal(false);
  protected readonly searchHasMore = signal(false);
  protected readonly submittedQuery = signal('');
  protected readonly coverUrl = coverUrl;
  protected readonly spotifyUrl = spotifyUrl;
  protected readonly musicKey = musicKey;
  protected readonly displayName = computed(
    () => this.account()?.displayName || this.auth.session()?.nome || 'Sua conta',
  );
  protected readonly initial = computed(() => this.displayName().slice(0, 1).toUpperCase());
  protected readonly searchForm = inject(FormBuilder).nonNullable.group({
    query: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(/\S/)]],
    type: ['album' as 'album' | 'track'],
  });
  private readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('searchInput');
  private offset = 0;
  private searchOffset = 0;
  private submittedType: 'album' | 'track' = 'album';
  private feedRequest?: Subscription;
  private metadataRequest = new Subscription();
  private metadataQueue = new Subject<MusicReference>();
  private searchRequest?: Subscription;
  private readonly now = signal(Date.now());
  private readonly feedBlockedUntil = signal(0);
  private readonly searchBlockedUntil = signal(0);
  protected readonly feedWait = computed(() =>
    Math.max(0, Math.ceil((this.feedBlockedUntil() - this.now()) / 1000)),
  );
  protected readonly searchWait = computed(() =>
    Math.max(0, Math.ceil((this.searchBlockedUntil() - this.now()) / 1000)),
  );

  constructor() {
    effect(() => {
      if (!this.auth.session()) void this.router.navigate(['/entrar']);
    });
    const clock = setInterval(() => this.now.set(Date.now()), 1000);
    this.destroyRef.onDestroy(() => {
      clearInterval(clock);
      this.feedRequest?.unsubscribe();
      this.metadataRequest.unsubscribe();
      this.searchRequest?.unsubscribe();
      const avatar = this.avatarUrl();
      if (avatar) URL.revokeObjectURL(avatar);
    });
    this.loadProfile();
    this.loadFeed();
  }

  protected loadProfile(): void {
    if (this.profileLoading()) return;
    this.profileLoading.set(true);
    this.profileError.set('');
    this.api
      .account()
      .pipe(
        mergeMap((account) => {
          this.account.set(account);
          if (account.username && account.avatarUrl && !this.avatarUrl()) {
            this.api
              .avatar(account.username)
              .pipe(takeUntilDestroyed(this.destroyRef))
              .subscribe({
                next: (blob) => {
                  if (blob.type !== 'image/png') return;
                  const previous = this.avatarUrl();
                  if (previous) URL.revokeObjectURL(previous);
                  this.avatarUrl.set(URL.createObjectURL(blob));
                },
                error: () => {
                  /* Initials remain visible when an avatar is unavailable. */
                },
              });
          }
          if (!account.username) return of({ connections: null, count: null });
          return forkJoin({
            connections: this.api.connections(account.username).pipe(catchError(() => of(null))),
            count: this.api.reviewCount(account.username).pipe(catchError(() => of(null))),
          });
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ connections, count }) => {
          this.connections.set(connections);
          this.reviewCount.set(count);
          this.profileLoading.set(false);
          if (!connections || count === null)
            this.profileError.set('Não foi possível carregar todas as informações da conta.');
        },
        error: () => {
          this.profileError.set('Não foi possível carregar sua conta.');
          this.profileLoading.set(false);
        },
      });
  }

  protected selectTab(tab: 'following' | 'discover'): void {
    this.searchActive.set(false);
    this.menuOpen.set(false);
    if (this.tab() === tab) return;
    this.tab.set(tab);
    this.loadFeed();
  }

  protected loadFeed(more = false): void {
    if (this.feedWait() || (more && this.loading())) return;
    this.feedRequest?.unsubscribe();
    if (!more) {
      this.metadataRequest.unsubscribe();
      this.metadataQueue.complete();
      this.metadataQueue = new Subject<MusicReference>();
      this.metadataRequest = this.metadataQueue
        .pipe(
          mergeMap(
            (item) =>
              this.api.metadata(item).pipe(map((metadata) => ({ key: musicKey(item), metadata }))),
            3,
          ),
        )
        .subscribe(({ key, metadata }) =>
          this.metadata.update((current) => ({ ...current, [key]: metadata })),
        );
      this.reviews.set([]);
      this.metadata.set({});
      this.offset = 0;
      this.hasMore.set(false);
    }
    this.loading.set(true);
    this.feedError.set('');
    this.feedRequest = this.api.reviews(this.tab(), this.offset).subscribe({
      next: (page) => {
        const previous = more ? this.reviews() : [];
        const ids = new Set(previous.map((review) => review.id));
        const added = page.items.filter((review) => !ids.has(review.id));
        this.reviews.set([...previous, ...added]);
        this.offset = page.offset + page.limit;
        this.hasMore.set(page.hasMore && this.offset <= 10_000);
        this.loading.set(false);
        const unique = [
          ...new Map(added.map((review) => [musicKey(review.item), review.item])).values(),
        ];
        for (const item of unique) this.metadataQueue.next(item);
      },
      error: (error: unknown) => {
        const problem = apiError(error);
        this.feedError.set(problem.message);
        this.feedBlockedUntil.set(Date.now() + problem.retryAfter * 1000);
        this.now.set(Date.now());
        this.loading.set(false);
      },
    });
  }

  protected openSearch(): void {
    this.searchActive.set(true);
    this.menuOpen.set(false);
    this.searchInput()?.nativeElement.focus();
  }

  protected search(more = false): void {
    if (this.searchWait() || (more && this.searchLoading())) return;
    if (!more) {
      this.searchForm.markAllAsTouched();
      if (this.searchForm.invalid) {
        this.searchInput()?.nativeElement.focus();
        return;
      }
      this.submittedQuery.set(this.searchForm.controls.query.value.trim());
      this.submittedType = this.searchForm.controls.type.value;
      this.results.set([]);
      this.searchOffset = 0;
      this.searchHasMore.set(false);
    }
    this.searchRequest?.unsubscribe();
    this.searchActive.set(true);
    this.searched.set(true);
    this.searchLoading.set(true);
    this.searchError.set('');
    this.searchRequest = this.api
      .search(this.submittedQuery(), this.submittedType, this.searchOffset)
      .subscribe({
        next: (page) => {
          const existing = more ? this.results() : [];
          const keys = new Set(existing.map(musicKey));
          this.results.set([
            ...existing,
            ...page.items.filter((item) => !keys.has(musicKey(item))),
          ]);
          this.searchOffset = page.offset + page.limit;
          this.searchHasMore.set(page.hasMore && this.searchOffset <= 1000);
          this.searchLoading.set(false);
        },
        error: (error: unknown) => {
          const problem = apiError(error);
          this.searchError.set(problem.message);
          this.searchBlockedUntil.set(Date.now() + problem.retryAfter * 1000);
          this.now.set(Date.now());
          this.searchLoading.set(false);
        },
      });
  }

  protected async logout(): Promise<void> {
    if (this.loggingOut()) return;
    this.loggingOut.set(true);
    this.logoutError.set('');
    try {
      await this.auth.logout();
      await this.router.navigate(['/entrar']);
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 401) this.auth.invalidate();
      else this.logoutError.set(apiError(error).message);
    } finally {
      this.loggingOut.set(false);
    }
  }
}
