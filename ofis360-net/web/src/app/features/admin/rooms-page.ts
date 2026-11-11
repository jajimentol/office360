import { RoomPolicyModal } from './room-policy-modal';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { cn } from '../../lib/utils';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import type { FeatureDto, FloorDto, RoomDto } from '../../core/api/types';
import {
  PageState,
  ToastService,
  UiAlert,
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
import { AdminFormError, floorLabelOf } from './admin-shared';

/** Kaynak: (app)/admin/odalar/page.tsx + components/admin/room-manager.tsx */

interface RoomForm {
  code: string;
  name: string;
  floorId: string;
  capacity: number;
  x: number;
  y: number;
  width: number;
  height: number;
  active: boolean;
  notes: string;
  featureKeys: string[];
}

const EMPTY_FORM: RoomForm = { code: '', name: '', floorId: '', capacity: 6, x: 700, y: 100, width: 200, height: 120, active: true, notes: '', featureKeys: [] };

interface PageData {
  rooms: RoomDto[];
  floors: FloorDto[];
  features: FeatureDto[];
  priorityGaps: Array<{ roomId: string; code: string; name: string }>;
}

@Component({
  selector: 'app-rooms-page',
  imports: [
    FormsModule,
    RoomPolicyModal,
    UiAlert,
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
    AdminFormError,
  ],
  template: `
    <ui-page-header title="Toplantı odası yönetimi" description="Oda kapasitesi, donanım bilgisi ve kullanım durumu yönetimi." />

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as d) {
      @if (d.priorityGaps.length > 0) {
        <ui-alert tone="warning" title="Oda önceliği çalışmıyor" class="mb-5 block">
          Şu odalara aktif öncelikli kişi atanmadı: {{ gapNames(d.priorityGaps) }}. Atanana kadar bu odaları herkes kendi rezervasyon penceresinde alabilir; "Kurallar" düğmesinden kişi ekleyin.
        </ui-alert>
      }
      <div ui-card>
        <ui-card-header title="Toplantı odaları" [description]="d.rooms.length + ' oda tanımlı'" hasIcon>
          <svg uiIcon icon name="DoorOpen" class="h-4.5 w-4.5"></svg>
        </ui-card-header>
        <ui-card-body>
          <div class="mb-4 flex justify-end">
            <button ui-button type="button" (click)="openCreate()">
              <svg uiIcon name="Plus" class="h-4 w-4"></svg>
              Toplantı odası ekle
            </button>
          </div>

          @if (d.rooms.length === 0) {
            <ui-empty-state title="Kayıtlı toplantı odası bulunamadı" />
          } @else {
            <ui-data-table caption="Toplantı odaları" [headers]="[{ label: 'Oda' }, { label: 'Kat' }, { label: 'Kapasite' }, { label: 'Donanım' }, { label: 'Rezervasyon' }, { label: 'Durum' }, { label: '', className: 'text-right' }]">
              @for (room of d.rooms; track room.id) {
                <tr [class]="cn('hover:bg-slate-50/60', !room.active && 'bg-red-50/30')">
                  <td ui-td>
                    <span class="font-semibold text-ink-900">{{ room.name }}</span>
                    <span class="block text-xs text-ink-500">{{ room.code }}</span>
                  </td>
                  <td ui-td>{{ room.floor?.name }}</td>
                  <td ui-td class="tabular-nums">{{ room.capacity }} kişi</td>
                  <td ui-td>
                    <div class="flex max-w-[260px] flex-wrap gap-1">
                      @for (feature of room.features; track feature.key) {
                        <span ui-badge tone="neutral">{{ feature.name }}</span>
                      }
                    </div>
                  </td>
                  <td ui-td class="tabular-nums">{{ room.reservationCount }}</td>
                  <td ui-td>
                    <span ui-badge [tone]="room.active ? 'success' : 'danger'">{{ room.active ? 'Kullanımda' : 'Kapalı' }}</span>
                  </td>
                  <td ui-td class="text-right">
                    <div class="flex justify-end gap-1.5">
                      <button ui-button type="button" size="sm" variant="ghost" (click)="toggleActive(room)">{{ room.active ? 'Kapat' : 'Aç' }}</button>
                      <button ui-button type="button" size="sm" variant="ghost" (click)="policyRoomId.set(room.id)">Kurallar</button>
                      <button ui-button type="button" size="sm" variant="secondary" (click)="openEdit(room)">
                        <svg uiIcon name="Pencil" class="h-3.5 w-3.5"></svg>
                        Düzenle
                      </button>
                      <button ui-button type="button" size="sm" variant="ghost" [attr.aria-label]="room.name + ' odasını sil'" (click)="deleteTarget.set(room)">
                        <svg uiIcon name="Trash2" class="h-3.5 w-3.5 text-red-600"></svg>
                      </button>
                    </div>
                  </td>
                </tr>
              }
            </ui-data-table>
          }
        </ui-card-body>
      </div>

      <app-room-policy-modal [roomId]="policyRoomId()" (closed)="policyRoomId.set(null)" />

      <ui-modal [open]="formOpen()" (closed)="closeForm()" [title]="editingId() ? 'Toplantı odasını düzenle' : 'Yeni toplantı odası'" description="Kapasite, donanım ve kat planındaki konum bilgileri." size="lg">
        <div class="space-y-4">
          @if (formError()) {
            <admin-form-error>{{ formError() }}</admin-form-error>
          }

          <div class="grid gap-4 sm:grid-cols-2">
            <ui-field label="Oda adı" required htmlFor="room-name">
              <input ui-input id="room-name" [(ngModel)]="form.name" placeholder="Örneğin: Boğaziçi" />
            </ui-field>
            <ui-field label="Oda kodu" required htmlFor="room-code" hint="Örn. T205">
              <input ui-input id="room-code" [(ngModel)]="form.code" />
            </ui-field>
          </div>

          <div class="grid gap-4 sm:grid-cols-2">
            <ui-field label="Kat" required htmlFor="room-floor">
              <select ui-select id="room-floor" [(ngModel)]="form.floorId">
                @for (floor of d.floors; track floor.id) {
                  <option [value]="floor.id">{{ floorLabel(floor) }}</option>
                }
              </select>
            </ui-field>
            <ui-field label="Kapasite" required htmlFor="room-capacity">
              <input ui-input id="room-capacity" type="number" min="1" [(ngModel)]="form.capacity" />
            </ui-field>
          </div>

          <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <ui-field label="X" htmlFor="room-x"><input ui-input id="room-x" type="number" [(ngModel)]="form.x" /></ui-field>
            <ui-field label="Y" htmlFor="room-y"><input ui-input id="room-y" type="number" [(ngModel)]="form.y" /></ui-field>
            <ui-field label="Genişlik" htmlFor="room-width"><input ui-input id="room-width" type="number" [(ngModel)]="form.width" /></ui-field>
            <ui-field label="Yükseklik" htmlFor="room-height"><input ui-input id="room-height" type="number" [(ngModel)]="form.height" /></ui-field>
          </div>

          <div>
            <p class="mb-2 text-sm font-medium text-ink-700">Donanım</p>
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

          <ui-field label="Not" htmlFor="room-notes">
            <textarea ui-textarea id="room-notes" rows="2" [(ngModel)]="form.notes"></textarea>
          </ui-field>

          <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
            <span>
              <span class="block text-sm text-ink-900">Kullanıma açık</span>
              <span class="block text-xs text-ink-500">Kapatıldığında çalışanlar bu odayı rezerve edemez.</span>
            </span>
            <ui-switch [checked]="form.active" (checkedChange)="form.active = $event" label="Kullanıma açık" />
          </label>
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
        title="Toplantı odasını sil"
        description="Rezervasyon geçmişi olan odalar silinemez; bunun yerine kullanıma kapatılabilir."
        confirmLabel="Odayı sil"
        hasContent
      >
        <p class="text-sm text-ink-700"><strong class="font-semibold text-ink-900">{{ deleteTarget()?.name }}</strong> odasını silmek istediğinize emin misiniz?</p>
      </ui-confirm-dialog>
    }
  `,
})
export class RoomsPage {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  readonly cn = cn;
  readonly floorLabel = floorLabelOf;
  readonly state = new PageState<PageData>();

  readonly formOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formError = signal<string | null>(null);
  readonly pending = signal(false);
  readonly deleteTarget = signal<RoomDto | null>(null);
  readonly policyRoomId = signal<string | null>(null);
  form: RoomForm = { ...EMPTY_FORM };

  constructor() {
    void this.load();
  }

  load(silent = false): Promise<unknown> {
    return this.state.load(
      async () => {
        const [rooms, floors, features, priorityGaps] = await Promise.all([
          this.api.get<RoomDto[]>('admin/rooms'),
          this.api.get<FloorDto[]>('admin/floors'),
          this.api.get<FeatureDto[]>('admin/room-features'),
          this.api.get<PageData['priorityGaps']>('admin/room-priority-gaps'),
        ]);
        return { rooms, floors, features, priorityGaps };
      },
      { silent },
    );
  }

  gapNames(gaps: PageData['priorityGaps']): string {
    return gaps.map((gap) => gap.code).join(', ');
  }

  openCreate(): void {
    this.form = { ...EMPTY_FORM, floorId: this.state.data()?.floors[0]?.id ?? '', featureKeys: [] };
    this.formError.set(null);
    this.editingId.set(null);
    this.formOpen.set(true);
  }

  openEdit(room: RoomDto): void {
    this.form = {
      code: room.code,
      name: room.name,
      floorId: room.floorId,
      capacity: room.capacity,
      x: room.x,
      y: room.y,
      width: room.width,
      height: room.height,
      active: room.active,
      notes: room.notes ?? '',
      featureKeys: room.features.map((feature) => feature.key),
    };
    this.formError.set(null);
    this.editingId.set(room.id);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
    this.editingId.set(null);
  }

  toggleFeature(key: string): void {
    this.form.featureKeys = this.form.featureKeys.includes(key) ? this.form.featureKeys.filter((k) => k !== key) : [...this.form.featureKeys, key];
  }

  async submit(): Promise<void> {
    this.pending.set(true);
    this.formError.set(null);
    const payload = {
      code: this.form.code.trim().toUpperCase(),
      name: this.form.name.trim(),
      floorId: this.form.floorId,
      capacity: Number(this.form.capacity),
      x: Number(this.form.x),
      y: Number(this.form.y),
      width: Number(this.form.width),
      height: Number(this.form.height),
      active: this.form.active,
      notes: this.form.notes.trim() || null,
      featureKeys: this.form.featureKeys,
    };
    try {
      const id = this.editingId();
      if (id) await this.api.put(`admin/rooms/${id}`, payload);
      else await this.api.post('admin/rooms', payload);
      this.toasts.toast({ tone: 'success', title: id ? 'Toplantı odası güncellendi' : 'Toplantı odası eklendi', description: `${payload.name} kaydedildi.` });
      this.closeForm();
      void this.load(true);
    } catch (error: unknown) {
      this.formError.set(errorMessage(error));
    } finally {
      this.pending.set(false);
    }
  }

  async toggleActive(room: RoomDto): Promise<void> {
    try {
      await this.api.put(`admin/rooms/${room.id}`, { active: !room.active });
      this.toasts.toast({ tone: 'success', title: room.active ? 'Oda kullanıma kapatıldı' : 'Oda yeniden kullanıma açıldı' });
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
      await this.api.delete(`admin/rooms/${target.id}`);
      this.toasts.toast({ tone: 'success', title: 'Toplantı odası silindi' });
      void this.load(true);
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Oda silinemedi', description: errorMessage(error) });
    } finally {
      this.deleteTarget.set(null);
      this.pending.set(false);
    }
  }
}
