import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PLAN_ELEMENT_DEFINITIONS, PLAN_FILL_COLORS, PLAN_STROKE_COLORS, clampRect, type PlanElement } from '../../../lib/plan';
import { cn } from '../../../lib/utils';
import { UiBadge, UiButton, UiEmptyState, UiField, UiIcon, UiInput, UiSelect } from '../../../shared/ui';
import type { EditorSnapshot, SelectionItem } from './editor-state';

/** Sağ özellik paneli (kaynak: plan-properties.tsx): seçili öğeye göre düzenlenebilir alanlar. */

export interface ElementPatch {
  id: string;
  patch: Partial<PlanElement>;
}
export interface DeskPatch {
  id: string;
  patch: { x?: number; y?: number; width?: number; height?: number; rotation?: number };
}
export interface RoomPatch {
  id: string;
  patch: { x?: number; y?: number; width?: number; height?: number };
}

@Component({
  selector: 'app-plan-properties',
  imports: [RouterLink, UiBadge, UiButton, UiEmptyState, UiField, UiIcon, UiInput, UiSelect],
  template: `
    @let sel = selection();
    @if (sel.length === 0) {
      <ui-empty-state title="Öğe seçilmedi" description="Çalışma alanından bir öğe seçtiğinizde özellikleri burada görünür." />
    } @else if (sel.length > 1) {
      <div class="space-y-4">
        <div class="rounded-lg bg-brand-50 px-3 py-2.5">
          <p class="text-sm font-semibold text-brand-800">{{ sel.length }} öğe seçili</p>
          <p class="mt-0.5 text-xs text-brand-900/70">Seçili nesneleri birlikte taşıyabilirsiniz. Kopyala ve sil yalnızca kroki öğelerine uygulanır; masa ve odalar korunur.</p>
        </div>
        <div class="flex gap-2">
          <button ui-button type="button" variant="secondary" size="sm" block (click)="duplicate.emit()">
            <svg uiIcon name="Copy" class="h-3.5 w-3.5"></svg>
            Kopyala
          </button>
          <button ui-button type="button" variant="danger" size="sm" block (click)="delete.emit()">
            <svg uiIcon name="Trash2" class="h-3.5 w-3.5"></svg>
            Sil
          </button>
        </div>
      </div>
    } @else if (element(); as element) {
      @let definition = definitions[element.type];
      <div class="space-y-4">
        <div class="flex items-center justify-between gap-2">
          <div>
            <p class="text-xs font-medium uppercase tracking-wide text-ink-400">Öğe türü</p>
            <p class="text-sm font-semibold text-ink-900">{{ definition?.label ?? element.type }}</p>
          </div>
          <div class="flex gap-1">
            <button ui-button type="button" variant="ghost" size="icon" [attr.aria-label]="element.visible ? 'Öğeyi gizle' : 'Öğeyi göster'" (click)="updateElement.emit({ id: element.id, patch: { visible: !element.visible } })">
              @if (element.visible) {
                <svg uiIcon name="Eye" class="h-4 w-4"></svg>
              } @else {
                <svg uiIcon name="EyeOff" class="h-4 w-4 text-ink-400"></svg>
              }
            </button>
            <button ui-button type="button" variant="ghost" size="icon" [attr.aria-label]="element.locked ? 'Kilidi aç' : 'Kilitle'" (click)="updateElement.emit({ id: element.id, patch: { locked: !element.locked } })">
              @if (element.locked) {
                <svg uiIcon name="Lock" class="h-4 w-4 text-amber-600"></svg>
              } @else {
                <svg uiIcon name="LockOpen" class="h-4 w-4"></svg>
              }
            </button>
          </div>
        </div>

        <ui-field label="Etiket" htmlFor="element-label">
          <input ui-input id="element-label" [value]="element.label ?? ''" maxlength="80" [placeholder]="definition?.defaultLabel ?? 'Etiket'" (input)="updateElement.emit({ id: element.id, patch: { label: inputValue($event) } })" />
        </ui-field>

        <div class="grid grid-cols-2 gap-3">
          <ui-field label="X" htmlFor="element-x"><input ui-input id="element-x" type="number" [value]="element.x" (input)="updateRect(element, { x: numberValue($event) })" /></ui-field>
          <ui-field label="Y" htmlFor="element-y"><input ui-input id="element-y" type="number" [value]="element.y" (input)="updateRect(element, { y: numberValue($event) })" /></ui-field>
          <ui-field label="Genişlik" htmlFor="element-w"><input ui-input id="element-w" type="number" [value]="element.width" (input)="updateRect(element, { width: numberValue($event) })" /></ui-field>
          <ui-field label="Yükseklik" htmlFor="element-h"><input ui-input id="element-h" type="number" [value]="element.height" (input)="updateRect(element, { height: numberValue($event) })" /></ui-field>
        </div>

        @if (definition?.rotatable) {
          <ui-field label="Dönüş açısı (derece)" htmlFor="element-rotation">
            <input ui-input id="element-rotation" type="number" min="0" max="359" [value]="element.rotation" (input)="updateElement.emit({ id: element.id, patch: { rotation: ((numberValue($event) % 360) + 360) % 360 } })" />
          </ui-field>
        }

        <div>
          <p class="mb-1.5 text-sm font-medium text-ink-700">Dolgu rengi</p>
          <div class="flex flex-wrap gap-1.5">
            @for (color of fillColors; track color.value) {
              <button
                type="button"
                [title]="color.label"
                [attr.aria-label]="'Dolgu rengi: ' + color.label"
                [attr.aria-pressed]="element.style.fill === color.value"
                (click)="updateElement.emit({ id: element.id, patch: { style: { fill: color.value, stroke: element.style.stroke, strokeWidth: element.style.strokeWidth, fontSize: element.style.fontSize } } })"
                [class]="cn('h-7 w-7 rounded-md border-2 transition-transform', element.style.fill === color.value ? 'border-brand-600 scale-110' : 'border-line hover:scale-105')"
                [style.background-color]="color.value === 'transparent' ? '#ffffff' : color.value"
                [style.background-image]="color.value === 'transparent' ? 'repeating-linear-gradient(45deg, #e2e8f0 0 4px, #ffffff 4px 8px)' : null"
              ></button>
            }
          </div>
        </div>

        <div>
          <p class="mb-1.5 text-sm font-medium text-ink-700">Çizgi rengi</p>
          <div class="flex flex-wrap gap-1.5">
            @for (color of strokeColors; track color.value) {
              <button
                type="button"
                [title]="color.label"
                [attr.aria-label]="'Çizgi rengi: ' + color.label"
                [attr.aria-pressed]="element.style.stroke === color.value"
                (click)="updateElement.emit({ id: element.id, patch: { style: { fill: element.style.fill, stroke: color.value, strokeWidth: element.style.strokeWidth, fontSize: element.style.fontSize } } })"
                [class]="cn('h-7 w-7 rounded-md border-2 transition-transform', element.style.stroke === color.value ? 'border-brand-600 scale-110' : 'border-line hover:scale-105')"
                [style.background-color]="color.value === 'transparent' ? '#ffffff' : color.value"
                [style.background-image]="color.value === 'transparent' ? 'repeating-linear-gradient(45deg, #e2e8f0 0 4px, #ffffff 4px 8px)' : null"
              ></button>
            }
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <ui-field label="Çizgi kalınlığı" htmlFor="element-stroke-width">
            <select ui-select id="element-stroke-width" [value]="String(element.style.strokeWidth ?? 1.5)" (change)="updateElement.emit({ id: element.id, patch: { style: { fill: element.style.fill, stroke: element.style.stroke, strokeWidth: numberValue($event), fontSize: element.style.fontSize } } })">
              @for (width of strokeWidths; track width) {
                <option [value]="width">{{ width === 0 ? 'Yok' : width + ' px' }}</option>
              }
            </select>
          </ui-field>
          @if (element.type === 'TEXT') {
            <ui-field label="Yazı boyutu" htmlFor="element-font-size">
              <select ui-select id="element-font-size" [value]="String(element.style.fontSize ?? 14)" (change)="updateElement.emit({ id: element.id, patch: { style: { fill: element.style.fill, stroke: element.style.stroke, strokeWidth: element.style.strokeWidth, fontSize: numberValue($event) } } })">
                @for (size of fontSizes; track size) {
                  <option [value]="size">{{ size }} px</option>
                }
              </select>
            </ui-field>
          } @else {
            <ui-field label="Katman sırası" htmlFor="element-layer">
              <input ui-input id="element-layer" type="number" [value]="element.layerOrder" readonly />
            </ui-field>
          }
        </div>

        <div class="grid grid-cols-2 gap-2">
          <button ui-button type="button" variant="secondary" size="sm" (click)="layerChange.emit('front')">
            <svg uiIcon name="ArrowUpToLine" class="h-3.5 w-3.5"></svg>
            Öne getir
          </button>
          <button ui-button type="button" variant="secondary" size="sm" (click)="layerChange.emit('back')">
            <svg uiIcon name="ArrowDownToLine" class="h-3.5 w-3.5"></svg>
            Arkaya gönder
          </button>
        </div>

        <div class="flex gap-2 border-t border-line pt-3">
          <button ui-button type="button" variant="secondary" size="sm" block (click)="duplicate.emit()">
            <svg uiIcon name="Copy" class="h-3.5 w-3.5"></svg>
            Kopyala
          </button>
          <button ui-button type="button" variant="danger" size="sm" block (click)="delete.emit()">
            <svg uiIcon name="Trash2" class="h-3.5 w-3.5"></svg>
            Sil
          </button>
        </div>
      </div>
    } @else if (desk(); as desk) {
      <div class="space-y-4">
        <div>
          <p class="text-xs font-medium uppercase tracking-wide text-ink-400">Öğe türü</p>
          <p class="text-sm font-semibold text-ink-900">Masa</p>
        </div>
        <div>
          <p class="text-xs font-medium uppercase tracking-wide text-ink-400">Masa kodu</p>
          <p class="text-lg font-semibold tracking-tight text-ink-900">{{ desk.code }}</p>
          <p class="text-xs text-ink-500">{{ desk.floorName }} · {{ desk.zoneName }}</p>
        </div>
        <div class="flex flex-wrap gap-1.5">
          <span ui-badge [tone]="desk.active ? 'success' : 'danger'">{{ desk.active ? 'Kullanımda' : 'Kapalı' }}</span>
          @if (desk.popular) {
            <span ui-badge tone="warning">★ Popüler</span>
          }
          @if (desk.accessibleOnly) {
            <span ui-badge tone="violet">Erişilebilirlik</span>
          }
          <span ui-badge tone="neutral">{{ desk.reservationCount }} rezervasyon</span>
        </div>
        <div class="grid grid-cols-2 gap-3">
          <ui-field label="X" htmlFor="desk-x"><input ui-input id="desk-x" type="number" [value]="desk.x" (input)="updateDesk.emit({ id: desk.id, patch: { x: numberValue($event) } })" /></ui-field>
          <ui-field label="Y" htmlFor="desk-y"><input ui-input id="desk-y" type="number" [value]="desk.y" (input)="updateDesk.emit({ id: desk.id, patch: { y: numberValue($event) } })" /></ui-field>
          <ui-field label="Genişlik" htmlFor="desk-w"><input ui-input id="desk-w" type="number" min="20" [value]="desk.width" (input)="updateDesk.emit({ id: desk.id, patch: { width: numberValue($event) } })" /></ui-field>
          <ui-field label="Yükseklik" htmlFor="desk-h"><input ui-input id="desk-h" type="number" min="16" [value]="desk.height" (input)="updateDesk.emit({ id: desk.id, patch: { height: numberValue($event) } })" /></ui-field>
        </div>
        <ui-field label="Dönüş açısı (derece)" htmlFor="desk-rotation">
          <input ui-input id="desk-rotation" type="number" min="0" max="359" [value]="desk.rotation" (input)="updateDesk.emit({ id: desk.id, patch: { rotation: ((numberValue($event) % 360) + 360) % 360 } })" />
        </ui-field>
        <p class="rounded-lg bg-slate-50 px-3 py-2 text-[12px] leading-relaxed text-ink-500">
          Masanın konumu, ölçüsü ve açısı değiştiğinde rezervasyonları ve özellikleri korunur. Kod, bölge ve donanım değişiklikleri için
          <a routerLink="/admin/masalar" class="font-medium text-brand-600 hover:text-brand-700">masa yönetimi</a> ekranını kullanın.
        </p>
      </div>
    } @else if (room(); as room) {
      <div class="space-y-4">
        <div>
          <p class="text-xs font-medium uppercase tracking-wide text-ink-400">Öğe türü</p>
          <p class="text-sm font-semibold text-ink-900">Toplantı odası</p>
          <p class="mt-2 text-lg font-semibold tracking-tight text-ink-900">{{ room.name }}</p>
          <p class="text-xs text-ink-500">{{ room.code }} · {{ room.capacity }} kişi</p>
        </div>
        <div class="flex flex-wrap gap-1.5">
          <span ui-badge [tone]="room.active ? 'success' : 'danger'">{{ room.active ? 'Kullanımda' : 'Kapalı' }}</span>
          <span ui-badge tone="neutral">{{ room.reservationCount }} rezervasyon</span>
        </div>
        <div class="grid grid-cols-2 gap-3">
          <ui-field label="X" htmlFor="room-x"><input ui-input id="room-x" type="number" [value]="room.x" (input)="updateRoomRect(room, { x: numberValue($event) })" /></ui-field>
          <ui-field label="Y" htmlFor="room-y"><input ui-input id="room-y" type="number" [value]="room.y" (input)="updateRoomRect(room, { y: numberValue($event) })" /></ui-field>
          <ui-field label="Genişlik" htmlFor="room-w"><input ui-input id="room-w" type="number" [value]="room.width" (input)="updateRoomRect(room, { width: numberValue($event) })" /></ui-field>
          <ui-field label="Yükseklik" htmlFor="room-h"><input ui-input id="room-h" type="number" [value]="room.height" (input)="updateRoomRect(room, { height: numberValue($event) })" /></ui-field>
        </div>
        <p class="rounded-lg bg-slate-50 px-3 py-2 text-[12px] leading-relaxed text-ink-500">
          Kapasite ve donanım bilgileri için <a routerLink="/admin/odalar" class="font-medium text-brand-600 hover:text-brand-700">toplantı odası yönetimi</a> ekranını kullanın.
        </p>
      </div>
    }
  `,
})
export class PlanProperties {
  readonly snapshot = input.required<EditorSnapshot>();
  readonly selection = input.required<SelectionItem[]>();
  readonly planWidth = input.required<number>();
  readonly planHeight = input.required<number>();
  readonly updateElement = output<ElementPatch>();
  readonly updateDesk = output<DeskPatch>();
  readonly updateRoom = output<RoomPatch>();
  readonly duplicate = output<void>();
  readonly delete = output<void>();
  readonly layerChange = output<'front' | 'back'>();

  readonly cn = cn;
  readonly String = String;
  readonly definitions = PLAN_ELEMENT_DEFINITIONS;
  readonly fillColors = PLAN_FILL_COLORS;
  readonly strokeColors = PLAN_STROKE_COLORS;
  readonly strokeWidths = [0, 1, 1.5, 2, 3, 4, 6];
  readonly fontSizes = [10, 12, 14, 16, 20, 24, 32];

  private readonly single = computed(() => (this.selection().length === 1 ? this.selection()[0] : null));
  readonly element = computed(() => {
    const item = this.single();
    return item?.kind === 'planElement' ? (this.snapshot().elements.find((entry) => entry.id === item.id) ?? null) : null;
  });
  readonly desk = computed(() => {
    const item = this.single();
    return item?.kind === 'desk' ? (this.snapshot().desks.find((entry) => entry.id === item.id) ?? null) : null;
  });
  readonly room = computed(() => {
    const item = this.single();
    return item?.kind === 'meetingRoom' ? (this.snapshot().rooms.find((entry) => entry.id === item.id) ?? null) : null;
  });

  inputValue(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  numberValue(event: Event): number {
    return Number((event.target as HTMLInputElement | HTMLSelectElement).value);
  }

  updateRect(element: PlanElement, patch: Partial<Pick<PlanElement, 'x' | 'y' | 'width' | 'height'>>): void {
    const next = clampRect({ ...element, ...patch }, this.planWidth(), this.planHeight());
    this.updateElement.emit({ id: element.id, patch: next });
  }

  updateRoomRect(room: { id: string; x: number; y: number; width: number; height: number }, patch: { x?: number; y?: number; width?: number; height?: number }): void {
    const next = clampRect({ ...room, ...patch }, this.planWidth(), this.planHeight());
    this.updateRoom.emit({ id: room.id, patch: { x: next.x, y: next.y, width: Math.max(40, next.width), height: Math.max(40, next.height) } });
  }
}
