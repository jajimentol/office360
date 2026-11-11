import { Component, HostListener, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { clamp, clampRect, type PlanElement } from '../../../lib/plan';
import { cn } from '../../../lib/utils';
import { ApiClient, errorMessage } from '../../../core/api/api-client';
import type { AutoPlaceResult, FloorPlanSnapshot, SavePlanResult, SnapshotDesk, SnapshotRoom } from '../../../core/api/types';
import { ToastService, UiAlert, UiBadge, UiButton, UiCard, UiCardBody, UiCardHeader, UiConfirmDialog, UiIcon, UiSelect, UiSwitch } from '../../../shared/ui';
import { PlanEditorState, type EditorDesk, type EditorRoom, type EditorSnapshot, type Point, type Rect, type SelectionItem } from './editor-state';
import { PlanCanvas, type PoolDrop } from './plan-canvas';
import { DeskDraftModal, RoomDraftModal, type DeskDraftValues, type RoomDraftValues } from './plan-draft-modals';
import { PlanPool, type PendingPoolItem, type PoolDesk, type PoolRoom } from './plan-pool';
import { PlanProperties, type DeskPatch, type ElementPatch, type RoomPatch } from './plan-properties';
import { PLAN_TEMPLATES, withFreshIds } from './plan-templates';
import { PlanToolbox, type EditorMode, type EditorTool } from './plan-toolbox';

/** Kat planı editörü (kaynak: components/admin/plan-editor/plan-editor.tsx). */

const ONBOARDING_KEY = 'ofis360.plan-editor.onboarding';
const ZOOM_STEPS = [0.5, 0.65, 0.8, 1, 1.25, 1.5, 2];

export interface PlanEditorFloor {
  id: string;
  name: string;
  planWidth: number;
  planHeight: number;
}

function clampElement(element: PlanElement, planWidth: number, planHeight: number): PlanElement {
  const rect = clampRect(element, planWidth, planHeight);
  return { ...element, ...rect };
}

function suggestDeskCode(desks: EditorDesk[], floor: PlanEditorFloor): string {
  const prefix = `F${floor.planWidth > 0 ? (floor.name.match(/\d+/)?.[0] ?? '1') : '1'}-`;
  let index = desks.length + 1;
  const existing = new Set(desks.map((desk) => desk.code));
  let code = `${prefix}M${String(index).padStart(2, '0')}`;
  while (existing.has(code)) {
    index += 1;
    code = `${prefix}M${String(index).padStart(2, '0')}`;
  }
  return code;
}

function suggestRoomCode(rooms: EditorRoom[], floor: PlanEditorFloor): string {
  const floorNumber = floor.name.match(/\d+/)?.[0] ?? '1';
  let index = rooms.length + 1;
  const existing = new Set(rooms.map((room) => room.code));
  let code = `T${floorNumber}${String(index).padStart(2, '0')}`;
  while (existing.has(code)) {
    index += 1;
    code = `T${floorNumber}${String(index).padStart(2, '0')}`;
  }
  return code;
}

@Component({
  selector: 'app-plan-editor',
  imports: [FormsModule, UiBadge, UiButton, UiAlert, UiCard, UiCardHeader, UiCardBody, UiIcon, UiSelect, UiSwitch, UiConfirmDialog, PlanToolbox, PlanCanvas, PlanPool, PlanProperties, DeskDraftModal, RoomDraftModal],
  template: `
    @let f = floor();
    @let snap = state.snapshot();
    <div class="space-y-4">
      <!-- Sekmeler -->
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div class="flex items-center gap-1 rounded-lg border border-line bg-slate-50 p-0.5">
          @for (item of modes; track item.key) {
            <button type="button" (click)="switchMode(item.key)" [attr.aria-pressed]="mode() === item.key" [class]="cn('rounded-md px-3 py-1.5 text-[13px] font-medium transition-colors', mode() === item.key ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')">
              {{ item.label }}
            </button>
          }
        </div>

        <div class="flex flex-wrap items-center gap-2">
          @if (state.isDirty()) {
            <span ui-badge tone="warning">Kaydedilmemiş değişiklik var</span>
          } @else {
            <span ui-badge tone="success">Kayıtlı</span>
          }
          <button ui-button type="button" size="sm" (click)="save()" [loading]="saving()" [disabled]="!state.isDirty()">
            <svg uiIcon name="Save" class="h-3.5 w-3.5"></svg>
            Krokiyi kaydet
          </button>
        </div>
      </div>

      @if (showOnboarding()) {
        <ui-alert tone="info" title="Kat planı editörü nasıl kullanılır?" hasIcon>
          <svg uiIcon icon name="Info" class="h-4 w-4"></svg>
          <ol class="ml-4 list-decimal space-y-1">
            <li>Soldaki araç kutusundan bir öğe seçin (örneğin Duvar veya Oda).</li>
            <li>Çalışma alanına sürükleyerek çizin. Duvarda Shift tuşu eksene sabitler.</li>
            <li>Seçim aracıyla öğeleri taşıyın, köşelerden boyutlandırın, sağ panelden düzenleyin.</li>
            <li>Kroki hazır olduğunda <strong>Rezervasyon Nesnelerini Yerleştir</strong> sekmesine geçip masa ve odaları konumlandırın.</li>
            <li>Son olarak <strong>Krokiyi kaydet</strong> düğmesine basın.</li>
          </ol>
          <div class="mt-2">
            <button ui-button type="button" size="sm" variant="secondary" (click)="dismissOnboarding()">
              <svg uiIcon name="X" class="h-3.5 w-3.5"></svg>
              Anladım, bir daha gösterme
            </button>
          </div>
        </ui-alert>
      }

      <!-- Araç çubuğu -->
      <div class="flex flex-wrap items-center gap-2 rounded-xl border border-line bg-white px-3 py-2">
        <button ui-button type="button" variant="ghost" size="sm" (click)="state.undo()" [disabled]="!state.canUndo()" title="Geri al (Ctrl+Z)">
          <svg uiIcon name="Undo2" class="h-4 w-4"></svg>
          Geri al
        </button>
        <button ui-button type="button" variant="ghost" size="sm" (click)="state.redo()" [disabled]="!state.canRedo()" title="Yinele (Ctrl+Y)">
          <svg uiIcon name="Redo2" class="h-4 w-4"></svg>
          Yinele
        </button>

        <span class="mx-1 h-5 w-px bg-line" aria-hidden="true"></span>

        <label class="flex items-center gap-2 text-[13px] text-ink-600">
          <ui-switch [(checked)]="showGrid" label="Izgarayı göster" />
          <svg uiIcon name="Grid3x3" class="h-4 w-4 text-ink-400"></svg>
          Izgara
        </label>
        <label class="flex items-center gap-2 text-[13px] text-ink-600">
          <ui-switch [(checked)]="snapEnabled" label="Izgaraya hizala" />
          <svg uiIcon name="Magnet" class="h-4 w-4 text-ink-400"></svg>
          Hizala
        </label>

        <span class="mx-1 h-5 w-px bg-line" aria-hidden="true"></span>

        <div class="flex items-center gap-1">
          <button ui-button type="button" variant="ghost" size="icon" aria-label="Uzaklaştır" (click)="zoomBy(-1)" [disabled]="zoomIndex() === 0">
            <svg uiIcon name="ZoomOut" class="h-4 w-4"></svg>
          </button>
          <span class="w-12 text-center text-[13px] tabular-nums text-ink-600">%{{ Math.round(zoom() * 100) }}</span>
          <button ui-button type="button" variant="ghost" size="icon" aria-label="Yakınlaştır" (click)="zoomBy(1)" [disabled]="zoomIndex() === zoomSteps.length - 1">
            <svg uiIcon name="ZoomIn" class="h-4 w-4"></svg>
          </button>
          <button ui-button type="button" variant="ghost" size="sm" (click)="zoomIndex.set(3)" title="Görünümü sıfırla">
            <svg uiIcon name="Maximize" class="h-4 w-4"></svg>
            Sıfırla
          </button>
        </div>

        <span class="mx-1 h-5 w-px bg-line" aria-hidden="true"></span>

        <label class="flex items-center gap-2">
          <span class="sr-only">Şablon seç</span>
          <svg uiIcon name="LayoutTemplate" class="h-4 w-4 text-ink-400"></svg>
          <select ui-select class="h-8 w-56 text-[13px]" [ngModel]="templateKey()" (ngModelChange)="chooseTemplate($event)">
            <option value="">Şablon uygula…</option>
            @for (template of templates; track template.key) {
              <option [value]="'template:' + template.key">{{ template.name }}</option>
            }
            @for (item of otherFloors(); track item.id) {
              <option [value]="'floor:' + item.id">{{ item.name }} krokisini kopyala</option>
            }
          </select>
        </label>
      </div>

      <!-- Editör düzeni -->
      <div class="grid gap-4 xl:grid-cols-[190px_minmax(0,1fr)_280px]">
        <div ui-card class="h-fit">
          <ui-card-body class="px-3 py-3">
            <app-plan-toolbox [tool]="tool()" [mode]="mode()" (selected)="tool.set($event)" />
          </ui-card-body>
        </div>

        <div class="min-w-0 space-y-2">
          <app-plan-canvas
            [planWidth]="f.planWidth"
            [planHeight]="f.planHeight"
            [snapshot]="snap"
            [mode]="mode()"
            [tool]="tool()"
            [selection]="state.selection()"
            [showGrid]="showGrid()"
            [snapEnabled]="snapEnabled()"
            [zoom]="zoom()"
            [pendingPoolItem]="pendingPoolItem()"
            (toolReset)="tool.set('SELECT')"
            (selectionChange)="state.selection.set($event)"
            (applyChange)="state.apply($event.updater, { commit: $event.commit })"
            (requestDesk)="deskDraft.set($event)"
            (requestRoom)="roomDraft.set($event)"
            (dropPoolItem)="handleDropPoolItem($event)"
          />
          @if (poolDesks().length + poolRooms().length > 0) {
            <app-plan-pool [desks]="poolDesks()" [rooms]="poolRooms()" [autoPlacing]="autoPlacing()" (autoPlace)="handleAutoPlace()" [pending]="pendingPoolItem()" (selectItem)="pendingPoolItem.set($event)" />
          }
          <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] text-ink-500">
            <span class="flex items-center gap-1.5">
              <span class="inline-block h-2.5 w-2.5 rounded-full bg-teal-accent" aria-hidden="true"></span>
              Rezervasyon yapılabilen nesne (seçilebilir ve taşınabilir)
            </span>
            <span>Delete: sil · Ctrl+Z / Ctrl+Y: geri al / yinele · Ctrl+D: kopyala</span>
            <span>Yön tuşları: 2 birim · Shift + yön tuşu: 10 birim</span>
            <span>Plan alanı: {{ f.planWidth }} × {{ f.planHeight }} · {{ snap.elements.length }} kroki öğesi</span>
          </div>
        </div>

        <div ui-card class="h-fit">
          <ui-card-header title="Özellikler" />
          <ui-card-body>
            <app-plan-properties
              [snapshot]="snap"
              [selection]="state.selection()"
              [planWidth]="f.planWidth"
              [planHeight]="f.planHeight"
              (updateElement)="updateElement($event)"
              (updateDesk)="updateDesk($event)"
              (updateRoom)="updateRoom($event)"
              (duplicate)="duplicateSelection()"
              (delete)="deleteSelection()"
              (layerChange)="changeLayer($event)"
            />
          </ui-card-body>
        </div>
      </div>

      <!-- Şablon onayı -->
      <ui-confirm-dialog [open]="pendingTemplate() !== null" (closed)="cancelTemplate()" (confirmed)="confirmTemplate()" tone="primary" title="Mevcut kroki değiştirilecek" confirmLabel="Şablonu uygula" hasContent>
        <p class="text-sm text-ink-700">Bu işlem bu kattaki mevcut kroki öğelerinin yerine şablonu koyar. Masa ve toplantı odaları etkilenmez. Değişikliği kaydetmeden önce geri alabilirsiniz.</p>
      </ui-confirm-dialog>

      <!-- Masa oluştur / bağla -->
      <app-desk-draft-modal [open]="deskDraft() !== null" (closed)="deskDraft.set(null)" [zones]="zones()" [desks]="snap.desks" [suggestedCode]="suggestedDeskCode()" [pending]="draftPending()" (create)="createDesk($event)" (link)="moveExistingDesk($event)" />

      <!-- Oda oluştur / bağla -->
      <app-room-draft-modal [open]="roomDraft() !== null" (closed)="roomDraft.set(null)" [rooms]="snap.rooms" [suggestedCode]="suggestedRoomCode()" [pending]="draftPending()" (create)="createRoom($event)" (link)="moveExistingRoom($event)" />
    </div>
  `,
})
export class PlanEditor {
  readonly floor = input.required<PlanEditorFloor>();
  readonly initialSnapshot = input.required<FloorPlanSnapshot>();
  readonly zones = input.required<Array<{ id: string; name: string; reservable: boolean }>>();
  readonly otherFloors = input.required<Array<{ id: string; name: string }>>();
  /** Kaydetme / otomatik yerleştirme sonrası üst sayfanın veriyi yenilemesi için. */
  readonly saved = output<void>();

  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly cn = cn;
  readonly Math = Math;
  readonly templates = PLAN_TEMPLATES;
  readonly zoomSteps = ZOOM_STEPS;
  readonly modes: Array<{ key: EditorMode; label: string }> = [
    { key: 'sketch', label: 'Krokiyi Düzenle' },
    { key: 'resources', label: 'Rezervasyon Nesnelerini Yerleştir' },
  ];

  readonly state = new PlanEditorState({ elements: [], desks: [], rooms: [] });
  private saved_: EditorSnapshot = { elements: [], desks: [], rooms: [] };

  readonly mode = signal<EditorMode>('sketch');
  readonly tool = signal<EditorTool>('SELECT');
  readonly showGrid = signal(true);
  readonly snapEnabled = signal(true);
  readonly zoomIndex = signal(3);
  readonly zoom = computed(() => ZOOM_STEPS[this.zoomIndex()]);
  readonly saving = signal(false);
  readonly autoPlacing = signal(false);
  readonly draftPending = signal(false);
  readonly pendingPoolItem = signal<PendingPoolItem>(null);
  // Krokiye sürüklenen nesneler havuzdan çıkarılır; kaydetme sonrası sunucudan gelen liste yeniden eşitlenir.
  readonly poolDesks = signal<PoolDesk[]>([]);
  readonly poolRooms = signal<PoolRoom[]>([]);
  readonly templateKey = signal('');
  readonly pendingTemplate = signal<null | { kind: 'template' | 'floor'; value: string }>(null);
  readonly showOnboarding = signal(false);
  readonly deskDraft = signal<Point | null>(null);
  readonly roomDraft = signal<Rect | null>(null);

  readonly suggestedDeskCode = computed(() => suggestDeskCode(this.state.snapshot().desks, this.floor()));
  readonly suggestedRoomCode = computed(() => suggestRoomCode(this.state.snapshot().rooms, this.floor()));

  constructor() {
    // Kat (veya sunucu verisi) değiştiğinde editör durumu sıfırlanır.
    effect(() => {
      const snapshot = this.initialSnapshot();
      untracked(() => {
        const initial: EditorSnapshot = { elements: snapshot.elements, desks: snapshot.desks.map(toEditorDesk), rooms: snapshot.rooms.map(toEditorRoom) };
        this.state.reset(initial);
        this.saved_ = initial;
        this.poolDesks.set(snapshot.unplacedDesks);
        this.poolRooms.set(snapshot.unplacedRooms);
        this.pendingPoolItem.set(null);
        // İlk kullanım yönlendirmesi
        try {
          const seen = window.localStorage.getItem(ONBOARDING_KEY);
          if (!seen || snapshot.elements.length === 0) this.showOnboarding.set(true);
        } catch {
          this.showOnboarding.set(snapshot.elements.length === 0);
        }
      });
    });
  }

  /** Kaydedilmemiş değişiklik uyarısı (sekme kapatma / yenileme). */
  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (!this.state.isDirty()) return;
    event.preventDefault();
    event.returnValue = '';
  }

  switchMode(mode: EditorMode): void {
    this.mode.set(mode);
    this.tool.set('SELECT');
    this.state.selection.set([]);
  }

  zoomBy(delta: number): void {
    this.zoomIndex.update((index) => clamp(index + delta, 0, ZOOM_STEPS.length - 1));
  }

  dismissOnboarding(): void {
    this.showOnboarding.set(false);
    try {
      window.localStorage.setItem(ONBOARDING_KEY, '1');
    } catch {
      // localStorage erişilemezse yönlendirme yalnızca bu oturumda gizlenir.
    }
  }

  // -------------------------------------------------------------------------
  // Öğe işlemleri
  // -------------------------------------------------------------------------
  updateElement({ id, patch }: ElementPatch): void {
    this.state.apply((current) => ({ ...current, elements: current.elements.map((element) => (element.id === id ? { ...element, ...patch } : element)) }));
  }

  updateDesk({ id, patch }: DeskPatch): void {
    const f = this.floor();
    this.state.apply((current) => ({
      ...current,
      desks: current.desks.map((desk) => {
        if (desk.id !== id) return desk;
        const width = Math.max(20, patch.width ?? desk.width);
        const height = Math.max(16, patch.height ?? desk.height);
        const next = clampRect({ x: (patch.x ?? desk.x) - width / 2, y: (patch.y ?? desk.y) - height / 2, width, height }, f.planWidth, f.planHeight);
        return {
          ...desk,
          width: next.width,
          height: next.height,
          rotation: ((Math.round(patch.rotation ?? desk.rotation) % 360) + 360) % 360,
          x: Math.round(next.x + next.width / 2),
          y: Math.round(next.y + next.height / 2),
        };
      }),
    }));
  }

  updateRoom({ id, patch }: RoomPatch): void {
    this.state.apply((current) => ({ ...current, rooms: current.rooms.map((room) => (room.id === id ? { ...room, ...patch } : room)) }));
  }

  deleteSelection(): void {
    const selection = this.state.selection();
    const elementIds = selection.filter((item) => item.kind === 'planElement').map((item) => item.id);
    const hasResources = selection.some((item) => item.kind !== 'planElement');
    if (elementIds.length === 0) {
      if (hasResources) {
        this.toasts.toast({ tone: 'warning', title: 'Rezervasyon nesneleri buradan silinemez', description: 'Masa ve odaları silmek için masa/oda yönetimi ekranlarını kullanın.' });
      }
      return;
    }
    this.state.apply((current) => ({ ...current, elements: current.elements.filter((element) => !elementIds.includes(element.id)) }));
    this.state.selection.set([]);
  }

  duplicateSelection(): void {
    const f = this.floor();
    const selection = this.state.selection();
    const elements = this.state.snapshot().elements.filter((element) => selection.some((item) => item.kind === 'planElement' && item.id === element.id));
    if (elements.length === 0) return;
    const copies = elements.map((element, index) => ({
      ...element,
      id: `kopya-${Date.now()}-${index}`,
      x: Math.min(element.x + 20, f.planWidth - element.width),
      y: Math.min(element.y + 20, f.planHeight - element.height),
      layerOrder: element.layerOrder + 1,
    }));
    this.state.apply((current) => ({ ...current, elements: [...current.elements, ...copies] }));
    this.state.selection.set(copies.map((element) => ({ kind: 'planElement', id: element.id })));
  }

  changeLayer(direction: 'front' | 'back'): void {
    const item = this.state.selection()[0];
    if (!item || item.kind !== 'planElement') return;
    const orders = this.state.snapshot().elements.map((element) => element.layerOrder);
    const target = direction === 'front' ? Math.max(...orders) + 1 : Math.min(...orders) - 1;
    this.updateElement({ id: item.id, patch: { layerOrder: Math.max(0, target) } });
  }

  // -------------------------------------------------------------------------
  // Klavye kısayolları
  // -------------------------------------------------------------------------
  @HostListener('window:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    const target = event.target as HTMLElement | null;
    if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT' || target.isContentEditable)) return;

    const meta = event.ctrlKey || event.metaKey;
    const key = event.key.toLowerCase();
    const selection = this.state.selection();
    const f = this.floor();

    if (meta && key === 'z' && !event.shiftKey) {
      event.preventDefault();
      this.state.undo();
      return;
    }
    if ((meta && key === 'y') || (meta && event.shiftKey && key === 'z')) {
      event.preventDefault();
      this.state.redo();
      return;
    }
    if (meta && key === 'c') {
      const copied = this.state.snapshot().elements.filter((element) => selection.some((item) => item.kind === 'planElement' && item.id === element.id));
      if (copied.length > 0) {
        this.state.clipboard.set(copied);
        event.preventDefault();
      }
      return;
    }
    if (meta && key === 'v') {
      const clipboard = this.state.clipboard();
      if (clipboard.length === 0) return;
      event.preventDefault();
      const copies = clipboard.map((element, index) => ({
        ...element,
        id: `yapistir-${Date.now()}-${index}`,
        x: Math.min(element.x + 24, f.planWidth - element.width),
        y: Math.min(element.y + 24, f.planHeight - element.height),
      }));
      this.state.apply((current) => ({ ...current, elements: [...current.elements, ...copies] }));
      this.state.selection.set(copies.map((element) => ({ kind: 'planElement', id: element.id })));
      return;
    }
    if (meta && key === 'd') {
      event.preventDefault();
      this.duplicateSelection();
      return;
    }
    if (event.key === 'Delete' || event.key === 'Backspace') {
      if (selection.length === 0) return;
      event.preventDefault();
      this.deleteSelection();
      return;
    }
    if (event.key === 'Escape') {
      this.state.selection.set([]);
      this.tool.set('SELECT');
      return;
    }

    const arrows: Record<string, Point> = { ArrowLeft: { x: -1, y: 0 }, ArrowRight: { x: 1, y: 0 }, ArrowUp: { x: 0, y: -1 }, ArrowDown: { x: 0, y: 1 } };
    const direction = arrows[event.key];
    if (direction && selection.length > 0) {
      event.preventDefault();
      const step = event.shiftKey ? 10 : 2;
      const deltaX = direction.x * step;
      const deltaY = direction.y * step;
      this.state.apply((current) => ({
        elements: current.elements.map((element) => {
          if (!selection.some((item) => item.kind === 'planElement' && item.id === element.id)) return element;
          if (element.locked) return element;
          const next = clampRect({ ...element, x: element.x + deltaX, y: element.y + deltaY }, f.planWidth, f.planHeight);
          return { ...element, x: next.x, y: next.y };
        }),
        desks: current.desks.map((desk) => {
          if (!selection.some((item) => item.kind === 'desk' && item.id === desk.id)) return desk;
          const next = clampRect({ x: desk.x + deltaX - desk.width / 2, y: desk.y + deltaY - desk.height / 2, width: desk.width, height: desk.height }, f.planWidth, f.planHeight);
          return { ...desk, x: Math.round(next.x + desk.width / 2), y: Math.round(next.y + desk.height / 2) };
        }),
        rooms: current.rooms.map((room) => {
          if (!selection.some((item) => item.kind === 'meetingRoom' && item.id === room.id)) return room;
          const next = clampRect({ ...room, x: room.x + deltaX, y: room.y + deltaY }, f.planWidth, f.planHeight);
          return { ...room, x: next.x, y: next.y };
        }),
      }));
    }
  }

  // -------------------------------------------------------------------------
  // Şablonlar
  // -------------------------------------------------------------------------
  chooseTemplate(value: string): void {
    this.templateKey.set(value);
    if (!value) return;
    const [kind, id] = value.split(':');
    this.pendingTemplate.set({ kind: kind as 'template' | 'floor', value: id });
  }

  cancelTemplate(): void {
    this.pendingTemplate.set(null);
    this.templateKey.set('');
  }

  async confirmTemplate(): Promise<void> {
    const choice = this.pendingTemplate();
    this.pendingTemplate.set(null);
    if (!choice) return;
    const f = this.floor();
    if (choice.kind === 'template') {
      const template = PLAN_TEMPLATES.find((item) => item.key === choice.value);
      if (!template) return;
      const elements = template.build(f.planWidth, f.planHeight).map((element) => clampElement(element, f.planWidth, f.planHeight));
      this.state.apply((current) => ({ ...current, elements }));
      this.toasts.toast({ tone: 'success', title: `"${template.name}" şablonu uygulandı` });
    } else {
      try {
        const result = await this.api.get<{ elements: PlanElement[] }>(`admin/floors/${f.id}/plan/copy-from/${choice.value}`);
        const elements = withFreshIds(result.elements).map((element) => clampElement(element, f.planWidth, f.planHeight));
        this.state.apply((current) => ({ ...current, elements }));
        this.toasts.toast({ tone: 'success', title: 'Kroki kopyalandı', description: `${elements.length} öğe bu kata aktarıldı. Kaydetmeyi unutmayın.` });
      } catch (error: unknown) {
        this.toasts.toast({ tone: 'error', title: 'Kroki kopyalanamadı', description: errorMessage(error) });
        this.templateKey.set('');
        return;
      }
    }
    this.state.selection.set([]);
    this.templateKey.set('');
  }

  // -------------------------------------------------------------------------
  // Havuz ve kaydetme
  // -------------------------------------------------------------------------
  /** Havuzdan sürüklenen nesneyi krokiye yerleştirir (kaydetme ile kalıcı olur). */
  handleDropPoolItem({ kind, id, point }: PoolDrop): void {
    this.pendingPoolItem.set(null);
    if (kind === 'desk') {
      const pooled = this.poolDesks().find((item) => item.id === id);
      if (!pooled) return;
      const floorName = this.floor().name;
      this.state.apply((current) => ({
        ...current,
        desks: [
          ...current.desks,
          {
            id: pooled.id,
            code: pooled.code,
            x: Math.round(point.x),
            y: Math.round(point.y),
            width: pooled.width,
            height: pooled.height,
            rotation: 0,
            zoneId: pooled.zoneId,
            zoneName: pooled.zoneName,
            floorName,
            active: pooled.active,
            popular: pooled.popular,
            accessibleOnly: pooled.accessibleOnly,
            reservationCount: 0,
          },
        ],
      }));
      this.poolDesks.update((items) => items.filter((item) => item.id !== id));
      this.state.selection.set([{ kind: 'desk', id }]);
      return;
    }
    const pooled = this.poolRooms().find((item) => item.id === id);
    if (!pooled) return;
    this.state.apply((current) => ({
      ...current,
      rooms: [
        ...current.rooms,
        { id: pooled.id, code: pooled.code, name: pooled.name, x: Math.round(point.x), y: Math.round(point.y), width: pooled.width, height: pooled.height, capacity: pooled.capacity, active: pooled.active, reservationCount: 0 },
      ],
    }));
    this.poolRooms.update((items) => items.filter((item) => item.id !== id));
    this.state.selection.set([{ kind: 'meetingRoom', id }]);
  }

  /** Havuzdaki tüm nesneleri krokinin boş alanlarına toplu yerleştirir. */
  async handleAutoPlace(): Promise<void> {
    this.autoPlacing.set(true);
    try {
      const result = await this.api.post<AutoPlaceResult>(`admin/floors/${this.floor().id}/plan/auto-place`);
      const { placedDesks, placedRooms, remainingDesks, remainingRooms } = result;
      const remaining = remainingDesks + remainingRooms;
      this.toasts.toast({
        tone: placedDesks + placedRooms > 0 ? 'success' : 'warning',
        title: placedDesks + placedRooms > 0 ? `${placedDesks} masa, ${placedRooms} oda yerleştirildi` : 'Krokide boş alan bulunamadı',
        description: remaining > 0 ? `${remaining} nesne havuzda kaldı; elle sürükleyerek yerleştirebilirsiniz.` : 'Havuzda bekleyen nesne kalmadı.',
      });
      this.saved.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Otomatik yerleştirme yapılamadı', description: errorMessage(error) });
    } finally {
      this.autoPlacing.set(false);
    }
  }

  async save(): Promise<void> {
    this.saving.set(true);
    const snapshot = this.state.snapshot();
    const f = this.floor();
    const previousDesks = new Map(this.saved_.desks.map((desk) => [desk.id, desk]));
    const previousRooms = new Map(this.saved_.rooms.map((room) => [room.id, room]));

    try {
      const result = await this.api.put<SavePlanResult>(`admin/floors/${f.id}/plan`, {
        floorId: f.id,
        elements: snapshot.elements.map((element) => ({
          ...element,
          x: Math.round(element.x),
          y: Math.round(element.y),
          width: Math.round(element.width),
          height: Math.round(element.height),
          rotation: Math.round(element.rotation),
          label: element.label ?? null,
          linkedResourceId: element.linkedResourceId ?? null,
          linkedResourceType: element.linkedResourceType ?? null,
        })),
        deskPlacements: snapshot.desks
          .filter((desk) => {
            const previous = previousDesks.get(desk.id);
            return !previous || previous.x !== desk.x || previous.y !== desk.y || previous.width !== desk.width || previous.height !== desk.height || previous.rotation !== desk.rotation;
          })
          .map((desk) => ({ id: desk.id, x: Math.round(desk.x), y: Math.round(desk.y), width: Math.round(desk.width), height: Math.round(desk.height), rotation: Math.round(desk.rotation) })),
        roomPlacements: snapshot.rooms
          .filter((room) => {
            const previous = previousRooms.get(room.id);
            return !previous || previous.x !== room.x || previous.y !== room.y || previous.width !== room.width || previous.height !== room.height;
          })
          .map((room) => ({ id: room.id, x: Math.round(room.x), y: Math.round(room.y), width: Math.round(room.width), height: Math.round(room.height) })),
      });

      // Sunucudan dönen kalıcı kimliklerle durumu tazele.
      const savedSnapshot: EditorSnapshot = { elements: result.elements, desks: snapshot.desks, rooms: snapshot.rooms };
      this.state.apply(() => savedSnapshot);
      this.state.markSaved(savedSnapshot);
      this.saved_ = savedSnapshot;
      this.state.selection.set([]);
      this.toasts.toast({ tone: 'success', title: 'Kroki kaydedildi', description: `${result.createdCount} eklendi · ${result.updatedCount} güncellendi · ${result.deletedCount} silindi` });
      this.saved.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Kroki kaydedilemedi', description: errorMessage(error) });
    } finally {
      this.saving.set(false);
    }
  }

  // -------------------------------------------------------------------------
  // Yeni masa / oda oluşturma
  // -------------------------------------------------------------------------
  async createDesk(values: DeskDraftValues): Promise<void> {
    const draft = this.deskDraft();
    if (!draft) return;
    this.draftPending.set(true);
    try {
      const desk = await this.api.post<SnapshotDesk>(`admin/floors/${this.floor().id}/plan/desks`, {
        floorId: this.floor().id,
        zoneId: values.zoneId,
        code: values.code,
        x: draft.x,
        y: draft.y,
        popular: values.popular,
        accessibleOnly: values.accessibleOnly,
      });
      const editorDesk = toEditorDesk(desk);
      this.state.apply((current) => ({ ...current, desks: [...current.desks, editorDesk] }));
      this.saved_ = { ...this.saved_, desks: [...this.saved_.desks, editorDesk] };
      this.state.markSaved({ ...this.state.snapshot() });
      this.deskDraft.set(null);
      this.state.selection.set([{ kind: 'desk', id: editorDesk.id }]);
      this.toasts.toast({ tone: 'success', title: 'Masa eklendi', description: `${editorDesk.code} krokiye yerleştirildi.` });
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Masa eklenemedi', description: errorMessage(error) });
    } finally {
      this.draftPending.set(false);
    }
  }

  async createRoom(values: RoomDraftValues): Promise<void> {
    const draft = this.roomDraft();
    if (!draft) return;
    this.draftPending.set(true);
    try {
      const room = await this.api.post<SnapshotRoom>(`admin/floors/${this.floor().id}/plan/rooms`, {
        floorId: this.floor().id,
        code: values.code,
        name: values.name,
        capacity: values.capacity,
        x: draft.x,
        y: draft.y,
        width: draft.width,
        height: draft.height,
      });
      const editorRoom = toEditorRoom(room);
      this.state.apply((current) => ({ ...current, rooms: [...current.rooms, editorRoom] }));
      this.saved_ = { ...this.saved_, rooms: [...this.saved_.rooms, editorRoom] };
      this.state.markSaved({ ...this.state.snapshot() });
      this.roomDraft.set(null);
      this.state.selection.set([{ kind: 'meetingRoom', id: editorRoom.id }]);
      this.toasts.toast({ tone: 'success', title: 'Toplantı odası eklendi', description: `${editorRoom.name} krokiye yerleştirildi.` });
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Toplantı odası eklenemedi', description: errorMessage(error) });
    } finally {
      this.draftPending.set(false);
    }
  }

  /** Mevcut masayı seçilen konuma taşır (yeni kayıt oluşturmadan). */
  moveExistingDesk(deskId: string): void {
    const draft = this.deskDraft();
    if (!draft) return;
    this.updateDesk({ id: deskId, patch: { x: draft.x, y: draft.y } });
    this.deskDraft.set(null);
    this.state.selection.set([{ kind: 'desk', id: deskId }]);
  }

  moveExistingRoom(roomId: string): void {
    const draft = this.roomDraft();
    if (!draft) return;
    this.updateRoom({ id: roomId, patch: draft });
    this.roomDraft.set(null);
    this.state.selection.set([{ kind: 'meetingRoom', id: roomId }]);
  }
}

function toEditorDesk(desk: SnapshotDesk): EditorDesk {
  return { ...desk };
}

function toEditorRoom(room: SnapshotRoom): EditorRoom {
  return { ...room };
}

export type { SelectionItem };
