import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiClient, ApiError } from '../api/api-client';
import type { MeResponse, Role, SessionUser, UserDto } from '../api/types';
import { APP_CONFIG } from '../../lib/config';

/** Rolüne göre varsayılan başlangıç sayfası (kaynak: giris/page.tsx + auth.actions.ts). */
export function defaultRouteForRole(role: Role): string {
  if (role === 'ADMIN') return '/admin';
  if (role === 'MANAGER') return '/yonetim';
  return '/panel';
}

/**
 * Oturum durumu. Kaynakta sunucu bileşenleri her istekte çerezi çözüyordu; burada /api/auth/me
 * bir kez çağrılır ve sonucu sinyal olarak tüm uygulamaya dağıtılır.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);

  readonly user = signal<SessionUser | null>(null);
  readonly appName = signal<string>(APP_CONFIG.name);
  readonly loaded = signal(false);
  readonly demoMode = signal(false);
  readonly isAuthenticated = computed(() => this.user() !== null);
  private pending: Promise<SessionUser | null> | null = null;

  /** Oturumu sunucudan yükler; eş zamanlı çağrılar tek isteğe indirgenir. */
  load(force = false): Promise<SessionUser | null> {
    if (this.loaded() && !force) return Promise.resolve(this.user());
    if (this.pending) return this.pending;
    this.pending = this.api
      .get<MeResponse>('auth/me')
      .then((response) => {
        this.user.set(response.user);
        this.demoMode.set(response.demoMode);
        this.appName.set(response.appName || APP_CONFIG.name);
        return response.user;
      })
      .catch((error: unknown) => {
        if (error instanceof ApiError && error.status === 401) {
          this.user.set(null);
          return null;
        }
        throw error;
      })
      .finally(() => {
        this.loaded.set(true);
        this.pending = null;
      });
    return this.pending;
  }

  async login(userId: string): Promise<{ role: Role; defaultRoute: string }> {
    const result = await this.api.post<{ role: Role; defaultRoute: string }>('auth/login', { userId });
    await this.load(true);
    return result;
  }

  async logout(): Promise<void> {
    try {
      await this.api.post('auth/logout');
    } finally {
      this.user.set(null);
      await this.router.navigateByUrl('/giris');
    }
  }

  /** Profil tercihleri kaydedilince oturum özetini günceller (sunucuya ikinci istek atmadan). */
  applyUserUpdate(user: UserDto): void {
    const current = this.user();
    if (!current || current.id !== user.id) return;
    this.user.set({
      ...current,
      preferredFloorId: user.preferredFloorId,
      prefersQuietZone: user.prefersQuietZone,
      notificationsEnabled: user.notificationsEnabled,
    });
  }

  hasRole(...roles: Role[]): boolean {
    const user = this.user();
    return user !== null && roles.includes(user.role);
  }
}
