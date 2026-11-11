import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import type { RuleViolation } from './types';

/** Sunucunun hata zarfı: { error: { code, message, violations?, details? } } */
export class ApiError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly status: number,
    public readonly violations?: RuleViolation[],
    public readonly details?: unknown,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export type QueryParams = Record<string, string | number | boolean | null | undefined>;

/**
 * Tüm ekranların kullandığı ince HTTP istemcisi. Çerez tabanlı oturum aynı origin (4210 → /api proxy)
 * üzerinden taşınır; hata zarfı ApiError'a çevrilir.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  get<T>(path: string, params?: QueryParams): Promise<T> {
    return this.run(this.http.get<T>(`/api/${path}`, { params: toParams(params) }));
  }

  post<T>(path: string, body?: unknown): Promise<T> {
    return this.run(this.http.post<T>(`/api/${path}`, body ?? {}));
  }

  put<T>(path: string, body?: unknown): Promise<T> {
    return this.run(this.http.put<T>(`/api/${path}`, body ?? {}));
  }

  delete<T>(path: string): Promise<T> {
    return this.run(this.http.delete<T>(`/api/${path}`));
  }

  /** CSV gibi ikili yanıtlar için. */
  blob(path: string, params?: QueryParams): Promise<{ blob: Blob; fileName: string | null }> {
    return firstValueFrom(
      this.http.get(`/api/${path}`, { params: toParams(params), observe: 'response', responseType: 'blob' }),
    )
      .then((response) => {
        const disposition = response.headers.get('content-disposition') ?? '';
        const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
        return { blob: response.body ?? new Blob(), fileName: match ? decodeURIComponent(match[1]) : null };
      })
      .catch((error: unknown) => {
        throw toApiError(error);
      });
  }

  private run<T>(request: Observable<T>): Promise<T> {
    return firstValueFrom(request).catch((error: unknown) => {
      throw toApiError(error);
    });
  }
}

function toParams(params?: QueryParams): HttpParams | undefined {
  if (!params) return undefined;
  let result = new HttpParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === null || value === undefined || value === '') continue;
    result = result.set(key, String(value));
  }
  return result;
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { error?: { code?: string; message?: string; violations?: RuleViolation[]; details?: unknown } } | null;
    const envelope = body && typeof body === 'object' ? body.error : undefined;
    if (envelope?.code) {
      return new ApiError(envelope.code, envelope.message ?? 'İşlem tamamlanamadı.', error.status, envelope.violations, envelope.details);
    }
    if (error.status === 0) {
      return new ApiError('NETWORK_ERROR', 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.', 0);
    }
    return new ApiError('INTERNAL_ERROR', 'Beklenmeyen bir hata oluştu.', error.status);
  }
  return new ApiError('INTERNAL_ERROR', error instanceof Error ? error.message : 'Beklenmeyen bir hata oluştu.', 500);
}

export function errorMessage(error: unknown): string {
  return toApiError(error).message;
}
