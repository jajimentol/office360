import { Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { map } from 'rxjs';
import { diffInDays, formatLongDate, isValidDayKey, minutesToTime, timeToMinutes, todayKey } from '../../lib/date';
import { cn } from '../../lib/utils';
import { ApiClient, toApiError } from '../../core/api/api-client';
import { AuthService } from '../../core/auth/auth.service';
import { PersonPicker, type PersonResult } from '../../shared/person-picker';
import { EmployeeContextService, type EmployeeContext } from '../../core/api/employee-context';
import type { RoomAvailabilityResult, RoomAvailabilityView, RoomSuggestion, RuleViolation } from '../../core/api/types';
import { ShellService } from '../../layout/shell.service';
import {
  PageState,
  ToastService,
  UiAlert,
  UiBadge,
  UiButton,
  UiCard,
  UiCardBody,
  UiCardHeader,
  UiEmptyState,
  UiField,
  UiIcon,
  UiInput,
  UiModal,
  UiPageError,
  UiPageHeader,
  UiPageLoading,
  UiSelect,
  UiSkeleton,
  type IconName,
} from '../../shared/ui';
import { DateStrip } from '../../shared/floor-plan/date-strip';

/** Kaynak: (app)/panel/toplanti/page.tsx + components/rooms/room-board.tsx + room-filters.tsx */

const PURPOSES = ['Ekip içi koordinasyon', 'Müşteri görüşmesi', 'Proje çalışması', 'Değerlendirme toplantısı', 'Eğitim ve gelişim', 'İşe alım süreci'];

const FEATURE_ICONS: Record<string, IconName> = {
  SCREEN: 'Presentation',
  VIDEO: 'Video',
  WHITEBOARD: 'Monitor',
};

interface FormValues {
  title: string;
  purpose: string;
  attendeeCount: number | string;
  startTime: string;
  endTime: string;
}

type FormErrors = Partial<Record<keyof FormValues, string>>;

/** Kaynak zod şeması (room-board.tsx formSchema) ile aynı sınırlar ve mesajlar. */
function validateForm(values: FormValues): FormErrors {
  const errors: FormErrors = {};
  if (values.title.length < 3) errors.title = 'Toplantı başlığı en az 3 karakter olmalıdır.';
  else if (values.title.length > 120) errors.title = 'Toplantı başlığı en fazla 120 karakter olabilir.';
  if (!values.purpose) errors.purpose = 'Rezervasyon amacı seçilmelidir.';
  const attendees = Number(values.attendeeCount);
  if (!Number.isInteger(attendees)) errors.attendeeCount = 'Katılımcı sayısı tam sayı olmalıdır.';
  else if (attendees < 1) errors.attendeeCount = 'Katılımcı sayısı en az 1 olmalıdır.';
  if (!/^\d{2}:\d{2}$/.test(values.startTime)) errors.startTime = 'Başlangıç saati geçersiz.';
  if (!/^\d{2}:\d{2}$/.test(values.endTime)) errors.endTime = 'Bitiş saati geçersiz.';
  return errors;
}

@Component({
  selector: 'app-room-page',
  imports: [
    FormsModule,
    PersonPicker,
    UiPageHeader,
    UiPageLoading,
    UiPageError,
    UiCard,
    UiCardHeader,
    UiCardBody,
    UiAlert,
    UiBadge,
    UiButton,
    UiEmptyState,
    UiField,
    UiInput,
    UiSelect,
    UiModal,
    UiIcon,
    UiSkeleton,
    DateStrip,
  ],
  template: `
    @if (context.loading()) {
      <ui-page-loading />
    } @else if (context.error()) {
      <ui-page-error [code]="context.error()" (retry)="loadContext()" />
    } @else if (context.data(); as ctx) {
      @let settings = ctx.settings;
      <ui-page-header
        title="Toplantı odası rezervasyonu"
        [description]="'Tarih ve saat aralığı seçin, uygun odaları takvim veya liste görünümünde inceleyin. Mesai saatleri: ' + minutesToTime(settings.roomWorkingHours.startMinutes) + ' - ' + minutesToTime(settings.roomWorkingHours.endMinutes) + '.'"
      />

      <div class="space-y-4">
        <div ui-card>
          <ui-card-header title="Tarih ve filtreler" hasIcon>
            <svg uiIcon icon name="DoorOpen" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body class="space-y-4">
            <app-date-strip [selectedDayKey]="dayKey()" [maxDayKey]="ctx.latestBookableDayKey" [blockedDayKeys]="ctx.blockedDayKeys" [visibleCount]="7" />
            <div class="border-t border-line pt-4">
              <!-- Toplantı odası filtreleri. Seçimler URL'e yazılır; sayfa yenilendiğinde korunur. -->
              <div class="space-y-3">
                <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
                  <label class="block">
                    <span class="mb-1 block text-xs font-medium text-ink-500">Başlangıç saati</span>
                    <input ui-input type="time" [attr.step]="slotStepSeconds()" [(ngModel)]="filterStart" />
                  </label>
                  <label class="block">
                    <span class="mb-1 block text-xs font-medium text-ink-500">Bitiş saati</span>
                    <input ui-input type="time" [attr.step]="slotStepSeconds()" [(ngModel)]="filterEnd" />
                  </label>
                  <label class="block">
                    <span class="mb-1 block text-xs font-medium text-ink-500">En az kapasite</span>
                    <input ui-input type="number" min="1" max="100" [(ngModel)]="filterCapacity" placeholder="Örn. 6" />
                  </label>
                  @if (roomFloors().length > 1) {
                    <label class="block">
                      <span class="mb-1 block text-xs font-medium text-ink-500">Kat</span>
                      <select ui-select [(ngModel)]="filterFloor">
                        <option value="">Tüm katlar</option>
                        @for (floor of roomFloors(); track floor.id) {
                          <option [value]="floor.id">{{ floor.name }}</option>
                        }
                      </select>
                    </label>
                  }
                  <div class="flex items-end gap-2">
                    <button ui-button type="button" block (click)="applyFilters()">
                      <svg uiIcon name="Filter" class="h-4 w-4"></svg>
                      Uygula
                    </button>
                    <button ui-button type="button" variant="secondary" size="icon" (click)="resetFilters()" aria-label="Filtreleri sıfırla">
                      <svg uiIcon name="RotateCcw" class="h-4 w-4"></svg>
                    </button>
                  </div>
                </div>

                <div class="flex flex-wrap items-center gap-1.5">
                  <span class="text-xs font-medium text-ink-500">Donanım:</span>
                  @for (feature of ctx.roomFeatures; track feature.key) {
                    @let active = featureKeys().includes(feature.key);
                    <button
                      type="button"
                      [attr.aria-pressed]="active"
                      (click)="toggleFeature(feature.key)"
                      [class]="cn('rounded-full border px-3 py-1 text-xs font-medium transition-colors', active ? 'border-brand-500 bg-brand-600 text-white' : 'border-line bg-white text-ink-600 hover:border-brand-200 hover:bg-brand-50')"
                    >
                      {{ feature.name }}
                    </button>
                  }
                </div>
              </div>
            </div>
          </ui-card-body>
        </div>

        @if (outsideWindow()) {
          <ui-alert tone="warning" title="Rezervasyon penceresi dışında">
            Toplantı odaları en fazla {{ settings.advanceBookingDays.days }} iş günü öncesinden rezerve edilebilir.
          </ui-alert>
        }

        @if (availability.data(); as av) {
          @let rooms = visibleRooms();
          <div ui-card [class.opacity-60]="availability.loading()" class="transition-opacity">
            <ui-card-header
              [title]="'Toplantı odaları · ' + formatLongDate(dayKey(), { withWeekday: false })"
              [description]="timeFilterActive() ? rooms.length + ' oda ' + minutesToTime(startMinutes()) + ' - ' + minutesToTime(endMinutes()) + ' arasında uygun' : openRoomCount() + ' oda kullanıma açık · Mesai ' + minutesToTime(av.workStartMinutes) + ' - ' + minutesToTime(av.workEndMinutes)"
              hasIcon
            >
              <svg uiIcon icon name="DoorOpen" class="h-4.5 w-4.5"></svg>
              <div action class="flex items-center gap-1 rounded-lg border border-line bg-slate-50 p-0.5">
                <button type="button" (click)="view.set('calendar')" [attr.aria-pressed]="view() === 'calendar'" [class]="cn('flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-[13px] font-medium transition-colors', view() === 'calendar' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')">
                  <svg uiIcon name="CalendarRange" class="h-4 w-4"></svg>
                  Takvim
                </button>
                <button type="button" (click)="view.set('list')" [attr.aria-pressed]="view() === 'list'" [class]="cn('flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-[13px] font-medium transition-colors', view() === 'list' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')">
                  <svg uiIcon name="List" class="h-4 w-4"></svg>
                  Liste
                </button>
              </div>
            </ui-card-header>
            <ui-card-body>
              @if (rooms.length === 0) {
                <ui-empty-state hasIcon title="Filtrelere uyan toplantı odası bulunamadı" [description]="timeFilterActive() ? 'Seçtiğiniz saatte rezerve edebileceğiniz oda yok. Saati değiştirin veya filtreleri sıfırlayın.' : 'Kapasite, kat veya donanım filtrelerini gevşeterek yeniden deneyin.'">
                  <svg uiIcon icon name="DoorOpen" class="h-6 w-6"></svg>
                </ui-empty-state>
              } @else if (view() === 'calendar') {
                <div class="overflow-x-auto scrollbar-thin" tabindex="0" role="region" aria-label="Toplantı odası takvimi">
                  <div class="min-w-[760px]">
                    <!-- Saat başlıkları -->
                    <div class="mb-2 flex">
                      <div class="w-44 shrink-0"></div>
                      <div class="relative flex-1">
                        <div class="flex">
                          @for (minutes of hourMarks(); track minutes) {
                            <div class="flex-1 border-l border-line pl-1 text-[11px] font-medium text-ink-400">{{ minutesToTime(minutes) }}</div>
                          }
                        </div>
                      </div>
                    </div>

                    <ul class="space-y-2">
                      @for (room of rooms; track room.id) {
                        <li class="flex items-stretch gap-3">
                          <div class="w-44 shrink-0">
                            <p class="truncate text-sm font-semibold text-ink-900">{{ room.name }}</p>
                            <p class="truncate text-xs text-ink-500">{{ room.code }} · {{ room.capacity }} kişi · {{ room.floorName }}</p>
                          </div>
                          <div class="relative h-14 flex-1 overflow-hidden rounded-lg border border-line bg-slate-50" [style.background-image]="slotGrid()">
                            @if (room.closed) {
                              <div class="desk-pattern-closed flex h-full items-center justify-center gap-2 text-xs font-medium text-red-700">
                                <svg uiIcon name="Lock" class="h-3.5 w-3.5"></svg>
                                {{ room.closureNote ?? 'Kullanıma kapalı' }}
                              </div>
                            } @else if (room.accessReason) {
                              <div class="flex h-full items-center justify-center gap-2 px-3 text-center text-xs font-medium text-ink-500">
                                <svg uiIcon name="Lock" class="h-3.5 w-3.5 shrink-0"></svg>
                                {{ room.accessReason }}
                              </div>
                            } @else {
                              <!-- Boş aralıklar - tıklanabilir -->
                              @for (slot of room.freeSlots; track slot.startMinutes) {
                                <button
                                  type="button"
                                  (click)="openBooking(room, slot.startMinutes)"
                                  [title]="minutesToTime(slot.startMinutes) + ' - ' + minutesToTime(slot.endMinutes) + ' uygun'"
                                  [attr.aria-label]="room.name + ' ' + minutesToTime(slot.startMinutes) + ' - ' + minutesToTime(slot.endMinutes) + ' aralığı uygun, rezervasyon oluştur'"
                                  class="absolute top-0 h-full bg-emerald-50/70 transition-colors hover:bg-emerald-100"
                                  [style.left.%]="pct(slot.startMinutes - av.workStartMinutes, av)"
                                  [style.width.%]="pct(slot.endMinutes - slot.startMinutes, av)"
                                ></button>
                              }
                              <!-- Dolu aralıklar -->
                              @for (slot of room.busySlots; track slot.id) {
                                <div
                                  [title]="slot.title + ' · ' + minutesToTime(slot.startMinutes) + ' - ' + minutesToTime(slot.endMinutes) + (slot.ownerName ? ' · ' + slot.ownerName : '')"
                                  class="desk-pattern-reserved absolute top-1 flex h-12 items-center overflow-hidden rounded-md border border-slate-300 bg-slate-200/90 px-2"
                                  [style.left.%]="pct(slot.startMinutes - av.workStartMinutes, av)"
                                  [style.width.%]="pct(slot.endMinutes - slot.startMinutes, av)"
                                >
                                  <span class="truncate text-[11px] font-medium text-ink-700">{{ minutesToTime(slot.startMinutes) }} {{ slot.title }}</span>
                                </div>
                              }
                            }
                          </div>
                          <div class="flex w-24 shrink-0 items-center justify-end">
                            <button ui-button type="button" size="sm" [variant]="room.closed || room.accessReason ? 'secondary' : 'subtle'" [disabled]="room.closed || !!room.accessReason || !!readOnlyReason() || room.freeSlots.length === 0" (click)="openBooking(room)">
                              Rezerve et
                            </button>
                          </div>
                        </li>
                      }
                    </ul>

                    <div class="mt-4 flex flex-wrap gap-4 border-t border-line pt-3 text-xs text-ink-600">
                      <span class="flex items-center gap-2">
                        <span class="inline-block h-3.5 w-6 rounded border border-emerald-200 bg-emerald-50" aria-hidden="true"></span>
                        Uygun aralık (tıklayarak rezerve edin)
                      </span>
                      <span class="flex items-center gap-2">
                        <span class="desk-pattern-reserved inline-block h-3.5 w-6 rounded border border-slate-300 bg-slate-200" aria-hidden="true"></span>
                        Dolu
                      </span>
                      <span class="flex items-center gap-2">
                        <span class="desk-pattern-closed inline-block h-3.5 w-6 rounded border border-red-300 bg-red-50" aria-hidden="true"></span>
                        Kullanıma kapalı
                      </span>
                    </div>
                  </div>
                </div>
              } @else {
                <ul class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                  @for (room of rooms; track room.id) {
                    <li class="rounded-xl border border-line p-4">
                      <div class="flex items-start justify-between gap-2">
                        <div>
                          <p class="text-sm font-semibold text-ink-900">{{ room.name }}</p>
                          <p class="text-xs text-ink-500">{{ room.code }} · {{ room.floorName }}</p>
                        </div>
                        <span ui-badge [tone]="room.closed ? 'danger' : room.accessReason ? 'neutral' : room.freeSlots.length > 0 ? 'success' : 'warning'">
                          {{ room.closed ? 'Kapalı' : room.accessReason ? 'Kısıtlı' : room.freeSlots.length > 0 ? 'Uygun' : 'Dolu' }}
                        </span>
                      </div>

                      <p class="mt-2 flex items-center gap-1.5 text-[13px] text-ink-600">
                        <svg uiIcon name="Users" class="h-3.5 w-3.5 text-ink-400"></svg>
                        {{ room.capacity }} kişi kapasiteli
                      </p>

                      <div class="mt-2 flex flex-wrap gap-1.5">
                        @for (feature of room.features; track feature.key) {
                          <span ui-badge tone="neutral">
                            @if (featureIcons[feature.key]; as icon) {
                              <svg uiIcon [name]="icon" class="h-3 w-3"></svg>
                            }
                            {{ feature.name }}
                          </span>
                        }
                      </div>

                      <div class="mt-3">
                        <p class="text-xs font-semibold uppercase tracking-wide text-ink-400">Uygun saatler</p>
                        @if (room.closed) {
                          <p class="mt-1 text-[13px] text-red-700">{{ room.closureNote ?? 'Bu tarihte kullanıma kapalı.' }}</p>
                        } @else if (room.accessReason) {
                          <p class="mt-1 text-[13px] text-ink-500">{{ room.accessReason }}</p>
                        } @else if (room.freeSlots.length === 0) {
                          <p class="mt-1 text-[13px] text-ink-500">Bu gün için uygun aralık kalmadı.</p>
                        } @else {
                          <div class="mt-1.5 flex flex-wrap gap-1.5">
                            @for (slot of room.freeSlots.slice(0, 5); track slot.startMinutes) {
                              <button
                                type="button"
                                [disabled]="!!readOnlyReason()"
                                (click)="openBooking(room, slot.startMinutes)"
                                class="rounded-lg border border-emerald-200 bg-emerald-50 px-2 py-1 text-[12px] font-medium text-emerald-800 transition-colors hover:bg-emerald-100 disabled:opacity-50"
                              >
                                {{ minutesToTime(slot.startMinutes) }} - {{ minutesToTime(slot.endMinutes) }}
                              </button>
                            }
                          </div>
                        }
                      </div>

                      <button ui-button type="button" class="mt-3" size="sm" block variant="secondary" [disabled]="room.closed || !!room.accessReason || !!readOnlyReason() || room.freeSlots.length === 0" (click)="openBooking(room)">
                        Rezervasyon oluştur
                      </button>
                    </li>
                  }
                </ul>
              }
            </ui-card-body>
          </div>
        } @else if (availability.error()) {
          <ui-page-error [code]="availability.error()" (retry)="reloadAvailability()" />
        } @else {
          <div ui-skeleton class="h-96" role="status" aria-live="polite"></div>
        }
      </div>

      <!-- Rezervasyon formu -->
      @let room = activeRoom();
      <ui-modal
        [open]="room !== null"
        (closed)="activeRoom.set(null)"
        [title]="room ? room.name + ' rezervasyonu' : 'Toplantı odası rezervasyonu'"
        [description]="room ? room.code + ' · ' + room.capacity + ' kişi · ' + room.floorName + ' · ' + formatLongDate(dayKey(), { withWeekday: false }) : undefined"
      >
        @if (room) {
          <form id="room-reservation-form" class="space-y-4" (ngSubmit)="submit()" novalidate>
            @if (canActForOthers()) {
              <ui-field label="Kimin adına" htmlFor="room-target" hint="Boş bırakırsanız kendi adınıza oluşturulur.">
                <app-person-picker inputId="room-target" (picked)="target.set($event)" />
              </ui-field>
            }

            <ui-field label="Toplantı başlığı" required htmlFor="room-title" [error]="errors().title">
              <input ui-input id="room-title" name="title" [(ngModel)]="form.title" placeholder="Örneğin: Sprint planlama" autofocus />
            </ui-field>

            <div class="grid gap-4 sm:grid-cols-2">
              <ui-field label="Başlangıç" required htmlFor="room-start" [error]="errors().startTime">
                <input ui-input id="room-start" name="startTime" type="time" [attr.step]="slotStepSeconds()" [(ngModel)]="form.startTime" />
              </ui-field>
              <ui-field label="Bitiş" required htmlFor="room-end" [error]="errors().endTime">
                <input ui-input id="room-end" name="endTime" type="time" [attr.step]="slotStepSeconds()" [(ngModel)]="form.endTime" />
              </ui-field>
            </div>

            <div class="grid gap-4 sm:grid-cols-2">
              <ui-field label="Katılımcı sayısı" required htmlFor="room-attendees" [hint]="(room.minAttendees > 1 ? 'En az ' + room.minAttendees + ', en çok ' : 'Oda kapasitesi: ') + room.capacity + ' kişi'" [error]="errors().attendeeCount">
                <input ui-input id="room-attendees" name="attendeeCount" type="number" [min]="room.minAttendees" [max]="room.capacity" [(ngModel)]="form.attendeeCount" />
              </ui-field>
              <ui-field label="Rezervasyon amacı" required htmlFor="room-purpose" [error]="errors().purpose">
                <select ui-select id="room-purpose" name="purpose" [(ngModel)]="form.purpose">
                  @for (purpose of purposes; track purpose) {
                    <option [value]="purpose">{{ purpose }}</option>
                  }
                </select>
              </ui-field>
            </div>

            <div class="rounded-lg bg-slate-50 px-3 py-2.5">
              <p class="text-xs font-semibold uppercase tracking-wide text-ink-400">Bu gün için uygun aralıklar</p>
              <div class="mt-1.5 flex flex-wrap gap-1.5">
                @if (room.freeSlots.length === 0) {
                  <span class="text-[13px] text-ink-500">Uygun aralık bulunmuyor.</span>
                } @else {
                  @for (slot of room.freeSlots; track slot.startMinutes) {
                    <button type="button" (click)="pickSlot(slot.startMinutes, Math.min(slot.startMinutes + 60, slot.endMinutes))" class="rounded-md border border-line bg-white px-2 py-1 text-[12px] text-ink-700 hover:border-brand-200 hover:bg-brand-50">
                      {{ minutesToTime(slot.startMinutes) }} - {{ minutesToTime(slot.endMinutes) }}
                    </button>
                  }
                }
              </div>
            </div>

            @if (violations(); as list) {
              @if (list.length > 0) {
                <div class="space-y-2">
                  @for (violation of list; track violation.code) {
                    <ui-alert tone="warning" [title]="violation.title">
                      <p>{{ violation.message }}</p>
                      @if (violation.hint) {
                        <p class="mt-1 text-ink-600">{{ violation.hint }}</p>
                      }
                    </ui-alert>
                  }
                </div>
              }
            }

            @if (suggestions(); as s) {
              @if (s.alternativeSlots?.length || s.alternativeRooms?.length) {
                <div class="rounded-xl border border-brand-200 bg-brand-50/60 p-3.5">
                  <p class="flex items-center gap-1.5 text-[13px] font-semibold text-brand-800">
                    <svg uiIcon name="Sparkles" class="h-4 w-4"></svg>
                    Alternatif öneriler
                  </p>
                  @if (s.alternativeSlots?.length) {
                    <div class="mt-2">
                      <p class="text-xs text-brand-900/70">Aynı odada uygun saatler</p>
                      <div class="mt-1 flex flex-wrap gap-1.5">
                        @for (slot of s.alternativeSlots; track slot.startMinutes) {
                          <button type="button" (click)="pickSlot(slot.startMinutes, slot.endMinutes)" class="rounded-md bg-white px-2 py-1 text-[12px] font-medium text-brand-700 hover:bg-brand-100">
                            {{ minutesToTime(slot.startMinutes) }} - {{ minutesToTime(slot.endMinutes) }}
                          </button>
                        }
                      </div>
                    </div>
                  }
                  @if (s.alternativeRooms?.length) {
                    <div class="mt-2.5">
                      <p class="text-xs text-brand-900/70">Aynı saatte uygun odalar</p>
                      <div class="mt-1 flex flex-wrap gap-1.5">
                        @for (alt of s.alternativeRooms; track alt.id) {
                          <button type="button" (click)="switchRoom(alt.id)" class="rounded-md bg-white px-2 py-1 text-[12px] font-medium text-brand-700 hover:bg-brand-100">
                            {{ alt.name }} ({{ alt.capacity }} kişi)
                          </button>
                        }
                      </div>
                    </div>
                  }
                </div>
              }
            }

            @if (readOnlyReason()) {
              <ui-alert tone="info">{{ readOnlyReason() }}</ui-alert>
            }
          </form>
        }
        <ng-container footer>
          <button ui-button type="button" variant="secondary" (click)="activeRoom.set(null)" [disabled]="pending()">Vazgeç</button>
          <button ui-button type="submit" form="room-reservation-form" [loading]="pending()">
            <svg uiIcon name="CheckCircle2" class="h-4 w-4"></svg>
            Rezervasyonu oluştur
          </button>
        </ng-container>
      </ui-modal>
    }
  `,
})
export class RoomPage {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthService);
  private readonly contextService = inject(EmployeeContextService);
  private readonly toasts = inject(ToastService);
  private readonly shell = inject(ShellService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly Math = Math;
  readonly cn = cn;
  readonly minutesToTime = minutesToTime;
  readonly formatLongDate = formatLongDate;
  readonly purposes = PURPOSES;
  readonly featureIcons = FEATURE_ICONS;
  readonly today = todayKey();

  readonly context = new PageState<EmployeeContext>();
  readonly availability = new PageState<RoomAvailabilityResult>();

  private readonly query = toSignal(
    this.route.queryParamMap.pipe(
      map((params): Record<string, string | null> => ({
        date: params.get('date'),
        start: params.get('start'),
        end: params.get('end'),
        capacity: params.get('capacity'),
        floor: params.get('floor'),
        features: params.get('features'),
      })),
    ),
    { initialValue: { date: null, start: null, end: null, capacity: null, floor: null, features: null } as Record<string, string | null> },
  );

  readonly dayKey = computed(() => {
    const requested = this.query()['date'];
    const valid = requested && isValidDayKey(requested) ? requested : this.today;
    return valid < this.today ? this.today : valid;
  });

  readonly startMinutes = computed(() => {
    const start = this.query()['start'];
    const settings = this.context.data()?.settings;
    return start ? timeToMinutes(start) : (settings?.roomWorkingHours.startMinutes ?? 8 * 60) + 60;
  });
  readonly endMinutes = computed(() => {
    const end = this.query()['end'];
    return end ? timeToMinutes(end) : this.startMinutes() + 60;
  });
  readonly featureKeys = computed(() => (this.query()['features'] ?? '').split(',').filter(Boolean));
  readonly roomFloors = computed(() => (this.context.data()?.floors ?? []).filter((floor) => floor.hasRooms).map((floor) => ({ id: floor.id, name: floor.name })));

  readonly outsideWindow = computed(() => {
    const latest = this.context.data()?.latestBookableDayKey;
    return latest ? this.dayKey() > latest : false;
  });
  readonly readOnlyReason = computed(() => (this.outsideWindow() ? 'Bu tarih rezervasyon penceresinin dışında olduğu için işlem yapılamaz.' : null));

  // Filtre formu (uygulanana kadar URL'e yazılmaz)
  filterStart = '';
  filterEnd = '';
  filterCapacity = '';
  filterFloor = '';

  readonly view = signal<'calendar' | 'list'>('calendar');
  readonly activeRoom = signal<RoomAvailabilityView | null>(null);
  readonly target = signal<PersonResult | null>(null);
  readonly canActForOthers = computed(() => {
    const user = this.auth.user();
    return !!user && (user.role === 'ADMIN' || user.isSecretary);
  });
  readonly violations = signal<RuleViolation[] | null>(null);
  readonly suggestions = signal<RoomSuggestion | null>(null);
  readonly pending = signal(false);
  readonly errors = signal<FormErrors>({});
  form: FormValues = { title: '', purpose: PURPOSES[0], attendeeCount: 4, startTime: '', endTime: '' };

  /** Saat filtresi yalnızca kullanıcı saat seçtiğinde uygulanır; aksi halde tüm odaların günlük takvimi görünür. */
  readonly timeFilterActive = computed(() => !!this.query()['start']);
  readonly visibleRooms = computed(() => {
    const rooms = this.availability.data()?.rooms ?? [];
    return this.timeFilterActive() ? rooms.filter((room) => room.availableForRequest === true) : rooms;
  });
  readonly openRoomCount = computed(() => (this.availability.data()?.rooms ?? []).filter((room) => !room.closed && !room.accessReason).length);
  readonly hourMarks = computed(() => {
    const av = this.availability.data();
    if (!av) return [];
    const marks: number[] = [];
    for (let minutes = av.workStartMinutes; minutes <= av.workEndMinutes; minutes += 60) marks.push(minutes);
    return marks;
  });
  readonly slotStepSeconds = computed(() => {
    const slot = this.context.data()?.settings.roomSlotMinutes;
    return slot?.enabled ? slot.minutes * 60 : 900;
  });

  readonly slotGrid = computed(() => {
    const av = this.availability.data();
    if (!av) return null;
    const slotCount = Math.ceil((av.workEndMinutes - av.workStartMinutes) / av.slotMinutes);
    return `repeating-linear-gradient(90deg, transparent 0, transparent calc(100%/${slotCount} - 1px), #e2e8f0 calc(100%/${slotCount} - 1px), #e2e8f0 calc(100%/${slotCount}))`;
  });

  private availabilityKey = '';

  constructor() {
    void this.loadContext();
    effect(() => {
      if (!this.context.data()) return;
      const q = this.query();
      const key = JSON.stringify([this.dayKey(), this.startMinutes(), this.endMinutes(), q['capacity'], q['floor'], q['features']]);
      if (key === this.availabilityKey) return;
      this.availabilityKey = key;
      this.filterStart = q['start'] ?? '';
      this.filterEnd = q['end'] ?? '';
      this.filterCapacity = q['capacity'] ?? '';
      this.filterFloor = q['floor'] ?? '';
      void this.fetchAvailability(this.availability.data() !== null);
    });
  }

  loadContext(): Promise<unknown> {
    return this.context.load(() => this.contextService.load());
  }

  pct(minutes: number, av: RoomAvailabilityResult): number {
    return (minutes / (av.workEndMinutes - av.workStartMinutes)) * 100;
  }

  private fetchAvailability(silent: boolean): Promise<unknown> {
    const q = this.query();
    const capacity = Number(q['capacity']);
    return this.availability.load(
      () =>
        this.api.get<RoomAvailabilityResult>('rooms/availability', {
          date: this.dayKey(),
          start: this.timeFilterActive() ? this.startMinutes() : undefined,
          end: this.timeFilterActive() ? this.endMinutes() : undefined,
          minCapacity: Number.isFinite(capacity) && capacity ? capacity : undefined,
          floorId: q['floor'] || undefined,
          features: q['features'] || undefined,
        }),
      { silent },
    );
  }

  reloadAvailability(): void {
    void this.fetchAvailability(false);
  }

  private navigate(params: Record<string, string | undefined>): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: params });
  }

  applyFilters(overrides?: { features?: string[] }): void {
    if (this.filterEnd && !this.filterStart) {
      this.toasts.toast({ tone: 'error', title: 'Başlangıç saatini de seçin' });
      return;
    }
    if (this.filterStart && this.filterEnd && this.filterEnd <= this.filterStart) {
      this.toasts.toast({ tone: 'error', title: 'Bitiş saati başlangıçtan sonra olmalıdır' });
      return;
    }
    const features = overrides?.features ?? this.featureKeys();
    this.navigate({
      date: this.dayKey(),
      start: this.filterStart || undefined,
      end: this.filterEnd || undefined,
      capacity: this.filterCapacity || undefined,
      floor: this.filterFloor || undefined,
      features: features.length > 0 ? features.join(',') : undefined,
    });
  }

  resetFilters(): void {
    this.filterStart = '';
    this.filterEnd = '';
    this.filterCapacity = '';
    this.filterFloor = '';
    this.navigate({ date: this.dayKey() });
  }

  toggleFeature(key: string): void {
    const current = this.featureKeys();
    const next = current.includes(key) ? current.filter((k) => k !== key) : [...current, key];
    this.applyFilters({ features: next });
  }

  openBooking(room: RoomAvailabilityView, startMinutes?: number): void {
    const av = this.availability.data();
    this.activeRoom.set(room);
    this.target.set(null);
    this.violations.set(null);
    this.suggestions.set(null);
    this.errors.set({});
    const useFilter = startMinutes === undefined && this.timeFilterActive();
    const start = useFilter ? this.startMinutes() : (startMinutes ?? room.freeSlots[0]?.startMinutes ?? this.startMinutes());
    const end = useFilter ? this.endMinutes() : Math.min(start + 60, av?.workEndMinutes ?? this.endMinutes());
    this.form = { title: '', purpose: PURPOSES[0], attendeeCount: Math.max(room.minAttendees, Math.min(4, room.capacity)), startTime: minutesToTime(start), endTime: minutesToTime(end) };
  }

  pickSlot(start: number, end: number): void {
    this.form = { ...this.form, startTime: minutesToTime(start), endTime: minutesToTime(end) };
  }

  switchRoom(roomId: string): void {
    const target = this.availability.data()?.rooms.find((item) => item.id === roomId);
    if (target) {
      this.activeRoom.set(target);
      this.violations.set(null);
      this.suggestions.set(null);
    }
  }

  async submit(): Promise<void> {
    const room = this.activeRoom();
    if (!room) return;
    const errors = validateForm(this.form);
    this.errors.set(errors);
    if (Object.keys(errors).length > 0) return;

    this.pending.set(true);
    this.violations.set(null);
    this.suggestions.set(null);
    try {
      const result = await this.api.post<{ id: string; roomName: string }>('room-reservations', {
        roomId: room.id,
        date: this.dayKey(),
        startMinutes: timeToMinutes(this.form.startTime),
        endMinutes: timeToMinutes(this.form.endTime),
        title: this.form.title,
        purpose: this.form.purpose,
        attendeeCount: Number(this.form.attendeeCount),
        targetUserId: this.target()?.id,
      });
      this.toasts.toast({ tone: 'success', title: 'Toplantı odası rezerve edildi', description: `${result.roomName} · ${this.form.startTime} - ${this.form.endTime}` });
      this.activeRoom.set(null);
      void this.shell.refreshUnread();
      void this.fetchAvailability(true);
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this.violations.set(apiError.violations ?? null);
      this.suggestions.set((apiError.details as RoomSuggestion | undefined) ?? null);
      this.toasts.toast({ tone: 'error', title: 'Rezervasyon oluşturulamadı', description: apiError.message });
    } finally {
      this.pending.set(false);
    }
  }
}
