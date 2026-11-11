import { Directive, ElementRef, Renderer2, effect, inject, input } from '@angular/core';
import { ICONS, type IconName } from './icons';

const SVG_NS = 'http://www.w3.org/2000/svg';

/**
 * `<svg uiIcon name="Bell" class="h-4 w-4"></svg>` — lucide-react'teki `<Bell className="h-4 w-4" />` karşılığı.
 * Sınıflar doğrudan svg elemanına uygulandığı için kaynak sınıf dizgileri birebir taşınır.
 */
@Directive({
  selector: 'svg[uiIcon]',
  host: {
    xmlns: SVG_NS,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    'stroke-width': '2',
    'stroke-linecap': 'round',
    'stroke-linejoin': 'round',
    'aria-hidden': 'true',
  },
})
export class UiIcon {
  readonly name = input.required<IconName>();
  private readonly host = inject<ElementRef<SVGSVGElement>>(ElementRef);
  private readonly renderer = inject(Renderer2);

  constructor() {
    effect(() => {
      const nodes = ICONS[this.name()];
      const svg = this.host.nativeElement;
      while (svg.firstChild) svg.removeChild(svg.firstChild);
      if (!nodes) return;
      for (const [tag, attrs] of nodes) {
        const element = this.renderer.createElement(tag, SVG_NS) as SVGElement;
        for (const [key, value] of Object.entries(attrs)) {
          this.renderer.setAttribute(element, key, String(value));
        }
        this.renderer.appendChild(svg, element);
      }
    });
  }
}
