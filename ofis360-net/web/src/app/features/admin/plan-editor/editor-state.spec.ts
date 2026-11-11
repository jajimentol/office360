import { PlanEditorState, fingerprint, type EditorSnapshot } from './editor-state';
import type { PlanElement } from '../../../lib/plan';

function element(id: string, x = 10): PlanElement {
  return { id, type: 'WALL', label: null, x, y: 10, width: 100, height: 10, rotation: 0, style: {}, layerOrder: 30, locked: false, visible: true, linkedResourceId: null, linkedResourceType: null };
}

const initial: EditorSnapshot = { elements: [element('a')], desks: [], rooms: [] };

describe('PlanEditorState (kaynak: use-plan-editor.ts)', () => {
  it('apply geçmişe yazar, undo/redo gezinir, kaydedilince isDirty sıfırlanır', () => {
    const state = new PlanEditorState(initial);
    expect(state.isDirty()).toBe(false);
    expect(state.canUndo()).toBe(false);

    state.apply((current) => ({ ...current, elements: [...current.elements, element('b')] }));
    expect(state.snapshot().elements.length).toBe(2);
    expect(state.isDirty()).toBe(true);
    expect(state.canUndo()).toBe(true);

    state.undo();
    expect(state.snapshot().elements.length).toBe(1);
    expect(state.isDirty()).toBe(false);
    expect(state.canRedo()).toBe(true);

    state.redo();
    expect(state.snapshot().elements.length).toBe(2);
    state.markSaved(state.snapshot());
    expect(state.isDirty()).toBe(false);
  });

  it('commit:false ara adımları geçmişe yazmaz (sürükleme tek kayıt olur)', () => {
    const state = new PlanEditorState(initial);
    state.apply((current) => ({ ...current, elements: [element('a', 20)] }), { commit: false });
    state.apply((current) => ({ ...current, elements: [element('a', 30)] }), { commit: false });
    expect(state.canUndo()).toBe(false);
    state.apply((current) => ({ ...current }), { commit: true });
    expect(state.canUndo()).toBe(true);
    state.undo();
    expect(state.snapshot().elements[0].x).toBe(30); // son ara adım, geçmişteki tek kayıt
  });

  it('fingerprint sıralamadan bağımsızdır ve konum değişimini yakalar', () => {
    const a = fingerprint({ elements: [element('a'), element('b')], desks: [], rooms: [] });
    const b = fingerprint({ elements: [element('b'), element('a')], desks: [], rooms: [] });
    expect(a).toBe(b);
    expect(fingerprint({ elements: [element('a', 99)], desks: [], rooms: [] })).not.toBe(fingerprint(initial));
  });
});
