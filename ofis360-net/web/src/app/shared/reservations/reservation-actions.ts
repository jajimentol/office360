import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient, errorMessage } from '../../core/api/api-client';
import { ShellService } from '../../layout/shell.service';
import { ToastService, UiButton, UiConfirmDialog, UiField, UiIcon, UiTextarea, type ButtonSize, type ButtonVariant } from '../ui';

/** Kaynak: components/reservations/reservation-actions.tsx */

/** Rezervasyon iptali - onay modalı ve gerekçe alanı ile. */
@Component({
  selector: 'app-cancel-reservation-button',
  imports: [FormsModule, UiButton, UiIcon, UiConfirmDialog, UiField, UiTextarea],
  template: `
    <button ui-button type="button" [variant]="variant()" [size]="size()" (click)="open.set(true)">
      <svg uiIcon name="XCircle" class="h-4 w-4"></svg>
      {{ label() }}
    </button>

    <ui-confirm-dialog
      [open]="open()"
      (closed)="open.set(false)"
      (confirmed)="handleConfirm()"
      [loading]="pending()"
      title="Rezervasyonu iptal et"
      [description]="description() ?? 'Bu işlem geri alınamaz. Kaynak aynı tarih için yeniden uygun hâle gelir.'"
      confirmLabel="Rezervasyonu iptal et"
      hasContent
    >
      @if (askReason()) {
        <ui-field label="İptal gerekçesi" hint="Gerekçe, rezervasyon sahibine gönderilen bildirimde yer alır." htmlFor="cancel-reason">
          <textarea ui-textarea id="cancel-reason" rows="3" maxlength="240" [(ngModel)]="reason" placeholder="Örneğin: Bu bölgede planlı bakım çalışması yapılacaktır."></textarea>
        </ui-field>
      } @else {
        <p class="text-sm text-ink-700">Rezervasyonu iptal etmek istediğinize emin misiniz?</p>
      }
    </ui-confirm-dialog>
  `,
})
export class CancelReservationButton {
  readonly reservationId = input.required<string>();
  readonly kind = input.required<'desk' | 'room'>();
  readonly label = input('İptal et');
  readonly size = input<ButtonSize>('sm');
  readonly variant = input<ButtonVariant>('secondary');
  readonly askReason = input(false);
  readonly description = input<string | undefined>();
  readonly cancelled = output<void>();

  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  private readonly shell = inject(ShellService);

  readonly open = signal(false);
  readonly pending = signal(false);
  reason = '';

  async handleConfirm(): Promise<void> {
    this.pending.set(true);
    try {
      const path = this.kind() === 'desk' ? `desk-reservations/${this.reservationId()}/cancel` : `room-reservations/${this.reservationId()}/cancel`;
      await this.api.post(path, { action: 'cancel', reason: this.reason.trim() || null });
      this.open.set(false);
      this.reason = '';
      this.toasts.toast({
        tone: 'success',
        title: 'Rezervasyon iptal edildi',
        description: this.kind() === 'desk' ? 'Masa aynı tarih için yeniden rezervasyona açıldı.' : 'Toplantı odası seçilen saat aralığında yeniden uygun.',
      });
      void this.shell.refreshUnread();
      this.cancelled.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'İptal edilemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}

/** Ofise geliş kaydı (check-in). */
@Component({
  selector: 'app-check-in-button',
  imports: [UiButton, UiIcon],
  template: `
    <button ui-button type="button" variant="accent" [loading]="pending()" (click)="handleClick()">
      <svg uiIcon name="LogIn" class="h-4 w-4"></svg>
      Ofisteyim, giriş yap
    </button>
  `,
})
export class CheckInButton {
  readonly reservationId = input.required<string>();
  readonly checkedIn = output<void>();
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  readonly pending = signal(false);

  async handleClick(): Promise<void> {
    this.pending.set(true);
    try {
      await this.api.post(`desk-reservations/${this.reservationId()}/check-in`);
      this.toasts.toast({ tone: 'success', title: 'Ofis girişiniz kaydedildi', description: 'İyi çalışmalar dileriz.' });
      this.checkedIn.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Giriş kaydedilemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}

/** Toplantı odasında "toplantı başladı" kaydı. */
@Component({
  selector: 'app-room-start-button',
  imports: [UiButton, UiIcon],
  template: `
    <button ui-button type="button" size="sm" variant="accent" [loading]="pending()" (click)="handleClick()">
      <svg uiIcon name="LogIn" class="h-3.5 w-3.5"></svg>
      Toplantı başladı
    </button>
  `,
})
export class RoomStartButton {
  readonly reservationId = input.required<string>();
  readonly started = output<void>();
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  readonly pending = signal(false);

  async handleClick(): Promise<void> {
    this.pending.set(true);
    try {
      await this.api.post(`room-reservations/${this.reservationId()}/start`);
      this.toasts.toast({ tone: 'success', title: 'Toplantı başladı olarak kaydedildi' });
      this.started.emit();
    } catch (error: unknown) {
      this.toasts.toast({ tone: 'error', title: 'Kaydedilemedi', description: errorMessage(error) });
    } finally {
      this.pending.set(false);
    }
  }
}

@Component({
  selector: 'app-checked-in-badge',
  imports: [UiIcon],
  template: `
    <span class="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-medium text-emerald-700">
      <svg uiIcon name="CheckCircle2" class="h-3.5 w-3.5"></svg>
      Ofis girişiniz kaydedildi
    </span>
  `,
})
export class CheckedInBadge {}
