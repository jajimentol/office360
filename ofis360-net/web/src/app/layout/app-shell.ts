import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { APP_CONFIG } from '../lib/config';
import { ROLE_LABELS, type Role } from '../lib/enums';
import { cn } from '../lib/utils';
import { AuthService } from '../core/auth/auth.service';
import { UiAvatar, UiBadge, UiIcon, type IconName } from '../shared/ui';
import { ShellService } from './shell.service';

interface NavItem {
  href: string;
  label: string;
  icon: IconName;
  badge?: boolean;
  exact?: boolean;
}

interface NavGroup {
  title: string;
  items: NavItem[];
}

/** Kaynak: components/layout/app-shell.tsx buildNavigation. */
function buildNavigation(role: Role, isSecretary: boolean): NavGroup[] {
  const employee: NavGroup = {
    title: 'Çalışan',
    items: [
      { href: '/panel', label: 'Ana sayfa', icon: 'Home', exact: true },
      { href: '/panel/masa', label: 'Masa rezervasyonu', icon: 'Grid2x2' },
      { href: '/panel/toplanti', label: 'Toplantı odası', icon: 'DoorOpen' },
      { href: '/panel/rezervasyonlarim', label: 'Rezervasyonlarım', icon: 'CalendarDays' },
      { href: '/panel/kullanimim', label: 'Kullanım özetim', icon: 'Sparkles' },
      { href: '/panel/bildirimler', label: 'Bildirimler', icon: 'Bell', badge: true },
      { href: '/panel/profil', label: 'Profil ve tercihler', icon: 'UserRound' },
    ],
  };

  if (isSecretary) {
    employee.items.splice(4, 0, { href: '/panel/tum-rezervasyonlar', label: 'Tüm rezervasyonlar', icon: 'ClipboardList' });
  }

  const groups: NavGroup[] = [employee];

  if (role === 'MANAGER' || role === 'ADMIN') {
    groups.push({
      title: 'Yönetici',
      items: [{ href: '/yonetim', label: 'Ofis analitiği', icon: 'ChartNoAxesCombined', exact: true }],
    });
  }

  if (role === 'ADMIN') {
    groups.push({
      title: 'Yönetim (Admin)',
      items: [
        { href: '/admin', label: 'Admin panosu', icon: 'LayoutGrid', exact: true },
        { href: '/admin/katlar', label: 'Kat ve alan yönetimi', icon: 'Building2' },
        { href: '/admin/masalar', label: 'Masa yönetimi', icon: 'Grid2x2' },
        { href: '/admin/odalar', label: 'Toplantı odaları', icon: 'DoorOpen' },
        { href: '/admin/rezervasyonlar', label: 'Rezervasyon yönetimi', icon: 'ClipboardList' },
        { href: '/admin/kullanicilar', label: 'Kullanıcı ve roller', icon: 'Users' },
        { href: '/admin/kurallar', label: 'Rezervasyon kuralları', icon: 'Scale' },
        { href: '/admin/katilim', label: 'Katılım raporu', icon: 'ChartNoAxesCombined' },
        { href: '/admin/kapatmalar', label: 'Bakım ve kapatmalar', icon: 'Wrench' },
        { href: '/admin/audit', label: 'Denetim kayıtları', icon: 'FileClock' },
        { href: '/admin/ayarlar', label: 'Sistem ayarları', icon: 'Settings' },
      ],
    });
  }

  return groups;
}

function isActive(pathname: string, item: NavItem): boolean {
  if (item.exact) return pathname === item.href;
  return pathname === item.href || pathname.startsWith(`${item.href}/`);
}

@Component({
  selector: 'app-shell',
  imports: [NgTemplateOutlet, RouterOutlet, RouterLink, UiIcon, UiAvatar, UiBadge],
  template: `
    @let u = user();
    <div class="flex min-h-screen">
      <!-- Masaüstü kenar çubuğu -->
      <aside class="fixed inset-y-0 left-0 z-30 hidden w-64 flex-col border-r border-line bg-white lg:flex">
        <ng-container *ngTemplateOutlet="brand" />
        <ng-container *ngTemplateOutlet="navigation" />
        @if (demoMode()) {
        <div class="border-t border-line p-3">
          <div class="rounded-lg bg-brand-50 p-3">
            <p class="text-[11px] font-semibold uppercase tracking-wide text-brand-700">Demo ortamı</p>
            <p class="mt-1 text-[12px] leading-relaxed text-brand-900/70">
              Veriler örnek amaçlıdır. Rol değiştirmek için oturumu kapatıp başka bir demo hesabı seçebilirsiniz.
            </p>
          </div>
        </div>
        }
      </aside>

      <!-- Mobil çekmece -->
      @if (mobileOpen()) {
        <div class="fixed inset-0 z-50 lg:hidden">
          <div class="absolute inset-0 bg-ink-900/40 animate-fade" (click)="mobileOpen.set(false)" aria-hidden="true"></div>
          <div class="relative flex h-full w-72 max-w-[85vw] flex-col bg-white shadow-2xl">
            <div class="flex items-center justify-between border-b border-line pr-2">
              <ng-container *ngTemplateOutlet="brand" />
              <button type="button" (click)="mobileOpen.set(false)" aria-label="Menüyü kapat" class="rounded-lg p-2 text-ink-500 hover:bg-slate-100">
                <svg uiIcon name="X" class="h-5 w-5"></svg>
              </button>
            </div>
            <ng-container *ngTemplateOutlet="navigation" />
          </div>
        </div>
      }

      <div class="flex min-w-0 flex-1 flex-col lg:pl-64">
        <header class="sticky top-0 z-20 border-b border-line bg-white/85 backdrop-blur-md">
          <div class="flex h-16 items-center gap-3 px-4 sm:px-6">
            <button type="button" (click)="mobileOpen.set(true)" aria-label="Menüyü aç" class="rounded-lg p-2 text-ink-700 hover:bg-slate-100 lg:hidden">
              <svg uiIcon name="Menu" class="h-5 w-5"></svg>
            </button>

            <div class="min-w-0 flex-1 lg:hidden">
              <p class="truncate text-sm font-semibold text-ink-900">{{ appName() }}</p>
            </div>

            <div class="ml-auto flex items-center gap-2">
              <a
                routerLink="/panel/bildirimler"
                class="relative rounded-lg p-2 text-ink-700 transition-colors hover:bg-slate-100"
                [attr.aria-label]="'Bildirimler' + (unreadCount() > 0 ? ', ' + unreadCount() + ' okunmamış' : '')"
              >
                <svg uiIcon name="Bell" class="h-5 w-5"></svg>
                @if (unreadCount() > 0) {
                  <span class="absolute right-1 top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-600 px-1 text-[10px] font-semibold text-white">
                    {{ unreadCount() > 9 ? '9+' : unreadCount() }}
                  </span>
                }
              </a>

              @if (u) {
                <div class="relative">
                  <button
                    type="button"
                    (click)="userMenuOpen.set(!userMenuOpen())"
                    [attr.aria-expanded]="userMenuOpen()"
                    aria-haspopup="menu"
                    aria-label="Kullanıcı menüsü"
                    class="flex items-center gap-2 rounded-lg px-2 py-1.5 transition-colors hover:bg-slate-100"
                  >
                    <ui-avatar [name]="u.fullName" size="sm" />
                    <span class="hidden text-left sm:block">
                      <span class="block text-[13px] font-semibold leading-tight text-ink-900">{{ u.fullName }}</span>
                      <span class="block text-[11px] leading-tight text-ink-500">{{ roleLabels[u.role] }}</span>
                    </span>
                    <svg uiIcon name="ChevronDown" class="h-4 w-4 text-ink-400"></svg>
                  </button>

                  @if (userMenuOpen()) {
                    <div role="menu" class="absolute right-0 top-full z-30 mt-1.5 w-64 overflow-hidden rounded-xl border border-line bg-white shadow-lg animate-in-up">
                      <div class="border-b border-line px-4 py-3">
                        <p class="text-sm font-semibold text-ink-900">{{ u.fullName }}</p>
                        <p class="mt-0.5 text-xs text-ink-500">{{ u.title }} · {{ u.departmentName }}</p>
                        <div class="mt-2 flex flex-wrap gap-1.5">
                          <span ui-badge tone="brand">{{ roleLabels[u.role] }}</span>
                          @if (u.accessibilityApproved) {
                            <span ui-badge tone="violet">Erişilebilirlik yetkili</span>
                          }
                        </div>
                      </div>
                      <a routerLink="/panel/profil" role="menuitem" class="flex items-center gap-2.5 px-4 py-2.5 text-sm text-ink-700 hover:bg-slate-50">
                        <svg uiIcon name="UserRound" class="h-4 w-4 text-ink-400"></svg>
                        Profil ve tercihler
                      </a>
                      @if (u.role === 'ADMIN') {
                        <a routerLink="/admin" role="menuitem" class="flex items-center gap-2.5 px-4 py-2.5 text-sm text-ink-700 hover:bg-slate-50">
                          <svg uiIcon name="ShieldCheck" class="h-4 w-4 text-ink-400"></svg>
                          Admin panosu
                        </a>
                      }
                      <div class="border-t border-line">
                        <button type="button" role="menuitem" (click)="logout()" class="flex w-full items-center gap-2.5 px-4 py-2.5 text-sm text-red-600 hover:bg-red-50">
                          <svg uiIcon name="LogOut" class="h-4 w-4"></svg>
                          Oturumu kapat
                        </button>
                      </div>
                    </div>
                  }
                </div>
              }
            </div>
          </div>
        </header>

        <main id="main-content" class="flex-1 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <div class="mx-auto w-full max-w-[1400px]"><router-outlet /></div>
        </main>

        <footer class="border-t border-line px-4 py-4 text-center text-xs text-ink-400 sm:px-6">
          {{ appName() }} · {{ config.tagline }} · Sürüm {{ config.version }}
        </footer>
      </div>
    </div>

    <ng-template #brand>
      <a routerLink="/" class="flex items-center gap-2.5 px-4 py-4">
        <span class="flex h-9 w-9 items-center justify-center rounded-lg bg-brand-600 text-white">
          <svg uiIcon name="Building2" class="h-5 w-5"></svg>
        </span>
        <span class="min-w-0">
          <span class="block truncate text-[15px] font-semibold tracking-tight text-ink-900">{{ appName() }}</span>
          <span class="block truncate text-[11px] text-ink-500">Rezervasyon sistemi</span>
        </span>
      </a>
    </ng-template>

    <ng-template #navigation>
      <nav class="flex-1 space-y-6 overflow-y-auto px-3 py-4 scrollbar-thin" aria-label="Ana gezinme">
        @for (group of groups(); track group.title) {
          <div>
            <p class="px-3 pb-2 text-[11px] font-semibold uppercase tracking-wider text-ink-500">{{ group.title }}</p>
            <ul class="space-y-0.5">
              @for (item of group.items; track item.href) {
                @let active = isActive(pathname(), item);
                <li>
                  <a
                    [routerLink]="item.href"
                    [attr.aria-current]="active ? 'page' : null"
                    [class]="cn('flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors', active ? 'bg-brand-50 font-semibold text-brand-700' : 'text-ink-700 hover:bg-slate-100')"
                  >
                    <svg uiIcon [name]="item.icon" [class]="cn('h-4.5 w-4.5 shrink-0', active ? 'text-brand-600' : 'text-ink-400')"></svg>
                    <span class="min-w-0 flex-1 truncate">{{ item.label }}</span>
                    @if (item.badge && unreadCount() > 0) {
                      <span class="flex h-5 min-w-5 items-center justify-center rounded-full bg-brand-600 px-1.5 text-[11px] font-semibold text-white">
                        {{ unreadCount() > 99 ? '99+' : unreadCount() }}
                      </span>
                    }
                  </a>
                </li>
              }
            </ul>
          </div>
        }
      </nav>
    </ng-template>
  `,
})
export class AppShell {
  private readonly auth = inject(AuthService);
  private readonly shell = inject(ShellService);
  private readonly router = inject(Router);

  readonly config = APP_CONFIG;
  readonly roleLabels = ROLE_LABELS;
  readonly cn = cn;
  readonly isActive = isActive;

  readonly user = this.auth.user;
  readonly appName = this.auth.appName;
  readonly unreadCount = this.shell.unreadCount;
  readonly mobileOpen = signal(false);
  readonly userMenuOpen = signal(false);
  readonly demoMode = inject(AuthService).demoMode;

  readonly pathname = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects.split('?')[0]),
    ),
    { initialValue: this.router.url.split('?')[0] },
  );

  readonly groups = computed(() => {
    const user = this.user();
    return user ? buildNavigation(user.role, !!user.isSecretary) : [];
  });

  constructor() {
    // Rota değişince menüler kapanır ve rozet sayacı yenilenir (kaynak: layout her istekte yeniden hesaplıyordu).
    effect(() => {
      this.pathname();
      this.mobileOpen.set(false);
      this.userMenuOpen.set(false);
      void this.shell.refreshUnread();
    });
  }

  async logout(): Promise<void> {
    this.userMenuOpen.set(false);
    await this.auth.logout();
  }
}
