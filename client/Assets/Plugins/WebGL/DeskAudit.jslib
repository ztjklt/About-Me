mergeInto(LibraryManager.library, {
  DeskAuditStartDrawCounter: function () {
    if (window.__deskDrawCounter) return;
    var state = window.__deskDrawCounter = { count: 0, lastRead: performance.now() };
    [window.WebGLRenderingContext, window.WebGL2RenderingContext].forEach(function (type) {
      if (!type) return;
      ['drawArrays', 'drawElements', 'drawArraysInstanced', 'drawElementsInstanced'].forEach(function (name) {
        var original = type.prototype[name];
        if (!original || original.__deskWrapped) return;
        var wrapped = function () { state.count++; return original.apply(this, arguments); };
        wrapped.__deskWrapped = true; type.prototype[name] = wrapped;
      });
    });
  },
  DeskAuditDrawCount: function () {
    var state = window.__deskDrawCounter;
    if (!state) return -1;
    var now = performance.now(), elapsed = now - state.lastRead, count = state.count;
    state.count = 0; state.lastRead = now;
    // Reset once after each settling period; sample engine frames, not monitor refreshes.
    return elapsed > 250 ? -1 : count;
  },
  DeskAuditReport: function (json) {
    window.__deskAuditReport = JSON.parse(UTF8ToString(json));
    console.info('DESK_AUDIT_COMPLETE', window.__deskAuditReport);
  }
});
