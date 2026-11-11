import { Component, computed, effect, inject, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { addDaysToKey, formatLongDate, isValidDayKey, todayKey } from '../../lib/date';
import type { PlanElement } from '../../lib/plan';
import { cn, formatPercent } from '../../lib/utils';
import { ApiClient } from '../../core/api/api-client';
import type { AnalyticsResult } from '../../core/api/types';
import {
  PageState,
  UiAlert,
  UiBadge,
  UiButton,
  UiCard,
  UiCardBody,
  UiCardHeader,
  UiIcon,
  UiInput,
  UiPageError,
  UiPageHeader,
  UiPageLoading,
  UiProgress,
  UiSelect,
  UiStatCard,
} from '../../shared/ui';
import { UiComparisonChart, UiTrendChart, type ChartRow } from '../../shared/charts/chart-kit';
import { UiPlanHeatmap, type HeatmapFloor } from '../../shared/charts/plan-heatmap';

/** Kaynak: (app)/yonetim/page.tsx + components/analytics/analytics-filters.tsx */

const PRESETS = [
  { key: '7', label: 'Son 7 gün', days: 6 },
  { key: '30', label: 'Son 30 gün', days: 29 },
  { key: '90', label: 'Son 90 gün', days: 89 },
];

const EXPORTS = [
  { type: 'ozet', label: 'Genel özet' },
  { type: 'doluluk', label: 'Günlük doluluk' },
  { type: 'kat', label: 'Kat bazında doluluk' },
  { type: 'masa', label: 'Masa kullanımı' },
  { type: 'oda', label: 'Toplantı odası kullanımı' },
  { type: 'departman', label: 'Departman dağılımı' },
  { type: 'adil-kullanim', label: 'Adil kullanım dağılımı' },
];

interface AnalyticsContext {
  floors: Array<{ id: string; name: string }>;
  departments: Array<{ id: string; name: string }>;
  planFloors: Array<{
    id: string;
    name: string;
    planWidth: number;
    planHeight: number;
    elements: PlanElement[];
    desks: Array<{ id: string; code: string; x: number; y: number; width: number; height: number; rotation: number }>;
  }>;
}

interface PageData {
  analytics: AnalyticsResult;
  context: AnalyticsContext;
  validRange: boolean;
}

interface AnalyticsQuery {
  from: string | null;
  to: string | null;
  floor: string | null;
  department: string | null;
  resource: string | null;
}

@Component({
  selector: 'app-analytics-page',
  imports: [FormsModule, UiPageHeader, UiPageLoading, UiPageError, UiCard, UiCardHeader, UiCardBody, UiAlert, UiStatCard, UiBadge, UiProgress, UiButton, UiIcon, UiInput, UiSelect, UiTrendChart, UiComparisonChart, UiPlanHeatmap],
  template: `
    <ui-page-header title="Ofis analitiği" description="Kat ve gün bazında doluluk, ortak alan kullanımı ve adil kullanım göstergeleri. Tüm raporlar toplulaştırılmış ve anonimdir." />

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as d) {
      @let a = d.analytics;
      @let k = a.kpis;
      <div ui-card class="mb-5">
        <ui-card-header title="Filtreler ve dışa aktarma" [description]="formatLongDate(a.filters.fromDayKey, { withWeekday: false }) + ' - ' + formatLongDate(a.filters.toDayKey, { withWeekday: false }) + ' · ' + a.filters.workdayCount + ' iş günü'" />
        <ui-card-body>
          @if (!d.validRange) {
            <ui-alert tone="warning" class="mb-4" title="Geçersiz tarih aralığı">Seçilen aralık geçersiz olduğu için varsayılan aralık (son 30 gün) gösteriliyor.</ui-alert>
          }

          <!-- Yönetici raporları için filtre çubuğu ve CSV dışa aktarma -->
          <div class="space-y-4">
            <div class="flex flex-wrap items-center gap-1.5">
              @for (preset of presets; track preset.key) {
                @let active = activePreset() === preset.key;
                <button type="button" (click)="applyPreset(preset.days)" [attr.aria-pressed]="active" [class]="cn('rounded-lg border px-3 py-1.5 text-[13px] font-medium transition-colors', active ? 'border-brand-500 bg-brand-600 text-white' : 'border-line bg-white text-ink-600 hover:border-brand-200 hover:bg-brand-50')">
                  {{ preset.label }}
                </button>
              }
            </div>

            <div class="grid gap-3 lg:grid-cols-6">
              <label class="block">
                <span class="mb-1 block text-xs font-medium text-ink-500">Başlangıç</span>
                <input ui-input type="date" [(ngModel)]="fFrom" [max]="fTo" />
              </label>
              <label class="block">
                <span class="mb-1 block text-xs font-medium text-ink-500">Bitiş</span>
                <input ui-input type="date" [(ngModel)]="fTo" [min]="fFrom" />
              </label>
              @if (d.context.floors.length > 1) {
                <label class="block">
                  <span class="mb-1 block text-xs font-medium text-ink-500">Kat</span>
                  <select ui-select [(ngModel)]="fFloor">
                    <option value="">Tüm katlar</option>
                    @for (item of d.context.floors; track item.id) {
                      <option [value]="item.id">{{ item.name }}</option>
                    }
                  </select>
                </label>
              }
              <label class="block">
                <span class="mb-1 block text-xs font-medium text-ink-500">Departman</span>
                <select ui-select [(ngModel)]="fDepartment">
                  <option value="">Tüm departmanlar</option>
                  @for (item of d.context.departments; track item.id) {
                    <option [value]="item.id">{{ item.name }}</option>
                  }
                </select>
              </label>
              <label class="block">
                <span class="mb-1 block text-xs font-medium text-ink-500">Kaynak türü</span>
                <select ui-select [(ngModel)]="fResource">
                  <option value="ALL">Masa ve toplantı odası</option>
                  <option value="DESK">Yalnızca masa</option>
                  <option value="ROOM">Yalnızca toplantı odası</option>
                </select>
              </label>
              <div class="flex items-end gap-2">
                <button ui-button type="button" block (click)="apply()">
                  <svg uiIcon name="Filter" class="h-4 w-4"></svg>
                  Uygula
                </button>
                <button ui-button type="button" variant="secondary" size="icon" aria-label="Filtreleri sıfırla" (click)="resetFilters()">
                  <svg uiIcon name="RotateCcw" class="h-4 w-4"></svg>
                </button>
              </div>
            </div>

            <div class="flex flex-wrap items-end gap-2 border-t border-line pt-4">
              <label class="block min-w-[220px] flex-1 sm:flex-none">
                <span class="mb-1 block text-xs font-medium text-ink-500">CSV raporu</span>
                <select ui-select [(ngModel)]="exportType">
                  @for (item of exports; track item.type) {
                    <option [value]="item.type">{{ item.label }}</option>
                  }
                </select>
              </label>
              <a [href]="exportHref()" class="inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-teal-accent px-4 text-sm font-medium text-white transition-colors hover:bg-teal-700">
                <svg uiIcon name="Download" class="h-4 w-4"></svg>
                CSV indir
              </a>
              <p class="ml-auto max-w-md text-xs text-ink-400">Dışa aktarılan raporlar anonimdir; çalışan adı veya kimliği içermez.</p>
            </div>
          </div>
        </ui-card-body>
      </div>

      <!-- Kurumsal ölçek göstergeleri -->
      <div class="mb-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <ui-stat-card label="Toplam çalışan" [value]="String(k.totalEmployees)" [hint]="k.activeEmployees + ' aktif · ' + (k.totalEmployees - k.activeEmployees) + ' pasif/izinli'" hasIcon>
          <svg uiIcon icon name="Users" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Bugün ofiste" [value]="String(k.inOfficeToday)" [hint]="k.activeEmployees + ' aktif çalışanın %' + Math.round((k.inOfficeToday / Math.max(k.activeEmployees, 1)) * 100) + '\\'i'" tone="accent" hasIcon>
          <svg uiIcon icon name="CalendarCheck" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Kullanılabilir / toplam masa" [value]="k.availableDesksToday + ' / ' + k.totalUsableDesks" hint="Bugün için boş masa" [tone]="k.availableDesksToday < k.totalUsableDesks * 0.1 ? 'warning' : 'brand'" hasIcon>
          <svg uiIcon icon name="LayoutGrid" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Masa bulamayan çalışan" [value]="String(k.unmetRequests)" [hint]="k.unmetRequestDays + ' günde kapasite yetersiz kaldı'" [tone]="k.unmetRequests > 0 ? 'warning' : 'success'" hasIcon>
          <svg uiIcon icon name="AlertTriangle" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
      </div>

      @if (k.peakDay && k.peakDay.occupancyRate >= 85) {
        <ui-alert tone="warning" class="mb-4" title="Yoğun gün uyarısı">
          Seçili aralıkta en yoğun gün <strong class="font-medium">{{ k.peakDay.label }} {{ k.peakDay.weekday }}</strong> — masa doluluğu %{{ Math.round(k.peakDay.occupancyRate) }}. Bu günlerde ek kapasite
          planlaması veya ekiplerin gün dağılımının dengelenmesi önerilir.
        </ui-alert>
      }

      <!-- KPI'lar -->
      <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-6">
        <ui-stat-card label="Bugünkü rezervasyon" [value]="String(k.todayReservations)" [hint]="k.activeDeskCount + ' masa kapasitesi'" hasIcon>
          <svg uiIcon icon name="CalendarCheck" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Bugünkü doluluk" [value]="formatPercent(k.todayOccupancyRate)" hint="Aktif masalara oranla" [tone]="k.todayOccupancyRate > 85 ? 'warning' : 'brand'" hasIcon>
          <svg uiIcon icon name="Percent" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Haftalık ortalama doluluk" [value]="formatPercent(k.weeklyAverageOccupancy)" hint="Son 7 iş günü" tone="accent" hasIcon>
          <svg uiIcon icon name="TrendingUp" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Kullanılmadı (no-show)" [value]="formatPercent(k.noShowRate)" [hint]="k.noShowLostSeats + ' masa-gün boşa kaldı'" [tone]="k.noShowRate > 15 ? 'danger' : 'success'" hasIcon>
          <svg uiIcon icon name="XCircle" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Toplantı odası kullanımı" [value]="formatPercent(k.roomUsageRate)" [hint]="k.totalRoomReservations + ' rezervasyon'" hasIcon>
          <svg uiIcon icon name="DoorOpen" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="İptal oranı" [value]="formatPercent(k.cancellationRate)" hint="Tüm masa rezervasyonlarına oranla" [tone]="k.cancellationRate > 12 ? 'warning' : 'success'" hasIcon>
          <svg uiIcon icon name="XCircle" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
      </div>

      <!-- Trend ve kat karşılaştırması -->
      <div class="mt-5 grid gap-5 xl:grid-cols-3">
        <ui-trend-chart class="xl:col-span-2" title="Günlere göre doluluk trendi" description="Masa doluluk oranının seçili aralıktaki seyri" [data]="occupancyTrend()" [series]="[{ key: 'oran', label: 'Doluluk oranı' }]" [valueFormat]="{ style: 'percent' }" [height]="280" />
        <ui-comparison-chart title="Kat bazında doluluk" description="Seçili aralıktaki ortalama doluluk" layout="horizontal" [data]="floorComparison()" [series]="[{ key: 'oran', label: 'Doluluk oranı' }]" [valueFormat]="{ style: 'percent' }" [height]="280" />
      </div>

      <!-- Saatlik yoğunluk ve departman -->
      <div class="mt-5 grid gap-5 xl:grid-cols-2">
        <ui-comparison-chart title="Saatlere göre yoğunluk" description="Toplantı odası kullanımı ve ofise giriş saatleri" [data]="hourlyDensity()" [series]="[{ key: 'toplanti', label: 'Toplantı (adet)' }, { key: 'giris', label: 'Ofis girişi (adet)' }]" [valueFormat]="{ suffix: 'adet' }" [height]="280" />
        <ui-comparison-chart title="Departman bazında ofise geliş oranı" description="Departmanın çalışan sayısına ve iş günü sayısına göre normalize edilmiş oran" layout="horizontal" [data]="departmentAttendance()" [series]="[{ key: 'oran', label: 'Ofise geliş oranı' }]" [valueFormat]="{ style: 'percent' }" [colorByIndex]="true" [height]="320" />
      </div>

      <!-- Kat bazında kapasite baskısı -->
      <section class="card-surface mt-5 p-5">
        <h3 class="text-[15px] font-semibold text-ink-900">Kat bazında kapasite baskısı</h3>
        <p class="mt-0.5 text-[13px] text-ink-500">Katların seçili aralıktaki ortalama ve zirve doluluğu. Doluluğun %90 ve üzerine çıktığı günlerde çalışanlar masa bulamayabilir.</p>
        <div class="mt-4 overflow-x-auto scrollbar-thin" tabindex="0" role="region" aria-label="Analitik tablosu">
          <table class="w-full min-w-[560px] text-left text-[13px]">
            <thead>
              <tr class="border-b border-line text-xs uppercase tracking-wide text-ink-400">
                <th class="py-2 font-semibold">Kat</th>
                <th class="py-2 font-semibold">Kapasite</th>
                <th class="py-2 font-semibold">Ortalama doluluk</th>
                <th class="py-2 font-semibold">Zirve doluluk</th>
                <th class="py-2 font-semibold">Dolu gün</th>
              </tr>
            </thead>
            <tbody>
              @for (floor of a.floorPressure; track floor.floorId) {
                <tr class="border-b border-line/60 last:border-0">
                  <td class="py-2.5 font-medium text-ink-900">{{ floor.name }}</td>
                  <td class="py-2.5 text-ink-600">{{ floor.capacity }} masa</td>
                  <td class="py-2.5">
                    <div class="flex items-center gap-2">
                      <ui-progress [value]="floor.averageOccupancy" class="w-28" />
                      <span class="text-ink-700">{{ formatPercent(floor.averageOccupancy) }}</span>
                    </div>
                  </td>
                  <td class="py-2.5">
                    <span [class]="floor.peakOccupancy >= 90 ? 'font-semibold text-amber-700' : 'text-ink-700'">{{ formatPercent(floor.peakOccupancy) }}</span>
                  </td>
                  <td class="py-2.5">
                    @if (floor.saturatedDays > 0) {
                      <span ui-badge tone="warning">{{ floor.saturatedDays }} gün</span>
                    } @else {
                      <span class="text-ink-400">yok</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Rezervasyon vs kullanım -->
      <div class="mt-5 grid gap-5 xl:grid-cols-3">
        <ui-trend-chart class="xl:col-span-2" title="Rezervasyon ve gerçekleşen kullanım" description="Oluşturulan rezervasyonlara karşılık ofise gelen çalışan sayısı" [data]="bookedVsUsed()" [series]="[{ key: 'rezervasyon', label: 'Rezervasyon' }, { key: 'kullanilan', label: 'Gerçekleşen kullanım' }]" [valueFormat]="{ suffix: 'adet' }" [height]="280" />
        @if (showRoom()) {
          <ui-comparison-chart title="Toplantı odası kullanım oranları" description="Mesai saatlerine oranla dolu geçen süre" layout="horizontal" [data]="roomUsage()" [series]="[{ key: 'oran', label: 'Kullanım oranı' }]" [valueFormat]="{ style: 'percent' }" [height]="280" />
        }
      </div>

      <!-- Adil kullanım -->
      <div class="mt-5 grid gap-5 xl:grid-cols-3">
        <div ui-card class="xl:col-span-1">
          <ui-card-header title="Adil kullanım göstergesi" description="Kişi bazlı değil, dağılım bazlı ölçüm" hasIcon>
            <svg uiIcon icon name="Scale" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body class="space-y-4">
            <div>
              <div class="flex items-end justify-between">
                <p class="text-sm text-ink-500">Dengesizlik endeksi (Gini)</p>
                <p class="text-lg font-semibold text-ink-900">{{ a.fairness.giniIndex }}</p>
              </div>
              <ui-progress class="mt-2" [value]="a.fairness.giniIndex * 100" [tone]="a.fairness.giniIndex > 0.5 ? 'danger' : a.fairness.giniIndex > 0.35 ? 'warning' : 'success'" label="Kullanım dağılımı dengesizlik endeksi" />
              <p class="mt-1.5 text-xs leading-relaxed text-ink-500">0'a yakın değerler ortak alanların dengeli paylaşıldığını, 1'e yakın değerler kullanımın az sayıda kişide yoğunlaştığını gösterir.</p>
            </div>

            <div>
              <div class="flex items-end justify-between">
                <p class="text-sm text-ink-500">En çok kullanılan 5 masanın payı</p>
                <p class="text-lg font-semibold text-ink-900">{{ formatPercent(a.fairness.topDeskShare) }}</p>
              </div>
              <ui-progress class="mt-2" [value]="a.fairness.topDeskShare" [tone]="a.fairness.topDeskShare > 30 ? 'warning' : 'accent'" label="En çok kullanılan masaların payı" />
            </div>

            <dl class="grid grid-cols-2 gap-3 border-t border-line pt-3 text-sm">
              <div>
                <dt class="text-xs text-ink-500">Aktif çalışan</dt>
                <dd class="mt-0.5 font-semibold text-ink-900">{{ a.fairness.activeEmployees }}</dd>
              </div>
              <div>
                <dt class="text-xs text-ink-500">Kişi başı ortalama</dt>
                <dd class="mt-0.5 font-semibold text-ink-900">{{ a.fairness.averageReservationsPerEmployee }} rezervasyon</dd>
              </div>
            </dl>

            <ui-alert tone="info" hasIcon>
              <svg uiIcon icon name="ShieldCheck" class="h-4 w-4"></svg>
              Bu ekran çalışan takibi amacı taşımaz. Kişi adı, kimlik veya sıralama içermez; yalnızca toplulaştırılmış dağılımlar gösterilir.
            </ui-alert>
          </ui-card-body>
        </div>

        <ui-comparison-chart title="Rezervasyon sayısına göre çalışan dağılımı" description="Kaç çalışan, kaç rezervasyon yaptı? (anonim kovalar)" [data]="distribution()" [series]="[{ key: 'calisan', label: 'Çalışan sayısı' }]" [valueFormat]="{ suffix: 'çalışan' }" [height]="260" />
        <ui-comparison-chart title="Popüler masa kullanım payı dağılımı" description="Çalışanların rezervasyonlarının yüzde kaçı popüler masalarda?" [data]="popularShare()" [series]="[{ key: 'calisan', label: 'Çalışan sayısı' }]" [valueFormat]="{ suffix: 'çalışan' }" [height]="260" />
      </div>

      <!-- Isı haritası - kat planı üzerinde veya liste olarak -->
      @if (showDesk()) {
        <div class="mt-5">
          <ui-plan-heatmap [floors]="heatmapFloors()" [counts]="heatmapCounts()" [cells]="a.deskHeatmap" />
        </div>
      }

      <!-- En çok / en az kullanılanlar -->
      <div class="mt-5 grid gap-5 lg:grid-cols-2">
        <div ui-card>
          <ui-card-header title="En çok kullanılan masalar" hasIcon>
            <svg uiIcon icon name="Building2" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body>
            <ul class="space-y-2">
              @for (desk of a.deskHeatmap.slice(0, 6); track desk.deskId) {
                <li class="flex items-center justify-between gap-3 rounded-lg border border-line px-3 py-2.5">
                  <span class="flex items-center gap-2">
                    <span class="text-sm font-semibold text-ink-900">{{ desk.code }}</span>
                    @if (desk.popular) {
                      <span ui-badge tone="warning">★ Popüler</span>
                    }
                    <span class="text-xs text-ink-500">{{ desk.zoneName }}</span>
                  </span>
                  <span class="text-sm font-semibold text-ink-900">{{ desk.reservations }} kez</span>
                </li>
              }
            </ul>
          </ui-card-body>
        </div>

        <div ui-card>
          <ui-card-header title="En az kullanılan masalar" description="Kapasite planlaması için değerlendirilebilir" hasIcon>
            <svg uiIcon icon name="Building2" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body>
            <ul class="space-y-2">
              @for (desk of leastUsed(); track desk.deskId) {
                <li class="flex items-center justify-between gap-3 rounded-lg border border-line px-3 py-2.5">
                  <span class="flex items-center gap-2">
                    <span class="text-sm font-semibold text-ink-900">{{ desk.code }}</span>
                    <span class="text-xs text-ink-500">{{ desk.zoneName }}</span>
                  </span>
                  <span class="text-sm font-semibold text-ink-900">{{ desk.reservations }} kez</span>
                </li>
              }
            </ul>
          </ui-card-body>
        </div>
      </div>
    }
  `,
})
export class AnalyticsPage {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly cn = cn;
  readonly Math = Math;
  readonly String = String;
  readonly formatLongDate = formatLongDate;
  readonly formatPercent = formatPercent;
  readonly presets = PRESETS;
  readonly exports = EXPORTS;
  readonly state = new PageState<PageData>();

  readonly query = toSignal(
    this.route.queryParamMap.pipe(map((p): AnalyticsQuery => ({ from: p.get('from'), to: p.get('to'), floor: p.get('floor'), department: p.get('department'), resource: p.get('resource') }))),
    { initialValue: { from: null, to: null, floor: null, department: null, resource: null } as AnalyticsQuery },
  );

  fFrom = '';
  fTo = '';
  fFloor = '';
  fDepartment = '';
  fResource = 'ALL';
  exportType = 'ozet';

  readonly showDesk = computed(() => (this.state.data()?.analytics.filters.resourceType ?? 'ALL') !== 'ROOM');
  readonly showRoom = computed(() => (this.state.data()?.analytics.filters.resourceType ?? 'ALL') !== 'DESK');

  readonly occupancyTrend = computed<ChartRow[]>(() => (this.state.data()?.analytics.occupancyTrend ?? []).map((row) => ({ label: row.label, oran: Number(row.occupancyRate.toFixed(1)) })));
  readonly floorComparison = computed<ChartRow[]>(() => (this.state.data()?.analytics.floorComparison ?? []).map((row) => ({ label: row.name, oran: Number(row.occupancyRate.toFixed(1)) })));
  readonly hourlyDensity = computed<ChartRow[]>(() => (this.state.data()?.analytics.hourlyDensity ?? []).map((row) => ({ label: row.hour, toplanti: row.meetings, giris: row.arrivals })));
  readonly departmentAttendance = computed<ChartRow[]>(() => (this.state.data()?.analytics.departmentDistribution ?? []).map((row) => ({ label: row.name, oran: Number(row.attendanceRate.toFixed(1)) })));
  readonly bookedVsUsed = computed<ChartRow[]>(() => (this.state.data()?.analytics.bookedVsUsed ?? []).map((row) => ({ label: row.label, rezervasyon: row.booked, kullanilan: row.used })));
  readonly roomUsage = computed<ChartRow[]>(() => (this.state.data()?.analytics.roomUsage ?? []).map((row) => ({ label: row.name, oran: Number(row.usageRate.toFixed(1)) })));
  readonly distribution = computed<ChartRow[]>(() => (this.state.data()?.analytics.fairness.distribution ?? []).map((row) => ({ label: row.bucket, calisan: row.employees })));
  readonly popularShare = computed<ChartRow[]>(() => (this.state.data()?.analytics.fairness.popularShare ?? []).map((row) => ({ label: row.bucket, calisan: row.employees })));
  readonly leastUsed = computed(() => [...(this.state.data()?.analytics.deskHeatmap ?? [])].reverse().slice(0, 6));
  readonly heatmapFloors = computed<HeatmapFloor[]>(() => this.state.data()?.context.planFloors ?? []);
  readonly heatmapCounts = computed(() => Object.fromEntries((this.state.data()?.analytics.deskHeatmap ?? []).map((desk) => [desk.deskId, desk.reservations])));

  readonly activePreset = computed(() => {
    const d = this.state.data();
    if (!d) return null;
    const today = todayKey();
    return PRESETS.find((preset) => d.analytics.filters.fromDayKey === addDaysToKey(today, -preset.days) && d.analytics.filters.toDayKey === today)?.key ?? null;
  });

  constructor() {
    effect(() => {
      this.query();
      void this.load(untracked(() => this.state.data()) !== null);
    });
  }

  private buildQuery(overrides?: Partial<Record<string, string>>): Record<string, string> {
    const params: Record<string, string> = {};
    params['from'] = overrides?.['from'] ?? this.fFrom;
    params['to'] = overrides?.['to'] ?? this.fTo;
    const floor = overrides?.['floor'] ?? this.fFloor;
    const department = overrides?.['department'] ?? this.fDepartment;
    const resource = overrides?.['resource'] ?? this.fResource;
    if (floor) params['floor'] = floor;
    if (department) params['department'] = department;
    if (resource && resource !== 'ALL') params['resource'] = resource;
    return params;
  }

  exportHref(): string {
    const params = new URLSearchParams({ type: this.exportType, ...this.buildQuery() });
    return `/api/analytics/export?${params.toString()}`;
  }

  load(silent = false): Promise<unknown> {
    const q = this.query();
    return this.state.load(
      async () => {
        const [analytics, context] = await Promise.all([
          this.api.get<AnalyticsResult>('analytics', { from: q.from, to: q.to, floor: q.floor, department: q.department, resource: q.resource }),
          this.api.get<AnalyticsContext>('analytics/context'),
        ]);
        // Kaynak: geçersiz aralık verilmişse sunucu varsayılan aralığı kullanır ve ekranda uyarı gösterilir.
        const validRange = !(q.from && q.to && isValidDayKey(q.from) && isValidDayKey(q.to) && (q.from > q.to || q.from !== analytics.filters.fromDayKey));
        this.fFrom = analytics.filters.fromDayKey;
        this.fTo = analytics.filters.toDayKey;
        this.fFloor = q.floor ?? '';
        this.fDepartment = q.department ?? '';
        this.fResource = q.resource ?? 'ALL';
        return { analytics, context, validRange };
      },
      { silent },
    );
  }

  apply(overrides?: Partial<Record<string, string>>): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: this.buildQuery(overrides) });
  }

  applyPreset(days: number): void {
    const today = todayKey();
    const start = addDaysToKey(today, -days);
    this.fFrom = start;
    this.fTo = today;
    this.apply({ from: start, to: today });
  }

  resetFilters(): void {
    this.fFloor = '';
    this.fDepartment = '';
    this.fResource = 'ALL';
    this.apply({ floor: '', department: '', resource: 'ALL' });
  }
}
