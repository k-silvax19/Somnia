import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { apiError } from './api-error';

describe('API error messages', () => {
  it('maps field names from ProblemDetails without discarding form data', () => {
    expect(
      apiError(
        new HttpErrorResponse({
          status: 409,
          error: { errors: { Email: ['O e-mail já está cadastrado.'] } },
        }),
      ).fields['email'],
    ).toBe('O e-mail já está cadastrado.');
  });
  it('uses Retry-After for throttling', () => {
    expect(
      apiError(
        new HttpErrorResponse({ status: 429, headers: new HttpHeaders({ 'Retry-After': '42' }) }),
      ).retryAfter,
    ).toBe(42);
  });
  it('does not expose backend diagnostics on unexpected failures', () => {
    expect(
      apiError(
        new HttpErrorResponse({
          status: 500,
          error: { detail: 'Internal connection string and stack trace' },
        }),
      ).message,
    ).not.toContain('connection string');
  });
});
