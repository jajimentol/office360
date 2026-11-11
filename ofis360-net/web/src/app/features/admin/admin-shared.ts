import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { cn } from '../../lib/utils';

/** Admin sayfalarında tekrarlanan küçük parçalar (kaynak: sayfa içi FilterLink / PageLink / StatTile yardımcıları). */

@Component({
  selector: 'a[adminFilterLink]',
  template: `<ng-content />`,
  host: {
    '[class]': 'classes()',
    '[attr.aria-current]': 'active() ? "page" : null',
  },
})
export class AdminFilterLink {
  readonly active = input(false);
  classes(): string {
    return cn(
      'rounded-lg border px-3 py-1.5 text-[13px] font-medium transition-colors',
      this.active() ? 'border-brand-500 bg-brand-600 text-white' : 'border-line bg-white text-ink-700 hover:border-brand-200 hover:bg-brand-50',
    );
  }
}

@Component({
  selector: 'admin-page-nav',
  imports: [RouterLink],
  template: `
    <span class="text-[13px] text-ink-500">{{ skip() + 1 }}-{{ skip() + shown() }} / {{ total() }} kayıt</span>
    <div class="flex items-center gap-2">
      @if (page() <= 1) {
        <span class="rounded-lg border border-line bg-slate-50 px-3 py-1.5 text-[13px] text-ink-300">Önceki</span>
      } @else {
        <a [routerLink]="[]" [queryParams]="queryFor(page() - 1)" class="rounded-lg border border-line bg-white px-3 py-1.5 text-[13px] font-medium text-ink-700 hover:border-brand-200 hover:bg-brand-50">Önceki</a>
      }
      <span class="text-[13px] text-ink-600">{{ page() }} / {{ pageCount() }}</span>
      @if (page() >= pageCount()) {
        <span class="rounded-lg border border-line bg-slate-50 px-3 py-1.5 text-[13px] text-ink-300">Sonraki</span>
      } @else {
        <a [routerLink]="[]" [queryParams]="queryFor(page() + 1)" class="rounded-lg border border-line bg-white px-3 py-1.5 text-[13px] font-medium text-ink-700 hover:border-brand-200 hover:bg-brand-50">Sonraki</a>
      }
    </div>
  `,
  host: { class: 'flex items-center justify-between' },
})
export class AdminPageNav {
  readonly page = input.required<number>();
  readonly pageCount = input.required<number>();
  readonly skip = input.required<number>();
  readonly shown = input.required<number>();
  readonly total = input.required<number>();
  /** Mevcut sorgu parametreleri; sayfa numarası `pageParam` anahtarıyla eklenir. */
  readonly query = input<Record<string, string | null | undefined>>({});
  readonly pageParam = input('sayfa');

  queryFor(nextPage: number): Record<string, string | null> {
    const result: Record<string, string | null> = {};
    for (const [key, value] of Object.entries(this.query())) result[key] = value ?? null;
    result[this.pageParam()] = nextPage > 1 ? String(nextPage) : null;
    return result;
  }
}

@Component({
  selector: 'admin-stat-tile',
  template: `
    <p class="text-xs font-semibold uppercase tracking-wide text-ink-400">{{ label() }}</p>
    <p [class]="cn('mt-1 text-2xl font-semibold', tone() === 'success' ? 'text-teal-700' : tone() === 'warning' ? 'text-amber-700' : tone() === 'danger' ? 'text-red-700' : 'text-ink-900')">{{ value() }}</p>
    @if (hint()) {
      <p class="mt-0.5 text-[12px] text-ink-500">{{ hint() }}</p>
    }
  `,
  host: { class: 'card-surface block px-4 py-3' },
})
export class AdminStatTile {
  readonly label = input.required<string>();
  readonly value = input.required<number | string>();
  readonly hint = input<string | null | undefined>();
  readonly tone = input<'success' | 'warning' | 'danger' | undefined>();
  readonly cn = cn;
}

/** Form modallarındaki sunucu hata kutusu. */
@Component({
  selector: 'admin-form-error',
  template: `<ng-content />`,
  host: { class: 'block rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700', role: 'alert' },
})
export class AdminFormError {}

/** Kaynaktaki floorLabel (space.service.ts): "Bina · Kat adı" ya da yalnızca kat adı. */
export function floorLabelOf(floor: { name: string; building?: { name: string } | null; label?: string }): string {
  if (floor.label) return floor.label;
  return floor.building ? `${floor.building.name} · ${floor.name}` : floor.name;
}
