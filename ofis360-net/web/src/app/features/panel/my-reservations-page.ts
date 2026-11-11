import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { currentMinutesForDay, formatLongDate, minutesToTime, todayKey } from '../../lib/date';
import { RESERVATION_STATUS_LABELS } from '../../lib/enums';
import { cn } from '../../lib/utils';
import { ApiClient } from '../../core/api/api-client';
import type { MyReservations } from '../../core/api/types';
import {
  PageState,
  UiBadge,
  UiButton,
  UiCard,
  UiCardBody,
  UiCardHeader,
  UiDataTable,
  UiEmptyState,
  UiIcon,
  UiPageError,
  UiPageHeader,
  UiPageLoading,
  UiTd,
  type BadgeTone,
} from '../../shared/ui';
import { CancelReservationButton } from '../../shared/reservations/reservation-actions';

/** Kaynak: (app)/panel/rezervasyonlarim/page.tsx */

const STATUS_TONE: Record<string, BadgeTone> = {
  BOOKED: 'brand',
  CHECKED_IN: 'success',
  COMPLETED: 'neutral',
  CANCELLED: 'danger',
  NO_SHOW: 'warning',
};

interface PastRow {
  id: string;
  dayKey: string;
  kind: string;
  resource: string;
  status: string;
  detail: string | null;
}

@Component({
  selector: 'app-my-reservations-page',
  imports: [RouterLink, UiPageHeader, UiPageLoading, UiPageError, UiCard, UiCardHeader, UiCardBody, UiDataTable, UiTd, UiEmptyState, UiBadge, UiButton, UiIcon, CancelReservationButton],
  template: `
    <ui-page-header title="Rezervasyonlarım" description="Yaklaşan, iptal edilen ve geçmiş masa ile toplantı odası rezervasyonlarınız.">
      <ng-container actions>
        <a ui-button variant="secondary" routerLink="/panel/toplanti">
          <svg uiIcon name="DoorOpen" class="h-4 w-4"></svg>
          Toplantı odası ayır
        </a>
        <a ui-button routerLink="/panel/masa">
          <svg uiIcon name="Grid2x2" class="h-4 w-4"></svg>
          Masa rezerve et
        </a>
      </ng-container>
    </ui-page-header>

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as data) {
      <div class="space-y-5">
        <!-- Yaklaşan masa rezervasyonları -->
        <div ui-card>
          <ui-card-header title="Yaklaşan masa rezervasyonları" [description]="data.upcomingDesks.length + ' aktif rezervasyon'" hasIcon>
            <svg uiIcon icon name="CalendarDays" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          @if (data.upcomingDesks.length === 0) {
            <ui-card-body>
              <ui-empty-state hasIcon title="Yaklaşan masa rezervasyonunuz yok" description="Kat planından uygun bir masa seçerek ofis gününüzü planlayabilirsiniz.">
                <svg uiIcon icon name="Grid2x2" class="h-6 w-6"></svg>
                <a action ui-button size="sm" routerLink="/panel/masa">Masa rezerve et</a>
              </ui-empty-state>
            </ui-card-body>
          } @else {
            <ui-data-table caption="Yaklaşan masa rezervasyonları" [headers]="[{ label: 'Tarih' }, { label: 'Masa' }, { label: 'Konum' }, { label: 'Durum' }, { label: '', className: 'text-right' }]">
              @for (reservation of data.upcomingDesks; track reservation.id) {
                <tr class="hover:bg-slate-50/60">
                  <td ui-td>
                    <span class="font-medium text-ink-900">{{ formatLongDate(reservation.date) }}</span>
                    @if (reservation.date === today) {
                      <span class="ml-2 text-xs font-medium text-teal-700">Bugün</span>
                    }
                    @if (reservation.note) {
                      <span class="mt-0.5 block text-xs text-ink-500">{{ reservation.note }}</span>
                    }
                  </td>
                  <td ui-td>
                    <span class="font-semibold text-ink-900">{{ reservation.desk?.code }}</span>
                    @if (reservation.desk?.popular) {
                      <span class="ml-1.5 text-xs text-amber-600" title="Popüler masa">★</span>
                    }
                  </td>
                  <td ui-td>
                    {{ reservation.desk?.floor?.name }}
                    <span class="block text-xs text-ink-500">{{ reservation.desk?.zone?.name }}</span>
                  </td>
                  <td ui-td>
                    <span ui-badge [tone]="statusTone[reservation.status]">{{ statusLabels[reservation.status] }}</span>
                  </td>
                  <td ui-td class="text-right">
                    <app-cancel-reservation-button [reservationId]="reservation.id" kind="desk" (cancelled)="refresh()" />
                  </td>
                </tr>
              }
            </ui-data-table>
          }
        </div>

        <!-- Yaklaşan toplantılar -->
        <div ui-card>
          <ui-card-header title="Yaklaşan toplantı odası rezervasyonları" [description]="data.upcomingRooms.length + ' aktif rezervasyon'" hasIcon>
            <svg uiIcon icon name="DoorOpen" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          @if (data.upcomingRooms.length === 0) {
            <ui-card-body>
              <ui-empty-state hasIcon title="Yaklaşan toplantı rezervasyonunuz yok" description="Toplantı odası ekranından uygun saat aralığını seçebilirsiniz.">
                <svg uiIcon icon name="DoorOpen" class="h-6 w-6"></svg>
                <a action ui-button size="sm" routerLink="/panel/toplanti">Toplantı odası ayır</a>
              </ui-empty-state>
            </ui-card-body>
          } @else {
            <ui-data-table caption="Yaklaşan toplantı odası rezervasyonları" [headers]="[{ label: 'Tarih ve saat' }, { label: 'Toplantı' }, { label: 'Oda' }, { label: 'Katılımcı' }, { label: '', className: 'text-right' }]">
              @for (reservation of data.upcomingRooms; track reservation.id) {
                <tr class="hover:bg-slate-50/60">
                  <td ui-td>
                    <span class="font-medium text-ink-900">{{ formatLongDate(reservation.date, { withWeekday: false }) }}</span>
                    <span class="block text-xs text-ink-500">{{ minutesToTime(reservation.startMinutes) }} - {{ minutesToTime(reservation.endMinutes) }}</span>
                  </td>
                  <td ui-td>
                    <span class="font-medium text-ink-900">{{ reservation.title }}</span>
                    <span class="block text-xs text-ink-500">{{ reservation.purpose }}</span>
                  </td>
                  <td ui-td>
                    {{ reservation.room?.name }}
                    <span class="block text-xs text-ink-500">{{ reservation.room?.code }} · {{ reservation.room?.floor?.name }}</span>
                  </td>
                  <td ui-td>{{ reservation.attendeeCount }} kişi</td>
                  <td ui-td class="text-right">
                    @if (!isFinished(reservation)) {
                      <app-cancel-reservation-button [reservationId]="reservation.id" kind="room" (cancelled)="refresh()" />
                    }
                  </td>
                </tr>
              }
            </ui-data-table>
          }
        </div>

        <!-- Geçmiş -->
        <div ui-card>
          <ui-card-header title="Geçmiş ve iptal edilen rezervasyonlar" description="Son kayıtlar" hasIcon>
            <svg uiIcon icon name="History" class="h-4.5 w-4.5"></svg>
            <a action routerLink="/panel/kullanimim" class="text-[13px] font-medium text-brand-600 hover:text-brand-700">Kullanım özetim</a>
          </ui-card-header>
          @if (pastRows().length === 0) {
            <ui-card-body>
              <ui-empty-state title="Geçmiş rezervasyon kaydınız yok" />
            </ui-card-body>
          } @else {
            <ui-data-table caption="Geçmiş ve iptal edilen rezervasyonlar" [headers]="[{ label: 'Tarih' }, { label: 'Tür' }, { label: 'Kaynak' }, { label: 'Durum' }]">
              @for (row of pastRows(); track row.id) {
                <tr class="hover:bg-slate-50/60">
                  <td ui-td>{{ formatLongDate(row.dayKey, { withWeekday: false }) }}</td>
                  <td ui-td>
                    <span [class]="cn('text-xs font-medium', row.kind === 'Masa' ? 'text-brand-700' : 'text-teal-700')">{{ row.kind }}</span>
                  </td>
                  <td ui-td>
                    {{ row.resource }}
                    @if (row.detail) {
                      <span class="block text-xs text-ink-500">{{ row.detail }}</span>
                    }
                  </td>
                  <td ui-td>
                    <span ui-badge [tone]="statusTone[row.status]">{{ statusLabels[row.status] }}</span>
                  </td>
                </tr>
              }
            </ui-data-table>
          }
        </div>
      </div>
    }
  `,
})
export class MyReservationsPage {
  private readonly api = inject(ApiClient);
  readonly state = new PageState<MyReservations>();
  readonly today = todayKey();
  readonly cn = cn;
  readonly formatLongDate = formatLongDate;
  readonly minutesToTime = minutesToTime;

  /** Bugün saati geçmiş toplantı artık iptal edilemez. */
  isFinished(room: { date: string; endMinutes: number }): boolean {
    return currentMinutesForDay(room.date) >= room.endMinutes;
  }
  readonly statusTone = STATUS_TONE;
  readonly statusLabels = RESERVATION_STATUS_LABELS;

  readonly pastRows = computed<PastRow[]>(() => {
    const data = this.state.data();
    if (!data) return [];
    return [
      ...data.pastDesks.map((item) => ({
        id: item.id,
        dayKey: item.date,
        kind: 'Masa',
        resource: `${item.desk?.code} · ${item.desk?.floor?.name}`,
        status: item.status,
        detail: this.statusDetail(item),
      })),
      ...data.pastRooms.map((item) => ({
        id: item.id,
        dayKey: item.date,
        kind: 'Toplantı odası',
        resource: `${item.room?.name} · ${minutesToTime(item.startMinutes)}-${minutesToTime(item.endMinutes)}`,
        status: item.status,
        detail: this.statusDetail(item),
      })),
    ]
      .sort((a, b) => (a.dayKey < b.dayKey ? 1 : -1))
      .slice(0, 40);
  });

  /** İptal gerekçesi yoksa iptali kimin yaptığı ya da durumun nedeni gösterilir. */
  private statusDetail(item: { status: string; cancelReason: string | null; cancelledById: string | null; userId: string }): string | null {
    if (item.cancelReason) return item.cancelReason;
    if (item.status === 'CANCELLED') return item.cancelledById === item.userId ? 'Tarafınızdan iptal edildi.' : 'Yönetim tarafından iptal edildi.';
    if (item.status === 'BOOKED') return 'Giriş kaydı alınmadı; katılım doğrulanamadı.';
    if (item.status === 'NO_SHOW') return 'Giriş kaydı olmadığı için gelmedi sayıldı.';
    return null;
  }

  constructor() {
    void this.load();
  }

  load(): Promise<unknown> {
    return this.state.load(() => this.api.get<MyReservations>('me/reservations'));
  }

  refresh(): void {
    void this.state.load(() => this.api.get<MyReservations>('me/reservations'), { silent: true });
  }
}
