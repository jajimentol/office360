import { DOCUMENT } from '@angular/common';
import { Component, ElementRef, Injectable, Injector, afterNextRender, computed, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { cn } from '../../lib/utils';
import { UiIcon } from './icon';
import { UiButton, booleanAttribute } from './primitives';

/** Modal, onay penceresi ve toast bildirimleri (kaynak: components/ui/overlay.tsx). */

@Component({
  selector: 'ui-modal',
  imports: [UiIcon],
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4">
        <div class="absolute inset-0 bg-ink-900/40 backdrop-blur-[2px] animate-fade" (click)="closed.emit()" aria-hidden="true"></div>
        <div #panel role="dialog" aria-modal="true" tabindex="-1" [attr.aria-label]="title()" [class]="panelClass()">
          <div class="flex items-start justify-between gap-4 border-b border-line px-5 py-4">
            <div>
              <h2 class="text-base font-semibold text-ink-900">{{ title() }}</h2>
              @if (description()) {
                <p class="mt-0.5 text-sm text-ink-500">{{ description() }}</p>
              }
            </div>
            <button
              type="button"
              (click)="closed.emit()"
              aria-label="Kapat"
              class="rounded-lg p-1.5 text-ink-400 transition-colors hover:bg-slate-100 hover:text-ink-700"
            >
              <svg uiIcon name="X" class="h-4.5 w-4.5"></svg>
            </button>
          </div>
          <div class="min-h-0 flex-1 overflow-y-auto px-5 py-4 scrollbar-thin"><ng-content /></div>
          <div class="flex items-center justify-end gap-2 border-t border-line bg-slate-50/60 px-5 py-3 empty:hidden">
            <ng-content select="[footer]" />
          </div>
        </div>
      </div>
    }
  `,
})
export class UiModal {
  /** Açık pencereler; klavye yalnızca en üsttekine işler. */
  private static readonly stack: UiModal[] = [];
  private static readonly focusable = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

  readonly open = input.required<boolean>();
  readonly title = input.required<string>();
  readonly description = input<string | null | undefined>();
  readonly size = input<'sm' | 'md' | 'lg' | 'xl'>('md');
  readonly closed = output<void>();
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  readonly panelClass = computed(() =>
    cn(
      'relative z-10 flex max-h-[92vh] w-full flex-col overflow-hidden rounded-t-2xl bg-white shadow-2xl animate-in-up sm:rounded-2xl',
      { sm: 'max-w-md', md: 'max-w-lg', lg: 'max-w-2xl', xl: 'max-w-4xl' }[this.size()],
    ),
  );

  constructor() {
    effect((onCleanup) => {
      if (!this.open()) return;
      const previous = this.document.activeElement as HTMLElement | null;
      UiModal.stack.push(this);
      const onKeyDown = (event: KeyboardEvent) => {
        if (UiModal.stack.at(-1) !== this) return;
        if (event.key === 'Escape') this.closed.emit();
        if (event.key === 'Tab') this.keepFocusInside(event);
      };
      this.document.addEventListener('keydown', onKeyDown);
      this.document.body.style.overflow = 'hidden';
      afterNextRender(() => untracked(() => this.focusFirst()), { injector: this.injector });
      onCleanup(() => {
        this.document.removeEventListener('keydown', onKeyDown);
        UiModal.stack.splice(UiModal.stack.indexOf(this), 1);
        if (UiModal.stack.length === 0) this.document.body.style.overflow = '';
        if (previous?.isConnected) previous.focus();
      });
    });
  }

  private focusables(): HTMLElement[] {
    return Array.from(this.panel()?.nativeElement.querySelectorAll<HTMLElement>(UiModal.focusable) ?? []);
  }

  /** Form alanı veya düğme varsa ilkine, yoksa pencerenin kendisine; kapatma düğmesi son tercihtir. */
  private focusFirst(): void {
    const panel = this.panel()?.nativeElement;
    if (!panel) return;
    const items = this.focusables();
    const target = panel.querySelector<HTMLElement>('[autofocus]') ?? items.find((item) => item.getAttribute('aria-label') !== 'Kapat') ?? items[0] ?? panel;
    target.focus();
  }

  private keepFocusInside(event: KeyboardEvent): void {
    const items = this.focusables();
    if (items.length === 0) {
      event.preventDefault();
      return;
    }
    const first = items[0];
    const last = items[items.length - 1];
    const active = this.document.activeElement as HTMLElement | null;
    const outside = !this.panel()?.nativeElement.contains(active);
    if (event.shiftKey && (active === first || outside)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (active === last || outside)) {
      event.preventDefault();
      first.focus();
    }
  }
}

@Component({
  selector: 'ui-confirm-dialog',
  imports: [UiModal, UiButton],
  template: `
    <ui-modal [open]="open()" [title]="title()" [description]="description()" size="sm" (closed)="closed.emit()">
      @if (hasContent()) {
        <ng-content />
      } @else {
        <p class="text-sm text-ink-700">Bu işlemi onaylıyor musunuz?</p>
      }
      <ng-container footer>
        <button ui-button variant="secondary" type="button" (click)="closed.emit()" [disabled]="loading()">{{ cancelLabel() }}</button>
        <button ui-button [variant]="tone() === 'danger' ? 'danger' : 'primary'" type="button" (click)="confirmed.emit()" [loading]="loading()">
          {{ confirmLabel() }}
        </button>
      </ng-container>
    </ui-modal>
  `,
})
export class UiConfirmDialog {
  readonly open = input.required<boolean>();
  readonly title = input.required<string>();
  readonly description = input<string | null | undefined>();
  readonly confirmLabel = input('Onayla');
  readonly cancelLabel = input('Vazgeç');
  readonly tone = input<'danger' | 'primary'>('danger');
  readonly loading = input(false, { transform: booleanAttribute });
  /** İçerik projeksiyonu verildiğinde true (varsayılan metni gizler). */
  readonly hasContent = input(false, { transform: booleanAttribute });
  readonly closed = output<void>();
  readonly confirmed = output<void>();
}

// ---------------------------------------------------------------------------
// Toast
// ---------------------------------------------------------------------------

export type ToastTone = 'success' | 'error' | 'info' | 'warning';

export interface ToastItem {
  id: number;
  tone: ToastTone;
  title: string;
  description?: string;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly items = signal<ToastItem[]>([]);
  private counter = 0;

  toast(input: { tone?: ToastTone; title: string; description?: string }): void {
    this.counter += 1;
    const id = this.counter;
    this.items.update((current) => [...current, { id, tone: input.tone ?? 'info', title: input.title, description: input.description }]);
    setTimeout(() => this.dismiss(id), 5200);
  }

  dismiss(id: number): void {
    this.items.update((current) => current.filter((item) => item.id !== id));
  }
}

@Component({
  selector: 'ui-toast-host',
  imports: [UiIcon],
  template: `
    <div class="pointer-events-none fixed bottom-4 right-4 z-[60] flex w-[calc(100vw-2rem)] max-w-sm flex-col gap-2" aria-live="polite" aria-atomic="false">
      @for (item of toasts.items(); track item.id) {
        <div class="pointer-events-auto flex items-start gap-3 rounded-xl border border-line bg-white p-3.5 shadow-lg animate-in-up" role="status">
          <span class="mt-0.5 shrink-0">
            @switch (item.tone) {
              @case ('success') {
                <svg uiIcon name="CheckCircle2" class="h-5 w-5 text-emerald-600"></svg>
              }
              @case ('error') {
                <svg uiIcon name="XCircle" class="h-5 w-5 text-red-600"></svg>
              }
              @case ('warning') {
                <svg uiIcon name="AlertTriangle" class="h-5 w-5 text-amber-600"></svg>
              }
              @default {
                <svg uiIcon name="Info" class="h-5 w-5 text-brand-600"></svg>
              }
            }
          </span>
          <div class="min-w-0 flex-1">
            <p class="text-sm font-semibold text-ink-900">{{ item.title }}</p>
            @if (item.description) {
              <p class="mt-0.5 text-[13px] leading-relaxed text-ink-500">{{ item.description }}</p>
            }
          </div>
          <button
            type="button"
            aria-label="Bildirimi kapat"
            (click)="toasts.dismiss(item.id)"
            class="rounded-md p-1 text-ink-400 transition-colors hover:bg-slate-100 hover:text-ink-700"
          >
            <svg uiIcon name="X" class="h-4 w-4"></svg>
          </button>
        </div>
      }
    </div>
  `,
})
export class UiToastHost {
  readonly toasts = inject(ToastService);
}

/** Basit tooltip - hover ve odaklanmada görünür. */
@Component({
  selector: 'ui-tooltip',
  template: `
    <ng-content />
    <span role="tooltip" [class]="tipClass()">{{ content() }}</span>
  `,
  host: { class: 'group/tooltip relative inline-flex' },
})
export class UiTooltip {
  readonly content = input.required<string>();
  readonly side = input<'top' | 'bottom'>('top');
  readonly tipClass = computed(() =>
    cn(
      'pointer-events-none absolute left-1/2 z-40 hidden -translate-x-1/2 whitespace-nowrap rounded-lg bg-ink-900 px-2.5 py-1.5 text-xs font-medium text-white shadow-lg group-hover/tooltip:block group-focus-within/tooltip:block',
      this.side() === 'top' ? 'bottom-full mb-1.5' : 'top-full mt-1.5',
    ),
  );
}
