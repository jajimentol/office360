import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { DeskView, RoomView, ZoneView } from '../../core/api/types';
import { FloorPlanSvg, shortDeskLabel } from './floor-plan-svg';

function desk(partial: Partial<DeskView>): DeskView {
  return {
    id: 'd1',
    code: 'PT36-A05-01',
    x: 100,
    y: 100,
    width: 46,
    height: 34,
    rotation: 0,
    zoneId: 'z1',
    zoneName: 'A Kanadı',
    zoneQuiet: false,
    zoneWindowSide: true,
    floorId: 'f1',
    floorNumber: 36,
    popular: false,
    accessibleOnly: false,
    active: true,
    notes: null,
    features: [],
    state: 'AVAILABLE',
    selectable: true,
    occupant: null,
    violations: [],
    closureNote: null,
    ...partial,
  };
}

@Component({
  imports: [FloorPlanSvg],
  template: `
    <app-floor-plan-svg [planWidth]="800" [planHeight]="600" [zones]="zones" [elements]="[]" [desks]="desks" [rooms]="rooms" [selectedDeskId]="selected()" (selectDesk)="picked.set($event)" />
  `,
})
class Host {
  readonly zones: ZoneView[] = [{ id: 'z1', code: 'A', name: 'A Kanadı', kind: 'WORK', x: 0, y: 0, width: 400, height: 300, windowSide: true, quiet: true, reservable: true }];
  readonly rooms: RoomView[] = [{ id: 'r1', code: 'T101', name: 'Boğaz', capacity: 12, x: 500, y: 50, width: 200, height: 120, active: true, features: [], closed: false }];
  readonly desks: DeskView[] = [
    desk({ id: 'd1', code: 'PT36-A05-01' }),
    desk({ id: 'd2', code: 'PT36-A05-02', state: 'RESERVED', selectable: false, occupant: { fullName: 'Mert Demir', departmentName: 'Operasyon' } }),
    desk({ id: 'd3', code: 'PT36-A05-03', state: 'MINE', accessibleOnly: true }),
  ];
  readonly selected = signal<string | null>(null);
  readonly picked = signal<string | null>(null);
}

describe('FloorPlanSvg (kaynak: components/floor-plan/floor-plan-svg.tsx)', () => {
  it('masa kodunun kısa etiketi bina/kat önekini atar', () => {
    expect(shortDeskLabel('PT36-A05-01')).toBe('A05-01');
    expect(shortDeskLabel('F2-D01')).toBe('D01');
    expect(shortDeskLabel('T-A01')).toBe('A01');
  });

  it('kroki yoksa bölgeleri, masaları ve odaları erişilebilir etiketlerle çizer; tıklama seçimi yayar', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const svg = fixture.nativeElement.querySelector('svg') as SVGSVGElement;
    expect(svg.getAttribute('viewBox')).toBe('0 0 800 600');

    const buttons = svg.querySelectorAll('g[role=button]');
    expect(buttons.length).toBe(3);
    expect(buttons[1].getAttribute('aria-label')).toBe('PT36-A05-02 masası, A Kanadı, durum: Rezerve edilmiş, kullanan: Mert Demir');
    expect(svg.textContent).toContain('Sessiz alan');
    expect(svg.textContent).toContain('Boğaz');
    expect(svg.textContent).toContain('♿');
    expect(svg.textContent).toContain('✓ siz');

    (buttons[0] as SVGGElement).dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(fixture.componentInstance.picked()).toBe('d1');

    fixture.componentInstance.selected.set('d1');
    await fixture.whenStable();
    expect(buttons[0].getAttribute('aria-pressed')).toBe('true');
    expect(buttons[0].querySelector('title')?.textContent).toContain('Seçili');
  });
});
