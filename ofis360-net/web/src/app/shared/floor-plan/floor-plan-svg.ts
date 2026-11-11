import { Component, computed, input, output } from '@angular/core';
import type { PlanElement } from '../../lib/plan';
import { cn } from '../../lib/utils';
import type { DeskView, RoomView, ZoneView } from '../../core/api/types';
import { PlanElementsLayer } from './plan-shapes';

/**
 * Kat planı görselleştirmesi (SVG). Kaynak: components/floor-plan/floor-plan-svg.tsx
 *
 * Erişilebilirlik: masa durumları yalnızca renkle değil; desen, ikon ve metin etiketiyle de ayrıştırılır.
 * Her masa klavye ile odaklanabilir ve Enter/Space ile seçilebilir; aria-label durum bilgisini içerir.
 */

const DESK_WIDTH = 46;
const DESK_HEIGHT = 34;

export interface DeskStateStyle {
  fill: string;
  stroke: string;
  text: string;
  label: string;
  pattern?: string;
}

export const DESK_STATE_STYLE: Record<string, DeskStateStyle> = {
  AVAILABLE: { fill: '#ffffff', stroke: '#94a3b8', text: '#334155', label: 'Uygun' },
  SELECTED: { fill: '#1f5493', stroke: '#0d2647', text: '#ffffff', label: 'Seçili' },
  RESERVED: { fill: '#e2e8f0', stroke: '#94a3b8', text: '#475569', label: 'Rezerve edilmiş', pattern: 'pattern-reserved' },
  CLOSED: { fill: '#fee2e2', stroke: '#ef4444', text: '#991b1b', label: 'Kullanıma kapalı', pattern: 'pattern-closed' },
  ACCESSIBLE: { fill: '#f5f3ff', stroke: '#7c3aed', text: '#5b21b6', label: 'Erişilebilirlik için ayrılmış', pattern: 'pattern-accessible' },
  MINE: { fill: '#ccfbf1', stroke: '#0d9488', text: '#115e59', label: 'Sizin rezervasyonunuz' },
  RESTRICTED_BY_RULE: {
    fill: '#fef3c7',
    stroke: '#d97706',
    text: '#92400e',
    label: 'Adil kullanım kuralı nedeniyle seçilemez',
    pattern: 'pattern-restricted',
  },
};

const ZONE_STYLE: Record<string, { fill: string; stroke: string; label: string }> = {
  WORK: { fill: '#f8fafc', stroke: '#cbd5e1', label: '#64748b' },
  MEETING: { fill: '#eff6ff', stroke: '#bfdbfe', label: '#1d4ed8' },
  SOCIAL: { fill: '#f0fdfa', stroke: '#99f6e4', label: '#0f766e' },
  CORRIDOR: { fill: '#f1f5f9', stroke: '#e2e8f0', label: '#94a3b8' },
  CORE: { fill: '#e2e8f0', stroke: '#cbd5e1', label: '#475569' },
  RESTRICTED: { fill: '#fef2f2', stroke: '#fecaca', label: '#b91c1c' },
};

export interface PlanViewBox {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** Masa kodunun krokide gösterilen kısa hali: bina/kat önekini atar. "PT36-A05-01" -> "A05-01". */
export function shortDeskLabel(code: string): string {
  return code.replace(/^[A-Z]{1,3}\d{0,3}-/, '');
}

interface DeskRender {
  desk: DeskView;
  state: string;
  style: DeskStateStyle;
  dimmed: boolean;
  width: number;
  height: number;
  x: number;
  y: number;
  tall: boolean;
  statusText: string | null;
  selected: boolean;
  ariaLabel: string;
}

@Component({
  selector: 'app-floor-plan-svg',
  imports: [PlanElementsLayer],
  template: `
    @let view = viewBoxValue();
    <svg [attr.viewBox]="view.x + ' ' + view.y + ' ' + view.width + ' ' + view.height" class="h-auto w-full select-none" role="group" aria-label="Kat planı">
      <defs>
        <pattern id="pattern-reserved" width="8" height="8" patternTransform="rotate(45)" patternUnits="userSpaceOnUse">
          <rect width="8" height="8" fill="#e2e8f0" />
          <line x1="0" y1="0" x2="0" y2="8" stroke="#94a3b8" stroke-width="3" />
        </pattern>
        <pattern id="pattern-closed" width="8" height="8" patternTransform="rotate(135)" patternUnits="userSpaceOnUse">
          <rect width="8" height="8" fill="#fee2e2" />
          <line x1="0" y1="0" x2="0" y2="8" stroke="#f87171" stroke-width="3" />
        </pattern>
        <pattern id="pattern-accessible" width="8" height="8" patternUnits="userSpaceOnUse">
          <rect width="8" height="8" fill="#f5f3ff" />
          <circle cx="4" cy="4" r="1.6" fill="#a78bfa" />
        </pattern>
        <pattern id="pattern-restricted" width="7" height="7" patternTransform="rotate(45)" patternUnits="userSpaceOnUse">
          <rect width="7" height="7" fill="#fef3c7" />
          <line x1="0" y1="0" x2="0" y2="7" stroke="#fbbf24" stroke-width="2.5" />
        </pattern>
        <pattern id="pattern-window" width="10" height="6" patternUnits="userSpaceOnUse">
          <rect width="10" height="6" fill="#bae6fd" />
          <line x1="0" y1="3" x2="10" y2="3" stroke="#0ea5e9" stroke-width="1.5" />
        </pattern>
      </defs>

      <rect x="0" y="0" [attr.width]="planWidth()" [attr.height]="planHeight()" fill="#ffffff" rx="10" />

      <!-- Fiziksel kroki: admin çizdiyse kroki, çizmediyse bölge dikdörtgenleri gösterilir -->
      @if (hasSketch()) {
        <svg:g planElementsLayer [elements]="elements() ?? []" />
      } @else {
        @for (zone of zones(); track zone.id) {
          @let zs = zoneStyle(zone.kind);
          <svg:g>
            <svg:rect
              [attr.x]="zone.x"
              [attr.y]="zone.y"
              [attr.width]="zone.width"
              [attr.height]="zone.height"
              rx="10"
              [attr.fill]="zs.fill"
              [attr.stroke]="zs.stroke"
              stroke-width="1.5"
              [attr.stroke-dasharray]="zone.kind === 'CORRIDOR' ? '6 5' : null"
            />
            <svg:text [attr.x]="zone.x + 12" [attr.y]="zone.y + 20" [attr.fill]="zs.label" font-size="12" font-weight="600" class="pointer-events-none">{{ zone.name }}</svg:text>
            @if (zone.quiet) {
              <svg:text [attr.x]="zone.x + 12" [attr.y]="zone.y + 36" fill="#0f766e" font-size="10" class="pointer-events-none">Sessiz alan</svg:text>
            }
            @if (zone.kind === 'CORE') {
              <svg:text [attr.x]="zone.x + zone.width / 2" [attr.y]="zone.y + zone.height / 2 + 4" fill="#475569" font-size="11" text-anchor="middle" class="pointer-events-none">Asansör / Merdiven</svg:text>
            }
            @if (zone.windowSide) {
              <svg:rect [attr.x]="zone.x" [attr.y]="zone.y - 7" [attr.width]="zone.width" height="6" rx="3" fill="url(#pattern-window)" />
            }
          </svg:g>
        }
      }

      <!-- Toplantı odaları -->
      @for (room of rooms(); track room.id) {
        <svg:g (click)="roomSelectable() && selectRoom.emit(room.id)" [class.cursor-pointer]="roomSelectable()">
          <svg:rect
            [attr.x]="room.x"
            [attr.y]="room.y"
            [attr.width]="room.width"
            [attr.height]="room.height"
            rx="8"
            [attr.fill]="room.closed ? '#fef2f2' : '#dbeafe'"
            [attr.stroke]="room.closed ? '#f87171' : '#60a5fa'"
            stroke-width="1.5"
          />
          <svg:text [attr.x]="room.x + room.width / 2" [attr.y]="room.y + room.height / 2 - 6" fill="#1e3a8a" font-size="13" font-weight="600" text-anchor="middle">{{ room.name }}</svg:text>
          <svg:text [attr.x]="room.x + room.width / 2" [attr.y]="room.y + room.height / 2 + 12" fill="#1d4ed8" font-size="11" text-anchor="middle">
            {{ room.code }} · {{ room.capacity }} kişi{{ room.closed ? ' · kapalı' : '' }}
          </svg:text>
        </svg:g>
      }

      <!-- Masalar -->
      @for (r of deskRenders(); track r.desk.id) {
        <svg:g
          role="button"
          tabindex="0"
          [attr.aria-label]="r.ariaLabel"
          [attr.aria-pressed]="r.selected"
          (click)="selectDesk.emit(r.desk.id)"
          (keydown.enter)="$event.preventDefault(); selectDesk.emit(r.desk.id)"
          (keydown.space)="$event.preventDefault(); selectDesk.emit(r.desk.id)"
          [class]="cn('cursor-pointer transition-opacity focus:outline-none', r.dimmed && 'opacity-25')"
          [attr.opacity]="r.dimmed ? 0.25 : 1"
          [attr.transform]="r.desk.rotation ? 'rotate(' + r.desk.rotation + ' ' + r.desk.x + ' ' + r.desk.y + ')' : null"
        >
          <title>{{ r.desk.code }} · {{ r.style.label }}</title>
          <!-- Sandalye göstergesi -->
          <svg:rect [attr.x]="r.desk.x - 9" [attr.y]="r.y + r.height + 1" width="18" height="6" rx="3" [attr.fill]="r.style.stroke" opacity="0.5" />
          <svg:rect
            [attr.x]="r.x"
            [attr.y]="r.y"
            [attr.width]="r.width"
            [attr.height]="r.height"
            rx="7"
            [attr.fill]="r.style.pattern ? 'url(#' + r.style.pattern + ')' : r.style.fill"
            [attr.stroke]="r.style.stroke"
            [attr.stroke-width]="r.selected ? 3 : 1.6"
          />
          <!-- Dar ve uzun masalarda (ada ucu) etiket ve durum metni dikey yazılır. -->
          <svg:text
            [attr.x]="r.tall ? r.desk.x - 5 : r.desk.x"
            [attr.y]="r.tall ? r.desk.y : r.desk.y + 1"
            [attr.fill]="r.style.text"
            font-size="10.5"
            font-weight="600"
            text-anchor="middle"
            [attr.dominant-baseline]="r.tall ? 'middle' : null"
            [attr.transform]="r.tall ? 'rotate(-90 ' + (r.desk.x - 5) + ' ' + r.desk.y + ')' : null"
          >
            {{ shortLabel(r.desk.code) }}
          </svg:text>

          <!-- Durum ikonları (renk körlüğü desteği) -->
          @if (r.statusText) {
            <svg:text
              [attr.x]="r.tall ? r.desk.x + 7 : r.desk.x"
              [attr.y]="r.tall ? r.desk.y : r.desk.y + 13"
              font-size="9"
              text-anchor="middle"
              [attr.fill]="r.style.text"
              [attr.dominant-baseline]="r.tall ? 'middle' : null"
              [attr.transform]="r.tall ? 'rotate(-90 ' + (r.desk.x + 7) + ' ' + r.desk.y + ')' : null"
            >
              {{ r.statusText }}
            </svg:text>
          }
          @if (r.desk.accessibleOnly) {
            <svg:text [attr.x]="r.desk.x" [attr.y]="r.desk.y - 9" font-size="10" text-anchor="middle" fill="#6d28d9">♿</svg:text>
          }
          @if (r.desk.popular && !r.desk.accessibleOnly) {
            <svg:text [attr.x]="r.x + r.width - 6" [attr.y]="r.desk.y - 9" font-size="9" text-anchor="middle" fill="#b45309">★</svg:text>
          }
        </svg:g>
      }
    </svg>
  `,
})
export class FloorPlanSvg {
  readonly planWidth = input.required<number>();
  readonly planHeight = input.required<number>();
  readonly zones = input.required<ZoneView[]>();
  /** Admin tarafından çizilen kroki. Boş ise bölge dikdörtgenleri gösterilir. */
  readonly elements = input<PlanElement[] | null | undefined>();
  readonly desks = input.required<DeskView[]>();
  readonly rooms = input.required<RoomView[]>();
  readonly selectedDeskId = input<string | null>(null);
  readonly highlightedDeskIds = input<Set<string> | null | undefined>(null);
  /** Odaya tıklama destekleniyorsa true (çalışan ekranında kapalı). */
  readonly roomSelectable = input(false);
  /** Odaklanılan bölüm (kanat / bölge). Verilmezse tüm kat gösterilir. */
  readonly viewBox = input<PlanViewBox | null | undefined>(null);
  readonly selectDesk = output<string>();
  readonly selectRoom = output<string>();

  readonly cn = cn;
  readonly shortLabel = shortDeskLabel;

  readonly hasSketch = computed(() => (this.elements()?.length ?? 0) > 0);
  readonly viewBoxValue = computed<PlanViewBox>(() => this.viewBox() ?? { x: 0, y: 0, width: this.planWidth(), height: this.planHeight() });

  zoneStyle(kind: string) {
    return ZONE_STYLE[kind] ?? ZONE_STYLE['WORK'];
  }

  readonly deskRenders = computed<DeskRender[]>(() => {
    const selectedId = this.selectedDeskId();
    const highlighted = this.highlightedDeskIds();
    return this.desks().map((desk) => {
      const selected = desk.id === selectedId;
      const state = selected ? 'SELECTED' : desk.state;
      const style = DESK_STATE_STYLE[state] ?? DESK_STATE_STYLE['AVAILABLE'];
      const dimmed = highlighted ? !highlighted.has(desk.id) : false;
      // Masa ölçüleri kat planı editöründen gelir; varsayılan değerler geriye dönük uyumludur.
      const width = desk.width || DESK_WIDTH;
      const height = desk.height || DESK_HEIGHT;
      const statusText =
        desk.state === 'MINE' && !selected ? '✓ siz' : desk.state === 'RESERVED' ? 'dolu' : desk.state === 'CLOSED' ? 'kapalı' : null;
      return {
        desk,
        state,
        style,
        dimmed,
        width,
        height,
        x: desk.x - width / 2,
        y: desk.y - height / 2,
        tall: height > width * 1.3,
        statusText,
        selected,
        ariaLabel: `${desk.code} masası, ${desk.zoneName}, durum: ${style.label}${desk.occupant ? `, kullanan: ${desk.occupant.fullName}` : ''}`,
      };
    });
  });
}

/** Plan göstergesi (legend). */
@Component({
  selector: 'app-floor-plan-legend',
  template: `
    <ul class="flex flex-wrap gap-x-4 gap-y-2">
      @for (item of items; track item.state) {
        @let style = styles[item.state];
        <li class="flex items-center gap-2 text-xs text-ink-600">
          <span
            class="inline-block h-4 w-6 shrink-0 rounded border"
            [style.background-color]="style.fill"
            [style.border-color]="style.stroke"
            [style.background-image]="patternFor(item.state)"
            [style.background-size]="item.state === 'ACCESSIBLE' ? '5px 5px' : null"
            aria-hidden="true"
          ></span>
          {{ item.description }}
        </li>
      }
      <li class="flex items-center gap-2 text-xs text-ink-600">
        <span aria-hidden="true" class="text-amber-600">★</span>
        Popüler / pencere yanı masa
      </li>
    </ul>
  `,
})
export class FloorPlanLegend {
  readonly styles = DESK_STATE_STYLE;
  readonly items = [
    { state: 'AVAILABLE', description: 'Uygun – seçilebilir' },
    { state: 'MINE', description: 'Sizin rezervasyonunuz' },
    { state: 'RESERVED', description: 'Başka bir çalışanda' },
    { state: 'RESTRICTED_BY_RULE', description: 'Adil kullanım kuralı gereği seçilemez' },
    { state: 'ACCESSIBLE', description: 'Erişilebilirlik için ayrılmış' },
    { state: 'CLOSED', description: 'Kullanıma kapalı' },
  ];

  patternFor(state: string): string | null {
    switch (state) {
      case 'RESERVED':
        return 'repeating-linear-gradient(45deg, rgba(100,116,139,.45) 0 3px, transparent 3px 7px)';
      case 'CLOSED':
        return 'repeating-linear-gradient(135deg, rgba(248,113,113,.55) 0 3px, transparent 3px 7px)';
      case 'ACCESSIBLE':
        return 'radial-gradient(circle, rgba(167,139,250,.9) 1px, transparent 1.6px)';
      case 'RESTRICTED_BY_RULE':
        return 'repeating-linear-gradient(45deg, rgba(251,191,36,.6) 0 2px, transparent 2px 6px)';
      default:
        return null;
    }
  }
}
