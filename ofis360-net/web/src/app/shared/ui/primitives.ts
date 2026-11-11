import { Component, Directive, computed, input, model } from '@angular/core';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn, initialsOf } from '../../lib/utils';
import { UiIcon } from './icon';

/** Ofis360 temel arayüz bileşenleri (kaynak: components/ui/primitives.tsx). Sınıf dizgileri birebir. */

export const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-medium transition-colors disabled:pointer-events-none disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-500',
  {
    variants: {
      variant: {
        primary: 'bg-brand-600 text-white hover:bg-brand-700 shadow-sm',
        secondary: 'bg-white text-ink-900 border border-line hover:bg-brand-50',
        ghost: 'text-ink-700 hover:bg-brand-50 hover:text-brand-700',
        danger: 'bg-red-600 text-white hover:bg-red-700 shadow-sm',
        subtle: 'bg-brand-50 text-brand-700 hover:bg-brand-100',
        accent: 'bg-teal-accent text-white hover:bg-teal-700',
      },
      size: {
        sm: 'h-8 px-3 text-[13px]',
        md: 'h-10 px-4',
        lg: 'h-11 px-5 text-[15px]',
        icon: 'h-9 w-9',
      },
      block: { true: 'w-full', false: '' },
    },
    defaultVariants: { variant: 'primary', size: 'md', block: false },
  },
);

export type ButtonVariant = NonNullable<VariantProps<typeof buttonVariants>['variant']>;
export type ButtonSize = NonNullable<VariantProps<typeof buttonVariants>['size']>;

/**
 * `<button ui-button variant="secondary" size="sm" [loading]="pending">` ve `<a ui-button routerLink="...">`.
 * Bağlantı biçimi kaynaktaki LinkButton'a karşılık gelir (buton içine <a> yerleştirmemek için).
 */
@Component({
  selector: 'button[ui-button], a[ui-button]',
  imports: [UiIcon],
  template: `
    @if (loading()) {
      <svg uiIcon name="Loader2" class="h-4 w-4 animate-spin"></svg>
    }
    <ng-content />
  `,
  host: {
    '[class]': 'classes()',
    '[attr.disabled]': 'isDisabled() ? "" : null',
    '[attr.aria-disabled]': 'isDisabled() ? "true" : null',
  },
})
export class UiButton {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  readonly block = input(false, { transform: booleanAttribute });
  readonly loading = input(false, { transform: booleanAttribute });
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly isDisabled = computed(() => this.disabled() || this.loading());
  readonly classes = computed(() => buttonVariants({ variant: this.variant(), size: this.size(), block: this.block() }));
}

const badgeVariants = cva('inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium border', {
  variants: {
    tone: {
      neutral: 'bg-slate-50 text-ink-700 border-slate-200',
      brand: 'bg-brand-50 text-brand-700 border-brand-200',
      success: 'bg-emerald-50 text-emerald-700 border-emerald-200',
      warning: 'bg-amber-50 text-amber-800 border-amber-200',
      danger: 'bg-red-50 text-red-700 border-red-200',
      accent: 'bg-teal-soft text-teal-800 border-teal-200',
      violet: 'bg-violet-50 text-violet-700 border-violet-200',
    },
  },
  defaultVariants: { tone: 'neutral' },
});

export type BadgeTone = NonNullable<VariantProps<typeof badgeVariants>['tone']>;

@Component({
  selector: 'span[ui-badge]',
  template: `<ng-content />`,
  host: { '[class]': 'classes()' },
})
export class UiBadge {
  readonly tone = input<BadgeTone | null | undefined>('neutral');
  readonly classes = computed(() => badgeVariants({ tone: this.tone() ?? 'neutral' }));
}

@Directive({
  selector: 'input[ui-input]',
  host: {
    class:
      'h-10 w-full rounded-lg border border-line bg-white px-3 text-sm text-ink-900 placeholder:text-ink-400 transition-colors hover:border-brand-200 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-100 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-ink-400',
  },
})
export class UiInput {}

@Directive({
  selector: 'textarea[ui-textarea]',
  host: {
    class:
      'w-full rounded-lg border border-line bg-white px-3 py-2 text-sm text-ink-900 placeholder:text-ink-400 hover:border-brand-200 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-100',
  },
})
export class UiTextarea {}

@Directive({
  selector: 'select[ui-select]',
  host: {
    class:
      'h-10 w-full appearance-none rounded-lg border border-line bg-white px-3 pr-9 text-sm text-ink-900 bg-[length:16px] bg-[right_0.65rem_center] bg-no-repeat hover:border-brand-200 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-100 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-ink-400',
    '[style.background-image]': 'arrow',
  },
})
export class UiSelect {
  readonly arrow =
    "url(\"data:image/svg+xml;charset=utf-8,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 24 24' stroke-width='2' stroke='%2364748b'%3E%3Cpath stroke-linecap='round' stroke-linejoin='round' d='m19.5 8.25-7.5 7.5-7.5-7.5'/%3E%3C/svg%3E\")";
}

@Component({
  selector: 'label[ui-label]',
  template: `
    <ng-content />
    @if (required()) {
      <span class="ml-0.5 text-red-600" aria-hidden="true">*</span>
    }
  `,
  host: { class: 'block text-sm font-medium text-ink-700' },
})
export class UiLabel {
  readonly required = input(false, { transform: booleanAttribute });
}

@Component({
  selector: 'ui-field',
  imports: [UiLabel],
  template: `
    <label ui-label [attr.for]="htmlFor() || null" [required]="required()">{{ label() }}</label>
    <ng-content />
    @if (error()) {
      <p class="text-xs font-medium text-red-600" role="alert">{{ error() }}</p>
    } @else if (hint()) {
      <p class="text-xs text-ink-500">{{ hint() }}</p>
    }
  `,
  host: { class: 'block space-y-1.5' },
})
export class UiField {
  readonly label = input.required<string>();
  readonly hint = input<string | null | undefined>();
  readonly error = input<string | null | undefined>();
  readonly required = input(false, { transform: booleanAttribute });
  readonly htmlFor = input<string | undefined>();
}

@Component({
  selector: 'ui-switch',
  template: `
    <button
      type="button"
      [id]="id() || null"
      role="switch"
      [attr.aria-checked]="checked()"
      [attr.aria-label]="label()"
      [disabled]="disabled()"
      (click)="toggle()"
      [class]="cn('relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors disabled:opacity-50', checked() ? 'bg-brand-600' : 'bg-slate-300')"
    >
      <span
        [class]="cn('inline-block h-4.5 w-4.5 transform rounded-full bg-white shadow transition-transform', checked() ? 'translate-x-6' : 'translate-x-1')"
      ></span>
    </button>
  `,
  host: { class: 'inline-flex' },
})
export class UiSwitch {
  readonly checked = model.required<boolean>();
  readonly label = input.required<string>();
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly id = input<string | undefined>();
  readonly cn = cn;

  toggle(): void {
    this.checked.set(!this.checked());
  }
}

@Component({
  selector: 'ui-avatar',
  template: `{{ initials() }}`,
  host: {
    '[class]': 'classes()',
    'aria-hidden': 'true',
  },
})
export class UiAvatar {
  readonly name = input.required<string>();
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly initials = computed(() => initialsOf(this.name()));
  readonly classes = computed(() =>
    cn(
      'inline-flex shrink-0 items-center justify-center rounded-full bg-brand-100 font-semibold text-brand-700',
      { sm: 'h-7 w-7 text-[11px]', md: 'h-9 w-9 text-xs', lg: 'h-12 w-12 text-sm' }[this.size()],
    ),
  );
}

@Component({
  selector: 'ui-spinner',
  imports: [UiIcon],
  template: `<svg uiIcon name="Loader2" class="h-5 w-5 animate-spin text-brand-600"></svg>`,
  host: { class: 'inline-flex' },
})
export class UiSpinner {}

@Directive({
  selector: 'div[ui-skeleton]',
  host: { class: 'animate-pulse rounded-lg bg-slate-200/70' },
})
export class UiSkeleton {}

export type ProgressTone = 'brand' | 'success' | 'warning' | 'danger' | 'accent';

@Component({
  selector: 'ui-progress',
  template: `<div [class]="barClass()" [style.width.%]="clamped()"></div>`,
  host: {
    class: 'block h-2 w-full overflow-hidden rounded-full bg-slate-200',
    role: 'progressbar',
    '[attr.aria-valuenow]': 'rounded()',
    'aria-valuemin': '0',
    'aria-valuemax': '100',
    '[attr.aria-label]': "label() || 'İlerleme'",
  },
})
export class UiProgress {
  readonly value = input.required<number>();
  readonly tone = input<ProgressTone>('brand');
  readonly label = input<string | undefined>();
  readonly clamped = computed(() => Math.max(0, Math.min(100, this.value())));
  readonly rounded = computed(() => Math.round(this.clamped()));
  readonly barClass = computed(() =>
    cn(
      'h-full rounded-full transition-all',
      { brand: 'bg-brand-600', success: 'bg-emerald-500', warning: 'bg-amber-500', danger: 'bg-red-500', accent: 'bg-teal-accent' }[this.tone()],
    ),
  );
}

export function booleanAttribute(value: unknown): boolean {
  return value !== false && value !== null && value !== undefined && value !== 'false' && value !== 0;
}
