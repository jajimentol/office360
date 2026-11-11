import { Component, computed, input } from '@angular/core';
import { PLAN_ELEMENT_DEFINITIONS, type PlanElement } from '../../lib/plan';

/**
 * Kroki öğelerinin SVG gösterimi (kaynak: components/floor-plan/plan-shapes.tsx).
 * Hem admin editöründe hem çalışan/yönetici ekranlarındaki salt okunur planda aynı bileşen kullanılır.
 */

export interface ShapeStyle {
  fill: string;
  stroke: string;
  strokeWidth: number;
  fontSize: number;
}

export function stylesFor(element: PlanElement): ShapeStyle {
  const defaults = PLAN_ELEMENT_DEFINITIONS[element.type]?.defaultStyle;
  return {
    fill: element.style.fill ?? defaults?.fill ?? '#f8fafc',
    stroke: element.style.stroke ?? defaults?.stroke ?? '#cbd5e1',
    strokeWidth: element.style.strokeWidth ?? defaults?.strokeWidth ?? 1.5,
    fontSize: element.style.fontSize ?? defaults?.fontSize ?? 12,
  };
}

const LABELLED_TYPES = ['ROOM', 'CORRIDOR', 'KITCHEN', 'WC', 'RESTRICTED', 'ELEVATOR', 'STAIRS', 'TERRACE'];

@Component({
  selector: 'g[planElementShape]',
  template: `
    @let e = element();
    @let s = style();
    @if (e.visible) {
      <svg:g [attr.role]="ariaLabel() ? 'img' : null" [attr.transform]="transform()" [attr.opacity]="muted() ? 0.55 : 1" [attr.aria-label]="ariaLabel()">
        @if (e.type === 'TEXT') {
          <svg:text
            [attr.x]="e.x"
            [attr.y]="e.y + e.height / 2 + s.fontSize / 3"
            [attr.font-size]="s.fontSize"
            font-weight="600"
            [attr.fill]="s.stroke === 'transparent' ? '#334155' : s.stroke"
          >
            {{ e.label ?? 'Etiket' }}
          </svg:text>
        } @else {
          <svg:rect
            [attr.x]="e.x"
            [attr.y]="e.y"
            [attr.width]="e.width"
            [attr.height]="e.height"
            [attr.rx]="e.type === 'WALL' || e.type === 'WINDOW' || e.type === 'DOOR' ? 2 : 8"
            [attr.fill]="s.fill"
            [attr.stroke]="s.stroke"
            [attr.stroke-width]="s.strokeWidth"
          />
        }

        <!-- Öğe türüne göre ek detay çizimleri (merdiven basamağı, asansör oku, kapı yayı…) -->
        @switch (e.type) {
          @case ('STAIRS') {
            @for (stepY of stairSteps(); track $index) {
              <svg:line [attr.x1]="e.x + 4" [attr.y1]="stepY" [attr.x2]="e.x + e.width - 4" [attr.y2]="stepY" [attr.stroke]="s.stroke" stroke-width="1" opacity="0.7" />
            }
          }
          @case ('ELEVATOR') {
            <svg:line [attr.x1]="e.x + 6" [attr.y1]="centerY()" [attr.x2]="e.x + e.width - 6" [attr.y2]="centerY()" [attr.stroke]="s.stroke" stroke-width="1" />
            <svg:path [attr.d]="elevatorPath()" [attr.fill]="s.stroke" opacity="0.75" />
          }
          @case ('DOOR') {
            <svg:path [attr.d]="doorPath()" fill="none" [attr.stroke]="s.stroke" stroke-width="1.2" stroke-dasharray="3 3" />
          }
          @case ('WC') {
            <svg:text [attr.x]="centerX()" [attr.y]="centerY() + 5" text-anchor="middle" font-size="14" font-weight="600" fill="#4f46e5">WC</svg:text>
          }
          @case ('RESTRICTED') {
            <svg:path [attr.d]="'M ' + e.x + ' ' + (e.y + e.height) + ' L ' + (e.x + e.width) + ' ' + e.y" stroke="#fca5a5" stroke-width="1.5" stroke-dasharray="6 4" opacity="0.8" />
          }
          @case ('TERRACE') {
            @for (dot of terraceDots(); track $index) {
              <svg:circle [attr.cx]="dot.cx" [attr.cy]="dot.cy" r="5" fill="#4ade80" opacity="0.55" />
            }
          }
        }

        @if (showLabel()) {
          <svg:text [attr.x]="e.x + 10" [attr.y]="e.y + 18" font-size="11" font-weight="600" fill="#64748b" class="pointer-events-none">{{ e.label }}</svg:text>
        }
      </svg:g>
    }
  `,
})
export class PlanElementShape {
  readonly element = input.required<PlanElement>();
  readonly muted = input(false);

  readonly style = computed(() => stylesFor(this.element()));
  readonly centerX = computed(() => this.element().x + this.element().width / 2);
  readonly centerY = computed(() => this.element().y + this.element().height / 2);
  readonly transform = computed(() => {
    const e = this.element();
    return e.rotation ? `rotate(${e.rotation} ${this.centerX()} ${this.centerY()})` : null;
  });
  readonly ariaLabel = computed(() => {
    const e = this.element();
    const definition = PLAN_ELEMENT_DEFINITIONS[e.type];
    return `${definition?.label ?? e.type}${e.label ? `: ${e.label}` : ''}`;
  });
  readonly showLabel = computed(() => {
    const e = this.element();
    return Boolean(e.label) && e.type !== 'TEXT' && e.width > 60 && e.height > 24 && LABELLED_TYPES.includes(e.type);
  });
  readonly stairSteps = computed(() => {
    const { y, height } = this.element();
    const steps = Math.max(3, Math.min(8, Math.floor(height / 18)));
    return Array.from({ length: steps - 1 }, (_, index) => y + ((index + 1) * height) / steps);
  });
  readonly elevatorPath = computed(() => {
    const cx = this.centerX();
    const cy = this.centerY();
    return `M ${cx} ${cy - 16} l -6 8 h 12 z M ${cx} ${cy + 16} l -6 -8 h 12 z`;
  });
  readonly doorPath = computed(() => {
    const { x, y, width, height } = this.element();
    const radius = Math.min(width, 34);
    return `M ${x} ${y + height} a ${radius} ${radius} 0 0 1 ${radius} ${-radius}`;
  });
  readonly terraceDots = computed(() => {
    const { x, y, width, height } = this.element();
    const step = 44;
    const dots: Array<{ cx: number; cy: number }> = [];
    if (width >= 60 && height >= 36) {
      const cols = Math.max(1, Math.floor((width - 24) / step));
      const rows = Math.max(1, Math.floor((height - 24) / step));
      for (let r = 0; r < rows; r += 1) {
        for (let c = 0; c < cols; c += 1) {
          dots.push({
            cx: x + 22 + c * step + (width - 24 - (cols - 1) * step - 20) / 2,
            cy: y + 22 + r * step + (height - 24 - (rows - 1) * step - 20) / 2,
          });
        }
      }
    }
    return dots;
  });
}

/** Krokinin tamamını katman sırasına göre çizer. */
@Component({
  selector: 'g[planElementsLayer]',
  imports: [PlanElementShape],
  template: `
    @for (element of ordered(); track element.id) {
      <svg:g planElementShape [element]="element" [muted]="muted()" />
    }
  `,
})
export class PlanElementsLayer {
  readonly elements = input.required<PlanElement[]>();
  readonly muted = input(false);
  readonly ordered = computed(() => [...this.elements()].sort((a, b) => a.layerOrder - b.layerOrder));
}
