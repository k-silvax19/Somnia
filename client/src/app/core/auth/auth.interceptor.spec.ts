import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';
import { environment } from '../../environments/environment';

describe('authInterceptor', () => {
  const session = { accessToken: 'private-token' };
  let http: HttpTestingController;
  let client: HttpClient;
  const auth = { ensureSession: vi.fn(), session: () => session, invalidate: vi.fn() };
  const router = { navigate: vi.fn() };

  beforeEach(() => {
    vi.clearAllMocks();
    auth.ensureSession.mockResolvedValue(session);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
  });
  afterEach(() => http.verify());

  it('adds bearer only after the session is ready', async () => {
    const url = `${environment.apiUrl}/me`;
    const response = firstValueFrom(client.get(url));
    http.expectNone(url);
    await Promise.resolve();
    const request = http.expectOne(url);
    expect(request.request.headers.get('Authorization')).toBe('Bearer private-token');
    expect(request.request.withCredentials).toBe(true);
    request.flush({});
    await response;
  });

  it.each([
    'https://outside.test/api/me',
    '//outside.test/api/me',
    '/assets/icon.svg',
    'https://localhost:7201/api/../outside',
    'https://localhost:7201/api/%2e%2e/outside',
    'https://localhost:7201/api/auth/login',
  ])('does not attach credentials to %s', async (url) => {
    const response = firstValueFrom(client.get(url));
    const request = http.expectOne(url);
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.withCredentials).toBe(false);
    expect(auth.ensureSession).not.toHaveBeenCalled();
    request.flush({});
    await response;
  });

  it('redirects an anonymous visitor without sending a protected request', async () => {
    auth.ensureSession.mockResolvedValue(null);
    const url = `${environment.apiUrl}/me`;
    await expect(firstValueFrom(client.get(url))).rejects.toMatchObject({ status: 401 });
    http.expectNone(url);
    expect(router.navigate).toHaveBeenCalledWith(['/entrar']);
  });

  it.each([401, 403, 503])('does not replay a failed mutation (%s)', async (status) => {
    const result = firstValueFrom(client.post(`${environment.apiUrl}/reviews`, { review: 'test' }));
    const rejection = expect(result).rejects.toBeInstanceOf(HttpErrorResponse);
    await Promise.resolve();
    http.expectOne(`${environment.apiUrl}/reviews`).flush({}, { status, statusText: 'Error' });
    await rejection;
    expect(auth.invalidate).toHaveBeenCalledTimes(status === 401 ? 1 : 0);
    expect(router.navigate).toHaveBeenCalledTimes(status === 401 ? 1 : 0);
    http.expectNone(`${environment.apiUrl}/auth/refresh`);
    http.expectNone(`${environment.apiUrl}/reviews`);
  });
});
