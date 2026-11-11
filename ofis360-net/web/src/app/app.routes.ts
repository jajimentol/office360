import { Routes } from '@angular/router';
import { adminOrSecretaryGuard, authGuard, guestGuard, roleGuard } from './core/auth/guards';

/**
 * Route tablosu kaynak Next.js yollarıyla aynıdır (bkz. docs/TASIMA-HARITASI.md §9).
 * (app)/layout.tsx → AppShell (authGuard); requireManagerPage → roleGuard(MANAGER, ADMIN); requireAdminPage → roleGuard(ADMIN).
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'giris' },
  {
    path: 'giris',
    canActivate: [guestGuard],
    title: 'Giriş · Ofis360',
    loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/app-shell').then((m) => m.AppShell),
    children: [
      { path: 'yetkisiz', title: 'Yetkisiz erişim · Ofis360', loadComponent: () => import('./features/misc/unauthorized-page').then((m) => m.UnauthorizedPage) },
      { path: 'panel', title: 'Ana sayfa · Ofis360', loadComponent: () => import('./features/panel/home-page').then((m) => m.HomePage) },
      { path: 'panel/masa', title: 'Masa rezervasyonu · Ofis360', loadComponent: () => import('./features/panel/desk-page').then((m) => m.DeskPage) },
      { path: 'panel/toplanti', title: 'Toplantı odası rezervasyonu · Ofis360', loadComponent: () => import('./features/panel/room-page').then((m) => m.RoomPage) },
      { path: 'panel/rezervasyonlarim', title: 'Rezervasyonlarım · Ofis360', loadComponent: () => import('./features/panel/my-reservations-page').then((m) => m.MyReservationsPage) },
      { path: 'panel/tum-rezervasyonlar', title: 'Tüm rezervasyonlar · Ofis360', canActivate: [adminOrSecretaryGuard], loadComponent: () => import('./features/admin/reservations-page').then((m) => m.ReservationsPage) },
      { path: 'panel/kullanimim', title: 'Kullanım özetim · Ofis360', loadComponent: () => import('./features/panel/usage-page').then((m) => m.UsagePage) },
      { path: 'panel/bildirimler', title: 'Bildirim merkezi · Ofis360', loadComponent: () => import('./features/panel/notifications-page').then((m) => m.NotificationsPage) },
      { path: 'panel/profil', title: 'Profil ve tercihler · Ofis360', loadComponent: () => import('./features/panel/profile-page').then((m) => m.ProfilePage) },
      {
        path: 'yonetim',
        canActivate: [roleGuard('MANAGER', 'ADMIN')],
        title: 'Ofis analitiği · Ofis360',
        loadComponent: () => import('./features/manager/analytics-page').then((m) => m.AnalyticsPage),
      },
      {
        path: 'admin',
        canActivate: [roleGuard('ADMIN')],
        children: [
          { path: '', pathMatch: 'full', title: 'Admin panosu · Ofis360', loadComponent: () => import('./features/admin/admin-home-page').then((m) => m.AdminHomePage) },
          { path: 'katlar', title: 'Kat ve alan yönetimi · Ofis360', loadComponent: () => import('./features/admin/floors-page').then((m) => m.FloorsPage) },
          { path: 'masalar', title: 'Masa yönetimi · Ofis360', loadComponent: () => import('./features/admin/desks-page').then((m) => m.DesksPage) },
          { path: 'odalar', title: 'Toplantı odaları · Ofis360', loadComponent: () => import('./features/admin/rooms-page').then((m) => m.RoomsPage) },
          { path: 'rezervasyonlar', title: 'Rezervasyon yönetimi · Ofis360', loadComponent: () => import('./features/admin/reservations-page').then((m) => m.ReservationsPage) },
          { path: 'kullanicilar', title: 'Kullanıcı ve roller · Ofis360', loadComponent: () => import('./features/admin/users-page').then((m) => m.UsersPage) },
          { path: 'kurallar', title: 'Rezervasyon kuralları · Ofis360', loadComponent: () => import('./features/admin/rules-page').then((m) => m.RulesPage) },
          { path: 'katilim', title: 'Katılım raporu · Ofis360', loadComponent: () => import('./features/admin/attendance-page').then((m) => m.AttendancePage) },
          { path: 'kapatmalar', title: 'Bakım ve kapatmalar · Ofis360', loadComponent: () => import('./features/admin/closures-page').then((m) => m.ClosuresPage) },
          { path: 'audit', title: 'Denetim kayıtları · Ofis360', loadComponent: () => import('./features/admin/audit-page').then((m) => m.AuditPage) },
          { path: 'ayarlar', title: 'Sistem ayarları · Ofis360', loadComponent: () => import('./features/admin/settings-page').then((m) => m.SettingsPage) },
        ],
      },
    ],
  },
  { path: '**', title: 'Sayfa bulunamadı · Ofis360', loadComponent: () => import('./features/misc/not-found-page').then((m) => m.NotFoundPage) },
];
