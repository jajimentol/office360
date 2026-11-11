import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface HealthResponse {
  status: 'ok' | 'migrations-pending' | 'unavailable';
  database: string;
  connected: boolean;
  schemaVersion?: string | null;
  appliedMigrations?: number;
  pendingMigrations?: string[];
  serverTimeUtc?: string;
}

@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly http = inject(HttpClient);

  get(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>(`${environment.apiBase}/health`);
  }
}
