/**
 * Kat krokisi ortak tanimlari.
 *
 * Kroki, adminin uygulama icinde cizdigi fiziksel plan katmanidir.
 * Rezervasyon kayitlarindan (Desk / MeetingRoom) bagimsizdir: masa ve odalarin
 * konumu kendi tablolarinda tutulur, kroki yalnizca duvar, kapi, koridor gibi
 * fiziksel ogeleri tanimlar.
 */

export const PLAN_ELEMENT_TYPES = [
  'WALL',
  'ROOM',
  'CORRIDOR',
  'DOOR',
  'WINDOW',
  'ELEVATOR',
  'STAIRS',
  'KITCHEN',
  'WC',
  'RESTRICTED',
  'TERRACE',
  'TEXT',
] as const;

export type PlanElementType = (typeof PLAN_ELEMENT_TYPES)[number];

/** Krokide bir kaynagi temsil edebilen oge turleri. */
export const LINKABLE_RESOURCE_TYPES = ['DESK', 'MEETING_ROOM'] as const;
export type LinkedResourceType = (typeof LINKABLE_RESOURCE_TYPES)[number];

export interface PlanElementStyle {
  fill?: string;
  stroke?: string;
  strokeWidth?: number;
  fontSize?: number;
}

export interface PlanElement {
  id: string;
  type: PlanElementType;
  label: string | null;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  style: PlanElementStyle;
  layerOrder: number;
  locked: boolean;
  visible: boolean;
  linkedResourceId: string | null;
  linkedResourceType: LinkedResourceType | null;
}

export interface PlanElementTypeDefinition {
  type: PlanElementType;
  label: string;
  description: string;
  icon: string;
  defaultWidth: number;
  defaultHeight: number;
  defaultLabel: string | null;
  defaultStyle: Required<Pick<PlanElementStyle, 'fill' | 'stroke' | 'strokeWidth'>> & {
    fontSize?: number;
  };
  /** Dondurulebilir mi? */
  rotatable: boolean;
  /** Cizim sirasinda iki nokta ile mi olusturulur (duvar gibi)? */
  lineTool: boolean;
  /** Varsayilan katman sirasi: kucuk deger arkada kalir. */
  baseLayer: number;
}

export const PLAN_ELEMENT_DEFINITIONS: Record<PlanElementType, PlanElementTypeDefinition> = {
  ROOM: {
    type: 'ROOM',
    label: 'Oda / bölge',
    description: 'Çalışma alanı, ofis veya bölge dikdörtgeni',
    icon: 'square',
    defaultWidth: 260,
    defaultHeight: 180,
    defaultLabel: 'Yeni bölge',
    defaultStyle: { fill: '#f8fafc', stroke: '#cbd5e1', strokeWidth: 2 },
    rotatable: false,
    lineTool: false,
    baseLayer: 10,
  },
  CORRIDOR: {
    type: 'CORRIDOR',
    label: 'Koridor',
    description: 'Geçiş alanı',
    icon: 'move-horizontal',
    defaultWidth: 400,
    defaultHeight: 56,
    defaultLabel: 'Koridor',
    defaultStyle: { fill: '#f1f5f9', stroke: '#e2e8f0', strokeWidth: 2 },
    rotatable: true,
    lineTool: false,
    baseLayer: 12,
  },
  WALL: {
    type: 'WALL',
    label: 'Duvar',
    description: 'Başlangıç ve bitiş noktasıyla çizilir',
    icon: 'minus',
    defaultWidth: 200,
    defaultHeight: 10,
    defaultLabel: null,
    defaultStyle: { fill: '#334155', stroke: '#1e293b', strokeWidth: 0 },
    rotatable: true,
    lineTool: true,
    baseLayer: 30,
  },
  DOOR: {
    type: 'DOOR',
    label: 'Kapı',
    description: 'Duvar üzerindeki geçiş',
    icon: 'door-open',
    defaultWidth: 44,
    defaultHeight: 12,
    defaultLabel: null,
    defaultStyle: { fill: '#fbbf24', stroke: '#b45309', strokeWidth: 1.5 },
    rotatable: true,
    lineTool: false,
    baseLayer: 40,
  },
  WINDOW: {
    type: 'WINDOW',
    label: 'Pencere',
    description: 'Cephe / doğal ışık',
    icon: 'panel-top',
    defaultWidth: 120,
    defaultHeight: 10,
    defaultLabel: null,
    defaultStyle: { fill: '#bae6fd', stroke: '#0284c7', strokeWidth: 1.5 },
    rotatable: true,
    lineTool: false,
    baseLayer: 40,
  },
  ELEVATOR: {
    type: 'ELEVATOR',
    label: 'Asansör',
    description: 'Asansör kovası',
    icon: 'arrow-up-down',
    defaultWidth: 100,
    defaultHeight: 90,
    defaultLabel: 'Asansör',
    defaultStyle: { fill: '#e2e8f0', stroke: '#94a3b8', strokeWidth: 2 },
    rotatable: false,
    lineTool: false,
    baseLayer: 20,
  },
  STAIRS: {
    type: 'STAIRS',
    label: 'Merdiven',
    description: 'Merdiven boşluğu',
    icon: 'stairs',
    defaultWidth: 100,
    defaultHeight: 120,
    defaultLabel: 'Merdiven',
    defaultStyle: { fill: '#e2e8f0', stroke: '#94a3b8', strokeWidth: 2 },
    rotatable: true,
    lineTool: false,
    baseLayer: 20,
  },
  KITCHEN: {
    type: 'KITCHEN',
    label: 'Mutfak / sosyal alan',
    description: 'Kafe, mutfak veya dinlenme alanı',
    icon: 'coffee',
    defaultWidth: 220,
    defaultHeight: 160,
    defaultLabel: 'Mutfak',
    defaultStyle: { fill: '#f0fdfa', stroke: '#5eead4', strokeWidth: 2 },
    rotatable: false,
    lineTool: false,
    baseLayer: 15,
  },
  WC: {
    type: 'WC',
    label: 'WC',
    description: 'Islak hacim',
    icon: 'droplets',
    defaultWidth: 110,
    defaultHeight: 110,
    defaultLabel: 'WC',
    defaultStyle: { fill: '#eef2ff', stroke: '#a5b4fc', strokeWidth: 2 },
    rotatable: false,
    lineTool: false,
    baseLayer: 15,
  },
  RESTRICTED: {
    type: 'RESTRICTED',
    label: 'Rezervasyona kapalı alan',
    description: 'Çalışan rezervasyonuna kapalı bölüm',
    icon: 'lock',
    defaultWidth: 200,
    defaultHeight: 150,
    defaultLabel: 'Kapalı alan',
    defaultStyle: { fill: '#fef2f2', stroke: '#fca5a5', strokeWidth: 2 },
    rotatable: false,
    lineTool: false,
    baseLayer: 15,
  },
  TERRACE: {
    type: 'TERRACE',
    label: 'Teras / açık alan',
    description: 'Teras, bahçe veya açık hava alanı',
    icon: 'tree-pine',
    defaultWidth: 300,
    defaultHeight: 120,
    defaultLabel: 'Teras',
    defaultStyle: { fill: '#ecfdf5', stroke: '#86efac', strokeWidth: 2 },
    rotatable: false,
    lineTool: false,
    baseLayer: 5,
  },
  TEXT: {
    type: 'TEXT',
    label: 'Metin / etiket',
    description: 'Serbest açıklama',
    icon: 'type',
    defaultWidth: 160,
    defaultHeight: 28,
    defaultLabel: 'Etiket',
    defaultStyle: { fill: 'transparent', stroke: 'transparent', strokeWidth: 0, fontSize: 14 },
    rotatable: true,
    lineTool: false,
    baseLayer: 50,
  },
};

/** Ozellik panelinde sunulan kontrollu renk secenekleri. */
export const PLAN_FILL_COLORS = [
  { value: '#ffffff', label: 'Beyaz' },
  { value: '#f8fafc', label: 'Açık gri' },
  { value: '#e2e8f0', label: 'Gri' },
  { value: '#eef4fb', label: 'Açık mavi' },
  { value: '#dbeafe', label: 'Mavi' },
  { value: '#f0fdfa', label: 'Turkuaz' },
  { value: '#fef9c3', label: 'Sarı' },
  { value: '#fef2f2', label: 'Kırmızı' },
  { value: '#f5f3ff', label: 'Mor' },
  { value: '#334155', label: 'Koyu gri' },
  { value: 'transparent', label: 'Saydam' },
] as const;

export const PLAN_STROKE_COLORS = [
  { value: '#1e293b', label: 'Koyu' },
  { value: '#94a3b8', label: 'Gri' },
  { value: '#cbd5e1', label: 'Açık gri' },
  { value: '#2f6db5', label: 'Mavi' },
  { value: '#0d9488', label: 'Turkuaz' },
  { value: '#b45309', label: 'Turuncu' },
  { value: '#dc2626', label: 'Kırmızı' },
  { value: 'transparent', label: 'Çizgisiz' },
] as const;

export const PLAN_GRID_SIZE = 10;
export const PLAN_MIN_SIZE = 8;
export const PLAN_MAX_ELEMENTS = 400;

/** Rezerve edilebilir nesnelerin krokideki varsayilan olculeri. */
export const RESOURCE_SIZES = {
  desk: { width: 46, height: 34 },
  room: { width: 200, height: 120 },
} as const;

export function snapToGrid(value: number, enabled: boolean, grid = PLAN_GRID_SIZE): number {
  if (!enabled) return Math.round(value);
  return Math.round(value / grid) * grid;
}

export function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

/**
 * Bir dikdortgeni plan sinirlari icinde tutar.
 * Oge plandan buyukse once boyutu kisilir.
 */
export function clampRect(
  rect: { x: number; y: number; width: number; height: number },
  planWidth: number,
  planHeight: number,
): { x: number; y: number; width: number; height: number } {
  const width = clamp(rect.width, PLAN_MIN_SIZE, planWidth);
  const height = clamp(rect.height, PLAN_MIN_SIZE, planHeight);
  return {
    width,
    height,
    x: clamp(rect.x, 0, planWidth - width),
    y: clamp(rect.y, 0, planHeight - height),
  };
}

/** Negatif genislik/yukseklikle cizilen dikdortgeni normalize eder. */
export function normalizeRect(
  start: { x: number; y: number },
  end: { x: number; y: number },
): { x: number; y: number; width: number; height: number } {
  return {
    x: Math.min(start.x, end.x),
    y: Math.min(start.y, end.y),
    width: Math.abs(end.x - start.x),
    height: Math.abs(end.y - start.y),
  };
}

/**
 * Duvar cizimi: iki nokta arasindaki kalin cizgiyi dikdortgene cevirir.
 * axisLock true ise en yakin eksene (yatay/dikey) sabitlenir.
 */
export function wallRectFromPoints(
  start: { x: number; y: number },
  end: { x: number; y: number },
  thickness: number,
  axisLock: boolean,
): { x: number; y: number; width: number; height: number } {
  let target = end;
  if (axisLock) {
    const deltaX = Math.abs(end.x - start.x);
    const deltaY = Math.abs(end.y - start.y);
    target = deltaX >= deltaY ? { x: end.x, y: start.y } : { x: start.x, y: end.y };
  }

  const horizontal = Math.abs(target.x - start.x) >= Math.abs(target.y - start.y);
  if (horizontal) {
    const x = Math.min(start.x, target.x);
    const width = Math.max(Math.abs(target.x - start.x), PLAN_MIN_SIZE);
    return { x, y: start.y - thickness / 2, width, height: thickness };
  }
  const y = Math.min(start.y, target.y);
  const height = Math.max(Math.abs(target.y - start.y), PLAN_MIN_SIZE);
  return { x: start.x - thickness / 2, y, width: thickness, height };
}

export function parsePlanStyle(raw: string | null | undefined): PlanElementStyle {
  if (!raw) return {};
  try {
    const parsed = JSON.parse(raw) as PlanElementStyle;
    if (!parsed || typeof parsed !== 'object') return {};
    return parsed;
  } catch {
    return {};
  }
}

export function planElementDefaults(type: PlanElementType): PlanElementTypeDefinition {
  return PLAN_ELEMENT_DEFINITIONS[type];
}

/** Krokide gosterilecek tur etiketi. */
export function planElementLabel(type: PlanElementType): string {
  return PLAN_ELEMENT_DEFINITIONS[type]?.label ?? type;
}
