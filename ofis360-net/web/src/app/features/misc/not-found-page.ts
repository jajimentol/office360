import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { APP_CONFIG } from '../../lib/config';
import { UiIcon } from '../../shared/ui';

/** Kaynak: app/not-found.tsx */
@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink, UiIcon],
  template: `
    <main id="main-content" class="flex min-h-screen flex-col items-center justify-center px-6 text-center">
      <span class="flex h-14 w-14 items-center justify-center rounded-full bg-brand-50 text-brand-600">
        <svg uiIcon name="Compass" class="h-7 w-7"></svg>
      </span>
      <h1 class="mt-5 text-2xl font-semibold tracking-tight text-ink-900">Sayfa bulunamadı</h1>
      <p class="mt-2 max-w-md text-sm leading-relaxed text-ink-500">
        Aradığınız sayfa taşınmış veya kaldırılmış olabilir. {{ config.name }} ana sayfasından devam edebilirsiniz.
      </p>
      <a routerLink="/" class="mt-6 inline-flex h-10 items-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-white transition-colors hover:bg-brand-700">
        Ana sayfaya dön
      </a>
    </main>
  `,
})
export class NotFoundPage {
  readonly config = APP_CONFIG;
}
