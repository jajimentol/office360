/*
 * Kat plani duzenleyici (eski plan-editor/*). Veri: /Admin/Api/floors/{id}/plan-snapshot, kayit: PUT /floors/{id}/plan.
 * Kroki ogeleri (duvar, kapi, asansor...) + masa ve oda yerlesimleri tek kayitta gider. Masa x/y merkez noktasidir.
 */
(function () {
  'use strict';

  var TYPES = [
    { type: 'ROOM', label: 'Oda / bölge', description: 'Çalışma alanı, ofis veya bölge', w: 260, h: 180, text: 'Yeni bölge', layer: 10 },
    { type: 'CORRIDOR', label: 'Koridor', description: 'Geçiş alanı', w: 400, h: 56, text: 'Koridor', layer: 12 },
    { type: 'WALL', label: 'Duvar', description: 'Kalın çizgi', w: 200, h: 10, text: null, layer: 30 },
    { type: 'DOOR', label: 'Kapı', description: 'Duvar üzerindeki geçiş', w: 44, h: 12, text: null, layer: 40 },
    { type: 'WINDOW', label: 'Pencere', description: 'Cephe / doğal ışık', w: 120, h: 10, text: null, layer: 40 },
    { type: 'ELEVATOR', label: 'Asansör', description: 'Asansör kovası', w: 100, h: 90, text: 'Asansör', layer: 20 },
    { type: 'STAIRS', label: 'Merdiven', description: 'Merdiven boşluğu', w: 100, h: 120, text: 'Merdiven', layer: 20 },
    { type: 'KITCHEN', label: 'Mutfak', description: 'Kafe, mutfak, dinlenme', w: 220, h: 160, text: 'Mutfak', layer: 15 },
    { type: 'WC', label: 'WC', description: 'Islak hacim', w: 110, h: 110, text: 'WC', layer: 15 },
    { type: 'RESTRICTED', label: 'Kapalı alan', description: 'Rezervasyona kapalı bölüm', w: 200, h: 150, text: 'Kapalı alan', layer: 15 },
    { type: 'TERRACE', label: 'Teras', description: 'Açık hava alanı', w: 300, h: 120, text: 'Teras', layer: 5 },
    { type: 'TEXT', label: 'Metin', description: 'Serbest açıklama', w: 160, h: 28, text: 'Etiket', layer: 50 }
  ];
  var FILLS = [
    { value: '#ffffff', label: 'Beyaz' }, { value: '#f8fafc', label: 'Açık gri' }, { value: '#e2e8f0', label: 'Gri' }, { value: '#eef4fb', label: 'Açık mavi' },
    { value: '#dbeafe', label: 'Mavi' }, { value: '#f0fdfa', label: 'Turkuaz' }, { value: '#fef9c3', label: 'Sarı' }, { value: '#fef2f2', label: 'Kırmızı' },
    { value: '#f5f3ff', label: 'Mor' }, { value: '#334155', label: 'Koyu gri' }, { value: 'transparent', label: 'Saydam' },
    { value: 'hatch-red', label: 'Kırmızı tarama (kapalı alan)' }
  ];
  var STROKES = [
    { value: '#1e293b', label: 'Koyu' }, { value: '#94a3b8', label: 'Gri' }, { value: '#cbd5e1', label: 'Açık gri' }, { value: '#2f6db5', label: 'Mavi' },
    { value: '#0d9488', label: 'Turkuaz' }, { value: '#b45309', label: 'Turuncu' }, { value: '#dc2626', label: 'Kırmızı' }, { value: 'transparent', label: 'Çizgisiz' }
  ];
  var GRID = 10, MIN = 8, HISTORY = 60;

  angular.module('ofis360').controller('PlanEditorCtrl', ['$scope', '$q', '$window', 'adminApi', 'toast', 'urlState', 'planShapes',
    function ($scope, $q, $window, adminApi, toast, urlState, planShapes) {
      var vm = this;
      vm.types = TYPES; vm.fills = FILLS; vm.strokes = STROKES; vm.grid = GRID;
      vm.tool = 'select'; vm.snapToGrid = true; vm.zoom = 1;
      vm.short = planShapes.shortDesk;
      vm.typeLabel = function (t) { var x = TYPES.filter(function (i) { return i.type === t; })[0]; return x ? x.label : t; };
      vm.notCurrent = function (f) { return f.id !== vm.floorId; };
      var past = [], future = [], saved = null, drag = null;

      // ---- Durum ve gecmis
      function state() { return { elements: vm.elements, desks: vm.desks, rooms: vm.rooms, unplacedDesks: vm.unplacedDesks, unplacedRooms: vm.unplacedRooms }; }
      function restore(json) {
        var s = JSON.parse(json);
        vm.elements = s.elements; vm.desks = s.desks; vm.rooms = s.rooms; vm.unplacedDesks = s.unplacedDesks; vm.unplacedRooms = s.unplacedRooms;
        refresh();
      }
      function checkpoint() { past.push(JSON.stringify(state())); if (past.length > HISTORY) past.shift(); future = []; }
      vm.canUndo = function () { return past.length > 0; };
      vm.canRedo = function () { return future.length > 0; };
      vm.undo = function () { if (!past.length) return; future.push(JSON.stringify(state())); restore(past.pop()); };
      vm.redo = function () { if (!future.length) return; past.push(JSON.stringify(state())); restore(future.pop()); };

      /** Ozellik paneli her alan degisiminde bir geri alma noktasi birakmasin diye ilk degisiklikte bir kez alinir. */
      var editing = null, lastStable = null;
      vm.changed = function () {
        var key = vm.sel ? vm.sel.kind + vm.sel.id : '';
        if (editing !== key) { if (lastStable) { past.push(lastStable); future = []; } editing = key; }
        normalize();
        refresh(true);
      };

      function normalize() {
        vm.elements.forEach(function (e) {
          e.width = Math.max(MIN, +e.width || MIN); e.height = Math.max(MIN, +e.height || MIN);
          e.x = Math.round(+e.x || 0); e.y = Math.round(+e.y || 0); e.rotation = Math.round(+e.rotation || 0) % 360; e.layerOrder = Math.max(0, +e.layerOrder || 0);
        });
        vm.desks.forEach(function (d) { d.x = Math.round(+d.x || 0); d.y = Math.round(+d.y || 0); d.width = Math.max(MIN, +d.width || 46); d.height = Math.max(MIN, +d.height || 34); d.rotation = Math.round(+d.rotation || 0) % 360; });
        vm.rooms.forEach(function (r) { r.x = Math.round(+r.x || 0); r.y = Math.round(+r.y || 0); r.width = Math.max(40, +r.width || 40); r.height = Math.max(40, +r.height || 40); });
        clampAll();
      }

      /** Her sey plan sinirlari icinde kalir; API sinir disindaki ogeyi reddeder. */
      function clampAll() {
        if (!vm.snap) return;
        var W = vm.snap.floor.planWidth, H = vm.snap.floor.planHeight;
        function box(o) {
          o.width = Math.min(o.width, W); o.height = Math.min(o.height, H);
          o.x = Math.min(Math.max(0, o.x), W - o.width); o.y = Math.min(Math.max(0, o.y), H - o.height);
        }
        vm.elements.forEach(box);
        vm.rooms.forEach(box);
        vm.desks.forEach(function (d) {
          d.x = Math.min(Math.max(Math.ceil(d.width / 2), d.x), W - Math.ceil(d.width / 2));
          d.y = Math.min(Math.max(Math.ceil(d.height / 2), d.y), H - Math.ceil(d.height / 2));
        });
      }

      function refresh(fromEdit) {
        var cleanStyle = function (s) { var o = {}; angular.forEach(s || {}, function (v, k) { if (v !== '' && v !== null && v !== undefined) o[k] = v; }); return o; };
        vm.rendered = vm.elements.slice().sort(function (a, b) { return a.layerOrder - b.layerOrder; }).map(function (e) {
          var r = planShapes.renderElement(angular.extend({}, e, { style: cleanStyle(e.style) }));
          r.locked = e.locked; r.hidden = !e.visible;
          return r;
        });
        vm.selEl = vm.sel && vm.sel.kind === 'el' ? find(vm.elements, vm.sel.id) : null;
        vm.selDesk = vm.sel && vm.sel.kind === 'desk' ? find(vm.desks, vm.sel.id) : null;
        vm.selRoom = vm.sel && vm.sel.kind === 'room' ? find(vm.rooms, vm.sel.id) : null;
        if (vm.selEl) vm.selBox = { x: vm.selEl.x, y: vm.selEl.y, w: vm.selEl.width, h: vm.selEl.height, resizable: !vm.selEl.locked };
        else if (vm.selRoom) vm.selBox = { x: vm.selRoom.x, y: vm.selRoom.y, w: vm.selRoom.width, h: vm.selRoom.height, resizable: true };
        else if (vm.selDesk) vm.selBox = { x: vm.selDesk.x - vm.selDesk.width / 2, y: vm.selDesk.y - vm.selDesk.height / 2, w: vm.selDesk.width, h: vm.selDesk.height, resizable: false };
        else vm.selBox = null;
        vm.dirty = saved !== null && JSON.stringify({ e: vm.elements, d: vm.desks, r: vm.rooms }) !== savedComparable;
        // Ozellik panelindeki duzenlemeden onceki son kararli durum (geri alma icin).
        if (!fromEdit && !drag) lastStable = JSON.stringify(state());
      }
      var savedComparable = null;

      function find(list, id) { return list.filter(function (x) { return x.id === id; })[0] || null; }
      function snap(v) { return vm.snapToGrid ? Math.round(v / GRID) * GRID : Math.round(v); }
      vm.isSel = function (kind, id) { return vm.sel && vm.sel.kind === kind && vm.sel.id === id; };
      vm.setTool = function (t) { vm.tool = t; };
      vm.zoomBy = function (d) { vm.zoom = Math.min(3, Math.max(0.5, vm.zoom + d)); };

      // ---- Yukleme
      function load() {
        if (!vm.floorId) return $q.resolve();
        vm.error = null;
        urlState.set({ floor: vm.floorId });
        return adminApi.get('floors/' + vm.floorId + '/plan-snapshot').then(function (s) {
          vm.snap = s;
          vm.elements = s.elements.map(function (e) {
            return { id: e.id, type: e.type, label: e.label, x: e.x, y: e.y, width: e.width, height: e.height, rotation: e.rotation || 0,
              style: { fill: e.style.fill || '', stroke: e.style.stroke || '', strokeWidth: e.style.strokeWidth, fontSize: e.style.fontSize },
              layerOrder: e.layerOrder, locked: e.locked, visible: e.visible, linkedResourceId: e.linkedResourceId, linkedResourceType: e.linkedResourceType };
          });
          vm.desks = s.desks.map(function (d) { return angular.copy(d); });
          vm.rooms = s.rooms.map(function (r) { return angular.copy(r); });
          vm.unplacedDesks = s.unplacedDesks.slice();
          vm.unplacedRooms = s.unplacedRooms.slice();
          past = []; future = []; vm.sel = null; editing = null;
          saved = JSON.stringify(state());
          savedComparable = JSON.stringify({ e: vm.elements, d: vm.desks, r: vm.rooms });
          refresh();
        }).catch(function (e) { vm.error = e.message; });
      }
      vm.changeFloor = function () {
        if (vm.dirty && !$window.confirm('Kaydedilmemiş değişiklikler kaybolacak. Kat değiştirilsin mi?')) { vm.floorId = vm.snap.floor.id; return; }
        load();
      };
      vm.discard = function () { if ($window.confirm('Kaydedilmemiş tüm değişiklikler atılsın mı?')) load(); };

      // ---- Ekleme, yerlestirme
      function center() { return { x: vm.snap.floor.planWidth / 2, y: vm.snap.floor.planHeight / 2 }; }
      function newId() { return 'yeni-' + Date.now().toString(36) + Math.random().toString(36).slice(2, 7); }
      vm.addElement = function (type) {
        var def = TYPES.filter(function (t) { return t.type === type; })[0], c = center();
        checkpoint();
        var e = { id: newId(), type: type, label: def.text, x: snap(c.x - def.w / 2), y: snap(c.y - def.h / 2), width: def.w, height: def.h, rotation: 0,
          style: { fill: '', stroke: '' }, layerOrder: def.layer, locked: false, visible: true, linkedResourceId: null, linkedResourceType: null };
        vm.elements.push(e);
        vm.sel = { kind: 'el', id: e.id }; editing = null; vm.tool = 'select';
        refresh();
      };
      vm.placeDesk = function (d) {
        var c = center();
        checkpoint();
        vm.unplacedDesks = vm.unplacedDesks.filter(function (x) { return x.id !== d.id; });
        vm.desks.push({ id: d.id, code: d.code, x: snap(c.x), y: snap(c.y), width: d.width || 46, height: d.height || 34, rotation: 0, zoneId: d.zoneId, zoneName: d.zoneName,
          active: d.active, popular: d.popular, accessibleOnly: d.accessibleOnly, reservationCount: 0 });
        vm.sel = { kind: 'desk', id: d.id }; editing = null;
        refresh();
      };
      vm.placeRoom = function (r) {
        var c = center(), w = r.width || 200, h = r.height || 120;
        checkpoint();
        vm.unplacedRooms = vm.unplacedRooms.filter(function (x) { return x.id !== r.id; });
        vm.rooms.push({ id: r.id, code: r.code, name: r.name, x: snap(c.x - w / 2), y: snap(c.y - h / 2), width: w, height: h, capacity: r.capacity, active: r.active, reservationCount: 0 });
        vm.sel = { kind: 'room', id: r.id }; editing = null;
        refresh();
      };
      vm.remove = function () {
        if (!vm.selEl) return;
        checkpoint();
        var id = vm.selEl.id;
        vm.elements = vm.elements.filter(function (e) { return e.id !== id; });
        vm.sel = null; editing = null; refresh();
      };
      vm.duplicate = function () {
        if (!vm.selEl) return;
        checkpoint();
        var copy = angular.copy(vm.selEl);
        copy.id = newId(); copy.x += 20; copy.y += 20; copy.locked = false;
        vm.elements.push(copy);
        vm.sel = { kind: 'el', id: copy.id }; editing = null; refresh();
      };
      vm.layer = function (dir) {
        if (!vm.selEl) return;
        checkpoint();
        var orders = vm.elements.map(function (e) { return e.layerOrder; });
        vm.selEl.layerOrder = dir > 0 ? Math.max.apply(null, orders) + 1 : Math.max(0, Math.min.apply(null, orders) - 1);
        refresh();
      };

      // ---- Fare ile tasima, boyutlandirma ve duvar cizimi
      function toSvg(evt) {
        var svg = document.querySelector('.editor-svg');
        var pt = svg.createSVGPoint(); pt.x = evt.clientX; pt.y = evt.clientY;
        return pt.matrixTransform(svg.getScreenCTM().inverse());
      }
      vm.itemDown = function (evt, kind, id) {
        if (vm.tool === 'wall' || evt.button !== 0) return;
        evt.stopPropagation(); evt.preventDefault();
        vm.sel = { kind: kind, id: id }; editing = null;
        refresh();
        var item = kind === 'el' ? vm.selEl : kind === 'desk' ? vm.selDesk : vm.selRoom;
        if (!item || item.locked) return;
        drag = { mode: 'move', item: item, start: toSvg(evt), ox: item.x, oy: item.y, before: JSON.stringify(state()), moved: false };
      };
      vm.handleDown = function (evt) {
        evt.stopPropagation(); evt.preventDefault();
        var item = vm.selEl || vm.selRoom;
        if (!item || item.locked) return;
        drag = { mode: 'resize', item: item, start: toSvg(evt), ow: item.width, oh: item.height, before: JSON.stringify(state()), moved: false };
      };
      vm.canvasDown = function (evt) {
        if (evt.button !== 0) return;
        if (vm.tool === 'wall') {
          evt.preventDefault();
          var p = toSvg(evt);
          drag = { mode: 'wall', start: { x: snap(p.x), y: snap(p.y) }, before: JSON.stringify(state()) };
          return;
        }
        vm.sel = null; editing = null; refresh();
      };
      function wallRect(a, b) {
        // En yakin eksene kilitli kalin cizgi (eski wallRectFromPoints).
        var t = 10;
        if (Math.abs(b.x - a.x) >= Math.abs(b.y - a.y)) return { x: Math.min(a.x, b.x), y: a.y - t / 2, width: Math.max(Math.abs(b.x - a.x), MIN), height: t };
        return { x: a.x - t / 2, y: Math.min(a.y, b.y), width: t, height: Math.max(Math.abs(b.y - a.y), MIN) };
      }
      function onMove(evt) {
        if (!drag) return;
        var p = toSvg(evt);
        $scope.$apply(function () {
          if (drag.mode === 'wall') { vm.wallDraft = wallRect(drag.start, { x: snap(p.x), y: snap(p.y) }); return; }
          var dx = p.x - drag.start.x, dy = p.y - drag.start.y;
          if (!drag.moved && Math.abs(dx) < 2 && Math.abs(dy) < 2) return;
          drag.moved = true;
          if (drag.mode === 'move') { drag.item.x = snap(drag.ox + dx); drag.item.y = snap(drag.oy + dy); }
          else { drag.item.width = Math.max(MIN, snap(drag.ow + dx)); drag.item.height = Math.max(MIN, snap(drag.oh + dy)); }
          clampAll();
          refresh();
        });
      }
      function onUp() {
        if (!drag) return;
        var d = drag; drag = null;
        $scope.$apply(function () {
          if (d.mode === 'wall') {
            var r = vm.wallDraft; vm.wallDraft = null;
            if (r && (r.width > 20 || r.height > 20)) {
              past.push(d.before); future = [];
              var w = { id: newId(), type: 'WALL', label: null, x: Math.round(r.x), y: Math.round(r.y), width: Math.round(r.width), height: Math.round(r.height), rotation: 0,
                style: { fill: '', stroke: '' }, layerOrder: 30, locked: false, visible: true, linkedResourceId: null, linkedResourceType: null };
              vm.elements.push(w);
              clampAll();
              vm.sel = { kind: 'el', id: w.id }; editing = null;
            }
            refresh();
            return;
          }
          if (d.moved) { past.push(d.before); future = []; editing = null; refresh(); }
        });
      }
      $window.addEventListener('mousemove', onMove);
      $window.addEventListener('mouseup', onUp);

      // ---- Klavye
      function onKey(evt) {
        var tag = (evt.target.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'select' || tag === 'textarea') return;
        var ctrl = evt.ctrlKey || evt.metaKey, handled = true;
        $scope.$apply(function () {
          if (ctrl && evt.key.toLowerCase() === 'z') vm.undo();
          else if (ctrl && evt.key.toLowerCase() === 'y') vm.redo();
          else if (ctrl && evt.key.toLowerCase() === 'd') vm.duplicate();
          else if ((evt.key === 'Delete' || evt.key === 'Backspace') && vm.selEl) vm.remove();
          else if (evt.key === 'Escape') { vm.sel = null; vm.tool = 'select'; refresh(); }
          else if (evt.key.indexOf('Arrow') === 0 && (vm.selEl || vm.selDesk || vm.selRoom)) {
            var item = vm.selEl || vm.selDesk || vm.selRoom;
            if (item.locked) return;
            var step = evt.shiftKey ? 10 : 1;
            checkpoint();
            if (evt.key === 'ArrowLeft') item.x -= step; else if (evt.key === 'ArrowRight') item.x += step;
            else if (evt.key === 'ArrowUp') item.y -= step; else item.y += step;
            refresh();
          } else handled = false;
        });
        if (handled) evt.preventDefault();
      }
      $window.addEventListener('keydown', onKey);
      function onBeforeUnload(evt) { if (vm.dirty) { evt.preventDefault(); evt.returnValue = ''; } }
      $window.addEventListener('beforeunload', onBeforeUnload);
      $scope.$on('$destroy', function () {
        $window.removeEventListener('mousemove', onMove); $window.removeEventListener('mouseup', onUp);
        $window.removeEventListener('keydown', onKey); $window.removeEventListener('beforeunload', onBeforeUnload);
      });

      // ---- Kayit ve sunucu islemleri
      vm.save = function () {
        normalize();
        var styleOf = function (s) { var o = {}; angular.forEach(s || {}, function (v, k) { if (v !== '' && v !== null && v !== undefined) o[k] = v; }); return o; };
        var body = {
          floorId: vm.floorId,
          elements: vm.elements.map(function (e) {
            return { id: e.id, type: e.type, label: e.label || null, x: e.x, y: e.y, width: e.width, height: e.height, rotation: e.rotation, style: styleOf(e.style),
              layerOrder: e.layerOrder, locked: !!e.locked, visible: e.visible !== false, linkedResourceId: e.linkedResourceId || null, linkedResourceType: e.linkedResourceType || null };
          }),
          deskPlacements: vm.desks.map(function (d) { return { id: d.id, x: d.x, y: d.y, width: d.width, height: d.height, rotation: d.rotation || 0 }; }),
          roomPlacements: vm.rooms.map(function (r) { return { id: r.id, x: r.x, y: r.y, width: r.width, height: r.height }; })
        };
        vm.busy = true;
        adminApi.put('floors/' + vm.floorId + '/plan', body).then(function (r) {
          toast.ok('Plan kaydedildi: ' + r.createdCount + ' yeni, ' + r.updatedCount + ' güncellenen, ' + r.deletedCount + ' silinen öğe; ' + r.movedDeskCount + ' masa, ' + r.movedRoomCount + ' oda yerleşimi.');
          return load();
        }).catch(function (e) { toast.error(e.message); vm.error = e.message; })
          .finally(function () { vm.busy = false; });
      };

      vm.autoPlace = function () {
        vm.busy = true;
        adminApi.post('floors/' + vm.floorId + '/plan/auto-place', {}).then(function (r) {
          toast.ok(r.placedDesks + ' masa ve ' + r.placedRooms + ' oda yerleştirildi' + (r.remainingDesks + r.remainingRooms ? ', ' + (r.remainingDesks + r.remainingRooms) + ' öğeye yer bulunamadı.' : '.'));
          return load();
        }).catch(function (e) { toast.error(e.message); }).finally(function () { vm.busy = false; });
      };

      vm.copyPlan = function () {
        if (!$window.confirm('Bu katın krokisi seçilen kattaki kroki ile değiştirilecek (masa ve odalar etkilenmez). Kaydetmeden önce geri alabilirsiniz. Devam edilsin mi?')) return;
        vm.busy = true;
        adminApi.get('floors/' + vm.floorId + '/plan/copy-from/' + vm.copyFrom).then(function (r) {
          checkpoint();
          vm.elements = r.elements.map(function (e) {
            return { id: newId(), type: e.type, label: e.label, x: e.x, y: e.y, width: e.width, height: e.height, rotation: e.rotation || 0,
              style: { fill: e.style.fill || '', stroke: e.style.stroke || '', strokeWidth: e.style.strokeWidth, fontSize: e.style.fontSize },
              layerOrder: e.layerOrder, locked: e.locked, visible: e.visible, linkedResourceId: null, linkedResourceType: null };
          });
          vm.sel = null; refresh();
          toast.ok(vm.elements.length + ' öğe kopyalandı. Kaydetmeyi unutmayın.');
        }).catch(function (e) { toast.error(e.message); }).finally(function () { vm.busy = false; });
      };

      vm.createQuick = function () {
        var q = vm.quick, c = center();
        vm.busy = true; vm.quickError = null;
        var call = q.kind === 'desk'
          ? adminApi.post('floors/' + vm.floorId + '/plan/desks', { floorId: vm.floorId, zoneId: q.zoneId, code: q.code, x: Math.round(c.x), y: Math.round(c.y) })
          : adminApi.post('floors/' + vm.floorId + '/plan/rooms', { floorId: vm.floorId, code: q.code, name: q.name, capacity: +q.capacity || 1, x: Math.round(c.x - 100), y: Math.round(c.y - 60), width: 200, height: 120 });
        call.then(function () { toast.ok((q.kind === 'desk' ? q.code + ' masası' : q.name) + ' oluşturuldu.'); vm.quick = null; return load(); })
          .catch(function (e) { vm.quickError = e.message; })
          .finally(function () { vm.busy = false; });
      };

      adminApi.get('floors').then(function (floors) {
        vm.floors = floors.map(function (f) { return { id: f.id, label: (f.building ? f.building.name + ' · ' : '') + f.name }; });
        var wanted = urlState.get('floor');
        vm.floorId = vm.floors.some(function (f) { return f.id === wanted; }) ? wanted : (vm.floors[0] && vm.floors[0].id);
        return load();
      }).catch(function (e) { vm.error = e.message; });
    }]);
})();
