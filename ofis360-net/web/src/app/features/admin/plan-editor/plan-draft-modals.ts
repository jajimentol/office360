import { Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { cn } from '../../../lib/utils';
import { UiButton, UiField, UiIcon, UiInput, UiModal, UiSelect, UiSwitch } from '../../../shared/ui';
import type { EditorDesk, EditorRoom } from './editor-state';

/** Masa / oda yerleştirme modalları (kaynak: plan-editor.tsx DeskDraftModal & RoomDraftModal). */

export interface DeskDraftValues {
  code: string;
  zoneId: string;
  popular: boolean;
  accessibleOnly: boolean;
}

export interface RoomDraftValues {
  code: string;
  name: string;
  capacity: number;
}

@Component({
  selector: 'app-desk-draft-modal',
  imports: [FormsModule, UiModal, UiButton, UiIcon, UiField, UiInput, UiSelect, UiSwitch],
  template: `
    <ui-modal [open]="open()" (closed)="closed.emit()" title="Masa yerleştir" description="Yeni bir masa kaydı oluşturabilir veya mevcut bir masayı bu konuma taşıyabilirsiniz.">
      <div class="space-y-4">
        <div class="flex items-center gap-1 rounded-lg border border-line bg-slate-50 p-0.5">
          <button type="button" (click)="tab.set('new')" [attr.aria-pressed]="tab() === 'new'" [class]="cn('flex-1 rounded-md px-3 py-1.5 text-[13px] font-medium transition-colors', tab() === 'new' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')">Yeni masa oluştur</button>
          <button type="button" (click)="tab.set('existing')" [attr.aria-pressed]="tab() === 'existing'" [class]="cn('flex-1 rounded-md px-3 py-1.5 text-[13px] font-medium transition-colors', tab() === 'existing' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')">Mevcut masayı bağla</button>
        </div>

        @if (tab() === 'new') {
          <div class="grid gap-4 sm:grid-cols-2">
            <ui-field label="Masa kodu" required htmlFor="plan-desk-code">
              <input ui-input id="plan-desk-code" [ngModel]="code()" (ngModelChange)="code.set($event.toUpperCase())" />
            </ui-field>
            <ui-field label="Bölge" required htmlFor="plan-desk-zone" hint="Adil kullanım kuralları bölgeye bağlıdır.">
              <select ui-select id="plan-desk-zone" [ngModel]="zoneId()" (ngModelChange)="zoneId.set($event)">
                @for (zone of sortedZones(); track zone.id) {
                  <option [value]="zone.id">{{ zone.name }}{{ zone.reservable ? '' : ' (rezervasyona kapalı)' }}</option>
                }
              </select>
            </ui-field>
          </div>
          <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
            <span class="text-sm text-ink-900">Popüler masa</span>
            <ui-switch [checked]="popular()" (checkedChange)="popular.set($event)" label="Popüler masa" />
          </label>
          <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
            <span class="text-sm text-ink-900">Erişilebilirlik için ayrılmış</span>
            <ui-switch [checked]="accessibleOnly()" (checkedChange)="accessibleOnly.set($event)" label="Erişilebilirlik için ayrılmış" />
          </label>
        } @else {
          <ui-field label="Masa" htmlFor="plan-desk-existing" hint="Seçilen masa bu konuma taşınır; rezervasyonları korunur.">
            <select ui-select id="plan-desk-existing" [ngModel]="existingId()" (ngModelChange)="existingId.set($event)">
              @for (desk of desks(); track desk.id) {
                <option [value]="desk.id">{{ desk.code }} · {{ desk.zoneName }}</option>
              }
            </select>
          </ui-field>
        }
      </div>
      <ng-container footer>
        <button ui-button type="button" variant="secondary" (click)="closed.emit()" [disabled]="pending()">Vazgeç</button>
        <button ui-button type="button" [loading]="pending()" (click)="submit()" [disabled]="tab() === 'new' ? !code() || !zoneId() : !existingId()">
          <svg uiIcon name="Table2" class="h-4 w-4"></svg>
          {{ tab() === 'new' ? 'Masayı oluştur' : 'Masayı buraya taşı' }}
        </button>
      </ng-container>
    </ui-modal>
  `,
})
export class DeskDraftModal {
  readonly open = input.required<boolean>();
  readonly zones = input.required<Array<{ id: string; name: string; reservable: boolean }>>();
  readonly desks = input.required<EditorDesk[]>();
  readonly suggestedCode = input.required<string>();
  readonly pending = input(false);
  readonly closed = output<void>();
  readonly create = output<DeskDraftValues>();
  readonly link = output<string>();

  readonly cn = cn;
  readonly tab = signal<'new' | 'existing'>('new');
  readonly code = signal('');
  readonly zoneId = signal('');
  readonly popular = signal(false);
  readonly accessibleOnly = signal(false);
  readonly existingId = signal('');

  readonly defaultZoneId = computed(() => this.zones().find((zone) => zone.reservable)?.id ?? this.zones()[0]?.id ?? '');
  readonly sortedZones = computed(() => [...this.zones()].sort((a, b) => Number(b.reservable) - Number(a.reservable)));

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const suggested = this.suggestedCode();
      const zone = this.defaultZoneId();
      const first = this.desks()[0]?.id ?? '';
      untracked(() => {
        this.code.set(suggested);
        this.zoneId.set(zone);
        this.popular.set(false);
        this.accessibleOnly.set(false);
        this.tab.set('new');
        this.existingId.set(first);
      });
    });
  }

  submit(): void {
    if (this.tab() === 'existing') {
      this.link.emit(this.existingId());
      return;
    }
    this.create.emit({ code: this.code(), zoneId: this.zoneId(), popular: this.popular(), accessibleOnly: this.accessibleOnly() });
  }
}

@Component({
  selector: 'app-room-draft-modal',
  imports: [FormsModule, UiModal, UiButton, UiField, UiInput, UiSelect],
  template: `
    <ui-modal [open]="open()" (closed)="closed.emit()" title="Toplantı odası yerleştir" description="Yeni bir oda kaydı oluşturabilir veya mevcut bir odayı bu alana taşıyabilirsiniz.">
      <div class="space-y-4">
        <div class="flex items-center gap-1 rounded-lg border border-line bg-slate-50 p-0.5">
          <button type="button" (click)="tab.set('new')" [attr.aria-pressed]="tab() === 'new'" [class]="cn('flex-1 rounded-md px-3 py-1.5 text-[13px] font-medium transition-colors', tab() === 'new' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')">Yeni oda oluştur</button>
          <button type="button" (click)="tab.set('existing')" [attr.aria-pressed]="tab() === 'existing'" [disabled]="rooms().length === 0" [class]="cn('flex-1 rounded-md px-3 py-1.5 text-[13px] font-medium transition-colors disabled:opacity-40', tab() === 'existing' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')">Mevcut odayı bağla</button>
        </div>

        @if (tab() === 'new') {
          <div class="grid gap-4 sm:grid-cols-2">
            <ui-field label="Oda adı" required htmlFor="plan-room-name">
              <input ui-input id="plan-room-name" [ngModel]="name()" (ngModelChange)="name.set($event)" placeholder="Örneğin: Boğaziçi" />
            </ui-field>
            <ui-field label="Oda kodu" required htmlFor="plan-room-code">
              <input ui-input id="plan-room-code" [ngModel]="code()" (ngModelChange)="code.set($event.toUpperCase())" />
            </ui-field>
            <ui-field label="Kapasite" required htmlFor="plan-room-capacity">
              <input ui-input id="plan-room-capacity" type="number" min="1" [ngModel]="capacity()" (ngModelChange)="capacity.set(Number($event))" />
            </ui-field>
          </div>
        } @else {
          <ui-field label="Toplantı odası" htmlFor="plan-room-existing" hint="Seçilen oda bu alana taşınır; rezervasyonları korunur.">
            <select ui-select id="plan-room-existing" [ngModel]="existingId()" (ngModelChange)="existingId.set($event)">
              @for (room of rooms(); track room.id) {
                <option [value]="room.id">{{ room.name }} ({{ room.code }})</option>
              }
            </select>
          </ui-field>
        }
      </div>
      <ng-container footer>
        <button ui-button type="button" variant="secondary" (click)="closed.emit()" [disabled]="pending()">Vazgeç</button>
        <button ui-button type="button" [loading]="pending()" (click)="submit()" [disabled]="tab() === 'new' ? !code() || name().trim().length < 2 : !existingId()">
          {{ tab() === 'new' ? 'Odayı oluştur' : 'Odayı buraya taşı' }}
        </button>
      </ng-container>
    </ui-modal>
  `,
})
export class RoomDraftModal {
  readonly open = input.required<boolean>();
  readonly rooms = input.required<EditorRoom[]>();
  readonly suggestedCode = input.required<string>();
  readonly pending = input(false);
  readonly closed = output<void>();
  readonly create = output<RoomDraftValues>();
  readonly link = output<string>();

  readonly cn = cn;
  readonly Number = Number;
  readonly tab = signal<'new' | 'existing'>('new');
  readonly code = signal('');
  readonly name = signal('');
  readonly capacity = signal(6);
  readonly existingId = signal('');

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const suggested = this.suggestedCode();
      const first = this.rooms()[0]?.id ?? '';
      untracked(() => {
        this.code.set(suggested);
        this.name.set('');
        this.capacity.set(6);
        this.tab.set('new');
        this.existingId.set(first);
      });
    });
  }

  submit(): void {
    if (this.tab() === 'existing') {
      this.link.emit(this.existingId());
      return;
    }
    this.create.emit({ code: this.code(), name: this.name(), capacity: this.capacity() });
  }
}
