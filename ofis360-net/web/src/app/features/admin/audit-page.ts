import { Component, computed, effect, inject, untracked } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { formatDateTimeLabel, relativeTimeLabel } from '../../lib/date';
import { pageWindow } from '../../lib/pagination';
import { ROLE_LABELS, type Role } from '../../lib/enums';
import { cn } from '../../lib/utils';
import { ApiClient } from '../../core/api/api-client';
import type { AuditLogDto, Paged } from '../../core/api/types';
import { PageState, UiAvatar, UiBadge, UiCard, UiCardBody, UiCardHeader, UiDataTable, UiEmptyState, UiIcon, UiPageError, UiPageHeader, UiPageLoading, UiTd } from '../../shared/ui';
import { AdminFilterLink } from './admin-shared';

/** Kaynak: (app)/admin/audit/page.tsx */

const ENTITY_FILTERS = [
  { value: '', label: 'Tümü' },
  { value: 'DeskReservation', label: 'Masa rezervasyonu' },
  { value: 'RoomReservation', label: 'Toplantı rezervasyonu' },
  { value: 'Desk', label: 'Masa' },
  { value: 'MeetingRoom', label: 'Toplantı odası' },
  { value: 'Floor', label: 'Kat' },
  { value: 'ReservationRule', label: 'Kural' },
  { value: 'Closure', label: 'Kapatma' },
  { value: 'User', label: 'Kullanıcı' },
  { value: 'SystemSetting', label: 'Ayar' },
  { value: 'Report', label: 'Rapor' },
];
const PAGE_SIZE = 40;

@Component({
  selector: 'app-audit-page',
  imports: [RouterLink, UiPageHeader, UiPageLoading, UiPageError, UiCard, UiCardHeader, UiCardBody, UiDataTable, UiTd, UiEmptyState, UiAvatar, UiBadge, UiIcon, AdminFilterLink],
  template: `
    <ui-page-header title="Denetim kayıtları" description="Sistemde yapılan tüm yönetsel işlemler, kim tarafından ve ne zaman yapıldığıyla birlikte kaydedilir." />

    <div ui-card class="mb-5">
      <ui-card-body>
        <div class="flex flex-wrap items-center gap-2">
          <span class="text-xs font-semibold uppercase tracking-wide text-ink-400">Kayıt türü</span>
          @for (filter of filters; track filter.value) {
            <a adminFilterLink routerLink="/admin/audit" [queryParams]="filter.value ? { entity: filter.value } : {}" [active]="entity() === filter.value">{{ filter.label }}</a>
          }
        </div>
      </ui-card-body>
    </div>

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as d) {
      <div ui-card>
        <ui-card-header title="Sistem hareketleri" [description]="d.total + ' kayıt · sayfa ' + page() + '/' + pageCount()" hasIcon>
          <svg uiIcon icon name="FileClock" class="h-4.5 w-4.5"></svg>
        </ui-card-header>
        <ui-card-body class="p-0">
          @if (d.items.length === 0) {
            <ui-empty-state title="Kayıt bulunamadı" description="Seçilen filtreye uyan hareket yok." />
          } @else {
            <ui-data-table caption="Denetim kayıtları" [headers]="[{ label: 'Zaman' }, { label: 'Kullanıcı' }, { label: 'İşlem' }, { label: 'Açıklama' }]">
              @for (log of d.items; track log.id) {
                <tr class="hover:bg-slate-50/60">
                  <td ui-td>
                    <span class="block text-ink-900">{{ relative(log.createdAt) }}</span>
                    <span class="block text-xs text-ink-500">{{ dateTime(log.createdAt) }}</span>
                  </td>
                  <td ui-td>
                    <span class="flex items-center gap-2">
                      <ui-avatar [name]="log.actorName" size="sm" />
                      <span class="min-w-0">
                        <span class="block truncate font-medium text-ink-900">{{ log.actorName }}</span>
                        <span class="block text-xs text-ink-500">{{ roleLabel(log.actorRole) }}</span>
                      </span>
                    </span>
                  </td>
                  <td ui-td><span ui-badge tone="neutral">{{ log.actionLabel }}</span></td>
                  <td ui-td class="max-w-[420px]">{{ log.summary }}</td>
                </tr>
              }
            </ui-data-table>
          }
        </ui-card-body>
      </div>

      @if (pageCount() > 1) {
        <nav class="mt-4 flex flex-wrap items-center justify-center gap-2" aria-label="Sayfalama">
          @for (item of pageNumbers(); track item; let i = $index) {
            @if (i > 0 && item - pageNumbers()[i - 1] > 1) {
              <span class="px-1 text-sm text-ink-500" aria-hidden="true">…</span>
            }
            <a
              routerLink="/admin/audit"
              [queryParams]="entity() ? { entity: entity(), page: item } : { page: item }"
              [attr.aria-current]="item === page() ? 'page' : null"
              [class]="cn('flex h-9 min-w-9 items-center justify-center rounded-lg border px-2 text-sm font-medium', item === page() ? 'border-brand-500 bg-brand-600 text-white' : 'border-line bg-white text-ink-700 hover:bg-brand-50')"
            >
              {{ item }}
            </a>
          }
        </nav>
      }
    }
  `,
})
export class AuditPage {
  private readonly api = inject(ApiClient);
  private readonly route = inject(ActivatedRoute);
  readonly cn = cn;
  readonly filters = ENTITY_FILTERS;
  readonly state = new PageState<Paged<AuditLogDto>>();

  readonly query = toSignal(this.route.queryParamMap.pipe(map((p) => ({ entity: p.get('entity') ?? '', page: p.get('page') }))), {
    initialValue: { entity: '', page: null as string | null },
  });
  readonly entity = computed(() => this.query().entity);
  readonly page = computed(() => Math.max(1, Number(this.query().page ?? '1') || 1));
  readonly pageCount = computed(() => Math.max(1, Math.ceil((this.state.data()?.total ?? 0) / PAGE_SIZE)));
  readonly pageNumbers = computed(() => pageWindow(this.page(), this.pageCount()));

  constructor() {
    effect(() => {
      this.query();
      void this.load(untracked(() => this.state.data()) !== null);
    });
  }

  roleLabel(role: string): string {
    return ROLE_LABELS[role as Role] ?? role;
  }

  relative(iso: string): string {
    return relativeTimeLabel(new Date(iso));
  }

  dateTime(iso: string): string {
    return formatDateTimeLabel(new Date(iso));
  }

  load(silent = false): Promise<unknown> {
    return this.state.load(() => this.api.get<Paged<AuditLogDto>>('admin/audit', { entityType: this.entity() || undefined, take: PAGE_SIZE, skip: (this.page() - 1) * PAGE_SIZE }), { silent });
  }
}
