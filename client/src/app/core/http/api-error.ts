import { HttpErrorResponse } from '@angular/common/http';

export interface ApiError {
  message: string;
  fields: Record<string, string>;
  retryAfter: number;
}

export function apiError(error: unknown): ApiError {
  const result: ApiError = {
    message: 'Não foi possível concluir. Tente novamente.',
    fields: {},
    retryAfter: 0,
  };
  if (!(error instanceof HttpErrorResponse)) return result;
  if (error.status === 0)
    result.message =
      'Não foi possível conectar ao Somnia. Verifique sua conexão e tente novamente.';
  else if (error.status === 401)
    result.message = 'E-mail ou senha incorretos, ou sessão expirada. Tente entrar novamente.';
  else if (error.status === 403)
    result.message =
      'Não foi possível validar a segurança da sessão. Recarregue a página e entre novamente.';
  else if (error.status === 429) {
    const header = error.headers.get('Retry-After');
    const seconds =
      header && /^\d+$/.test(header)
        ? Number(header)
        : header
          ? Math.ceil((Date.parse(header) - Date.now()) / 1000)
          : 60;
    result.retryAfter = Number.isFinite(seconds) ? Math.max(1, seconds) : 60;
    result.message = 'Muitas tentativas. Aguarde antes de tentar novamente.';
  } else if (error.status >= 500)
    result.message = 'O Somnia está indisponível no momento. Tente novamente em instantes.';
  else if (error.status === 400 || error.status === 409) {
    const body: unknown = error.error;
    if (body && typeof body === 'object') {
      const problem = body as { detail?: unknown; errors?: unknown };
      if (typeof problem.detail === 'string') result.message = problem.detail;
      if (problem.errors && typeof problem.errors === 'object') {
        for (const [key, values] of Object.entries(problem.errors)) {
          if (Array.isArray(values) && values.every((value) => typeof value === 'string')) {
            result.fields[key.toLowerCase()] = values.join(' ');
          }
        }
        if (Object.keys(result.fields).length)
          result.message = 'Confira os campos indicados e tente novamente.';
      }
    }
  }
  return result;
}
