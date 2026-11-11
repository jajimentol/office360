import { Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { addDaysToKey, formatLongDate, isValidDayKey, todayKey } from '../../lib/date';
import { cn } from '../../lib/utils';
import { ApiClient } from '../../core/api/api-client';
import { EmployeeContextService, type ContextFloor, type EmployeeContext } from '../../core/api/employee-context';
import type { FloorPlanView } from '../../core/api/types';
import { AuthService } from '../../core/auth/auth.service';
import { PageState, UiAlert, UiCard, UiCardBody, UiIcon, UiPageError, UiPageHeader, UiPageLoading, UiSkeleton } from '../../shared/ui';
import { DateStrip } from '../../shared/floor-plan/date-strip';
import { FloorPlanBoard } from '../../shared/floor-plan/floor-plan-board';
import { PersonPicker, type PersonResult } from '../../shared/person-picker';

/** Kaynak: (app)/panel/masa/page.tsx */

interface BuildingGroup {
  id: string;
  name: string;
  code: string;
  floors: ContextFloor[];
}

@Component({
  selector: 'app-desk-page',
  imports: [RouterLink, UiPageHeader, UiPageLoading, UiPageError, UiAlert, UiCard, UiCardBody, UiIcon, UiSkeleton, DateStrip, FloorPlanBoard, PersonPicker],
  template: `
    @if (context.loading()) {
      <ui-page-loading />
    } @else if (context.error()) {
      <ui-page-error [code]="context.error()" (retry)="loadContext()" />
    } @else if (context.data(); as ctx) {
      @if (ctx.floors.length === 0) {
        <ui-page-header title="Masa rezervasyonu" description="Tarih ve kat seçerek uygun bir masayı rezerve edin." />
        <ui-alert tone="info" title="Rezervasyona açık kat yok">
          Henüz çalışan erişimine açık bir bina ve kat tanımlanmamış. Yönetim bir kat planı yayınladığında burada görünecek.
        </ui-alert>
      } @else {
        <ui-page-header title="Masa rezervasyonu" description="Tarih ve kat seçerek uygun bir masayı rezerve edin. Masa durumları renk, desen ve etiketle ayrıştırılmıştır." />

        <div class="space-y-4">
          <div ui-card>
            <ui-card-body class="space-y-4">
              <app-date-strip [selectedDayKey]="dayKey()" [maxDayKey]="ctx.latestBookableDayKey" [blockedDayKeys]="ctx.blockedDayKeys" [visibleCount]="7" />

              @if (canActForOthers()) {
                <div class="border-t border-line pt-4">
                  <label for="desk-target" class="mb-1 block text-xs font-semibold uppercase tracking-wide text-ink-400">Kimin adına</label>
                  <app-person-picker class="block max-w-md" inputId="desk-target" placeholder="Boş bırakırsanız kendi adınıza" (picked)="setTarget($event)" />
                </div>
              }

              <div class="flex flex-wrap items-center gap-x-5 gap-y-2 border-t border-line pt-4">
                @for (building of buildings(); track building.id) {
                  <div class="flex flex-wrap items-center gap-2">
                    <span class="inline-flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-ink-400">
                      <svg uiIcon name="Building2" class="h-3.5 w-3.5"></svg>
                      {{ building.name }}
                    </span>
                    @for (floor of building.floors; track floor.id) {
                      @let active = floor.id === selectedFloor()?.id;
                      <a
                        routerLink="/panel/masa"
                        [queryParams]="{ date: dayKey(), floor: floor.id }"
                        [attr.aria-current]="active ? 'page' : null"
                        [class]="cn('inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-[13px] font-medium transition-colors', active ? 'border-brand-500 bg-brand-600 text-white' : floor.reservable ? 'border-line bg-white text-ink-700 hover:border-brand-200 hover:bg-brand-50' : 'border-line bg-slate-50 text-ink-400 hover:bg-slate-100')"
                      >
                        @if (!floor.reservable) {
                          <svg uiIcon name="Lock" class="h-3.5 w-3.5"></svg>
                        }
                        {{ floor.name }}
                      </a>
                    }
                  </div>
                }
              </div>
            </ui-card-body>
          </div>

          @if (outsideWindow()) {
            <ui-alert tone="warning" title="Rezervasyon penceresi dışında" hasIcon>
              <svg uiIcon icon name="Scale" class="h-4 w-4"></svg>
              Rezervasyonlar en fazla {{ ctx.settings.advanceBookingDays.days }} iş günü öncesinden yapılabilir.
              {{ formatLongDate(ctx.latestBookableDayKey) }} tarihine kadar seçim yapabilirsiniz.
            </ui-alert>
          }

          @if (plan.data(); as p) {
            <div [class.opacity-60]="plan.loading()" class="transition-opacity">
              <app-floor-plan-board [plan]="p" [dayKey]="dayKey()" [readOnlyReason]="readOnlyReason()" [targetUserId]="target()?.id ?? null" [targetName]="target()?.fullName ?? null" (changed)="reloadPlan(true)" />
            </div>
          } @else if (plan.error()) {
            <ui-page-error [code]="plan.error()" (retry)="reloadPlan()" />
          } @else {
            <div class="grid gap-5 lg:grid-cols-[minmax(0,1fr)_360px]" role="status" aria-live="polite">
              <span class="sr-only">Kat planı yükleniyor</span>
              <div class="space-y-4">
                <div ui-skeleton class="h-40"></div>
                <div ui-skeleton class="h-[420px]"></div>
              </div>
              <div ui-skeleton class="h-72"></div>
            </div>
          }
        </div>
      }
    }
  `,
})
export class DeskPage {
  private readonly api = inject(ApiClient);
  private readonly contextService = inject(EmployeeContextService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  readonly cn = cn;
  readonly addDaysToKey = addDaysToKey;
  readonly formatLongDate = formatLongDate;
  readonly today = todayKey();

  readonly context = new PageState<EmployeeContext>();
  readonly plan = new PageState<FloorPlanView>();

  private readonly query = toSignal(this.route.queryParamMap.pipe(map((params) => ({ date: params.get('date'), floor: params.get('floor') }))), {
    initialValue: { date: this.route.snapshot.queryParamMap.get('date'), floor: this.route.snapshot.queryParamMap.get('floor') },
  });

  readonly dayKey = computed(() => {
    const requested = this.query().date;
    const valid = requested && isValidDayKey(requested) ? requested : this.today;
    return valid < this.today ? this.today : valid;
  });

  readonly buildings = computed<BuildingGroup[]>(() => {
    const groups = new Map<string, BuildingGroup>();
    for (const floor of this.context.data()?.floors ?? []) {
      const entry = groups.get(floor.buildingId) ?? { id: floor.buildingId, name: floor.buildingName, code: floor.buildingCode, floors: [] };
      entry.floors.push(floor);
      groups.set(floor.buildingId, entry);
    }
    return [...groups.values()];
  });

  readonly selectedFloor = computed(() => {
    const floors = this.context.data()?.floors ?? [];
    const requested = this.query().floor;
    const preferred = this.auth.user()?.preferredFloorId;
    return (
      floors.find((floor) => floor.id === requested) ??
      floors.find((floor) => floor.id === preferred && floor.reservable) ??
      floors.find((floor) => floor.reservable) ??
      floors[0] ??
      null
    );
  });

  readonly outsideWindow = computed(() => {
    const latest = this.context.data()?.latestBookableDayKey;
    return latest ? this.dayKey() > latest : false;
  });

  readonly readOnlyReason = computed(() => {
    const p = this.plan.data();
    if (this.outsideWindow()) return 'Bu tarih rezervasyon penceresinin dışında olduğu için işlem yapılamaz.';
    if (p && !p.floor.reservable) return `${p.floor.buildingName} · ${p.floor.name} çalışan rezervasyonuna kapalıdır.`;
    return null;
  });

  private readonly planKey = signal<string | null>(null);

  readonly target = signal<PersonResult | null>(null);
  readonly canActForOthers = computed(() => {
    const user = this.auth.user();
    return !!user && (user.role === 'ADMIN' || user.isSecretary);
  });

  setTarget(person: PersonResult | null): void {
    this.target.set(person);
  }

  private planParams(day: string): Record<string, string | undefined> {
    return { date: day, asUser: this.target()?.id };
  }

  constructor() {
    void this.loadContext();
    // Kat veya tarih değişince plan yeniden yüklenir.
    effect(() => {
      const floor = this.selectedFloor();
      const day = this.dayKey();
      if (!floor) return;
      const key = `${floor.id}|${day}|${this.target()?.id ?? ''}`;
      if (this.planKey() === key) return;
      this.planKey.set(key);
      void this.plan.load(() => this.api.get<FloorPlanView>(`floors/${floor.id}/plan`, this.planParams(day)), { silent: this.plan.data() !== null });
    });
  }

  loadContext(): Promise<unknown> {
    return this.context.load(() => this.contextService.load());
  }

  reloadPlan(silent = false): void {
    const floor = this.selectedFloor();
    if (!floor) return;
    void this.plan.load(() => this.api.get<FloorPlanView>(`floors/${floor.id}/plan`, this.planParams(this.dayKey())), { silent });
  }
}
