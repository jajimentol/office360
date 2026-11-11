import { Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { cn } from '../../lib/utils';
import { ApiClient } from '../../core/api/api-client';
import type { BuildingDto, DeskDto, FloorCapacityStats, FloorDto, FloorPlanSnapshot, RoomDto } from '../../core/api/types';
import { PageState, UiAlert, UiBadge, UiCard, UiCardBody, UiCardHeader, UiIcon, UiPageError, UiPageHeader, UiPageLoading, UiSkeleton } from '../../shared/ui';
import { AdminStatTile, floorLabelOf } from './admin-shared';
import { BuildingSettingsForm, CreateBuildingButton, CreateFloorButton, FloorSettingsForm, ZoneTable, type BuildingOption, type ZoneRow } from './floor-manager';
import { PlanEditor } from './plan-editor/plan-editor';

/** Kaynak: (app)/admin/katlar/page.tsx */

interface ListData {
  buildings: BuildingDto[];
  floors: FloorDto[];
  capacity: FloorCapacityStats[];
}

interface DetailData {
  floor: FloorDto;
  desks: DeskDto[];
  rooms: RoomDto[];
  snapshot: FloorPlanSnapshot;
}

@Component({
  selector: 'app-floors-page',
  imports: [
    RouterLink,
    UiPageHeader,
    UiPageLoading,
    UiPageError,
    UiCard,
    UiCardHeader,
    UiCardBody,
    UiAlert,
    UiBadge,
    UiIcon,
    UiSkeleton,
    AdminStatTile,
    BuildingSettingsForm,
    CreateBuildingButton,
    CreateFloorButton,
    FloorSettingsForm,
    ZoneTable,
    PlanEditor,
  ],
  template: `
    @if (list.loading()) {
      <ui-page-loading />
    } @else if (list.error()) {
      <ui-page-error [code]="list.error()" (retry)="loadList()" />
    } @else if (list.data(); as d) {
      <ui-page-header title="Bina, kat ve alan yönetimi" description="Binaları, katları, bölgeleri ve kat planı yerleşimini yönetin.">
        <ng-container actions>
          <app-create-building-button (created)="loadList(true)" />
          <app-create-floor-button [nextNumber]="nextNumber()" [buildings]="buildingOptions()" [defaultBuildingId]="defaultBuildingId()" (created)="loadList(true)" />
        </ng-container>
      </ui-page-header>

      <div ui-card class="mb-5">
        <ui-card-body class="space-y-3">
          @for (building of d.buildings; track building.id) {
            @let buildingFloors = floorsOf(building.id);
            <div class="flex flex-wrap items-center gap-2">
              <span class="inline-flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-ink-400">
                <svg uiIcon name="Building2" class="h-3.5 w-3.5"></svg>
                {{ building.name }}
              </span>
              @if (!building.active) {
                <span ui-badge tone="neutral">Çalışanlara kapalı{{ building.isDemo ? ' · demo' : '' }}</span>
              }
              @if (buildingFloors.length === 0) {
                <span class="text-[12.5px] text-ink-400">Henüz kat yok</span>
              }
              @for (floor of buildingFloors; track floor.id) {
                @let stats = statsOf(floor.id);
                @let active = floor.id === selectedId();
                <a
                  routerLink="/admin/katlar"
                  [queryParams]="{ floor: floor.id }"
                  [attr.aria-current]="active ? 'page' : null"
                  [class]="cn('inline-flex items-center gap-2 rounded-lg border px-3 py-1.5 text-[13px] font-medium transition-colors', active ? 'border-brand-500 bg-brand-600 text-white' : building.active ? 'border-line bg-white text-ink-700 hover:border-brand-200 hover:bg-brand-50' : 'border-line bg-slate-50 text-ink-500 hover:bg-slate-100')"
                >
                  {{ floor.name }}
                  <span [class]="cn('text-[11px]', active ? 'text-white/70' : 'text-ink-400')">{{ floor.deskCount }} masa</span>
                  @if ((stats?.unplacedDesks ?? 0) > 0) {
                    <span [class]="cn('rounded px-1.5 py-0.5 text-[11px] font-semibold', active ? 'bg-white/20 text-white' : 'bg-amber-100 text-amber-800')" title="Krokiye yerleştirilmemiş masa">{{ stats?.unplacedDesks }} bekliyor</span>
                  }
                </a>
              }
            </div>
          }
        </ui-card-body>
      </div>

      @if (selectedBuilding(); as sb) {
        @if (!sb.active) {
          <ui-alert tone="info" class="mb-5" [title]="sb.name + ' çalışan erişimine kapalı'">
            Bu binanın katları çalışan ekranlarında, rezervasyonda ve raporlarda görünmez. Kroki, masa ve rezervasyon verileri korunur; bina bilgileri kartından yeniden açabilirsiniz.
          </ui-alert>
        }
      }

      @if (selectedStats(); as st) {
        <section class="mb-5" aria-label="Kat kapasite sayaçları">
          <div class="grid gap-3 sm:grid-cols-3 lg:grid-cols-5">
            <admin-stat-tile label="Toplam masa" [value]="st.totalDesks" />
            <admin-stat-tile label="Krokiye yerleştirilmiş" [value]="st.placedDesks" tone="success" />
            <admin-stat-tile label="Yerleştirilmemiş" [value]="st.unplacedDesks" [tone]="st.unplacedDesks > 0 ? 'warning' : undefined" [hint]="st.unplacedDesks > 0 ? 'Rezervasyona kapalı' : undefined" />
            <admin-stat-tile label="Aktif masa" [value]="st.activeDesks" />
            <admin-stat-tile label="Kapalı masa" [value]="st.closedDesks" [tone]="st.closedDesks > 0 ? 'danger' : undefined" />
          </div>
          <p class="mt-2 text-[12.5px] text-ink-500">
            {{ st.buildingName }} · {{ st.floorName }}: {{ st.totalRooms }} toplantı odası ({{ st.placedRooms }} yerleşik{{ st.unplacedRooms > 0 ? ', ' + st.unplacedRooms + ' bekliyor' : '' }}).
            {{ st.reservable ? ' Bu kat çalışan rezervasyonuna açıktır.' : ' Bu kat çalışan rezervasyonuna kapalıdır.' }}
          </p>
        </section>
      }

      @if (detail.data(); as det) {
        <div class="space-y-5" [class.opacity-60]="detail.loading()">
          <div ui-card>
            <ui-card-header title="Kat planı editörü" description="Krokiyi sıfırdan çizin, ardından masa ve toplantı odalarını krokinin üzerine yerleştirin." hasIcon>
              <svg uiIcon icon name="Move" class="h-4.5 w-4.5"></svg>
            </ui-card-header>
            <ui-card-body>
              <app-plan-editor
                [floor]="{ id: det.snapshot.floor.id, name: floorLabel(det.floor), planWidth: det.snapshot.floor.planWidth, planHeight: det.snapshot.floor.planHeight }"
                [initialSnapshot]="det.snapshot"
                [zones]="det.snapshot.zones"
                [otherFloors]="otherFloors()"
                (saved)="refreshAll()"
              />
            </ui-card-body>
          </div>

          <div class="grid gap-5 lg:grid-cols-2">
            <div ui-card>
              <ui-card-header title="Kat bilgileri" hasIcon>
                <svg uiIcon icon name="Layers" class="h-4.5 w-4.5"></svg>
                <span action ui-badge [tone]="det.floor.reservable ? 'success' : 'warning'">{{ det.floor.reservable ? 'Rezervasyona açık' : 'Rezervasyona kapalı' }}</span>
              </ui-card-header>
              <ui-card-body>
                <app-floor-settings-form
                  [floor]="{ id: det.floor.id, buildingId: det.floor.buildingId, number: det.floor.number, name: det.floor.name, description: det.floor.description, reservable: det.floor.reservable, active: det.floor.active, planWidth: det.floor.planWidth, planHeight: det.floor.planHeight }"
                  [buildings]="buildingOptions()"
                  (saved)="refreshAll()"
                />
              </ui-card-body>
            </div>

            @if (selectedBuilding(); as sb) {
              <div ui-card>
                <ui-card-header title="Bina bilgileri" [description]="sb.address || undefined" hasIcon>
                  <svg uiIcon icon name="Building2" class="h-4.5 w-4.5"></svg>
                  <span action ui-badge [tone]="sb.active ? 'success' : 'neutral'">{{ sb.active ? 'Çalışanlara açık' : 'Çalışanlara kapalı' }}</span>
                </ui-card-header>
                <ui-card-body>
                  <app-building-settings-form [building]="{ id: sb.id, code: sb.code, name: sb.name, address: sb.address, order: sb.order, active: sb.active, isDemo: sb.isDemo, floorCount: sb.floorCount ?? 0 }" (saved)="refreshAll()" />
                </ui-card-body>
              </div>
            }
          </div>

          <div ui-card>
            <ui-card-header title="Bölgeler" [description]="zoneRows().length + ' bölge tanımlı'" hasIcon>
              <svg uiIcon icon name="Layers" class="h-4.5 w-4.5"></svg>
            </ui-card-header>
            <app-zone-table [zones]="zoneRows()" (saved)="refreshAll()" />
          </div>
        </div>
      } @else if (detail.error()) {
        <ui-page-error [code]="detail.error()" (retry)="loadDetail()" />
      } @else if (selectedId()) {
        <div ui-skeleton class="h-[560px]" role="status" aria-live="polite"></div>
      }
    }
  `,
})
export class FloorsPage {
  private readonly api = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);

  readonly cn = cn;
  readonly floorLabel = floorLabelOf;
  readonly list = new PageState<ListData>();
  readonly detail = new PageState<DetailData>();

  private readonly requestedFloor = toSignal(this.route.queryParamMap.pipe(map((p) => p.get('floor'))), { initialValue: this.route.snapshot.queryParamMap.get('floor') });

  // Varsayılan seçim: aktif bir binanın ilk katı; yoksa ilk kat.
  readonly selectedId = computed(() => {
    const d = this.list.data();
    if (!d) return null;
    const requested = this.requestedFloor();
    return d.floors.find((floor) => floor.id === requested)?.id ?? d.floors.find((floor) => floor.building?.active)?.id ?? d.floors[0]?.id ?? null;
  });

  readonly selectedStats = computed(() => this.list.data()?.capacity.find((item) => item.floorId === this.selectedId()) ?? null);
  readonly selectedBuilding = computed(() => {
    const det = this.detail.data();
    const d = this.list.data();
    if (!det || !d) return null;
    return d.buildings.find((building) => building.id === det.floor.buildingId) ?? null;
  });
  readonly defaultBuildingId = computed(() => {
    const d = this.list.data();
    return this.selectedBuilding()?.id ?? d?.buildings.find((building) => building.active)?.id ?? d?.buildings[0]?.id ?? '';
  });
  readonly nextNumber = computed(() => {
    const d = this.list.data();
    const buildingId = this.defaultBuildingId();
    return Math.max(0, ...(d?.floors ?? []).filter((floor) => floor.buildingId === buildingId).map((floor) => floor.number)) + 1;
  });
  readonly buildingOptions = computed<BuildingOption[]>(() => (this.list.data()?.buildings ?? []).map((building) => ({ id: building.id, name: building.name, active: building.active })));
  readonly otherFloors = computed(() =>
    (this.list.data()?.floors ?? []).filter((floor) => floor.id !== this.selectedId()).map((floor) => ({ id: floor.id, name: floorLabelOf(floor) })),
  );
  readonly zoneRows = computed<ZoneRow[]>(() => {
    const det = this.detail.data();
    if (!det) return [];
    return (det.floor.zones ?? []).map((zone) => ({ ...zone, deskCount: det.desks.filter((desk) => desk.zoneId === zone.id).length }));
  });

  private detailKey = signal<string | null>(null);

  constructor() {
    void this.loadList();
    effect(() => {
      const id = this.selectedId();
      if (!id || id === this.detailKey()) return;
      this.detailKey.set(id);
      void this.loadDetail();
    });
  }

  floorsOf(buildingId: string): FloorDto[] {
    return (this.list.data()?.floors ?? []).filter((floor) => floor.buildingId === buildingId);
  }

  statsOf(floorId: string): FloorCapacityStats | undefined {
    return this.list.data()?.capacity.find((item) => item.floorId === floorId);
  }

  loadList(silent = false): Promise<unknown> {
    return this.list.load(
      async () => {
        const [buildings, floors, capacity] = await Promise.all([
          this.api.get<BuildingDto[]>('admin/buildings', { includeInactive: true }),
          this.api.get<FloorDto[]>('admin/floors'),
          this.api.get<FloorCapacityStats[]>('admin/floors/capacity'),
        ]);
        return { buildings, floors, capacity };
      },
      { silent },
    );
  }

  loadDetail(silent = false): Promise<unknown> {
    const id = this.selectedId();
    if (!id) return Promise.resolve(null);
    return this.detail.load(
      async () => {
        const [detail, snapshot] = await Promise.all([
          this.api.get<{ floor: FloorDto; desks: DeskDto[]; rooms: RoomDto[] }>(`admin/floors/${id}`),
          this.api.get<FloorPlanSnapshot>(`admin/floors/${id}/plan-snapshot`),
        ]);
        return { ...detail, snapshot };
      },
      { silent },
    );
  }

  /** router.refresh() karşılığı: liste + detay birlikte yenilenir. */
  refreshAll(): void {
    void this.loadList(true);
    void this.loadDetail(true);
  }
}
