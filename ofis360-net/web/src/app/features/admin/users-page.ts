import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { GRADES, GRADE_LABELS, ROLES, ROLE_LABELS, type Grade, type Role } from '../../lib/enums';
import { cn } from '../../lib/utils';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import type { DepartmentDto, Paged, UserDto, UserImportResult, UserStats } from '../../core/api/types';
import {
  PageState,
  ToastService,
  UiAvatar,
  UiBadge,
  UiButton,
  UiCard,
  UiCardBody,
  UiCardHeader,
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
  UiSwitch,
  UiTd,
} from '../../shared/ui';
import { AdminFilterLink, AdminFormError, AdminPageNav, AdminStatTile } from './admin-shared';

/** Kaynak: (app)/admin/kullanicilar/page.tsx + components/admin/user-manager.tsx */

interface UserForm {
  fullName: string;
  email: string;
  title: string;
  role: Role;
  departmentId: string;
  grade: Grade | null;
  isSecretary: boolean;
  accessibilityApproved: boolean;
  active: boolean;
}

interface UserQuery {
  role: string | null;
  department: string | null;
  kademe: string | null;
  q: string | null;
  durum: string | null;
  sayfa: string | null;
}

const IMPORT_TEMPLATE = '﻿e-posta;ad soyad;unvan;kademe;departman;sekreter\r\nornek.kisi@sirket.com;Örnek Kişi;Sigorta Uzmanı;Uzman;Hasar;Hayır\r\n';

const PAGE_SIZE = 50;

interface PageData {
  users: Paged<UserDto>;
  departments: DepartmentDto[];
  stats: UserStats;
}

@Component({
  selector: 'app-users-page',
  imports: [
    FormsModule,
    RouterLink,
    UiPageHeader,
    UiPageLoading,
    UiPageError,
    UiCard,
    UiCardHeader,
    UiCardBody,
    UiDataTable,
    UiTd,
    UiEmptyState,
    UiBadge,
    UiAvatar,
    UiButton,
    UiIcon,
    UiModal,
    UiField,
    UiInput,
    UiSelect,
    UiSwitch,
    AdminFilterLink,
    AdminPageNav,
    AdminStatTile,
    AdminFormError,
  ],
  template: `
    <ui-page-header title="Kullanıcı ve rol yönetimi" description="Çalışan hesapları, roller, kademeler ve erişilebilirlik yetkileri." />

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as d) {
      @let q = query();
      <div class="mb-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <admin-stat-tile label="Toplam çalışan" [value]="d.stats.total" />
        <admin-stat-tile label="Aktif" [value]="d.stats.active" [hint]="d.stats.passive + ' pasif / izinli'" />
        <admin-stat-tile label="Yönetici" [value]="d.stats.managers" [hint]="d.stats.admins + ' admin'" />
        <admin-stat-tile label="Kademesi atanmamış" [value]="d.stats.withoutGrade" [hint]="d.stats.secretaries + ' sekreter'" />
        <admin-stat-tile label="Erişilebilirlik yetkili" [value]="d.stats.accessibility" />
      </div>

      <div ui-card class="mb-5">
        <ui-card-body>
          <div class="flex flex-wrap items-center gap-2">
            <span class="text-xs font-semibold uppercase tracking-wide text-ink-400">Rol</span>
            <a adminFilterLink routerLink="/admin/kullanicilar" [active]="!q.role && !q.department && !q.durum && !q.kademe">Tümü ({{ d.stats.total }})</a>
            @for (role of roles; track role) {
              <a adminFilterLink routerLink="/admin/kullanicilar" [queryParams]="{ role }" [active]="q.role === role">{{ roleLabels[role] }}</a>
            }
            <span class="mx-1 h-5 w-px bg-line" aria-hidden="true"></span>
            <span class="text-xs font-semibold uppercase tracking-wide text-ink-400">Departman</span>
            @for (department of d.departments; track department.id) {
              <a adminFilterLink routerLink="/admin/kullanicilar" [queryParams]="{ department: department.id }" [active]="q.department === department.id">{{ department.name }} ({{ department.userCount }})</a>
            }
            <span class="mx-1 h-5 w-px bg-line" aria-hidden="true"></span>
            <a adminFilterLink routerLink="/admin/kullanicilar" [queryParams]="{ durum: 'pasif' }" [active]="q.durum === 'pasif'">Pasif / izinli ({{ d.stats.passive }})</a>
          </div>
          <div class="mt-3 flex flex-wrap items-center gap-2">
            <span class="text-xs font-semibold uppercase tracking-wide text-ink-400">Kademe</span>
            @for (grade of grades; track grade) {
              <a adminFilterLink routerLink="/admin/kullanicilar" [queryParams]="{ kademe: grade }" [active]="q.kademe === grade">{{ gradeLabels[grade] }}</a>
            }
            <a adminFilterLink routerLink="/admin/kullanicilar" [queryParams]="{ kademe: 'NONE' }" [active]="q.kademe === 'NONE'">Atanmamış ({{ d.stats.withoutGrade }})</a>
            <a adminFilterLink routerLink="/admin/kullanicilar" [queryParams]="{ kademe: 'SECRETARY' }" [active]="q.kademe === 'SECRETARY'">Sekreter ({{ d.stats.secretaries }})</a>
          </div>
          <form class="mt-3 flex flex-wrap items-center gap-2" (ngSubmit)="search()">
            <label for="user-search" class="text-xs font-semibold uppercase tracking-wide text-ink-400">Ara</label>
            <input id="user-search" name="q" [(ngModel)]="searchText" placeholder="Ad, e-posta veya unvan" class="h-9 w-64 rounded-lg border border-line px-3 text-[13px] outline-none focus:border-brand-400" />
            <button type="submit" class="h-9 rounded-lg border border-line bg-white px-3 text-[13px] font-medium text-ink-700 hover:border-brand-200 hover:bg-brand-50">Ara</button>
          </form>
        </ui-card-body>
      </div>

      <div ui-card>
        <ui-card-header title="Kullanıcılar" [description]="d.users.total + ' kayıt · ' + page() + '/' + pageCount() + '. sayfa (' + d.users.items.length + ' gösteriliyor)'" hasIcon>
          <svg uiIcon icon name="Users" class="h-4.5 w-4.5"></svg>
        </ui-card-header>
        <ui-card-body>
          <div class="mb-4 flex flex-wrap justify-end gap-2">
            <button ui-button type="button" variant="secondary" (click)="openImport()">
              <svg uiIcon name="Upload" class="h-4 w-4"></svg>
              Toplu yükle
            </button>
            <button ui-button type="button" (click)="openCreate()">
              <svg uiIcon name="Plus" class="h-4 w-4"></svg>
              Kullanıcı ekle
            </button>
          </div>

          @if (d.users.items.length === 0) {
            <ui-empty-state title="Kullanıcı bulunamadı" description="Arama kriterlerini değiştirin." />
          } @else {
            <ui-data-table caption="Kullanıcı listesi" [headers]="[{ label: 'Çalışan' }, { label: 'Departman' }, { label: 'Kademe' }, { label: 'Rol' }, { label: 'Rezervasyon' }, { label: 'Durum' }, { label: '', className: 'text-right' }]">
              @for (user of d.users.items; track user.id) {
                <tr [class]="cn('hover:bg-slate-50/60', !user.active && 'bg-slate-50')">
                  <td ui-td>
                    <span class="flex items-center gap-2.5">
                      <ui-avatar [name]="user.fullName" size="sm" />
                      <span class="min-w-0">
                        <span class="block truncate font-medium text-ink-900">
                          {{ user.fullName }}
                          @if (user.isDemoAccount) {
                            <span class="ml-1.5 text-[11px] font-medium text-brand-600">demo</span>
                          }
                        </span>
                        <span class="block truncate text-xs text-ink-500">{{ user.email }}</span>
                      </span>
                    </span>
                  </td>
                  <td ui-td>
                    {{ user.department?.name }}
                    <span class="block text-xs text-ink-500">{{ user.title }}</span>
                  </td>
                  <td ui-td>
                    <span class="flex flex-wrap items-center gap-1.5">
                      @if (user.gradeLabel) {
                        <span class="text-[13px] text-ink-700">{{ user.gradeLabel }}</span>
                      } @else {
                        <span class="text-xs text-ink-400">Atanmamış</span>
                      }
                      @if (user.isSecretary) {
                        <span ui-badge tone="brand">Sekreter</span>
                      }
                    </span>
                  </td>
                  <td ui-td>
                    <span class="flex flex-wrap items-center gap-1.5">
                      <span ui-badge [tone]="user.role === 'ADMIN' ? 'brand' : user.role === 'MANAGER' ? 'accent' : 'neutral'">{{ roleLabels[user.role] }}</span>
                      @if (user.accessibilityApproved) {
                        <span ui-badge tone="violet">
                          <svg uiIcon name="Accessibility" class="h-3 w-3"></svg>
                          Erişilebilirlik
                        </span>
                      }
                    </span>
                  </td>
                  <td ui-td class="tabular-nums text-ink-500">{{ user.deskReservationCount }} masa · {{ user.roomReservationCount }} oda</td>
                  <td ui-td>
                    <span ui-badge [tone]="user.active ? 'success' : 'danger'">{{ user.active ? 'Aktif' : 'Pasif' }}</span>
                  </td>
                  <td ui-td class="text-right">
                    <button ui-button type="button" size="sm" variant="secondary" (click)="openEdit(user)">
                      <svg uiIcon name="Pencil" class="h-3.5 w-3.5"></svg>
                      Düzenle
                    </button>
                  </td>
                </tr>
              }
            </ui-data-table>
          }

          @if (pageCount() > 1) {
            <admin-page-nav class="mt-4 border-t border-line pt-4" aria-label="Kullanıcı listesi sayfaları" [page]="page()" [pageCount]="pageCount()" [skip]="d.users.skip" [shown]="d.users.items.length" [total]="d.users.total" [query]="{ role: q.role, department: q.department, q: q.q, durum: q.durum, kademe: q.kademe }" />
          }
        </ui-card-body>
      </div>

      <ui-modal [open]="formOpen()" (closed)="closeForm()" [title]="editingId() ? 'Kullanıcıyı düzenle' : 'Yeni kullanıcı'" description="Rol ve yetki değişiklikleri denetim kaydına yazılır.">
        <div class="space-y-4">
          @if (formError()) {
            <admin-form-error>{{ formError() }}</admin-form-error>
          }

          <ui-field label="Ad soyad" required htmlFor="user-name">
            <input ui-input id="user-name" [(ngModel)]="form.fullName" />
          </ui-field>

          <div class="grid gap-4 sm:grid-cols-2">
            <ui-field label="E-posta" required htmlFor="user-email">
              <input ui-input id="user-email" type="email" [(ngModel)]="form.email" />
            </ui-field>
            <ui-field label="Unvan" required htmlFor="user-title">
              <input ui-input id="user-title" [(ngModel)]="form.title" />
            </ui-field>
          </div>

          <div class="grid gap-4 sm:grid-cols-2">
            <ui-field label="Departman" required htmlFor="user-department">
              <select ui-select id="user-department" [(ngModel)]="form.departmentId">
                @for (department of d.departments; track department.id) {
                  <option [value]="department.id">{{ department.name }}</option>
                }
              </select>
            </ui-field>
            <ui-field label="Rol" required htmlFor="user-role">
              <select ui-select id="user-role" [(ngModel)]="form.role">
                @for (role of roles; track role) {
                  <option [value]="role">{{ roleLabels[role] }}</option>
                }
              </select>
            </ui-field>
          </div>

          <ui-field label="Kademe" htmlFor="user-grade">
            <select ui-select id="user-grade" [(ngModel)]="form.grade">
              <option [ngValue]="null">Atanmamış</option>
              @for (grade of grades; track grade) {
                <option [ngValue]="grade">{{ gradeLabels[grade] }}</option>
              }
            </select>
          </ui-field>

          <div class="space-y-2">
            <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
              <span>
                <span class="block text-sm text-ink-900">Sekreter yetkisi</span>
                <span class="block text-xs text-ink-500">Başkaları adına rezervasyon işlemleri yapar.</span>
              </span>
              <ui-switch [checked]="form.isSecretary" (checkedChange)="form.isSecretary = $event" label="Sekreter yetkisi" />
            </label>
            <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
              <span>
                <span class="block text-sm text-ink-900">Erişilebilirlik yetkisi</span>
                <span class="block text-xs text-ink-500">Erişilebilirlik için ayrılmış masaları seçebilir.</span>
              </span>
              <ui-switch [checked]="form.accessibilityApproved" (checkedChange)="form.accessibilityApproved = $event" label="Erişilebilirlik yetkisi" />
            </label>
            <label class="flex items-center justify-between gap-4 rounded-lg border border-line px-3 py-2.5">
              <span>
                <span class="block text-sm text-ink-900">Hesap aktif</span>
                <span class="block text-xs text-ink-500">Pasif hesaplar oturum açamaz ve rezervasyon oluşturamaz.</span>
              </span>
              <ui-switch [checked]="form.active" (checkedChange)="form.active = $event" label="Hesap aktif" />
            </label>
          </div>
        </div>
        <ng-container footer>
          <button ui-button type="button" variant="secondary" (click)="closeForm()" [disabled]="pending()">Vazgeç</button>
          <button ui-button type="button" (click)="submit()" [loading]="pending()">
            <svg uiIcon name="Save" class="h-4 w-4"></svg>
            Kaydet
          </button>
        </ng-container>
      </ui-modal>

      <ui-modal [open]="importOpen()" (closed)="closeImport()" size="xl" title="Personel listesini toplu yükle" description="Önce önizleme yapılır. Tek bir satır hatalıysa hiçbir kayıt yazılmaz.">
        <div class="space-y-4">
          @if (importError()) {
            <admin-form-error>{{ importError() }}</admin-form-error>
          }

          <div class="rounded-lg border border-line bg-slate-50/60 p-3 text-[13px] text-ink-700">
            <p>Excel'de <strong>Farklı Kaydet &gt; CSV UTF-8</strong> seçin. Sütunlar: e-posta (zorunlu), ad soyad, unvan, kademe, departman, sekreter.</p>
            <p class="mt-1 text-ink-500">E-postaya göre eşleşir. Boş hücre mevcut değeri korur. Listede olmayan kişilere, rollere ve aktif/pasif durumuna dokunulmaz.</p>
            <button type="button" (click)="downloadTemplate()" class="mt-2 inline-flex items-center gap-1.5 text-[13px] font-medium text-brand-700 hover:underline">
              <svg uiIcon name="Download" class="h-3.5 w-3.5"></svg>
              Örnek dosyayı indir
            </button>
          </div>

          <ui-field label="Dosya" htmlFor="user-import-file">
            <input id="user-import-file" type="file" accept=".csv,.txt,text/csv" (change)="onImportFile($event)" class="block w-full text-sm text-ink-700 file:mr-3 file:rounded-lg file:border-0 file:bg-brand-50 file:px-3 file:py-2 file:text-[13px] file:font-medium file:text-brand-700" />
          </ui-field>

          @if (importResult(); as r) {
            @if (r.fileError) {
              <admin-form-error>{{ r.fileError }}</admin-form-error>
            } @else {
              <div class="grid gap-3 sm:grid-cols-4">
                <admin-stat-tile label="Yeni" [value]="r.creates" />
                <admin-stat-tile label="Güncellenecek" [value]="r.updates" />
                <admin-stat-tile label="Değişiklik yok" [value]="r.unchanged" />
                <admin-stat-tile label="Hatalı" [value]="r.errors" />
              </div>
              @if (r.newDepartments.length > 0) {
                <p class="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-[13px] text-amber-900">
                  Bu departmanlar yeni oluşturulacak, yazımlarını kontrol edin: {{ r.newDepartments.join(', ') }}
                </p>
              }
              @if (importRows().length > 0) {
                <div class="max-h-72 overflow-y-auto rounded-lg border border-line scrollbar-thin">
                  <table class="w-full text-left text-[13px]">
                    <caption class="sr-only">Yükleme önizlemesi</caption>
                    <thead class="sticky top-0 bg-slate-50 text-xs text-ink-500">
                      <tr>
                        <th scope="col" class="px-3 py-2">Satır</th>
                        <th scope="col" class="px-3 py-2">E-posta</th>
                        <th scope="col" class="px-3 py-2">İşlem</th>
                        <th scope="col" class="px-3 py-2">Ayrıntı</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (row of importRows(); track row.line) {
                        <tr class="border-t border-line align-top">
                          <td class="px-3 py-2 tabular-nums text-ink-500">{{ row.line }}</td>
                          <td class="px-3 py-2 break-all">{{ row.email || '(boş)' }}</td>
                          <td class="px-3 py-2">
                            <span ui-badge [tone]="row.action === 'ERROR' ? 'danger' : row.action === 'CREATE' ? 'success' : 'accent'">{{ importActionLabels[row.action] }}</span>
                          </td>
                          <td class="px-3 py-2" [class.text-red-700]="row.action === 'ERROR'">{{ (row.action === 'ERROR' ? row.errors : row.changes).join(' · ') }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
                @if (importHiddenCount() > 0) {
                  <p class="text-xs text-ink-500">İlk {{ importRows().length }} satır gösteriliyor, {{ importHiddenCount() }} satır daha var.</p>
                }
              }
            }
          }
        </div>
        <ng-container footer>
          <button ui-button type="button" variant="secondary" (click)="closeImport()" [disabled]="importPending()">Kapat</button>
          <button ui-button type="button" (click)="applyImport()" [loading]="importPending()" [disabled]="!canApplyImport()">
            <svg uiIcon name="Save" class="h-4 w-4"></svg>
            Uygula
          </button>
        </ng-container>
      </ui-modal>
    }
  `,
})
export class UsersPage {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly cn = cn;
  readonly roles = ROLES;
  readonly roleLabels = ROLE_LABELS;
  readonly grades = GRADES;
  readonly gradeLabels = GRADE_LABELS;
  readonly importActionLabels = { CREATE: 'Yeni', UPDATE: 'Güncelleme', UNCHANGED: 'Değişiklik yok', ERROR: 'Hata' };
  readonly state = new PageState<PageData>();

  readonly query = toSignal(
    this.route.queryParamMap.pipe(map((p): UserQuery => ({ role: p.get('role'), department: p.get('department'), kademe: p.get('kademe'), q: p.get('q'), durum: p.get('durum'), sayfa: p.get('sayfa') }))),
    { initialValue: { role: null, department: null, kademe: null, q: null, durum: null, sayfa: null } as UserQuery },
  );
  readonly page = computed(() => Math.max(1, Number(this.query().sayfa ?? '1') || 1));
  readonly pageCount = computed(() => Math.max(1, Math.ceil((this.state.data()?.users.total ?? 0) / PAGE_SIZE)));
  searchText = '';

  readonly formOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formError = signal<string | null>(null);
  readonly pending = signal(false);
  form: UserForm = this.emptyForm();

  readonly importOpen = signal(false);
  readonly importPending = signal(false);
  readonly importError = signal<string | null>(null);
  readonly importResult = signal<UserImportResult | null>(null);
  private importCsv: string | null = null;

  /** Önce hatalar, sonra değişecek satırlar; değişmeyenler listelenmez. */
  private readonly importVisible = computed(() => {
    const rows = this.importResult()?.rows ?? [];
    return [...rows.filter((r) => r.action === 'ERROR'), ...rows.filter((r) => r.action === 'CREATE' || r.action === 'UPDATE')];
  });
  readonly importRows = computed(() => this.importVisible().slice(0, 300));
  readonly importHiddenCount = computed(() => Math.max(0, this.importVisible().length - 300));
  readonly canApplyImport = computed(() => {
    const r = this.importResult();
    return !!r && !r.fileError && !r.applied && r.errors === 0 && r.creates + r.updates > 0;
  });

  constructor() {
    effect(() => {
      this.query();
      void this.load(untracked(() => this.state.data()) !== null);
    });
  }

  private emptyForm(): UserForm {
    return { fullName: '', email: '', title: '', role: 'EMPLOYEE', departmentId: this.state.data()?.departments[0]?.id ?? '', grade: null, isSecretary: false, accessibilityApproved: false, active: true };
  }

  load(silent = false): Promise<unknown> {
    const q = this.query();
    this.searchText = q.q ?? '';
    return this.state.load(
      async () => {
        const [users, departments, stats] = await Promise.all([
          this.api.get<Paged<UserDto>>('admin/users', {
            role: q.role,
            departmentId: q.department,
            grade: q.kademe,
            search: q.q,
            status: q.durum === 'pasif' ? 'passive' : q.durum === 'aktif' ? 'active' : undefined,
            take: PAGE_SIZE,
            skip: (this.page() - 1) * PAGE_SIZE,
          }),
          this.api.get<DepartmentDto[]>('admin/departments'),
          this.api.get<UserStats>('admin/users/stats'),
        ]);
        return { users, departments, stats };
      },
      { silent },
    );
  }

  search(): void {
    const q = this.query();
    void this.router.navigate([], { relativeTo: this.route, queryParams: { role: q.role, department: q.department, durum: q.durum, kademe: q.kademe, q: this.searchText.trim() || null } });
  }

  openCreate(): void {
    this.form = this.emptyForm();
    this.formError.set(null);
    this.editingId.set(null);
    this.formOpen.set(true);
  }

  openEdit(user: UserDto): void {
    this.form = {
      fullName: user.fullName,
      email: user.email,
      title: user.title,
      role: user.role,
      departmentId: user.departmentId,
      grade: (user.grade ?? null) as Grade | null,
      isSecretary: user.isSecretary,
      accessibilityApproved: user.accessibilityApproved,
      active: user.active,
    };
    this.formError.set(null);
    this.editingId.set(user.id);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
    this.editingId.set(null);
  }

  async submit(): Promise<void> {
    this.pending.set(true);
    this.formError.set(null);
    const payload = {
      fullName: this.form.fullName.trim(),
      email: this.form.email.trim().toLowerCase(),
      title: this.form.title.trim(),
      role: this.form.role,
      departmentId: this.form.departmentId,
      grade: this.form.grade,
      isSecretary: this.form.isSecretary,
      accessibilityApproved: this.form.accessibilityApproved,
      active: this.form.active,
    };
    try {
      const id = this.editingId();
      if (id) await this.api.put(`admin/users/${id}`, payload);
      else await this.api.post('admin/users', payload);
      this.toasts.toast({ tone: 'success', title: id ? 'Kullanıcı güncellendi' : 'Kullanıcı eklendi', description: payload.fullName });
      this.closeForm();
      void this.load(true);
    } catch (error: unknown) {
      this.formError.set(errorMessage(error));
    } finally {
      this.pending.set(false);
    }
  }

  openImport(): void {
    this.importCsv = null;
    this.importResult.set(null);
    this.importError.set(null);
    this.importOpen.set(true);
  }

  closeImport(): void {
    this.importOpen.set(false);
  }

  downloadTemplate(): void {
    const url = URL.createObjectURL(new Blob([IMPORT_TEMPLATE], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'personel-listesi-ornek.csv';
    link.click();
    URL.revokeObjectURL(url);
  }

  /** Excel'in düz "CSV" çıktısı Windows-1254'tür; UTF-8 değilse ona düşülür. */
  private decode(buffer: ArrayBuffer): string {
    try {
      return new TextDecoder('utf-8', { fatal: true }).decode(buffer);
    } catch {
      return new TextDecoder('windows-1254').decode(buffer);
    }
  }

  async onImportFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.importResult.set(null);
    this.importError.set(null);
    this.importCsv = null;
    if (/\.xlsx?$/i.test(file.name)) {
      this.importError.set("Excel dosyası doğrudan yüklenemez. Excel'de Farklı Kaydet > CSV UTF-8 ile kaydedip o dosyayı seçin.");
      input.value = '';
      return;
    }
    this.importCsv = this.decode(await file.arrayBuffer());
    await this.sendImport(false);
  }

  applyImport(): Promise<void> {
    return this.sendImport(true);
  }

  private async sendImport(apply: boolean): Promise<void> {
    if (!this.importCsv) return;
    this.importPending.set(true);
    this.importError.set(null);
    try {
      const result = await this.api.post<UserImportResult>('admin/users/import', { csv: this.importCsv, apply });
      this.importResult.set(result);
      if (apply && result.applied) {
        this.toasts.toast({ tone: 'success', title: 'Personel listesi yüklendi', description: `${result.creates} yeni, ${result.updates} güncelleme` });
        void this.load(true);
      }
    } catch (error: unknown) {
      this.importError.set(errorMessage(error));
    } finally {
      this.importPending.set(false);
    }
  }
}
