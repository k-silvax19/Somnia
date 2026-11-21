import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { MusicItem, MusicReference, Review } from '../home/home.models';
import { ReviewStoryShare } from '../home/review-story-share';
import { MusicApi, musicError } from './music-api';

@Component({
  selector: 'app-review-editor',
  imports: [ReactiveFormsModule],
  templateUrl: './review-editor.html',
  styleUrl: './review-editor.scss',
})
export class ReviewEditor {
  readonly item = input.required<MusicReference>();
  readonly current = input<Review | null>(null);
  readonly music = input<MusicItem | null>(null);
  readonly refreshing = input(false);
  readonly changed = output<Review | null>();
  readonly busyChange = output<boolean>();
  readonly refresh = output<void>();
  private readonly api = inject(MusicApi);
  private readonly storyShare = inject(ReviewStoryShare);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly editing = signal(false);
  protected readonly busy = signal(false);
  protected readonly confirming = signal(false);
  protected readonly error = signal('');
  protected readonly fields = signal<Record<string, string>>({});
  protected readonly success = signal('');
  protected readonly sharing = signal(false);
  protected readonly preparedStory = signal<File | null>(null);
  protected readonly shareStatus = signal('');
  protected readonly shareError = signal('');
  protected readonly conflict = signal(false);
  private readonly now = signal(Date.now());
  private readonly blockedUntil = signal(0);
  protected readonly wait = computed(() =>
    Math.max(0, Math.ceil((this.blockedUntil() - this.now()) / 1000)),
  );
  private editingId: string | null = null;
  protected readonly form = new FormGroup(
    {
      score: new FormControl<number | null>(null, [
        Validators.min(0),
        Validators.max(100),
        (control) =>
          control.value === null || Number.isInteger(control.value) ? null : { integer: true },
      ]),
      review: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(5000)] }),
    },
    {
      validators: (form) =>
        form.value.score !== null || form.value.review?.trim() ? null : { empty: true },
    },
  );

  constructor() {
    const clock = setInterval(() => this.now.set(Date.now()), 1000);
    this.destroyRef.onDestroy(() => clearInterval(clock));
  }
  protected edit() {
    const current = this.current();
    this.editingId = current?.id ?? null;
    this.form.reset({ score: current?.score ?? null, review: current?.review ?? '' });
    this.editing.set(true);
    this.confirming.set(false);
    this.error.set('');
    this.fields.set({});
    this.success.set('');
  }
  protected cancel() {
    this.editing.set(false);
    this.editingId = null;
    this.form.reset({ score: null, review: '' });
    this.error.set('');
    this.fields.set({});
    this.conflict.set(false);
  }
  protected reload() {
    this.cancel();
    this.confirming.set(false);
    this.refresh.emit();
  }
  protected save() {
    if (this.busy() || this.refreshing() || this.wait() || this.conflict()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const content = {
      score: this.form.controls.score.value,
      review: this.form.controls.review.value.trim() || null,
    };
    this.run(
      this.editingId
        ? this.api.update(this.editingId, content)
        : this.api.create(this.item(), content),
      false,
    );
  }
  protected remove() {
    if (
      this.busy() ||
      this.refreshing() ||
      this.wait() ||
      this.conflict() ||
      !this.confirming() ||
      !this.current()
    )
      return;
    this.run(this.api.delete(this.current()!.id), true);
  }
  protected shareStory() {
    const review = this.current();
    if (!review || this.sharing()) return;
    this.sharing.set(true);
    this.shareStatus.set('');
    this.shareError.set('');
    const prepared = this.preparedStory();
    if (prepared) {
      this.storyShare.share(prepared, this.music()).then((result) => {
        if (result === 'shared') this.shareStatus.set('Imagem enviada ao compartilhador do dispositivo.');
        if (result === 'downloaded') this.shareStatus.set('Imagem baixada; abra o Instagram para publicar no Story.');
        if (result === 'cancelled') this.shareStatus.set('Compartilhamento cancelado.');
        if (result !== 'cancelled') this.preparedStory.set(null);
      }).catch(() => {
        this.shareError.set('Não foi possível compartilhar a imagem. Tente novamente.');
      }).finally(() => this.sharing.set(false));
      return;
    }

    this.storyShare.prepare(review, this.music()).then((file) => {
      this.preparedStory.set(file);
      this.shareStatus.set('Imagem pronta. Toque em “Abrir compartilhamento” para continuar.');
    }).catch(() => {
      this.shareError.set('Não foi possível gerar a imagem. Tente novamente.');
    }).finally(() => this.sharing.set(false));
  }
  private run(request: Observable<Review | void>, deleting: boolean) {
    this.busy.set(true);
    this.busyChange.emit(true);
    this.error.set('');
    this.fields.set({});
    this.success.set('');
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (review) => {
        this.busy.set(false);
        this.busyChange.emit(false);
        this.cancel();
        this.confirming.set(false);
        this.success.set(deleting ? 'Avaliação excluída.' : 'Avaliação salva.');
        this.changed.emit(review ?? null);
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.busyChange.emit(false);
        const problem = musicError(error, !this.editingId && !deleting);
        this.error.set(problem.message);
        this.fields.set(problem.fields);
        this.blockedUntil.set(Date.now() + problem.retryAfter * 1000);
        if (error instanceof HttpErrorResponse && [404, 409].includes(error.status)) {
          this.conflict.set(true);
          this.error.set(
            error.status === 409
              ? 'Você já avaliou este item. Carregue a avaliação existente antes de editar.'
              : 'O item ou a avaliação não está mais disponível. Atualize antes de tentar novamente.',
          );
        }
      },
    });
  }
}
