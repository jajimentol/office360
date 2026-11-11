import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { relativeTimeLabel } from '../../lib/date';
import { cn } from '../../lib/utils';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import type { NotificationsResponse } from '../../core/api/types';
import { AuthService } from '../../core/auth/auth.service';
import { ShellService } from '../../layout/shell.service';
import { PageState, ToastService, UiButton, UiCard, UiCardBody, UiCardHeader, UiEmptyState, UiIcon, UiPageError, UiPageHeader, UiPageLoading } from '../../shared/ui';

/** Kaynak: (app)/panel/bildirimler/page.tsx + components/notifications/notification-list.tsx */

const CHANNEL_LABELS: Record<string, string> = {
  IN_APP: 'Uygulama içi',
  EMAIL: 'E-posta',
  TEAMS: 'Microsoft Teams',
};

const SEVERITY_STYLE: Record<string, { dot: string; label: string }> = {
  INFO: { dot: 'bg-brand-500', label: 'Bilgi' },
  SUCCESS: { dot: 'bg-emerald-500', label: 'Onay' },
  WARNING: { dot: 'bg-amber-500', label: 'Uyarı' },
};

@Component({
  selector: 'app-notifications-page',
  imports: [RouterLink, UiPageHeader, UiPageLoading, UiPageError, UiCard, UiCardHeader, UiCardBody, UiEmptyState, UiButton, UiIcon],
  template: `
    <ui-page-header title="Bildirim merkezi" description="Rezervasyon hareketleri, yönetim iptalleri ve kapatma duyuruları." />

    @if (state.loading()) {
      <ui-page-loading />
    } @else if (state.error()) {
      <ui-page-error [code]="state.error()" (retry)="load()" />
    } @else if (state.data(); as data) {
      <div class="grid gap-5 lg:grid-cols-[minmax(0,1fr)_300px]">
        <div ui-card>
          <ui-card-header title="Bildirimleriniz" hasIcon>
            <svg uiIcon icon name="Bell" class="h-4.5 w-4.5"></svg>
          </ui-card-header>
          <ui-card-body>
            <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
              <div class="flex items-center gap-1 rounded-lg border border-line bg-slate-50 p-0.5">
                <button type="button" (click)="filter.set('all')" [attr.aria-pressed]="filter() === 'all'" [class]="cn('rounded-md px-3 py-1.5 text-[13px] font-medium transition-colors', filter() === 'all' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')">
                  Tümü ({{ data.items.length }})
                </button>
                <button type="button" (click)="filter.set('unread')" [attr.aria-pressed]="filter() === 'unread'" [class]="cn('rounded-md px-3 py-1.5 text-[13px] font-medium transition-colors', filter() === 'unread' ? 'bg-white text-brand-700 shadow-sm' : 'text-ink-500')">
                  Okunmamış ({{ unreadCount() }})
                </button>
              </div>

              <button ui-button type="button" variant="secondary" size="sm" (click)="markAll()" [loading]="pending()" [disabled]="unreadCount() === 0">
                <svg uiIcon name="CheckCheck" class="h-4 w-4"></svg>
                Tümünü okundu işaretle
              </button>
            </div>

            @if (visible().length === 0) {
              <ui-empty-state hasIcon [title]="filter() === 'unread' ? 'Okunmamış bildiriminiz yok' : 'Henüz bildiriminiz yok'" description="Rezervasyon oluşturduğunuzda, iptal ettiğinizde veya yönetim bir alanı kapattığında burada bilgilendirilirsiniz.">
                <svg uiIcon icon [name]="filter() === 'unread' ? 'BellOff' : 'Bell'" class="h-6 w-6"></svg>
              </ui-empty-state>
            } @else {
              <ul class="space-y-2">
                @for (item of visible(); track item.id) {
                  @let severity = severityStyle(item.severity);
                  @let unread = !item.readAt;
                  <li [class]="cn('rounded-xl border p-4 transition-colors', unread ? 'border-brand-200 bg-brand-50/40' : 'border-line bg-white')">
                    <div class="flex items-start gap-3">
                      <span [class]="cn('mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full', severity.dot)" aria-hidden="true"></span>
                      <div class="min-w-0 flex-1">
                        <div class="flex flex-wrap items-center gap-2">
                          <p class="text-sm font-semibold text-ink-900">{{ item.title }}</p>
                          @if (unread) {
                            <span class="rounded-full bg-brand-600 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-white">Yeni</span>
                          }
                          <span class="sr-only">{{ severity.label }}</span>
                        </div>
                        <p class="mt-1 text-[13px] leading-relaxed text-ink-600">{{ item.body }}</p>
                        <div class="mt-2 flex flex-wrap items-center gap-3">
                          <span class="text-xs text-ink-400">{{ relativeTime(item.createdAt) }}</span>
                          @if (item.link) {
                            <a [routerLink]="linkPath(item.link)" [queryParams]="linkQuery(item.link)" class="text-xs font-medium text-brand-600 hover:text-brand-700">Detaya git</a>
                          }
                          @if (unread) {
                            <button type="button" (click)="markRead(item.id)" class="flex items-center gap-1 text-xs font-medium text-ink-500 hover:text-ink-700">
                              <svg uiIcon name="Check" class="h-3.5 w-3.5"></svg>
                              Okundu işaretle
                            </button>
                          }
                        </div>
                      </div>
                    </div>
                  </li>
                }
              </ul>
            }
          </ui-card-body>
        </div>

        <div ui-card class="h-fit">
          <ui-card-header title="Bildirim kanalları" [description]="demoMode() ? 'Demo ortamında etkin kanallar' : 'Etkin kanallar'" />
          <ui-card-body>
            <ul class="space-y-2.5">
              @for (channel of data.channels; track channel.key) {
                <li class="flex items-center justify-between rounded-lg border border-line px-3 py-2.5">
                  <span class="text-sm text-ink-700">{{ channelLabels[channel.key] ?? channel.key }}</span>
                  <span [class]="channel.enabled ? 'rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700' : 'rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-ink-500'">
                    {{ channel.enabled ? 'Etkin' : 'Hazır (pasif)' }}
                  </span>
                </li>
              }
            </ul>
            <p class="mt-3 text-xs leading-relaxed text-ink-400">
              Bildirim servisi kanal tabanlı tasarlanmıştır. E-posta ve Teams kanalları aynı sözleşmeyi uyguladığından, kurumsal ortamda etkinleştirmek için servis kodunun değişmesi gerekmez.
            </p>
          </ui-card-body>
        </div>
      </div>
    }
  `,
})
export class NotificationsPage {
  private readonly api = inject(ApiClient);
  readonly demoMode = inject(AuthService).demoMode;
  private readonly toasts = inject(ToastService);
  private readonly shell = inject(ShellService);
  readonly state = new PageState<NotificationsResponse>();
  readonly cn = cn;
  readonly channelLabels = CHANNEL_LABELS;
  readonly filter = signal<'all' | 'unread'>('all');
  readonly pending = signal(false);

  readonly unreadCount = computed(() => (this.state.data()?.items ?? []).filter((item) => !item.readAt).length);
  readonly visible = computed(() => {
    const items = this.state.data()?.items ?? [];
    return this.filter() === 'unread' ? items.filter((item) => !item.readAt) : items;
  });

  constructor() {
    void this.load();
  }

  severityStyle(severity: string) {
    return SEVERITY_STYLE[severity] ?? SEVERITY_STYLE['INFO'];
  }

  relativeTime(iso: string): string {
    return relativeTimeLabel(new Date(iso));
  }

  linkPath(link: string): string {
    return link.split('?')[0];
  }

  linkQuery(link: string): Record<string, string> {
    const index = link.indexOf('?');
    if (index < 0) return {};
    return Object.fromEntries(new URLSearchParams(link.slice(index + 1)).entries());
  }

  load(): Promise<unknown> {
    return this.state.load(() => this.api.get<NotificationsResponse>('me/notifications', { take: 100 }));
  }

  private refresh(): void {
    void this.state.load(() => this.api.get<NotificationsResponse>('me/notifications', { take: 100 }), { silent: true });
    void this.shell.refreshUnread();
  }

  async markRead(notificationId: string): Promise<void> {
    try {
      await this.api.post(`me/notifications/${notificationId}/read`);
      this.refresh();
    } catch {
      // Sessiz: kaynak da başarısızlıkta uyarı göstermiyordu.
    }
  }

  async markAll(): Promise<void> {
    this.pending.set(true);
    try {
      await this.api.post('me/notifications/read-all');
      this.toasts.toast({ tone: 'success', title: 'Tüm bildirimler okundu olarak işaretlendi' });
      this.refresh();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'İşlem tamamlanamadı', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}
