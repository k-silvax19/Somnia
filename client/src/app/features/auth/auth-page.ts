import { Component, DestroyRef, ElementRef, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { apiError } from '../../core/http/api-error';
import { Brand } from '../../shared/brand';

type Field = 'displayName' | 'username' | 'email' | 'password' | 'confirmation';

@Component({
  selector: 'app-auth-page',
  imports: [ReactiveFormsModule, RouterLink, Brand],
  templateUrl: './auth-page.html',
  styleUrl: './auth-page.scss',
})
export class AuthPage {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly registering = inject(ActivatedRoute).snapshot.data['register'] === true;
  private readonly navigationState = this.router.currentNavigation()?.extras.state;
  protected readonly registered = this.navigationState?.['registered'] === true;
  protected readonly pending = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly error = signal('');
  protected readonly serverFields = signal<Record<string, string>>({});
  protected readonly cooldown = signal(0);
  private cooldownTimer?: ReturnType<typeof setInterval>;
  protected readonly form = inject(FormBuilder).nonNullable.group(
    {
      displayName: [
        '',
        this.registering
          ? [Validators.required, Validators.maxLength(256), Validators.pattern(/\S/)]
          : [],
      ],
      username: [
        '',
        this.registering ? [Validators.required, Validators.pattern(/^[a-zA-Z0-9._-]{3,30}$/)] : [],
      ],
      email: [
        typeof this.navigationState?.['email'] === 'string' ? this.navigationState['email'] : '',
        [Validators.required, Validators.email],
      ],
      password: [
        '',
        this.registering
          ? [
              Validators.required,
              Validators.minLength(8),
              Validators.pattern(/^(?=[\s\S]*\d)(?=[\s\S]*[^a-zA-Z0-9])[\s\S]*$/),
            ]
          : [Validators.required],
      ],
      confirmation: ['', this.registering ? [Validators.required] : []],
    },
    {
      validators: this.registering
        ? (group) =>
            group.get('password')?.value === group.get('confirmation')?.value
              ? null
              : { passwordsDiffer: true }
        : null,
    },
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => clearInterval(this.cooldownTimer));
  }

  protected fieldError(field: Field): string {
    const server = this.serverFields()[field.toLowerCase()];
    if (server) return server;
    const control = this.form.controls[field];
    if (!control.touched) return '';
    if (control.hasError('required')) return 'Preencha este campo.';
    if (field === 'confirmation' && this.form.hasError('passwordsDiffer'))
      return 'As senhas precisam ser iguais.';
    if (!control.invalid) return '';
    switch (field) {
      case 'displayName':
        return 'Use um nome de até 256 caracteres.';
      case 'username':
        return 'Use de 3 a 30 letras sem acento, números, ponto, hífen ou sublinhado.';
      case 'email':
        return 'Informe um e-mail válido.';
      case 'password':
        return 'Use pelo menos 8 caracteres, um número e um caractere especial.';
      default:
        return '';
    }
  }

  protected clearServerError(field: Field): void {
    this.serverFields.update((fields) => ({ ...fields, [field.toLowerCase()]: '' }));
  }

  protected async submit(): Promise<void> {
    if (this.pending() || this.cooldown()) return;
    this.serverFields.set({});
    this.error.set('');
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.error.set('Confira os campos indicados para continuar.');
      this.focusFeedback();
      return;
    }
    this.pending.set(true);
    const value = this.form.getRawValue();
    try {
      if (this.registering) {
        await this.auth.register({
          displayName: value.displayName.trim(),
          username: value.username.trim(),
          email: value.email.trim(),
          password: value.password,
        });
        await this.router.navigate(['/entrar'], {
          state: { registered: true, email: value.email.trim() },
        });
      } else {
        await this.auth.login({ email: value.email.trim(), password: value.password });
        await this.router.navigate(['/inicio']);
      }
    } catch (error: unknown) {
      const problem = apiError(error);
      this.error.set(problem.message);
      this.serverFields.set(problem.fields);
      if (problem.retryAfter) this.startCooldown(problem.retryAfter);
      this.focusFeedback();
    } finally {
      this.pending.set(false);
    }
  }

  private focusFeedback(): void {
    // Wait for Angular to render the error and aria-invalid attributes.
    setTimeout(() =>
      this.element.nativeElement
        .querySelector<HTMLElement>('[aria-invalid="true"], [role="alert"]')
        ?.focus(),
    );
  }

  private startCooldown(seconds: number): void {
    clearInterval(this.cooldownTimer);
    const until = Date.now() + seconds * 1000;
    this.cooldown.set(seconds);
    this.cooldownTimer = setInterval(() => {
      this.cooldown.set(Math.max(0, Math.ceil((until - Date.now()) / 1000)));
      if (!this.cooldown()) clearInterval(this.cooldownTimer);
    }, 1000);
  }
}
