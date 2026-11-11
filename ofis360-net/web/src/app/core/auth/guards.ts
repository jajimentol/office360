import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, defaultRouteForRole } from './auth.service';
import type { Role } from '../api/types';

/** (app)/layout.tsx: oturum yoksa /giris. */
export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const user = await auth.load();
  return user ? true : router.createUrlTree(['/giris']);
};

/** requireManagerPage / requireAdminPage: rol uymuyorsa /yetkisiz. */
export function roleGuard(...roles: Role[]): CanActivateFn {
  return async () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const user = await auth.load();
    if (!user) return router.createUrlTree(['/giris']);
    return roles.includes(user.role) ? true : router.createUrlTree(['/yetkisiz']);
  };
}

/** Admin rolü veya Sekreter yetkisi. */
export const adminOrSecretaryGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const user = await auth.load();
  if (!user) return router.createUrlTree(['/giris']);
  return user.role === 'ADMIN' || user.isSecretary ? true : router.createUrlTree(['/yetkisiz']);
};

/** giris/page.tsx: oturum varsa rolün varsayılan sayfasına yönlendir. */
export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const user = await auth.load();
  return user ? router.createUrlTree([defaultRouteForRole(user.role)]) : true;
};
