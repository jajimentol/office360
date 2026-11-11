import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { UiToastHost } from './shared/ui';

/** Kaynak: app/layout.tsx (RootLayout) — atlama bağlantısı + ToastProvider. */
@Component({
  imports: [RouterOutlet, UiToastHost],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {}
