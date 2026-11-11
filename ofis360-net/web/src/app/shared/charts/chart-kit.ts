import { Component, DestroyRef, Directive, ElementRef, afterNextRender, computed, effect, inject, input, model, signal, viewChild } from '@angular/core';
import { Chart, registerables, type ChartConfiguration, type TooltipItem } from 'chart.js';
import { cn } from '../../lib/utils';
import { UiIcon, booleanAttribute } from '../ui';

/**
 * Ofis360 grafik seti (kaynak: components/charts/chart-kit.tsx; recharts → Chart.js).
 *
 * Renk paleti CVD (renk körlüğü) ayrımı, açıklık bandı, kroma tabanı ve yüzey kontrastı kontrollerinden
 * geçirilmiştir. Kimlik yalnızca renkle taşınmaz: her grafikte açıklama (legend) ve tablo görünümü bulunur.
 */
Chart.register(...registerables);

export const CATEGORICAL = [
  '#1f5493', // lacivert
  '#0d9488', // turkuaz
  '#7c3aed', // mor
  '#b45309', // kehribar
  '#0284c7', // gök mavisi
  '#dc2626', // kırmızı
] as const;

/** Tek hue, açıktan koyuya - büyüklük (magnitude) kodlaması için. */
export const SEQUENTIAL = ['#eef4fb', '#d7e6f6', '#b0cdec', '#7fabdd', '#4c85c9', '#2f6db5', '#1f5493', '#164277'] as const;

const AXIS_COLOR = '#64748b';
const GRID_COLOR = '#e2e8f0';
const FONT_FAMILY = "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Helvetica Neue', Arial, sans-serif";

export interface ValueFormat {
  style?: 'percent' | 'number';
  suffix?: string;
}

export function formatValue(value: number, format?: ValueFormat): string {
  if (format?.style === 'percent') return `%${value}`;
  return format?.suffix ? `${value} ${format.suffix}` : String(value);
}

export interface SeriesConfig {
  key: string;
  label: string;
  color?: string;
}

export type ChartRow = Record<string, string | number>;

function tooltipOptions(valueFormat?: ValueFormat) {
  return {
    backgroundColor: '#ffffff',
    titleColor: '#0f172a',
    bodyColor: '#334155',
    borderColor: GRID_COLOR,
    borderWidth: 1,
    padding: 10,
    titleFont: { family: FONT_FAMILY, size: 12, weight: 600 as const },
    bodyFont: { family: FONT_FAMILY, size: 12 },
    boxPadding: 4,
    callbacks: {
      label: (context: TooltipItem<'bar' | 'line'>) => {
        const horizontal = (context.chart.options as { indexAxis?: string }).indexAxis === 'y';
        const parsed = context.parsed as { x: number | null; y: number | null };
        const raw = (horizontal ? parsed.x : parsed.y) ?? 0;
        return ` ${context.dataset.label ?? ''}: ${formatValue(raw, valueFormat)}`;
      },
    },
  };
}

const AXIS_TICKS = { color: AXIS_COLOR, font: { family: FONT_FAMILY, size: 11 } };

/**
 * Grafik çerçevesi: başlık, açıklama, seri göstergesi ve tablo görünümü geçişi.
 * Tablo görünümü erişilebilirlik için her grafikte bulunur.
 */
@Component({
  selector: 'ui-chart-frame',
  imports: [UiIcon],
  template: `
    <div class="mb-4 flex items-start justify-between gap-3">
      <div>
        <h3 class="text-[15px] font-semibold text-ink-900">{{ title() }}</h3>
        @if (description()) {
          <p class="mt-0.5 text-[13px] text-ink-500">{{ description() }}</p>
        }
      </div>
      <button
        type="button"
        (click)="showTable.set(!showTable())"
        [attr.aria-pressed]="showTable()"
        class="flex shrink-0 items-center gap-1.5 rounded-lg border border-line px-2.5 py-1.5 text-xs font-medium text-ink-600 transition-colors hover:bg-slate-50"
      >
        @if (showTable()) {
          <svg uiIcon name="ChartColumn" class="h-3.5 w-3.5"></svg>
          Grafik
        } @else {
          <svg uiIcon name="Table2" class="h-3.5 w-3.5"></svg>
          Tablo
        }
      </button>
    </div>

    @if (series().length > 1) {
      <ul class="mb-3 flex flex-wrap gap-x-4 gap-y-1.5">
        @for (item of series(); track item.key; let index = $index) {
          <li class="flex items-center gap-1.5 text-xs text-ink-600">
            <span class="inline-block h-2.5 w-2.5 rounded-sm" [style.background-color]="item.color ?? palette[index % palette.length]" aria-hidden="true"></span>
            {{ item.label }}
          </li>
        }
      </ul>
    }

    @if (showTable()) {
      <div class="max-h-[280px] overflow-auto scrollbar-thin rounded-lg border border-line">
        <table class="w-full text-sm">
          <thead class="sticky top-0 bg-slate-50">
            <tr>
              <th scope="col" class="px-3 py-2 text-left text-xs font-semibold text-ink-500">{{ labelKey() === 'label' ? 'Dönem' : 'Kategori' }}</th>
              @for (item of series(); track item.key) {
                <th scope="col" class="px-3 py-2 text-right text-xs font-semibold text-ink-500">{{ item.label }}</th>
              }
            </tr>
          </thead>
          <tbody class="divide-y divide-line">
            @for (row of data(); track $index) {
              <tr>
                <td class="px-3 py-1.5 text-ink-700">{{ row[labelKey()] }}</td>
                @for (item of series(); track item.key) {
                  <td class="px-3 py-1.5 text-right font-medium text-ink-900">{{ cell(row, item.key) }}</td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
    <div [class.hidden]="showTable()" [style.height.px]="height()"><ng-content /></div>
  `,
  host: { class: 'card-surface block p-5' },
})
export class UiChartFrame {
  readonly title = input.required<string>();
  readonly description = input<string | undefined>();
  readonly series = input.required<SeriesConfig[]>();
  readonly data = input.required<ChartRow[]>();
  readonly labelKey = input('label');
  readonly valueFormat = input<ValueFormat | undefined>();
  readonly height = input(260);
  readonly showTable = model(false);
  readonly palette = CATEGORICAL;

  cell(row: ChartRow, key: string): string | number {
    const value = row[key];
    return typeof value === 'number' ? formatValue(value, this.valueFormat()) : value;
  }
}

/** Chart.js örneğini canvas'a bağlayan ortak taban. */
@Directive()
abstract class ChartHost {
  readonly data = input.required<ChartRow[]>();
  readonly series = input.required<SeriesConfig[]>();
  readonly labelKey = input('label');
  readonly valueFormat = input<ValueFormat | undefined>();
  readonly height = input(260);
  readonly title = input.required<string>();
  readonly description = input<string | undefined>();
  readonly showTable = signal(false);

  protected readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private chart: Chart | null = null;
  private ready = false;

  constructor() {
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      this.ready = true;
      this.render();
    });
    effect(() => {
      this.data();
      this.series();
      this.labelKey();
      this.valueFormat();
      if (this.ready) this.render();
    });
    effect(() => {
      // Tablo görünümünden grafiğe dönünce konteyner yeniden ölçülür.
      if (!this.showTable() && this.ready) setTimeout(() => this.chart?.resize(), 0);
    });
    destroyRef.onDestroy(() => this.chart?.destroy());
  }

  protected abstract config(): ChartConfiguration;

  private render(): void {
    this.chart?.destroy();
    this.chart = new Chart(this.canvas().nativeElement, this.config());
  }
}

/** Zaman serisi çizgi grafiği. */
@Component({
  selector: 'ui-trend-chart',
  imports: [UiChartFrame],
  template: `
    <ui-chart-frame [title]="title()" [description]="description()" [series]="series()" [data]="data()" [labelKey]="labelKey()" [valueFormat]="valueFormat()" [height]="height()" [(showTable)]="showTable">
      <canvas #canvas role="img" [attr.aria-label]="title()"></canvas>
    </ui-chart-frame>
  `,
  host: { class: 'block' },
})
export class UiTrendChart extends ChartHost {
  protected config(): ChartConfiguration {
    const rows = this.data();
    const labelKey = this.labelKey();
    return {
      type: 'line',
      data: {
        labels: rows.map((row) => String(row[labelKey])),
        datasets: this.series().map((item, index) => ({
          label: item.label,
          data: rows.map((row) => Number(row[item.key] ?? 0)),
          borderColor: item.color ?? CATEGORICAL[index % CATEGORICAL.length],
          backgroundColor: item.color ?? CATEGORICAL[index % CATEGORICAL.length],
          borderWidth: 2,
          pointRadius: 0,
          pointHoverRadius: 4,
          pointHoverBorderWidth: 2,
          pointHoverBorderColor: '#ffffff',
          tension: 0.35,
        })),
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { display: false }, tooltip: tooltipOptions(this.valueFormat()) },
        scales: {
          x: { grid: { display: false }, border: { color: GRID_COLOR }, ticks: { ...AXIS_TICKS, maxRotation: 0, autoSkip: true, autoSkipPadding: 18 } },
          y: { beginAtZero: true, grid: { color: GRID_COLOR, tickBorderDash: [3, 3] }, border: { display: false }, ticks: AXIS_TICKS },
        },
      },
    };
  }
}

/** Kategorik karşılaştırma - dikey veya yatay çubuk. */
@Component({
  selector: 'ui-comparison-chart',
  imports: [UiChartFrame],
  template: `
    <ui-chart-frame [title]="title()" [description]="description()" [series]="series()" [data]="data()" [labelKey]="labelKey()" [valueFormat]="valueFormat()" [height]="height()" [(showTable)]="showTable">
      <canvas #canvas role="img" [attr.aria-label]="title()"></canvas>
    </ui-chart-frame>
  `,
  host: { class: 'block' },
})
export class UiComparisonChart extends ChartHost {
  readonly layout = input<'vertical' | 'horizontal'>('vertical');
  readonly colorByIndex = input(false);

  protected config(): ChartConfiguration {
    const rows = this.data();
    const labelKey = this.labelKey();
    const horizontal = this.layout() === 'horizontal';
    const valueAxis = { beginAtZero: true, grid: { color: GRID_COLOR, tickBorderDash: [3, 3] }, border: { display: false }, ticks: AXIS_TICKS };
    const categoryAxis = { grid: { display: false }, border: { color: horizontal ? 'transparent' : GRID_COLOR }, ticks: { ...AXIS_TICKS, maxRotation: 0, autoSkip: !horizontal } };
    return {
      type: 'bar',
      data: {
        labels: rows.map((row) => String(row[labelKey])),
        datasets: this.series().map((item, index) => {
          const base = item.color ?? CATEGORICAL[index % CATEGORICAL.length];
          return {
            label: item.label,
            data: rows.map((row) => Number(row[item.key] ?? 0)),
            backgroundColor: this.colorByIndex() ? rows.map((_, cellIndex) => CATEGORICAL[cellIndex % CATEGORICAL.length]) : base,
            borderRadius: 4,
            maxBarThickness: horizontal ? 18 : 34,
          };
        }),
      },
      options: {
        indexAxis: horizontal ? 'y' : 'x',
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false }, tooltip: tooltipOptions(this.valueFormat()) },
        scales: horizontal ? { x: valueAxis, y: categoryAxis } : { x: categoryAxis, y: valueAxis },
      },
    };
  }
}

export interface HeatmapCell {
  deskId: string;
  code: string;
  floorNumber: number;
  zoneName: string;
  reservations: number;
  popular: boolean;
}

export function sequentialColor(value: number, max: number): string {
  const index = Math.min(SEQUENTIAL.length - 1, Math.round((value / max) * (SEQUENTIAL.length - 1)));
  return SEQUENTIAL[index];
}

/**
 * Masa kullanım ısı haritası (liste biçimi).
 * Sıralı (sequential) tek hue ramp kullanır; her hücrede sayı görünür, böylece bilgi yalnızca renkle taşınmaz.
 */
@Component({
  selector: 'ui-desk-heatmap',
  template: `
    @if (!bare()) {
      <h3 class="text-[15px] font-semibold text-ink-900">{{ title() }}</h3>
      @if (description()) {
        <p class="mt-0.5 text-[13px] text-ink-500">{{ description() }}</p>
      }
    }
    <div [class]="cn('space-y-4', bare() ? '' : 'mt-4')">
      @for (group of byFloor(); track group.floorNumber) {
        <div>
          <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-400">{{ group.floorNumber }}. Kat</p>
          <div class="flex flex-wrap gap-1.5">
            @for (cell of group.cells; track cell.deskId) {
              @let intensity = cell.reservations / max();
              <div
                [title]="cell.code + ' · ' + cell.zoneName + ' · ' + cell.reservations + ' rezervasyon'"
                class="flex h-12 w-[58px] flex-col items-center justify-center rounded-lg border text-[10px]"
                [style.background-color]="color(cell.reservations)"
                [style.border-color]="intensity > 0.6 ? '#164277' : '#cbd5e1'"
                [style.color]="intensity > 0.55 ? '#ffffff' : '#334155'"
              >
                <span class="font-semibold">{{ shortCode(cell.code) }}</span>
                <span class="tabular-nums">{{ cell.reservations }}</span>
              </div>
            }
          </div>
        </div>
      }
    </div>

    <div class="mt-4 flex items-center gap-2 border-t border-line pt-3">
      <span class="text-xs text-ink-500">Az kullanılan</span>
      <div class="flex">
        @for (color of palette; track color) {
          <span class="h-3 w-6 first:rounded-l last:rounded-r" [style.background-color]="color" aria-hidden="true"></span>
        }
      </div>
      <span class="text-xs text-ink-500">Çok kullanılan (en fazla {{ max() }})</span>
    </div>
  `,
  host: { '[class]': 'hostClass()' },
})
export class UiDeskHeatmap {
  readonly title = input.required<string>();
  readonly description = input<string | undefined>();
  readonly cells = input.required<HeatmapCell[]>();
  /** true ise kart çerçevesi ve başlık çizilmez (başka bir kartın içinde kullanım). */
  readonly bare = input(false, { transform: booleanAttribute });
  readonly cn = cn;
  readonly palette = SEQUENTIAL;

  readonly hostClass = computed(() => (this.bare() ? 'block' : 'card-surface block p-5'));
  readonly max = computed(() => Math.max(1, ...this.cells().map((cell) => cell.reservations)));
  readonly byFloor = computed(() => {
    const groups = new Map<number, HeatmapCell[]>();
    for (const cell of this.cells()) {
      const bucket = groups.get(cell.floorNumber);
      if (bucket) bucket.push(cell);
      else groups.set(cell.floorNumber, [cell]);
    }
    return [...groups.entries()]
      .sort((a, b) => a[0] - b[0])
      .map(([floorNumber, cells]) => ({ floorNumber, cells: [...cells].sort((a, b) => a.code.localeCompare(b.code, 'tr')) }));
  });

  color(value: number): string {
    return sequentialColor(value, this.max());
  }

  shortCode(code: string): string {
    return code.replace(/^F\d-/, '');
  }
}
