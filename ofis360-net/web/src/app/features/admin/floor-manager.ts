import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ZONE_KINDS, ZONE_KIND_LABELS, type ZoneKind } from '../../lib/enums';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import type { ZoneDto } from '../../core/api/types';
import { ToastService, UiBadge, UiButton, UiDataTable, UiField, UiIcon, UiInput, UiModal, UiSelect, UiSwitch, UiTd, UiTextarea } from '../../shared/ui';

/** Kaynak: components/admin/floor-manager.tsx */

export interface BuildingOption {
  id: string;
  name: string;
  active: boolean;
}

export interface FloorFormValues {
  id: string;
  buildingId: string;
  number: number;
  name: string;
  description: string;
  reservable: boolean;
  active: boolean;
  planWidth: number;
  planHeight: number;
}

export interface BuildingFormValues {
  id: string;
  code: string;
  name: string;
  address: string;
  order: number;
  active: boolean;
  isDemo: boolean;
  floorCount: number;
}

/** Bina bilgilerini düzenleme formu (ad, kod, sıra, çalışan erişimi). */
@Component({
  selector: 'app-building-settings-form',
  imports: [FormsModule, UiField, UiInput, UiSwitch, UiButton, UiIcon],
  template: `
    @let v = values();
    <div class="space-y-4">
      <div class="grid gap-4 sm:grid-cols-[1fr_140px_120px]">
        <ui-field label="Bina adı" required htmlFor="building-name">
          <input ui-input id="building-name" [ngModel]="v.name" (ngModelChange)="patch({ name: $event })" />
        </ui-field>
        <ui-field label="Kod" required htmlFor="building-code" hint="Masa kodlarının öneki">
          <input ui-input id="building-code" [ngModel]="v.code" (ngModelChange)="patch({ code: $event.toUpperCase() })" />
        </ui-field>
        <ui-field label="Sıra" htmlFor="building-order">
          <input ui-input id="building-order" type="number" [ngModel]="v.order" (ngModelChange)="patch({ order: Number($event) })" />
        </ui-field>
      </div>
      <ui-field label="Adres" htmlFor="building-address">
        <input ui-input id="building-address" [ngModel]="v.address" (ngModelChange)="patch({ address: $event })" />
      </ui-field>
      <div class="flex items-center justify-between gap-4 rounded-xl border border-line p-3.5">
        <div>
          <p class="text-sm font-medium text-ink-900">Çalışan erişimine açık</p>
          <p class="text-xs text-ink-500">
            Kapalıyken binanın katları çalışan ekranlarında ve raporlarda görünmez; veriler korunur.{{ building().isDemo ? ' Bu bina kurgusal demo verisi taşır.' : '' }}
          </p>
        </div>
        <ui-switch [checked]="v.active" (checkedChange)="patch({ active: $event })" label="Bina aktif" />
      </div>
      <div class="flex items-center justify-between">
        <p class="text-xs text-ink-400">{{ building().floorCount }} kat bu binaya bağlı.</p>
        <button ui-button type="button" (click)="save()" [loading]="pending()" [disabled]="!dirty()">
          <svg uiIcon name="Save" class="h-4 w-4"></svg>
          Bina bilgilerini kaydet
        </button>
      </div>
    </div>
  `,
})
export class BuildingSettingsForm {
  readonly building = input.required<BuildingFormValues>();
  readonly saved = output<void>();
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  readonly Number = Number;
  readonly values = signal<BuildingFormValues>({ id: '', code: '', name: '', address: '', order: 0, active: true, isDemo: false, floorCount: 0 });
  readonly pending = signal(false);
  readonly dirty = computed(() => JSON.stringify(this.values()) !== JSON.stringify(this.building()));

  constructor() {
    effect(() => {
      const building = this.building();
      untracked(() => this.values.set(building));
    });
  }

  patch(partial: Partial<BuildingFormValues>): void {
    this.values.update((current) => ({ ...current, ...partial }));
  }

  async save(): Promise<void> {
    const v = this.values();
    this.pending.set(true);
    try {
      await this.api.put(`admin/buildings/${this.building().id}`, { code: v.code, name: v.name, address: v.address, order: v.order, active: v.active });
      this.toasts.toast({ tone: 'success', title: 'Bina bilgileri güncellendi' });
      this.saved.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Bina güncellenemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}

/** Yeni bina ekleme. */
@Component({
  selector: 'app-create-building-button',
  imports: [FormsModule, UiButton, UiIcon, UiModal, UiField, UiInput, UiSwitch],
  template: `
    <button ui-button type="button" variant="secondary" (click)="open.set(true)">
      <svg uiIcon name="Plus" class="h-4 w-4"></svg>
      Bina ekle
    </button>
    <ui-modal [open]="open()" (closed)="open.set(false)" title="Yeni bina ekle" description="Ana bina gibi ikinci bir lokasyon eklendiğinde katlar bu bina altında gruplanır.">
      <div class="space-y-4">
        <div class="grid gap-4 sm:grid-cols-[1fr_140px]">
          <ui-field label="Bina adı" required htmlFor="new-building-name">
            <input ui-input id="new-building-name" [(ngModel)]="form.name" placeholder="Örneğin: Ana Bina" />
          </ui-field>
          <ui-field label="Kod" required htmlFor="new-building-code" hint="Büyük harf, örn. AB">
            <input ui-input id="new-building-code" [ngModel]="form.code" (ngModelChange)="form.code = $event.toUpperCase()" />
          </ui-field>
        </div>
        <ui-field label="Adres" htmlFor="new-building-address">
          <input ui-input id="new-building-address" [(ngModel)]="form.address" />
        </ui-field>
        <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
          <span class="text-sm text-ink-700">Çalışan erişimine açık</span>
          <ui-switch [checked]="form.active" (checkedChange)="form.active = $event" label="Bina aktif" />
        </label>
      </div>
      <ng-container footer>
        <button ui-button type="button" variant="secondary" (click)="open.set(false)" [disabled]="pending()">Vazgeç</button>
        <button ui-button type="button" (click)="submit()" [loading]="pending()">
          <svg uiIcon name="Building2" class="h-4 w-4"></svg>
          Binayı oluştur
        </button>
      </ng-container>
    </ui-modal>
  `,
})
export class CreateBuildingButton {
  readonly created = output<void>();
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  readonly open = signal(false);
  readonly pending = signal(false);
  form = { code: '', name: '', address: '', order: 20, active: true };

  async submit(): Promise<void> {
    this.pending.set(true);
    try {
      await this.api.post('admin/buildings', this.form);
      this.toasts.toast({ tone: 'success', title: 'Bina eklendi', description: 'Şimdi binaya kat ekleyebilirsiniz.' });
      this.open.set(false);
      this.form = { code: '', name: '', address: '', order: 20, active: true };
      this.created.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Bina eklenemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}

/** Kat bilgilerini düzenleme formu. */
@Component({
  selector: 'app-floor-settings-form',
  imports: [FormsModule, UiField, UiInput, UiSelect, UiTextarea, UiSwitch, UiButton, UiIcon],
  template: `
    @let v = values();
    <div class="space-y-4">
      <div class="grid gap-4 sm:grid-cols-[1fr_1fr_140px]">
        <ui-field label="Bina" required htmlFor="floor-building">
          <select ui-select id="floor-building" [ngModel]="v.buildingId" (ngModelChange)="patch({ buildingId: $event })">
            @for (building of buildings(); track building.id) {
              <option [value]="building.id">{{ building.name }}{{ !building.active ? ' (pasif)' : '' }}</option>
            }
          </select>
        </ui-field>
        <ui-field label="Kat adı" required htmlFor="floor-name">
          <input ui-input id="floor-name" [ngModel]="v.name" (ngModelChange)="patch({ name: $event })" />
        </ui-field>
        <ui-field label="Kat numarası" required htmlFor="floor-number">
          <input ui-input id="floor-number" type="number" [ngModel]="v.number" (ngModelChange)="patch({ number: Number($event) })" />
        </ui-field>
      </div>

      <ui-field label="Açıklama" htmlFor="floor-description">
        <textarea ui-textarea id="floor-description" rows="2" [ngModel]="v.description" (ngModelChange)="patch({ description: $event })"></textarea>
      </ui-field>

      <div class="grid gap-4 sm:grid-cols-2">
        <ui-field label="Plan genişliği" htmlFor="floor-width" hint="Plan koordinat sistemi birimi">
          <input ui-input id="floor-width" type="number" [ngModel]="v.planWidth" (ngModelChange)="patch({ planWidth: Number($event) })" />
        </ui-field>
        <ui-field label="Plan yüksekliği" htmlFor="floor-height" hint="Plan koordinat sistemi birimi">
          <input ui-input id="floor-height" type="number" [ngModel]="v.planHeight" (ngModelChange)="patch({ planHeight: Number($event) })" />
        </ui-field>
      </div>

      <div class="flex flex-col gap-3 sm:flex-row">
        <div class="flex flex-1 items-center justify-between gap-4 rounded-xl border border-line p-3.5">
          <div>
            <p class="text-sm font-medium text-ink-900">Çalışan rezervasyonuna açık</p>
            <p class="text-xs text-ink-500">Kapatıldığında çalışanlar bu katta rezervasyon yapamaz.</p>
          </div>
          <ui-switch [checked]="v.reservable" (checkedChange)="patch({ reservable: $event })" label="Rezervasyona açık" />
        </div>
        <div class="flex flex-1 items-center justify-between gap-4 rounded-xl border border-line p-3.5">
          <div>
            <p class="text-sm font-medium text-ink-900">Kat aktif</p>
            <p class="text-xs text-ink-500">Pasif katlar arayüzde listelenmez.</p>
          </div>
          <ui-switch [checked]="v.active" (checkedChange)="patch({ active: $event })" label="Kat aktif" />
        </div>
      </div>

      <div class="flex justify-end">
        <button ui-button type="button" (click)="save()" [loading]="pending()" [disabled]="!dirty()">
          <svg uiIcon name="Save" class="h-4 w-4"></svg>
          Kat bilgilerini kaydet
        </button>
      </div>
    </div>
  `,
})
export class FloorSettingsForm {
  readonly floor = input.required<FloorFormValues>();
  readonly buildings = input.required<BuildingOption[]>();
  readonly saved = output<void>();
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  readonly Number = Number;
  readonly values = signal<FloorFormValues>({ id: '', buildingId: '', number: 0, name: '', description: '', reservable: true, active: true, planWidth: 0, planHeight: 0 });
  readonly pending = signal(false);
  readonly dirty = computed(() => JSON.stringify(this.values()) !== JSON.stringify(this.floor()));

  constructor() {
    effect(() => {
      const floor = this.floor();
      untracked(() => this.values.set(floor));
    });
  }

  patch(partial: Partial<FloorFormValues>): void {
    this.values.update((current) => ({ ...current, ...partial }));
  }

  async save(): Promise<void> {
    const v = this.values();
    this.pending.set(true);
    try {
      await this.api.put(`admin/floors/${this.floor().id}`, {
        buildingId: v.buildingId,
        number: v.number,
        name: v.name,
        description: v.description,
        reservable: v.reservable,
        active: v.active,
        planWidth: v.planWidth,
        planHeight: v.planHeight,
      });
      this.toasts.toast({ tone: 'success', title: 'Kat bilgileri güncellendi' });
      this.saved.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Kat güncellenemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}

export interface ZoneRow extends ZoneDto {
  deskCount: number;
}

/** Bölge listesi ve düzenleme. */
@Component({
  selector: 'app-zone-table',
  imports: [FormsModule, UiDataTable, UiTd, UiBadge, UiButton, UiIcon, UiModal, UiField, UiInput, UiSelect, UiSwitch],
  template: `
    <ui-data-table caption="Kat bölgeleri" [headers]="[{ label: 'Bölge' }, { label: 'Tür' }, { label: 'Konum (x, y)' }, { label: 'Boyut' }, { label: 'Özellik' }, { label: 'Masa' }, { label: '', className: 'text-right' }]">
      @for (zone of zones(); track zone.id) {
        <tr class="hover:bg-slate-50/60">
          <td ui-td>
            <span class="font-medium text-ink-900">{{ zone.name }}</span>
            <span class="block text-xs text-ink-500">{{ zone.code }}</span>
          </td>
          <td ui-td>{{ kindLabel(zone.kind) }}</td>
          <td ui-td class="tabular-nums">{{ zone.x }}, {{ zone.y }}</td>
          <td ui-td class="tabular-nums">{{ zone.width }} × {{ zone.height }}</td>
          <td ui-td>
            <div class="flex flex-wrap gap-1">
              @if (zone.windowSide) {
                <span ui-badge tone="accent">Pencere</span>
              }
              @if (zone.quiet) {
                <span ui-badge tone="brand">Sessiz</span>
              }
              @if (!zone.reservable) {
                <span ui-badge tone="neutral">Rezervasyona kapalı</span>
              }
            </div>
          </td>
          <td ui-td class="tabular-nums">{{ zone.deskCount }}</td>
          <td ui-td class="text-right">
            <button ui-button type="button" size="sm" variant="secondary" (click)="edit(zone)">
              <svg uiIcon name="Pencil" class="h-3.5 w-3.5"></svg>
              Düzenle
            </button>
          </td>
        </tr>
      }
    </ui-data-table>

    @let e = editing();
    <ui-modal [open]="e !== null" (closed)="editing.set(null)" title="Bölge düzenle" [description]="e ? e.code + ' · ' + e.name : undefined">
      @if (e) {
        <div class="space-y-4">
          <ui-field label="Bölge adı" required htmlFor="zone-name">
            <input ui-input id="zone-name" [ngModel]="e.name" (ngModelChange)="patch({ name: $event })" />
          </ui-field>
          <ui-field label="Bölge türü" htmlFor="zone-kind">
            <select ui-select id="zone-kind" [ngModel]="e.kind" (ngModelChange)="patch({ kind: $event })">
              @for (kind of kinds; track kind) {
                <option [value]="kind">{{ kindLabels[kind] }}</option>
              }
            </select>
          </ui-field>
          <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <ui-field label="X" htmlFor="zone-x"><input ui-input id="zone-x" type="number" [ngModel]="e.x" (ngModelChange)="patch({ x: Number($event) })" /></ui-field>
            <ui-field label="Y" htmlFor="zone-y"><input ui-input id="zone-y" type="number" [ngModel]="e.y" (ngModelChange)="patch({ y: Number($event) })" /></ui-field>
            <ui-field label="Genişlik" htmlFor="zone-width"><input ui-input id="zone-width" type="number" [ngModel]="e.width" (ngModelChange)="patch({ width: Number($event) })" /></ui-field>
            <ui-field label="Yükseklik" htmlFor="zone-height"><input ui-input id="zone-height" type="number" [ngModel]="e.height" (ngModelChange)="patch({ height: Number($event) })" /></ui-field>
          </div>

          <div class="space-y-2">
            <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
              <span class="text-sm text-ink-700">Pencere kenarı</span>
              <ui-switch [checked]="e.windowSide" (checkedChange)="patch({ windowSide: $event })" label="Pencere kenarı" />
            </label>
            <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
              <span class="text-sm text-ink-700">Sessiz alan</span>
              <ui-switch [checked]="e.quiet" (checkedChange)="patch({ quiet: $event })" label="Sessiz alan" />
            </label>
            <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
              <span class="text-sm text-ink-700">Rezervasyona açık</span>
              <ui-switch [checked]="e.reservable" (checkedChange)="patch({ reservable: $event })" label="Rezervasyona açık" />
            </label>
          </div>
        </div>
      }
      <ng-container footer>
        <button ui-button type="button" variant="secondary" (click)="editing.set(null)" [disabled]="pending()">Vazgeç</button>
        <button ui-button type="button" (click)="save()" [loading]="pending()">
          <svg uiIcon name="Save" class="h-4 w-4"></svg>
          Kaydet
        </button>
      </ng-container>
    </ui-modal>
  `,
})
export class ZoneTable {
  readonly zones = input.required<ZoneRow[]>();
  readonly saved = output<void>();
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  readonly Number = Number;
  readonly kinds = ZONE_KINDS;
  readonly kindLabels = ZONE_KIND_LABELS;
  readonly editing = signal<ZoneRow | null>(null);
  readonly pending = signal(false);

  kindLabel(kind: string): string {
    return ZONE_KIND_LABELS[kind as ZoneKind] ?? kind;
  }

  edit(zone: ZoneRow): void {
    this.editing.set({ ...zone });
  }

  patch(partial: Partial<ZoneRow>): void {
    this.editing.update((current) => (current ? { ...current, ...partial } : current));
  }

  async save(): Promise<void> {
    const e = this.editing();
    if (!e) return;
    this.pending.set(true);
    try {
      await this.api.put(`admin/zones/${e.id}`, { name: e.name, kind: e.kind, x: e.x, y: e.y, width: e.width, height: e.height, windowSide: e.windowSide, quiet: e.quiet, reservable: e.reservable });
      this.toasts.toast({ tone: 'success', title: 'Bölge güncellendi' });
      this.editing.set(null);
      this.saved.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Bölge güncellenemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}

/** Yeni kat ekleme. */
@Component({
  selector: 'app-create-floor-button',
  imports: [FormsModule, UiButton, UiIcon, UiModal, UiField, UiInput, UiSelect, UiTextarea, UiSwitch],
  template: `
    <button ui-button type="button" (click)="openModal()" [disabled]="buildings().length === 0">
      <svg uiIcon name="Plus" class="h-4 w-4"></svg>
      Kat ekle
    </button>

    <ui-modal [open]="open()" (closed)="open.set(false)" title="Yeni kat ekle" description="Kat oluşturulduğunda varsayılan bir çalışma bölgesi ve koridor tanımlanır.">
      <div class="space-y-4">
        <ui-field label="Bina" required htmlFor="new-floor-building">
          <select ui-select id="new-floor-building" [(ngModel)]="form.buildingId">
            @for (building of buildings(); track building.id) {
              <option [value]="building.id">{{ building.name }}{{ !building.active ? ' (pasif)' : '' }}</option>
            }
          </select>
        </ui-field>
        <div class="grid gap-4 sm:grid-cols-2">
          <ui-field label="Kat numarası" required htmlFor="new-floor-number">
            <input ui-input id="new-floor-number" type="number" [ngModel]="form.number" (ngModelChange)="changeNumber($event)" />
          </ui-field>
          <ui-field label="Kat adı" required htmlFor="new-floor-name">
            <input ui-input id="new-floor-name" [(ngModel)]="form.name" />
          </ui-field>
        </div>
        <ui-field label="Açıklama" htmlFor="new-floor-description">
          <textarea ui-textarea id="new-floor-description" rows="2" [(ngModel)]="form.description" placeholder="Örneğin: Ortak çalışma alanları ve toplantı odaları."></textarea>
        </ui-field>
        <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
          <span class="text-sm text-ink-700">Çalışan rezervasyonuna açık</span>
          <ui-switch [checked]="form.reservable" (checkedChange)="form.reservable = $event" label="Rezervasyona açık" />
        </label>
      </div>
      <ng-container footer>
        <button ui-button type="button" variant="secondary" (click)="open.set(false)" [disabled]="pending()">Vazgeç</button>
        <button ui-button type="button" (click)="submit()" [loading]="pending()">
          <svg uiIcon name="Building2" class="h-4 w-4"></svg>
          Katı oluştur
        </button>
      </ng-container>
    </ui-modal>
  `,
})
export class CreateFloorButton {
  readonly nextNumber = input.required<number>();
  readonly buildings = input.required<BuildingOption[]>();
  readonly defaultBuildingId = input.required<string>();
  readonly created = output<void>();
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  readonly open = signal(false);
  readonly pending = signal(false);
  form = { buildingId: '', number: 1, name: '1. Kat', description: '', reservable: true, active: true };

  openModal(): void {
    const next = this.nextNumber();
    this.form = { ...this.form, buildingId: this.defaultBuildingId(), number: next, name: `${next}. Kat` };
    this.open.set(true);
  }

  changeNumber(value: string | number): void {
    this.form.number = Number(value);
    this.form.name = `${value}. Kat`;
  }

  async submit(): Promise<void> {
    this.pending.set(true);
    try {
      await this.api.post('admin/floors', this.form);
      this.toasts.toast({ tone: 'success', title: 'Kat eklendi', description: 'Varsayılan bir çalışma bölgesi ve koridor otomatik oluşturuldu.' });
      this.open.set(false);
      this.created.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Kat eklenemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}
