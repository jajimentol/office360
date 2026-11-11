import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { addDaysToKey, formatLongDate, todayKey } from '../../lib/date';
import { CLOSURE_REASONS, CLOSURE_REASON_LABELS, CLOSURE_SCOPES, CLOSURE_SCOPE_LABELS, type ClosureReason, type ClosureScope } from '../../lib/enums';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import type { ClosureDto, DeskDto, FloorDto, Paged, RoomDto } from '../../core/api/types';
import {
  PageState,
  ToastService,
  UiAlert,
  UiBadge,
  UiButton,
  UiCard,
  UiCardBody,
  UiCardHeader,
  UiConfirmDialog,
  UiDataTable,
  UiEmptyState,
  UiField,
  UiIcon,
  UiInput,
  UiModal,
  UiPageError,
  UiPageHeader,
  UiPageLoading,
  UiSelect,
  UiTd,
  UiTextarea,
} from '../../shared/ui';
import { AdminFormError, floorLabelOf } from './admin-shared';

/** Kaynak: (app)/admin/kapatmalar/page.tsx + components/admin/closure-manager.tsx */

interface Option {
  id: string;
  label: string;
}

interface PageData {
  closures: ClosureDto[];
  floors: Option[];
  desks: Option[];
  rooms: Option[];
}

@Component({
  selector: 'app-closures-page',
  imports: [FormsModule, UiPageHeader, UiPageLoading, UiPageError, UiAlert, UiCard, UiCardHeader, UiCardBody, UiDataTable, UiTd, UiEmptyState, UiBadge, UiButton, UiIcon, UiModal, UiConfirmDialog, UiField, UiInput, UiSelect, UiTextarea, AdminFormError],
  template: `
    <ui-page-header title="Bakım ve kapatma yönetimi" description="Bakım, etkinlik veya özel kullanım nedeniyle masa, toplantı odası ya da kat kapatın." />

    <ui-alert tone="warning" class="mb-5" title="Kapatma işleminin etkisi">
      Kapatma kaydı oluşturulduğunda seçilen tarih aralığındaki mevcut rezervasyonlar otomatik olarak iptal edilir, rezervasyon sahiplerine bildirim
      gönderilir ve işlem denetim kaydına yazılır.
    </ui-alert>

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as d) {
      <div ui-card>
        <ui-card-header title="Kapatma kayıtları" [description]="activeCount() + ' aktif kapatma'" hasIcon>
          <svg uiIcon icon name="Wrench" class="h-4.5 w-4.5"></svg>
        </ui-card-header>
        <ui-card-body>
          <div class="mb-4 flex justify-end">
            <button ui-button type="button" (click)="openForm()">
              <svg uiIcon name="Plus" class="h-4 w-4"></svg>
              Kapatma kaydı oluştur
            </button>
          </div>

          @if (d.closures.length === 0) {
            <ui-empty-state hasIcon title="Kapatma kaydı bulunmuyor" description="Bakım, etkinlik veya özel kullanım için masa, oda ya da kat kapatabilirsiniz.">
              <svg uiIcon icon name="Wrench" class="h-6 w-6"></svg>
            </ui-empty-state>
          } @else {
            <ui-data-table caption="Kapatma kayıtları" [headers]="[{ label: 'Kapsam' }, { label: 'Alan' }, { label: 'Tarih aralığı' }, { label: 'Gerekçe' }, { label: 'Durum' }, { label: '', className: 'text-right' }]">
              @for (closure of d.closures; track closure.id) {
                <tr class="hover:bg-slate-50/60">
                  <td ui-td>{{ closure.scopeLabel }}</td>
                  <td ui-td class="font-medium text-ink-900">{{ targetLabel(closure) }}</td>
                  <td ui-td>{{ formatLongDate(closure.startDate, { withWeekday: false }) }} – {{ formatLongDate(closure.endDate, { withWeekday: false }) }}</td>
                  <td ui-td>
                    <span class="block">{{ closure.reasonLabel }}</span>
                    <span class="mt-0.5 block max-w-[280px] text-xs text-ink-500">{{ closure.note }}</span>
                  </td>
                  <td ui-td>
                    <span ui-badge [tone]="isRunning(closure) ? 'warning' : 'neutral'">{{ isRunning(closure) ? 'Aktif' : 'Sona erdi' }}</span>
                  </td>
                  <td ui-td class="text-right">
                    @if (isRunning(closure)) {
                      <button ui-button type="button" size="sm" variant="secondary" (click)="endTarget.set(closure)">
                        <svg uiIcon name="Undo2" class="h-3.5 w-3.5"></svg>
                        Kapatmayı kaldır
                      </button>
                    } @else {
                      <span class="text-xs text-ink-400">—</span>
                    }
                  </td>
                </tr>
              }
            </ui-data-table>
          }
        </ui-card-body>
      </div>

      <ui-modal [open]="formOpen()" (closed)="formOpen.set(false)" title="Kapatma kaydı oluştur" description="Seçilen tarih aralığındaki mevcut rezervasyonlar otomatik iptal edilir ve çalışanlar bilgilendirilir.">
        <div class="space-y-4">
          @if (formError()) {
            <admin-form-error>{{ formError() }}</admin-form-error>
          }

          <div class="grid gap-4 sm:grid-cols-2">
            <ui-field label="Kapsam" required htmlFor="closure-scope">
              <select ui-select id="closure-scope" [ngModel]="form.scope" (ngModelChange)="changeScope($event)">
                @for (scope of scopes; track scope) {
                  <option [value]="scope">{{ scopeLabels[scope] }}</option>
                }
              </select>
            </ui-field>
            <ui-field label="Alan" required htmlFor="closure-target">
              <select ui-select id="closure-target" [(ngModel)]="form.targetId">
                @for (target of targets(); track target.id) {
                  <option [value]="target.id">{{ target.label }}</option>
                }
              </select>
            </ui-field>
          </div>

          <div class="grid gap-4 sm:grid-cols-2">
            <ui-field label="Başlangıç tarihi" required htmlFor="closure-start">
              <input ui-input id="closure-start" type="date" [min]="today" [(ngModel)]="form.startDayKey" />
            </ui-field>
            <ui-field label="Bitiş tarihi" required htmlFor="closure-end">
              <input ui-input id="closure-end" type="date" [min]="form.startDayKey" [(ngModel)]="form.endDayKey" />
            </ui-field>
          </div>

          <ui-field label="Gerekçe" required htmlFor="closure-reason">
            <select ui-select id="closure-reason" [(ngModel)]="form.reason">
              @for (reason of reasons; track reason) {
                <option [value]="reason">{{ reasonLabels[reason] }}</option>
              }
            </select>
          </ui-field>

          <ui-field label="Açıklama" required htmlFor="closure-note" hint="Bu açıklama çalışanlara gönderilen bildirimde yer alır.">
            <textarea ui-textarea id="closure-note" rows="3" [(ngModel)]="form.note" placeholder="Örneğin: Monitör ve dock istasyonu değişimi yapılacaktır."></textarea>
          </ui-field>
        </div>
        <ng-container footer>
          <button ui-button type="button" variant="secondary" (click)="formOpen.set(false)" [disabled]="pending()">Vazgeç</button>
          <button ui-button type="button" (click)="submit()" [loading]="pending()">
            <svg uiIcon name="Save" class="h-4 w-4"></svg>
            Kapatmayı uygula
          </button>
        </ng-container>
      </ui-modal>

      <ui-confirm-dialog [open]="endTarget() !== null" (closed)="endTarget.set(null)" (confirmed)="endClosure()" [loading]="pending()" tone="primary" title="Kapatmayı kaldır" confirmLabel="Kullanıma aç" hasContent>
        <p class="text-sm text-ink-700">
          <strong class="font-semibold text-ink-900">{{ endTarget() ? targetLabel(endTarget()!) : '' }}</strong> yeniden rezervasyona açılacak. Daha önce iptal edilen rezervasyonlar geri yüklenmez.
        </p>
      </ui-confirm-dialog>
    }
  `,
})
export class ClosuresPage {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  readonly state = new PageState<PageData>();
  readonly today = todayKey();
  readonly formatLongDate = formatLongDate;
  readonly scopes = CLOSURE_SCOPES;
  readonly scopeLabels = CLOSURE_SCOPE_LABELS;
  readonly reasons = CLOSURE_REASONS;
  readonly reasonLabels = CLOSURE_REASON_LABELS;

  readonly formOpen = signal(false);
  readonly pending = signal(false);
  readonly formError = signal<string | null>(null);
  readonly endTarget = signal<ClosureDto | null>(null);
  private readonly scope = signal<ClosureScope>('DESK');
  form = { scope: 'DESK' as ClosureScope, targetId: '', startDayKey: this.today, endDayKey: addDaysToKey(this.today, 2), reason: 'MAINTENANCE' as ClosureReason, note: '' };

  readonly activeCount = computed(() => (this.state.data()?.closures ?? []).filter((closure) => this.isRunning(closure)).length);

  /** Bitiş tarihi geçen kapatma, kaydı açık kalsa da artık yürürlükte değildir. */
  isRunning(closure: { active: boolean; endDate: string }): boolean {
    return closure.active && closure.endDate >= this.today;
  }
  readonly targets = computed<Option[]>(() => {
    const d = this.state.data();
    if (!d) return [];
    const scope = this.scope();
    return scope === 'DESK' ? d.desks : scope === 'ROOM' ? d.rooms : d.floors;
  });

  constructor() {
    void this.load();
  }

  targetLabel(closure: ClosureDto): string {
    return closure.desk?.code ?? (closure.room ? `${closure.room.name} (${closure.room.code})` : null) ?? closure.floor?.name ?? 'Bilinmiyor';
  }

  load(silent = false): Promise<unknown> {
    return this.state.load(
      async () => {
        const [closures, floors, desks, rooms] = await Promise.all([
          this.api.get<ClosureDto[]>('admin/closures'),
          this.api.get<FloorDto[]>('admin/floors'),
          this.api.get<Paged<DeskDto>>('admin/desks', { placement: 'placed', take: 500 }),
          this.api.get<RoomDto[]>('admin/rooms'),
        ]);
        return {
          closures,
          floors: floors.map((floor) => ({ id: floor.id, label: floorLabelOf(floor) })),
          desks: desks.items.map((desk) => ({ id: desk.id, label: `${desk.code} · ${desk.floor?.name}` })),
          rooms: rooms.map((room) => ({ id: room.id, label: `${room.name} (${room.code}) · ${room.floor?.name}` })),
        };
      },
      { silent },
    );
  }

  openForm(): void {
    this.formError.set(null);
    if (!this.form.targetId) this.form.targetId = this.targets()[0]?.id ?? '';
    this.formOpen.set(true);
  }

  changeScope(scope: ClosureScope): void {
    this.form.scope = scope;
    this.scope.set(scope);
    this.form.targetId = this.targets()[0]?.id ?? '';
  }

  async submit(): Promise<void> {
    this.pending.set(true);
    this.formError.set(null);
    try {
      const result = await this.api.post<{ id: string; cancelledReservations: number }>('admin/closures', this.form);
      this.toasts.toast({
        tone: 'success',
        title: 'Kapatma kaydı oluşturuldu',
        description: result.cancelledReservations > 0 ? `${result.cancelledReservations} rezervasyon iptal edildi ve ilgili çalışanlar bilgilendirildi.` : 'Etkilenen rezervasyon bulunmuyor.',
      });
      this.formOpen.set(false);
      this.form = { ...this.form, note: '' };
      void this.load(true);
    } catch (error: unknown) {
      this.formError.set(errorMessage(error));
    } finally {
      this.pending.set(false);
    }
  }

  async endClosure(): Promise<void> {
    const target = this.endTarget();
    if (!target) return;
    this.pending.set(true);
    try {
      await this.api.put(`admin/closures/${target.id}`, { active: false });
      this.toasts.toast({ tone: 'success', title: 'Kapatma sonlandırıldı', description: 'Alan yeniden kullanıma açıldı.' });
      void this.load(true);
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'İşlem başarısız', description: errorMessage(error) });
    } finally {
      this.endTarget.set(null);
      this.pending.set(false);
    }
  }
}
