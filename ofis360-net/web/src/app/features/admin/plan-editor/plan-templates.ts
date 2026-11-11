import { PLAN_ELEMENT_DEFINITIONS, type PlanElement, type PlanElementType } from '../../../lib/plan';

/**
 * Hazır kroki şablonları (kaynak: plan-templates.ts).
 * Sıfırdan çizime başlamayı kolaylaştırır; seçildiğinde mevcut kroki değiştirilir (kullanıcıya onay gösterilir).
 */

export interface PlanTemplate {
  key: string;
  name: string;
  description: string;
  build: (planWidth: number, planHeight: number) => PlanElement[];
}

let counter = 0;

function makeElement(type: PlanElementType, rect: { x: number; y: number; width: number; height: number }, label?: string | null, layerOffset = 0): PlanElement {
  counter += 1;
  const definition = PLAN_ELEMENT_DEFINITIONS[type];
  return {
    id: `sablon-${counter}-${type}`,
    type,
    label: label === undefined ? definition.defaultLabel : label,
    x: Math.round(rect.x),
    y: Math.round(rect.y),
    width: Math.max(8, Math.round(rect.width)),
    height: Math.max(8, Math.round(rect.height)),
    rotation: 0,
    style: { ...definition.defaultStyle },
    layerOrder: definition.baseLayer + layerOffset,
    locked: false,
    visible: true,
    linkedResourceId: null,
    linkedResourceType: null,
  };
}

/** Dış duvarlar (dört kenar). */
function perimeterWalls(planWidth: number, planHeight: number, thickness = 12): PlanElement[] {
  return [
    makeElement('WALL', { x: 0, y: 0, width: planWidth, height: thickness }, null),
    makeElement('WALL', { x: 0, y: planHeight - thickness, width: planWidth, height: thickness }, null),
    makeElement('WALL', { x: 0, y: 0, width: thickness, height: planHeight }, null),
    makeElement('WALL', { x: planWidth - thickness, y: 0, width: thickness, height: planHeight }, null),
  ];
}

export const PLAN_TEMPLATES: PlanTemplate[] = [
  {
    key: 'blank',
    name: 'Boş plan',
    description: 'Tamamen boş bir çalışma alanı ile başla.',
    build: () => [],
  },
  {
    key: 'rectangle',
    name: 'Dikdörtgen ofis',
    description: 'Dış duvarlar, giriş kapısı, pencere cephesi, asansör ve merdiven.',
    build: (planWidth, planHeight) => {
      const wall = 12;
      return [
        ...perimeterWalls(planWidth, planHeight, wall),
        // Kuzey cephesi boyunca pencereler
        makeElement('WINDOW', { x: 120, y: 2, width: planWidth / 3, height: 10 }, null),
        makeElement('WINDOW', { x: planWidth / 2 + 60, y: 2, width: planWidth / 4, height: 10 }, null),
        // Giriş
        makeElement('DOOR', { x: planWidth / 2 - 22, y: planHeight - wall - 2, width: 44, height: 14 }, null),
        // Çekirdek
        makeElement('ELEVATOR', { x: wall + 20, y: planHeight / 2 - 45, width: 100, height: 90 }, 'Asansör'),
        makeElement('STAIRS', { x: wall + 20, y: planHeight / 2 + 55, width: 100, height: 110 }, 'Merdiven'),
        makeElement('WC', { x: wall + 20, y: wall + 20, width: 110, height: 100 }, 'WC'),
        // Çalışma alanları
        makeElement('ROOM', { x: 170, y: 40, width: planWidth - 220, height: planHeight / 2 - 70 }, 'Açık çalışma alanı'),
        makeElement('KITCHEN', { x: 170, y: planHeight - 200, width: 240, height: 150 }, 'Mutfak'),
        makeElement('TEXT', { x: 180, y: 20, width: 200, height: 24 }, 'Kat planı'),
      ];
    },
  },
  {
    key: 'central-corridor',
    name: 'Merkezi koridorlu ofis',
    description: 'Ortadan geçen koridor, iki yanda çalışma bölgeleri ve toplantı alanı.',
    build: (planWidth, planHeight) => {
      const wall = 12;
      const corridorHeight = 70;
      const corridorY = planHeight / 2 - corridorHeight / 2;
      return [
        ...perimeterWalls(planWidth, planHeight, wall),
        makeElement('CORRIDOR', { x: wall, y: corridorY, width: planWidth - wall * 2, height: corridorHeight }, 'Ana koridor'),
        makeElement('ELEVATOR', { x: planWidth / 2 - 50, y: corridorY - 100, width: 100, height: 90 }, 'Asansör'),
        makeElement('STAIRS', { x: planWidth / 2 + 70, y: corridorY - 110, width: 90, height: 100 }, 'Merdiven'),
        makeElement('WC', { x: planWidth / 2 - 170, y: corridorY - 110, width: 100, height: 100 }, 'WC'),
        makeElement('ROOM', { x: wall + 20, y: wall + 20, width: planWidth / 2 - 120, height: corridorY - wall - 40 }, 'Kuzeybatı çalışma alanı'),
        makeElement('ROOM', { x: wall + 20, y: corridorY + corridorHeight + 20, width: planWidth / 2 - 120, height: planHeight - corridorY - corridorHeight - wall - 40 }, 'Güneybatı çalışma alanı'),
        makeElement('ROOM', { x: planWidth / 2 + 190, y: corridorY + corridorHeight + 20, width: planWidth / 2 - 210, height: planHeight - corridorY - corridorHeight - wall - 40 }, 'Güneydoğu çalışma alanı'),
        makeElement('KITCHEN', { x: planWidth - 260, y: wall + 20, width: 230, height: 150 }, 'Mutfak ve dinlenme'),
        makeElement('WINDOW', { x: 100, y: 2, width: planWidth / 3, height: 10 }, null),
        makeElement('WINDOW', { x: 100, y: planHeight - 12, width: planWidth / 3, height: 10 }, null),
        makeElement('DOOR', { x: wall - 2, y: corridorY + 15, width: 14, height: 40 }, null),
      ];
    },
  },
];

/** Kopyalanan krokiye yeni geçici kimlikler verir. */
export function withFreshIds(elements: PlanElement[]): PlanElement[] {
  return elements.map((element, index) => ({ ...element, id: `kopya-${Date.now()}-${index}` }));
}
