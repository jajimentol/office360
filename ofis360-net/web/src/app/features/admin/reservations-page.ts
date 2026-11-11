import { Component, computed, effect, inject, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { addDaysToKey, currentMinutesForDay, formatLongDate, isValidDayKey, minutesToTime, todayKey } from '../../lib/date';
import { DESK_RESERVATION_STATUSES, RESERVATION_STATUS_LABELS, ROOM_RESERVATION_STATUSES } from '../../lib/enums';
import { cn } from '../../lib/utils';
import { ApiClient } from '../../core/api/api-client';
import { AuthService } from '../../core/auth/auth.service';
import type { DeskReservationDto, FloorDto, FloorSummary, Paged, RoomReservationDto } from '../../core/api/types';
import {
  PageState,
  UiAvatar,
  UiBadge,
  UiButton,
  UiCard,
  UiCardBody,
  UiCardHeader,
  UiDataTable,
  UiEmptyState,
  UiIcon,
  UiInput,
  UiPageError,
  UiPageHeader,
  UiPageLoading,
  UiSelect,
  UiTd,
  type BadgeTone,
} from '../../shared/ui';
import { CancelReservationButton } from '../../shared/reservations/reservation-actions';
import { AdminPageNav, floorLabelOf } from './admin-shared';

/** Kaynak: (app)/admin/rezervasyonlar/page.tsx + components/admin/reservation-filters.tsx */

interface ReservationQuery {
  tur: string | null;
  from: string | null;
  to: string | null;
  floor: string | null;
  status: string | null;
  q: string | null;
  sayfa: string | null;
}

const STATUS_TONE: Record<string, BadgeTone> = { BOOKED: 'brand', CHECKED_IN: 'success', COMPLETED: 'neutral', CANCELLED: 'danger', NO_SHOW: 'warning' };
const PAGE_SIZE = 50;

interface PageData {
  floors: ReservationFloor[];
  desks: Paged<DeskReservationDto>;
  rooms: Paged<RoomReservationDto>;
}

type ReservationFloor = { id: string; name: string; building?: { name: string } | null; label?: string };

@Component({
  selector: 'app-admin-reservations-page',
  imports: [FormsModule, RouterLink, UiPageHeader, UiPageLoading, UiPageError, UiCard, UiCardHeader, UiCardBody, UiDataTable, UiTd, UiEmptyState, UiBadge, UiAvatar, UiButton, UiIcon, UiInput, UiSelect, CancelReservationButton, AdminPageNav],
  template: `
    <ui-page-header [title]="isAdmin() ? 'Rezervasyon yönetimi' : 'Tüm rezervasyonlar'" description="Tüm rezervasyonları görüntüleyin ve gerektiğinde gerekçeli olarak iptal edin. İptal edilen rezervasyonun sahibi bildirim alır." />

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as d) {
      @let k = kind();
      <div ui-card class="mb-5">
        <ui-card-body class="space-y-4">
          <div class="flex flex-wrap items-center gap-2">
            <a routerLink="." [queryParams]="{ tur: 'masa', from: fromDayKey(), to: toDayKey() }" [attr.aria-current]="k === 'masa' ? 'page' : null" [class]="cn('rounded-lg border px-3 py-1.5 text-[13px] font-medium transition-colors', k === 'masa' ? 'border-brand-500 bg-brand-600 text-white' : 'border-line bg-white text-ink-700 hover:bg-brand-50')">
              Masa rezervasyonları
            </a>
            <a routerLink="." [queryParams]="{ tur: 'oda', from: fromDayKey(), to: toDayKey() }" [attr.aria-current]="k === 'oda' ? 'page' : null" [class]="cn('rounded-lg border px-3 py-1.5 text-[13px] font-medium transition-colors', k === 'oda' ? 'border-brand-500 bg-brand-600 text-white' : 'border-line bg-white text-ink-700 hover:bg-brand-50')">
              Toplantı odası rezervasyonları
            </a>
          </div>

          <form class="grid gap-3 lg:grid-cols-6" (ngSubmit)="apply()">
            <label class="block">
              <span class="mb-1 block text-xs font-medium text-ink-500">Başlangıç</span>
              <input ui-input type="date" name="from" [(ngModel)]="fFrom" [max]="fTo" />
            </label>
            <label class="block">
              <span class="mb-1 block text-xs font-medium text-ink-500">Bitiş</span>
              <input ui-input type="date" name="to" [(ngModel)]="fTo" [min]="fFrom" />
            </label>
            @if (k === 'masa' && d.floors.length > 1) {
              <label class="block">
                <span class="mb-1 block text-xs font-medium text-ink-500">Kat</span>
                <select ui-select name="floor" [(ngModel)]="fFloor">
                  <option value="">Tüm katlar</option>
                  @for (floor of d.floors; track floor.id) {
                    <option [value]="floor.id">{{ floorLabel(floor) }}</option>
                  }
                </select>
              </label>
            }
            <label class="block">
              <span class="mb-1 block text-xs font-medium text-ink-500">Durum</span>
              <select ui-select name="status" [(ngModel)]="fStatus">
                <option value="">Tüm durumlar</option>
                @for (status of statuses(); track status) {
                  <option [value]="status">{{ statusLabels[status] }}</option>
                }
              </select>
            </label>
            <label class="block">
              <span class="mb-1 block text-xs font-medium text-ink-500">Ara</span>
              <span class="relative block">
                <svg uiIcon name="Search" class="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-400"></svg>
                <input ui-input class="pl-8" name="q" [(ngModel)]="fSearch" [placeholder]="k === 'masa' ? 'Çalışan veya masa kodu' : 'Toplantı, oda veya kişi'" />
              </span>
            </label>
            <div class="flex items-end gap-2">
              <button ui-button type="submit" block>
                <svg uiIcon name="Filter" class="h-4 w-4"></svg>
                Uygula
              </button>
              <button ui-button type="button" variant="secondary" size="icon" aria-label="Filtreleri sıfırla" (click)="reset()">
                <svg uiIcon name="RotateCcw" class="h-4 w-4"></svg>
              </button>
            </div>
          </form>
        </ui-card-body>
      </div>

      <div ui-card>
        <ui-card-header
          [title]="k === 'masa' ? 'Masa rezervasyonları' : 'Toplantı odası rezervasyonları'"
          [description]="total() + ' kayıt · ' + page() + '/' + pageCount() + '. sayfa (' + shown() + ' gösteriliyor) · ' + formatLongDate(fromDayKey(), { withWeekday: false }) + ' - ' + formatLongDate(toDayKey(), { withWeekday: false })"
          hasIcon
        >
          <svg uiIcon icon name="ClipboardList" class="h-4.5 w-4.5"></svg>
        </ui-card-header>
        <ui-card-body class="p-0">
          @if (k === 'masa') {
            @if (d.desks.items.length === 0) {
              <ui-empty-state title="Kayıt bulunamadı" description="Seçilen filtrelere uyan masa rezervasyonu yok." />
            } @else {
              <ui-data-table caption="Masa rezervasyonları" [headers]="[{ label: 'Tarih' }, { label: 'Çalışan' }, { label: 'Masa' }, { label: 'Kat / Bölge' }, { label: 'Durum' }, { label: '', className: 'text-right' }]">
                @for (r of d.desks.items; track r.id) {
                  @let cancellable = r.date >= today && (r.status === 'BOOKED' || r.status === 'CHECKED_IN');
                  <tr class="hover:bg-slate-50/60">
                    <td ui-td>
                      <span class="font-medium text-ink-900">{{ formatLongDate(r.date, { withWeekday: false }) }}</span>
                      @if (r.date === today) {
                        <span class="ml-2 text-xs font-medium text-teal-700">Bugün</span>
                      }
                    </td>
                    <td ui-td>
                      <span class="flex items-center gap-2">
                        <ui-avatar [name]="r.user?.fullName ?? ''" size="sm" />
                        <span class="min-w-0">
                          <span class="block truncate font-medium text-ink-900">{{ r.user?.fullName }}</span>
                          <span class="block truncate text-xs text-ink-500">{{ r.user?.departmentName }}</span>
                        </span>
                      </span>
                    </td>
                    <td ui-td>
                      <span class="font-semibold text-ink-900">{{ r.desk?.code }}</span>
                      @if (r.desk?.popular) {
                        <span class="ml-1 text-xs text-amber-600">★</span>
                      }
                    </td>
                    <td ui-td>
                      {{ r.desk?.floor?.name }}
                      <span class="block text-xs text-ink-500">{{ r.desk?.zone?.name }}</span>
                    </td>
                    <td ui-td>
                      <span ui-badge [tone]="statusTone[r.status]">{{ statusLabels[r.status] }}</span>
                      @if (r.cancelReason) {
                        <span class="mt-0.5 block max-w-[220px] truncate text-xs text-ink-500">{{ r.cancelReason }}</span>
                      }
                    </td>
                    <td ui-td class="text-right">
                      @if (cancellable) {
                        <app-cancel-reservation-button [reservationId]="r.id" kind="desk" [askReason]="true" label="Yönetici iptali" description="Rezervasyon sahibine bildirim gönderilir ve alternatif masalar önerilir." (cancelled)="load(true)" />
                      } @else {
                        <span class="text-xs text-ink-400">İşlem yapılamaz</span>
                      }
                    </td>
                  </tr>
                }
              </ui-data-table>
            }
          } @else {
            @if (d.rooms.items.length === 0) {
              <ui-empty-state title="Kayıt bulunamadı" description="Seçilen filtrelere uyan toplantı odası rezervasyonu yok." />
            } @else {
              <ui-data-table caption="Toplantı odası rezervasyonları" [headers]="[{ label: 'Tarih ve saat' }, { label: 'Organizatör' }, { label: 'Toplantı' }, { label: 'Oda' }, { label: 'Durum' }, { label: '', className: 'text-right' }]">
                @for (r of d.rooms.items; track r.id) {
                  @let cancellable = r.date >= today && r.status === 'BOOKED' && !isFinished(r);
                  <tr class="hover:bg-slate-50/60">
                    <td ui-td>
                      <span class="font-medium text-ink-900">{{ formatLongDate(r.date, { withWeekday: false }) }}</span>
                      <span class="block text-xs text-ink-500">{{ minutesToTime(r.startMinutes) }} - {{ minutesToTime(r.endMinutes) }}</span>
                    </td>
                    <td ui-td>
                      <span class="flex items-center gap-2">
                        <ui-avatar [name]="r.user?.fullName ?? ''" size="sm" />
                        <span class="min-w-0">
                          <span class="block truncate font-medium text-ink-900">{{ r.user?.fullName }}</span>
                          <span class="block truncate text-xs text-ink-500">{{ r.user?.departmentName }}</span>
                        </span>
                      </span>
                    </td>
                    <td ui-td>
                      <span class="font-medium text-ink-900">{{ r.title }}</span>
                      <span class="block text-xs text-ink-500">{{ r.attendeeCount }} katılımcı · {{ r.purpose }}</span>
                    </td>
                    <td ui-td>
                      {{ r.room?.name }}
                      <span class="block text-xs text-ink-500">{{ r.room?.code }} · {{ r.room?.floor?.name }}</span>
                    </td>
                    <td ui-td>
                      <span ui-badge [tone]="statusTone[r.status]">{{ statusLabels[r.status] }}</span>
                    </td>
                    <td ui-td class="text-right">
                      @if (cancellable) {
                        <app-cancel-reservation-button [reservationId]="r.id" kind="room" [askReason]="true" label="Yönetici iptali" description="Rezervasyon sahibine bildirim gönderilir." (cancelled)="load(true)" />
                      } @else {
                        <span class="text-xs text-ink-400">İşlem yapılamaz</span>
                      }
                    </td>
                  </tr>
                }
              </ui-data-table>
            }
          }
          @if (pageCount() > 1) {
            <admin-page-nav class="border-t border-line px-5 py-4" aria-label="Rezervasyon listesi sayfaları" [page]="page()" [pageCount]="pageCount()" [skip]="skip()" [shown]="shown()" [total]="total()" [query]="{ tur: k, from: query().from, to: query().to, floor: query().floor, status: query().status, q: query().q }" />
          }
        </ui-card-body>
      </div>
    }
  `,
})
export class ReservationsPage {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly cn = cn;
  readonly floorLabel = floorLabelOf;

  isFinished(room: { date: string; endMinutes: number }): boolean {
    return currentMinutesForDay(room.date) >= room.endMinutes;
  }

  isAdmin(): boolean {
    return this.auth.user()?.role === 'ADMIN';
  }

  private loadFloors(): Promise<ReservationFloor[]> {
    if (this.isAdmin()) return this.api.get<FloorDto[]>('admin/floors');
    return this.api.get<FloorSummary[]>('floors/summaries').then((floors) => floors.map((f) => ({ id: f.id, name: f.name, label: `${f.buildingName} · ${f.name}` })));
  }
  readonly formatLongDate = formatLongDate;
  readonly minutesToTime = minutesToTime;
  readonly statusTone = STATUS_TONE;
  readonly statusLabels = RESERVATION_STATUS_LABELS;
  readonly today = todayKey();
  readonly state = new PageState<PageData>();

  readonly query = toSignal(
    this.route.queryParamMap.pipe(
      map((p): ReservationQuery => ({ tur: p.get('tur'), from: p.get('from'), to: p.get('to'), floor: p.get('floor'), status: p.get('status'), q: p.get('q'), sayfa: p.get('sayfa') })),
    ),
    { initialValue: { tur: null, from: null, to: null, floor: null, status: null, q: null, sayfa: null } as ReservationQuery },
  );

  readonly kind = computed(() => (this.query().tur === 'oda' ? 'oda' : 'masa'));
  readonly fromDayKey = computed(() => {
    const value = this.query().from;
    return value && isValidDayKey(value) ? value : this.today;
  });
  readonly toDayKey = computed(() => {
    const value = this.query().to;
    return value && isValidDayKey(value) ? value : addDaysToKey(this.today, 14);
  });
  readonly page = computed(() => Math.max(1, Number(this.query().sayfa ?? '1') || 1));
  readonly skip = computed(() => (this.page() - 1) * PAGE_SIZE);
  readonly total = computed(() => (this.kind() === 'masa' ? this.state.data()?.desks.total : this.state.data()?.rooms.total) ?? 0);
  readonly shown = computed(() => (this.kind() === 'masa' ? this.state.data()?.desks.items.length : this.state.data()?.rooms.items.length) ?? 0);
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));
  readonly statuses = computed(() => (this.kind() === 'masa' ? DESK_RESERVATION_STATUSES : ROOM_RESERVATION_STATUSES));

  fFrom = '';
  fTo = '';
  fFloor = '';
  fStatus = '';
  fSearch = '';

  constructor() {
    effect(() => {
      this.query();
      void this.load(untracked(() => this.state.data()) !== null);
    });
  }

  load(silent = false): Promise<unknown> {
    const q = this.query();
    this.fFrom = this.fromDayKey();
    this.fTo = this.toDayKey();
    this.fFloor = q.floor ?? '';
    this.fStatus = q.status ?? '';
    this.fSearch = q.q ?? '';
    const empty = { items: [], total: 0, take: PAGE_SIZE, skip: 0 };
    return this.state.load(
      async () => {
        const [floors, desks, rooms] = await Promise.all([
          this.loadFloors(),
          this.kind() === 'masa'
            ? this.api.get<Paged<DeskReservationDto>>('admin/desk-reservations', { from: this.fromDayKey(), to: this.toDayKey(), floorId: q.floor, status: q.status, search: q.q, take: PAGE_SIZE, skip: this.skip() })
            : Promise.resolve(empty as Paged<DeskReservationDto>),
          this.kind() === 'oda'
            ? this.api.get<Paged<RoomReservationDto>>('admin/room-reservations', { from: this.fromDayKey(), to: this.toDayKey(), status: q.status, search: q.q, take: PAGE_SIZE, skip: this.skip() })
            : Promise.resolve(empty as Paged<RoomReservationDto>),
        ]);
        return { floors, desks, rooms };
      },
      { silent },
    );
  }

  apply(): void {
    const kind = this.kind();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        tur: kind,
        from: this.fFrom,
        to: this.fTo,
        floor: kind === 'masa' && this.fFloor ? this.fFloor : null,
        status: this.fStatus || null,
        q: this.fSearch.trim() || null,
      },
    });
  }

  reset(): void {
    this.fFloor = '';
    this.fStatus = '';
    this.fSearch = '';
    void this.router.navigate([], { relativeTo: this.route, queryParams: { tur: this.kind(), from: this.fFrom, to: this.fTo } });
  }
}
