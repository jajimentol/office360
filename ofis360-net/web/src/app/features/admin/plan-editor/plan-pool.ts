import { Component, input, output } from '@angular/core';
import { cn } from '../../../lib/utils';
import type { UnplacedDesk, UnplacedRoom } from '../../../core/api/types';
import { UiIcon, type IconName } from '../../../shared/ui';

/**
 * Yerleştirilmemiş masa ve toplantı odası havuzu (kaynak: plan-pool.tsx).
 * Envantere eklenmiş ancak krokide henüz konumu olmayan nesneler burada bekler. Havuzdaki nesneler
 * çalışan rezervasyonuna kapalıdır. Admin bir nesneyi çalışma alanına sürükleyerek ya da toplu yerleştirme
 * düğmesiyle konumlandırır.
 */

export type PoolDesk = UnplacedDesk;
export type PoolRoom = UnplacedRoom;

export const POOL_DRAG_TYPE = 'application/x-ofis360-pool-item';

export type PendingPoolItem = { kind: 'desk' | 'meetingRoom'; id: string; label: string } | null;

@Component({
  selector: 'app-plan-pool',
  imports: [UiIcon],
  template: `
    <section class="rounded-xl border border-line bg-white" aria-labelledby="plan-pool-title">
      <header class="flex flex-wrap items-start justify-between gap-3 border-b border-line px-4 py-3">
        <div>
          <h3 id="plan-pool-title" class="text-[14px] font-semibold text-ink-900">Yerleştirilmemiş nesneler</h3>
          <p class="mt-0.5 text-[12.5px] text-ink-500">
            {{ total() === 0 ? 'Bu katta bekleyen nesne yok.' : desks().length + ' masa, ' + rooms().length + ' toplantı odası krokide konumlandırılmayı bekliyor. Bunlar çalışan rezervasyonuna kapalıdır.' }}
          </p>
        </div>
        @if (total() > 0) {
          <button
            type="button"
            (click)="autoPlace.emit()"
            [disabled]="autoPlacing()"
            class="flex items-center gap-1.5 rounded-lg border border-brand-200 bg-brand-50 px-3 py-1.5 text-[13px] font-medium text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-60"
          >
            @if (autoPlacing()) {
              <svg uiIcon name="Loader2" class="h-4 w-4 animate-spin"></svg>
            } @else {
              <svg uiIcon name="Sparkles" class="h-4 w-4"></svg>
            }
            Havuzdaki nesneleri otomatik yerleştir
          </button>
        }
      </header>

      @if (total() > 0) {
        <div class="max-h-56 overflow-y-auto p-3 scrollbar-thin">
          <ul class="flex flex-wrap gap-2">
            @for (room of rooms(); track room.id) {
              <li>
                <button
                  type="button"
                  (click)="toggle({ kind: 'meetingRoom', id: room.id, label: room.code })"
                  [attr.aria-pressed]="pending()?.id === room.id"
                  draggable="true"
                  (dragstart)="onDragStart($event, { kind: 'meetingRoom', id: room.id })"
                  [title]="room.code + ' · ' + room.name + ' · ' + room.capacity + ' kişi — tıklayıp krokide bir noktaya tıklayın ya da sürükleyin'"
                  [class]="chipClass(pending()?.id === room.id, 'room')"
                >
                  <svg uiIcon name="Users" class="h-3.5 w-3.5"></svg>
                  {{ room.code }}
                </button>
              </li>
            }
            @for (desk of desks(); track desk.id) {
              <li>
                <button
                  type="button"
                  (click)="toggle({ kind: 'desk', id: desk.id, label: desk.code })"
                  [attr.aria-pressed]="pending()?.id === desk.id"
                  draggable="true"
                  (dragstart)="onDragStart($event, { kind: 'desk', id: desk.id })"
                  [title]="desk.code + ' · ' + desk.zoneName + ' — tıklayıp krokide bir noktaya tıklayın ya da sürükleyin'"
                  [class]="chipClass(pending()?.id === desk.id, desk.active ? 'desk' : 'closed')"
                >
                  <svg uiIcon [name]="deskIcon(desk)" class="h-3.5 w-3.5"></svg>
                  {{ desk.code }}
                </button>
              </li>
            }
          </ul>
          @if (pending(); as p) {
            <p class="mt-3 rounded-lg bg-brand-50 px-3 py-2 text-[12.5px] font-medium text-brand-800">
              {{ p.label }} seçildi. Şimdi çalışma alanında koymak istediğiniz noktaya tıklayın. Vazgeçmek için tekrar {{ p.label }} üzerine tıklayın.
            </p>
          } @else {
            <p class="mt-3 text-[12px] text-ink-500">
              Bir nesneye tıklayıp krokide bir noktaya tıklayarak (veya nesneyi doğrudan çalışma alanına sürükleyerek) konumlandırabilir, ardından
              <strong class="font-medium text-ink-700">Krokiyi kaydet</strong> ile kalıcı hâle getirebilirsiniz.
            </p>
          }
        </div>
      }
    </section>
  `,
})
export class PlanPool {
  readonly desks = input.required<PoolDesk[]>();
  readonly rooms = input.required<PoolRoom[]>();
  readonly autoPlacing = input(false);
  /** Yerleştirilmek üzere seçilmiş nesne (tıklayarak yerleştirme akışı). */
  readonly pending = input.required<PendingPoolItem>();
  readonly autoPlace = output<void>();
  readonly selectItem = output<PendingPoolItem>();

  total(): number {
    return this.desks().length + this.rooms().length;
  }

  toggle(item: NonNullable<PendingPoolItem>): void {
    this.selectItem.emit(this.pending()?.id === item.id ? null : item);
  }

  deskIcon(desk: PoolDesk): IconName {
    return desk.accessibleOnly ? 'Accessibility' : desk.popular ? 'Star' : 'LayoutGrid';
  }

  onDragStart(event: DragEvent, payload: { kind: 'desk' | 'meetingRoom'; id: string }): void {
    event.dataTransfer?.setData(POOL_DRAG_TYPE, JSON.stringify(payload));
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  chipClass(selected: boolean, tone: 'desk' | 'room' | 'closed'): string {
    return cn(
      'flex cursor-grab items-center gap-1.5 rounded-lg border px-2.5 py-1.5 text-[12.5px] font-medium transition-colors active:cursor-grabbing',
      selected
        ? 'border-brand-500 bg-brand-600 text-white'
        : tone === 'room'
          ? 'border-blue-200 bg-blue-50 text-blue-800'
          : tone === 'closed'
            ? 'border-red-200 bg-red-50 text-red-700'
            : 'border-line bg-slate-50 text-ink-700',
    );
  }
}
