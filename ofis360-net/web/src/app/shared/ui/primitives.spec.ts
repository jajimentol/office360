import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { UiBadge, UiButton, UiProgress, UiSwitch } from './primitives';

@Component({
  imports: [UiButton, UiBadge, UiProgress, UiSwitch],
  template: `
    <button ui-button id="b" [loading]="loading()" [variant]="variant()">Kaydet</button>
    <span ui-badge id="badge" tone="danger">Kapalı</span>
    <ui-progress id="p" [value]="140" tone="warning" label="Doluluk" />
    <ui-switch [(checked)]="on" label="Sessiz alan" />
  `,
})
class Host {
  readonly loading = signal(false);
  readonly variant = signal<'primary' | 'secondary'>('primary');
  readonly on = signal(false);
}

describe('UI primitives (kaynak: components/ui/primitives.tsx)', () => {
  it('Button: yüklenirken devre dışı kalır ve spinner gösterir, varyant sınıfları uygulanır', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const button = fixture.nativeElement.querySelector('#b') as HTMLButtonElement;
    expect(button.className).toContain('bg-brand-600');
    expect(button.hasAttribute('disabled')).toBe(false);
    expect(button.querySelector('svg')).toBeNull();

    fixture.componentInstance.loading.set(true);
    fixture.componentInstance.variant.set('secondary');
    await fixture.whenStable();
    expect(button.hasAttribute('disabled')).toBe(true);
    expect(button.querySelector('svg.animate-spin')).not.toBeNull();
    expect(button.className).toContain('border-line');
  });

  it('Badge tonu, Progress sınırlama ve Switch iki yönlü bağlama', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const badge = fixture.nativeElement.querySelector('#badge') as HTMLElement;
    expect(badge.className).toContain('text-red-700');

    const progress = fixture.nativeElement.querySelector('#p') as HTMLElement;
    expect(progress.getAttribute('aria-valuenow')).toBe('100');
    expect((progress.firstElementChild as HTMLElement).style.width).toBe('100%');
    expect(progress.firstElementChild?.className).toContain('bg-amber-500');

    const toggle = fixture.nativeElement.querySelector('button[role=switch]') as HTMLButtonElement;
    expect(toggle.getAttribute('aria-checked')).toBe('false');
    toggle.click();
    await fixture.whenStable();
    expect(fixture.componentInstance.on()).toBe(true);
    expect(toggle.getAttribute('aria-checked')).toBe('true');
  });
});
