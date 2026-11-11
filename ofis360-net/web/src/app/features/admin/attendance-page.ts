import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { addDaysToKey, formatLongDate, todayKey } from '../../lib/date';
import { ApiClient } from '../../core/api/api-client';
import type { DepartmentDto } from '../../core/api/types';
import { PageState, UiAlert, UiButton, UiCard, UiCardBody, UiCardHeader, UiDataTable, UiEmptyState, UiIcon, UiInput, UiPageError, UiPageHeader, UiPageLoading, UiSelect, UiTd } from '../../shared/ui';
import { AdminStatTile } from './admin-shared';

interface AttendanceReport {
  from: string;
  to: string;
  personDetailEnabled: boolean;
  lateCancelMinutes: number;
  totals: { deskAttended: number; deskNoShow: number; deskNoShowRate: number; roomHeld: number; roomEmpty: number; roomEmptyRate: number; lateCancels: number };
  departments: Array<{ name: string; deskAttended: number; deskNoShow: number; deskNoShowRate: number; roomHeld: number; roomEmpty: number; lateCancels: number }>;
  rooms: Array<{ code: string; name: string; held: number; empty: number; emptyRate: number; lateCancels: number }>;
  weeks: Array<{ weekStart: string; deskNoShow: number; roomEmpty: number; lateCancels: number }>;
  people?: Array<{ fullName: string; department: string; deskNoShow: number; roomEmpty: number; lateCancels: number }> | null;
}

/** Gelmeme, boş toplantı ve geç iptal raporu. Kişi adları ayarla açılır. */
@Component({
  selector: 'app-attendance-page',
  imports: [FormsModule, RouterLink, UiPageHeader, UiPageLoading, UiPageError, UiAlert, UiButton, UiCard, UiCardBody, UiCardHeader, UiDataTable, UiEmptyState, UiIcon, UiInput, UiSelect, UiTd, AdminStatTile],
  template: `
    <ui-page-header title="Katılım raporu" description="Gelinmeyen masalar, boş toplantılar ve geç iptaller." />
    @if (feedConfigured() === false) {
      <ui-alert tone="warning" class="mb-5" title="Turnike bağlı değil" hasIcon>
      Otomatik masa bırakma turnike verisi olmadan çalışmaz; kayıtlar gelmedi sayılmaz. Çalışanlar uygulamadan giriş yapabilir.
    </ui-alert>
    }

    <div ui-card class="mb-5">
      <ui-card-body>
        <form class="flex flex-wrap items-end gap-3" (ngSubmit)="apply()">
          <label class="block">
            <span class="mb-1 block text-xs font-medium text-ink-500">Başlangıç</span>
            <input ui-input class="w-44" type="date" name="from" [(ngModel)]="from" />
          </label>
          <label class="block">
            <span class="mb-1 block text-xs font-medium text-ink-500">Bitiş</span>
            <input ui-input class="w-44" type="date" name="to" [(ngModel)]="to" />
          </label>
          <label class="block">
            <span class="mb-1 block text-xs font-medium text-ink-500">Departman</span>
            <select ui-select class="w-64" name="department" [(ngModel)]="department">
              <option value="">Tümü</option>
              @for (item of departments(); track item.id) {
                <option [value]="item.id">{{ item.name }}</option>
              }
            </select>
          </label>
          <button ui-button type="submit">
            <svg uiIcon name="Search" class="h-4 w-4"></svg>
            Uygula
          </button>
        </form>
      </ui-card-body>
    </div>

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as r) {
      <div class="mb-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <admin-stat-tile label="Gelinmeyen masa" [value]="r.totals.deskNoShow" [hint]="'%' + r.totals.deskNoShowRate.toFixed(1) + ' · ' + r.totals.deskAttended + ' geliş'" />
        <admin-stat-tile label="Boş toplantı" [value]="r.totals.roomEmpty" [hint]="'%' + r.totals.roomEmptyRate.toFixed(1) + ' · ' + r.totals.roomHeld + ' toplantı'" />
        <admin-stat-tile label="Geç iptal" [value]="r.totals.lateCancels" [hint]="'Başlangıca ' + r.lateCancelMinutes + ' dakikadan az kala'" />
      </div>

      <div class="space-y-5">
        <div ui-card>
          <ui-card-header title="Departmanlar" description="En çok kayıp olandan aza">
            <a ui-button size="sm" variant="secondary" [href]="exportHref('departman')">CSV indir</a>
          </ui-card-header>
          <ui-card-body>
            @if (r.departments.length === 0) {
              <ui-empty-state title="Kayıt yok" description="Seçilen dönemde veri bulunmuyor." />
            } @else {
              <ui-data-table caption="Departmanlara göre katılım" [headers]="[{ label: 'Departman' }, { label: 'Gelinen masa' }, { label: 'Gelinmeyen masa' }, { label: 'Oran' }, { label: 'Toplantı' }, { label: 'Boş toplantı' }, { label: 'Geç iptal' }]">
                @for (row of r.departments; track row.name) {
                  <tr>
                    <td ui-td class="font-medium text-ink-900">{{ row.name }}</td>
                    <td ui-td class="tabular-nums">{{ row.deskAttended }}</td>
                    <td ui-td class="tabular-nums">{{ row.deskNoShow }}</td>
                    <td ui-td class="tabular-nums">%{{ row.deskNoShowRate.toFixed(1) }}</td>
                    <td ui-td class="tabular-nums">{{ row.roomHeld }}</td>
                    <td ui-td class="tabular-nums">{{ row.roomEmpty }}</td>
                    <td ui-td class="tabular-nums">{{ row.lateCancels }}</td>
                  </tr>
                }
              </ui-data-table>
            }
          </ui-card-body>
        </div>

        <div ui-card>
          <ui-card-header title="Toplantı odaları">
            <a ui-button size="sm" variant="secondary" [href]="exportHref('oda')">CSV indir</a>
          </ui-card-header>
          <ui-card-body>
            @if (r.rooms.length === 0) {
              <ui-empty-state title="Kayıt yok" description="Seçilen dönemde veri bulunmuyor." />
            } @else {
              <ui-data-table caption="Odalara göre boş toplantı" [headers]="[{ label: 'Oda' }, { label: 'Toplantı' }, { label: 'Boş toplantı' }, { label: 'Oran' }, { label: 'Geç iptal' }]">
                @for (row of r.rooms; track row.code) {
                  <tr>
                    <td ui-td><span class="font-medium text-ink-900">{{ row.name }}</span> <span class="ml-1 text-xs text-ink-500">{{ row.code }}</span></td>
                    <td ui-td class="tabular-nums">{{ row.held }}</td>
                    <td ui-td class="tabular-nums">{{ row.empty }}</td>
                    <td ui-td class="tabular-nums">%{{ row.emptyRate.toFixed(1) }}</td>
                    <td ui-td class="tabular-nums">{{ row.lateCancels }}</td>
                  </tr>
                }
              </ui-data-table>
            }
          </ui-card-body>
        </div>

        <div ui-card>
          <ui-card-header title="Haftalar">
            <a ui-button size="sm" variant="secondary" [href]="exportHref('hafta')">CSV indir</a>
          </ui-card-header>
          <ui-card-body>
            @if (r.weeks.length === 0) {
              <ui-empty-state title="Kayıt yok" description="Seçilen dönemde veri bulunmuyor." />
            } @else {
              <ui-data-table caption="Haftalara göre katılım" [headers]="[{ label: 'Hafta başlangıcı' }, { label: 'Gelinmeyen masa' }, { label: 'Boş toplantı' }, { label: 'Geç iptal' }]">
                @for (row of r.weeks; track row.weekStart) {
                  <tr>
                    <td ui-td>{{ formatLongDate(row.weekStart, { withWeekday: false }) }}</td>
                    <td ui-td class="tabular-nums">{{ row.deskNoShow }}</td>
                    <td ui-td class="tabular-nums">{{ row.roomEmpty }}</td>
                    <td ui-td class="tabular-nums">{{ row.lateCancels }}</td>
                  </tr>
                }
              </ui-data-table>
            }
          </ui-card-body>
        </div>

        @if (r.people; as people) {
          <div ui-card>
            <ui-card-header title="Kişiler" description="Kişisel veri içerir; yalnızca yetkili kullanım içindir.">
              <a ui-button size="sm" variant="secondary" [href]="exportHref('kisi')">CSV indir</a>
            </ui-card-header>
            <ui-card-body>
              @if (people.length === 0) {
                <ui-empty-state title="Kayıt yok" description="Seçilen dönemde kayıp bulunmuyor." />
              } @else {
                <ui-data-table caption="Kişilere göre katılım" [headers]="[{ label: 'Ad soyad' }, { label: 'Departman' }, { label: 'Gelinmeyen masa' }, { label: 'Boş toplantı' }, { label: 'Geç iptal' }]">
                  @for (row of people; track $index) {
                    <tr>
                      <td ui-td class="font-medium text-ink-900">{{ row.fullName }}</td>
                      <td ui-td>{{ row.department }}</td>
                      <td ui-td class="tabular-nums">{{ row.deskNoShow }}</td>
                      <td ui-td class="tabular-nums">{{ row.roomEmpty }}</td>
                      <td ui-td class="tabular-nums">{{ row.lateCancels }}</td>
                    </tr>
                  }
                </ui-data-table>
              }
            </ui-card-body>
          </div>
        } @else {
          <ui-alert tone="info" title="Kişi bazlı görünüm kapalı">
            İK ve KVKK onayı alındığında <a routerLink="/admin/ayarlar" class="font-medium underline">Sistem ayarları</a> sayfasından açılabilir.
          </ui-alert>
        }
      </div>
    }
  `,
})
export class AttendancePage {
  private readonly api = inject(ApiClient);

  readonly formatLongDate = formatLongDate;
  readonly state = new PageState<AttendanceReport>();
  readonly departments = signal<DepartmentDto[]>([]);

  from = addDaysToKey(todayKey(), -29);
  to = todayKey();
  department = '';

  private readonly applied = signal({ from: this.from, to: this.to, department: this.department });
  private readonly params = computed(() => {
    const a = this.applied();
    return { from: a.from || undefined, to: a.to || undefined, departmentId: a.department || undefined };
  });

  readonly feedConfigured = signal<boolean | null>(null);

  constructor() {
    void this.api.get<{ configured: boolean }>('admin/rules/attendance-feed').then((r) => this.feedConfigured.set(r.configured));
    void this.api.get<DepartmentDto[]>('admin/departments').then((items) => this.departments.set(items));
    effect(() => {
      this.params();
      void this.load();
    });
  }

  load(): Promise<unknown> {
    return this.state.load(() => this.api.get<AttendanceReport>('admin/attendance-report', this.params()));
  }

  apply(): void {
    this.applied.set({ from: this.from, to: this.to, department: this.department });
  }

  exportHref(type: string): string {
    const query = new URLSearchParams({ type });
    for (const [key, value] of Object.entries(this.params())) {
      if (value) query.set(key, value);
    }
    return `/api/admin/attendance-report/export?${query.toString()}`;
  }
}
