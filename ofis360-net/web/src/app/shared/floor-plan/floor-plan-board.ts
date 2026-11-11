import { Component, ElementRef, computed, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { formatLongDate } from '../../lib/date';
import { cn } from '../../lib/utils';
import { ApiClient, toApiError } from '../../core/api/api-client';
import type { DeskView, FloorPlanView, RuleViolation } from '../../core/api/types';
import {
  ToastService,
  UiAlert,
  UiBadge,
  UiButton,
  UiCard,
  UiCardBody,
  UiCardHeader,
  UiConfirmDialog,
  UiEmptyState,
  UiField,
  UiIcon,
  UiTextarea,
} from '../ui';
import { DESK_STATE_STYLE, FloorPlanLegend, FloorPlanSvg, type PlanViewBox } from './floor-plan-svg';

/** Kaynak: components/floor-plan/floor-plan-board.tsx */

const FEATURE_FILTERS = [
  { key: 'WINDOW', label: 'Pencere yanı' },
  { key: 'DUAL_MONITOR', label: 'Çift monitör' },
  { key: 'DOCK', label: 'Dock istasyonu' },
  { key: 'QUIET', label: 'Sessiz alan' },
  { key: 'STANDING', label: 'Yükseklik ayarlı' },
  { key: 'ERGONOMIC', label: 'Ergonomik' },
  { key: 'ACCESSIBLE', label: 'Erişilebilir' },
];

const FOCUS_PADDING = 24;
const MIN_ZOOM = 1;
const MAX_ZOOM = 3;

interface FocusOption {
  key: string;
  label: string;
  box: PlanViewBox | null;
}

/**
 * Büyük kat planlarında tüm katı tek seferde okumak zordur. Bu yardımcı, masa içeren çalışma bölgelerini
 * "odak" seçenekleri olarak üretir; seçilen bölge SVG viewBox'ına kırpılır.
 */
function buildFocusOptions(plan: FloorPlanView): FocusOption[] {
  const all: FocusOption = { key: 'ALL', label: 'Tüm kat', box: null };
  const deskZoneIds = new Set(plan.desks.map((desk) => desk.zoneId));
  const zones = plan.zones.filter((zone) => deskZoneIds.has(zone.id));
  if (zones.length < 2) return [all];

  return [
    all,
    ...zones.map((zone) => ({
      key: zone.id,
      label: zone.name,
      box: {
        x: Math.max(0, zone.x - FOCUS_PADDING),
        y: Math.max(0, zone.y - FOCUS_PADDING),
        width: Math.min(plan.floor.planWidth, zone.width + FOCUS_PADDING * 2),
        height: Math.min(plan.floor.planHeight, zone.height + FOCUS_PADDING * 2),
      },
    })),
  ];
}

@Component({
  selector: 'app-floor-plan-board',
  imports: [
    FormsModule,
    UiIcon,
    UiCard,
    UiCardHeader,
    UiCardBody,
    UiAlert,
    UiBadge,
    UiButton,
    UiEmptyState,
    UiField,
    UiTextarea,
    UiConfirmDialog,
    FloorPlanSvg,
    FloorPlanLegend,
  ],
  template: `
    @let p = plan();
    @let selected = selectedDesk();
    <div class="grid gap-5 lg:grid-cols-[minmax(0,1fr)_360px]">
      <div class="space-y-4">
        <!-- Özet + filtreler -->
        <div ui-card>
          <ui-card-header [title]="floorTitle() + ' · ' + formatLongDate(dayKey(), { withWeekday: false })" [description]="p.floor.description" hasIcon>
            <svg uiIcon icon name="MapPin" class="h-4.5 w-4.5"></svg>
            <div action class="flex items-center gap-1 rounded-lg border border-line bg-slate-50 p-0.5">
              <button
                type="button"
                (click)="viewMode.set('plan')"
                [attr.aria-pressed]="viewMode() === 'plan'"
                [class]="cn('flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-[13px] font-medium transition-colors', viewMode() === 'plan' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')"
              >
                <svg uiIcon name="LayoutGrid" class="h-4 w-4"></svg>
                Kat planı
              </button>
              <button
                type="button"
                (click)="viewMode.set('list')"
                [attr.aria-pressed]="viewMode() === 'list'"
                [class]="cn('flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-[13px] font-medium transition-colors', viewMode() === 'list' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')"
              >
                <svg uiIcon name="List" class="h-4 w-4"></svg>
                Liste
              </button>
            </div>
          </ui-card-header>
          <ui-card-body class="space-y-4">
            <dl class="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <div class="rounded-lg bg-slate-50 px-3 py-2">
                <dt class="text-xs text-ink-500">Rezervasyona açık</dt>
                <dd class="text-lg font-semibold text-ink-900">{{ p.summary.total }}</dd>
              </div>
              <div class="rounded-lg bg-emerald-50 px-3 py-2">
                <dt class="text-xs text-emerald-700">Uygun</dt>
                <dd class="text-lg font-semibold text-emerald-800">{{ p.summary.available }}</dd>
              </div>
              <div class="rounded-lg bg-slate-100 px-3 py-2">
                <dt class="text-xs text-ink-500">Dolu</dt>
                <dd class="text-lg font-semibold text-ink-900">{{ p.summary.reserved }}</dd>
              </div>
              <div class="rounded-lg bg-red-50 px-3 py-2">
                <dt class="text-xs text-red-700">Kapalı</dt>
                <dd class="text-lg font-semibold text-red-800">{{ p.summary.closed }}</dd>
              </div>
            </dl>

            <div>
              <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-400">Özelliğe göre filtrele</p>
              <div class="flex flex-wrap gap-1.5">
                @for (feature of featureFilters; track feature.key) {
                  @let active = activeFeatures().includes(feature.key);
                  <button
                    type="button"
                    [attr.aria-pressed]="active"
                    (click)="toggleFeature(feature.key)"
                    [class]="cn('rounded-full border px-3 py-1 text-xs font-medium transition-colors', active ? 'border-brand-500 bg-brand-600 text-white' : 'border-line bg-white text-ink-600 hover:border-brand-200 hover:bg-brand-50')"
                  >
                    {{ feature.label }}
                  </button>
                }
                @if (activeFeatures().length > 0) {
                  <button type="button" (click)="activeFeatures.set([])" class="rounded-full px-3 py-1 text-xs font-medium text-ink-500 underline underline-offset-2 hover:text-ink-700">
                    Filtreleri temizle
                  </button>
                }
              </div>
            </div>
          </ui-card-body>
        </div>

        <!-- Plan / liste -->
        <div ui-card>
          <ui-card-body>
            @if (!p.floor.reservable) {
              <ui-alert tone="warning" class="mb-4" hasIcon>
                <svg uiIcon icon name="Lock" class="h-4 w-4"></svg>
                {{ p.floor.name }} çalışan rezervasyonuna kapalıdır. Plan yalnızca bilgi amaçlı gösterilmektedir.
              </ui-alert>
            }

            @if (viewMode() === 'plan') {
              <div class="space-y-2">
                <div class="flex flex-wrap items-center justify-between gap-2">
                  @if (focusOptions().length > 1) {
                    <div class="flex flex-wrap items-center gap-1.5" role="group" aria-label="Plan odağı">
                      <svg uiIcon name="Focus" class="h-4 w-4 text-ink-400"></svg>
                      @for (option of focusOptions(); track option.key) {
                        @let active = option.key === focusKey();
                        <button
                          type="button"
                          [attr.aria-pressed]="active"
                          (click)="focusKey.set(option.key)"
                          [class]="cn('rounded-full border px-2.5 py-1 text-xs font-medium transition-colors', active ? 'border-brand-500 bg-brand-600 text-white' : 'border-line bg-white text-ink-600 hover:border-brand-200 hover:bg-brand-50')"
                        >
                          {{ option.label }}
                        </button>
                      }
                    </div>
                  } @else {
                    <span></span>
                  }
                  <div class="flex items-center gap-1">
                    <button
                      type="button"
                      aria-label="Planı uzaklaştır"
                      (click)="zoomBy(-0.5)"
                      [disabled]="planZoom() <= minZoom"
                      class="flex h-8 w-8 items-center justify-center rounded-lg border border-line bg-white text-ink-600 transition-colors hover:bg-slate-50 disabled:opacity-40"
                    >
                      <span aria-hidden="true">−</span>
                    </button>
                    <span class="w-12 text-center text-xs tabular-nums text-ink-500">%{{ Math.round(planZoom() * 100) }}</span>
                    <button
                      type="button"
                      aria-label="Planı yakınlaştır"
                      (click)="zoomBy(0.5)"
                      [disabled]="planZoom() >= maxZoom"
                      class="flex h-8 w-8 items-center justify-center rounded-lg border border-line bg-white text-ink-600 transition-colors hover:bg-slate-50 disabled:opacity-40"
                    >
                      <span aria-hidden="true">+</span>
                    </button>
                    <button
                      type="button"
                      (click)="planZoom.set(1); focusKey.set('ALL')"
                      class="rounded-lg border border-line bg-white px-2.5 py-1.5 text-xs font-medium text-ink-600 transition-colors hover:bg-slate-50"
                    >
                      Sıfırla
                    </button>
                  </div>
                </div>
                <div class="overflow-x-auto scrollbar-thin" tabindex="0" role="region" aria-label="Kat planı">
                  <!-- %100 = konteyner genişliği; yakınlaştırmada plan yatay kaydırılır. -->
                  <div [style.width.%]="Math.round(planZoom() * 100)">
                    <app-floor-plan-svg
                      [planWidth]="p.floor.planWidth"
                      [planHeight]="p.floor.planHeight"
                      [zones]="p.zones"
                      [elements]="p.elements"
                      [desks]="p.desks"
                      [rooms]="p.rooms"
                      [selectedDeskId]="selectedDeskId()"
                      [highlightedDeskIds]="highlightedDeskIds()"
                      (selectDesk)="handleSelect($event)"
                      [viewBox]="focusBox()"
                    />
                  </div>
                </div>
              </div>
            } @else {
              <!-- Liste görünümü -->
              @if (visibleDesks().length === 0) {
                <ui-empty-state title="Filtreye uyan masa bulunamadı" description="Seçtiğiniz özellik kombinasyonuna sahip masa yok. Filtreleri gevşetmeyi deneyin." />
              } @else {
                <div class="space-y-5">
                  @for (group of groupedDesks(); track group.zoneName) {
                    <div>
                      <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-400">{{ group.zoneName }}</p>
                      <ul class="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
                        @for (desk of group.desks; track desk.id) {
                          @let style = stateStyle(desk.state);
                          <li>
                            <button
                              type="button"
                              (click)="handleSelect(desk.id)"
                              [attr.aria-pressed]="desk.id === selectedDeskId()"
                              [class]="cn('flex w-full items-center justify-between gap-3 rounded-xl border p-3 text-left transition-all', desk.id === selectedDeskId() ? 'border-brand-500 ring-2 ring-brand-100' : 'border-line hover:border-brand-200 hover:bg-brand-50/40')"
                            >
                              <span class="min-w-0">
                                <span class="block text-sm font-semibold text-ink-900">{{ desk.code }}</span>
                                <span class="mt-0.5 block truncate text-xs text-ink-500">{{ featureSummary(desk) }}</span>
                              </span>
                              <span
                                class="shrink-0 rounded-full border px-2 py-0.5 text-[11px] font-medium"
                                [style.background-color]="style.fill"
                                [style.border-color]="style.stroke"
                                [style.color]="style.text"
                              >
                                {{ style.label }}
                              </span>
                            </button>
                          </li>
                        }
                      </ul>
                    </div>
                  }
                </div>
              }
            }

            <div class="mt-4 border-t border-line pt-3">
              <app-floor-plan-legend />
            </div>
          </ui-card-body>
        </div>
      </div>

      <!-- Detay paneli -->
      <div #detail class="lg:sticky lg:top-24 lg:self-start">
        <div ui-card>
          <ui-card-header title="Masa detayı" [description]="selected ? selected.code : 'Plandan bir masa seçin'" hasIcon>
            <svg uiIcon icon name="Info" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body>
            @if (!selected) {
              <ui-empty-state hasIcon title="Henüz masa seçmediniz" description="Kat planı üzerindeki bir masaya tıklayarak detayları görebilir ve rezervasyon oluşturabilirsiniz.">
                <svg uiIcon icon name="MapPin" class="h-6 w-6"></svg>
              </ui-empty-state>
            } @else {
              @let st = stateStyle(selected.state);
              <div class="space-y-4">
                <div>
                  <div class="flex items-start justify-between gap-3">
                    <div>
                      <p class="text-xl font-semibold tracking-tight text-ink-900">{{ selected.code }}</p>
                      <p class="mt-0.5 text-sm text-ink-500">{{ floorTitle() }} · {{ selected.zoneName }}</p>
                    </div>
                    <span class="rounded-full border px-2.5 py-1 text-xs font-medium" [style.background-color]="st.fill" [style.border-color]="st.stroke" [style.color]="st.text">
                      {{ st.label }}
                    </span>
                  </div>
                  <p class="mt-2 text-sm text-ink-500">Seçilen tarih: {{ formatLongDate(dayKey()) }}</p>
                </div>

                <div>
                  <p class="mb-1.5 text-xs font-semibold uppercase tracking-wide text-ink-400">Özellikler</p>
                  <div class="flex flex-wrap gap-1.5">
                    @if (selected.features.length === 0) {
                      <span class="text-sm text-ink-500">Tanımlı özellik yok</span>
                    } @else {
                      @for (feature of selected.features; track feature.key) {
                        <span ui-badge tone="neutral">
                          @if (feature.key === 'ACCESSIBLE') {
                            <svg uiIcon name="Accessibility" class="h-3 w-3"></svg>
                          } @else if (feature.key.includes('MONITOR')) {
                            <svg uiIcon name="Monitor" class="h-3 w-3"></svg>
                          }
                          {{ feature.name }}
                        </span>
                      }
                    }
                    @if (selected.popular) {
                      <span ui-badge tone="warning">★ Popüler masa</span>
                    }
                  </div>
                  @if (selected.notes) {
                    <p class="mt-2 text-[13px] text-ink-500">{{ selected.notes }}</p>
                  }
                </div>

                @if (selected.occupant) {
                  <div class="flex items-center gap-2.5 rounded-lg bg-slate-50 px-3 py-2.5">
                    <svg uiIcon name="UserRound" class="h-4 w-4 text-ink-400"></svg>
                    <div>
                      <p class="text-sm font-medium text-ink-900">{{ selected.state === 'MINE' ? 'Sizin rezervasyonunuz' : selected.occupant.fullName }}</p>
                      <p class="text-xs text-ink-500">{{ selected.occupant.departmentName }}</p>
                    </div>
                  </div>
                }

                <!-- Rezervasyon yapılamama gerekçeleri -->
                @if (activeViolations().length > 0) {
                  <div class="space-y-2">
                    @for (violation of activeViolations(); track violation.code) {
                      <ui-alert [tone]="violation.code === 'ALREADY_OWNED' ? 'info' : 'warning'" [title]="violation.title">
                        <p>{{ violation.message }}</p>
                        @if (violation.hint) {
                          <p class="mt-1 text-ink-600">{{ violation.hint }}</p>
                        }
                      </ui-alert>
                    }
                  </div>
                }

                <!-- Alternatif öneriler -->
                @if (alternatives().length > 0) {
                  <div class="rounded-xl border border-brand-200 bg-brand-50/60 p-3.5">
                    <p class="flex items-center gap-1.5 text-[13px] font-semibold text-brand-800">
                      <svg uiIcon name="Sparkles" class="h-4 w-4"></svg>
                      Bu tarihte uygun alternatifler
                    </p>
                    <ul class="mt-2 space-y-1.5">
                      @for (desk of alternatives(); track desk.id) {
                        <li>
                          <button type="button" (click)="handleSelect(desk.id)" class="flex w-full items-center justify-between rounded-lg bg-white px-3 py-2 text-left text-sm transition-colors hover:bg-brand-100/50">
                            <span class="font-medium text-ink-900">{{ desk.code }}</span>
                            <span class="text-xs text-ink-500">{{ desk.zoneId === selected.zoneId ? 'Aynı bölge' : desk.zoneName }}</span>
                          </button>
                        </li>
                      }
                    </ul>
                  </div>
                }

                @if (readOnlyReason()) {
                  <ui-alert tone="info">{{ readOnlyReason() }}</ui-alert>
                } @else if (selected.selectable) {
                  <div class="space-y-3">
                    <ui-field label="Not (isteğe bağlı)" htmlFor="reservation-note">
                      <textarea ui-textarea id="reservation-note" rows="2" maxlength="240" [(ngModel)]="note" placeholder="Örneğin: Ekip planlama günü nedeniyle ofisteyim."></textarea>
                    </ui-field>
                    <button ui-button type="button" block size="lg" (click)="confirmOpen.set(true)">
                      <svg uiIcon name="CheckCircle2" class="h-4.5 w-4.5"></svg>
                      Bu masayı rezerve et
                    </button>
                  </div>
                }
              </div>
            }
          </ui-card-body>
        </div>
      </div>

      <ui-confirm-dialog
        [open]="confirmOpen()"
        (closed)="confirmOpen.set(false)"
        (confirmed)="handleConfirm()"
        [loading]="pending()"
        tone="primary"
        title="Rezervasyonu onaylayın"
        confirmLabel="Rezervasyonu oluştur"
        hasContent
      >
        <div class="space-y-2 text-sm text-ink-700">
          <p>
            <strong class="font-semibold text-ink-900">{{ selected?.code }}</strong> kodlu masa
            <strong class="font-semibold text-ink-900">{{ formatLongDate(dayKey()) }}</strong> tarihi için {{ targetName() ? targetName() + ' adına' : 'adınıza' }} ayrılacak.
          </p>
          <p class="text-ink-500">{{ floorTitle() }} · {{ selected?.zoneName }}</p>
          @if (selected?.popular) {
            <p class="rounded-lg bg-amber-50 px-3 py-2 text-[13px] text-amber-800">
              Bu masa popüler olarak işaretlenmiştir. Adil kullanım kuralları gereği ardışık kullanım ve kullanım payı sınırlarına tabidir.
            </p>
          }
        </div>
      </ui-confirm-dialog>
    </div>
  `,
})
export class FloorPlanBoard {
  readonly plan = input.required<FloorPlanView>();
  readonly dayKey = input.required<string>();
  readonly readOnlyReason = input<string | null | undefined>(null);
  /** Sekreter veya Admin başkası adına işlem yapıyorsa hedef kişi. */
  readonly targetUserId = input<string | null>(null);
  readonly targetName = input<string | null>(null);
  /** Rezervasyon oluşturuldu / başarısız oldu → üst sayfa planı yeniler (router.refresh karşılığı). */
  readonly changed = output<void>();

  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  private readonly detailRef = viewChild<ElementRef<HTMLDivElement>>('detail');

  readonly Math = Math;
  readonly cn = cn;
  readonly formatLongDate = formatLongDate;
  readonly featureFilters = FEATURE_FILTERS;
  readonly minZoom = MIN_ZOOM;
  readonly maxZoom = MAX_ZOOM;

  readonly selectedDeskId = signal<string | null>(null);
  readonly viewMode = signal<'plan' | 'list'>('plan');
  readonly activeFeatures = signal<string[]>([]);
  readonly confirmOpen = signal(false);
  note = '';
  readonly pending = signal(false);
  // Yakınlaştırma: %100 = plan konteynere sığar; büyük planlarda yatay kaydırma ile incelenir.
  readonly planZoom = signal(1);
  // Odak: kanat / çalışma bölgesi seçildiğinde plan o bölgeye kırpılır.
  readonly focusKey = signal('ALL');
  readonly violations = signal<RuleViolation[] | null>(null);

  constructor() {
    // Kat veya tarih değiştiğinde seçim sıfırlanır; kat değişince odak da sıfırlanır.
    let lastFloor: string | null = null;
    effect(() => {
      const floorId = this.plan().floor.id;
      this.dayKey();
      this.selectedDeskId.set(null);
      this.violations.set(null);
      this.note = '';
      if (floorId !== lastFloor) {
        this.focusKey.set('ALL');
        lastFloor = floorId;
      }
    });
  }

  readonly focusOptions = computed(() => buildFocusOptions(this.plan()));
  readonly focusBox = computed(() => this.focusOptions().find((option) => option.key === this.focusKey())?.box ?? null);
  readonly selectedDesk = computed(() => this.plan().desks.find((desk) => desk.id === this.selectedDeskId()) ?? null);
  readonly floorTitle = computed(() => `${this.plan().floor.buildingName} · ${this.plan().floor.name}`);

  readonly highlightedDeskIds = computed(() => {
    const features = this.activeFeatures();
    if (features.length === 0) return null;
    return new Set(
      this.plan()
        .desks.filter((desk) => features.every((key) => desk.features.some((f) => f.key === key)))
        .map((desk) => desk.id),
    );
  });

  readonly alternatives = computed(() => {
    const selected = this.selectedDesk();
    if (!selected || selected.selectable) return [];
    return this.plan()
      .desks.filter((desk) => desk.selectable && desk.id !== selected.id)
      .map((desk) => ({ desk, score: (desk.zoneId === selected.zoneId ? 2 : 0) + (desk.popular ? 0 : 1) }))
      .sort((a, b) => b.score - a.score)
      .slice(0, 3)
      .map((item) => item.desk);
  });

  readonly activeViolations = computed(() => this.violations() ?? this.selectedDesk()?.violations ?? []);

  readonly visibleDesks = computed(() => {
    const highlighted = this.highlightedDeskIds();
    return highlighted ? this.plan().desks.filter((desk) => highlighted.has(desk.id)) : this.plan().desks;
  });

  readonly groupedDesks = computed(() => {
    const groups = new Map<string, DeskView[]>();
    for (const desk of this.visibleDesks()) {
      const bucket = groups.get(desk.zoneName);
      if (bucket) bucket.push(desk);
      else groups.set(desk.zoneName, [desk]);
    }
    return [...groups.entries()].map(([zoneName, desks]) => ({ zoneName, desks }));
  });

  stateStyle(state: string) {
    return DESK_STATE_STYLE[state] ?? DESK_STATE_STYLE['AVAILABLE'];
  }

  featureSummary(desk: DeskView): string {
    return desk.features.slice(0, 2).map((feature) => feature.name).join(' · ') || 'Standart masa';
  }

  toggleFeature(key: string): void {
    this.activeFeatures.update((current) => (current.includes(key) ? current.filter((k) => k !== key) : [...current, key]));
  }

  zoomBy(delta: number): void {
    this.planZoom.update((value) => Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, Number((value + delta).toFixed(2)))));
  }

  handleSelect(deskId: string): void {
    this.selectedDeskId.set(deskId);
    this.violations.set(null);
    window.requestAnimationFrame(() => {
      if (window.innerWidth < 1024) {
        this.detailRef()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    });
  }

  async handleConfirm(): Promise<void> {
    const selected = this.selectedDesk();
    if (!selected) return;
    this.pending.set(true);
    try {
      const result = await this.api.post<{ id: string; deskCode: string; date: string }>('desk-reservations', {
        deskId: selected.id,
        date: this.dayKey(),
        note: this.note.trim() || null,
        targetUserId: this.targetUserId() ?? undefined,
      });
      this.confirmOpen.set(false);
      this.note = '';
      this.toasts.toast({
        tone: 'success',
        title: 'Rezervasyon oluşturuldu',
        description: `${result.deskCode} · ${formatLongDate(this.dayKey(), { withWeekday: false })}`,
      });
      this.selectedDeskId.set(null);
      this.changed.emit();
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this.confirmOpen.set(false);
      this.violations.set(apiError.violations ?? null);
      this.toasts.toast({ tone: 'error', title: 'Rezervasyon oluşturulamadı', description: apiError.message });
      this.changed.emit();
    } finally {
      this.pending.set(false);
    }
  }
}
