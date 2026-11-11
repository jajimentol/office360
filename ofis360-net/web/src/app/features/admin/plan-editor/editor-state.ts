import { computed, signal } from '@angular/core';
import type { PlanElement } from '../../../lib/plan';

/** Kat planı editörünün durum ve geçmiş (geri al / yinele) yönetimi (kaynak: use-plan-editor.ts). */

export interface EditorDesk {
  id: string;
  code: string;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  zoneId: string;
  zoneName: string;
  floorName: string;
  active: boolean;
  popular: boolean;
  accessibleOnly: boolean;
  reservationCount: number;
}

export interface EditorRoom {
  id: string;
  code: string;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
  capacity: number;
  active: boolean;
  reservationCount: number;
}

export interface EditorSnapshot {
  elements: PlanElement[];
  desks: EditorDesk[];
  rooms: EditorRoom[];
}

/**
 * Editörün ortak seçilebilir nesne arayüzü. Kroki öğeleri ve rezervasyon kaynakları (masa, toplantı odası)
 * aynı seçim sistemine dahildir; hiçbiri dekoratif şekle dönüştürülmez.
 */
export type SelectionKind = 'planElement' | 'desk' | 'meetingRoom';

export interface SelectionItem {
  kind: SelectionKind;
  id: string;
}

export interface Point {
  x: number;
  y: number;
}

export interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

const HISTORY_LIMIT = 60;

/** Kaydedilmemiş değişiklik tespiti için sadeleştirilmiş imza. */
export function fingerprint(snapshot: EditorSnapshot): string {
  return JSON.stringify({
    elements: [...snapshot.elements]
      .map((element) => ({
        id: element.id,
        type: element.type,
        label: element.label ?? null,
        x: element.x,
        y: element.y,
        width: element.width,
        height: element.height,
        rotation: element.rotation,
        style: element.style ?? {},
        layerOrder: element.layerOrder,
        locked: element.locked,
        visible: element.visible,
        linkedResourceId: element.linkedResourceId ?? null,
        linkedResourceType: element.linkedResourceType ?? null,
      }))
      .sort((a, b) => a.id.localeCompare(b.id)),
    desks: [...snapshot.desks]
      .map((desk) => ({ id: desk.id, x: desk.x, y: desk.y, width: desk.width, height: desk.height, rotation: desk.rotation }))
      .sort((a, b) => a.id.localeCompare(b.id)),
    rooms: [...snapshot.rooms]
      .map((room) => ({ id: room.id, x: room.x, y: room.y, width: room.width, height: room.height }))
      .sort((a, b) => a.id.localeCompare(b.id)),
  });
}

export class PlanEditorState {
  readonly snapshot = signal<EditorSnapshot>({ elements: [], desks: [], rooms: [] });
  private readonly past = signal<EditorSnapshot[]>([]);
  private readonly future = signal<EditorSnapshot[]>([]);
  private readonly savedFingerprint = signal('');
  readonly selection = signal<SelectionItem[]>([]);
  readonly clipboard = signal<PlanElement[]>([]);

  readonly canUndo = computed(() => this.past().length > 0);
  readonly canRedo = computed(() => this.future().length > 0);
  readonly isDirty = computed(() => fingerprint(this.snapshot()) !== this.savedFingerprint());

  constructor(initial: EditorSnapshot) {
    this.reset(initial);
  }

  /** Kat değiştiğinde editör durumu sıfırlanır. */
  reset(initial: EditorSnapshot): void {
    this.snapshot.set(initial);
    this.past.set([]);
    this.future.set([]);
    this.savedFingerprint.set(fingerprint(initial));
    this.selection.set([]);
  }

  apply(updater: (current: EditorSnapshot) => EditorSnapshot, options?: { commit?: boolean }): void {
    const current = this.snapshot();
    const next = updater(current);
    if (options?.commit === false) {
      // Sürükleme sırasındaki ara adımlar geçmişe ayrı kayıt olarak yazılmaz.
      this.snapshot.set(next);
      return;
    }
    this.past.update((past) => [...past, current].slice(-HISTORY_LIMIT));
    this.snapshot.set(next);
    this.future.set([]);
  }

  undo(): void {
    const past = this.past();
    const previous = past.at(-1);
    if (!previous) return;
    this.future.update((future) => [this.snapshot(), ...future].slice(0, HISTORY_LIMIT));
    this.past.set(past.slice(0, -1));
    this.snapshot.set(previous);
    this.selection.set([]);
  }

  redo(): void {
    const future = this.future();
    const next = future[0];
    if (!next) return;
    this.past.update((past) => [...past, this.snapshot()].slice(-HISTORY_LIMIT));
    this.future.set(future.slice(1));
    this.snapshot.set(next);
    this.selection.set([]);
  }

  markSaved(saved: EditorSnapshot): void {
    this.savedFingerprint.set(fingerprint(saved));
  }

  isSelected(kind: SelectionKind, id: string): boolean {
    return this.selection().some((item) => item.kind === kind && item.id === id);
  }
}
