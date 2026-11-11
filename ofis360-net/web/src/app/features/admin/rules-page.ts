import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { minutesToTime, timeToMinutes } from '../../lib/date';
import { RULE_KEYS } from '../../lib/enums';
import { cn } from '../../lib/utils';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import type { RuleDto } from '../../core/api/types';
import { PageState, ToastService, UiAlert, UiBadge, UiButton, UiIcon, UiInput, UiPageError, UiPageHeader, UiPageLoading, UiSwitch } from '../../shared/ui';
import { HolidayPanel, RuleMatrixPanel } from './rule-matrix';

/** Kaynak: (app)/admin/kurallar/page.tsx + components/admin/rules-manager.tsx */

const CATEGORY_LABELS: Record<string, string> = {
  DESK: 'Masa kuralları',
  ROOM: 'Toplantı odası kuralları',
  GENERAL: 'Genel kurallar',
};

function displayValue(rule: RuleDto, value: number | null, isWorkingHours: boolean): string {
  if (value === null) return '';
  return isWorkingHours ? minutesToTime(value) : String(value);
}

@Component({
  selector: 'app-rule-card',
  imports: [FormsModule, UiBadge, UiButton, UiIcon, UiInput, UiSwitch],
  template: `
    @let r = rule();
    <div [class]="cn('rounded-xl border p-4 transition-colors', enabled() ? 'border-line bg-white' : 'border-line bg-slate-50')">
      <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div class="min-w-0 flex-1">
          <div class="flex flex-wrap items-center gap-2">
            <h3 class="text-sm font-semibold text-ink-900">{{ r.name }}</h3>
            @if (r.systemRule) {
              <span ui-badge tone="brand">
                <svg uiIcon name="Lock" class="h-3 w-3"></svg>
                Sistem kuralı
              </span>
            }
            <span ui-badge [tone]="enabled() ? 'success' : 'neutral'">{{ enabled() ? 'Aktif' : 'Pasif' }}</span>
          </div>
          <p class="mt-1.5 max-w-2xl text-[13px] leading-relaxed text-ink-500">{{ r.description }}</p>
          @if (r.unit) {
            <p class="mt-1 text-xs text-ink-400">Birim: {{ r.unit }}</p>
          }
        </div>

        <div class="flex flex-wrap items-end gap-3">
          @if (r.intValue !== null && r.editableLimit) {
            <label class="block">
              <span class="mb-1 block text-xs font-medium text-ink-500">{{ isWorkingHours() ? 'Başlangıç' : r.key === popularShareKey ? 'Pencere (N)' : isNoShow() ? 'Tolerans (dk)' : 'Değer' }}</span>
              <input
                ui-input
                class="w-32"
                [type]="isWorkingHours() ? 'time' : 'number'"
                [min]="r.minValue ?? null"
                [max]="r.maxValue ?? null"
                [ngModel]="intValue()"
                (ngModelChange)="intValue.set($event)"
                [disabled]="!enabled()"
                [attr.aria-label]="r.name + ' değeri'"
              />
            </label>
          }

          @if (r.secondValue !== null && r.editableLimit) {
            <label class="block">
              <span class="mb-1 block text-xs font-medium text-ink-500">{{ isWorkingHours() ? 'Bitiş' : isNoShow() ? 'Başlangıç saati' : 'Üst sınır (M)' }}</span>
              <input ui-input class="w-32" [type]="secondIsTime() ? 'time' : 'number'" [ngModel]="secondValue()" (ngModelChange)="secondValue.set($event)" [disabled]="!enabled()" [attr.aria-label]="r.name + ' ikinci değeri'" />
            </label>
          }

          <div class="flex items-center gap-2 pb-1">
            <ui-switch [checked]="enabled()" (checkedChange)="enabled.set($event)" [disabled]="r.systemRule" [label]="r.name + ' kuralını etkinleştir'" />
            <span class="text-xs text-ink-500">{{ enabled() ? 'Açık' : 'Kapalı' }}</span>
          </div>

          <div class="flex gap-2">
            @if (dirty()) {
              <button ui-button type="button" variant="ghost" size="sm" (click)="reset()" [disabled]="pending()">
                <svg uiIcon name="RotateCcw" class="h-3.5 w-3.5"></svg>
                Geri al
              </button>
            }
            <button ui-button type="button" size="sm" (click)="save()" [loading]="pending()" [disabled]="!dirty()">
              <svg uiIcon name="Save" class="h-3.5 w-3.5"></svg>
              Kaydet
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class RuleCard {
  readonly rule = input.required<RuleDto>();
  readonly saved = output<void>();
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly cn = cn;
  readonly popularShareKey = RULE_KEYS.popularShareLimit;
  readonly isWorkingHours = computed(() => this.rule().key === RULE_KEYS.roomWorkingHours);
  readonly initialInt = computed(() => displayValue(this.rule(), this.rule().intValue, this.isWorkingHours()));
  readonly isNoShow = computed(() => this.rule().key === RULE_KEYS.noShowRelease);
  readonly secondIsTime = computed(() => this.isWorkingHours() || this.isNoShow());
  readonly initialSecond = computed(() => displayValue(this.rule(), this.rule().secondValue, this.secondIsTime()));

  readonly enabled = signal(false);
  readonly intValue = signal('');
  readonly secondValue = signal('');
  readonly pending = signal(false);

  constructor() {
    // Girdi değiştiğinde (kaydettikten sonra yeniden yüklenince) yerel durum kaynağa eşitlenir.
    effect(() => {
      const rule = this.rule();
      untracked(() => {
        this.enabled.set(rule.enabled);
        this.intValue.set(this.initialInt());
        this.secondValue.set(this.initialSecond());
      });
    });
  }

  readonly dirty = computed(
    () => this.enabled() !== this.rule().enabled || this.intValue() !== this.initialInt() || this.secondValue() !== this.initialSecond(),
  );

  reset(): void {
    this.enabled.set(this.rule().enabled);
    this.intValue.set(this.initialInt());
    this.secondValue.set(this.initialSecond());
  }

  async save(): Promise<void> {
    const rule = this.rule();
    if ((this.isWorkingHours() && !this.intValue()) || (rule.secondValue !== null && this.secondIsTime() && !this.secondValue())) {
      this.toasts.toast({ tone: 'error', title: 'Kural güncellenemedi', description: 'Saat alanı boş bırakılamaz.' });
      return;
    }
    this.pending.set(true);
    const payload: { enabled: boolean; intValue?: number | null; secondValue?: number | null } = { enabled: this.enabled() };
    if (rule.intValue !== null) payload.intValue = this.isWorkingHours() ? timeToMinutes(this.intValue()) : Number(this.intValue());
    if (rule.secondValue !== null) payload.secondValue = this.secondIsTime() ? timeToMinutes(this.secondValue()) : Number(this.secondValue());
    try {
      await this.api.put(`admin/rules/${rule.key}`, payload);
      this.toasts.toast({ tone: 'success', title: 'Kural güncellendi', description: `${rule.name} ayarı kaydedildi ve hemen geçerli oldu.` });
      this.saved.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Kural güncellenemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}

@Component({
  selector: 'app-rules-page',
  imports: [UiPageHeader, UiPageLoading, UiPageError, UiAlert, UiIcon, RuleCard, RuleMatrixPanel, HolidayPanel],
  template: `
    <ui-page-header title="Rezervasyon kuralları" description="Adil kullanım kuralları servis katmanında uygulanır. Buradaki değişiklikler kaydedildiği anda tüm yeni rezervasyonlarda geçerli olur." />

    @if (feedConfigured() === false) {
      <ui-alert tone="warning" class="mb-5" title="Turnike bağlı değil" hasIcon>
      Otomatik masa bırakma turnike verisi olmadan çalışmaz; kayıtlar gelmedi sayılmaz. Çalışanlar uygulamadan giriş yapabilir.
    </ui-alert>
    }
    <ui-alert tone="info" class="mb-5" title="Kurallar nasıl çalışır?" hasIcon>
      <svg uiIcon icon name="Scale" class="h-4 w-4"></svg>
      Her rezervasyon talebi, kural motorundan geçer. Bir kural engel oluşturduğunda çalışana gerekçe ve alternatif masa önerileri gösterilir. Sistem
      kuralları veri bütünlüğü için devre dışı bırakılamaz.
    </ui-alert>

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else {
      <div class="space-y-6">
        <app-rule-matrix />
        <app-holiday-panel />
        @for (group of grouped(); track group.category) {
          <section>
            <h2 class="mb-3 text-base font-semibold text-ink-900">{{ categoryLabels[group.category] ?? group.category }} (varsayılan)</h2>
            <ul class="space-y-3">
              @for (rule of group.rules; track rule.key) {
                <li><app-rule-card [rule]="rule" (saved)="load(true)" /></li>
              }
            </ul>
          </section>
        }
      </div>
    }
  `,
})
export class RulesPage {
  private readonly api = inject(ApiClient);
  readonly state = new PageState<RuleDto[]>();
  readonly categoryLabels = CATEGORY_LABELS;

  readonly grouped = computed(() => {
    const groups = new Map<string, RuleDto[]>();
    for (const rule of this.state.data() ?? []) {
      const bucket = groups.get(rule.category);
      if (bucket) bucket.push(rule);
      else groups.set(rule.category, [rule]);
    }
    return [...groups.entries()].map(([category, rules]) => ({ category, rules }));
  });

  readonly feedConfigured = signal<boolean | null>(null);

  constructor() {
    void this.api.get<{ configured: boolean }>('admin/rules/attendance-feed').then((r) => this.feedConfigured.set(r.configured));
    void this.load();
  }

  load(silent = false): Promise<unknown> {
    return this.state.load(() => this.api.get<RuleDto[]>('admin/rules'), { silent });
  }
}
