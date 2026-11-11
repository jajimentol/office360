import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ROLE_LABELS } from '../../lib/enums';
import { AuthService } from '../../core/auth/auth.service';
import { UiButton, UiCard, UiCardBody, UiIcon, UiPageHeader } from '../../shared/ui';

/** Kaynak: (app)/yetkisiz/page.tsx */
@Component({
  selector: 'app-unauthorized-page',
  imports: [RouterLink, UiPageHeader, UiCard, UiCardBody, UiIcon, UiButton],
  template: `
    <ui-page-header title="Bu sayfaya erişim yetkiniz yok" />
    <div ui-card class="mx-auto max-w-2xl">
      <ui-card-body class="py-10 text-center">
        <span class="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-amber-50 text-amber-600">
          <svg uiIcon name="ShieldAlert" class="h-7 w-7"></svg>
        </span>
        <h2 class="mt-4 text-lg font-semibold text-ink-900">Yetki gerekiyor</h2>
        <p class="mx-auto mt-2 max-w-md text-sm leading-relaxed text-ink-500">
          Bu ekran yalnızca belirli roller için açıktır. Mevcut rolünüz:
          <strong class="font-semibold text-ink-700">{{ roleLabel() }}</strong>. Erişim ihtiyacınız varsa sistem
          yöneticinizle görüşebilirsiniz.
        </p>
        <div class="mt-6 flex flex-wrap justify-center gap-2">
          <a ui-button routerLink="/panel">Ana sayfaya dön</a>
          <a ui-button variant="secondary" routerLink="/panel/masa">Masa rezervasyonu</a>
        </div>
      </ui-card-body>
    </div>
  `,
})
export class UnauthorizedPage {
  private readonly auth = inject(AuthService);
  roleLabel(): string {
    const user = this.auth.user();
    return user ? ROLE_LABELS[user.role] : '';
  }
}
