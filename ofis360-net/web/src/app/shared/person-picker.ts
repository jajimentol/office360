import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../core/api/api-client';
import { UiIcon, UiInput } from './ui';

export interface PersonResult {
  id: string;
  fullName: string;
  title: string;
  departmentName: string;
}

/** Ad veya e-postayla kişi arama; yalnızca Admin ve Sekreter kullanır. */
@Component({
  selector: 'app-person-picker',
  imports: [FormsModule, UiIcon, UiInput],
  template: `
    @if (selected(); as person) {
      <div class="flex items-center justify-between gap-2 rounded-lg border border-brand-200 bg-brand-50/60 px-3 py-2">
        <span class="min-w-0 text-[13px]">
          <span class="block truncate font-medium text-ink-900">{{ person.fullName }}</span>
          <span class="block truncate text-xs text-ink-500">{{ person.title }} · {{ person.departmentName }}</span>
        </span>
        <button type="button" (click)="clear()" [attr.aria-label]="person.fullName + ' seçimini kaldır'" class="rounded p-1 text-ink-400 hover:bg-white hover:text-ink-700">
          <svg uiIcon name="X" class="h-4 w-4"></svg>
        </button>
      </div>
    } @else {
      <div class="relative">
        <input ui-input [id]="inputId()" autocomplete="off" [placeholder]="placeholder()" [ngModel]="term()" [ngModelOptions]="{ standalone: true }" (ngModelChange)="onTerm($event)" />
        @if (results().length > 0) {
          <ul class="absolute z-20 mt-1 max-h-60 w-full overflow-y-auto rounded-lg border border-line bg-white shadow-lg">
            @for (person of results(); track person.id) {
              <li>
                <button type="button" (click)="pick(person)" class="block w-full px-3 py-2 text-left text-[13px] hover:bg-brand-50">
                  <span class="block font-medium text-ink-900">{{ person.fullName }}</span>
                  <span class="block text-xs text-ink-500">{{ person.title }} · {{ person.departmentName }}</span>
                </button>
              </li>
            }
          </ul>
        } @else if (term().trim().length >= 2 && !searching()) {
          <p class="mt-1 text-xs text-ink-500">Kişi bulunamadı.</p>
        }
      </div>
    }
  `,
})
export class PersonPicker {
  private readonly api = inject(ApiClient);

  readonly inputId = input('person-picker');
  readonly placeholder = input('Ad veya e-posta yazın');
  readonly picked = output<PersonResult | null>();

  readonly term = signal('');
  readonly results = signal<PersonResult[]>([]);
  readonly selected = signal<PersonResult | null>(null);
  readonly searching = signal(false);
  private timer: ReturnType<typeof setTimeout> | undefined;
  private requestId = 0;

  onTerm(value: string): void {
    this.term.set(value);
    clearTimeout(this.timer);
    this.results.set([]);
    if (value.trim().length < 2) return;
    this.searching.set(true);
    this.timer = setTimeout(() => void this.search(value.trim()), 250);
  }

  private async search(q: string): Promise<void> {
    const id = ++this.requestId;
    try {
      const people = await this.api.get<PersonResult[]>('people/search', { q });
      if (id === this.requestId) this.results.set(people);
    } finally {
      if (id === this.requestId) this.searching.set(false);
    }
  }

  pick(person: PersonResult): void {
    this.selected.set(person);
    this.results.set([]);
    this.term.set('');
    this.picked.emit(person);
  }

  clear(): void {
    this.selected.set(null);
    this.picked.emit(null);
  }
}
