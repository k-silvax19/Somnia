import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  // Credentials go only to safe API paths, or to the configured local HTTPS API.
  const safeRelativeApiPath = /^\/api\/(?:[a-zA-Z0-9_-]+\/)*[a-zA-Z0-9_-]+(?:\?[^#]*)?$/;
  const safeLocalApiUrl =
    /^https:\/\/localhost:\d+\/api\/(?:[a-zA-Z0-9_-]+\/)*[a-zA-Z0-9_-]+(?:\?[^#]*)?$/;
  const relativeApiRequest = safeRelativeApiPath.test(request.url);
  const configuredLocalApiRequest =
    request.url.startsWith(`${environment.apiUrl}/`) && safeLocalApiUrl.test(request.url);
  if ((!relativeApiRequest && !configuredLocalApiRequest) || request.url.includes('/api/auth/'))
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
