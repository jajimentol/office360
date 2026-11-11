import { Component, computed, input, output } from '@angular/core';
import type { PlanElementType } from '../../../lib/plan';
import { cn } from '../../../lib/utils';
import { UiIcon, type IconName } from '../../../shared/ui';

export type EditorTool = 'SELECT' | PlanElementType | 'DESK' | 'ROOM_RESOURCE';
export type EditorMode = 'sketch' | 'resources';

/** Sol araç kutusu (kaynak: plan-toolbox.tsx). Araçlar düzenleme moduna göre etkinleşir. */

interface ToolDefinition {
  tool: EditorTool;
  label: string;
  hint: string;
  icon: IconName;
  mode: EditorMode | 'both';
}

const TOOL_GROUPS: Array<{ title: string; tools: ToolDefinition[] }> = [
  {
    title: 'Genel',
    tools: [{ tool: 'SELECT', label: 'Seçim', hint: 'Öğeleri seç, taşı ve boyutlandır', icon: 'MousePointer2', mode: 'both' }],
  },
  {
    title: 'Yapı',
    tools: [
      { tool: 'WALL', label: 'Duvar', hint: 'Sürükleyerek çiz · Shift ile eksene sabitle', icon: 'Minus', mode: 'sketch' },
      { tool: 'ROOM', label: 'Oda / bölge', hint: 'Dikdörtgen alan', icon: 'Square', mode: 'sketch' },
      { tool: 'CORRIDOR', label: 'Koridor', hint: 'Geçiş alanı', icon: 'MoveHorizontal', mode: 'sketch' },
    ],
  },
  {
    title: 'Detaylar',
    tools: [
      { tool: 'DOOR', label: 'Kapı', hint: 'Duvar üzerine yerleştir', icon: 'DoorOpen', mode: 'sketch' },
      { tool: 'WINDOW', label: 'Pencere', hint: 'Cephe / doğal ışık', icon: 'PanelTop', mode: 'sketch' },
      { tool: 'ELEVATOR', label: 'Asansör', hint: 'Asansör kovası', icon: 'ArrowUpDown', mode: 'sketch' },
      { tool: 'STAIRS', label: 'Merdiven', hint: 'Merdiven boşluğu', icon: 'Grid2x2', mode: 'sketch' },
      { tool: 'KITCHEN', label: 'Mutfak / sosyal', hint: 'Kafe, mutfak, dinlenme', icon: 'Coffee', mode: 'sketch' },
      { tool: 'WC', label: 'WC', hint: 'Islak hacim', icon: 'Droplets', mode: 'sketch' },
      { tool: 'RESTRICTED', label: 'Kapalı alan', hint: 'Rezervasyona kapalı bölüm', icon: 'Lock', mode: 'sketch' },
      { tool: 'TERRACE', label: 'Teras / açık alan', hint: 'Teras, bahçe, açık hava alanı', icon: 'TreePine', mode: 'sketch' },
      { tool: 'TEXT', label: 'Metin', hint: 'Serbest etiket', icon: 'Type', mode: 'sketch' },
    ],
  },
  {
    title: 'Rezervasyon nesneleri',
    tools: [
      { tool: 'DESK', label: 'Masa', hint: 'Yeni masa oluştur veya mevcut masayı taşı', icon: 'Table2', mode: 'resources' },
      { tool: 'ROOM_RESOURCE', label: 'Toplantı odası', hint: 'Sürükleyerek oda alanı çiz', icon: 'Square', mode: 'resources' },
    ],
  },
];

@Component({
  selector: 'app-plan-toolbox',
  imports: [UiIcon],
  template: `
    <div class="space-y-4">
      @for (group of visibleGroups(); track group.title) {
        <div>
          <p class="mb-1.5 px-1 text-[11px] font-semibold uppercase tracking-wider text-ink-400">{{ group.title }}</p>
          <ul class="space-y-1">
            @for (item of group.tools; track item.tool) {
              @let active = tool() === item.tool;
              <li>
                <button
                  type="button"
                  (click)="selected.emit(item.tool)"
                  [attr.aria-pressed]="active"
                  [title]="item.hint"
                  [class]="cn('flex w-full items-center gap-2.5 rounded-lg border px-2.5 py-2 text-left text-[13px] transition-colors', active ? 'border-brand-500 bg-brand-600 text-white' : 'border-transparent text-ink-700 hover:border-line hover:bg-slate-50')"
                >
                  <svg uiIcon [name]="item.icon" [class]="cn('h-4 w-4 shrink-0', active ? 'text-white' : 'text-ink-400')"></svg>
                  <span class="min-w-0 flex-1 truncate">{{ item.label }}</span>
                </button>
              </li>
            }
          </ul>
        </div>
      }

      <p class="rounded-lg bg-slate-50 px-3 py-2 text-[11px] leading-relaxed text-ink-500">
        {{ mode() === 'sketch' ? 'Bir araç seçip çalışma alanına sürükleyin. Duvarda Shift tuşu yatay/dikey eksene sabitler.' : 'Masa ve odaları sürükleyerek krokinin üzerine yerleştirin. Yeni masa için Masa aracına tıklayın.' }}
      </p>
    </div>
  `,
})
export class PlanToolbox {
  readonly tool = input.required<EditorTool>();
  readonly mode = input.required<EditorMode>();
  readonly selected = output<EditorTool>();
  readonly cn = cn;

  readonly visibleGroups = computed(() =>
    TOOL_GROUPS.map((group) => ({ title: group.title, tools: group.tools.filter((item) => item.mode === 'both' || item.mode === this.mode()) })).filter(
      (group) => group.tools.length > 0,
    ),
  );
}
