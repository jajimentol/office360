import { Component, computed, inject } from '@angular/core';
import { formatPercent, ratio } from '../../lib/utils';
import { ApiClient } from '../../core/api/api-client';
import type { PersonalSummary } from '../../core/api/types';
import {
  PageState,
  UiAlert,
  UiBadge,
  UiCard,
  UiCardBody,
  UiCardHeader,
  UiEmptyState,
  UiIcon,
  UiPageError,
  UiPageHeader,
  UiPageLoading,
  UiProgress,
  UiStatCard,
} from '../../shared/ui';
import { UiComparisonChart, UiTrendChart } from '../../shared/charts/chart-kit';

/** Kaynak: (app)/panel/kullanimim/page.tsx */
@Component({
  selector: 'app-usage-page',
  imports: [UiPageHeader, UiPageLoading, UiPageError, UiStatCard, UiCard, UiCardHeader, UiCardBody, UiProgress, UiAlert, UiEmptyState, UiBadge, UiIcon, UiTrendChart, UiComparisonChart],
  template: `
    <ui-page-header title="Kullanım özetim" description="Ofis günleriniz, tercihleriniz ve adil kullanım hakkınız. Bu veriler yalnızca size gösterilir." />

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as summary) {
      @let fairUse = summary.fairUse;
      @let totals = summary.totals;
      <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <ui-stat-card label="Toplam ofis günü" [value]="String(totals.deskReservations)" hint="Tüm zamanlar" hasIcon>
          <svg uiIcon icon name="CalendarCheck" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Son 30 gün" [value]="String(totals.last30Days)" hint="Ofiste geçirdiğiniz gün sayısı" tone="accent" hasIcon>
          <svg uiIcon icon name="TrendingUp" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Toplantı rezervasyonu" [value]="String(totals.roomReservations)" hint="Organize ettiğiniz toplantılar" hasIcon>
          <svg uiIcon icon name="Sparkles" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Kullanılmayan rezervasyon" [value]="formatPercent(totals.noShowRate)" [hint]="totals.noShow + ' rezervasyon kullanılmadı'" [tone]="totals.noShowRate > 15 ? 'warning' : 'success'" hasIcon>
          <svg uiIcon icon name="XCircle" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
      </div>

      <div class="mt-5 grid gap-5 lg:grid-cols-3">
        <div ui-card class="lg:col-span-2">
          <ui-card-header title="Adil kullanım durumunuz" description="Limitler yönetim tarafından belirlenir ve herkese aynı şekilde uygulanır." hasIcon>
            <svg uiIcon icon name="Scale" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body class="grid gap-5 sm:grid-cols-2">
            <div>
              <div class="flex items-end justify-between">
                <p class="text-sm text-ink-500">Bu haftaki masa hakkı</p>
                <p class="text-sm font-semibold text-ink-900">{{ fairUse.weeklyUsed }} / {{ fairUse.weeklyLimit }}</p>
              </div>
              <ui-progress class="mt-2" [value]="ratio(fairUse.weeklyUsed, fairUse.weeklyLimit)" [tone]="fairUse.weeklyRemaining === 0 ? 'warning' : 'brand'" label="Haftalık masa hakkı" />
              <p class="mt-1.5 text-xs text-ink-500">Kalan hak: {{ fairUse.weeklyRemaining }} rezervasyon</p>
            </div>

            <div>
              <div class="flex items-end justify-between">
                <p class="text-sm text-ink-500">Popüler masa kullanım payı</p>
                <p class="text-sm font-semibold text-ink-900">{{ fairUse.popularUsed }} / {{ fairUse.popularMaxUsage }}</p>
              </div>
              <ui-progress class="mt-2" [value]="ratio(fairUse.popularUsed, Math.max(fairUse.popularMaxUsage, 1))" [tone]="fairUse.popularRestricted ? 'danger' : 'accent'" label="Popüler masa kullanım payı" />
              <p class="mt-1.5 text-xs text-ink-500">Son {{ fairUse.popularWindowSize }} rezervasyondaki popüler masa sayınız</p>
            </div>

            <div class="sm:col-span-2">
              @if (fairUse.popularRestricted) {
                <ui-alert tone="warning" title="Popüler masa seçimi geçici olarak sınırlı">
                  Ortak alanların adil dağılımı için popüler masa hakkınız geçici olarak kapatıldı. Standart bir masa seçtiğinizde bu hak otomatik olarak yeniden açılır.
                </ui-alert>
              } @else {
                <ui-alert tone="success" title="Adil kullanım sınırları içindesiniz">
                  Popüler masa kullanımınız tanımlı sınırın altında; tüm masaları seçebilirsiniz.
                </ui-alert>
              }
            </div>
          </ui-card-body>
        </div>

        <div ui-card>
          <ui-card-header title="Kat tercihiniz" />
          <ui-card-body>
            @if (summary.floorPreference.length === 0) {
              <ui-empty-state title="Henüz rezervasyon geçmişiniz yok" />
            } @else {
              <ul class="space-y-3">
                @for (floor of summary.floorPreference; track floor.name) {
                  @let share = ratio(floor.count, totals.deskReservations);
                  <li>
                    <div class="flex items-center justify-between text-sm">
                      <span class="text-ink-700">{{ floor.name }}</span>
                      <span class="font-semibold text-ink-900">{{ formatPercent(share) }}</span>
                    </div>
                    <ui-progress class="mt-1.5" [value]="share" [label]="floor.name + ' kullanım payı'" />
                    <p class="mt-1 text-xs text-ink-500">{{ floor.count }} rezervasyon</p>
                  </li>
                }
              </ul>
            }
          </ui-card-body>
        </div>
      </div>

      <div class="mt-5 grid gap-5 lg:grid-cols-2">
        <ui-trend-chart title="Haftalara göre ofis günleriniz" description="Son 12 hafta" [data]="trendData()" [series]="[{ key: 'days', label: 'Ofis günü' }]" [valueFormat]="{ suffix: 'gün' }" />
        <ui-comparison-chart title="Günlere göre dağılımınız" description="Hangi günler ofiste olmayı tercih ediyorsunuz?" [data]="weekdayData()" [series]="[{ key: 'count', label: 'Rezervasyon' }]" [valueFormat]="{ suffix: 'rezervasyon' }" />
      </div>

      <div ui-card class="mt-5">
        <ui-card-header title="En sık kullandığınız masalar" description="Adil kullanım göstergeleriyle birlikte" />
        <ui-card-body>
          @if (summary.favoriteDesks.length === 0) {
            <ui-empty-state title="Henüz masa kullanım geçmişiniz yok" />
          } @else {
            <ul class="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
              @for (desk of summary.favoriteDesks; track desk.code) {
                <li class="rounded-xl border border-line p-3.5">
                  <div class="flex items-center justify-between">
                    <p class="text-sm font-semibold text-ink-900">{{ desk.code }}</p>
                    @if (desk.popular) {
                      <span ui-badge tone="warning">★ Popüler</span>
                    }
                  </div>
                  <p class="mt-1 truncate text-xs text-ink-500">{{ desk.zoneName }}</p>
                  <p class="mt-2 text-2xl font-semibold tracking-tight text-ink-900">{{ desk.count }}</p>
                  <p class="text-xs text-ink-500">kez kullandınız</p>
                </li>
              }
            </ul>
          }
        </ui-card-body>
      </div>
    }
  `,
})
export class UsagePage {
  private readonly api = inject(ApiClient);
  readonly state = new PageState<PersonalSummary>();
  readonly Math = Math;
  readonly String = String;
  readonly formatPercent = formatPercent;
  readonly ratio = ratio;

  readonly trendData = computed(() => (this.state.data()?.weeklyTrend ?? []).map((item) => ({ label: item.label, days: item.days })));
  readonly weekdayData = computed(() => (this.state.data()?.weekdayDistribution ?? []).map((item) => ({ label: item.label, count: item.count })));

  constructor() {
    void this.load();
  }

  load(): Promise<unknown> {
    return this.state.load(() => this.api.get<PersonalSummary>('me/usage'));
  }
}
