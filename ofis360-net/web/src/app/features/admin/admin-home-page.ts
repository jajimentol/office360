import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatLongDate, relativeTimeLabel, todayKey } from '../../lib/date';
import { AUDIT_ACTION_LABELS, CLOSURE_SCOPE_LABELS, type ClosureScope } from '../../lib/enums';
import { formatPercent, ratio } from '../../lib/utils';
import { ApiClient } from '../../core/api/api-client';
import type { RuleSettings } from '../../core/api/employee-context';
import type { AuditLogDto, ClosureDto, FloorSummary, Paged, UserStats } from '../../core/api/types';
import {
  PageState,
  UiAvatar,
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
  type IconName,
} from '../../shared/ui';

/** Kaynak: (app)/admin/page.tsx */

const QUICK_LINKS: Array<{ href: string; label: string; icon: IconName; description: string }> = [
  { href: '/admin/katlar', label: 'Kat ve alan yönetimi', icon: 'Building2', description: 'Kat planı yerleşimi ve bölgeler' },
  { href: '/admin/masalar', label: 'Masa yönetimi', icon: 'Grid2x2', description: 'Masa ekleme, özellik ve kapatma' },
  { href: '/admin/odalar', label: 'Toplantı odaları', icon: 'DoorOpen', description: 'Kapasite ve donanım yönetimi' },
  { href: '/admin/rezervasyonlar', label: 'Rezervasyon yönetimi', icon: 'ClipboardList', description: 'Tüm rezervasyonlar ve iptaller' },
  { href: '/admin/kullanicilar', label: 'Kullanıcı ve roller', icon: 'Users', description: 'Hesaplar ve yetkiler' },
  { href: '/admin/kurallar', label: 'Rezervasyon kuralları', icon: 'Scale', description: 'Adil kullanım limitleri' },
  { href: '/admin/kapatmalar', label: 'Bakım ve kapatmalar', icon: 'Wrench', description: 'Geçici kullanım kısıtları' },
  { href: '/admin/audit', label: 'Denetim kayıtları', icon: 'FileClock', description: 'Sistem hareket geçmişi' },
  { href: '/admin/ayarlar', label: 'Sistem ayarları', icon: 'Settings', description: 'Marka, mesai ve duyurular' },
];

interface DashboardData {
  floors: FloorSummary[];
  deskCount: number;
  inactiveDeskCount: number;
  roomCount: number;
  userCount: number;
  activeClosures: ClosureDto[];
  audit: AuditLogDto[];
  rules: RuleSettings;
}

@Component({
  selector: 'app-admin-home-page',
  imports: [RouterLink, UiPageHeader, UiPageLoading, UiPageError, UiStatCard, UiCard, UiCardHeader, UiCardBody, UiBadge, UiProgress, UiEmptyState, UiAvatar, UiIcon],
  template: `
    <ui-page-header title="Admin panosu" [description]="formatLongDate(today) + ' · sistem durumu ve yönetim kısayolları'" />

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as d) {
      <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <ui-stat-card label="Bugünkü doluluk" [value]="formatPercent(ratio(todayReservations(), totalReservable()))" [hint]="todayReservations() + ' / ' + totalReservable() + ' masa'" hasIcon>
          <svg uiIcon icon name="Grid2x2" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Tanımlı masa" [value]="String(d.deskCount)" [hint]="d.inactiveDeskCount + ' masa kullanıma kapalı'" [tone]="d.inactiveDeskCount > 0 ? 'warning' : 'brand'" hasIcon>
          <svg uiIcon icon name="Layers" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Toplantı odası" [value]="String(d.roomCount)" [hint]="d.floors.length + ' kat'" tone="accent" hasIcon>
          <svg uiIcon icon name="DoorOpen" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
        <ui-stat-card label="Aktif kullanıcı" [value]="String(d.userCount)" hint="Sistemde tanımlı çalışan" hasIcon>
          <svg uiIcon icon name="Users" class="h-4.5 w-4.5"></svg>
        </ui-stat-card>
      </div>

      <div class="mt-5 grid gap-5 lg:grid-cols-3">
        <div ui-card class="lg:col-span-2">
          <ui-card-header title="Kat doluluk özeti" description="Bugünkü kullanım durumu" hasIcon>
            <svg uiIcon icon name="Building2" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body>
            <ul class="space-y-4">
              @for (floor of d.floors; track floor.id) {
                <li>
                  <div class="flex flex-wrap items-center justify-between gap-2">
                    <div>
                      <p class="text-sm font-semibold text-ink-900">{{ floor.name }}</p>
                      <p class="text-xs text-ink-500">{{ floor.description }}</p>
                    </div>
                    <div class="flex items-center gap-2">
                      @if (!floor.reservable) {
                        <span ui-badge tone="neutral">Rezervasyona kapalı</span>
                      } @else {
                        <span ui-badge [tone]="floor.occupancyRate > 85 ? 'danger' : floor.occupancyRate > 60 ? 'warning' : 'success'">{{ formatPercent(floor.occupancyRate) }}</span>
                      }
                      <span class="text-xs text-ink-500">{{ floor.reservedDesks }} / {{ floor.totalDesks }} masa</span>
                    </div>
                  </div>
                  @if (floor.reservable) {
                    <ui-progress class="mt-2" [value]="floor.occupancyRate" [tone]="floor.occupancyRate > 85 ? 'danger' : 'brand'" [label]="floor.name + ' doluluk'" />
                  }
                </li>
              }
            </ul>
          </ui-card-body>
        </div>

        <div ui-card>
          <ui-card-header title="Aktif kapatmalar" [description]="d.activeClosures.length + ' kayıt'" hasIcon>
            <svg uiIcon icon name="Wrench" class="h-4.5 w-4.5"></svg>
            <a action routerLink="/admin/kapatmalar" class="text-[13px] font-medium text-brand-600 hover:text-brand-700">Yönet</a>
          </ui-card-header>
          <ui-card-body>
            @if (d.activeClosures.length === 0) {
              <ui-empty-state title="Aktif kapatma yok" description="Tüm alanlar kullanıma açık." />
            } @else {
              <ul class="space-y-2.5">
                @for (closure of d.activeClosures.slice(0, 5); track closure.id) {
                  <li class="rounded-lg border border-line px-3 py-2.5">
                    <div class="flex items-center justify-between gap-2">
                      <p class="text-sm font-medium text-ink-900">{{ closure.desk?.code ?? closure.room?.name ?? closure.floor?.name ?? 'Bilinmiyor' }}</p>
                      <span ui-badge tone="warning">{{ scopeLabel(closure.scope) }}</span>
                    </div>
                    <p class="mt-1 text-xs text-ink-500">{{ closure.note }}</p>
                  </li>
                }
              </ul>
            }
          </ui-card-body>
        </div>
      </div>

      <div class="mt-5 grid gap-5 lg:grid-cols-3">
        <div ui-card class="lg:col-span-2">
          <ui-card-header title="Yönetim kısayolları" description="Tüm admin ekranlarına hızlı erişim" />
          <ui-card-body>
            <ul class="grid gap-2.5 sm:grid-cols-2">
              @for (link of quickLinks; track link.href) {
                <li>
                  <a [routerLink]="link.href" class="flex items-start gap-3 rounded-xl border border-line p-3.5 transition-colors hover:border-brand-200 hover:bg-brand-50/40">
                    <span class="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand-600">
                      <svg uiIcon [name]="link.icon" class="h-4.5 w-4.5"></svg>
                    </span>
                    <span class="min-w-0">
                      <span class="block text-sm font-medium text-ink-900">{{ link.label }}</span>
                      <span class="block text-xs text-ink-500">{{ link.description }}</span>
                    </span>
                  </a>
                </li>
              }
            </ul>
          </ui-card-body>
        </div>

        <div ui-card>
          <ui-card-header title="Son sistem hareketleri" hasIcon>
            <svg uiIcon icon name="FileClock" class="h-4.5 w-4.5"></svg>
            <a action routerLink="/admin/audit" class="text-[13px] font-medium text-brand-600 hover:text-brand-700">Tümü</a>
          </ui-card-header>
          <ui-card-body>
            <ul class="space-y-3">
              @for (log of d.audit; track log.id) {
                <li class="flex gap-2.5">
                  <ui-avatar [name]="log.actorName" size="sm" />
                  <div class="min-w-0 flex-1">
                    <p class="text-[13px] leading-snug text-ink-700">{{ log.summary }}</p>
                    <p class="mt-0.5 text-xs text-ink-400">{{ auditLabels[log.action] ?? log.action }} · {{ relative(log.createdAt) }}</p>
                  </div>
                </li>
              }
            </ul>
          </ui-card-body>
        </div>
      </div>

      <div ui-card class="mt-5">
        <ui-card-header title="Aktif adil kullanım kuralları" description="Değişiklikler anında tüm yeni rezervasyonlara uygulanır" hasIcon>
          <svg uiIcon icon name="Scale" class="h-4.5 w-4.5"></svg>
          <a action routerLink="/admin/kurallar" class="text-[13px] font-medium text-brand-600 hover:text-brand-700">Kuralları düzenle</a>
        </ui-card-header>
        <ui-card-body>
          <dl class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <div class="rounded-lg bg-slate-50 px-3.5 py-3">
              <dt class="text-xs text-ink-500">Haftalık masa limiti</dt>
              <dd class="mt-1 text-lg font-semibold text-ink-900">{{ d.rules.weeklyDeskLimit.enabled ? d.rules.weeklyDeskLimit.limit : 'Kapalı' }}</dd>
            </div>
            <div class="rounded-lg bg-slate-50 px-3.5 py-3">
              <dt class="text-xs text-ink-500">Önceden rezervasyon</dt>
              <dd class="mt-1 text-lg font-semibold text-ink-900">{{ d.rules.advanceBookingDays.days }} iş günü</dd>
            </div>
            <div class="rounded-lg bg-slate-50 px-3.5 py-3">
              <dt class="text-xs text-ink-500">Popüler masa serisi</dt>
              <dd class="mt-1 text-lg font-semibold text-ink-900">{{ d.rules.popularDeskStreakLimit.enabled ? d.rules.popularDeskStreakLimit.limit + ' gün' : 'Kapalı' }}</dd>
            </div>
            <div class="rounded-lg bg-slate-50 px-3.5 py-3">
              <dt class="text-xs text-ink-500">Popüler masa payı</dt>
              <dd class="mt-1 text-lg font-semibold text-ink-900">{{ d.rules.popularShareLimit.enabled ? d.rules.popularShareLimit.maxPopular + ' / ' + d.rules.popularShareLimit.windowSize : 'Kapalı' }}</dd>
            </div>
          </dl>
        </ui-card-body>
      </div>
    }
  `,
})
export class AdminHomePage {
  private readonly api = inject(ApiClient);
  readonly state = new PageState<DashboardData>();
  readonly today = todayKey();
  readonly String = String;
  readonly formatLongDate = formatLongDate;
  readonly formatPercent = formatPercent;
  readonly ratio = ratio;
  readonly quickLinks = QUICK_LINKS;
  readonly auditLabels = AUDIT_ACTION_LABELS;

  readonly totalReservable = computed(() => (this.state.data()?.floors ?? []).filter((f) => f.reservable).reduce((sum, f) => sum + f.totalDesks, 0));
  readonly todayReservations = computed(() => (this.state.data()?.floors ?? []).reduce((sum, f) => sum + f.reservedDesks, 0));

  constructor() {
    void this.load();
  }

  scopeLabel(scope: string): string {
    return CLOSURE_SCOPE_LABELS[scope as ClosureScope] ?? scope;
  }

  relative(iso: string): string {
    return relativeTimeLabel(new Date(iso));
  }

  load(): Promise<unknown> {
    return this.state.load(async () => {
      const [floors, desks, inactive, rooms, stats, closures, audit, rules] = await Promise.all([
        this.api.get<FloorSummary[]>('floors/summaries', { date: this.today }),
        this.api.get<Paged<unknown>>('admin/desks', { take: 1 }),
        this.api.get<Paged<unknown>>('admin/desks', { take: 1, onlyInactive: true }),
        this.api.get<unknown[]>('admin/rooms'),
        this.api.get<UserStats>('admin/users/stats'),
        this.api.get<ClosureDto[]>('admin/closures', { active: true }),
        this.api.get<Paged<AuditLogDto>>('admin/audit', { take: 8 }),
        this.api.get<RuleSettings>('admin/rules/settings'),
      ]);
      return {
        floors,
        deskCount: desks.total,
        inactiveDeskCount: inactive.total,
        roomCount: rooms.length,
        userCount: stats.active,
        activeClosures: closures,
        audit: audit.items,
        rules,
      };
    });
  }
}
