import { Component, ElementRef, computed, input, output, signal, viewChild } from '@angular/core';
import { PLAN_ELEMENT_DEFINITIONS, PLAN_GRID_SIZE, PLAN_MIN_SIZE, RESOURCE_SIZES, clampRect, normalizeRect, snapToGrid, wallRectFromPoints, type PlanElementType } from '../../../lib/plan';
import { cn } from '../../../lib/utils';
import { PlanElementsLayer } from '../../../shared/floor-plan/plan-shapes';
import type { EditorSnapshot, Point, Rect, SelectionItem, SelectionKind } from './editor-state';
import { POOL_DRAG_TYPE } from './plan-pool';
import type { EditorMode, EditorTool } from './plan-toolbox';

/** Çizim alanı (kaynak: plan-canvas.tsx). Pointer olayları, 8 tutamaç, döndürme, çoklu seçim, ızgara/hizalama. */

type HandleId = 'nw' | 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w';

type Interaction =
  | { mode: 'idle' }
  | { mode: 'draw'; start: Point; current: Point; shift: boolean }
  | { mode: 'move'; start: Point; origins: Record<string, Point>; changed: boolean }
  | { mode: 'resize'; handle: HandleId; start: Point; original: Rect; target: SelectionItem; changed: boolean }
  | { mode: 'rotate'; center: Point; original: number; startAngle: number; target: SelectionItem; changed: boolean }
  | { mode: 'marquee'; start: Point; current: Point };

const HANDLES: Array<{ id: HandleId; fx: number; fy: number; cursor: string }> = [
  { id: 'nw', fx: 0, fy: 0, cursor: 'nwse-resize' },
  { id: 'n', fx: 0.5, fy: 0, cursor: 'ns-resize' },
  { id: 'ne', fx: 1, fy: 0, cursor: 'nesw-resize' },
  { id: 'e', fx: 1, fy: 0.5, cursor: 'ew-resize' },
  { id: 'se', fx: 1, fy: 1, cursor: 'nwse-resize' },
  { id: 's', fx: 0.5, fy: 1, cursor: 'ns-resize' },
  { id: 'sw', fx: 0, fy: 1, cursor: 'nesw-resize' },
  { id: 'w', fx: 0, fy: 0.5, cursor: 'ew-resize' },
];

/** Sürükleme eşiği (plan birimi). Salt tıklama nesneyi kaydırmamalıdır. */
const MOVE_THRESHOLD = 3;

/** Masanın krokideki dikdörtgeni: x/y merkez noktasıdır. */
export function deskRect(desk: { x: number; y: number; width: number; height: number }): Rect {
  return { x: desk.x - desk.width / 2, y: desk.y - desk.height / 2, width: desk.width, height: desk.height };
}

function applyResize(original: Rect, handle: HandleId, deltaX: number, deltaY: number): Rect {
  let { x, y, width, height } = original;
  if (handle.includes('w')) {
    x = original.x + deltaX;
    width = original.width - deltaX;
  }
  if (handle.includes('e')) width = original.width + deltaX;
  if (handle.includes('n')) {
    y = original.y + deltaY;
    height = original.height - deltaY;
  }
  if (handle.includes('s')) height = original.height + deltaY;
  if (width < PLAN_MIN_SIZE) {
    if (handle.includes('w')) x = original.x + original.width - PLAN_MIN_SIZE;
    width = PLAN_MIN_SIZE;
  }
  if (height < PLAN_MIN_SIZE) {
    if (handle.includes('n')) y = original.y + original.height - PLAN_MIN_SIZE;
    height = PLAN_MIN_SIZE;
  }
  return { x, y, width, height };
}

function rectsIntersect(a: Rect, b: Rect): boolean {
  return a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;
}

export interface PoolDrop {
  kind: 'desk' | 'meetingRoom';
  id: string;
  point: Point;
}

@Component({
  selector: 'app-plan-canvas',
  imports: [PlanElementsLayer],
  template: `
    @let s = snapshot();
    @let w = planWidth();
    @let h = planHeight();
    @let sketch = mode() === 'sketch';
    @let selecting = tool() === 'SELECT';
    <div class="overflow-auto rounded-xl border border-line bg-white scrollbar-thin" tabindex="0" role="region" aria-label="Kroki düzenleme alanı">
      <svg
        #svg
        [attr.width]="w * zoom()"
        [attr.height]="h * zoom()"
        [attr.viewBox]="'0 0 ' + w + ' ' + h"
        [class]="cn('block touch-none select-none', pendingPoolItem() ? 'cursor-copy' : selecting ? 'cursor-default' : 'cursor-crosshair')"
        (pointerdown)="onCanvasPointerDown($event)"
        (pointermove)="onPointerMove($event)"
        (pointerup)="onPointerUp($event)"
        (pointercancel)="interaction.set({ mode: 'idle' })"
        (dragover)="onDragOver($event)"
        (drop)="onDrop($event)"
        role="application"
        aria-label="Kat planı çizim alanı"
      >
        <defs>
          <pattern id="plan-grid" [attr.width]="grid" [attr.height]="grid" patternUnits="userSpaceOnUse">
            <path [attr.d]="'M ' + grid + ' 0 L 0 0 0 ' + grid" fill="none" stroke="#e2e8f0" stroke-width="0.6" />
          </pattern>
          <pattern id="plan-grid-major" [attr.width]="grid * 10" [attr.height]="grid * 10" patternUnits="userSpaceOnUse">
            <rect [attr.width]="grid * 10" [attr.height]="grid * 10" fill="url(#plan-grid)" />
            <path [attr.d]="'M ' + grid * 10 + ' 0 L 0 0 0 ' + grid * 10" fill="none" stroke="#cbd5e1" stroke-width="1" />
          </pattern>
        </defs>

        <rect x="0" y="0" [attr.width]="w" [attr.height]="h" fill="#ffffff" />
        @if (showGrid()) {
          <rect x="0" y="0" [attr.width]="w" [attr.height]="h" fill="url(#plan-grid-major)" />
        }
        <rect x="0.5" y="0.5" [attr.width]="w - 1" [attr.height]="h - 1" fill="none" stroke="#94a3b8" stroke-width="1" stroke-dasharray="6 4" />

        <!-- Kroki katmanı (rezervasyon sekmesinde soluk gösterilir) -->
        <svg:g planElementsLayer [elements]="s.elements" [muted]="!sketch" />

        <!-- Kroki öğelerinin tıklama alanları. Masalar ve odalar bunların üzerine çizildiği için üstteki nesne tıklamayı önce yakalar. -->
        @for (element of orderedElements(); track element.id) {
          <rect
            [attr.x]="element.x"
            [attr.y]="element.y"
            [attr.width]="element.width"
            [attr.height]="element.height"
            [attr.transform]="element.rotation ? 'rotate(' + element.rotation + ' ' + (element.x + element.width / 2) + ' ' + (element.y + element.height / 2) + ')' : null"
            fill="transparent"
            [class]="element.locked || !element.visible ? 'cursor-not-allowed' : 'cursor-move'"
            [style.pointer-events]="selecting && !element.locked && element.visible ? 'auto' : 'none'"
            (pointerdown)="objectPointerDown($event, 'planElement', element.id)"
          />
        }

        <!-- Toplantı odaları - her iki sekmede de seçilebilir -->
        @for (room of s.rooms; track room.id) {
          @let roomSelected = isSelected('meetingRoom', room.id);
          <g [attr.opacity]="sketch ? 0.75 : 1">
            <rect
              [attr.x]="room.x"
              [attr.y]="room.y"
              [attr.width]="room.width"
              [attr.height]="room.height"
              rx="8"
              [attr.fill]="room.active ? '#dbeafe' : '#fef2f2'"
              [attr.stroke]="roomSelected ? '#1f5493' : room.active ? '#60a5fa' : '#f87171'"
              [attr.stroke-width]="roomSelected ? 3 : 1.6"
              [style.pointer-events]="selecting ? 'auto' : 'none'"
              [class.cursor-move]="selecting"
              (pointerdown)="objectPointerDown($event, 'meetingRoom', room.id)"
            />
            <text [attr.x]="room.x + room.width / 2" [attr.y]="room.y + room.height / 2 - 4" text-anchor="middle" font-size="13" font-weight="600" fill="#1e3a8a" class="pointer-events-none">{{ room.name }}</text>
            <text [attr.x]="room.x + room.width / 2" [attr.y]="room.y + room.height / 2 + 13" text-anchor="middle" font-size="11" fill="#1d4ed8" class="pointer-events-none">{{ room.code }} · {{ room.capacity }} kişi</text>
            <circle [attr.cx]="room.x + 12" [attr.cy]="room.y + 12" r="5" fill="#0d9488" class="pointer-events-none" />
          </g>
        }

        <!-- Masalar - her iki sekmede de seçilebilir -->
        @for (desk of s.desks; track desk.id) {
          @let deskSelected = isSelected('desk', desk.id);
          @let r = deskRect(desk);
          <g [attr.opacity]="sketch ? 0.75 : 1" [attr.transform]="desk.rotation ? 'rotate(' + desk.rotation + ' ' + desk.x + ' ' + desk.y + ')' : null">
            <rect
              [attr.x]="r.x"
              [attr.y]="r.y"
              [attr.width]="r.width"
              [attr.height]="r.height"
              rx="7"
              [attr.fill]="desk.active ? '#ffffff' : '#fee2e2'"
              [attr.stroke]="deskSelected ? '#1f5493' : desk.active ? '#94a3b8' : '#ef4444'"
              [attr.stroke-width]="deskSelected ? 3 : 1.6"
              [style.pointer-events]="selecting ? 'auto' : 'none'"
              [class.cursor-move]="selecting"
              (pointerdown)="objectPointerDown($event, 'desk', desk.id)"
            />
            <text [attr.x]="desk.x" [attr.y]="desk.y + 4" text-anchor="middle" font-size="10.5" font-weight="600" fill="#334155" class="pointer-events-none">{{ shortCode(desk.code) }}</text>
            <circle [attr.cx]="r.x + 7" [attr.cy]="r.y + 7" r="3.5" fill="#0d9488" class="pointer-events-none" />
            @if (desk.popular) {
              <text [attr.x]="r.x + r.width - 6" [attr.y]="r.y + 10" font-size="9" fill="#b45309" class="pointer-events-none">★</text>
            }
            @if (desk.accessibleOnly) {
              <text [attr.x]="desk.x" [attr.y]="r.y - 3" font-size="10" text-anchor="middle" fill="#6d28d9" class="pointer-events-none">♿</text>
            }
          </g>
        }

        <!-- Çizim önizlemesi -->
        @if (drawPreview(); as preview) {
          <rect [attr.x]="preview.x" [attr.y]="preview.y" [attr.width]="Math.max(1, preview.width)" [attr.height]="Math.max(1, preview.height)" fill="rgba(47, 109, 181, 0.16)" stroke="#2f6db5" stroke-width="1.5" stroke-dasharray="5 4" class="pointer-events-none" />
        }

        <!-- Çoklu seçim dikdörtgeni -->
        @if (marqueeRect(); as mq) {
          <rect [attr.x]="mq.x" [attr.y]="mq.y" [attr.width]="mq.width" [attr.height]="mq.height" fill="rgba(47, 109, 181, 0.1)" stroke="#2f6db5" stroke-width="1" stroke-dasharray="4 3" class="pointer-events-none" />
        }

        <!-- Seçim çerçevesi ve tutamaçlar - üç nesne türü için ortak -->
        @if (activeRect(); as active) {
          <g [attr.transform]="active.rotation ? 'rotate(' + active.rotation + ' ' + (active.x + active.width / 2) + ' ' + (active.y + active.height / 2) + ')' : null">
            <rect [attr.x]="active.x - 3" [attr.y]="active.y - 3" [attr.width]="active.width + 6" [attr.height]="active.height + 6" fill="none" stroke="#1f5493" stroke-width="2" stroke-dasharray="5 3" class="pointer-events-none" />
            @for (handle of handles; track handle.id) {
              <rect
                [attr.x]="active.x + active.width * handle.fx - 5"
                [attr.y]="active.y + active.height * handle.fy - 5"
                width="10"
                height="10"
                rx="2"
                fill="#ffffff"
                stroke="#1f5493"
                stroke-width="1.5"
                [style.cursor]="handle.cursor"
                (pointerdown)="beginResize($event, handle.id, active)"
              />
            }
            @if (active.rotatable) {
              <g>
                <line [attr.x1]="active.x + active.width / 2" [attr.y1]="active.y - 3" [attr.x2]="active.x + active.width / 2" [attr.y2]="active.y - 26" stroke="#1f5493" stroke-width="1.5" class="pointer-events-none" />
                <circle [attr.cx]="active.x + active.width / 2" [attr.cy]="active.y - 30" r="7" fill="#ffffff" stroke="#1f5493" stroke-width="1.5" style="cursor: grab" (pointerdown)="beginRotate($event, active)" />
              </g>
            }
          </g>
        }

        <!-- Çoklu seçim vurgusu -->
        @for (item of multiRects(); track item.key) {
          <rect
            [attr.x]="item.rect.x - 3"
            [attr.y]="item.rect.y - 3"
            [attr.width]="item.rect.width + 6"
            [attr.height]="item.rect.height + 6"
            [attr.transform]="item.rotation ? 'rotate(' + item.rotation + ' ' + (item.rect.x + item.rect.width / 2) + ' ' + (item.rect.y + item.rect.height / 2) + ')' : null"
            fill="none"
            stroke="#1f5493"
            stroke-width="2"
            stroke-dasharray="5 3"
            class="pointer-events-none"
          />
        }
      </svg>
    </div>
  `,
})
export class PlanCanvas {
  readonly planWidth = input.required<number>();
  readonly planHeight = input.required<number>();
  readonly snapshot = input.required<EditorSnapshot>();
  readonly mode = input.required<EditorMode>();
  readonly tool = input.required<EditorTool>();
  readonly selection = input.required<SelectionItem[]>();
  readonly showGrid = input(true);
  readonly snapEnabled = input(true);
  readonly zoom = input(1);
  /** Havuzda seçili bekleyen nesne; tuvale tıklandığında oraya yerleştirilir. */
  readonly pendingPoolItem = input<{ kind: 'desk' | 'meetingRoom'; id: string } | null>(null);

  readonly toolReset = output<void>();
  readonly selectionChange = output<SelectionItem[]>();
  readonly applyChange = output<{ updater: (current: EditorSnapshot) => EditorSnapshot; commit?: boolean }>();
  readonly requestDesk = output<Point>();
  readonly requestRoom = output<Rect>();
  /** Havuzdan sürüklenen ya da tıklanarak seçilen nesnenin krokiye bırakılması. */
  readonly dropPoolItem = output<PoolDrop>();

  private readonly svgRef = viewChild.required<ElementRef<SVGSVGElement>>('svg');
  readonly interaction = signal<Interaction>({ mode: 'idle' });

  readonly cn = cn;
  readonly Math = Math;
  readonly grid = PLAN_GRID_SIZE;
  readonly handles = HANDLES;
  readonly deskRect = deskRect;

  readonly orderedElements = computed(() => [...this.snapshot().elements].sort((a, b) => a.layerOrder - b.layerOrder));

  shortCode(code: string): string {
    return code.replace(/^F\d+-/, '');
  }

  isSelected(kind: SelectionKind, id: string): boolean {
    return this.selection().some((item) => item.kind === kind && item.id === id);
  }

  private apply(updater: (current: EditorSnapshot) => EditorSnapshot, commit = true): void {
    this.applyChange.emit({ updater, commit });
  }

  private toPlan(event: { clientX: number; clientY: number }): Point {
    const svg = this.svgRef().nativeElement;
    const matrix = svg.getScreenCTM();
    if (!matrix) return { x: 0, y: 0 };
    const point = svg.createSVGPoint();
    point.x = event.clientX;
    point.y = event.clientY;
    const transformed = point.matrixTransform(matrix.inverse());
    return { x: transformed.x, y: transformed.y };
  }

  /** Seçili tek nesnenin dikdörtgeni: kroki öğesi, masa veya toplantı odası. */
  readonly activeRect = computed<(Rect & { rotation: number; rotatable: boolean }) | null>(() => {
    const selection = this.selection();
    if (selection.length !== 1) return null;
    const item = selection[0];
    const s = this.snapshot();
    if (item.kind === 'planElement') {
      const element = s.elements.find((entry) => entry.id === item.id);
      if (!element || element.locked) return null;
      return { x: element.x, y: element.y, width: element.width, height: element.height, rotation: element.rotation, rotatable: PLAN_ELEMENT_DEFINITIONS[element.type]?.rotatable ?? false };
    }
    if (item.kind === 'desk') {
      const desk = s.desks.find((entry) => entry.id === item.id);
      if (!desk) return null;
      return { ...deskRect(desk), rotation: desk.rotation, rotatable: true };
    }
    const room = s.rooms.find((entry) => entry.id === item.id);
    if (!room) return null;
    return { x: room.x, y: room.y, width: room.width, height: room.height, rotation: 0, rotatable: false };
  });

  readonly drawPreview = computed<Rect | null>(() => {
    const interaction = this.interaction();
    if (interaction.mode !== 'draw') return null;
    const tool = this.tool();
    if (tool === 'ROOM_RESOURCE') return normalizeRect(interaction.start, interaction.current);
    const definition = PLAN_ELEMENT_DEFINITIONS[tool as PlanElementType];
    if (!definition) return null;
    if (definition.lineTool) return wallRectFromPoints(interaction.start, interaction.current, definition.defaultHeight, interaction.shift);
    return normalizeRect(interaction.start, interaction.current);
  });

  readonly marqueeRect = computed<Rect | null>(() => {
    const interaction = this.interaction();
    return interaction.mode === 'marquee' ? normalizeRect(interaction.start, interaction.current) : null;
  });

  readonly multiRects = computed(() => {
    const selection = this.selection();
    if (selection.length < 2) return [];
    const s = this.snapshot();
    const rects: Array<{ key: string; rect: Rect; rotation: number }> = [];
    for (const item of selection) {
      if (item.kind === 'planElement') {
        const element = s.elements.find((entry) => entry.id === item.id);
        if (element) rects.push({ key: `multi-${item.id}`, rect: { x: element.x, y: element.y, width: element.width, height: element.height }, rotation: element.rotation });
      } else if (item.kind === 'desk') {
        const desk = s.desks.find((entry) => entry.id === item.id);
        if (desk) rects.push({ key: `multi-${item.id}`, rect: deskRect(desk), rotation: desk.rotation });
      } else {
        const room = s.rooms.find((entry) => entry.id === item.id);
        if (room) rects.push({ key: `multi-${item.id}`, rect: { x: room.x, y: room.y, width: room.width, height: room.height }, rotation: 0 });
      }
    }
    return rects;
  });

  // -------------------------------------------------------------------------
  // Seçim ve taşıma
  // -------------------------------------------------------------------------
  private beginMove(event: PointerEvent, kind: SelectionKind, id: string): void {
    const selection = this.selection();
    const additive = event.shiftKey;
    const already = selection.some((item) => item.kind === kind && item.id === id);
    let nextSelection: SelectionItem[];
    if (additive) nextSelection = already ? selection.filter((item) => !(item.kind === kind && item.id === id)) : [...selection, { kind, id }];
    else nextSelection = already ? selection : [{ kind, id }];
    this.selectionChange.emit(nextSelection);

    const s = this.snapshot();
    const origins: Record<string, Point> = {};
    for (const item of nextSelection) {
      if (item.kind === 'planElement') {
        const element = s.elements.find((entry) => entry.id === item.id);
        if (element && !element.locked) origins[`planElement:${item.id}`] = { x: element.x, y: element.y };
      } else if (item.kind === 'desk') {
        const desk = s.desks.find((entry) => entry.id === item.id);
        if (desk) origins[`desk:${item.id}`] = { x: desk.x, y: desk.y };
      } else {
        const room = s.rooms.find((entry) => entry.id === item.id);
        if (room) origins[`meetingRoom:${item.id}`] = { x: room.x, y: room.y };
      }
    }
    if (Object.keys(origins).length === 0) return;
    this.interaction.set({ mode: 'move', start: this.toPlan(event), origins, changed: false });
  }

  /** Nesneye basıldığında olay tuvale yayılmaz; seçim anında oluşur. */
  objectPointerDown(event: PointerEvent, kind: SelectionKind, id: string): void {
    if (this.pendingPoolItem()) return;
    if (this.tool() !== 'SELECT') return;
    event.stopPropagation();
    event.preventDefault();
    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
    this.beginMove(event, kind, id);
  }

  beginResize(event: PointerEvent, handle: HandleId, active: Rect): void {
    event.stopPropagation();
    event.preventDefault();
    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
    this.interaction.set({
      mode: 'resize',
      handle,
      start: this.toPlan(event),
      original: { x: active.x, y: active.y, width: active.width, height: active.height },
      target: this.selection()[0],
      changed: false,
    });
  }

  beginRotate(event: PointerEvent, active: Rect & { rotation: number }): void {
    event.stopPropagation();
    event.preventDefault();
    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
    const center = { x: active.x + active.width / 2, y: active.y + active.height / 2 };
    const point = this.toPlan(event);
    this.interaction.set({
      mode: 'rotate',
      center,
      original: active.rotation,
      startAngle: (Math.atan2(point.y - center.y, point.x - center.x) * 180) / Math.PI,
      target: this.selection()[0],
      changed: false,
    });
  }

  // -------------------------------------------------------------------------
  // Tuval olayları
  // -------------------------------------------------------------------------
  onCanvasPointerDown(event: PointerEvent): void {
    if (event.button !== 0) return;
    const point = this.toPlan(event);
    const snap = this.snapEnabled();

    // Havuzdan bir nesne seçilmişse tıklanan nokta onun konumu olur.
    const pending = this.pendingPoolItem();
    if (pending) {
      this.dropPoolItem.emit({ kind: pending.kind, id: pending.id, point: { x: snapToGrid(point.x, snap), y: snapToGrid(point.y, snap) } });
      return;
    }

    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);

    if (this.tool() === 'SELECT') {
      this.selectionChange.emit([]);
      this.interaction.set({ mode: 'marquee', start: point, current: point });
      return;
    }

    if (this.tool() === 'DESK') {
      this.requestDesk.emit({ x: snapToGrid(point.x, snap), y: snapToGrid(point.y, snap) });
      this.toolReset.emit();
      return;
    }

    this.interaction.set({ mode: 'draw', start: point, current: point, shift: event.shiftKey });
  }

  onPointerMove(event: PointerEvent): void {
    const interaction = this.interaction();
    if (interaction.mode === 'idle') return;
    const point = this.toPlan(event);
    const snap = this.snapEnabled();
    const w = this.planWidth();
    const h = this.planHeight();

    if (interaction.mode === 'draw') {
      this.interaction.set({ ...interaction, current: point, shift: event.shiftKey });
      return;
    }
    if (interaction.mode === 'marquee') {
      this.interaction.set({ ...interaction, current: point });
      return;
    }

    if (interaction.mode === 'move') {
      const deltaX = point.x - interaction.start.x;
      const deltaY = point.y - interaction.start.y;
      // Eşik aşılmadıysa seçim yapılır ama nesne yerinde kalır.
      if (!interaction.changed && Math.abs(deltaX) < MOVE_THRESHOLD && Math.abs(deltaY) < MOVE_THRESHOLD) return;
      this.apply(
        (current) => ({
          elements: current.elements.map((element) => {
            const origin = interaction.origins[`planElement:${element.id}`];
            if (!origin) return element;
            const next = clampRect({ x: snapToGrid(origin.x + deltaX, snap), y: snapToGrid(origin.y + deltaY, snap), width: element.width, height: element.height }, w, h);
            return { ...element, x: next.x, y: next.y };
          }),
          desks: current.desks.map((desk) => {
            const origin = interaction.origins[`desk:${desk.id}`];
            if (!origin) return desk;
            const next = clampRect(
              { x: snapToGrid(origin.x + deltaX, snap) - desk.width / 2, y: snapToGrid(origin.y + deltaY, snap) - desk.height / 2, width: desk.width, height: desk.height },
              w,
              h,
            );
            return { ...desk, x: Math.round(next.x + desk.width / 2), y: Math.round(next.y + desk.height / 2) };
          }),
          rooms: current.rooms.map((room) => {
            const origin = interaction.origins[`meetingRoom:${room.id}`];
            if (!origin) return room;
            const next = clampRect({ x: snapToGrid(origin.x + deltaX, snap), y: snapToGrid(origin.y + deltaY, snap), width: room.width, height: room.height }, w, h);
            return { ...room, x: next.x, y: next.y };
          }),
        }),
        false,
      );
      if (!interaction.changed) this.interaction.set({ ...interaction, changed: true });
      return;
    }

    if (interaction.mode === 'resize') {
      const deltaX = point.x - interaction.start.x;
      const deltaY = point.y - interaction.start.y;
      const resized = applyResize(interaction.original, interaction.handle, deltaX, deltaY);
      const snapped = clampRect(
        {
          x: snapToGrid(resized.x, snap),
          y: snapToGrid(resized.y, snap),
          width: Math.max(PLAN_MIN_SIZE, snapToGrid(resized.width, snap)),
          height: Math.max(PLAN_MIN_SIZE, snapToGrid(resized.height, snap)),
        },
        w,
        h,
      );
      const target = interaction.target;
      this.apply(
        (current) => ({
          elements: target.kind === 'planElement' ? current.elements.map((element) => (element.id === target.id ? { ...element, ...snapped } : element)) : current.elements,
          desks:
            target.kind === 'desk'
              ? current.desks.map((desk) => {
                  if (desk.id !== target.id) return desk;
                  const width = Math.max(20, snapped.width);
                  const height = Math.max(16, snapped.height);
                  return { ...desk, width, height, x: Math.round(snapped.x + width / 2), y: Math.round(snapped.y + height / 2) };
                })
              : current.desks,
          rooms:
            target.kind === 'meetingRoom'
              ? current.rooms.map((room) => (room.id === target.id ? { ...room, x: snapped.x, y: snapped.y, width: Math.max(40, snapped.width), height: Math.max(40, snapped.height) } : room))
              : current.rooms,
        }),
        false,
      );
      if (!interaction.changed) this.interaction.set({ ...interaction, changed: true });
      return;
    }

    if (interaction.mode === 'rotate') {
      const angle = (Math.atan2(point.y - interaction.center.y, point.x - interaction.center.x) * 180) / Math.PI;
      const raw = interaction.original + (angle - interaction.startAngle);
      const rotation = ((Math.round(event.shiftKey ? Math.round(raw / 15) * 15 : raw) % 360) + 360) % 360;
      const target = interaction.target;
      this.apply(
        (current) => ({
          ...current,
          elements: target.kind === 'planElement' ? current.elements.map((element) => (element.id === target.id ? { ...element, rotation } : element)) : current.elements,
          desks: target.kind === 'desk' ? current.desks.map((desk) => (desk.id === target.id ? { ...desk, rotation } : desk)) : current.desks,
        }),
        false,
      );
      if (!interaction.changed) this.interaction.set({ ...interaction, changed: true });
    }
  }

  onPointerUp(event: PointerEvent): void {
    const interaction = this.interaction();
    if (interaction.mode === 'idle') return;
    const snap = this.snapEnabled();
    const w = this.planWidth();
    const h = this.planHeight();
    const tool = this.tool();

    if (interaction.mode === 'draw') {
      const start = interaction.start;
      const end = this.toPlan(event);
      const dragged = Math.abs(end.x - start.x) > 4 || Math.abs(end.y - start.y) > 4;

      if (tool === 'ROOM_RESOURCE') {
        const rect = dragged ? normalizeRect(start, end) : { x: start.x, y: start.y, width: RESOURCE_SIZES.room.width, height: RESOURCE_SIZES.room.height };
        this.requestRoom.emit(
          clampRect(
            { x: snapToGrid(rect.x, snap), y: snapToGrid(rect.y, snap), width: Math.max(60, snapToGrid(rect.width, snap)), height: Math.max(50, snapToGrid(rect.height, snap)) },
            w,
            h,
          ),
        );
        this.toolReset.emit();
        this.interaction.set({ mode: 'idle' });
        return;
      }

      const definition = PLAN_ELEMENT_DEFINITIONS[tool as PlanElementType];
      if (definition) {
        let rect: Rect;
        if (definition.lineTool) {
          rect = wallRectFromPoints(
            { x: snapToGrid(start.x, snap), y: snapToGrid(start.y, snap) },
            { x: snapToGrid(end.x, snap), y: snapToGrid(end.y, snap) },
            definition.defaultHeight,
            interaction.shift || event.shiftKey,
          );
        } else if (dragged) {
          const normalized = normalizeRect(start, end);
          rect = {
            x: snapToGrid(normalized.x, snap),
            y: snapToGrid(normalized.y, snap),
            width: Math.max(PLAN_MIN_SIZE, snapToGrid(normalized.width, snap)),
            height: Math.max(PLAN_MIN_SIZE, snapToGrid(normalized.height, snap)),
          };
        } else {
          rect = { x: snapToGrid(start.x, snap), y: snapToGrid(start.y, snap), width: definition.defaultWidth, height: definition.defaultHeight };
        }

        const final = clampRect(rect, w, h);
        const id = `yeni-${Date.now()}-${Math.round(final.x)}-${Math.round(final.y)}`;
        this.apply((current) => ({
          ...current,
          elements: [
            ...current.elements,
            {
              id,
              type: definition.type,
              label: definition.defaultLabel,
              x: Math.round(final.x),
              y: Math.round(final.y),
              width: Math.round(final.width),
              height: Math.round(final.height),
              rotation: 0,
              style: { ...definition.defaultStyle },
              layerOrder: definition.baseLayer + current.elements.filter((element) => element.type === definition.type).length,
              locked: false,
              visible: true,
              linkedResourceId: null,
              linkedResourceType: null,
            },
          ],
        }));
        this.selectionChange.emit([{ kind: 'planElement', id }]);
        this.toolReset.emit();
      }
      this.interaction.set({ mode: 'idle' });
      return;
    }

    if (interaction.mode === 'marquee') {
      const rect = normalizeRect(interaction.start, interaction.current);
      if (rect.width > 4 && rect.height > 4) {
        const s = this.snapshot();
        const picked: SelectionItem[] = [];
        for (const element of s.elements) {
          if (!element.locked && element.visible && rectsIntersect(rect, element)) picked.push({ kind: 'planElement', id: element.id });
        }
        for (const desk of s.desks) if (rectsIntersect(rect, deskRect(desk))) picked.push({ kind: 'desk', id: desk.id });
        for (const room of s.rooms) if (rectsIntersect(rect, room)) picked.push({ kind: 'meetingRoom', id: room.id });
        this.selectionChange.emit(picked);
      }
      this.interaction.set({ mode: 'idle' });
      return;
    }

    if ((interaction.mode === 'move' || interaction.mode === 'resize' || interaction.mode === 'rotate') && interaction.changed) {
      // Sürükleme bittiğinde geçmişe tek kayıt yazılır.
      this.apply((current) => ({ ...current }), true);
    }
    this.interaction.set({ mode: 'idle' });
  }

  onDragOver(event: DragEvent): void {
    if (!event.dataTransfer?.types.includes(POOL_DRAG_TYPE)) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }

  onDrop(event: DragEvent): void {
    const raw = event.dataTransfer?.getData(POOL_DRAG_TYPE);
    if (!raw) return;
    event.preventDefault();
    try {
      const payload = JSON.parse(raw) as { kind: 'desk' | 'meetingRoom'; id: string };
      const point = this.toPlan(event);
      const snap = this.snapEnabled();
      this.dropPoolItem.emit({ kind: payload.kind, id: payload.id, point: { x: snapToGrid(point.x, snap), y: snapToGrid(point.y, snap) } });
    } catch {
      // Geçersiz sürükleme verisi yok sayılır.
    }
  }
}
