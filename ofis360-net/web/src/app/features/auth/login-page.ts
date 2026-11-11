import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { APP_CONFIG } from '../../lib/config';
import { ROLE_LABELS, type Role } from '../../lib/enums';
import { cn } from '../../lib/utils';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import type { UserDto } from '../../core/api/types';
import { AuthService, defaultRouteForRole } from '../../core/auth/auth.service';
import { ToastService, UiBadge, UiButton, UiIcon, type IconName } from '../../shared/ui';

/** Kaynak: app/giris/page.tsx + demo-login-panel.tsx */

const HIGHLIGHTS: Array<{ icon: IconName; title: string; description: string }> = [
  {
    icon: 'CalendarCheck',
    title: 'Ofise gelmeden yerini ayır',
    description: 'Kat planı üzerinden masanı seç, toplantı odanı saat bazında rezerve et.',
  },
  {
    icon: 'Scale',
    title: 'Adil kullanım kuralları',
    description: 'Haftalık limitler, popüler masa sınırları ve erişilebilirlik koruması servis katmanında uygulanır.',
  },
  {
    icon: 'ChartNoAxesCombined',
    title: 'Veriye dayalı kapasite yönetimi',
    description: 'Kat ve gün bazında doluluk, no-show ve kullanım oranları anonim raporlarla izlenir.',
  },
];

const ROLE_ICON: Record<string, IconName> = {
  EMPLOYEE: 'UserRound',
  MANAGER: 'ChartNoAxesCombined',
  ADMIN: 'ShieldCheck',
};

const ROLE_DESCRIPTION: Record<string, string> = {
  EMPLOYEE: 'Masa ve toplantı odası rezervasyonu, kişisel kullanım özeti',
  MANAGER: 'Doluluk, no-show ve adil kullanım raporları (anonim)',
  ADMIN: 'Kat, masa, oda, kural, kullanıcı ve kapatma yönetimi',
};

@Component({
  selector: 'app-login-page',
  imports: [UiIcon, UiBadge, UiButton],
  template: `
    <main id="main-content" class="min-h-screen lg:grid lg:grid-cols-[1.05fr_1fr]">
      <!-- Tanıtım paneli -->
      <section class="relative hidden overflow-hidden bg-brand-900 px-10 py-14 text-white lg:flex lg:flex-col lg:justify-between xl:px-16">
        <div
          class="pointer-events-none absolute inset-0 opacity-[0.14]"
          aria-hidden="true"
          style="background-image: radial-gradient(circle at 20% 20%, #7fabdd 0, transparent 45%), radial-gradient(circle at 80% 70%, #0d9488 0, transparent 40%)"
        ></div>
        <div class="relative">
          <div class="flex items-center gap-3">
            <span class="flex h-11 w-11 items-center justify-center rounded-xl bg-white/10 ring-1 ring-white/20">
              <svg uiIcon name="Building2" class="h-6 w-6"></svg>
            </span>
            <div>
              <p class="text-lg font-semibold tracking-tight">{{ config.name }}</p>
              <p class="text-xs text-white/70">{{ config.tagline }}</p>
            </div>
          </div>

          <h1 class="mt-14 max-w-lg text-[34px] font-semibold leading-[1.15] tracking-tight text-balance xl:text-[40px]">
            Hibrit ofiste kimin nerede çalıştığı artık belirsiz değil.
          </h1>
          <p class="mt-4 max-w-md text-[15px] leading-relaxed text-white/75">
            Çalışanlar ofise gelmeden masasını ve toplantı odasını planlar; yönetim kat bazında doluluğu ve ortak
            alanların adil dağılımını tek ekrandan izler.
          </p>

          <ul class="mt-10 space-y-5">
            @for (item of highlights; track item.title) {
              <li class="flex gap-4">
                <span class="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-white/10 ring-1 ring-white/15">
                  <svg uiIcon [name]="item.icon" class="h-4.5 w-4.5"></svg>
                </span>
                <div>
                  <p class="text-sm font-semibold">{{ item.title }}</p>
                  <p class="mt-0.5 text-[13px] leading-relaxed text-white/70">{{ item.description }}</p>
                </div>
              </li>
            }
          </ul>
        </div>

        <dl class="relative mt-12 grid grid-cols-3 gap-4 border-t border-white/15 pt-6">
          <div>
            <dt class="text-xs text-white/60">Rezervasyona açık masa</dt>
            <dd class="mt-1 text-2xl font-semibold">{{ stats().deskCount }}</dd>
          </div>
          <div>
            <dt class="text-xs text-white/60">Toplantı odası</dt>
            <dd class="mt-1 text-2xl font-semibold">{{ stats().roomCount }}</dd>
          </div>
          <div>
            <dt class="text-xs text-white/60">Kat</dt>
            <dd class="mt-1 text-2xl font-semibold">{{ stats().floorCount }}</dd>
          </div>
        </dl>
      </section>

      <!-- Giriş paneli -->
      <section class="flex min-h-screen items-center justify-center px-5 py-10 sm:px-8">
        <div class="w-full max-w-md">
          <div class="mb-8 lg:hidden">
            <div class="flex items-center gap-3">
              <span class="flex h-11 w-11 items-center justify-center rounded-xl bg-brand-600 text-white">
                <svg uiIcon name="Building2" class="h-6 w-6"></svg>
              </span>
              <div>
                <p class="text-lg font-semibold tracking-tight text-ink-900">{{ config.name }}</p>
                <p class="text-xs text-ink-500">{{ config.tagline }}</p>
              </div>
            </div>
          </div>

          @if (authStatus() !== 'DEMO') {
            <h2 class="text-2xl font-semibold tracking-tight text-ink-900">Ofis360'a giriş</h2>
            @if (handoffError()) {
              <p class="mt-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800" role="alert">{{ handoffError() }}</p>
            } @else if (pending()) {
              <p class="mt-2 text-sm leading-relaxed text-ink-500" role="status">Giriş yapılıyor...</p>
            } @else if (authStatus() === 'CONFIG_ERROR') {
              <p class="mt-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900" role="alert">Kurumsal giriş henüz yapılandırılmadı. Lütfen sistem yöneticinize başvurun.</p>
            } @else {
              <p class="mt-2 text-sm leading-relaxed text-ink-500">Ofis360'a RayExpress uygulaması üzerinden giriş yapabilirsiniz.</p>
            }
          } @else {
          <h2 class="text-2xl font-semibold tracking-tight text-ink-900">Demo hesabıyla giriş</h2>
          <p class="mt-2 text-sm leading-relaxed text-ink-500">
            Bu sürüm tanıtım amaçlıdır; şifre gerekmez. Rolleri karşılaştırmak için farklı hesaplarla giriş yapabilirsiniz.
          </p>

          <div class="mt-7">
            <fieldset>
              <legend class="sr-only">Demo hesabı seçin</legend>
              <ul class="space-y-2.5">
                @for (account of accounts(); track account.id) {
                  @let isSelected = account.id === selectedId();
                  <li>
                    <label
                      [class]="cn('flex cursor-pointer items-start gap-3 rounded-xl border bg-white p-3.5 transition-all', isSelected ? 'border-brand-500 ring-2 ring-brand-100 shadow-sm' : 'border-line hover:border-brand-200 hover:bg-brand-50/40')"
                    >
                      <input type="radio" name="demo-account" [value]="account.id" [checked]="isSelected" (change)="selectedId.set(account.id)" class="sr-only" />
                      <span [class]="cn('mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg', isSelected ? 'bg-brand-600 text-white' : 'bg-brand-50 text-brand-600')">
                        <svg uiIcon [name]="roleIcon[account.role] ?? 'UserRound'" class="h-4.5 w-4.5"></svg>
                      </span>
                      <span class="min-w-0 flex-1">
                        <span class="flex flex-wrap items-center gap-2">
                          <span class="text-sm font-semibold text-ink-900">{{ account.fullName }}</span>
                          <span ui-badge [tone]="account.role === 'ADMIN' ? 'brand' : account.role === 'MANAGER' ? 'accent' : 'neutral'">
                            {{ roleLabels[account.role] }}
                          </span>
                          @if (account.accessibilityApproved) {
                            <span ui-badge tone="violet">
                              <svg uiIcon name="Accessibility" class="h-3 w-3"></svg>
                              Erişilebilirlik yetkili
                            </span>
                          }
                        </span>
                        <span class="mt-0.5 block text-[13px] text-ink-500">{{ account.title }} · {{ account.department?.name }}</span>
                        <span class="mt-1 block text-xs text-ink-400">{{ roleDescription[account.role] ?? '' }}</span>
                      </span>
                    </label>
                  </li>
                }
              </ul>
            </fieldset>

            <button ui-button type="button" class="mt-6" size="lg" block [loading]="pending()" [disabled]="!selectedId()" (click)="login()">
              {{ selected() ? selected()!.fullName + ' olarak devam et' : 'Devam et' }}
              <svg uiIcon name="ArrowRight" class="h-4 w-4"></svg>
            </button>
          </div>

          <p class="mt-8 text-xs leading-relaxed text-ink-400">
            Bu ekran yalnızca geliştirme ortamında görünür; canlıda giriş RayExpress üzerinden yapılır.
          </p>
          }
        </div>
      </section>
    </main>
  `,
})
export class LoginPage {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toasts = inject(ToastService);

  readonly config = APP_CONFIG;
  readonly highlights = HIGHLIGHTS;
  readonly roleIcon = ROLE_ICON;
  readonly roleDescription = ROLE_DESCRIPTION;
  readonly roleLabels = ROLE_LABELS;
  readonly cn = cn;

  readonly accounts = signal<UserDto[]>([]);
  readonly stats = signal({ deskCount: 0, roomCount: 0, floorCount: 0 });
  readonly selectedId = signal('');
  readonly pending = signal(false);
  readonly authStatus = signal<'DEMO' | 'READY' | 'CONFIG_ERROR' | null>(null);
  readonly handoffError = signal<string | null>(null);

  selected(): UserDto | undefined {
    return this.accounts().find((account) => account.id === this.selectedId());
  }

  constructor() {
    void this.api.get<{ status: 'DEMO' | 'READY' | 'CONFIG_ERROR' }>('auth/options').then((options) => {
      this.authStatus.set(options.status);
      if (options.status === 'DEMO') {
        void this.api.get<UserDto[]>('auth/demo-accounts').then((accounts) => {
          this.accounts.set(accounts);
          if (!this.selectedId() && accounts[0]) this.selectedId.set(accounts[0].id);
        });
        return;
      }
      const ticket = this.route.snapshot.queryParamMap.get('ticket');
      if (ticket) void this.handoff(ticket);
    });
    void this.api
      .get<{ deskCount: number; roomCount: number; floorCount: number }>('auth/intro')
      .then((stats) => this.stats.set(stats))
      .catch(() => undefined);
  }

  private async handoff(ticket: string): Promise<void> {
    this.pending.set(true);
    try {
      const result = await this.api.post<{ role: Role }>('auth/handoff', { ticket });
      await this.auth.load(true);
      await this.router.navigateByUrl(defaultRouteForRole(result.role));
    } catch (error: unknown) {
      this.handoffError.set(errorMessage(error));
    } finally {
      this.pending.set(false);
    }
  }

  async login(): Promise<void> {
    const id = this.selectedId();
    if (!id) return;
    this.pending.set(true);
    try {
      const result = await this.auth.login(id);
      this.toasts.toast({ tone: 'success', title: 'Giriş yapıldı', description: `${this.selected()?.fullName} olarak devam ediliyor.` });
      await this.router.navigateByUrl(defaultRouteForRole(result.role as Role));
    } catch (error: unknown) {
      this.pending.set(false);
      this.toasts.toast({ tone: 'error', title: 'Giriş yapılamadı', description: errorMessage(error) });
    }
  }
}
