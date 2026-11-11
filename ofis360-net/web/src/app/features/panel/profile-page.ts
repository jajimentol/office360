import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { minutesToTime } from '../../lib/date';
import { ROLE_LABELS } from '../../lib/enums';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import { EmployeeContextService, type EmployeeContext } from '../../core/api/employee-context';
import type { UserDto } from '../../core/api/types';
import { AuthService } from '../../core/auth/auth.service';
import {
  PageState,
  ToastService,
  UiAlert,
  UiAvatar,
  UiBadge,
  UiButton,
  UiCard,
  UiCardBody,
  UiCardHeader,
  UiField,
  UiIcon,
  UiPageError,
  UiPageHeader,
  UiPageLoading,
  UiSelect,
  UiSwitch,
} from '../../shared/ui';

/** Kaynak: (app)/panel/profil/page.tsx + components/profile/preferences-form.tsx */
@Component({
  selector: 'app-profile-page',
  imports: [FormsModule, UiPageHeader, UiPageLoading, UiPageError, UiCard, UiCardHeader, UiCardBody, UiAvatar, UiBadge, UiAlert, UiIcon, UiField, UiSelect, UiSwitch, UiButton],
  template: `
    <ui-page-header title="Profil ve tercihler" description="Hesap bilgileriniz ve rezervasyon deneyiminizi kişiselleştiren ayarlar." />

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as ctx) {
      @let user = auth.user()!;
      @let settings = ctx.settings;
      <div class="grid gap-5 lg:grid-cols-[320px_minmax(0,1fr)]">
        <div class="space-y-5">
          <div ui-card>
            <ui-card-body>
              <div class="flex items-center gap-4">
                <ui-avatar [name]="user.fullName" size="lg" />
                <div class="min-w-0">
                  <p class="truncate text-lg font-semibold tracking-tight text-ink-900">{{ user.fullName }}</p>
                  <p class="truncate text-sm text-ink-500">{{ user.title }}</p>
                </div>
              </div>

              <dl class="mt-5 space-y-3 text-sm">
                <div class="flex items-center justify-between gap-3">
                  <dt class="text-ink-500">Departman</dt>
                  <dd class="font-medium text-ink-900">{{ user.departmentName }}</dd>
                </div>
                <div class="flex items-center justify-between gap-3">
                  <dt class="text-ink-500">Rol</dt>
                  <dd><span ui-badge tone="brand">{{ roleLabels[user.role] }}</span></dd>
                </div>
                <div class="flex items-center justify-between gap-3">
                  <dt class="flex items-center gap-1.5 text-ink-500">
                    <svg uiIcon name="Mail" class="h-3.5 w-3.5"></svg>
                    E-posta
                  </dt>
                  <dd class="truncate font-medium text-ink-900">{{ user.email }}</dd>
                </div>
                <div class="flex items-center justify-between gap-3">
                  <dt class="text-ink-500">Masa rezervasyonu</dt>
                  <dd class="font-medium text-ink-900">{{ ctx.deskReservationCount }}</dd>
                </div>
                <div class="flex items-center justify-between gap-3">
                  <dt class="text-ink-500">Toplantı rezervasyonu</dt>
                  <dd class="font-medium text-ink-900">{{ ctx.roomReservationCount }}</dd>
                </div>
              </dl>

              @if (user.accessibilityApproved) {
                <div class="mt-4 rounded-lg border border-violet-200 bg-violet-50 p-3">
                  <p class="flex items-center gap-1.5 text-[13px] font-semibold text-violet-800">
                    <svg uiIcon name="Accessibility" class="h-4 w-4"></svg>
                    Erişilebilirlik yetkiniz var
                  </p>
                  <p class="mt-1 text-xs leading-relaxed text-violet-900/70">Erişilebilirlik için ayrılmış masaları seçebilirsiniz.</p>
                </div>
              }
            </ui-card-body>
          </div>

          <div ui-card>
            <ui-card-header title="Geçerli rezervasyon kuralları" />
            <ui-card-body>
              <ul class="space-y-2.5 text-[13px] text-ink-600">
                <li class="flex justify-between gap-3">
                  <span>Haftalık masa limiti</span>
                  <span class="font-semibold text-ink-900">{{ settings.weeklyDeskLimit.enabled ? settings.weeklyDeskLimit.limit + ' rezervasyon' : 'Kapalı' }}</span>
                </li>
                <li class="flex justify-between gap-3">
                  <span>Önceden rezervasyon</span>
                  <span class="font-semibold text-ink-900">{{ settings.advanceBookingDays.days }} iş günü</span>
                </li>
                <li class="flex justify-between gap-3">
                  <span>Popüler masa ardışık limiti</span>
                  <span class="font-semibold text-ink-900">{{ settings.popularDeskStreakLimit.enabled ? settings.popularDeskStreakLimit.limit + ' gün' : 'Kapalı' }}</span>
                </li>
                <li class="flex justify-between gap-3">
                  <span>Popüler masa payı</span>
                  <span class="font-semibold text-ink-900">{{ settings.popularShareLimit.windowSize }} rezervasyonda {{ settings.popularShareLimit.maxPopular }}</span>
                </li>
                <li class="flex justify-between gap-3">
                  <span>Toplantı odası mesaisi</span>
                  <span class="font-semibold text-ink-900">{{ minutesToTime(settings.roomWorkingHours.startMinutes) }} - {{ minutesToTime(settings.roomWorkingHours.endMinutes) }}</span>
                </li>
              </ul>
            </ui-card-body>
          </div>
        </div>

        <div class="space-y-5">
          <div ui-card>
            <ui-card-header title="Tercihler" description="Bu ayarlar yalnızca sizin deneyiminizi etkiler." hasIcon>
              <svg uiIcon icon name="Settings2" class="h-4.5 w-4.5"></svg>
            </ui-card-header>
            <ui-card-body>
              <div class="space-y-5">
                <ui-field label="Varsayılan kat" hint="Masa rezervasyon ekranı açıldığında bu kat seçili gelir." htmlFor="preferred-floor">
                  <select ui-select id="preferred-floor" [ngModel]="preferredFloorId()" (ngModelChange)="preferredFloorId.set($event)">
                    <option value="">Tercih yok</option>
                    @for (floor of reservableFloors(); track floor.id) {
                      <option [value]="floor.id">{{ floor.name }}</option>
                    }
                  </select>
                </ui-field>

                <div class="flex items-start justify-between gap-4 rounded-xl border border-line p-4">
                  <div>
                    <p class="text-sm font-medium text-ink-900">Sessiz alan tercihi</p>
                    <p class="mt-0.5 text-[13px] text-ink-500">Sessiz bölgelerdeki masalar öneri listelerinde öne çıkarılır.</p>
                  </div>
                  <ui-switch [(checked)]="prefersQuietZone" label="Sessiz alan tercihi" />
                </div>

                <div class="flex items-start justify-between gap-4 rounded-xl border border-line p-4">
                  <div>
                    <p class="text-sm font-medium text-ink-900">Uygulama içi bildirimler</p>
                    <p class="mt-0.5 text-[13px] text-ink-500">Rezervasyon onayı, iptal ve kapatma bildirimleri gönderilsin.</p>
                  </div>
                  <ui-switch [(checked)]="notificationsEnabled" label="Uygulama içi bildirimler" />
                </div>

                <div class="flex justify-end">
                  <button ui-button type="button" (click)="save()" [loading]="pending()" [disabled]="!dirty()">
                    <svg uiIcon name="Save" class="h-4 w-4"></svg>
                    Tercihleri kaydet
                  </button>
                </div>
              </div>
            </ui-card-body>
          </div>

          @if (auth.demoMode()) {
            <div ui-card>
              <ui-card-header title="Hesap ve kimlik doğrulama" hasIcon>
                <svg uiIcon icon name="UserRound" class="h-4.5 w-4.5"></svg>
              </ui-card-header>
              <ui-card-body>
                <ui-alert tone="info" title="Demo ortamı">
                  Bu sürümde şifre doğrulaması yapılmaz; oturum, seçilen demo hesabının kimliğiyle açılır. Kurumsal kurulumda oturum katmanı şirket
                  kimlik sağlayıcısına (SSO / Microsoft Entra ID) bağlanacak şekilde tasarlanmıştır; uygulamanın geri kalanında değişiklik gerekmez.
                </ui-alert>
              </ui-card-body>
            </div>
          }
        </div>
      </div>
    }
  `,
})
export class ProfilePage {
  private readonly api = inject(ApiClient);
  private readonly contextService = inject(EmployeeContextService);
  private readonly toasts = inject(ToastService);
  readonly auth = inject(AuthService);
  readonly state = new PageState<EmployeeContext>();
  readonly roleLabels = ROLE_LABELS;
  readonly minutesToTime = minutesToTime;

  readonly preferredFloorId = signal(this.auth.user()?.preferredFloorId ?? '');
  readonly prefersQuietZone = signal(this.auth.user()?.prefersQuietZone ?? false);
  readonly notificationsEnabled = signal(this.auth.user()?.notificationsEnabled ?? true);
  readonly pending = signal(false);

  readonly reservableFloors = computed(() => (this.state.data()?.floors ?? []).filter((floor) => floor.reservable));
  readonly dirty = computed(() => {
    const user = this.auth.user();
    if (!user) return false;
    return (
      (user.preferredFloorId ?? '') !== this.preferredFloorId() ||
      user.prefersQuietZone !== this.prefersQuietZone() ||
      user.notificationsEnabled !== this.notificationsEnabled()
    );
  });

  constructor() {
    void this.load();
  }

  load(): Promise<unknown> {
    return this.state.load(() => this.contextService.load());
  }

  async save(): Promise<void> {
    this.pending.set(true);
    try {
      const result = await this.api.put<{ updated: boolean; user: UserDto }>('me/preferences', {
        preferredFloorId: this.preferredFloorId() || null,
        prefersQuietZone: this.prefersQuietZone(),
        notificationsEnabled: this.notificationsEnabled(),
      });
      this.auth.applyUserUpdate(result.user);
      this.toasts.toast({ tone: 'success', title: 'Tercihleriniz kaydedildi' });
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Tercihler kaydedilemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}
