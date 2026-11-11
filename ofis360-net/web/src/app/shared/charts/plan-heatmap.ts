import { Component, computed, input, signal } from '@angular/core';
import type { PlanElement } from '../../lib/plan';
import { RESOURCE_SIZES } from '../../lib/plan';
import { cn } from '../../lib/utils';
import { PlanElementsLayer } from '../floor-plan/plan-shapes';
import { UiIcon } from '../ui';
import { SEQUENTIAL, UiDeskHeatmap, sequentialColor, type HeatmapCell } from './chart-kit';

/**
 * Kat planı üzerinde masa kullanım ısı haritası (kaynak: components/charts/plan-heatmap.tsx).
 * Çalışan ekranıyla aynı koordinat sistemini ve aynı kroki katmanını kullanır.
 * Değer yalnızca renkle değil, her hücredeki sayıyla da gösterilir.
 */

export interface HeatmapFloor {
  id: string;
  name: string;
  planWidth: number;
  planHeight: number;
  elements: PlanElement[];
  desks: Array<{ id: string; code: string; x: number; y: number; width: number; height: number; rotation: number }>;
}

@Component({
  selector: 'ui-plan-heatmap',
  imports: [UiIcon, PlanElementsLayer, UiDeskHeatmap],
  template: `
    <div class="mb-4 flex flex-wrap items-start justify-between gap-3">
      <div>
        <h3 class="text-[15px] font-semibold text-ink-900">Masa kullanım ısı haritası</h3>
        <p class="mt-0.5 text-[13px] text-ink-500">
          Seçili aralıkta her masanın kaç kez rezerve edildiği. Kat planı görünümü, çalışanların gördüğü krokinin aynısıdır.
        </p>
      </div>
      <div class="flex items-center gap-1 rounded-lg border border-line bg-slate-50 p-0.5">
        <button
          type="button"
          (click)="view.set('plan')"
          [attr.aria-pressed]="view() === 'plan'"
          [class]="cn('flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-[13px] font-medium transition-colors', view() === 'plan' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')"
        >
          <svg uiIcon name="Map" class="h-4 w-4"></svg>
          Kat planı
        </button>
        <button
          type="button"
          (click)="view.set('grid')"
          [attr.aria-pressed]="view() === 'grid'"
          [class]="cn('flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-[13px] font-medium transition-colors', view() === 'grid' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')"
        >
          <svg uiIcon name="LayoutGrid" class="h-4 w-4"></svg>
          Liste
        </button>
      </div>
    </div>

    @if (view() === 'grid') {
      <ui-desk-heatmap bare title="Masa kullanım listesi" [cells]="cells()" />
    } @else {
      <div class="space-y-3">
        <div class="flex flex-wrap gap-1.5">
          @for (item of floors(); track item.id; let index = $index) {
            <button
              type="button"
              (click)="floorIndex.set(index)"
              [attr.aria-pressed]="index === floorIndex()"
              [class]="cn('rounded-lg border px-3 py-1.5 text-[13px] font-medium transition-colors', index === floorIndex() ? 'border-brand-500 bg-brand-600 text-white' : 'border-line bg-white text-ink-700 hover:border-brand-200 hover:bg-brand-50')"
            >
              {{ item.name }}
            </button>
          }
        </div>

        @if (floor(); as f) {
          <div class="overflow-x-auto scrollbar-thin" tabindex="0" role="region" aria-label="Kat planı yoğunluk haritası">
            <div class="min-w-[680px]">
              <svg [attr.viewBox]="'0 0 ' + f.planWidth + ' ' + f.planHeight" class="h-auto w-full" role="img" [attr.aria-label]="f.name + ' masa kullanım ısı haritası'">
                <rect x="0" y="0" [attr.width]="f.planWidth" [attr.height]="f.planHeight" fill="#ffffff" rx="10" />
                <svg:g planElementsLayer [elements]="f.elements" [muted]="true" />
                @for (desk of f.desks; track desk.id) {
                  @let value = counts()[desk.id] ?? 0;
                  @let intensity = value / max();
                  @let w = desk.width || deskSize.width;
                  @let h = desk.height || deskSize.height;
                  <svg:g [attr.transform]="desk.rotation ? 'rotate(' + desk.rotation + ' ' + desk.x + ' ' + desk.y + ')' : null">
                    <title>{{ desk.code }}: {{ value }} rezervasyon</title>
                    <svg:rect
                      [attr.x]="desk.x - w / 2"
                      [attr.y]="desk.y - h / 2"
                      [attr.width]="w"
                      [attr.height]="h"
                      rx="7"
                      [attr.fill]="color(value)"
                      [attr.stroke]="intensity > 0.6 ? '#164277' : '#94a3b8'"
                      stroke-width="1.4"
                    />
                    <svg:text [attr.x]="desk.x" [attr.y]="desk.y - 1" text-anchor="middle" font-size="9" [attr.fill]="intensity > 0.55 ? '#ffffff' : '#475569'">{{ shortCode(desk.code) }}</svg:text>
                    <svg:text [attr.x]="desk.x" [attr.y]="desk.y + 10" text-anchor="middle" font-size="10" font-weight="600" [attr.fill]="intensity > 0.55 ? '#ffffff' : '#1e293b'">{{ value }}</svg:text>
                  </svg:g>
                }
              </svg>
            </div>
          </div>
        } @else {
          <p class="py-8 text-center text-sm text-ink-500">Gösterilecek kat bulunamadı.</p>
        }

        <div class="flex items-center gap-2 border-t border-line pt-3">
          <span class="text-xs text-ink-500">Az kullanılan</span>
          <div class="flex">
            @for (c of palette; track c) {
              <span class="h-3 w-6 first:rounded-l last:rounded-r" [style.background-color]="c" aria-hidden="true"></span>
            }
          </div>
          <span class="text-xs text-ink-500">Çok kullanılan (en fazla {{ max() }})</span>
        </div>
      </div>
    }
  `,
  host: { class: 'card-surface block p-5' },
})
export class UiPlanHeatmap {
  readonly floors = input.required<HeatmapFloor[]>();
  readonly counts = input.required<Record<string, number>>();
  /** Liste görünümü için hücreler (DeskHeatmap bare). */
  readonly cells = input.required<HeatmapCell[]>();

  readonly cn = cn;
  readonly palette = SEQUENTIAL;
  readonly deskSize = RESOURCE_SIZES.desk;
  readonly view = signal<'plan' | 'grid'>('plan');
  readonly floorIndex = signal(0);

  readonly max = computed(() => Math.max(1, ...Object.values(this.counts())));
  readonly floor = computed(() => this.floors()[this.floorIndex()] ?? null);

  color(value: number): string {
    return sequentialColor(value, this.max());
  }

  shortCode(code: string): string {
    return code.replace(/^F\d+-/, '');
  }
}
