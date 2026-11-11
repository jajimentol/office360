import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { cn } from '../../lib/utils';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import type { DeskDto, FeatureDto, FloorDto, Paged } from '../../core/api/types';
import {
  PageState,
  ToastService,
  UiBadge,
  UiButton,
  UiCard,
  UiCardBody,
  UiCardHeader,
  UiConfirmDialog,
  UiDataTable,
  UiEmptyState,
  UiField,
  UiIcon,
  UiInput,
  UiModal,
  UiPageError,
  UiPageHeader,
  UiPageLoading,
  UiSelect,
  UiSwitch,
  UiTd,
  UiTextarea,
} from '../../shared/ui';
import { AdminFilterLink, AdminFormError, AdminPageNav, floorLabelOf } from './admin-shared';

/** Kaynak: (app)/admin/masalar/page.tsx + components/admin/desk-manager.tsx */

interface DeskForm {
  code: string;
  floorId: string;
  zoneId: string;
  x: number;
  y: number;
  active: boolean;
  accessibleOnly: boolean;
  popular: boolean;
  notes: string;
  featureKeys: string[];
}

const EMPTY_FORM: DeskForm = { code: '', floorId: '', zoneId: '', x: 200, y: 200, active: true, accessibleOnly: false, popular: false, notes: '', featureKeys: [] };
const PAGE_SIZE = 60;

interface PageData {
  desks: Paged<DeskDto>;
  floors: FloorDto[];
  features: FeatureDto[];
}

@Component({
  selector: 'app-desks-page',
  imports: [
    FormsModule,
    RouterLink,
    UiPageHeader,
    UiPageLoading,
    UiPageError,
    UiCard,
    UiCardHeader,
    UiCardBody,
    UiDataTable,
    UiTd,
    UiEmptyState,
    UiBadge,
    UiButton,
    UiIcon,
    UiModal,
    UiConfirmDialog,
    UiField,
    UiInput,
    UiSelect,
    UiTextarea,
    UiSwitch,
    AdminFilterLink,
    AdminPageNav,
    AdminFormError,
  ],
  template: `
    <ui-page-header title="Masa yönetimi" description="Masa ekleyin, özelliklerini tanımlayın, koordinatlarını düzenleyin veya geçici olarak kullanıma kapatın." />

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as d) {
      @let q = query();
      <div ui-card class="mb-5">
        <ui-card-body>
          <div class="flex flex-wrap items-center gap-2">
            <span class="text-xs font-semibold uppercase tracking-wide text-ink-400">Kat</span>
            <a adminFilterLink routerLink="/admin/masalar" [active]="!q.floor && q.durum !== 'kapali'">Tümü</a>
            @for (floor of d.floors; track floor.id) {
              <a adminFilterLink routerLink="/admin/masalar" [queryParams]="{ floor: floor.id }" [active]="q.floor === floor.id">{{ floorLabel(floor) }} ({{ floor.deskCount }})</a>
            }
            <span class="mx-1 h-5 w-px bg-line" aria-hidden="true"></span>
            <a adminFilterLink routerLink="/admin/masalar" [queryParams]="{ durum: 'kapali' }" [active]="q.durum === 'kapali'">Yalnızca kapalı masalar</a>
            <a adminFilterLink routerLink="/admin/masalar" [queryParams]="{ durum: 'yerlesmemis' }" [active]="q.durum === 'yerlesmemis'">Yerleştirilmemiş masalar</a>
            <a adminFilterLink routerLink="/admin/masalar" [queryParams]="{ durum: 'yerlesik' }" [active]="q.durum === 'yerlesik'">Krokiye yerleştirilmiş</a>
          </div>
          <form class="mt-3 flex flex-wrap items-center gap-2" (ngSubmit)="search()">
            <label for="desk-search" class="text-xs font-semibold uppercase tracking-wide text-ink-400">Masa kodu ara</label>
            <input id="desk-search" name="q" [(ngModel)]="searchText" placeholder="Örn. PT36-A05-01" class="h-9 w-52 rounded-lg border border-line px-3 text-[13px] outline-none focus:border-brand-400" />
            <button type="submit" class="h-9 rounded-lg border border-line bg-white px-3 text-[13px] font-medium text-ink-700 hover:border-brand-200 hover:bg-brand-50">Ara</button>
          </form>
        </ui-card-body>
      </div>

      <div ui-card>
        <ui-card-header title="Masalar" [description]="d.desks.total + ' kayıt · ' + page() + '/' + pageCount() + '. sayfa (' + d.desks.items.length + ' gösteriliyor)'" hasIcon>
          <svg uiIcon icon name="Grid2x2" class="h-4.5 w-4.5"></svg>
        </ui-card-header>
        <ui-card-body>
          <div class="mb-4 flex justify-end">
            <button ui-button type="button" (click)="openCreate()">
              <svg uiIcon name="Plus" class="h-4 w-4"></svg>
              Masa ekle
            </button>
          </div>

          @if (d.desks.items.length === 0) {
            <ui-empty-state title="Kayıtlı masa bulunamadı" description="Filtreleri değiştirin veya yeni bir masa ekleyin." />
          } @else {
            <ui-data-table caption="Masa listesi" [headers]="[{ label: 'Masa' }, { label: 'Konum' }, { label: 'Koordinat' }, { label: 'Özellikler' }, { label: 'Rezervasyon' }, { label: 'Durum' }, { label: '', className: 'text-right' }]">
              @for (desk of d.desks.items; track desk.id) {
                <tr [class]="cn('hover:bg-slate-50/60', !desk.active && 'bg-red-50/30')">
                  <td ui-td>
                    <span class="font-semibold text-ink-900">{{ desk.code }}</span>
                    @if (desk.notes) {
                      <span class="mt-0.5 block max-w-[220px] truncate text-xs text-ink-500">{{ desk.notes }}</span>
                    }
                  </td>
                  <td ui-td>
                    {{ desk.floor?.name }}
                    <span class="block text-xs text-ink-500">{{ desk.zone?.name }}</span>
                  </td>
                  <td ui-td class="tabular-nums text-ink-500">{{ desk.x }}, {{ desk.y }}</td>
                  <td ui-td>
                    <div class="flex max-w-[240px] flex-wrap gap-1">
                      @if (desk.popular) {
                        <span ui-badge tone="warning">★ Popüler</span>
                      }
                      @if (desk.accessibleOnly) {
                        <span ui-badge tone="violet">
                          <svg uiIcon name="Accessibility" class="h-3 w-3"></svg>
                          Erişilebilir
                        </span>
                      }
                      @for (feature of desk.features.slice(0, 2); track feature.key) {
                        <span ui-badge tone="neutral">{{ feature.name }}</span>
                      }
                      @if (desk.features.length > 2) {
                        <span ui-badge tone="neutral">+{{ desk.features.length - 2 }}</span>
                      }
                    </div>
                  </td>
                  <td ui-td class="tabular-nums">{{ desk.reservationCount }}</td>
                  <td ui-td>
                    @if (!desk.placed) {
                      <span ui-badge tone="warning">Yerleştirilmedi</span>
                    }
                    <span ui-badge [tone]="desk.active ? 'success' : 'danger'">{{ desk.active ? 'Kullanımda' : 'Kapalı' }}</span>
                  </td>
                  <td ui-td class="text-right">
                    <div class="flex justify-end gap-1.5">
                      <button ui-button type="button" size="sm" variant="ghost" (click)="toggleActive(desk)">{{ desk.active ? 'Kapat' : 'Aç' }}</button>
                      <button ui-button type="button" size="sm" variant="secondary" (click)="openEdit(desk)">
                        <svg uiIcon name="Pencil" class="h-3.5 w-3.5"></svg>
                        Düzenle
                      </button>
                      <button ui-button type="button" size="sm" variant="ghost" [attr.aria-label]="desk.code + ' masasını sil'" (click)="deleteTarget.set(desk)">
                        <svg uiIcon name="Trash2" class="h-3.5 w-3.5 text-red-600"></svg>
                      </button>
                    </div>
                  </td>
                </tr>
              }
            </ui-data-table>
          }

          @if (pageCount() > 1) {
            <admin-page-nav class="mt-4 border-t border-line pt-4" aria-label="Masa listesi sayfaları" [page]="page()" [pageCount]="pageCount()" [skip]="d.desks.skip" [shown]="d.desks.items.length" [total]="d.desks.total" [query]="{ floor: q.floor, durum: q.durum, q: q.q }" />
          }
        </ui-card-body>
      </div>

      <!-- Masa formu -->
      <ui-modal [open]="formOpen()" (closed)="closeForm()" [title]="editingId() ? 'Masayı düzenle' : 'Yeni masa ekle'" description="Kat planındaki konum, özellikler ve kullanım durumu." size="lg">
        <div class="space-y-4">
          @if (formError()) {
            <admin-form-error>{{ formError() }}</admin-form-error>
          }

          <div class="grid gap-4 sm:grid-cols-3">
            <ui-field label="Masa kodu" required htmlFor="desk-code" hint="Örn. PT36-A05-07">
              <input ui-input id="desk-code" [(ngModel)]="form.code" />
            </ui-field>
            <ui-field label="Kat" required htmlFor="desk-floor">
              <select ui-select id="desk-floor" [ngModel]="form.floorId" (ngModelChange)="changeFloor($event)">
                @for (floor of d.floors; track floor.id) {
                  <option [value]="floor.id">{{ floorLabel(floor) }}</option>
                }
              </select>
            </ui-field>
            <ui-field label="Bölge" required htmlFor="desk-zone">
              <select ui-select id="desk-zone" [(ngModel)]="form.zoneId">
                @for (zone of zonesForFloor(); track zone.id) {
                  <option [value]="zone.id">{{ zone.name }}{{ zone.reservable ? '' : ' (rezervasyona kapalı)' }}</option>
                }
              </select>
            </ui-field>
          </div>

          <div class="grid gap-4 sm:grid-cols-2">
            <ui-field label="X koordinatı" htmlFor="desk-x">
              <input ui-input id="desk-x" type="number" [(ngModel)]="form.x" />
            </ui-field>
            <ui-field label="Y koordinatı" htmlFor="desk-y">
              <input ui-input id="desk-y" type="number" [(ngModel)]="form.y" />
            </ui-field>
          </div>

          <div>
            <p class="mb-2 text-sm font-medium text-ink-700">Masa özellikleri</p>
            <div class="flex flex-wrap gap-1.5">
              @for (feature of d.features; track feature.key) {
                @let active = form.featureKeys.includes(feature.key);
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

          <ui-field label="Not" htmlFor="desk-notes">
            <textarea ui-textarea id="desk-notes" rows="2" [(ngModel)]="form.notes" placeholder="Örneğin: Pencere kenarı, yoğun tercih edilen masa."></textarea>
          </ui-field>

          <div class="space-y-2">
            <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
              <span>
                <span class="block text-sm text-ink-900">Kullanıma açık</span>
                <span class="block text-xs text-ink-500">Kapatıldığında çalışanlar bu masayı seçemez.</span>
              </span>
              <ui-switch [checked]="form.active" (checkedChange)="form.active = $event" label="Kullanıma açık" />
            </label>
            <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
              <span>
                <span class="block text-sm text-ink-900">Popüler masa</span>
                <span class="block text-xs text-ink-500">Adil kullanım kurallarındaki popüler masa sınırlarına tabi olur.</span>
              </span>
              <ui-switch [checked]="form.popular" (checkedChange)="form.popular = $event" label="Popüler masa" />
            </label>
            <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
              <span>
                <span class="block text-sm text-ink-900">Erişilebilirlik için ayrılmış</span>
                <span class="block text-xs text-ink-500">Yalnızca erişilebilirlik yetkisi olan çalışanlar seçebilir.</span>
              </span>
              <ui-switch [checked]="form.accessibleOnly" (checkedChange)="form.accessibleOnly = $event" label="Erişilebilirlik için ayrılmış" />
            </label>
          </div>
        </div>
        <ng-container footer>
          <button ui-button type="button" variant="secondary" (click)="closeForm()" [disabled]="pending()">Vazgeç</button>
          <button ui-button type="button" (click)="submit()" [loading]="pending()">
            <svg uiIcon name="Save" class="h-4 w-4"></svg>
            Kaydet
          </button>
        </ng-container>
      </ui-modal>

      <ui-confirm-dialog
        [open]="deleteTarget() !== null"
        (closed)="deleteTarget.set(null)"
        (confirmed)="confirmDelete()"
        [loading]="pending()"
        title="Masayı sil"
        description="Rezervasyon geçmişi olan masalar silinemez; bunun yerine kullanıma kapatılabilir."
        confirmLabel="Masayı sil"
        hasContent
      >
        <p class="text-sm text-ink-700">
          <strong class="font-semibold text-ink-900">{{ deleteTarget()?.code }}</strong> kodlu masayı silmek istediğinize emin misiniz? Bu işlem geri alınamaz.
        </p>
      </ui-confirm-dialog>
    }
  `,
})
export class DesksPage {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly cn = cn;
  readonly floorLabel = floorLabelOf;
  readonly state = new PageState<PageData>();

  readonly query = toSignal(
    this.route.queryParamMap.pipe(map((p) => ({ floor: p.get('floor'), durum: p.get('durum'), q: p.get('q'), sayfa: p.get('sayfa') }))),
    { initialValue: { floor: null, durum: null, q: null, sayfa: null } as { floor: string | null; durum: string | null; q: string | null; sayfa: string | null } },
  );
  readonly page = computed(() => Math.max(1, Number(this.query().sayfa ?? '1') || 1));
  readonly pageCount = computed(() => Math.max(1, Math.ceil((this.state.data()?.desks.total ?? 0) / PAGE_SIZE)));
  searchText = '';

  readonly formOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formError = signal<string | null>(null);
  readonly pending = signal(false);
  readonly deleteTarget = signal<DeskDto | null>(null);
  form: DeskForm = { ...EMPTY_FORM };
  private readonly formFloorId = signal('');

  readonly zonesForFloor = computed(() => this.state.data()?.floors.find((floor) => floor.id === this.formFloorId())?.zones ?? []);

  constructor() {
    effect(() => {
      this.query();
      void this.load(untracked(() => this.state.data()) !== null);
    });
  }

  load(silent = false): Promise<unknown> {
    const q = this.query();
    this.searchText = q.q ?? '';
    return this.state.load(
      async () => {
        const [desks, floors, features] = await Promise.all([
          this.api.get<Paged<DeskDto>>('admin/desks', {
            floorId: q.floor,
            onlyInactive: q.durum === 'kapali' ? true : undefined,
            placement: q.durum === 'yerlesmemis' ? 'unplaced' : q.durum === 'yerlesik' ? 'placed' : undefined,
            search: q.q,
            take: PAGE_SIZE,
            skip: (this.page() - 1) * PAGE_SIZE,
          }),
          this.api.get<FloorDto[]>('admin/floors'),
          this.api.get<FeatureDto[]>('admin/desk-features'),
        ]);
        return { desks, floors, features };
      },
      { silent },
    );
  }

  search(): void {
    const q = this.query();
    void this.router.navigate([], { relativeTo: this.route, queryParams: { floor: q.floor, durum: q.durum, q: this.searchText.trim() || null } });
  }

  openCreate(): void {
    const firstFloor = this.state.data()?.floors[0];
    this.form = { ...EMPTY_FORM, floorId: firstFloor?.id ?? '', zoneId: firstFloor?.zones?.[0]?.id ?? '' };
    this.formFloorId.set(this.form.floorId);
    this.formError.set(null);
    this.editingId.set(null);
    this.formOpen.set(true);
  }

  openEdit(desk: DeskDto): void {
    this.form = {
      code: desk.code,
      floorId: desk.floorId,
      zoneId: desk.zoneId,
      x: desk.x,
      y: desk.y,
      active: desk.active,
      accessibleOnly: desk.accessibleOnly,
      popular: desk.popular,
      notes: desk.notes ?? '',
      featureKeys: desk.features.map((feature) => feature.key),
    };
    this.formFloorId.set(desk.floorId);
    this.formError.set(null);
    this.editingId.set(desk.id);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
    this.editingId.set(null);
  }

  changeFloor(floorId: string): void {
    const floor = this.state.data()?.floors.find((item) => item.id === floorId);
    this.form.floorId = floorId;
    this.form.zoneId = floor?.zones?.[0]?.id ?? '';
    this.formFloorId.set(floorId);
  }

  toggleFeature(key: string): void {
    this.form.featureKeys = this.form.featureKeys.includes(key) ? this.form.featureKeys.filter((k) => k !== key) : [...this.form.featureKeys, key];
  }

  async submit(): Promise<void> {
    this.pending.set(true);
    this.formError.set(null);
    const payload = {
      code: this.form.code.trim().toUpperCase(),
      floorId: this.form.floorId,
      zoneId: this.form.zoneId,
      x: Number(this.form.x),
      y: Number(this.form.y),
      active: this.form.active,
      accessibleOnly: this.form.accessibleOnly,
      popular: this.form.popular,
      notes: this.form.notes.trim() || null,
      featureKeys: this.form.featureKeys,
    };
    try {
      const id = this.editingId();
      if (id) await this.api.put(`admin/desks/${id}`, payload);
      else await this.api.post('admin/desks', payload);
      this.toasts.toast({ tone: 'success', title: id ? 'Masa güncellendi' : 'Masa eklendi', description: `${payload.code} kaydedildi.` });
      this.closeForm();
      void this.load(true);
    } catch (error: unknown) {
      this.formError.set(errorMessage(error));
    } finally {
      this.pending.set(false);
    }
  }

  async toggleActive(desk: DeskDto): Promise<void> {
    try {
      await this.api.put(`admin/desks/${desk.id}`, { active: !desk.active });
      this.toasts.toast({ tone: 'success', title: desk.active ? 'Masa kullanıma kapatıldı' : 'Masa yeniden kullanıma açıldı', description: `${desk.code} güncellendi.` });
      void this.load(true);
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'İşlem başarısız', description: errorMessage(error) });
    }
  }

  async confirmDelete(): Promise<void> {
    const target = this.deleteTarget();
    if (!target) return;
    this.pending.set(true);
    try {
      await this.api.delete(`admin/desks/${target.id}`);
      this.toasts.toast({ tone: 'success', title: 'Masa silindi', description: `${target.code} kaldırıldı.` });
      void this.load(true);
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Masa silinemedi', description: errorMessage(error) });
    } finally {
      this.deleteTarget.set(null);
      this.pending.set(false);
    }
  }
}
