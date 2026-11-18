import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LucideCamera, LucideCheck, LucideLogOut, LucideSettings2, LucideX } from '@lucide/angular';
import { Observable, Subscription, from, mergeMap, tap } from 'rxjs';
import { Account } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { apiError } from '../../core/http/api-error';
import { ShellSidebar } from '../../shared/shell-sidebar';
import { HomeApi } from '../home/home-api';
import { MusicItem, Review, coverUrl, musicKey } from '../home/home.models';
import { ReviewCard } from '../home/review-card';
import { ReviewAvatarCache } from '../home/review-avatar-cache';
import {
  AvatarRequest,
  BannerTheme,
  ProfileApi,
  PublicProfile,
  RelatedProfile,
} from './profile-api';

@Component({
  selector: 'app-profile-page',
  imports: [
    ShellSidebar,
    RouterLink,
    ReactiveFormsModule,
    ReviewCard,
    LucideCamera,
    LucideCheck,
    LucideLogOut,
    LucideSettings2,
    LucideX,
  ],
  providers: [ProfileApi, HomeApi],
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.scss',
})
export class ProfilePage {
  private readonly api = inject(ProfileApi);
  private readonly homeApi = inject(HomeApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reviewAvatars = inject(ReviewAvatarCache);
  protected readonly auth = inject(AuthService);
  protected readonly own = this.route.snapshot.data['own'] === true;
  protected readonly selfUsername = signal('');
  protected readonly selfLoaded = signal(false);
  protected readonly profile = signal<PublicProfile | null>(null);
  protected readonly account = signal<Account | null>(null);
  protected readonly connections = signal<{
    isFollowing: boolean;
    followersCount: number;
    followingCount: number;
  } | null>(null);
  protected readonly reviews = signal<Review[]>([]);
  protected readonly related = signal<RelatedProfile[]>([]);
  protected readonly metadata = signal<Record<string, MusicItem | null>>({});
  protected readonly avatarUrl = signal<string | null>(null);
  protected readonly relatedAvatarUrls = signal<Record<string, string>>({});
  protected readonly section = signal<'reviews' | 'followers' | 'following'>('reviews');
  protected readonly loading = signal(true);
  protected readonly listLoading = signal(false);
  protected readonly listMore = signal(false);
  protected readonly listTotal = signal<number | null>(null);
  protected readonly reviewTotal = signal<number | null>(null);
  protected readonly error = signal('');
  protected readonly listError = signal('');
  protected readonly connectionError = signal('');
  protected readonly saving = signal(false);
  protected readonly editing = signal(false);
  protected readonly saveError = signal('');
  protected readonly saved = signal(false);
  protected readonly following = signal(false);
  protected readonly avatarBusy = signal(false);
  protected readonly avatarPickerOpen = signal(false);
  protected readonly avatarMessage = signal('');
  protected readonly avatarError = signal('');
  protected readonly loggingOut = signal(false);
  protected readonly logoutError = signal('');
  protected readonly musicKey = musicKey;
  protected readonly coverUrl = coverUrl;
  protected readonly bannerThemes: { value: BannerTheme; label: string }[] = [
    { value: 'midnight', label: 'Meia-noite' },
    { value: 'aurora', label: 'Aurora' },
    { value: 'mist', label: 'Neblina' },
  ];
  protected readonly initial = computed(() =>
    (this.profile()?.displayName || 'S').slice(0, 1).toUpperCase(),
  );
  protected readonly reviewedAlbums = computed(() => {
    const seen = new Set<string>();
    return this.reviews()
      .filter((review) => review.item.type === 'album')
      .map((review) => this.metadata()[musicKey(review.item)])
      .filter((item): item is MusicItem => !!item)
      .filter((item) => {
        const key = musicKey(item);
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
      })
      .slice(0, 4);
  });
  protected readonly editForm = inject(FormBuilder).nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(256), Validators.pattern(/\S/)]],
    bio: ['', [Validators.maxLength(300)]],
    bannerTheme: ['midnight' as BannerTheme],
  });
  private requests = new Subscription();
  private listRequest?: Subscription;
  private avatarRequest?: Subscription;
  private readonly relatedAvatarRequests = new Map<string, Subscription>();
  private avatarPoll?: ReturnType<typeof setTimeout>;
  private avatarAttempts = 0;
  private readonly avatarFileInput = viewChild<ElementRef<HTMLInputElement>>('avatarFileInput');
  private readonly avatarTrigger = viewChild<ElementRef<HTMLButtonElement>>('avatarTrigger');
  private readonly chooseAvatarButton =
    viewChild<ElementRef<HTMLButtonElement>>('chooseAvatarButton');
  private currentUsername = '';
  private nextOffset = 0;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.reset();
      const username = params.get('username');
      if (this.own) this.loadOwn();
      else if (username && /^[a-zA-Z0-9._-]{3,30}$/.test(username)) this.loadPublic(username);
      else {
        this.loading.set(false);
        this.error.set('Nome de usuário inválido.');
      }
    });
    this.destroyRef.onDestroy(() => this.reset());
  }

  private reset(): void {
    this.requests.unsubscribe();
    this.requests = new Subscription();
    this.listRequest = undefined;
    this.avatarRequest = undefined;
    if (this.avatarPoll) clearTimeout(this.avatarPoll);
    this.clearRelatedAvatars();
    this.avatarUrl.set(null);
    this.profile.set(null);
    this.account.set(null);
    this.connections.set(null);
    this.reviews.set([]);
    this.related.set([]);
    this.metadata.set({});
    this.section.set('reviews');
    this.loading.set(true);
    this.listLoading.set(false);
    this.listMore.set(false);
    this.listTotal.set(null);
    this.reviewTotal.set(null);
    this.error.set('');
    this.listError.set('');
    this.connectionError.set('');
    this.avatarMessage.set('');
    this.avatarError.set('');
    this.avatarPickerOpen.set(false);
    this.editing.set(false);
    this.currentUsername = '';
    this.selfUsername.set('');
    this.selfLoaded.set(false);
    this.nextOffset = 0;
  }

  private loadOwn(): void {
    this.requests.add(
      this.api.me().subscribe({
        next: (account) => {
          if (!account.username) {
            this.profileFailed(new Error('Sua conta não possui nome de usuário.'));
            return;
          }
          this.account.set(account);
          this.editForm.reset({
            displayName: account.displayName,
            bio: account.bio ?? '',
            bannerTheme: account.bannerTheme ?? 'midnight',
          });
          this.showProfile({ ...account, username: account.username });
        },
        error: (error: unknown) => this.profileFailed(error),
      }),
    );
  }

  private loadPublic(username: string): void {
    this.requests.add(
      this.api.me().subscribe({
        next: (me) => {
          this.selfUsername.set(me.username ?? '');
          this.selfLoaded.set(true);
        },
        error: () => this.selfLoaded.set(true),
      }),
    );
    this.requests.add(
      this.api.profile(username).subscribe({
        next: (profile) => this.showProfile(profile),
        error: (error: unknown) => this.profileFailed(error),
      }),
    );
  }

  private profileFailed(error: unknown): void {
    this.loading.set(false);
    this.error.set(
      error instanceof HttpErrorResponse && error.status === 404
        ? 'Perfil não encontrado.'
        : apiError(error).message,
    );
  }

  protected retryProfile(): void {
    this.reset();
    if (this.own) this.loadOwn();
    else this.loadPublic(this.route.snapshot.paramMap.get('username') ?? '');
  }

  private showProfile(profile: PublicProfile): void {
    this.profile.set(profile);
    this.currentUsername = profile.username;
    this.loading.set(false);
    this.loadAvatar();
    this.loadConnections();
    this.loadList();
  }

  private loadAvatar(uploaded = false): void {
    if (!this.currentUsername) return;
    this.avatarRequest?.unsubscribe();
    if (uploaded) this.reviewAvatars.invalidate(this.currentUsername);
    this.avatarRequest = this.reviewAvatars.loadUrl(this.currentUsername).subscribe((url) => {
      this.avatarUrl.set(url);
      if (uploaded && url) {
        this.avatarBusy.set(false);
        this.avatarMessage.set('Avatar atualizado.');
      } else if (uploaded) {
        this.avatarBusy.set(false);
        this.avatarMessage.set('');
        this.avatarError.set('A foto foi processada, mas n\u00e3o p\u00f4de ser carregada. Tente novamente.');
      } else if (!url) {
        this.avatarError.set('');
      }
    });
    this.requests.add(this.avatarRequest);
  }

  protected loadConnections(): void {
    this.connectionError.set('');
    this.requests.add(
      this.api.connections(this.currentUsername).subscribe({
        next: (connections) => this.connections.set(connections),
        error: (error: unknown) => this.connectionError.set(apiError(error).message),
      }),
    );
  }

  protected changeSection(section: 'reviews' | 'followers' | 'following'): void {
    this.editing.set(false);
    if (this.section() === section) return;
    this.listRequest?.unsubscribe();
    this.clearRelatedAvatars();
    this.listLoading.set(false);
    this.section.set(section);
    this.reviews.set([]);
    this.related.set([]);
    this.listTotal.set(null);
    this.listMore.set(false);
    this.listError.set('');
    this.nextOffset = 0;
    this.loadList();
  }

  protected loadList(more = false): void {
    if (!this.currentUsername || this.listLoading()) return;
    this.listLoading.set(true);
    this.listError.set('');
    const section = this.section();
    const offset = more ? this.nextOffset : 0;
    const request: Observable<{
      items: (Review | RelatedProfile)[];
      total: number;
      hasMore: boolean;
    }> =
      section === 'reviews'
        ? this.api.reviews(this.currentUsername, offset)
        : this.api.related(this.currentUsername, section, offset);
    this.listRequest = request.subscribe({
      next: (page) => {
        if (section !== this.section()) return;
        if (section === 'reviews') {
          const items = page.items as Review[];
          const existing = more ? this.reviews() : [];
          this.reviews.set([
            ...existing,
            ...items.filter((item) => !existing.some((old) => old.id === item.id)),
          ]);
          this.reviewTotal.set(page.total);
          this.loadMetadata(items);
        } else {
          const items = page.items as RelatedProfile[];
          const existing = more ? this.related() : [];
          this.related.set([
            ...existing,
            ...items.filter((item) => !existing.some((old) => old.username === item.username)),
          ]);
          this.loadRelatedAvatars(items);
        }
        this.listTotal.set(page.total);
        this.nextOffset = offset + page.items.length;
        this.listMore.set(page.hasMore && this.nextOffset <= 10_000 && page.items.length > 0);
        this.listLoading.set(false);
      },
      error: (error: unknown) => {
        if (section !== this.section()) return;
        this.listError.set(apiError(error).message);
        this.listLoading.set(false);
      },
    });
    this.requests.add(this.listRequest);
  }

  private loadRelatedAvatars(people: RelatedProfile[]): void {
    for (const person of people) {
      const key = person.username.toLowerCase();
      if (this.relatedAvatarUrls()[key] || this.relatedAvatarRequests.has(key)) continue;
      const request = this.reviewAvatars.loadUrl(person.username).subscribe((url) => {
        if (!url) return;
        this.relatedAvatarUrls.update((avatars) => ({ ...avatars, [key]: url }));
      });
      this.relatedAvatarRequests.set(key, request);
    }
  }

  private clearRelatedAvatars(): void {
    for (const request of this.relatedAvatarRequests.values()) request.unsubscribe();
    this.relatedAvatarRequests.clear();
    this.relatedAvatarUrls.set({});
  }

  private loadMetadata(items: Review[]): void {
    const unique = items.filter(
      (review, index, all) =>
        !this.metadata().hasOwnProperty(musicKey(review.item)) &&
        all.findIndex((other) => musicKey(other.item) === musicKey(review.item)) === index,
    );
    this.requests.add(
      from(unique)
        .pipe(
          mergeMap(
            (review) =>
              this.homeApi.metadata(review.item).pipe(
                tap((item) => {
                  this.metadata.update((current) => ({
                    ...current,
                    [musicKey(review.item)]: item,
                  }));
                }),
              ),
            3,
          ),
        )
        .subscribe(),
    );
  }

  protected toggleFollow(): void {
    const connections = this.connections();
    if (!connections || this.following()) return;
    this.following.set(true);
    this.connectionError.set('');
    this.requests.add(
      this.api.follow(this.currentUsername, !connections.isFollowing).subscribe({
        next: () => {
          this.following.set(false);
          this.loadConnections();
          if (this.section() === 'followers') this.loadList();
        },
        error: (error: unknown) => {
          this.following.set(false);
          this.connectionError.set(apiError(error).message);
        },
      }),
    );
  }

  protected save(): void {
    if (this.editForm.invalid || this.saving()) {
      this.editForm.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.saved.set(false);
    this.saveError.set('');
    const { displayName, bio, bannerTheme } = this.editForm.getRawValue();
    this.requests.add(
      this.api.edit(displayName.trim(), bio.trim() || null, bannerTheme).subscribe({
        next: (account) => {
          this.account.set(account);
          this.profile.set({ ...account, username: this.currentUsername });
          this.editForm.reset({
            displayName: account.displayName,
            bio: account.bio ?? '',
            bannerTheme: account.bannerTheme ?? 'midnight',
          });
          this.saving.set(false);
          this.saved.set(true);
        },
        error: (error: unknown) => {
          this.saving.set(false);
          this.saveError.set(apiError(error).message);
        },
      }),
    );
  }

  protected selectAvatar(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || this.avatarBusy()) return;
    if (file.size > 2 * 1024 * 1024 || !['image/png', 'image/jpeg'].includes(file.type)) {
      this.avatarError.set('Escolha um PNG ou JPEG de até 2 MiB.');
      return;
    }
    this.avatarBusy.set(true);
    this.avatarAttempts = 0;
    this.avatarError.set('');
    this.avatarMessage.set('Enviando imagem…');
    this.requests.add(
      this.api.upload(file).subscribe({
        next: (request) => this.pollAvatar(request),
        error: (error: unknown) => this.avatarFailed(error),
      }),
    );
  }

  protected openAvatarPicker(): void {
    this.avatarPickerOpen.set(true);
    setTimeout(() => this.chooseAvatarButton()?.nativeElement.focus());
  }

  protected closeAvatarPicker(): void {
    this.avatarPickerOpen.set(false);
    setTimeout(() => this.avatarTrigger()?.nativeElement.focus());
  }

  protected chooseAvatar(): void {
    this.avatarFileInput()?.nativeElement.click();
  }

  private pollAvatar(request: AvatarRequest): void {
    if (request.status === 'completed') {
      this.avatarMessage.set('Carregando foto…');
      this.profile.update(
        (profile) => profile && { ...profile, avatarUrl: `/api/users/${profile.username}/avatar` },
      );
      this.loadAvatar(true);
      return;
    }
    if (request.status !== 'pending') {
      this.avatarBusy.set(false);
      this.avatarMessage.set('');
      this.avatarError.set(request.erro || 'Não foi possível processar a imagem.');
      return;
    }
    this.avatarMessage.set('Processando imagem…');
    if (++this.avatarAttempts > 60) {
      this.avatarBusy.set(false);
      this.avatarMessage.set(
        'O processamento está demorando. Reabra o perfil para conferir o avatar.',
      );
      return;
    }
    this.avatarPoll = setTimeout(() => {
      this.requests.add(
        this.api.avatarStatus(request.id).subscribe({
          next: (status) => this.pollAvatar(status),
          error: (error: unknown) => this.avatarFailed(error),
        }),
      );
    }, 1500);
  }

  private avatarFailed(error: unknown): void {
    this.avatarBusy.set(false);
    this.avatarMessage.set('');
    this.avatarError.set(apiError(error).message);
  }

  protected removeAvatar(): void {
    if (this.avatarBusy()) return;
    this.avatarRequest?.unsubscribe();
    this.avatarBusy.set(true);
    this.avatarError.set('');
    this.requests.add(
      this.api.removeAvatar().subscribe({
        next: () => {
          this.avatarUrl.set(null);
          this.profile.update((profile) => profile && { ...profile, avatarUrl: null });
          this.reviewAvatars.invalidate(this.currentUsername);
          this.avatarBusy.set(false);
          this.avatarMessage.set('Avatar removido.');
        },
        error: (error: unknown) => this.avatarFailed(error),
      }),
    );
  }

  protected async logout(): Promise<void> {
    if (this.loggingOut()) return;
    this.loggingOut.set(true);
    this.logoutError.set('');
    try {
      await this.auth.logout();
      await this.router.navigate(['/entrar']);
    } catch (error: unknown) {
      this.logoutError.set(apiError(error).message);
    } finally {
      this.loggingOut.set(false);
    }
  }
}
