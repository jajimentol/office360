import { Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GRADES, GRADE_LABELS, type Grade } from '../../lib/enums';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import { ToastService, UiAlert, UiButton, UiField, UiIcon, UiInput, UiModal, UiSwitch } from '../../shared/ui';
import { PersonPicker, type PersonResult } from '../../shared/person-picker';
import { AdminFormError } from './admin-shared';

interface PriorityPerson {
  id: string;
  fullName: string;
  title: string;
}

interface RoomPolicyDto {
  roomId: string;
  roomName: string;
  capacity: number;
  secretaryOnly: boolean;
  priorityOpenDays?: number | null;
  gradeRules: Array<{ grade: Grade; minAttendees: number; bypassPriority?: boolean }>;
  priorityUsers: PriorityPerson[];
}

interface GradeRow {
  grade: Grade;
  allowed: boolean;
  min: number;
  bypass: boolean;
}

/** Odaya özel kurallar: kimler açabilir, kaç kişiyle, öncelikli kişiler. */
@Component({
  selector: 'app-room-policy-modal',
  imports: [FormsModule, UiAlert, UiButton, UiField, UiIcon, UiInput, UiModal, UiSwitch, PersonPicker, AdminFormError],
  template: `
    <ui-modal [open]="roomId() !== null" (closed)="closed.emit()" size="lg" [title]="(policy()?.roomName ?? 'Oda') + ' kuralları'" description="Yalnızca yeni rezervasyonlarda geçerli olur.">
      @if (policy(); as p) {
        <div class="space-y-5">
          @if (error()) {
            <admin-form-error>{{ error() }}</admin-form-error>
          }

          <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
            <span>
              <span class="block text-sm text-ink-900">Yalnızca Sekreter rezerve edebilir</span>
              <span class="block text-xs text-ink-500">Açıkken aşağıdaki kademe ve öncelik ayarları kullanılmaz.</span>
            </span>
            <ui-switch [checked]="secretaryOnly()" (checkedChange)="secretaryOnly.set($event)" label="Yalnızca Sekreter" />
          </label>

          @if (!secretaryOnly()) {
            <section>
              <h3 class="text-sm font-semibold text-ink-900">Kimler rezerve edebilir</h3>
              <p class="mt-0.5 text-xs text-ink-500">Hiçbir kademe seçilmezse oda herkese açıktır. Kapasite: {{ p.capacity }} kişi.</p>
              <table class="mt-2 w-full text-[13px]">
                <caption class="sr-only">Kademeye göre oda erişimi</caption>
                <thead class="text-xs text-ink-500">
                  <tr>
                    <th scope="col" class="py-1.5 text-left font-medium">Kademe</th>
                    <th scope="col" class="w-24 py-1.5 text-center font-medium">Açabilir</th>
                    <th scope="col" class="w-28 py-1.5 text-center font-medium">En az kişi</th>
                    <th scope="col" class="w-28 py-1.5 text-center font-medium">Önceliği beklemez</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of rows; track row.grade) {
                    <tr class="border-t border-line">
                      <th scope="row" class="py-1.5 text-left font-normal text-ink-900">{{ gradeLabels[row.grade] }}</th>
                      <td class="py-1.5 text-center">
                        <input type="checkbox" class="h-4 w-4" [(ngModel)]="row.allowed" [attr.aria-label]="gradeLabels[row.grade] + ' açabilir'" />
                      </td>
                      <td class="py-1.5 text-center">
                        <input type="number" class="h-8 w-20 rounded-md border border-line px-1.5 text-center text-[13px] disabled:bg-slate-50 disabled:text-ink-400" min="1" [max]="p.capacity" [disabled]="!row.allowed" [(ngModel)]="row.min" [attr.aria-label]="gradeLabels[row.grade] + ' en az kişi'" />
                      </td>
                      <td class="py-1.5 text-center">
                        <input type="checkbox" class="h-4 w-4" [disabled]="!row.allowed" [(ngModel)]="row.bypass" [attr.aria-label]="gradeLabels[row.grade] + ' önceliği beklemez'" />
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </section>

            <section class="space-y-3">
              <div>
                <h3 class="text-sm font-semibold text-ink-900">Öncelikli kişiler</h3>
                <p class="mt-0.5 text-xs text-ink-500">Listedekiler odayı kendi rezervasyon penceresinde alır, diğerleri son iş günlerinde alabilir.</p>
              </div>
              @for (person of priority(); track person.id) {
                <div class="flex items-center justify-between gap-2 rounded-lg border border-line px-3 py-2 text-[13px]">
                  <span><span class="font-medium text-ink-900">{{ person.fullName }}</span><span class="ml-1.5 text-ink-500">{{ person.title }}</span></span>
                  <button type="button" (click)="removePriority(person.id)" [attr.aria-label]="person.fullName + ' önceliğini kaldır'" class="rounded p-1 text-ink-400 hover:bg-slate-100 hover:text-ink-700">
                    <svg uiIcon name="X" class="h-4 w-4"></svg>
                  </button>
                </div>
              }
              @if (priority().length === 0 && openDays !== null && $any(openDays) !== '') {
                <ui-alert tone="warning">Kişi eklenmedikçe öncelik çalışmaz; oda herkese kendi rezervasyon penceresinde açık kalır.</ui-alert>
              }
              <ui-field label="Kişi ekle" htmlFor="policy-person">
                <app-person-picker #picker inputId="policy-person" (picked)="addPriority($event); picker.clear()" />
              </ui-field>
              <ui-field label="Diğerlerine açılma süresi (son kaç iş günü)" htmlFor="policy-days" hint="1: yalnızca bir önceki iş gününden itibaren alınabilir.">
                <input ui-input id="policy-days" class="w-32" type="number" min="0" max="90" [(ngModel)]="openDays" />
              </ui-field>
            </section>
          }
        </div>
      }
      <ng-container footer>
        <button ui-button type="button" variant="secondary" (click)="closed.emit()" [disabled]="pending()">Vazgeç</button>
        <button ui-button type="button" (click)="save()" [loading]="pending()" [disabled]="!policy()">
          <svg uiIcon name="Save" class="h-4 w-4"></svg>
          Kaydet
        </button>
      </ng-container>
    </ui-modal>
  `,
})
export class RoomPolicyModal {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);

  readonly roomId = input<string | null>(null);
  readonly closed = output<void>();

  readonly gradeLabels = GRADE_LABELS;
  readonly policy = signal<RoomPolicyDto | null>(null);
  readonly secretaryOnly = signal(false);
  readonly priority = signal<PriorityPerson[]>([]);
  readonly pending = signal(false);
  readonly error = signal<string | null>(null);
  rows: GradeRow[] = [];
  openDays: number | null = null;

  constructor() {
    effect(() => {
      const id = this.roomId();
      untracked(() => {
        this.policy.set(null);
        this.error.set(null);
        if (id) void this.load(id);
      });
    });
  }

  private async load(id: string): Promise<void> {
    try {
      const data = await this.api.get<RoomPolicyDto>(`admin/rooms/${id}/policy`);
      this.rows = GRADES.map((grade) => {
        const rule = data.gradeRules.find((r) => r.grade === grade);
        return { grade, allowed: !!rule, min: rule?.minAttendees ?? 1, bypass: !!rule?.bypassPriority };
      });
      this.secretaryOnly.set(data.secretaryOnly);
      this.priority.set(data.priorityUsers);
      this.openDays = data.priorityOpenDays ?? null;
      this.policy.set(data);
    } catch (error: unknown) {
      this.error.set(errorMessage(error));
    }
  }

  addPriority(person: PersonResult | null): void {
    if (!person || this.priority().some((p) => p.id === person.id)) return;
    this.priority.update((list) => [...list, { id: person.id, fullName: person.fullName, title: person.title }]);
  }

  removePriority(id: string): void {
    this.priority.update((list) => list.filter((p) => p.id !== id));
  }

  async save(): Promise<void> {
    const id = this.roomId();
    if (!id) return;
    this.pending.set(true);
    this.error.set(null);
    const hasDays = this.openDays !== null && String(this.openDays) !== '';
    try {
      await this.api.put(`admin/rooms/${id}/policy`, {
        secretaryOnly: this.secretaryOnly(),
        priorityOpenDays: hasDays ? Number(this.openDays) : null,
        gradeRules: this.rows.filter((r) => r.allowed).map((r) => ({ grade: r.grade, minAttendees: Number(r.min), bypassPriority: r.bypass })),
        priorityUserIds: this.priority().map((p) => p.id),
      });
      this.toasts.toast({ tone: 'success', title: 'Oda kuralları güncellendi', description: this.policy()?.roomName });
      this.closed.emit();
    } catch (error: unknown) {
      this.error.set(errorMessage(error));
    } finally {
      this.pending.set(false);
    }
  }
}
