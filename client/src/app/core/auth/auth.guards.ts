import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

const requireSession = async (returnUrl: string) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return (await auth.ensureSession())
    ? true
    : router.createUrlTree(['/entrar'], { queryParams: { returnUrl } });
};

export const authGuard: CanActivateFn = (_route, state) => requireSession(state.url);

export const authMatchGuard: CanMatchFn = (_route, segments) =>
  requireSession(`/${segments.map((segment) => segment.path).join('/')}`);

export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return (await auth.ensureSession()) ? router.createUrlTree(['/inicio']) : true;
};
