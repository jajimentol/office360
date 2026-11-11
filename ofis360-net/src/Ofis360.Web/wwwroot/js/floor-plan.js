/*
 * Kat plani SVG cizimi (eski Angular floor-plan-svg.ts + plan-shapes.ts karsiligi).
 *
 * Renkler (11 Kasim karari):
 *   - Dolu masa ve tamamen dolu oda: #FF0025
 *   - Kullanicinin kendi masasi:     #0135EB
 *   - Kismen dolu oda:               #6B8BF5 (acik mavi)
 * Durumlar yalnizca renkle degil, metin etiketi ve desenle de ayristirilir (renk korlugu).
 */
(function () {
  'use strict';

  var COLORS = {
    booked: '#FF0025', bookedStroke: '#B3001A',
    mine: '#0135EB', mineStroke: '#00208F',
    partial: '#6B8BF5', partialStroke: '#3F5FD6'
  };

  var DESK_STYLE = {
    AVAILABLE: { fill: '#FFFFFF', stroke: '#94A3B8', text: '#334155', label: 'Uygun', status: null },
    MINE: { fill: COLORS.mine, stroke: COLORS.mineStroke, text: '#FFFFFF', label: 'Sizin masanız', status: '✓ siz' },
    RESERVED: { fill: COLORS.booked, stroke: COLORS.bookedStroke, text: '#FFFFFF', label: 'Dolu', status: 'dolu' },
    CLOSED: { fill: 'url(#o3-p-closed)', stroke: '#94A3B8', text: '#475569', label: 'Kullanıma kapalı', status: 'kapalı' },
    ACCESSIBLE: { fill: 'url(#o3-p-accessible)', stroke: '#7C3AED', text: '#5B21B6', label: 'Erişilebilirlik için ayrılmış', status: null },
    RESTRICTED_BY_RULE: { fill: 'url(#o3-p-restricted)', stroke: '#D97706', text: '#92400E', label: 'Kural gereği seçilemez', status: null }
  };

  var ROOM_STYLE = {
    FREE: { fill: '#FFFFFF', stroke: COLORS.partial, title: '#1E3A8A', sub: '#3B4F8F', label: 'boş' },
    PARTIAL: { fill: COLORS.partial, stroke: COLORS.partialStroke, title: '#0B1B4D', sub: '#0B1B4D', label: 'kısmen dolu' },
    FULL: { fill: COLORS.booked, stroke: COLORS.bookedStroke, title: '#FFFFFF', sub: '#FFFFFF', label: 'tamamen dolu' },
    CLOSED: { fill: 'url(#o3-p-closed)', stroke: '#94A3B8', title: '#334155', sub: '#475569', label: 'kapalı' }
  };

  // Kroki ogelerinin varsayilan stilleri (eski lib/plan.ts PLAN_ELEMENT_DEFINITIONS).
  var ELEMENT_DEFAULTS = {
    ROOM: { fill: '#f8fafc', stroke: '#cbd5e1', strokeWidth: 2 },
    CORRIDOR: { fill: '#f1f5f9', stroke: '#e2e8f0', strokeWidth: 2 },
    WALL: { fill: '#334155', stroke: '#1e293b', strokeWidth: 0 },
    DOOR: { fill: '#fbbf24', stroke: '#b45309', strokeWidth: 1.5 },
    WINDOW: { fill: '#bae6fd', stroke: '#0284c7', strokeWidth: 1.5 },
    ELEVATOR: { fill: '#e2e8f0', stroke: '#94a3b8', strokeWidth: 2 },
    STAIRS: { fill: '#e2e8f0', stroke: '#94a3b8', strokeWidth: 2 },
    KITCHEN: { fill: '#f0fdfa', stroke: '#5eead4', strokeWidth: 2 },
    WC: { fill: '#eef2ff', stroke: '#a5b4fc', strokeWidth: 2 },
    RESTRICTED: { fill: '#fef2f2', stroke: '#fca5a5', strokeWidth: 2 },
    TERRACE: { fill: '#ecfdf5', stroke: '#86efac', strokeWidth: 2 },
    TEXT: { fill: 'transparent', stroke: 'transparent', strokeWidth: 0, fontSize: 14 }
  };
  /** Kroki ogesi dolgusu "hatch-red" ise acik kirmizi tarama ile cizilir (kapali alanlar). */
  var HATCH_RED = 'hatch-red';
  var HATCH_PATTERN = '<pattern id="o3-p-hatch-red" width="14" height="14" patternTransform="rotate(45)" patternUnits="userSpaceOnUse"><rect width="14" height="14" fill="rgba(254, 226, 226, 0.78)"></rect><line x1="0" y1="0" x2="0" y2="14" stroke="rgba(248, 113, 113, 0.75)" stroke-width="4"></line></pattern>';

  var LABELLED = ['ROOM', 'CORRIDOR', 'KITCHEN', 'WC', 'RESTRICTED', 'ELEVATOR', 'STAIRS', 'TERRACE'];
  var ZONE_STYLE = {
    WORK: { fill: '#f8fafc', stroke: '#cbd5e1', label: '#64748b' },
    MEETING: { fill: '#eff6ff', stroke: '#bfdbfe', label: '#1d4ed8' },
    SOCIAL: { fill: '#f0fdfa', stroke: '#99f6e4', label: '#0f766e' },
    CORRIDOR: { fill: '#f1f5f9', stroke: '#e2e8f0', label: '#94a3b8' },
    CORE: { fill: '#e2e8f0', stroke: '#cbd5e1', label: '#475569' },
    RESTRICTED: { fill: '#fef2f2', stroke: '#fecaca', label: '#b91c1c' }
  };

  /** "PT36-A05-01" -> "A05-01" */
  function shortDesk(code) { return String(code || '').replace(/^[A-Z]{1,3}\d{0,3}-/, ''); }

  function renderElement(e) {
    var d = ELEMENT_DEFAULTS[e.type] || ELEMENT_DEFAULTS.ROOM;
    var st = e.style || {};
    var s = {
      fill: st.fill || d.fill, stroke: st.stroke || d.stroke,
      strokeWidth: st.strokeWidth != null ? st.strokeWidth : d.strokeWidth,
      fontSize: st.fontSize || d.fontSize || 12
    };
    var cx = e.x + e.width / 2, cy = e.y + e.height / 2;
    var r = {
      id: e.id, type: e.type, x: e.x, y: e.y, w: e.width, h: e.height, s: s, label: e.label,
      rx: (e.type === 'WALL' || e.type === 'WINDOW' || e.type === 'DOOR') ? 2 : 8,
      transform: e.rotation ? 'rotate(' + e.rotation + ' ' + cx + ' ' + cy + ')' : null,
      textFill: s.stroke === 'transparent' ? '#334155' : s.stroke,
      showLabel: !!e.label && e.type !== 'TEXT' && e.width > 60 && e.height > 24 && LABELLED.indexOf(e.type) >= 0,
      lines: [], dots: [], path: null, cx: cx, cy: cy
    };
    if (e.type === 'STAIRS') {
      var steps = Math.max(3, Math.min(8, Math.floor(e.height / 18)));
      for (var i = 1; i < steps; i++) r.lines.push({ x1: e.x + 4, y1: e.y + i * e.height / steps, x2: e.x + e.width - 4, y2: e.y + i * e.height / steps });
    } else if (e.type === 'ELEVATOR') {
      r.lines.push({ x1: e.x + 6, y1: cy, x2: e.x + e.width - 6, y2: cy });
      r.path = 'M ' + cx + ' ' + (cy - 16) + ' l -6 8 h 12 z M ' + cx + ' ' + (cy + 16) + ' l -6 -8 h 12 z';
    } else if (e.type === 'DOOR') {
      var rad = Math.min(e.width, 34);
      r.arc = 'M ' + e.x + ' ' + (e.y + e.height) + ' a ' + rad + ' ' + rad + ' 0 0 1 ' + rad + ' ' + (-rad);
    } else if (e.type === 'RESTRICTED' && s.fill === HATCH_RED) {
      // Kapali alan (orn. Anadolu Bolge Mudurlugu): acik kirmizi tarama, ortada buyuk etiket.
      r.s = angular.extend({}, s, { fill: 'url(#o3-p-hatch-red)', stroke: s.stroke || '#f87171', strokeWidth: s.strokeWidth || 2 });
      r.showLabel = false;
      r.bigLabel = e.label;
      r.bigSize = st.fontSize || 22;
      // Etiket alanin ust seridine yazilir; alandaki masalarin altinda kalmaz.
      r.bigY = e.y + r.bigSize + 10;
    } else if (e.type === 'RESTRICTED') {
      r.diag = 'M ' + e.x + ' ' + (e.y + e.height) + ' L ' + (e.x + e.width) + ' ' + e.y;
    } else if (e.type === 'TERRACE' && e.width >= 60 && e.height >= 36) {
      var step = 44, cols = Math.max(1, Math.floor((e.width - 24) / step)), rows = Math.max(1, Math.floor((e.height - 24) / step));
      for (var a = 0; a < rows; a++) for (var b = 0; b < cols; b++) {
        r.dots.push({ cx: e.x + 22 + b * step + (e.width - 24 - (cols - 1) * step - 20) / 2, cy: e.y + 22 + a * step + (e.height - 24 - (rows - 1) * step - 20) / 2 });
      }
    }
    return r;
  }

  function renderDesk(desk, selectedId) {
    var style = DESK_STYLE[desk.state] || DESK_STYLE.AVAILABLE;
    var w = desk.width || 46, h = desk.height || 34;
    var tall = h > w * 1.3;
    return {
      desk: desk, style: style, w: w, h: h, x: desk.x - w / 2, y: desk.y - h / 2, tall: tall,
      selected: desk.id === selectedId,
      label: shortDesk(desk.code),
      transform: desk.rotation ? 'rotate(' + desk.rotation + ' ' + desk.x + ' ' + desk.y + ')' : null,
      labelTransform: tall ? 'rotate(-90 ' + (desk.x - 5) + ' ' + desk.y + ')' : null,
      statusTransform: tall ? 'rotate(-90 ' + (desk.x + 7) + ' ' + desk.y + ')' : null,
      aria: desk.code + ' masası, ' + desk.zoneName + ', durum: ' + style.label + (desk.occupant ? ', kullanan: ' + desk.occupant.fullName : '')
    };
  }

  function renderRoom(room, selectedId) {
    var key = room.closed ? 'CLOSED' : (room.dayState || 'FREE');
    var style = ROOM_STYLE[key] || ROOM_STYLE.FREE;
    return {
      room: room, style: style, selected: room.id === selectedId,
      cx: room.x + room.width / 2, cy: room.y + room.height / 2,
      sub: room.code + ' · ' + room.capacity + ' kişi',
      aria: room.name + ' toplantı odası, ' + room.capacity + ' kişilik, ' + style.label
    };
  }

  /** Plan duzenleyicisi ayni cizim kurallarini kullanir. */
  angular.module('ofis360').constant('planShapes', {
    renderElement: renderElement, elementDefaults: ELEMENT_DEFAULTS, shortDesk: shortDesk, roomStyle: ROOM_STYLE, hatchPattern: HATCH_PATTERN, hatchRed: HATCH_RED
  });

  angular.module('ofis360').directive('o3FloorPlan', function () {
    return {
      restrict: 'E',
      scope: { plan: '<', selectedDeskId: '<', selectedRoomId: '<', highlighted: '<?', deskFill: '<?', onDesk: '&', onRoom: '&' },
      template:
        '<svg ng-if="plan" class="floor-svg" ng-attr-view_box="0 0 {{ plan.floor.planWidth }} {{ plan.floor.planHeight }}" role="group" aria-label="Kat planı">' +
        '<defs>' +
        '  <pattern id="o3-p-closed" width="8" height="8" patternTransform="rotate(135)" patternUnits="userSpaceOnUse"><rect width="8" height="8" fill="#e2e8f0"></rect><line x1="0" y1="0" x2="0" y2="8" stroke="#94a3b8" stroke-width="3"></line></pattern>' +
        '  <pattern id="o3-p-accessible" width="8" height="8" patternUnits="userSpaceOnUse"><rect width="8" height="8" fill="#f5f3ff"></rect><circle cx="4" cy="4" r="1.6" fill="#a78bfa"></circle></pattern>' +
        HATCH_PATTERN +
        '  <pattern id="o3-p-restricted" width="7" height="7" patternTransform="rotate(45)" patternUnits="userSpaceOnUse"><rect width="7" height="7" fill="#fef3c7"></rect><line x1="0" y1="0" x2="0" y2="7" stroke="#fbbf24" stroke-width="2.5"></line></pattern>' +
        '</defs>' +
        '<rect x="0" y="0" ng-attr-width="{{ plan.floor.planWidth }}" ng-attr-height="{{ plan.floor.planHeight }}" fill="#ffffff" rx="10"></rect>' +

        // Fiziksel kroki ya da (cizilmemisse) bolge dikdortgenleri
        '<g ng-repeat="e in elements track by e.id" ng-attr-transform="{{ e.transform || undefined }}">' +
        '  <text ng-if="e.type === \'TEXT\'" ng-attr-x="{{ e.x }}" ng-attr-y="{{ e.y + e.h / 2 + e.s.fontSize / 3 }}" ng-attr-font-size="{{ e.s.fontSize }}" font-weight="600" ng-attr-fill="{{ e.textFill }}">{{ e.label || \'\' }}</text>' +
        '  <rect ng-if="e.type !== \'TEXT\'" ng-attr-x="{{ e.x }}" ng-attr-y="{{ e.y }}" ng-attr-width="{{ e.w }}" ng-attr-height="{{ e.h }}" ng-attr-rx="{{ e.rx }}" ng-attr-fill="{{ e.s.fill }}" ng-attr-stroke="{{ e.s.stroke }}" ng-attr-stroke-width="{{ e.s.strokeWidth }}"></rect>' +
        '  <line ng-repeat="l in e.lines" ng-attr-x1="{{ l.x1 }}" ng-attr-y1="{{ l.y1 }}" ng-attr-x2="{{ l.x2 }}" ng-attr-y2="{{ l.y2 }}" ng-attr-stroke="{{ e.s.stroke }}" stroke-width="1" opacity="0.7"></line>' +
        '  <path ng-if="e.path" ng-attr-d="{{ e.path }}" ng-attr-fill="{{ e.s.stroke }}" opacity="0.75"></path>' +
        '  <path ng-if="e.arc" ng-attr-d="{{ e.arc }}" fill="none" ng-attr-stroke="{{ e.s.stroke }}" stroke-width="1.2" stroke-dasharray="3 3"></path>' +
        '  <path ng-if="e.diag" ng-attr-d="{{ e.diag }}" stroke="#fca5a5" stroke-width="1.5" stroke-dasharray="6 4" opacity="0.8"></path>' +
        '  <circle ng-repeat="dot in e.dots" ng-attr-cx="{{ dot.cx }}" ng-attr-cy="{{ dot.cy }}" r="5" fill="#4ade80" opacity="0.55"></circle>' +
        '  <text ng-if="e.type === \'WC\'" ng-attr-x="{{ e.cx }}" ng-attr-y="{{ e.cy + 5 }}" text-anchor="middle" font-size="14" font-weight="600" fill="#4f46e5">WC</text>' +
        '  <text ng-if="e.showLabel" ng-attr-x="{{ e.x + 10 }}" ng-attr-y="{{ e.y + 18 }}" font-size="11" font-weight="600" fill="#64748b" class="no-pointer">{{ e.label }}</text>' +
        '  <text ng-if="e.bigLabel" ng-attr-x="{{ e.cx }}" ng-attr-y="{{ e.bigY }}" ng-attr-font-size="{{ e.bigSize }}" font-weight="800" fill="#b91c1c" text-anchor="middle" dominant-baseline="middle" class="no-pointer big-label">{{ e.bigLabel }}</text>' +
        '</g>' +
        '<g ng-repeat="z in zones track by z.id">' +
        '  <rect ng-attr-x="{{ z.x }}" ng-attr-y="{{ z.y }}" ng-attr-width="{{ z.width }}" ng-attr-height="{{ z.height }}" rx="10" ng-attr-fill="{{ z.st.fill }}" ng-attr-stroke="{{ z.st.stroke }}" stroke-width="1.5"></rect>' +
        '  <text ng-attr-x="{{ z.x + 12 }}" ng-attr-y="{{ z.y + 20 }}" ng-attr-fill="{{ z.st.label }}" font-size="12" font-weight="600" class="no-pointer">{{ z.name }}</text>' +
        '</g>' +

        // Toplanti odalari
        '<g ng-repeat="r in rooms track by r.room.id" class="room" ng-class="{ selected: r.selected }" role="button" tabindex="0" aria-label="{{ r.aria }}"' +
        '   ng-click="onRoom({ id: r.room.id })" ng-keydown="key($event, \'room\', r.room.id)">' +
        '  <title>{{ r.aria }}</title>' +
        '  <rect ng-attr-x="{{ r.room.x }}" ng-attr-y="{{ r.room.y }}" ng-attr-width="{{ r.room.width }}" ng-attr-height="{{ r.room.height }}" rx="8" ng-attr-fill="{{ r.style.fill }}" ng-attr-stroke="{{ r.selected ? \'#111827\' : r.style.stroke }}" ng-attr-stroke-width="{{ r.selected ? 4 : 2 }}"></rect>' +
        '  <text ng-attr-x="{{ r.cx }}" ng-attr-y="{{ r.cy - 8 }}" ng-attr-fill="{{ r.style.title }}" font-size="13" font-weight="700" text-anchor="middle">{{ r.room.name }}</text>' +
        '  <text ng-attr-x="{{ r.cx }}" ng-attr-y="{{ r.cy + 8 }}" ng-attr-fill="{{ r.style.sub }}" font-size="11" text-anchor="middle">{{ r.sub }}</text>' +
        '  <text ng-attr-x="{{ r.cx }}" ng-attr-y="{{ r.cy + 22 }}" ng-attr-fill="{{ r.style.sub }}" font-size="10.5" font-weight="600" text-anchor="middle">{{ r.style.label }}</text>' +
        '</g>' +

        // Masalar (masa = tek sandalye)
        '<g ng-repeat="d in desks track by d.desk.id" class="desk" ng-class="{ selected: d.selected, dimmed: d.dimmed }" ng-attr-opacity="{{ d.dimmed ? 0.22 : undefined }}" role="button" tabindex="0" aria-label="{{ d.aria }}" aria-pressed="{{ d.selected }}"' +
        '   ng-attr-transform="{{ d.transform || undefined }}" ng-click="onDesk({ id: d.desk.id })" ng-keydown="key($event, \'desk\', d.desk.id)">' +
        '  <title>{{ d.desk.code }} · {{ d.style.label }}{{ d.desk.occupant ? \' · \' + d.desk.occupant.fullName : \'\' }}</title>' +
        '  <rect ng-attr-x="{{ d.desk.x - 9 }}" ng-attr-y="{{ d.y + d.h + 1 }}" width="18" height="6" rx="3" ng-attr-fill="{{ d.style.stroke }}" opacity="0.55"></rect>' +
        '  <rect ng-if="d.selected" ng-attr-x="{{ d.x - 5 }}" ng-attr-y="{{ d.y - 5 }}" ng-attr-width="{{ d.w + 10 }}" ng-attr-height="{{ d.h + 10 }}" rx="10" fill="none" stroke="#F59E0B" stroke-width="3.5"></rect>' +
        '  <rect ng-attr-x="{{ d.x }}" ng-attr-y="{{ d.y }}" ng-attr-width="{{ d.w }}" ng-attr-height="{{ d.h }}" rx="7" ng-attr-fill="{{ d.style.fill }}" ng-attr-stroke="{{ d.selected ? \'#111827\' : d.style.stroke }}" ng-attr-stroke-width="{{ d.selected ? 2.5 : 1.6 }}"></rect>' +
        '  <text ng-attr-x="{{ d.tall ? d.desk.x - 5 : d.desk.x }}" ng-attr-y="{{ d.tall ? d.desk.y : d.desk.y + 1 }}" ng-attr-fill="{{ d.style.text }}" font-size="10.5" font-weight="600" text-anchor="middle" ng-attr-dominant-baseline="{{ d.tall ? \'middle\' : undefined }}" ng-attr-transform="{{ d.labelTransform || undefined }}">{{ d.label }}</text>' +
        '  <text ng-if="d.style.status" ng-attr-x="{{ d.tall ? d.desk.x + 7 : d.desk.x }}" ng-attr-y="{{ d.tall ? d.desk.y : d.desk.y + 13 }}" font-size="9" text-anchor="middle" ng-attr-fill="{{ d.style.text }}" ng-attr-dominant-baseline="{{ d.tall ? \'middle\' : undefined }}" ng-attr-transform="{{ d.statusTransform || undefined }}">{{ d.style.status }}</text>' +
        '  <text ng-if="d.desk.accessibleOnly" ng-attr-x="{{ d.desk.x }}" ng-attr-y="{{ d.desk.y - 9 }}" font-size="10" text-anchor="middle" fill="#6d28d9">♿</text>' +
        '</g>' +
        '</svg>',
      link: function (scope) {
        scope.key = function (event, kind, id) {
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            if (kind === 'desk') scope.onDesk({ id: id }); else scope.onRoom({ id: id });
          }
        };
        scope.$watchGroup(['plan', 'selectedDeskId', 'selectedRoomId', 'highlighted', 'deskFill'], function () {
          var plan = scope.plan;
          if (!plan) { scope.elements = scope.zones = scope.desks = scope.rooms = []; return; }
          var hasSketch = plan.elements && plan.elements.some(function (e) { return e.visible; });
          scope.elements = hasSketch
            ? plan.elements.filter(function (e) { return e.visible; }).sort(function (a, b) { return a.layerOrder - b.layerOrder; }).map(renderElement)
            : [];
          scope.zones = hasSketch ? [] : plan.zones.map(function (z) { return angular.extend({ st: ZONE_STYLE[z.kind] || ZONE_STYLE.WORK }, z); });
          scope.rooms = plan.rooms.map(function (r) { return renderRoom(r, scope.selectedRoomId); });
          scope.desks = plan.desks.map(function (d) {
            var r = renderDesk(d, scope.selectedDeskId);
            if (scope.deskFill) {
              // Isi haritasi: durum renkleri yerine kullanim yogunlugu.
              var fill = scope.deskFill[d.id];
              r.style = { fill: fill ? fill.color : '#f1f5f9', stroke: '#64748b', text: fill && fill.dark ? '#ffffff' : '#0f172a', label: fill ? fill.label : 'Kullanım yok', status: null };
              r.aria = d.code + ' masası, ' + r.style.label;
            }
            r.dimmed = !!scope.highlighted && !scope.highlighted[d.id];
            return r;
          });
        });
      }
    };
  });
})();
