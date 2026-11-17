import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { authGuard, guestGuard } from './auth.guards';
import { AuthService } from './auth.service';

@Component({ selector: 'test-private', template: 'private content' })
class PrivatePage {}
@Component({ selector: 'test-login', template: 'login' })
class LoginPage {}

describe('route guards', () => {
  const auth = { ensureSession: vi.fn() };
  beforeEach(() => {
    auth.ensureSession.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        provideRouter([
          { path: 'entrar', component: LoginPage, canActivate: [guestGuard] },
          { path: 'cadastro', component: LoginPage, canActivate: [guestGuard] },
          {
            path: '',
            canActivate: [authGuard],
            canActivateChild: [authGuard],
            children: [
              { path: 'inicio', component: PrivatePage },
              { path: 'protected-child', component: PrivatePage },
            ],
          },
        ]),
      ],
    });
  });

  it('blocks a direct private URL for an anonymous visitor', async () => {
    auth.ensureSession.mockResolvedValue(null);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/inicio', LoginPage);
    expect(TestBed.inject(Router).url).toBe('/entrar');
    expect(harness.routeNativeElement?.textContent ?? '').not.toContain('private');
  });

  it.each(['/entrar', '/cadastro'])('redirects a connected account away from %s', async (url) => {
    auth.ensureSession.mockResolvedValue({ accessToken: 'valid' });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, PrivatePage);
    expect(TestBed.inject(Router).url).toBe('/inicio');
  });

  it('waits for restoration before rendering private content', async () => {
    let restore!: (value: object) => void;
    auth.ensureSession.mockImplementation(
      () =>
        new Promise((resolve) => {
          restore = resolve;
        }),
    );
    const harness = await RouterTestingHarness.create();
    const navigation = harness.navigateByUrl('/inicio', PrivatePage);
    await vi.waitFor(() => expect(restore).toBeDefined());
    expect(harness.routeNativeElement?.textContent ?? '').not.toContain('private');
    auth.ensureSession.mockResolvedValue({ accessToken: 'valid' });
    restore({ accessToken: 'valid' });
    await navigation;
    expect(harness.routeNativeElement?.textContent).toContain('private');
  });

  it('rechecks child navigation after the session is lost', async () => {
    auth.ensureSession.mockResolvedValue({ accessToken: 'valid' });
    const harness = await RouterTestingHarness.create('/inicio');
    auth.ensureSession.mockResolvedValue(null);
    await harness.navigateByUrl('/protected-child', LoginPage);
    expect(TestBed.inject(Router).url).toBe('/entrar');
  });
});
