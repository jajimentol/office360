import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { formatLongDate } from '../../lib/date';
import { GRADES, GRADE_LABELS, type Grade } from '../../lib/enums';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import { PageState, ToastService, UiButton, UiIcon, UiInput, UiModal, UiSelect } from '../../shared/ui';
import { AdminFormError } from './admin-shared';

interface MatrixRule {
  key: string;
  name: string;
  kind: 'toggle' | 'number';
  unit?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  baseEnabled: boolean;
  baseValue?: number | null;
  allowedValues?: number[] | null;
}

interface MatrixCell {
  ruleKey: string;
  grade: Grade;
  enabled?: boolean | null;
  intValue?: number | null;
}

interface MatrixData {
  buildingId?: string | null;
  rules: MatrixRule[];
  cells: MatrixCell[];
  buildings: Array<{ id: string; name: string }>;
}

interface MatrixChange {
  rule: string;
  grade: string;
  from: string;
  to: string;
}

const cellId = (ruleKey: string, grade: string): string => `${ruleKey}|${grade}`;

/** Kademe x kural matrisi. Boş hücre varsayılan değeri kullanır. */
@Component({
  selector: 'app-rule-matrix',
  imports: [FormsModule, UiButton, UiIcon, UiSelect, UiModal, AdminFormError],
  template: `
    <section>
      <div class="mb-3 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 class="text-base font-semibold text-ink-900">Kademe bazlı kurallar</h2>
          <p class="mt-0.5 max-w-3xl text-[13px] text-ink-500">Boş hücre varsayılanı kullanır; kademesi atanmamış kişiler de varsayılana tabidir. Değişiklikler yalnızca yeni rezervasyonlarda geçerli olur.</p>
        </div>
        <label class="block">
          <span class="mb-1 block text-xs font-medium text-ink-500">Kapsam</span>
          <select ui-select class="w-56" [ngModel]="buildingId()" (ngModelChange)="changeBuilding($event)" aria-label="Kural kapsamı">
            <option value="">Tüm binalar</option>
            @for (building of state.data()?.buildings ?? []; track building.id) {
              <option [value]="building.id">{{ building.name }}</option>
            }
          </select>
        </label>
      </div>

      @if (state.error()) {
        <admin-form-error>{{ state.error() }}</admin-form-error>
      } @else if (state.data(); as d) {
        <div class="overflow-x-auto rounded-xl border border-line bg-white scrollbar-thin">
          <table class="w-full min-w-[1080px] border-collapse text-[13px]">
            <caption class="sr-only">Kademeye göre rezervasyon kuralları</caption>
            <thead>
              <tr class="bg-slate-50 text-xs text-ink-500">
                <th scope="col" class="sticky left-0 z-10 bg-slate-50 px-3 py-2 text-left font-semibold">Kural</th>
                <th scope="col" class="px-2 py-2 text-center font-semibold">Varsayılan</th>
                @for (grade of grades; track grade) {
                  <th scope="col" class="px-2 py-2 text-center font-semibold">{{ gradeLabels[grade] }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (rule of d.rules; track rule.key) {
                <tr class="border-t border-line">
                  <th scope="row" class="sticky left-0 z-10 bg-white px-3 py-2 text-left font-medium text-ink-900">
                    {{ rule.name }}
                    @if (rule.unit) {
                      <span class="block text-[11px] font-normal text-ink-400">{{ rule.unit }}</span>
                    }
                  </th>
                  <td class="px-2 py-2 text-center tabular-nums text-ink-500">{{ baseLabel(rule) }}</td>
                  @for (grade of grades; track grade) {
                    <td class="px-1 py-1.5 text-center">
                      @if (rule.kind === 'toggle') {
                        <select class="h-8 w-[76px] rounded-md border border-line bg-white px-1 text-[12px]" [ngModel]="value(rule.key, grade)" (ngModelChange)="set(rule.key, grade, $event)" [attr.aria-label]="rule.name + ' - ' + gradeLabels[grade]">
                          <option value="">-</option>
                          <option value="true">Açık</option>
                          <option value="false">Kapalı</option>
                        </select>
                      } @else if (rule.allowedValues) {
                        <select class="h-8 w-[76px] rounded-md border border-line bg-white px-1 text-[12px]" [ngModel]="value(rule.key, grade)" (ngModelChange)="set(rule.key, grade, $event)" [attr.aria-label]="rule.name + ' - ' + gradeLabels[grade]">
                          <option value="">-</option>
                          @for (option of rule.allowedValues; track option) {
                            <option [value]="'' + option">{{ option }}</option>
                          }
                        </select>
                      } @else {
                        <input type="number" class="h-8 w-[76px] rounded-md border border-line px-1.5 text-center text-[12px]" [min]="rule.minValue ?? null" [max]="rule.maxValue ?? null" [placeholder]="'' + (rule.baseValue ?? '')" [ngModel]="value(rule.key, grade)" (ngModelChange)="set(rule.key, grade, $event)" [attr.aria-label]="rule.name + ' - ' + gradeLabels[grade]" />
                      }
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
        <div class="mt-3 flex items-center justify-end gap-2">
          @if (changes().length > 0) {
            <span class="text-xs text-ink-500">{{ changes().length }} değişiklik</span>
            <button ui-button type="button" variant="ghost" size="sm" (click)="reset()">
              <svg uiIcon name="RotateCcw" class="h-3.5 w-3.5"></svg>
              Geri al
            </button>
          }
          <button ui-button type="button" size="sm" [disabled]="changes().length === 0" (click)="confirmOpen.set(true)">
            <svg uiIcon name="Save" class="h-3.5 w-3.5"></svg>
            Kaydet
          </button>
        </div>

        <ui-modal [open]="confirmOpen()" (closed)="confirmOpen.set(false)" size="lg" title="Kural değişikliklerini onaylayın" description="Mevcut rezervasyonlar etkilenmez; yeni rezervasyonlar hemen bu değerlerle kontrol edilir.">
          <div class="space-y-3">
            @if (saveError()) {
              <admin-form-error>{{ saveError() }}</admin-form-error>
            }
            <table class="w-full text-left text-[13px]">
              <caption class="sr-only">Değişiklik özeti</caption>
              <thead class="text-xs text-ink-500">
                <tr>
                  <th scope="col" class="py-1.5 pr-3">Kural</th>
                  <th scope="col" class="py-1.5 pr-3">Kademe</th>
                  <th scope="col" class="py-1.5 pr-3">Eski</th>
                  <th scope="col" class="py-1.5">Yeni</th>
                </tr>
              </thead>
              <tbody>
                @for (change of changes(); track $index) {
                  <tr class="border-t border-line">
                    <td class="py-1.5 pr-3">{{ change.rule }}</td>
                    <td class="py-1.5 pr-3">{{ change.grade }}</td>
                    <td class="py-1.5 pr-3 text-ink-500">{{ change.from }}</td>
                    <td class="py-1.5 font-medium text-ink-900">{{ change.to }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <ng-container footer>
            <button ui-button type="button" variant="secondary" (click)="confirmOpen.set(false)" [disabled]="pending()">Vazgeç</button>
            <button ui-button type="button" (click)="save()" [loading]="pending()">Onayla ve kaydet</button>
          </ng-container>
        </ui-modal>
      }
    </section>
  `,
})
export class RuleMatrixPanel {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly grades = GRADES;
  readonly gradeLabels = GRADE_LABELS;
  readonly state = new PageState<MatrixData>();
  readonly buildingId = signal('');
  readonly draft = signal<Record<string, string>>({});
  private readonly original = signal<Record<string, string>>({});
  readonly confirmOpen = signal(false);
  readonly pending = signal(false);
  readonly saveError = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    await this.state.load(() => this.api.get<MatrixData>('admin/rules/matrix', { buildingId: this.buildingId() || undefined }), { silent: this.state.data() !== null });
    const values: Record<string, string> = {};
    for (const cell of this.state.data()?.cells ?? []) {
      values[cellId(cell.ruleKey, cell.grade)] = cell.intValue != null ? String(cell.intValue) : String(cell.enabled);
    }
    this.original.set(values);
    this.draft.set({ ...values });
  }

  changeBuilding(id: string): void {
    this.buildingId.set(id);
    void this.load();
  }

  value(ruleKey: string, grade: string): string {
    return this.draft()[cellId(ruleKey, grade)] ?? '';
  }

  set(ruleKey: string, grade: string, value: string | number | null): void {
    const text = value === null || value === undefined ? '' : String(value);
    this.draft.update((current) => ({ ...current, [cellId(ruleKey, grade)]: text }));
  }

  reset(): void {
    this.draft.set({ ...this.original() });
  }

  baseLabel(rule: MatrixRule): string {
    return rule.kind === 'toggle' ? (rule.baseEnabled ? 'Açık' : 'Kapalı') : String(rule.baseValue ?? '-');
  }

  private display(rule: MatrixRule, value: string): string {
    if (!value) return `Varsayılan (${this.baseLabel(rule)})`;
    if (rule.kind === 'toggle') return value === 'true' ? 'Açık' : 'Kapalı';
    return value;
  }

  readonly changes = computed<MatrixChange[]>(() => {
    const result: MatrixChange[] = [];
    const before = this.original();
    const after = this.draft();
    for (const rule of this.state.data()?.rules ?? []) {
      for (const grade of GRADES) {
        const id = cellId(rule.key, grade);
        const oldValue = before[id] ?? '';
        const newValue = after[id] ?? '';
        if (oldValue !== newValue) result.push({ rule: rule.name, grade: GRADE_LABELS[grade], from: this.display(rule, oldValue), to: this.display(rule, newValue) });
      }
    }
    return result;
  });

  async save(): Promise<void> {
    this.pending.set(true);
    this.saveError.set(null);
    const cells: MatrixCell[] = [];
    for (const rule of this.state.data()?.rules ?? []) {
      for (const grade of GRADES) {
        const value = this.draft()[cellId(rule.key, grade)] ?? '';
        if (!value) continue;
        cells.push(rule.kind === 'toggle' ? { ruleKey: rule.key, grade, enabled: value === 'true' } : { ruleKey: rule.key, grade, intValue: Number(value) });
      }
    }
    try {
      await this.api.put('admin/rules/matrix', { buildingId: this.buildingId() || null, cells });
      this.toasts.toast({ tone: 'success', title: 'Kurallar güncellendi', description: 'Yeni rezervasyonlarda hemen geçerli.' });
      this.confirmOpen.set(false);
      await this.load();
    } catch (error: unknown) {
      this.saveError.set(errorMessage(error));
    } finally {
      this.pending.set(false);
    }
  }
}

interface HolidayDto {
  id: string;
  dayKey: string;
  name: string;
}

/** Resmi tatil takvimi: iş günü hesabından düşer. */
@Component({
  selector: 'app-holiday-panel',
  imports: [FormsModule, UiButton, UiIcon, UiInput, AdminFormError],
  template: `
    <section>
      <h2 class="text-base font-semibold text-ink-900">Resmi tatiller</h2>
      <p class="mb-3 mt-0.5 max-w-3xl text-[13px] text-ink-500">Listelenen günler iş günü sayılmaz. Dini bayramların tarihi her yıl değiştiği için yılda bir buradan girilmelidir.</p>

      <div class="rounded-xl border border-line bg-white p-4">
        @if (error()) {
          <admin-form-error class="mb-3">{{ error() }}</admin-form-error>
        }
        <form class="flex flex-wrap items-end gap-3" (ngSubmit)="add()">
          <label class="block">
            <span class="mb-1 block text-xs font-medium text-ink-500">Tarih</span>
            <input ui-input class="w-44" type="date" name="holiday-date" [(ngModel)]="date" />
          </label>
          <label class="block min-w-56 flex-1">
            <span class="mb-1 block text-xs font-medium text-ink-500">Tatil adı</span>
            <input ui-input name="holiday-name" maxlength="80" [(ngModel)]="name" />
          </label>
          <button ui-button type="submit" size="sm" [loading]="pending()" [disabled]="!date || name.trim().length < 2">
            <svg uiIcon name="Plus" class="h-3.5 w-3.5"></svg>
            Ekle
          </button>
        </form>

        @if (state.data(); as holidays) {
          @if (holidays.length === 0) {
            <p class="mt-4 text-[13px] text-ink-500">Henüz tatil tanımlı değil.</p>
          } @else {
            <ul class="mt-4 divide-y divide-line">
              @for (holiday of holidays; track holiday.id) {
                <li class="flex items-center justify-between gap-3 py-2 text-[13px]">
                  <span>
                    <span class="font-medium text-ink-900">{{ formatLongDate(holiday.dayKey) }}</span>
                    <span class="ml-2 text-ink-500">{{ holiday.name }}</span>
                  </span>
                  <button ui-button type="button" variant="ghost" size="sm" (click)="remove(holiday)" [attr.aria-label]="holiday.name + ' tatilini sil'">
                    <svg uiIcon name="X" class="h-3.5 w-3.5"></svg>
                    Sil
                  </button>
                </li>
              }
            </ul>
          }
        }
      </div>
    </section>
  `,
})
export class HolidayPanel {
  private readonly api = inject(ApiClient);
  readonly state = new PageState<HolidayDto[]>();
  readonly formatLongDate = formatLongDate;
  readonly pending = signal(false);
  readonly error = signal<string | null>(null);
  date = '';
  name = '';

  constructor() {
    void this.load();
  }

  load(): Promise<unknown> {
    return this.state.load(() => this.api.get<HolidayDto[]>('admin/holidays'), { silent: this.state.data() !== null });
  }

  async add(): Promise<void> {
    this.pending.set(true);
    this.error.set(null);
    try {
      await this.api.post('admin/holidays', { date: this.date, name: this.name.trim() });
      this.date = '';
      this.name = '';
      await this.load();
    } catch (error: unknown) {
      this.error.set(errorMessage(error));
    } finally {
      this.pending.set(false);
    }
  }

  async remove(holiday: HolidayDto): Promise<void> {
    this.error.set(null);
    try {
      await this.api.delete(`admin/holidays/${holiday.id}`);
      await this.load();
    } catch (error: unknown) {
      this.error.set(errorMessage(error));
    }
  }
}
