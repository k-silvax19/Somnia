import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  // Only our relative API paths may receive credentials. No absolute URLs or traversal.
  if (
    !/^\/api\/(?:[a-zA-Z0-9_-]+\/)*[a-zA-Z0-9_-]+(?:\?[^#]*)?$/.test(request.url) ||
    request.url.startsWith('/api/auth/')
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
