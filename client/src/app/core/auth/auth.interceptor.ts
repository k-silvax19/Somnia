import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  // Match the configured API exactly, including its path boundary. Usernames may contain dots.
  if (!request.url.startsWith(`${environment.apiUrl}/`)) return next(request);
  const path = request.url.slice(environment.apiUrl.length).split('?')[0];
  if (
    !/^\/(?:[a-zA-Z0-9._-]+\/)*[a-zA-Z0-9._-]+$/.test(path) ||
    path.split('/').some((segment) => segment === '.' || segment === '..') ||
    path.startsWith('/auth/')
  )
    return next(request);

  const auth = inject(AuthService);
  const router = inject(Router);
  return from(auth.ensureSession()).pipe(
    switchMap((session) => {
      if (!session) {
        void router.navigate(['/entrar']);
        return throwError(() => new HttpErrorResponse({ status: 401 }));
      }
      return next(
        request.clone({
          setHeaders: { Authorization: `Bearer ${session.accessToken}` },
          withCredentials: true,
        }),
      ).pipe(
        catchError((error: unknown) => {
          if (
            error instanceof HttpErrorResponse &&
            error.status === 401 &&
            auth.session()?.accessToken === session.accessToken
          ) {
            auth.invalidate();
            void router.navigate(['/entrar']);
          }
          // Do not replay mutations or loop on a rejected JWT/refresh.
          return throwError(() => error);
        }),
      );
    }),
  );
};
