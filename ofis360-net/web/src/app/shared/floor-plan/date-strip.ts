import { Component, computed, inject, input, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { addDaysToKey, diffInDays, formatLongDate, isWeekend, todayKey, weekdayLabel } from '../../lib/date';
import { cn } from '../../lib/utils';
import { UiIcon } from '../ui';

/**
 * Tarih seçim şeridi (kaynak: components/floor-plan/date-strip.tsx).
 * Rezervasyon penceresi kadar ileri gidilebilir; geçmiş günler seçilemez. Seçim URL'e yazılır.
 */
@Component({
  selector: 'app-date-strip',
  imports: [UiIcon],
  template: `
    <div class="flex items-center gap-2">
      <button
        type="button"
        (click)="shift(-visibleCount())"
        [disabled]="!canGoBack()"
        aria-label="Önceki günler"
        class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border border-line bg-white text-ink-500 transition-colors hover:bg-slate-50 disabled:opacity-40"
      >
        <svg uiIcon name="ChevronLeft" class="h-4 w-4"></svg>
      </button>

      <div class="flex flex-1 gap-1.5 overflow-x-auto scrollbar-thin" role="group" aria-label="Tarih seçimi">
        @for (dayKey of days(); track dayKey) {
          @let selected = dayKey === selectedDayKey();
          @let disabled = dayKey < today || dayKey > maxDayKey() || blockedDayKeys().includes(dayKey);
          @let isToday = dayKey === today;
          <button
            type="button"
            (click)="select(dayKey)"
            [disabled]="disabled"
            [attr.aria-pressed]="selected"
            [attr.aria-label]="formatLongDate(dayKey)"
            [class]="cn('flex min-w-[64px] flex-1 flex-col items-center rounded-xl border px-2 py-2 transition-all', selected ? 'border-brand-500 bg-brand-600 text-white shadow-sm' : disabled ? 'cursor-not-allowed border-line bg-slate-50 text-ink-400' : 'border-line bg-white text-ink-700 hover:border-brand-200 hover:bg-brand-50/60')"
          >
            <span [class]="cn('text-[11px] font-medium', selected ? 'text-white/80' : isWeekend(dayKey) ? 'text-amber-700' : 'text-ink-500')">
              {{ weekdayLabel(dayKey, true) }}
            </span>
            <span class="text-base font-semibold leading-tight">{{ dayKey.slice(8, 10) }}</span>
            <span [class]="cn('text-[10px]', selected ? 'text-white/70' : 'text-ink-400')">
              {{ isToday ? 'Bugün' : dayKey.slice(5, 7) + '.' + dayKey.slice(2, 4) }}
            </span>
          </button>
        }
      </div>

      <button
        type="button"
        (click)="shift(visibleCount())"
        [disabled]="!canGoForward()"
        aria-label="Sonraki günler"
        class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border border-line bg-white text-ink-500 transition-colors hover:bg-slate-50 disabled:opacity-40"
      >
        <svg uiIcon name="ChevronRight" class="h-4 w-4"></svg>
      </button>

      <label class="relative hidden shrink-0 items-center sm:flex">
        <span class="sr-only">Takvimden tarih seç</span>
        <svg uiIcon name="CalendarDays" class="pointer-events-none absolute left-2.5 h-4 w-4 text-ink-400"></svg>
        <input
          type="date"
          [value]="selectedDayKey()"
          [min]="today"
          [max]="maxDayKey()"
          (change)="onDateInput($event)"
          class="h-9 rounded-lg border border-line bg-white pl-8 pr-2 text-sm text-ink-700 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-100"
        />
      </label>
    </div>
  `,
})
export class DateStrip {
  readonly selectedDayKey = input.required<string>();
  /** Sunucunun iş günü takvimine göre hesapladığı son seçilebilir gün. */
  readonly maxDayKey = input.required<string>();
  readonly blockedDayKeys = input<string[]>([]);
  readonly paramName = input('date');
  readonly visibleCount = input(7);

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly today = todayKey();
  readonly cn = cn;
  readonly isWeekend = isWeekend;
  readonly weekdayLabel = weekdayLabel;
  readonly formatLongDate = formatLongDate;

  private readonly windowOffset = signal<number | null>(null);
  readonly windowStart = computed(() => {
    const override = this.windowOffset();
    if (override !== null) return addDaysToKey(this.today, override);
    const offset = Math.max(0, diffInDays(this.today, this.selectedDayKey()) - Math.floor(this.visibleCount() / 2));
    return addDaysToKey(this.today, Math.min(offset, Math.max(0, diffInDays(this.today, this.maxDayKey()) - this.visibleCount() + 1)));
  });

  readonly days = computed(() => Array.from({ length: this.visibleCount() }, (_, index) => addDaysToKey(this.windowStart(), index)));
  readonly canGoBack = computed(() => this.windowStart() > this.today);
  readonly canGoForward = computed(() => addDaysToKey(this.windowStart(), this.visibleCount()) <= this.maxDayKey());

  shift(days: number): void {
    this.windowOffset.set(diffInDays(this.today, addDaysToKey(this.windowStart(), days)));
  }

  select(dayKey: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { [this.paramName()]: dayKey },
      queryParamsHandling: 'merge',
    });
  }

  onDateInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (value) this.select(value);
  }
}
