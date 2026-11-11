import { Component, Directive, computed, input } from '@angular/core';
import { cn } from '../../lib/utils';

/** Kart, başlık, tablo ve boş durum bileşenleri (kaynak: components/ui/layout.tsx). */

@Directive({
  selector: '[ui-card]',
  host: { class: 'card-surface block' },
})
export class UiCard {}

/**
 * `<ui-card-header title="..." description="...">` — ikon ve eylem alanı projeksiyonla verilir:
 * `<svg uiIcon icon ...>` ve `<ng-container action>...</ng-container>`.
 */
@Component({
  selector: 'ui-card-header',
  template: `
    <div class="flex items-start gap-3">
      @if (hasIcon()) {
        <span class="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand-600">
          <ng-content select="[icon]" />
        </span>
      }
      <div>
        <h2 class="text-[15px] font-semibold text-ink-900">{{ title() }}<ng-content select="[title-extra]" /></h2>
        @if (description()) {
          <p class="mt-0.5 text-sm text-ink-500">{{ description() }}</p>
        }
      </div>
    </div>
    <div class="flex shrink-0 items-center gap-2 empty:hidden">
      <ng-content select="[action]" />
    </div>
  `,
  host: {
    class: 'flex flex-col gap-3 border-b border-line px-5 py-4 sm:flex-row sm:items-center sm:justify-between',
  },
})
export class UiCardHeader {
  readonly title = input.required<string>();
  readonly description = input<string | null | undefined>();
  readonly hasIcon = input(false, { transform: (v: unknown) => v !== false && v !== null && v !== undefined && v !== 'false' });
}

@Directive({
  selector: 'ui-card-body, [ui-card-body]',
  host: { class: 'block px-5 py-4' },
})
export class UiCardBody {}

@Component({
  selector: 'ui-page-header',
  template: `
    <div class="mb-2 text-xs text-ink-500 empty:hidden"><ng-content select="[breadcrumb]" /></div>
    <div class="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h1 class="text-2xl font-semibold tracking-tight text-ink-900 sm:text-[28px]">{{ title() }}</h1>
        @if (description()) {
          <p class="mt-1.5 max-w-2xl text-sm text-ink-500 text-balance">{{ description() }}</p>
        }
      </div>
      <div class="flex flex-wrap items-center gap-2 empty:hidden"><ng-content select="[actions]" /></div>
    </div>
  `,
  host: { class: 'mb-6 block' },
})
export class UiPageHeader {
  readonly title = input.required<string>();
  readonly description = input<string | null | undefined>();
}

@Component({
  selector: 'ui-empty-state',
  template: `
    @if (hasIcon()) {
      <span class="mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-ink-400">
        <ng-content select="[icon]" />
      </span>
    }
    <p class="text-sm font-semibold text-ink-900">{{ title() }}</p>
    @if (description()) {
      <p class="mt-1 max-w-sm text-sm text-ink-500">{{ description() }}</p>
    }
    <div class="mt-4 empty:hidden"><ng-content select="[action]" /></div>
  `,
  host: { class: 'flex flex-col items-center justify-center px-6 py-12 text-center' },
})
export class UiEmptyState {
  readonly title = input.required<string>();
  readonly description = input<string | null | undefined>();
  readonly hasIcon = input(false, { transform: (v: unknown) => v !== false && v !== null && v !== undefined && v !== 'false' });
}

export type AlertTone = 'info' | 'success' | 'warning' | 'danger';

@Component({
  selector: 'ui-alert',
  template: `
    <div class="flex gap-3">
      @if (hasIcon()) {
        <span class="mt-0.5 shrink-0"><ng-content select="[icon]" /></span>
      }
      <div class="min-w-0 flex-1">
        @if (title()) {
          <p class="font-semibold">{{ title() }}</p>
        }
        <div [class]="cn(title() && 'mt-1', 'text-[13px] leading-relaxed')"><ng-content /></div>
      </div>
    </div>
  `,
  host: {
    '[class]': 'classes()',
    '[attr.role]': 'tone() === "danger" ? "alert" : "status"',
  },
})
export class UiAlert {
  readonly tone = input<AlertTone>('info');
  readonly title = input<string | null | undefined>();
  readonly hasIcon = input(false, { transform: (v: unknown) => v !== false && v !== null && v !== undefined && v !== 'false' });
  readonly cn = cn;
  readonly classes = computed(() =>
    cn(
      'block rounded-xl border px-4 py-3 text-sm',
      {
        info: 'bg-brand-50 border-brand-200 text-brand-900',
        success: 'bg-emerald-50 border-emerald-200 text-emerald-900',
        warning: 'bg-amber-50 border-amber-200 text-amber-900',
        danger: 'bg-red-50 border-red-200 text-red-900',
      }[this.tone()],
    ),
  );
}

export type StatTone = 'brand' | 'success' | 'warning' | 'danger' | 'accent';

@Component({
  selector: 'ui-stat-card',
  template: `
    <div class="flex items-start justify-between gap-3">
      <p class="text-sm font-medium text-ink-500">{{ label() }}</p>
      @if (hasIcon()) {
        <span [class]="iconClass()"><ng-content select="[icon]" /></span>
      }
    </div>
    <p class="mt-3 text-[26px] font-semibold leading-none tracking-tight text-ink-900">{{ value() }}</p>
    <div class="mt-2 flex items-center gap-2">
      @if (trend(); as t) {
        <span [class]="cn('text-xs font-medium', t.positive ? 'text-emerald-600' : 'text-red-600')">{{ t.value }}</span>
      }
      @if (hint()) {
        <p class="text-xs text-ink-500">{{ hint() }}</p>
      }
    </div>
  `,
  host: { class: 'card-surface block p-5' },
})
export class UiStatCard {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly hint = input<string | null | undefined>();
  readonly tone = input<StatTone>('brand');
  readonly trend = input<{ value: string; positive: boolean } | null | undefined>();
  readonly hasIcon = input(false, { transform: (v: unknown) => v !== false && v !== null && v !== undefined && v !== 'false' });
  readonly cn = cn;
  readonly iconClass = computed(() =>
    cn(
      'flex h-9 w-9 items-center justify-center rounded-lg',
      {
        brand: 'bg-brand-50 text-brand-600',
        success: 'bg-emerald-50 text-emerald-600',
        warning: 'bg-amber-50 text-amber-600',
        danger: 'bg-red-50 text-red-600',
        accent: 'bg-teal-soft text-teal-800',
      }[this.tone()],
    ),
  );
}

export interface TableHeader {
  label: string;
  className?: string;
}

/** `<ui-data-table [headers]="..." caption="..."><tr>...</tr></ui-data-table>` */
@Component({
  selector: 'ui-data-table',
  template: `
    <table class="w-full min-w-[640px] border-collapse text-sm">
      @if (caption()) {
        <caption class="sr-only">{{ caption() }}</caption>
      }
      <thead>
        <tr class="border-b border-line bg-slate-50/60">
          @for (header of headers(); track $index) {
            <th scope="col" [class]="cn('px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-ink-500', header.className)">
              {{ header.label }}
            </th>
          }
        </tr>
      </thead>
      <tbody class="divide-y divide-line"><ng-content /></tbody>
    </table>
  `,
  host: { class: 'block overflow-x-auto scrollbar-thin', tabindex: '0', role: 'region', '[attr.aria-label]': "caption() || 'Tablo'" },
})
export class UiDataTable {
  readonly headers = input.required<TableHeader[]>();
  readonly caption = input<string | undefined>();
  readonly cn = cn;
}

@Directive({
  selector: 'td[ui-td]',
  host: { class: 'px-4 py-3 align-middle text-ink-700' },
})
export class UiTd {}

@Component({
  selector: 'ui-section-title',
  template: `
    <div>
      <h2 class="text-base font-semibold text-ink-900">{{ title() }}</h2>
      @if (description()) {
        <p class="mt-0.5 text-sm text-ink-500">{{ description() }}</p>
      }
    </div>
    <ng-content select="[action]" />
  `,
  host: { class: 'mb-3 flex items-end justify-between gap-3' },
})
export class UiSectionTitle {
  readonly title = input.required<string>();
  readonly description = input<string | null | undefined>();
}
