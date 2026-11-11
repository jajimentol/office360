import { Component, input, output, signal } from '@angular/core';
import { toApiError } from '../../core/api/api-client';
import { UiIcon } from './icon';
import { UiButton, UiSkeleton } from './primitives';

/** Sayfa geçişlerinde gösterilen yükleniyor iskeleti (kaynak: (app)/loading.tsx). */
@Component({
  selector: 'ui-page-loading',
  imports: [UiSkeleton],
  template: `
    <div class="animate-fade" role="status" aria-live="polite">
      <span class="sr-only">İçerik yükleniyor</span>
      <div ui-skeleton class="h-8 w-64"></div>
      <div ui-skeleton class="mt-3 h-4 w-96"></div>
      <div class="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        @for (i of [0, 1, 2, 3]; track i) {
          <div ui-skeleton class="h-28"></div>
        }
      </div>
      <div class="mt-5 grid gap-5 lg:grid-cols-3">
        <div ui-skeleton class="h-72 lg:col-span-2"></div>
        <div ui-skeleton class="h-72"></div>
      </div>
    </div>
  `,
})
export class UiPageLoading {}

/** Uygulama içi hata sınırı: teknik hata yerine anlaşılır bir bilgilendirme gösterir (kaynak: (app)/error.tsx). */
@Component({
  selector: 'ui-page-error',
  imports: [UiIcon, UiButton],
  template: `
    <div class="mx-auto max-w-xl py-16 text-center">
      <span class="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-red-50 text-red-600">
        <svg uiIcon name="AlertTriangle" class="h-7 w-7"></svg>
      </span>
      <h1 class="mt-5 text-xl font-semibold tracking-tight text-ink-900">Bu ekran yüklenirken bir sorun oluştu</h1>
      <p class="mt-2 text-sm leading-relaxed text-ink-500">
        İşleminiz tamamlanamadı. Sayfayı yeniden deneyebilir veya birkaç dakika sonra tekrar gelebilirsiniz.
      </p>
      @if (code()) {
        <p class="mt-2 font-mono text-xs text-ink-400">Hata kodu: {{ code() }}</p>
      }
      <button ui-button type="button" class="mt-6" (click)="retry.emit()">
        <svg uiIcon name="RotateCcw" class="h-4 w-4"></svg>
        Yeniden dene
      </button>
    </div>
  `,
})
export class UiPageError {
  readonly code = input<string | null | undefined>();
  readonly retry = output<void>();
}

/**
 * Sayfa veri yükleme durumu: loading / error / data. Kaynaktaki sunucu bileşeni + loading.tsx + error.tsx
 * üçlüsünün istemci tarafı karşılığı. Her yükleme bir sürüm numarası taşır; geç gelen eski yanıt yok sayılır.
 */
export class PageState<T> {
  readonly data = signal<T | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  private version = 0;

  async load(fetcher: () => Promise<T>, options: { silent?: boolean } = {}): Promise<T | null> {
    const version = ++this.version;
    if (!options.silent) this.loading.set(true);
    this.error.set(null);
    try {
      const result = await fetcher();
      if (version !== this.version) return null;
      this.data.set(result);
      return result;
    } catch (error: unknown) {
      if (version !== this.version) return null;
      const apiError = toApiError(error);
      console.error('Sayfa verisi yüklenemedi:', apiError);
      this.error.set(apiError.code);
      return null;
    } finally {
      if (version === this.version) this.loading.set(false);
    }
  }
}
