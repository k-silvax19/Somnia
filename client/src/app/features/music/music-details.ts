import { Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { MusicReference, Review, coverUrl, spotifyUrl } from '../home/home.models';
import { ReviewCard } from '../home/review-card';
import { HomeApi } from '../home/home-api';
import { Icon } from '../../shared/icon';
import { ItemReviews, MusicApi, MusicDetails, musicError, musicRoute } from './music-api';
import { ReviewEditor } from './review-editor';

@Component({
  selector: 'app-music-details',
  imports: [RouterLink, Icon, ReviewCard, ReviewEditor],
  providers: [HomeApi],
  templateUrl: './music-details.html',
  styleUrl: './music-details.scss',
})
export class MusicDetailsView implements OnInit {
  readonly reference = input.required<MusicReference>();
  private readonly api = inject(MusicApi);
  private readonly previewApi = inject(HomeApi);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly item = signal<MusicDetails | null>(null);
  protected readonly metadataLoading = signal(false);
  protected readonly metadataError = signal('');
  protected readonly imageFailed = signal(false);
  protected readonly reviews = signal<Review[]>([]);
  protected readonly summary = signal<ItemReviews['summary'] | null>(null);
  protected readonly mine = signal<Review | null>(null);
  protected readonly loaded = signal(false);
  protected readonly loading = signal(false);
  protected readonly reviewError = signal('');
  protected readonly hasMore = signal(false);
  protected readonly writing = signal(false);
  protected readonly retryAppend = signal(false);
  protected readonly musicRoute = musicRoute;
  protected readonly cover = computed(() => coverUrl(this.item()?.coverUrl ?? null));
  protected readonly spotify = computed(() => (this.item() ? spotifyUrl(this.item()!) : null));
  protected readonly average = computed(() => {
    const average = this.summary()?.average;
    return average == null
      ? '—'
      : new Intl.NumberFormat('pt-BR', {
          minimumFractionDigits: 1,
          maximumFractionDigits: 1,
        }).format(average);
  });
  private offset = 0;
  private request?: Subscription;
  private readonly now = signal(Date.now());
  private readonly catalogBlockedUntil = signal(0);
  private readonly reviewsBlockedUntil = signal(0);
  protected readonly catalogWait = computed(() =>
    Math.max(0, Math.ceil((this.catalogBlockedUntil() - this.now()) / 1000)),
  );
  protected readonly catalogWaitLabel = computed(() => {
    const seconds = this.catalogWait();
    if (seconds < 60) return `${seconds}s`;
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.ceil((seconds % 3600) / 60);
    return hours ? `${hours}h${minutes ? ` ${minutes}min` : ''}` : `${minutes}min`;
  });
  protected readonly reviewsWait = computed(() =>
    Math.max(0, Math.ceil((this.reviewsBlockedUntil() - this.now()) / 1000)),
  );

  constructor() {
    const clock = setInterval(() => this.now.set(Date.now()), 1000);
    this.destroyRef.onDestroy(() => clearInterval(clock));
  }
  ngOnInit() {
    this.loadMetadata();
    this.loadReviews();
  }
  protected loadMetadata() {
    if (this.metadataLoading() || this.catalogWait()) return;
    this.metadataLoading.set(true);
    this.metadataError.set('');
    this.api
      .details(this.reference())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (item) => {
          this.item.set(item);
          this.imageFailed.set(false);
          this.metadataLoading.set(false);
        },
        error: (error: unknown) => {
          const problem = musicError(error, true);
          this.metadataError.set(problem.message);
          this.catalogBlockedUntil.set(Date.now() + problem.retryAfter * 1000);
          if (this.reference().provider !== 'spotify') {
            this.metadataLoading.set(false);
            return;
          }
          this.previewApi
            .metadata(this.reference())
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe((preview) => {
              if (preview) {
                this.item.set(preview);
                this.imageFailed.set(false);
              }
              this.metadataLoading.set(false);
            });
        },
      });
  }
  protected loadReviews(append = false) {
    if (this.loading() || this.writing() || this.reviewsWait()) return;
    this.request?.unsubscribe();
    if (!append) this.offset = 0;
    this.retryAppend.set(append);
    this.loading.set(true);
    this.reviewError.set('');
    this.request = this.api
      .reviews(this.reference(), this.offset)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          const all = append ? [...this.reviews(), ...data.reviews.items] : data.reviews.items;
          this.reviews.set([...new Map(all.map((review) => [review.id, review])).values()]);
          this.summary.set(data.summary);
          this.mine.set(data.myReview);
          this.loaded.set(true);
          this.offset = data.reviews.offset + data.reviews.limit;
          this.hasMore.set(data.reviews.hasMore && this.offset <= 10_000);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          const problem = musicError(error);
          this.reviewError.set(problem.message);
          this.reviewsBlockedUntil.set(Date.now() + problem.retryAfter * 1000);
          this.loading.set(false);
        },
      });
  }
  protected onBusy(busy: boolean) {
    this.writing.set(busy);
    if (busy) {
      // A read started before a write must never resurrect a deleted/older review.
      this.request?.unsubscribe();
      this.loading.set(false);
    }
  }
  protected onChanged(review: Review | null) {
    this.mine.set(review);
    this.summary.set(null);
    this.reviews.set([]);
    this.hasMore.set(false);
    this.loadReviews();
  }
}
