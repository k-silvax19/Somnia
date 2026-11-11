import { provideHttpClient, HttpErrorResponse } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { Session } from './auth.models';

const token = (overrides: Partial<Session> = {}): Session => ({
  usuarioId: 'user-1',
  nome: 'Ana',
  email: 'ana@example.test',
  accessToken: 'memory-only',
  dataExpiracaoEmUtc: new Date(Date.now() + 900_000).toISOString(),
  refreshExpiraEmUtc: new Date(Date.now() + 86_400_000).toISOString(),
  csrfToken: 'csrf-next',
  ...overrides,
});

describe('AuthService (real HTTP contract)', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let cookie = '';

  beforeEach(() => {
    cookie = '';
    vi.spyOn(document, 'cookie', 'get').mockImplementation(() => cookie);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  async function login(session = token()) {
    const result = auth.login({ email: 'ana@example.test', password: 'secret' });
    http.expectOne('/api/auth/login').flush(session);
    await result;
  }

  it('does not refresh an anonymous visitor', async () => {
    expect(await auth.ensureSession()).toBeNull();
    http.expectNone('/api/auth/refresh');
  });

  it('registers without opening a session or storing credentials', async () => {
    const storage = vi.spyOn(Storage.prototype, 'setItem');
    const result = auth.register({
      displayName: 'Ana',
      username: 'ana',
      email: 'ana@example.test',
      password: 'Secret123!',
    });
    const request = http.expectOne('/api/auth/register');
    expect(request.request.headers.get('X-Somnia-Client')).toBe('web');
    expect(request.request.withCredentials).toBe(true);
    request.flush({ id: 'user-1' }, { status: 201, statusText: 'Created' });
    await result;
    expect(auth.session()).toBeNull();
    expect(storage).not.toHaveBeenCalled();
  });

  it('keeps the access token in memory and reuses a valid session', async () => {
    const storage = vi.spyOn(Storage.prototype, 'setItem');
    await login();
    expect((await auth.ensureSession())?.accessToken).toBe('memory-only');
    expect(storage).not.toHaveBeenCalled();
    http.expectNone('/api/auth/refresh');
  });

  it('shares a single refresh between concurrent callers and sends CSRF, not a refresh body', async () => {
    cookie = '__Host-somnia-csrf=csrf-current';
    const calls = [auth.ensureSession(), auth.ensureSession(), auth.ensureSession()];
    const request = http.expectOne('/api/auth/refresh');
    expect(request.request.body).toEqual({});
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.headers.get('X-CSRF-Token')).toBe('csrf-current');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush(token());
    const results = await Promise.all(calls);
    expect(results.every((session) => session === results[0])).toBe(true);
  });

  it('renews before expiry and uses the newest cookie', async () => {
    await login(token({ dataExpiracaoEmUtc: new Date(Date.now() + 5000).toISOString() }));
    cookie = '__Host-somnia-csrf=rotated-in-another-tab';
    const result = auth.ensureSession();
    const request = http.expectOne('/api/auth/refresh');
    expect(request.request.headers.get('X-CSRF-Token')).toBe('rotated-in-another-tab');
    request.flush(token({ accessToken: 'renewed' }));
    expect((await result)?.accessToken).toBe('renewed');
  });

  it.each([0, 401, 403, 429, 503])('does not retry refresh after failure %s', async (status) => {
    cookie = '__Host-somnia-csrf=csrf-current';
    const result = auth.ensureSession();
    const request = http.expectOne('/api/auth/refresh');
    if (status === 0) request.error(new ProgressEvent('error'));
    else request.flush({}, { status, statusText: 'Failed' });
    expect(await result).toBeNull();
    expect(await auth.ensureSession()).toBeNull();
    expect(auth.notice()).not.toBe('');
    http.expectNone('/api/auth/refresh');
  });

  it('does not resurrect a session invalidated while refresh was in flight', async () => {
    cookie = '__Host-somnia-csrf=csrf-current';
    const result = auth.ensureSession();
    auth.invalidate();
    http.expectOne('/api/auth/refresh').flush(token());
    expect(await result).toBeNull();
    expect(auth.session()).toBeNull();
  });

  it('sends bearer and CSRF on logout, clearing state only after success', async () => {
    await login();
    cookie = '__Host-somnia-csrf=csrf-current';
    const result = auth.logout();
    await Promise.resolve();
    const request = http.expectOne('/api/auth/logout');
    expect(auth.session()).not.toBeNull();
    expect(request.request.headers.get('Authorization')).toBe('Bearer memory-only');
    expect(request.request.headers.get('X-CSRF-Token')).toBe('csrf-current');
    expect(request.request.headers.get('X-Somnia-Client')).toBe('web');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await result;
    expect(auth.session()).toBeNull();
    expect(await auth.ensureSession()).toBeNull();
  });

  it('keeps session available for retry when logout fails', async () => {
    await login();
    cookie = '__Host-somnia-csrf=csrf-current';
    const result = auth.logout();
    const rejection = expect(result).rejects.toBeInstanceOf(HttpErrorResponse);
    await Promise.resolve();
    http.expectOne('/api/auth/logout').flush({}, { status: 503, statusText: 'Unavailable' });
    await rejection;
    expect(auth.session()).not.toBeNull();
  });
});
