import { Injectable, inject, signal } from '@angular/core';
import { ApiClient } from '../core/api/api-client';

/**
 * Kabuk durumu: okunmamış bildirim sayısı. Kaynakta (app)/layout.tsx her gezinmede countUnread çağırıyordu;
 * burada her rota geçişinde ve bildirim üreten işlemlerden sonra yenilenir.
 */
@Injectable({ providedIn: 'root' })
export class ShellService {
  private readonly api = inject(ApiClient);
  readonly unreadCount = signal(0);

  async refreshUnread(): Promise<void> {
    try {
      const result = await this.api.get<{ unread: number }>('me/notifications/unread-count');
      this.unreadCount.set(result.unread);
    } catch {
      // Rozet sayacı kritik değildir; hata ekranı bozmaz.
    }
  }
}
