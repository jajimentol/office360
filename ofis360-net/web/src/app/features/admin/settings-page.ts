import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { APP_CONFIG } from '../../lib/config';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import type { SettingDto } from '../../core/api/types';
import { AuthService } from '../../core/auth/auth.service';
import { PageState, ToastService, UiAlert, UiButton, UiCard, UiCardBody, UiCardHeader, UiIcon, UiInput, UiPageError, UiPageHeader, UiPageLoading, UiSelect, UiTextarea } from '../../shared/ui';

/** Kaynak: (app)/admin/ayarlar/page.tsx + components/admin/settings-form.tsx */

const CHANNEL_LABELS: Record<string, string> = {
  IN_APP: 'Uygulama içi bildirim',
  EMAIL: 'E-posta bildirimi',
  TEAMS: 'Microsoft Teams bildirimi',
};

const GROUP_LABELS: Record<string, string> = {
  GENERAL: 'Genel',
  OFFICE: 'Ofis ve mesai',
};

@Component({
  selector: 'app-setting-row',
  imports: [FormsModule, UiButton, UiIcon, UiInput, UiSelect, UiTextarea],
  template: `
    @let s = setting();
    <div class="rounded-xl border border-line bg-white p-4">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div class="min-w-0 flex-1">
          <label [for]="'setting-' + s.key" class="text-sm font-semibold text-ink-900">{{ s.label }}</label>
          <p class="mt-1 max-w-2xl text-[13px] leading-relaxed text-ink-500">{{ s.description }}</p>
          <p class="mt-1 font-mono text-[11px] text-ink-400">{{ s.key }}</p>
        </div>

        <div class="flex w-full shrink-0 items-end gap-2 lg:w-[420px]">
          <div class="flex-1">
            @if (isLongText()) {
              <textarea ui-textarea [id]="'setting-' + s.key" rows="3" [ngModel]="value()" (ngModelChange)="value.set($event)"></textarea>
            } @else if (s.type === 'BOOLEAN') {
              <select ui-select [id]="'setting-' + s.key" [ngModel]="value()" (ngModelChange)="value.set($event)">
                <option value="false">Kapalı</option>
                <option value="true">Açık</option>
              </select>
            } @else {
              <input ui-input [id]="'setting-' + s.key" [type]="s.type === 'TIME' ? 'time' : s.type === 'NUMBER' ? 'number' : 'text'" [ngModel]="value()" (ngModelChange)="value.set($event)" />
            }
          </div>
          <button ui-button type="button" size="sm" (click)="save()" [loading]="pending()" [disabled]="!dirty()">
            <svg uiIcon name="Save" class="h-3.5 w-3.5"></svg>
            Kaydet
          </button>
        </div>
      </div>
    </div>
  `,
})
export class SettingRow {
  readonly setting = input.required<SettingDto>();
  readonly saved = output<void>();
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly value = signal('');
  readonly pending = signal(false);

  readonly isLongText = computed(() => this.setting().key.includes('announcement'));
  readonly dirty = computed(() => this.value() !== this.setting().value);

  constructor() {
    // Girdi yenilendiğinde (kaydettikten sonra) yerel değer kaynağa eşitlenir.
    effect(() => {
      const current = this.setting().value;
      untracked(() => this.value.set(current));
    });
  }

  async save(): Promise<void> {
    const setting = this.setting();
    this.pending.set(true);
    try {
      await this.api.put(`admin/settings/${setting.key}`, { value: this.value() });
      this.toasts.toast({ tone: 'success', title: 'Ayar güncellendi', description: setting.label });
      this.saved.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Ayar kaydedilemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}

@Component({
  selector: 'app-settings-page',
  imports: [UiPageHeader, UiPageLoading, UiPageError, UiCard, UiCardHeader, UiCardBody, UiAlert, UiIcon, SettingRow],
  template: `
    <ui-page-header title="Sistem ayarları" description="Marka adı, mesai saatleri, duyurular ve iletişim bilgileri." />

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as d) {
      <div class="grid gap-5 lg:grid-cols-[minmax(0,1fr)_320px]">
        <div ui-card>
          <ui-card-header title="Ayarlar" hasIcon>
            <svg uiIcon icon name="Settings" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body>
            <div class="space-y-6">
              @for (group of grouped(); track group.group) {
                <section>
                  <h2 class="mb-3 text-base font-semibold text-ink-900">{{ groupLabels[group.group] ?? group.group }}</h2>
                  <ul class="space-y-3">
                    @for (setting of group.settings; track setting.key) {
                      <li><app-setting-row [setting]="setting" (saved)="afterSave()" /></li>
                    }
                  </ul>
                </section>
              }
            </div>
          </ui-card-body>
        </div>

        <div class="space-y-5">
          <div ui-card class="h-fit">
            <ui-card-header title="Bildirim kanalları" />
            <ui-card-body>
              <ul class="space-y-2.5">
                @for (channel of d.channels; track channel.key) {
                  <li class="flex items-center justify-between rounded-lg border border-line px-3 py-2.5">
                    <span class="text-sm text-ink-700">{{ channelLabels[channel.key] ?? channel.key }}</span>
                    <span [class]="channel.enabled ? 'rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700' : 'rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-ink-500'">
                      {{ channel.enabled ? 'Etkin' : 'Hazır (pasif)' }}
                    </span>
                  </li>
                }
              </ul>
              <p class="mt-3 text-xs leading-relaxed text-ink-400">E-posta ve Teams kanalları servis katmanında tanımlıdır; kurumsal entegrasyon yapıldığında etkinleştirilmeleri yeterlidir.</p>
            </ui-card-body>
          </div>

          <div ui-card class="h-fit">
            <ui-card-header title="Uygulama bilgisi" />
            <ui-card-body>
              <dl class="space-y-2.5 text-sm">
                <div class="flex justify-between gap-3">
                  <dt class="text-ink-500">Marka adı</dt>
                  <dd class="font-medium text-ink-900">{{ config.name }}</dd>
                </div>
                <div class="flex justify-between gap-3">
                  <dt class="text-ink-500">Sürüm</dt>
                  <dd class="font-medium text-ink-900">{{ config.version }}</dd>
                </div>
                <div class="flex justify-between gap-3">
                  <dt class="text-ink-500">Saat dilimi</dt>
                  <dd class="font-medium text-ink-900">{{ config.timeZoneLabel }}</dd>
                </div>
              </dl>
              <ui-alert tone="info" class="mt-4">
                Marka adı merkezi yapılandırmadan yönetilir. Buradan güncellendiğinde arayüzdeki başlıklar ve bildirim metinleri otomatik olarak değişir.
              </ui-alert>
            </ui-card-body>
          </div>
        </div>
      </div>
    }
  `,
})
export class SettingsPage {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthService);
  readonly state = new PageState<{ settings: SettingDto[]; channels: Array<{ key: string; enabled: boolean }> }>();
  readonly config = APP_CONFIG;
  readonly channelLabels = CHANNEL_LABELS;
  readonly groupLabels = GROUP_LABELS;

  readonly grouped = computed(() => {
    const groups = new Map<string, SettingDto[]>();
    for (const setting of this.state.data()?.settings ?? []) {
      const bucket = groups.get(setting.group);
      if (bucket) bucket.push(setting);
      else groups.set(setting.group, [setting]);
    }
    return [...groups.entries()].map(([group, settings]) => ({ group, settings }));
  });

  constructor() {
    void this.load();
  }

  load(silent = false): Promise<unknown> {
    return this.state.load(
      async () => {
        const [settings, notifications] = await Promise.all([
          this.api.get<SettingDto[]>('admin/settings'),
          this.api.get<{ channels: Array<{ key: string; enabled: boolean }> }>('me/notifications', { take: 1 }),
        ]);
        return { settings, channels: notifications.channels };
      },
      { silent },
    );
  }

  afterSave(): void {
    void this.load(true);
    // Marka adı değişmiş olabilir: kabuk başlığını yenile.
    void this.auth.load(true);
  }
}
