import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { addDaysToKey, formatLongDate, isWeekend, minutesToTime, weekdayLabel } from '../../lib/date';
import { formatPercent, ratio } from '../../lib/utils';
import { ApiClient } from '../../core/api/api-client';
import type { HomeResponse } from '../../core/api/types';
import { AuthService } from '../../core/auth/auth.service';
import {
  PageState,
  UiAlert,
  UiBadge,
  UiButton,
  UiCard,
  UiCardBody,
  UiCardHeader,
  UiEmptyState,
  UiIcon,
  UiPageError,
  UiPageHeader,
  UiPageLoading,
  UiProgress,
} from '../../shared/ui';
import { CheckInButton, CheckedInBadge, RoomStartButton } from '../../shared/reservations/reservation-actions';

/** Kaynak: (app)/panel/page.tsx — çalışan ana sayfası. */

/** Bir sonraki iş gününü döndürür (hafta sonlarını atlar). */
function nextWorkday(fromKey: string): string {
  let candidate = addDaysToKey(fromKey, 1);
  while (isWeekend(candidate)) candidate = addDaysToKey(candidate, 1);
  return candidate;
}

@Component({
  selector: 'app-home-page',
  imports: [
    RoomStartButton,
    RouterLink,
    UiPageHeader,
    UiPageLoading,
    UiPageError,
    UiCard,
    UiCardHeader,
    UiCardBody,
    UiEmptyState,
    UiAlert,
    UiBadge,
    UiButton,
    UiProgress,
    UiIcon,
    CheckInButton,
    CheckedInBadge,
  ],
  template: `
    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as summary) {
      @let user = auth.user()!;
      @let today = summary.today;
      @let suggestedDay = nextWorkday(today);
      @let fairUse = summary.fairUse;
      <ui-page-header [title]="'Merhaba ' + user.fullName.split(' ')[0]" [description]="formatLongDate(today) + ' · ' + user.title + ' · ' + user.departmentName">
        <ng-container actions>
          <a ui-button variant="secondary" routerLink="/panel/toplanti" [queryParams]="{ date: suggestedDay }">
            <svg uiIcon name="DoorOpen" class="h-4 w-4"></svg>
            Toplantı odası
          </a>
          <a ui-button routerLink="/panel/masa" [queryParams]="{ date: suggestedDay }">
            <svg uiIcon name="CalendarPlus" class="h-4 w-4"></svg>
            Masa rezerve et
          </a>
        </ng-container>
      </ui-page-header>

      <div class="grid gap-5 lg:grid-cols-3">
        <!-- Bugün ofiste misin? -->
        <div ui-card class="lg:col-span-2">
          <ui-card-header title="Bugün ofiste misin?" [description]="weekdayLabel(today) + ' günü için durumunuz'" hasIcon>
            <svg uiIcon icon name="MapPin" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body>
            @if (summary.todayDesk; as todayDesk) {
              <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div class="flex items-start gap-4">
                  <div class="flex h-14 w-14 shrink-0 flex-col items-center justify-center rounded-xl bg-brand-600 text-white">
                    <span class="text-[10px] uppercase tracking-wide opacity-80">Masa</span>
                    <span class="text-sm font-semibold">{{ todayDesk.desk?.code }}</span>
                  </div>
                  <div>
                    <p class="text-sm font-semibold text-ink-900">{{ todayDesk.desk?.floor?.name }} · {{ todayDesk.desk?.zone?.name }}</p>
                    <p class="mt-0.5 text-sm text-ink-500">Bugün için masanız hazır. Ofise geldiğinizde giriş kaydı oluşturabilirsiniz.</p>
                    <div class="mt-2 flex flex-wrap gap-1.5">
                      @if (todayDesk.desk?.zone?.windowSide) {
                        <span ui-badge tone="accent">Pencere kenarı</span>
                      }
                      @if (todayDesk.desk?.zone?.quiet) {
                        <span ui-badge tone="brand">Sessiz alan</span>
                      }
                    </div>
                  </div>
                </div>
                <div class="shrink-0">
                  @if (todayDesk.status === 'CHECKED_IN') {
                    <app-checked-in-badge />
                  } @else {
                    <app-check-in-button [reservationId]="todayDesk.id" (checkedIn)="refresh()" />
                  }
                </div>
              </div>
            } @else {
              <ui-empty-state hasIcon title="Bugün için masa rezervasyonunuz yok" description="Ofise gelmeyi planlıyorsanız kat planından uygun bir masa seçebilirsiniz.">
                <svg uiIcon icon name="Building2" class="h-6 w-6"></svg>
                <a action ui-button routerLink="/panel/masa" [queryParams]="{ date: today }">
                  Bugün için masa seç
                  <svg uiIcon name="ArrowRight" class="h-4 w-4"></svg>
                </a>
              </ui-empty-state>
            }

            @if (summary.todayRooms.length > 0) {
              <div class="mt-5 border-t border-line pt-4">
                <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-400">Bugünkü toplantılarınız</p>
                <ul class="space-y-2">
                  @for (reservation of summary.todayRooms; track reservation.id) {
                    <li class="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-slate-50 px-3 py-2">
                      <span class="text-sm font-medium text-ink-900">{{ reservation.title }}</span>
                      <span class="text-[13px] text-ink-500">{{ reservation.room?.name }} · {{ minutesToTime(reservation.startMinutes) }} - {{ minutesToTime(reservation.endMinutes) }}</span>
                      @if (reservation.startedAt) {
                        <span class="text-xs font-medium text-emerald-700">Toplantı başladı</span>
                      } @else if (canStart(reservation)) {
                        <app-room-start-button [reservationId]="reservation.id" (started)="refresh()" />
                      }
                    </li>
                  }
                </ul>
              </div>
            }
          </ui-card-body>
        </div>

        <!-- Adil kullanım hakkı -->
        <div ui-card>
          <ui-card-header title="Adil kullanım hakkınız" description="Bu hafta kalan rezervasyon hakkı" hasIcon>
            <svg uiIcon icon name="Scale" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body class="space-y-4">
            <div>
              <div class="flex items-end justify-between">
                <p class="text-sm text-ink-500">Haftalık masa hakkı</p>
                <p class="text-sm font-semibold text-ink-900">{{ fairUse.weeklyUsed }} / {{ fairUse.weeklyLimit }}</p>
              </div>
              <ui-progress class="mt-2" [value]="ratio(fairUse.weeklyUsed, fairUse.weeklyLimit)" [tone]="fairUse.weeklyRemaining === 0 ? 'warning' : 'brand'" label="Haftalık masa rezervasyon hakkı kullanımı" />
              <p class="mt-1.5 text-xs text-ink-500">
                {{ fairUse.weeklyRemaining > 0 ? 'Bu hafta ' + fairUse.weeklyRemaining + ' rezervasyon hakkınız kaldı.' : 'Bu haftaki hakkınızı kullandınız. Gelecek hafta için rezervasyon oluşturabilirsiniz.' }}
              </p>
            </div>

            <div>
              <div class="flex items-end justify-between">
                <p class="text-sm text-ink-500">Popüler masa kullanımı</p>
                <p class="text-sm font-semibold text-ink-900">{{ fairUse.popularUsed }} / {{ fairUse.popularMaxUsage }}</p>
              </div>
              <ui-progress class="mt-2" [value]="ratio(fairUse.popularUsed, Math.max(fairUse.popularMaxUsage, 1))" [tone]="fairUse.popularRestricted ? 'danger' : 'accent'" label="Popüler masa kullanım payı" />
              <p class="mt-1.5 text-xs text-ink-500">Son {{ fairUse.popularWindowSize }} rezervasyonun en fazla {{ fairUse.popularMaxUsage }} tanesi pencere yanı / popüler masada olabilir.</p>
            </div>

            @if (fairUse.popularRestricted) {
              <ui-alert tone="warning" hasIcon>
                <svg uiIcon icon name="Info" class="h-4 w-4"></svg>
                Popüler masa kullanımınız geçici olarak sınırlandırıldı. Standart bir masa seçtiğinizde bu hak yeniden açılır.
              </ui-alert>
            }

            <p class="text-xs text-ink-400">Rezervasyonlar en fazla {{ fairUse.advanceDays }} iş günü öncesinden oluşturulabilir.</p>
          </ui-card-body>
        </div>
      </div>

      <!-- Yaklaşan rezervasyon + doluluk -->
      <div class="mt-5 grid gap-5 lg:grid-cols-3">
        <div ui-card>
          <ui-card-header title="Yaklaşan rezervasyonunuz" hasIcon>
            <svg uiIcon icon name="CalendarPlus" class="h-4.5 w-4.5"></svg>
            <a action routerLink="/panel/rezervasyonlarim" class="text-[13px] font-medium text-brand-600 hover:text-brand-700">Tümü</a>
          </ui-card-header>
          <ui-card-body>
            @if (summary.nextDesk || summary.nextRoom) {
              <div class="space-y-3">
                @if (summary.nextDesk; as nextDesk) {
                  <div>
                    <p class="text-sm font-semibold text-ink-900">{{ formatLongDate(nextDesk.date) }}</p>
                    <p class="mt-0.5 text-sm text-ink-500">{{ nextDesk.desk?.code }} · {{ nextDesk.desk?.floor?.name }} · {{ nextDesk.desk?.zone?.name }}</p>
                  </div>
                }
                @if (summary.nextRoom; as nextRoom) {
                  <div class="rounded-lg bg-slate-50 px-3 py-2">
                    <p class="text-xs font-semibold uppercase tracking-wide text-ink-400">Yaklaşan toplantı</p>
                    <p class="mt-1 text-sm text-ink-700">{{ nextRoom.title }} · {{ nextRoom.room?.name }}</p>
                    <p class="text-xs text-ink-500">{{ formatLongDate(nextRoom.date, { withWeekday: false }) }} {{ minutesToTime(nextRoom.startMinutes) }} - {{ minutesToTime(nextRoom.endMinutes) }}</p>
                  </div>
                }
              </div>
            } @else {
              <ui-empty-state title="Yaklaşan rezervasyonunuz yok" description="Gelecek günler için masa veya toplantı odası ayırabilirsiniz.">
                <a action ui-button size="sm" variant="secondary" routerLink="/panel/masa" [queryParams]="{ date: suggestedDay }">
                  {{ formatLongDate(suggestedDay, { withWeekday: false }) }} için planla
                </a>
              </ui-empty-state>
            }
          </ui-card-body>
        </div>

        <div ui-card class="lg:col-span-2">
          <ui-card-header title="Bugünkü ofis doluluğu" [description]="todayReservationCount() + ' / ' + totalActiveDesks() + ' masa kullanımda'" hasIcon>
            <svg uiIcon icon name="Users" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body>
            <div class="mb-5 flex items-center gap-4">
              <div class="text-3xl font-semibold tracking-tight text-ink-900">{{ formatPercent(occupancy()) }}</div>
              <ui-progress [value]="occupancy()" [tone]="occupancy() > 85 ? 'warning' : 'brand'" label="Bugünkü genel doluluk oranı" />
            </div>

            <ul class="grid gap-3 sm:grid-cols-3">
              @for (floor of reservableFloors(); track floor.id) {
                <li class="rounded-xl border border-line p-3.5">
                  <div class="flex items-center justify-between">
                    <p class="text-sm font-semibold text-ink-900">{{ floor.buildingName }} · {{ floor.name }}</p>
                    <span ui-badge [tone]="floor.occupancyRate > 85 ? 'danger' : floor.occupancyRate > 60 ? 'warning' : 'success'">{{ formatPercent(floor.occupancyRate) }}</span>
                  </div>
                  <ui-progress class="mt-2.5" [value]="floor.occupancyRate" [tone]="floor.occupancyRate > 85 ? 'danger' : 'brand'" [label]="floor.name + ' doluluk oranı'" />
                  <p class="mt-2 text-xs text-ink-500">{{ floor.availableDesks }} masa uygun · {{ floor.roomCount }} toplantı odası</p>
                  <a routerLink="/panel/masa" [queryParams]="{ date: today, floor: floor.id }" class="mt-2 inline-flex items-center gap-1 text-[13px] font-medium text-brand-600 hover:text-brand-700">
                    Kat planını aç
                    <svg uiIcon name="ArrowRight" class="h-3.5 w-3.5"></svg>
                  </a>
                </li>
              }
            </ul>

            @if (closedFloorNames()) {
              <p class="mt-4 text-xs text-ink-400">{{ closedFloorNames() }} çalışan rezervasyonuna kapalıdır.</p>
            }
          </ui-card-body>
        </div>
      </div>

      <!-- Duyuru + hızlı işlemler -->
      <div class="mt-5 grid gap-5 lg:grid-cols-3">
        <div ui-card class="lg:col-span-2">
          <ui-card-header title="Sistem duyuruları" hasIcon>
            <svg uiIcon icon name="Megaphone" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body>
            @if (summary.announcement) {
              <ui-alert tone="info" hasIcon>
                <svg uiIcon icon name="Megaphone" class="h-4 w-4"></svg>
                {{ summary.announcement }}
              </ui-alert>
            } @else {
              <ui-empty-state title="Şu anda aktif duyuru yok" />
            }
            <p class="mt-3 text-xs text-ink-400">Duyurular yönetim tarafından sistem ayarları ekranından güncellenir.</p>
          </ui-card-body>
        </div>

        <div ui-card>
          <ui-card-header title="Hızlı işlemler" hasIcon>
            <svg uiIcon icon name="Sparkles" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body class="space-y-2.5">
            <a routerLink="/panel/masa" [queryParams]="{ date: suggestedDay }" class="flex items-center justify-between rounded-lg border border-line px-3.5 py-3 transition-colors hover:border-brand-200 hover:bg-brand-50/50">
              <span>
                <span class="block text-sm font-medium text-ink-900">Masa rezerve et</span>
                <span class="block text-xs text-ink-500">{{ formatLongDate(suggestedDay, { withWeekday: false }) }} · kat planından seç</span>
              </span>
              <svg uiIcon name="ArrowRight" class="h-4 w-4 text-ink-400"></svg>
            </a>
            <a routerLink="/panel/toplanti" [queryParams]="{ date: suggestedDay }" class="flex items-center justify-between rounded-lg border border-line px-3.5 py-3 transition-colors hover:border-brand-200 hover:bg-brand-50/50">
              <span>
                <span class="block text-sm font-medium text-ink-900">Toplantı odası ayır</span>
                <span class="block text-xs text-ink-500">Saat bazında uygunluk takvimi</span>
              </span>
              <svg uiIcon name="ArrowRight" class="h-4 w-4 text-ink-400"></svg>
            </a>
            <a routerLink="/panel/kullanimim" class="flex items-center justify-between rounded-lg border border-line px-3.5 py-3 transition-colors hover:border-brand-200 hover:bg-brand-50/50">
              <span>
                <span class="block text-sm font-medium text-ink-900">Kullanım özetim</span>
                <span class="block text-xs text-ink-500">Ofis günleriniz ve tercihleriniz</span>
              </span>
              <svg uiIcon name="ArrowRight" class="h-4 w-4 text-ink-400"></svg>
            </a>
          </ui-card-body>
        </div>
      </div>
    }
  `,
})
export class HomePage {
  private readonly api = inject(ApiClient);
  readonly auth = inject(AuthService);
  readonly state = new PageState<HomeResponse>();

  readonly Math = Math;
  readonly formatLongDate = formatLongDate;
  readonly weekdayLabel = weekdayLabel;
  readonly minutesToTime = minutesToTime;
  readonly formatPercent = formatPercent;
  readonly ratio = ratio;
  readonly nextWorkday = nextWorkday;

  readonly reservableFloors = computed(() => (this.state.data()?.floors ?? []).filter((floor) => floor.reservable));
  readonly todayReservationCount = computed(() => (this.state.data()?.floors ?? []).reduce((sum, floor) => sum + floor.reservedDesks, 0));
  readonly totalActiveDesks = computed(() => this.reservableFloors().reduce((sum, floor) => sum + floor.totalDesks, 0));
  readonly occupancy = computed(() => ratio(this.todayReservationCount(), this.totalActiveDesks()));
  readonly closedFloorNames = computed(() =>
    (this.state.data()?.floors ?? [])
      .filter((floor) => !floor.reservable)
      .map((floor) => floor.name)
      .join(', '),
  );

  constructor() {
    void this.load();
  }

  load(): Promise<unknown> {
    return this.state.load(() => this.api.get<HomeResponse>('panel/home'));
  }

  canStart(reservation: { status: string; startMinutes: number; endMinutes: number }): boolean {
    const now = new Date();
    const minutes = now.getHours() * 60 + now.getMinutes();
    return reservation.status === 'BOOKED' && minutes >= reservation.startMinutes - 15 && minutes < reservation.endMinutes;
  }

  refresh(): void {
    void this.state.load(() => this.api.get<HomeResponse>('panel/home'), { silent: true });
  }
}
