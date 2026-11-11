import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { UiModal } from './overlay';

@Component({
  imports: [UiModal],
  template: `
    <button id="trigger" type="button">Aç</button>
    <ui-modal [open]="open()" title="Deneme" (closed)="open.set(false)">
      <input id="first" />
      <button id="last" type="button">Tamam</button>
    </ui-modal>
    <ui-modal [open]="second()" title="Üstteki" (closed)="second.set(false)">
      <input id="other" />
    </ui-modal>
  `,
})
class Host {
  readonly open = signal(false);
  readonly second = signal(false);
}

const tab = (shiftKey = false) => {
  const event = new KeyboardEvent('keydown', { key: 'Tab', shiftKey, bubbles: true, cancelable: true });
  document.dispatchEvent(event);
  return event;
};

describe('UiModal klavye odağı', () => {
  async function setup() {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const trigger = fixture.nativeElement.querySelector('#trigger') as HTMLButtonElement;
    trigger.focus();
    return { fixture, host: fixture.componentInstance, trigger };
  }

  it('açılınca odağı içeriğe alır, kapanınca geri verir', async () => {
    const { fixture, host, trigger } = await setup();

    host.open.set(true);
    await fixture.whenStable();
    expect(document.activeElement?.id).toBe('first');

    host.open.set(false);
    await fixture.whenStable();
    expect(document.activeElement).toBe(trigger);
  });

  it('Tab tuşu pencere içinde döner', async () => {
    const { fixture, host } = await setup();
    host.open.set(true);
    await fixture.whenStable();

    (fixture.nativeElement.querySelector('#last') as HTMLElement).focus();
    expect(tab().defaultPrevented).toBe(true);
    expect(document.activeElement?.getAttribute('aria-label')).toBe('Kapat');

    expect(tab(true).defaultPrevented).toBe(true);
    expect(document.activeElement?.id).toBe('last');
  });

  it('Escape yalnızca en üstteki pencereyi kapatır', async () => {
    const { fixture, host } = await setup();
    host.open.set(true);
    await fixture.whenStable();
    host.second.set(true);
    await fixture.whenStable();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await fixture.whenStable();

    expect(host.second()).toBe(false);
    expect(host.open()).toBe(true);
  });
});
