/**
* @vue/shared v3.5.41
* (c) 2018-present Yuxi (Evan) You and Vue contributors
* @license MIT
**/
// @__NO_SIDE_EFFECTS__
function makeMap(str) {
  const map = /* @__PURE__ */ Object.create(null);
  for (const key of str.split(",")) map[key] = 1;
  return (val) => val in map;
}
const EMPTY_OBJ = {};
const EMPTY_ARR = [];
const NOOP = () => {
};
const NO = () => false;
const isOn = (key) => key.charCodeAt(0) === 111 && key.charCodeAt(1) === 110 && // uppercase letter
(key.charCodeAt(2) > 122 || key.charCodeAt(2) < 97);
const isModelListener = (key) => key.startsWith("onUpdate:");
const extend = Object.assign;
const remove = (arr, el) => {
  const i = arr.indexOf(el);
  if (i > -1) {
    arr.splice(i, 1);
  }
};
const hasOwnProperty$1 = Object.prototype.hasOwnProperty;
const hasOwn = (val, key) => hasOwnProperty$1.call(val, key);
const isArray$1 = Array.isArray;
const isMap = (val) => toTypeString(val) === "[object Map]";
const isSet = (val) => toTypeString(val) === "[object Set]";
const isDate = (val) => toTypeString(val) === "[object Date]";
const isFunction = (val) => typeof val === "function";
const isString = (val) => typeof val === "string";
const isSymbol = (val) => typeof val === "symbol";
const isObject = (val) => val !== null && typeof val === "object";
const isPromise = (val) => {
  return (isObject(val) || isFunction(val)) && isFunction(val.then) && isFunction(val.catch);
};
const objectToString = Object.prototype.toString;
const toTypeString = (value) => objectToString.call(value);
const toRawType = (value) => {
  return toTypeString(value).slice(8, -1);
};
const isPlainObject = (val) => toTypeString(val) === "[object Object]";
const isIntegerKey = (key) => isString(key) && key !== "NaN" && key[0] !== "-" && "" + parseInt(key, 10) === key;
const isReservedProp = /* @__PURE__ */ makeMap(
  // the leading comma is intentional so empty string "" is also included
  ",key,ref,ref_for,ref_key,onVnodeBeforeMount,onVnodeMounted,onVnodeBeforeUpdate,onVnodeUpdated,onVnodeBeforeUnmount,onVnodeUnmounted"
);
const cacheStringFunction = (fn) => {
  const cache = /* @__PURE__ */ Object.create(null);
  return (str) => {
    const hit = cache[str];
    return hit || (cache[str] = fn(str));
  };
};
const camelizeRE = /-\w/g;
const camelize = cacheStringFunction(
  (str) => {
    return str.replace(camelizeRE, (c) => c.slice(1).toUpperCase());
  }
);
const hyphenateRE = /\B([A-Z])/g;
const hyphenate = cacheStringFunction(
  (str) => str.replace(hyphenateRE, "-$1").toLowerCase()
);
const capitalize = cacheStringFunction((str) => {
  return str.charAt(0).toUpperCase() + str.slice(1);
});
const toHandlerKey = cacheStringFunction(
  (str) => {
    const s = str ? `on${capitalize(str)}` : ``;
    return s;
  }
);
const hasChanged = (value, oldValue) => !Object.is(value, oldValue);
const invokeArrayFns = (fns, ...arg) => {
  for (let i = 0; i < fns.length; i++) {
    fns[i](...arg);
  }
};
const def = (obj, key, value, writable = false) => {
  Object.defineProperty(obj, key, {
    configurable: true,
    enumerable: false,
    writable,
    value
  });
};
const looseToNumber = (val) => {
  const n = parseFloat(val);
  return isNaN(n) ? val : n;
};
let _globalThis;
const getGlobalThis = () => {
  return _globalThis || (_globalThis = typeof globalThis !== "undefined" ? globalThis : typeof self !== "undefined" ? self : typeof window !== "undefined" ? window : typeof global !== "undefined" ? global : {});
};
function normalizeStyle(value) {
  if (isArray$1(value)) {
    const res = {};
    for (let i = 0; i < value.length; i++) {
      const item = value[i];
      const normalized = isString(item) ? parseStringStyle(item) : normalizeStyle(item);
      if (normalized) {
        for (const key in normalized) {
          res[key] = normalized[key];
        }
      }
    }
    return res;
  } else if (isString(value) || isObject(value)) {
    return value;
  }
}
const listDelimiterRE = /;(?![^(]*\))/g;
const propertyDelimiterRE = /:([^]+)/;
const styleCommentRE = /\/\*[^]*?\*\//g;
function parseStringStyle(cssText) {
  const ret = {};
  cssText.replace(styleCommentRE, "").split(listDelimiterRE).forEach((item) => {
    if (item) {
      const tmp = item.split(propertyDelimiterRE);
      tmp.length > 1 && (ret[tmp[0].trim()] = tmp[1].trim());
    }
  });
  return ret;
}
function normalizeClass(value) {
  let res = "";
  if (isString(value)) {
    res = value;
  } else if (isArray$1(value)) {
    for (let i = 0; i < value.length; i++) {
      const normalized = normalizeClass(value[i]);
      if (normalized) {
        res += normalized + " ";
      }
    }
  } else if (isObject(value)) {
    for (const name in value) {
      if (value[name]) {
        res += name + " ";
      }
    }
  }
  return res.trim();
}
const specialBooleanAttrs = `itemscope,allowfullscreen,formnovalidate,ismap,nomodule,novalidate,readonly`;
const isSpecialBooleanAttr = /* @__PURE__ */ makeMap(specialBooleanAttrs);
function includeBooleanAttr(value) {
  return !!value || value === "";
}
function looseCompareArrays(a, b) {
  if (a.length !== b.length) return false;
  let equal = true;
  for (let i = 0; equal && i < a.length; i++) {
    equal = looseEqual(a[i], b[i]);
  }
  return equal;
}
function looseEqual(a, b) {
  if (a === b) return true;
  let aValidType = isDate(a);
  let bValidType = isDate(b);
  if (aValidType || bValidType) {
    return aValidType && bValidType ? a.getTime() === b.getTime() : false;
  }
  aValidType = isSymbol(a);
  bValidType = isSymbol(b);
  if (aValidType || bValidType) {
    return a === b;
  }
  aValidType = isArray$1(a);
  bValidType = isArray$1(b);
  if (aValidType || bValidType) {
    return aValidType && bValidType ? looseCompareArrays(a, b) : false;
  }
  aValidType = isObject(a);
  bValidType = isObject(b);
  if (aValidType || bValidType) {
    if (!aValidType || !bValidType) {
      return false;
    }
    const aKeysCount = Object.keys(a).length;
    const bKeysCount = Object.keys(b).length;
    if (aKeysCount !== bKeysCount) {
      return false;
    }
    for (const key in a) {
      const aHasKey = a.hasOwnProperty(key);
      const bHasKey = b.hasOwnProperty(key);
      if (aHasKey && !bHasKey || !aHasKey && bHasKey || !looseEqual(a[key], b[key])) {
        return false;
      }
    }
  }
  return String(a) === String(b);
}
function looseIndexOf(arr, val) {
  return arr.findIndex((item) => looseEqual(item, val));
}
const isRef$1 = (val) => {
  return !!(val && val["__v_isRef"] === true);
};
const toDisplayString = (val) => {
  return isString(val) ? val : val == null ? "" : isArray$1(val) || isObject(val) && (val.toString === objectToString || !isFunction(val.toString)) ? isRef$1(val) ? toDisplayString(val.value) : JSON.stringify(val, replacer, 2) : String(val);
};
const replacer = (_key, val) => {
  if (isRef$1(val)) {
    return replacer(_key, val.value);
  } else if (isMap(val)) {
    return {
      [`Map(${val.size})`]: [...val.entries()].reduce(
        (entries, [key, val2], i) => {
          entries[stringifySymbol(key, i) + " =>"] = val2;
          return entries;
        },
        {}
      )
    };
  } else if (isSet(val)) {
    return {
      [`Set(${val.size})`]: [...val.values()].map((v) => stringifySymbol(v))
    };
  } else if (isSymbol(val)) {
    return stringifySymbol(val);
  } else if (isObject(val) && !isArray$1(val) && !isPlainObject(val)) {
    return String(val);
  }
  return val;
};
const stringifySymbol = (v, i = "") => {
  var _a;
  return (
    // Symbol.description in es2019+ so we need to cast here to pass
    // the lib: es2016 check
    isSymbol(v) ? `Symbol(${(_a = v.description) != null ? _a : i})` : v
  );
};
/**
* @vue/reactivity v3.5.41
* (c) 2018-present Yuxi (Evan) You and Vue contributors
* @license MIT
**/
let activeEffectScope;
class EffectScope {
  // TODO isolatedDeclarations "__v_skip"
  constructor(detached = false) {
    this.detached = detached;
    this._active = true;
    this._on = 0;
    this.effects = [];
    this.cleanups = [];
    this._isPaused = false;
    this._warnOnRun = true;
    this.__v_skip = true;
    if (!detached && activeEffectScope) {
      if (activeEffectScope.active) {
        this.parent = activeEffectScope;
        this.index = (activeEffectScope.scopes || (activeEffectScope.scopes = [])).push(
          this
        ) - 1;
      } else {
        this._active = false;
        this._warnOnRun = false;
      }
    }
  }
  get active() {
    return this._active;
  }
  pause() {
    if (this._active) {
      this._isPaused = true;
      let i, l;
      if (this.scopes) {
        const scopes = this.scopes.slice();
        for (i = 0, l = scopes.length; i < l; i++) {
          scopes[i].pause();
        }
      }
      for (i = 0, l = this.effects.length; i < l; i++) {
        this.effects[i].pause();
      }
    }
  }
  /**
   * Resumes the effect scope, including all child scopes and effects.
   */
  resume() {
    if (this._active) {
      if (this._isPaused) {
        this._isPaused = false;
        let i, l;
        if (this.scopes) {
          const scopes = this.scopes.slice();
          for (i = 0, l = scopes.length; i < l; i++) {
            scopes[i].resume();
          }
        }
        const effects = this.effects.slice();
        for (i = 0, l = effects.length; i < l; i++) {
          effects[i].resume();
        }
      }
    }
  }
  run(fn) {
    if (this._active) {
      const currentEffectScope = activeEffectScope;
      try {
        activeEffectScope = this;
        return fn();
      } finally {
        activeEffectScope = currentEffectScope;
      }
    }
  }
  /**
   * This should only be called on non-detached scopes
   * @internal
   */
  on() {
    if (++this._on === 1) {
      this.prevScope = activeEffectScope;
      activeEffectScope = this;
    }
  }
  /**
   * This should only be called on non-detached scopes
   * @internal
   */
  off() {
    if (this._on > 0 && --this._on === 0) {
      if (activeEffectScope === this) {
        activeEffectScope = this.prevScope;
      } else {
        let current = activeEffectScope;
        while (current) {
          if (current.prevScope === this) {
            current.prevScope = this.prevScope;
            break;
          }
          current = current.prevScope;
        }
      }
      this.prevScope = void 0;
    }
  }
  stop(fromParent) {
    if (this._active) {
      this._active = false;
      let i, l;
      for (i = 0, l = this.effects.length; i < l; i++) {
        this.effects[i].stop();
      }
      this.effects.length = 0;
      for (i = 0, l = this.cleanups.length; i < l; i++) {
        this.cleanups[i]();
      }
      this.cleanups.length = 0;
      if (this.scopes) {
        const scopes = this.scopes.slice();
        for (i = 0, l = scopes.length; i < l; i++) {
          scopes[i].stop(true);
        }
        this.scopes.length = 0;
      }
      if (!this.detached && this.parent && !fromParent) {
        const last = this.parent.scopes.pop();
        if (last && last !== this) {
          this.parent.scopes[this.index] = last;
          last.index = this.index;
        }
      }
      this.parent = void 0;
    }
  }
}
function getCurrentScope() {
  return activeEffectScope;
}
let activeSub;
const pausedQueueEffects = /* @__PURE__ */ new WeakSet();
class ReactiveEffect {
  constructor(fn) {
    this.fn = fn;
    this.deps = void 0;
    this.depsTail = void 0;
    this.flags = 1 | 4;
    this.next = void 0;
    this.cleanup = void 0;
    this.scheduler = void 0;
    if (activeEffectScope) {
      if (activeEffectScope.active) {
        activeEffectScope.effects.push(this);
      } else {
        this.flags &= -2;
      }
    }
  }
  pause() {
    this.flags |= 64;
  }
  resume() {
    if (this.flags & 64) {
      this.flags &= -65;
      if (pausedQueueEffects.has(this)) {
        pausedQueueEffects.delete(this);
        this.trigger();
      }
    }
  }
  /**
   * @internal
   */
  notify() {
    if (this.flags & 2 && !(this.flags & 32)) {
      return;
    }
    if (!(this.flags & 8)) {
      batch(this);
    }
  }
  run() {
    if (!(this.flags & 1)) {
      return this.fn();
    }
    this.flags |= 2;
    cleanupEffect(this);
    prepareDeps(this);
    const prevEffect = activeSub;
    const prevShouldTrack = shouldTrack;
    activeSub = this;
    shouldTrack = true;
    try {
      return this.fn();
    } finally {
      cleanupDeps(this);
      activeSub = prevEffect;
      shouldTrack = prevShouldTrack;
      this.flags &= -3;
    }
  }
  stop() {
    if (this.flags & 1) {
      for (let link = this.deps; link; link = link.nextDep) {
        removeSub(link);
      }
      this.deps = this.depsTail = void 0;
      cleanupEffect(this);
      this.onStop && this.onStop();
      this.flags &= -2;
    }
  }
  trigger() {
    if (this.flags & 64) {
      pausedQueueEffects.add(this);
    } else if (this.scheduler) {
      this.scheduler();
    } else {
      this.runIfDirty();
    }
  }
  /**
   * @internal
   */
  runIfDirty() {
    if (isDirty(this)) {
      this.run();
    }
  }
  get dirty() {
    return isDirty(this);
  }
}
let batchDepth = 0;
let batchedSub;
let batchedComputed;
function batch(sub, isComputed = false) {
  sub.flags |= 8;
  if (isComputed) {
    sub.next = batchedComputed;
    batchedComputed = sub;
    return;
  }
  sub.next = batchedSub;
  batchedSub = sub;
}
function startBatch() {
  batchDepth++;
}
function endBatch() {
  if (--batchDepth > 0) {
    return;
  }
  if (batchedComputed) {
    let e = batchedComputed;
    batchedComputed = void 0;
    while (e) {
      const next = e.next;
      e.next = void 0;
      e.flags &= -9;
      e = next;
    }
  }
  let error;
  while (batchedSub) {
    let e = batchedSub;
    batchedSub = void 0;
    while (e) {
      const next = e.next;
      e.next = void 0;
      e.flags &= -9;
      if (e.flags & 1) {
        try {
          ;
          e.trigger();
        } catch (err) {
          if (!error) error = err;
        }
      }
      e = next;
    }
  }
  if (error) throw error;
}
function prepareDeps(sub) {
  for (let link = sub.deps; link; link = link.nextDep) {
    link.version = -1;
    link.prevActiveLink = link.dep.activeLink;
    link.dep.activeLink = link;
  }
}
function cleanupDeps(sub) {
  let head;
  let tail = sub.depsTail;
  let link = tail;
  while (link) {
    const prev = link.prevDep;
    if (link.version === -1) {
      if (link === tail) tail = prev;
      removeSub(link);
      removeDep(link);
    } else {
      head = link;
    }
    link.dep.activeLink = link.prevActiveLink;
    link.prevActiveLink = void 0;
    link = prev;
  }
  sub.deps = head;
  sub.depsTail = tail;
}
function isDirty(sub) {
  for (let link = sub.deps; link; link = link.nextDep) {
    if (link.dep.version !== link.version || link.dep.computed && (refreshComputed(link.dep.computed) || link.dep.version !== link.version)) {
      return true;
    }
  }
  if (sub._dirty) {
    return true;
  }
  return false;
}
function refreshComputed(computed2) {
  if (computed2.flags & 4 && !(computed2.flags & 16)) {
    return;
  }
  computed2.flags &= -17;
  if (computed2.globalVersion === globalVersion) {
    return;
  }
  computed2.globalVersion = globalVersion;
  if (!computed2.isSSR && computed2.flags & 128 && (!computed2.deps && !computed2._dirty || !isDirty(computed2))) {
    return;
  }
  computed2.flags |= 2;
  const dep = computed2.dep;
  const prevSub = activeSub;
  const prevShouldTrack = shouldTrack;
  activeSub = computed2;
  shouldTrack = true;
  try {
    prepareDeps(computed2);
    const value = computed2.fn(computed2._value);
    if (dep.version === 0 || hasChanged(value, computed2._value)) {
      computed2.flags |= 128;
      computed2._value = value;
      dep.version++;
    }
  } catch (err) {
    dep.version++;
    throw err;
  } finally {
    activeSub = prevSub;
    shouldTrack = prevShouldTrack;
    cleanupDeps(computed2);
    computed2.flags &= -3;
  }
}
function removeSub(link, soft = false) {
  const { dep, prevSub, nextSub } = link;
  if (prevSub) {
    prevSub.nextSub = nextSub;
    link.prevSub = void 0;
  }
  if (nextSub) {
    nextSub.prevSub = prevSub;
    link.nextSub = void 0;
  }
  if (dep.subs === link) {
    dep.subs = prevSub;
    if (!prevSub && dep.computed) {
      dep.computed.flags &= -5;
      for (let l = dep.computed.deps; l; l = l.nextDep) {
        removeSub(l, true);
      }
    }
  }
  if (!soft && !--dep.sc && dep.map) {
    dep.map.delete(dep.key);
  }
}
function removeDep(link) {
  const { prevDep, nextDep } = link;
  if (prevDep) {
    prevDep.nextDep = nextDep;
    link.prevDep = void 0;
  }
  if (nextDep) {
    nextDep.prevDep = prevDep;
    link.nextDep = void 0;
  }
}
let shouldTrack = true;
const trackStack = [];
function pauseTracking() {
  trackStack.push(shouldTrack);
  shouldTrack = false;
}
function resetTracking() {
  const last = trackStack.pop();
  shouldTrack = last === void 0 ? true : last;
}
function cleanupEffect(e) {
  const { cleanup } = e;
  e.cleanup = void 0;
  if (cleanup) {
    const prevSub = activeSub;
    activeSub = void 0;
    try {
      cleanup();
    } finally {
      activeSub = prevSub;
    }
  }
}
let globalVersion = 0;
class Link {
  constructor(sub, dep) {
    this.sub = sub;
    this.dep = dep;
    this.version = dep.version;
    this.nextDep = this.prevDep = this.nextSub = this.prevSub = this.prevActiveLink = void 0;
  }
}
class Dep {
  // TODO isolatedDeclarations "__v_skip"
  constructor(computed2) {
    this.computed = computed2;
    this.version = 0;
    this.activeLink = void 0;
    this.subs = void 0;
    this.map = void 0;
    this.key = void 0;
    this.sc = 0;
    this.__v_skip = true;
  }
  track(debugInfo) {
    if (!activeSub || !shouldTrack || activeSub === this.computed) {
      return;
    }
    let link = this.activeLink;
    if (link === void 0 || link.sub !== activeSub) {
      link = this.activeLink = new Link(activeSub, this);
      if (!activeSub.deps) {
        activeSub.deps = activeSub.depsTail = link;
      } else {
        link.prevDep = activeSub.depsTail;
        activeSub.depsTail.nextDep = link;
        activeSub.depsTail = link;
      }
      addSub(link);
    } else if (link.version === -1) {
      link.version = this.version;
      if (link.nextDep) {
        const next = link.nextDep;
        next.prevDep = link.prevDep;
        if (link.prevDep) {
          link.prevDep.nextDep = next;
        }
        link.prevDep = activeSub.depsTail;
        link.nextDep = void 0;
        activeSub.depsTail.nextDep = link;
        activeSub.depsTail = link;
        if (activeSub.deps === link) {
          activeSub.deps = next;
        }
      }
    }
    return link;
  }
  trigger(debugInfo) {
    this.version++;
    globalVersion++;
    this.notify(debugInfo);
  }
  notify(debugInfo) {
    startBatch();
    try {
      if (false) ;
      for (let link = this.subs; link; link = link.prevSub) {
        if (link.sub.notify()) {
          ;
          link.sub.dep.notify();
        }
      }
    } finally {
      endBatch();
    }
  }
}
function addSub(link) {
  link.dep.sc++;
  if (link.sub.flags & 4) {
    const computed2 = link.dep.computed;
    if (computed2 && !link.dep.subs) {
      computed2.flags |= 4 | 16;
      for (let l = computed2.deps; l; l = l.nextDep) {
        addSub(l);
      }
    }
    const currentTail = link.dep.subs;
    if (currentTail !== link) {
      link.prevSub = currentTail;
      if (currentTail) currentTail.nextSub = link;
    }
    link.dep.subs = link;
  }
}
const targetMap = /* @__PURE__ */ new WeakMap();
const ITERATE_KEY = /* @__PURE__ */ Symbol(
  ""
);
const MAP_KEY_ITERATE_KEY = /* @__PURE__ */ Symbol(
  ""
);
const ARRAY_ITERATE_KEY = /* @__PURE__ */ Symbol(
  ""
);
function track(target, type, key) {
  if (shouldTrack && activeSub) {
    let depsMap = targetMap.get(target);
    if (!depsMap) {
      targetMap.set(target, depsMap = /* @__PURE__ */ new Map());
    }
    let dep = depsMap.get(key);
    if (!dep) {
      depsMap.set(key, dep = new Dep());
      dep.map = depsMap;
      dep.key = key;
    }
    {
      dep.track();
    }
  }
}
function trigger(target, type, key, newValue, oldValue, oldTarget) {
  const depsMap = targetMap.get(target);
  if (!depsMap) {
    globalVersion++;
    return;
  }
  const run = (dep) => {
    if (dep) {
      {
        dep.trigger();
      }
    }
  };
  startBatch();
  if (type === "clear") {
    depsMap.forEach(run);
  } else {
    const targetIsArray = isArray$1(target);
    const isArrayIndex = targetIsArray && isIntegerKey(key);
    if (targetIsArray && key === "length") {
      const newLength = Number(newValue);
      depsMap.forEach((dep, key2) => {
        if (key2 === "length" || key2 === ARRAY_ITERATE_KEY || !isSymbol(key2) && key2 >= newLength) {
          run(dep);
        }
      });
    } else {
      if (key !== void 0 || depsMap.has(void 0)) {
        run(depsMap.get(key));
      }
      if (isArrayIndex) {
        run(depsMap.get(ARRAY_ITERATE_KEY));
      }
      switch (type) {
        case "add":
          if (!targetIsArray) {
            run(depsMap.get(ITERATE_KEY));
            if (isMap(target)) {
              run(depsMap.get(MAP_KEY_ITERATE_KEY));
            }
          } else if (isArrayIndex) {
            run(depsMap.get("length"));
          }
          break;
        case "delete":
          if (!targetIsArray) {
            run(depsMap.get(ITERATE_KEY));
            if (isMap(target)) {
              run(depsMap.get(MAP_KEY_ITERATE_KEY));
            }
          }
          break;
        case "set":
          if (isMap(target)) {
            run(depsMap.get(ITERATE_KEY));
          }
          break;
      }
    }
  }
  endBatch();
}
function reactiveReadArray(array) {
  const raw = /* @__PURE__ */ toRaw(array);
  if (raw === array) return raw;
  track(raw, "iterate", ARRAY_ITERATE_KEY);
  return /* @__PURE__ */ isShallow(array) ? raw : raw.map(toReactive);
}
function shallowReadArray(arr) {
  track(arr = /* @__PURE__ */ toRaw(arr), "iterate", ARRAY_ITERATE_KEY);
  return arr;
}
function toWrapped(target, item) {
  if (/* @__PURE__ */ isReadonly(target)) {
    return /* @__PURE__ */ isReactive(target) ? toReadonly(toReactive(item)) : toReadonly(item);
  }
  return toReactive(item);
}
const arrayInstrumentations = {
  __proto__: null,
  [Symbol.iterator]() {
    return iterator(this, Symbol.iterator, (item) => toWrapped(this, item));
  },
  concat(...args) {
    return reactiveReadArray(this).concat(
      ...args.map((x) => isArray$1(x) ? reactiveReadArray(x) : x)
    );
  },
  entries() {
    return iterator(this, "entries", (value) => {
      value[1] = toWrapped(this, value[1]);
      return value;
    });
  },
  every(fn, thisArg) {
    return apply(this, "every", fn, thisArg, void 0, arguments);
  },
  filter(fn, thisArg) {
    return apply(
      this,
      "filter",
      fn,
      thisArg,
      (v) => v.map((item) => toWrapped(this, item)),
      arguments
    );
  },
  find(fn, thisArg) {
    return apply(
      this,
      "find",
      fn,
      thisArg,
      (item) => toWrapped(this, item),
      arguments
    );
  },
  findIndex(fn, thisArg) {
    return apply(this, "findIndex", fn, thisArg, void 0, arguments);
  },
  findLast(fn, thisArg) {
    return apply(
      this,
      "findLast",
      fn,
      thisArg,
      (item) => toWrapped(this, item),
      arguments
    );
  },
  findLastIndex(fn, thisArg) {
    return apply(this, "findLastIndex", fn, thisArg, void 0, arguments);
  },
  // flat, flatMap could benefit from ARRAY_ITERATE but are not straight-forward to implement
  forEach(fn, thisArg) {
    return apply(this, "forEach", fn, thisArg, void 0, arguments);
  },
  includes(...args) {
    return searchProxy(this, "includes", args);
  },
  indexOf(...args) {
    return searchProxy(this, "indexOf", args);
  },
  join(separator) {
    return reactiveReadArray(this).join(separator);
  },
  // keys() iterator only reads `length`, no optimization required
  lastIndexOf(...args) {
    return searchProxy(this, "lastIndexOf", args);
  },
  map(fn, thisArg) {
    return apply(this, "map", fn, thisArg, void 0, arguments);
  },
  pop() {
    return noTracking(this, "pop");
  },
  push(...args) {
    return noTracking(this, "push", args);
  },
  reduce(fn, ...args) {
    return reduce(this, "reduce", fn, args);
  },
  reduceRight(fn, ...args) {
    return reduce(this, "reduceRight", fn, args);
  },
  shift() {
    return noTracking(this, "shift");
  },
  // slice could use ARRAY_ITERATE but also seems to beg for range tracking
  some(fn, thisArg) {
    return apply(this, "some", fn, thisArg, void 0, arguments);
  },
  splice(...args) {
    return noTracking(this, "splice", args);
  },
  toReversed() {
    return reactiveReadArray(this).toReversed();
  },
  toSorted(comparer) {
    return reactiveReadArray(this).toSorted(comparer);
  },
  toSpliced(...args) {
    return reactiveReadArray(this).toSpliced(...args);
  },
  unshift(...args) {
    return noTracking(this, "unshift", args);
  },
  values() {
    return iterator(this, "values", (item) => toWrapped(this, item));
  }
};
function iterator(self2, method, wrapValue) {
  const arr = shallowReadArray(self2);
  const iter = arr[method]();
  if (arr !== self2 && !/* @__PURE__ */ isShallow(self2)) {
    iter._next = iter.next;
    iter.next = () => {
      const result = iter._next();
      if (!result.done) {
        result.value = wrapValue(result.value);
      }
      return result;
    };
  }
  return iter;
}
const arrayProto = Array.prototype;
function apply(self2, method, fn, thisArg, wrappedRetFn, args) {
  const arr = shallowReadArray(self2);
  const needsWrap = arr !== self2 && !/* @__PURE__ */ isShallow(self2);
  const methodFn = arr[method];
  if (methodFn !== arrayProto[method]) {
    const result2 = methodFn.apply(self2, args);
    return needsWrap ? toReactive(result2) : result2;
  }
  let wrappedFn = fn;
  if (arr !== self2) {
    if (needsWrap) {
      wrappedFn = function(item, index) {
        return fn.call(this, toWrapped(self2, item), index, self2);
      };
    } else if (fn.length > 2) {
      wrappedFn = function(item, index) {
        return fn.call(this, item, index, self2);
      };
    }
  }
  const result = methodFn.call(arr, wrappedFn, thisArg);
  return needsWrap && wrappedRetFn ? wrappedRetFn(result) : result;
}
function reduce(self2, method, fn, args) {
  const arr = shallowReadArray(self2);
  const needsWrap = arr !== self2 && !/* @__PURE__ */ isShallow(self2);
  let wrappedFn = fn;
  let wrapInitialAccumulator = false;
  if (arr !== self2) {
    if (needsWrap) {
      wrapInitialAccumulator = args.length === 0;
      wrappedFn = function(acc, item, index) {
        if (wrapInitialAccumulator) {
          wrapInitialAccumulator = false;
          acc = toWrapped(self2, acc);
        }
        return fn.call(this, acc, toWrapped(self2, item), index, self2);
      };
    } else if (fn.length > 3) {
      wrappedFn = function(acc, item, index) {
        return fn.call(this, acc, item, index, self2);
      };
    }
  }
  const result = arr[method](wrappedFn, ...args);
  return wrapInitialAccumulator ? toWrapped(self2, result) : result;
}
function searchProxy(self2, method, args) {
  const arr = /* @__PURE__ */ toRaw(self2);
  track(arr, "iterate", ARRAY_ITERATE_KEY);
  const res = arr[method](...args);
  if ((res === -1 || res === false) && /* @__PURE__ */ isProxy(args[0])) {
    args[0] = /* @__PURE__ */ toRaw(args[0]);
    return arr[method](...args);
  }
  return res;
}
function noTracking(self2, method, args = []) {
  pauseTracking();
  startBatch();
  const res = (/* @__PURE__ */ toRaw(self2))[method].apply(self2, args);
  endBatch();
  resetTracking();
  return res;
}
const isNonTrackableKeys = /* @__PURE__ */ makeMap(`__proto__,__v_isRef,__isVue`);
const builtInSymbols = new Set(
  /* @__PURE__ */ Object.getOwnPropertyNames(Symbol).filter((key) => key !== "arguments" && key !== "caller").map((key) => Symbol[key]).filter(isSymbol)
);
function hasOwnProperty(key) {
  if (!isSymbol(key)) key = String(key);
  const obj = /* @__PURE__ */ toRaw(this);
  track(obj, "has", key);
  return obj.hasOwnProperty(key);
}
class BaseReactiveHandler {
  constructor(_isReadonly = false, _isShallow = false) {
    this._isReadonly = _isReadonly;
    this._isShallow = _isShallow;
  }
  get(target, key, receiver) {
    if (key === "__v_skip") return target["__v_skip"];
    const isReadonly2 = this._isReadonly, isShallow2 = this._isShallow;
    if (key === "__v_isReactive") {
      return !isReadonly2;
    } else if (key === "__v_isReadonly") {
      return isReadonly2;
    } else if (key === "__v_isShallow") {
      return isShallow2;
    } else if (key === "__v_raw") {
      if (receiver === (isReadonly2 ? isShallow2 ? shallowReadonlyMap : readonlyMap : isShallow2 ? shallowReactiveMap : reactiveMap).get(target) || // receiver is not the reactive proxy, but has the same prototype
      // this means the receiver is a user proxy of the reactive proxy
      Object.getPrototypeOf(target) === Object.getPrototypeOf(receiver)) {
        return target;
      }
      return;
    }
    const targetIsArray = isArray$1(target);
    if (!isReadonly2) {
      let fn;
      if (targetIsArray && (fn = arrayInstrumentations[key])) {
        return fn;
      }
      if (key === "hasOwnProperty") {
        return hasOwnProperty;
      }
    }
    const res = Reflect.get(
      target,
      key,
      // if this is a proxy wrapping a ref, return methods using the raw ref
      // as receiver so that we don't have to call `toRaw` on the ref in all
      // its class methods
      /* @__PURE__ */ isRef(target) ? target : receiver
    );
    if (isSymbol(key) ? builtInSymbols.has(key) : isNonTrackableKeys(key)) {
      return res;
    }
    if (!isReadonly2) {
      track(target, "get", key);
    }
    if (isShallow2) {
      return res;
    }
    if (/* @__PURE__ */ isRef(res)) {
      const value = targetIsArray && isIntegerKey(key) ? res : res.value;
      return isReadonly2 && isObject(value) ? /* @__PURE__ */ readonly(value) : value;
    }
    if (isObject(res)) {
      return isReadonly2 ? /* @__PURE__ */ readonly(res) : /* @__PURE__ */ reactive(res);
    }
    return res;
  }
}
class MutableReactiveHandler extends BaseReactiveHandler {
  constructor(isShallow2 = false) {
    super(false, isShallow2);
  }
  set(target, key, value, receiver) {
    let oldValue = target[key];
    const isArrayWithIntegerKey = isArray$1(target) && isIntegerKey(key);
    if (!this._isShallow) {
      const isOldValueReadonly = /* @__PURE__ */ isReadonly(oldValue);
      if (!/* @__PURE__ */ isShallow(value) && !/* @__PURE__ */ isReadonly(value)) {
        oldValue = /* @__PURE__ */ toRaw(oldValue);
        value = /* @__PURE__ */ toRaw(value);
      }
      if (!isArrayWithIntegerKey && /* @__PURE__ */ isRef(oldValue) && !/* @__PURE__ */ isRef(value)) {
        if (isOldValueReadonly) {
          return true;
        } else {
          oldValue.value = value;
          return true;
        }
      }
    }
    const hadKey = isArrayWithIntegerKey ? Number(key) < target.length : hasOwn(target, key);
    const result = Reflect.set(
      target,
      key,
      value,
      /* @__PURE__ */ isRef(target) ? target : receiver
    );
    if (target === /* @__PURE__ */ toRaw(receiver) && result) {
      if (!hadKey) {
        trigger(target, "add", key, value);
      } else if (hasChanged(value, oldValue)) {
        trigger(target, "set", key, value);
      }
    }
    return result;
  }
  deleteProperty(target, key) {
    const hadKey = hasOwn(target, key);
    target[key];
    const result = Reflect.deleteProperty(target, key);
    if (result && hadKey) {
      trigger(target, "delete", key, void 0);
    }
    return result;
  }
  has(target, key) {
    const result = Reflect.has(target, key);
    if (!isSymbol(key) || !builtInSymbols.has(key)) {
      track(target, "has", key);
    }
    return result;
  }
  ownKeys(target) {
    track(
      target,
      "iterate",
      isArray$1(target) ? "length" : ITERATE_KEY
    );
    return Reflect.ownKeys(target);
  }
}
class ReadonlyReactiveHandler extends BaseReactiveHandler {
  constructor(isShallow2 = false) {
    super(true, isShallow2);
  }
  set(target, key) {
    return true;
  }
  deleteProperty(target, key) {
    return true;
  }
}
const mutableHandlers = /* @__PURE__ */ new MutableReactiveHandler();
const readonlyHandlers = /* @__PURE__ */ new ReadonlyReactiveHandler();
const shallowReactiveHandlers = /* @__PURE__ */ new MutableReactiveHandler(true);
const shallowReadonlyHandlers = /* @__PURE__ */ new ReadonlyReactiveHandler(true);
const toShallow = (value) => value;
const getProto = (v) => Reflect.getPrototypeOf(v);
function createIterableMethod(method, isReadonly2, isShallow2) {
  return function(...args) {
    const target = this["__v_raw"];
    const rawTarget = /* @__PURE__ */ toRaw(target);
    const targetIsMap = isMap(rawTarget);
    const isPair = method === "entries" || method === Symbol.iterator && targetIsMap;
    const isKeyOnly = method === "keys" && targetIsMap;
    const innerIterator = target[method](...args);
    const wrap = isShallow2 ? toShallow : isReadonly2 ? toReadonly : toReactive;
    !isReadonly2 && track(
      rawTarget,
      "iterate",
      isKeyOnly ? MAP_KEY_ITERATE_KEY : ITERATE_KEY
    );
    return extend(
      // inheriting all iterator properties
      Object.create(innerIterator),
      {
        // iterator protocol
        next() {
          const { value, done } = innerIterator.next();
          return done ? { value, done } : {
            value: isPair ? [wrap(value[0]), wrap(value[1])] : wrap(value),
            done
          };
        }
      }
    );
  };
}
function createReadonlyMethod(type) {
  return function(...args) {
    return type === "delete" ? false : type === "clear" ? void 0 : this;
  };
}
function createInstrumentations(readonly2, shallow) {
  const instrumentations = {
    get(key) {
      const target = this["__v_raw"];
      const rawTarget = /* @__PURE__ */ toRaw(target);
      const rawKey = /* @__PURE__ */ toRaw(key);
      if (!readonly2) {
        if (hasChanged(key, rawKey)) {
          track(rawTarget, "get", key);
        }
        track(rawTarget, "get", rawKey);
      }
      const { has } = getProto(rawTarget);
      const wrap = shallow ? toShallow : readonly2 ? toReadonly : toReactive;
      if (has.call(rawTarget, key)) {
        return wrap(target.get(key));
      } else if (has.call(rawTarget, rawKey)) {
        return wrap(target.get(rawKey));
      } else if (target !== rawTarget) {
        target.get(key);
      }
    },
    get size() {
      const target = this["__v_raw"];
      !readonly2 && track(/* @__PURE__ */ toRaw(target), "iterate", ITERATE_KEY);
      return target.size;
    },
    has(key) {
      const target = this["__v_raw"];
      const rawTarget = /* @__PURE__ */ toRaw(target);
      const rawKey = /* @__PURE__ */ toRaw(key);
      if (!readonly2) {
        if (hasChanged(key, rawKey)) {
          track(rawTarget, "has", key);
        }
        track(rawTarget, "has", rawKey);
      }
      return key === rawKey ? target.has(key) : target.has(key) || target.has(rawKey);
    },
    forEach(callback, thisArg) {
      const observed = this;
      const target = observed["__v_raw"];
      const rawTarget = /* @__PURE__ */ toRaw(target);
      const wrap = shallow ? toShallow : readonly2 ? toReadonly : toReactive;
      !readonly2 && track(rawTarget, "iterate", ITERATE_KEY);
      return target.forEach((value, key) => {
        return callback.call(thisArg, wrap(value), wrap(key), observed);
      });
    }
  };
  extend(
    instrumentations,
    readonly2 ? {
      add: createReadonlyMethod("add"),
      set: createReadonlyMethod("set"),
      delete: createReadonlyMethod("delete"),
      clear: createReadonlyMethod("clear")
    } : {
      add(value) {
        const target = /* @__PURE__ */ toRaw(this);
        const proto = getProto(target);
        const rawValue = /* @__PURE__ */ toRaw(value);
        const valueToAdd = !shallow && !/* @__PURE__ */ isShallow(value) && !/* @__PURE__ */ isReadonly(value) ? rawValue : value;
        const hadKey = proto.has.call(target, valueToAdd) || hasChanged(value, valueToAdd) && proto.has.call(target, value) || hasChanged(rawValue, valueToAdd) && proto.has.call(target, rawValue);
        if (!hadKey) {
          target.add(valueToAdd);
          trigger(target, "add", valueToAdd, valueToAdd);
        }
        return this;
      },
      set(key, value) {
        if (!shallow && !/* @__PURE__ */ isShallow(value) && !/* @__PURE__ */ isReadonly(value)) {
          value = /* @__PURE__ */ toRaw(value);
        }
        const target = /* @__PURE__ */ toRaw(this);
        const { has, get } = getProto(target);
        let hadKey = has.call(target, key);
        if (!hadKey) {
          key = /* @__PURE__ */ toRaw(key);
          hadKey = has.call(target, key);
        }
        const oldValue = get.call(target, key);
        target.set(key, value);
        if (!hadKey) {
          trigger(target, "add", key, value);
        } else if (hasChanged(value, oldValue)) {
          trigger(target, "set", key, value);
        }
        return this;
      },
      delete(key) {
        const target = /* @__PURE__ */ toRaw(this);
        const { has, get } = getProto(target);
        let hadKey = has.call(target, key);
        if (!hadKey) {
          key = /* @__PURE__ */ toRaw(key);
          hadKey = has.call(target, key);
        }
        get ? get.call(target, key) : void 0;
        const result = target.delete(key);
        if (hadKey) {
          trigger(target, "delete", key, void 0);
        }
        return result;
      },
      clear() {
        const target = /* @__PURE__ */ toRaw(this);
        const hadItems = target.size !== 0;
        const result = target.clear();
        if (hadItems) {
          trigger(
            target,
            "clear",
            void 0,
            void 0
          );
        }
        return result;
      }
    }
  );
  const iteratorMethods = [
    "keys",
    "values",
    "entries",
    Symbol.iterator
  ];
  iteratorMethods.forEach((method) => {
    instrumentations[method] = createIterableMethod(method, readonly2, shallow);
  });
  return instrumentations;
}
function createInstrumentationGetter(isReadonly2, shallow) {
  const instrumentations = createInstrumentations(isReadonly2, shallow);
  return (target, key, receiver) => {
    if (key === "__v_isReactive") {
      return !isReadonly2;
    } else if (key === "__v_isReadonly") {
      return isReadonly2;
    } else if (key === "__v_raw") {
      return target;
    }
    return Reflect.get(
      hasOwn(instrumentations, key) && key in target ? instrumentations : target,
      key,
      receiver
    );
  };
}
const mutableCollectionHandlers = {
  get: /* @__PURE__ */ createInstrumentationGetter(false, false)
};
const shallowCollectionHandlers = {
  get: /* @__PURE__ */ createInstrumentationGetter(false, true)
};
const readonlyCollectionHandlers = {
  get: /* @__PURE__ */ createInstrumentationGetter(true, false)
};
const shallowReadonlyCollectionHandlers = {
  get: /* @__PURE__ */ createInstrumentationGetter(true, true)
};
const reactiveMap = /* @__PURE__ */ new WeakMap();
const shallowReactiveMap = /* @__PURE__ */ new WeakMap();
const readonlyMap = /* @__PURE__ */ new WeakMap();
const shallowReadonlyMap = /* @__PURE__ */ new WeakMap();
function targetTypeMap(rawType) {
  switch (rawType) {
    case "Object":
    case "Array":
      return 1;
    case "Map":
    case "Set":
    case "WeakMap":
    case "WeakSet":
      return 2;
    default:
      return 0;
  }
}
// @__NO_SIDE_EFFECTS__
function reactive(target) {
  if (/* @__PURE__ */ isReadonly(target)) {
    return target;
  }
  return createReactiveObject(
    target,
    false,
    mutableHandlers,
    mutableCollectionHandlers,
    reactiveMap
  );
}
// @__NO_SIDE_EFFECTS__
function shallowReactive(target) {
  return createReactiveObject(
    target,
    false,
    shallowReactiveHandlers,
    shallowCollectionHandlers,
    shallowReactiveMap
  );
}
// @__NO_SIDE_EFFECTS__
function readonly(target) {
  return createReactiveObject(
    target,
    true,
    readonlyHandlers,
    readonlyCollectionHandlers,
    readonlyMap
  );
}
// @__NO_SIDE_EFFECTS__
function shallowReadonly(target) {
  return createReactiveObject(
    target,
    true,
    shallowReadonlyHandlers,
    shallowReadonlyCollectionHandlers,
    shallowReadonlyMap
  );
}
function createReactiveObject(target, isReadonly2, baseHandlers, collectionHandlers, proxyMap) {
  if (!isObject(target)) {
    return target;
  }
  if (target["__v_raw"] && !(isReadonly2 && target["__v_isReactive"])) {
    return target;
  }
  if (target["__v_skip"] || !Object.isExtensible(target)) {
    return target;
  }
  const existingProxy = proxyMap.get(target);
  if (existingProxy) {
    return existingProxy;
  }
  const targetType = targetTypeMap(toRawType(target));
  if (targetType === 0) {
    return target;
  }
  const proxy = new Proxy(
    target,
    targetType === 2 ? collectionHandlers : baseHandlers
  );
  proxyMap.set(target, proxy);
  return proxy;
}
// @__NO_SIDE_EFFECTS__
function isReactive(value) {
  if (/* @__PURE__ */ isReadonly(value)) {
    return /* @__PURE__ */ isReactive(value["__v_raw"]);
  }
  return !!(value && value["__v_isReactive"]);
}
// @__NO_SIDE_EFFECTS__
function isReadonly(value) {
  return !!(value && value["__v_isReadonly"]);
}
// @__NO_SIDE_EFFECTS__
function isShallow(value) {
  return !!(value && value["__v_isShallow"]);
}
// @__NO_SIDE_EFFECTS__
function isProxy(value) {
  return value ? !!value["__v_raw"] : false;
}
// @__NO_SIDE_EFFECTS__
function toRaw(observed) {
  const raw = observed && observed["__v_raw"];
  return raw ? /* @__PURE__ */ toRaw(raw) : observed;
}
function markRaw(value) {
  if (!hasOwn(value, "__v_skip") && Object.isExtensible(value)) {
    def(value, "__v_skip", true);
  }
  return value;
}
const toReactive = (value) => isObject(value) ? /* @__PURE__ */ reactive(value) : value;
const toReadonly = (value) => isObject(value) ? /* @__PURE__ */ readonly(value) : value;
// @__NO_SIDE_EFFECTS__
function isRef(r) {
  return r ? r["__v_isRef"] === true : false;
}
// @__NO_SIDE_EFFECTS__
function ref(value) {
  return createRef(value, false);
}
// @__NO_SIDE_EFFECTS__
function shallowRef(value) {
  return createRef(value, true);
}
function createRef(rawValue, shallow) {
  if (/* @__PURE__ */ isRef(rawValue)) {
    return rawValue;
  }
  return new RefImpl(rawValue, shallow);
}
class RefImpl {
  constructor(value, isShallow2) {
    this.dep = new Dep();
    this["__v_isRef"] = true;
    this["__v_isShallow"] = false;
    this._rawValue = isShallow2 ? value : /* @__PURE__ */ toRaw(value);
    this._value = isShallow2 ? value : toReactive(value);
    this["__v_isShallow"] = isShallow2;
  }
  get value() {
    {
      this.dep.track();
    }
    return this._value;
  }
  set value(newValue) {
    const oldValue = this._rawValue;
    const useDirectValue = this["__v_isShallow"] || /* @__PURE__ */ isShallow(newValue) || /* @__PURE__ */ isReadonly(newValue);
    newValue = useDirectValue ? newValue : /* @__PURE__ */ toRaw(newValue);
    if (hasChanged(newValue, oldValue)) {
      this._rawValue = newValue;
      this._value = useDirectValue ? newValue : toReactive(newValue);
      {
        this.dep.trigger();
      }
    }
  }
}
function unref(ref2) {
  return /* @__PURE__ */ isRef(ref2) ? ref2.value : ref2;
}
const shallowUnwrapHandlers = {
  get: (target, key, receiver) => key === "__v_raw" ? target : unref(Reflect.get(target, key, receiver)),
  set: (target, key, value, receiver) => {
    const oldValue = target[key];
    if (/* @__PURE__ */ isRef(oldValue) && !/* @__PURE__ */ isRef(value)) {
      oldValue.value = value;
      return true;
    } else {
      return Reflect.set(target, key, value, receiver);
    }
  }
};
function proxyRefs(objectWithRefs) {
  return /* @__PURE__ */ isReactive(objectWithRefs) ? objectWithRefs : new Proxy(objectWithRefs, shallowUnwrapHandlers);
}
class ComputedRefImpl {
  constructor(fn, setter, isSSR) {
    this.fn = fn;
    this.setter = setter;
    this._value = void 0;
    this.dep = new Dep(this);
    this.__v_isRef = true;
    this.deps = void 0;
    this.depsTail = void 0;
    this.flags = 16;
    this.globalVersion = globalVersion - 1;
    this.next = void 0;
    this.effect = this;
    this["__v_isReadonly"] = !setter;
    this.isSSR = isSSR;
  }
  /**
   * @internal
   */
  notify() {
    this.flags |= 16;
    if (!(this.flags & 8) && // avoid infinite self recursion
    activeSub !== this) {
      batch(this, true);
      return true;
    }
  }
  get value() {
    const link = this.dep.track();
    refreshComputed(this);
    if (link) {
      link.version = this.dep.version;
    }
    return this._value;
  }
  set value(newValue) {
    if (this.setter) {
      this.setter(newValue);
    }
  }
}
// @__NO_SIDE_EFFECTS__
function computed$1(getterOrOptions, debugOptions, isSSR = false) {
  let getter;
  let setter;
  if (isFunction(getterOrOptions)) {
    getter = getterOrOptions;
  } else {
    getter = getterOrOptions.get;
    setter = getterOrOptions.set;
  }
  const cRef = new ComputedRefImpl(getter, setter, isSSR);
  return cRef;
}
const INITIAL_WATCHER_VALUE = {};
const cleanupMap = /* @__PURE__ */ new WeakMap();
let activeWatcher = void 0;
function onWatcherCleanup(cleanupFn, failSilently = false, owner = activeWatcher) {
  if (owner) {
    let cleanups = cleanupMap.get(owner);
    if (!cleanups) cleanupMap.set(owner, cleanups = []);
    cleanups.push(cleanupFn);
  }
}
function watch$1(source, cb, options = EMPTY_OBJ) {
  const { immediate, deep, once, scheduler, augmentJob, call } = options;
  const reactiveGetter = (source2) => {
    if (deep) return source2;
    if (/* @__PURE__ */ isShallow(source2) || deep === false || deep === 0)
      return traverse(source2, 1);
    return traverse(source2);
  };
  let effect2;
  let getter;
  let cleanup;
  let boundCleanup;
  let forceTrigger = false;
  let isMultiSource = false;
  if (/* @__PURE__ */ isRef(source)) {
    getter = () => source.value;
    forceTrigger = /* @__PURE__ */ isShallow(source);
  } else if (/* @__PURE__ */ isReactive(source)) {
    getter = () => reactiveGetter(source);
    forceTrigger = true;
  } else if (isArray$1(source)) {
    isMultiSource = true;
    forceTrigger = source.some((s) => /* @__PURE__ */ isReactive(s) || /* @__PURE__ */ isShallow(s));
    getter = () => source.map((s) => {
      if (/* @__PURE__ */ isRef(s)) {
        return s.value;
      } else if (/* @__PURE__ */ isReactive(s)) {
        return reactiveGetter(s);
      } else if (isFunction(s)) {
        return call ? call(s, 2) : s();
      } else ;
    });
  } else if (isFunction(source)) {
    if (cb) {
      getter = call ? () => call(source, 2) : source;
    } else {
      getter = () => {
        if (cleanup) {
          pauseTracking();
          try {
            cleanup();
          } finally {
            resetTracking();
          }
        }
        const currentEffect = activeWatcher;
        activeWatcher = effect2;
        try {
          return call ? call(source, 3, [boundCleanup]) : source(boundCleanup);
        } finally {
          activeWatcher = currentEffect;
        }
      };
    }
  } else {
    getter = NOOP;
  }
  if (cb && deep) {
    const baseGetter = getter;
    const depth = deep === true ? Infinity : deep;
    getter = () => traverse(baseGetter(), depth);
  }
  const scope = getCurrentScope();
  const watchHandle = () => {
    effect2.stop();
    if (scope && scope.active) {
      remove(scope.effects, effect2);
    }
  };
  if (once && cb) {
    const _cb = cb;
    cb = (...args) => {
      const res = _cb(...args);
      watchHandle();
      return res;
    };
  }
  let oldValue = isMultiSource ? new Array(source.length).fill(INITIAL_WATCHER_VALUE) : INITIAL_WATCHER_VALUE;
  const job = (immediateFirstRun) => {
    if (!(effect2.flags & 1) || !effect2.dirty && !immediateFirstRun) {
      return;
    }
    if (cb) {
      const newValue = effect2.run();
      if (immediateFirstRun || deep || forceTrigger || (isMultiSource ? newValue.some((v, i) => hasChanged(v, oldValue[i])) : hasChanged(newValue, oldValue))) {
        if (cleanup) {
          cleanup();
        }
        const currentWatcher = activeWatcher;
        activeWatcher = effect2;
        try {
          const args = [
            newValue,
            // pass undefined as the old value when it's changed for the first time
            oldValue === INITIAL_WATCHER_VALUE ? void 0 : isMultiSource && oldValue[0] === INITIAL_WATCHER_VALUE ? [] : oldValue,
            boundCleanup
          ];
          oldValue = newValue;
          call ? call(cb, 3, args) : (
            // @ts-expect-error
            cb(...args)
          );
        } finally {
          activeWatcher = currentWatcher;
        }
      }
    } else {
      effect2.run();
    }
  };
  if (augmentJob) {
    augmentJob(job);
  }
  effect2 = new ReactiveEffect(getter);
  effect2.scheduler = scheduler ? () => scheduler(job, false) : job;
  boundCleanup = (fn) => onWatcherCleanup(fn, false, effect2);
  cleanup = effect2.onStop = () => {
    const cleanups = cleanupMap.get(effect2);
    if (cleanups) {
      if (call) {
        call(cleanups, 4);
      } else {
        for (const cleanup2 of cleanups) cleanup2();
      }
      cleanupMap.delete(effect2);
    }
  };
  if (cb) {
    if (immediate) {
      job(true);
    } else {
      oldValue = effect2.run();
    }
  } else if (scheduler) {
    scheduler(job.bind(null, true), true);
  } else {
    effect2.run();
  }
  watchHandle.pause = effect2.pause.bind(effect2);
  watchHandle.resume = effect2.resume.bind(effect2);
  watchHandle.stop = watchHandle;
  return watchHandle;
}
function traverse(value, depth = Infinity, seen) {
  if (depth <= 0 || !isObject(value) || value["__v_skip"]) {
    return value;
  }
  seen = seen || /* @__PURE__ */ new Map();
  if ((seen.get(value) || 0) >= depth) {
    return value;
  }
  seen.set(value, depth);
  depth--;
  if (/* @__PURE__ */ isRef(value)) {
    traverse(value.value, depth, seen);
  } else if (isArray$1(value)) {
    for (let i = 0; i < value.length; i++) {
      traverse(value[i], depth, seen);
    }
  } else if (isSet(value) || isMap(value)) {
    value.forEach((v) => {
      traverse(v, depth, seen);
    });
  } else if (isPlainObject(value)) {
    for (const key in value) {
      traverse(value[key], depth, seen);
    }
    for (const key of Object.getOwnPropertySymbols(value)) {
      if (Object.prototype.propertyIsEnumerable.call(value, key)) {
        traverse(value[key], depth, seen);
      }
    }
  }
  return value;
}
/**
* @vue/runtime-core v3.5.41
* (c) 2018-present Yuxi (Evan) You and Vue contributors
* @license MIT
**/
const stack = [];
let isWarning = false;
function warn$1(msg, ...args) {
  if (isWarning) return;
  isWarning = true;
  pauseTracking();
  const instance = stack.length ? stack[stack.length - 1].component : null;
  const appWarnHandler = instance && instance.appContext.config.warnHandler;
  const trace = getComponentTrace();
  if (appWarnHandler) {
    callWithErrorHandling(
      appWarnHandler,
      instance,
      11,
      [
        // eslint-disable-next-line no-restricted-syntax
        msg + args.map((a) => {
          var _a, _b;
          return (_b = (_a = a.toString) == null ? void 0 : _a.call(a)) != null ? _b : JSON.stringify(a);
        }).join(""),
        instance && instance.proxy,
        trace.map(
          ({ vnode }) => `at <${formatComponentName(instance, vnode.type)}>`
        ).join("\n"),
        trace
      ]
    );
  } else {
    const warnArgs = [`[Vue warn]: ${msg}`, ...args];
    if (trace.length && // avoid spamming console during tests
    true) {
      warnArgs.push(`
`, ...formatTrace(trace));
    }
    console.warn(...warnArgs);
  }
  resetTracking();
  isWarning = false;
}
function getComponentTrace() {
  let currentVNode = stack[stack.length - 1];
  if (!currentVNode) {
    return [];
  }
  const normalizedStack = [];
  while (currentVNode) {
    const last = normalizedStack[0];
    if (last && last.vnode === currentVNode) {
      last.recurseCount++;
    } else {
      normalizedStack.push({
        vnode: currentVNode,
        recurseCount: 0
      });
    }
    const parentInstance = currentVNode.component && currentVNode.component.parent;
    currentVNode = parentInstance && parentInstance.vnode;
  }
  return normalizedStack;
}
function formatTrace(trace) {
  const logs = [];
  trace.forEach((entry, i) => {
    logs.push(...i === 0 ? [] : [`
`], ...formatTraceEntry(entry));
  });
  return logs;
}
function formatTraceEntry({ vnode, recurseCount }) {
  const postfix = recurseCount > 0 ? `... (${recurseCount} recursive calls)` : ``;
  const isRoot = vnode.component ? vnode.component.parent == null : false;
  const open = ` at <${formatComponentName(
    vnode.component,
    vnode.type,
    isRoot
  )}`;
  const close = `>` + postfix;
  return vnode.props ? [open, ...formatProps(vnode.props), close] : [open + close];
}
function formatProps(props) {
  const res = [];
  const keys = Object.keys(props);
  keys.slice(0, 3).forEach((key) => {
    res.push(...formatProp(key, props[key]));
  });
  if (keys.length > 3) {
    res.push(` ...`);
  }
  return res;
}
function formatProp(key, value, raw) {
  if (isString(value)) {
    value = JSON.stringify(value);
    return raw ? value : [`${key}=${value}`];
  } else if (typeof value === "number" || typeof value === "boolean" || value == null) {
    return raw ? value : [`${key}=${value}`];
  } else if (/* @__PURE__ */ isRef(value)) {
    value = formatProp(key, /* @__PURE__ */ toRaw(value.value), true);
    return raw ? value : [`${key}=Ref<`, value, `>`];
  } else if (isFunction(value)) {
    return [`${key}=fn${value.name ? `<${value.name}>` : ``}`];
  } else {
    value = /* @__PURE__ */ toRaw(value);
    return raw ? value : [`${key}=`, value];
  }
}
function callWithErrorHandling(fn, instance, type, args) {
  try {
    return args ? fn(...args) : fn();
  } catch (err) {
    handleError(err, instance, type);
  }
}
function callWithAsyncErrorHandling(fn, instance, type, args) {
  if (isFunction(fn)) {
    const res = callWithErrorHandling(fn, instance, type, args);
    if (res && isPromise(res)) {
      res.catch((err) => {
        handleError(err, instance, type);
      });
    }
    return res;
  }
  if (isArray$1(fn)) {
    const values = [];
    for (let i = 0; i < fn.length; i++) {
      values.push(callWithAsyncErrorHandling(fn[i], instance, type, args));
    }
    return values;
  }
}
function handleError(err, instance, type, throwInDev = true) {
  const contextVNode = instance ? instance.vnode : null;
  const { errorHandler, throwUnhandledErrorInProduction } = instance && instance.appContext.config || EMPTY_OBJ;
  if (instance) {
    let cur = instance.parent;
    const exposedInstance = instance.proxy;
    const errorInfo = `https://vuejs.org/error-reference/#runtime-${type}`;
    while (cur) {
      const errorCapturedHooks = cur.ec;
      if (errorCapturedHooks) {
        for (let i = 0; i < errorCapturedHooks.length; i++) {
          if (errorCapturedHooks[i](err, exposedInstance, errorInfo) === false) {
            return;
          }
        }
      }
      cur = cur.parent;
    }
    if (errorHandler) {
      pauseTracking();
      callWithErrorHandling(errorHandler, null, 10, [
        err,
        exposedInstance,
        errorInfo
      ]);
      resetTracking();
      return;
    }
  }
  logError(err, type, contextVNode, throwInDev, throwUnhandledErrorInProduction);
}
function logError(err, type, contextVNode, throwInDev = true, throwInProd = false) {
  if (throwInProd) {
    throw err;
  } else {
    console.error(err);
  }
}
const queue = [];
let flushIndex = -1;
const pendingPostFlushCbs = [];
let activePostFlushCbs = null;
let postFlushIndex = 0;
const resolvedPromise = /* @__PURE__ */ Promise.resolve();
let currentFlushPromise = null;
function nextTick(fn) {
  const p2 = currentFlushPromise || resolvedPromise;
  return fn ? p2.then(this ? fn.bind(this) : fn) : p2;
}
function findInsertionIndex$1(id) {
  let start = flushIndex + 1;
  let end = queue.length;
  while (start < end) {
    const middle = start + end >>> 1;
    const middleJob = queue[middle];
    const middleJobId = getId(middleJob);
    if (middleJobId < id || middleJobId === id && middleJob.flags & 2) {
      start = middle + 1;
    } else {
      end = middle;
    }
  }
  return start;
}
function queueJob(job) {
  if (!(job.flags & 1)) {
    const jobId = getId(job);
    const lastJob = queue[queue.length - 1];
    if (!lastJob || // fast path when the job id is larger than the tail
    !(job.flags & 2) && jobId >= getId(lastJob)) {
      queue.push(job);
    } else {
      queue.splice(findInsertionIndex$1(jobId), 0, job);
    }
    job.flags |= 1;
    queueFlush();
  }
}
function queueFlush() {
  if (!currentFlushPromise) {
    currentFlushPromise = resolvedPromise.then(flushJobs);
  }
}
function queuePostFlushCb(cb) {
  if (!isArray$1(cb)) {
    if (activePostFlushCbs && cb.id === -1) {
      activePostFlushCbs.splice(postFlushIndex + 1, 0, cb);
    } else if (!(cb.flags & 1)) {
      pendingPostFlushCbs.push(cb);
      cb.flags |= 1;
    }
  } else {
    for (let i = 0; i < cb.length; i++) {
      pendingPostFlushCbs.push(cb[i]);
    }
  }
  queueFlush();
}
function flushPreFlushCbs(instance, seen, i = flushIndex + 1) {
  for (; i < queue.length; i++) {
    const cb = queue[i];
    if (cb && cb.flags & 2) {
      if (instance && cb.id !== instance.uid) {
        continue;
      }
      queue.splice(i, 1);
      i--;
      if (cb.flags & 4) {
        cb.flags &= -2;
      }
      cb();
      if (!(cb.flags & 4)) {
        cb.flags &= -2;
      }
    }
  }
}
function flushPostFlushCbs(seen) {
  if (pendingPostFlushCbs.length) {
    const deduped = [...new Set(pendingPostFlushCbs)].sort(
      (a, b) => getId(a) - getId(b)
    );
    pendingPostFlushCbs.length = 0;
    if (activePostFlushCbs) {
      for (let i = 0; i < deduped.length; i++) {
        activePostFlushCbs.push(deduped[i]);
      }
      return;
    }
    activePostFlushCbs = deduped;
    for (postFlushIndex = 0; postFlushIndex < activePostFlushCbs.length; postFlushIndex++) {
      const cb = activePostFlushCbs[postFlushIndex];
      if (cb.flags & 4) {
        cb.flags &= -2;
      }
      if (!(cb.flags & 8)) cb();
      cb.flags &= -2;
    }
    activePostFlushCbs = null;
    postFlushIndex = 0;
  }
}
const getId = (job) => job.id == null ? job.flags & 2 ? -1 : Infinity : job.id;
function flushJobs(seen) {
  try {
    for (flushIndex = 0; flushIndex < queue.length; flushIndex++) {
      const job = queue[flushIndex];
      if (job && !(job.flags & 8)) {
        if (false) ;
        if (job.flags & 4) {
          job.flags &= ~1;
        }
        callWithErrorHandling(
          job,
          job.i,
          job.i ? 15 : 14
        );
        if (!(job.flags & 4)) {
          job.flags &= ~1;
        }
      }
    }
  } finally {
    for (; flushIndex < queue.length; flushIndex++) {
      const job = queue[flushIndex];
      if (job) {
        job.flags &= -2;
      }
    }
    flushIndex = -1;
    queue.length = 0;
    flushPostFlushCbs();
    currentFlushPromise = null;
    if (queue.length || pendingPostFlushCbs.length) {
      flushJobs();
    }
  }
}
let currentRenderingInstance = null;
let currentScopeId = null;
function setCurrentRenderingInstance(instance) {
  const prev = currentRenderingInstance;
  currentRenderingInstance = instance;
  currentScopeId = instance && instance.type.__scopeId || null;
  return prev;
}
function withCtx(fn, ctx = currentRenderingInstance, isNonScopedSlot) {
  if (!ctx) return fn;
  if (fn._n) {
    return fn;
  }
  const renderFnWithContext = (...args) => {
    if (renderFnWithContext._d) {
      setBlockTracking(-1);
    }
    const prevInstance = setCurrentRenderingInstance(ctx);
    const prevStackSize = blockStack.length;
    let res;
    try {
      res = fn(...args);
    } finally {
      for (let i = blockStack.length; i > prevStackSize; i--) closeBlock();
      setCurrentRenderingInstance(prevInstance);
      if (renderFnWithContext._d) {
        setBlockTracking(1);
      }
    }
    return res;
  };
  renderFnWithContext._n = true;
  renderFnWithContext._c = true;
  renderFnWithContext._d = true;
  return renderFnWithContext;
}
function withDirectives(vnode, directives) {
  if (currentRenderingInstance === null) {
    return vnode;
  }
  const instance = getComponentPublicInstance(currentRenderingInstance);
  const bindings = vnode.dirs || (vnode.dirs = []);
  for (let i = 0; i < directives.length; i++) {
    let [dir, value, arg, modifiers = EMPTY_OBJ] = directives[i];
    if (dir) {
      if (isFunction(dir)) {
        dir = {
          mounted: dir,
          updated: dir
        };
      }
      if (dir.deep) {
        traverse(value);
      }
      bindings.push({
        dir,
        instance,
        value,
        oldValue: void 0,
        arg,
        modifiers
      });
    }
  }
  return vnode;
}
function invokeDirectiveHook(vnode, prevVNode, instance, name) {
  const bindings = vnode.dirs;
  const oldBindings = prevVNode && prevVNode.dirs;
  for (let i = 0; i < bindings.length; i++) {
    const binding = bindings[i];
    if (oldBindings) {
      binding.oldValue = oldBindings[i].value;
    }
    let hook = binding.dir[name];
    if (hook) {
      pauseTracking();
      callWithAsyncErrorHandling(hook, instance, 8, [
        vnode.el,
        binding,
        vnode,
        prevVNode
      ]);
      resetTracking();
    }
  }
}
function provide(key, value) {
  if (currentInstance) {
    let provides = currentInstance.provides;
    const parentProvides = currentInstance.parent && currentInstance.parent.provides;
    if (parentProvides === provides) {
      provides = currentInstance.provides = Object.create(parentProvides);
    }
    provides[key] = value;
  }
}
function inject(key, defaultValue, treatDefaultAsFactory = false) {
  const instance = getCurrentInstance();
  if (instance || currentApp) {
    let provides = currentApp ? currentApp._context.provides : instance ? instance.parent == null || instance.ce ? instance.vnode.appContext && instance.vnode.appContext.provides : instance.parent.provides : void 0;
    if (provides && key in provides) {
      return provides[key];
    } else if (arguments.length > 1) {
      return treatDefaultAsFactory && isFunction(defaultValue) ? defaultValue.call(instance && instance.proxy) : defaultValue;
    } else ;
  }
}
const ssrContextKey = /* @__PURE__ */ Symbol.for("v-scx");
const useSSRContext = () => {
  {
    const ctx = inject(ssrContextKey);
    return ctx;
  }
};
function watch(source, cb, options) {
  return doWatch(source, cb, options);
}
function doWatch(source, cb, options = EMPTY_OBJ) {
  const { immediate, deep, flush, once } = options;
  const baseWatchOptions = extend({}, options);
  const runsImmediately = cb && immediate || !cb && flush !== "post";
  let ssrCleanup;
  if (isInSSRComponentSetup) {
    if (flush === "sync") {
      const ctx = useSSRContext();
      ssrCleanup = ctx.__watcherHandles || (ctx.__watcherHandles = []);
    } else if (!runsImmediately) {
      const watchStopHandle = () => {
      };
      watchStopHandle.stop = NOOP;
      watchStopHandle.resume = NOOP;
      watchStopHandle.pause = NOOP;
      return watchStopHandle;
    }
  }
  const instance = currentInstance;
  baseWatchOptions.call = (fn, type, args) => callWithAsyncErrorHandling(fn, instance, type, args);
  let isPre = false;
  if (flush === "post") {
    baseWatchOptions.scheduler = (job) => {
      queuePostRenderEffect(job, instance && instance.suspense);
    };
  } else if (flush !== "sync") {
    isPre = true;
    baseWatchOptions.scheduler = (job, isFirstRun) => {
      if (isFirstRun) {
        job();
      } else {
        queueJob(job);
      }
    };
  }
  baseWatchOptions.augmentJob = (job) => {
    if (cb) {
      job.flags |= 4;
    }
    if (isPre) {
      job.flags |= 2;
      if (instance) {
        job.id = instance.uid;
        job.i = instance;
      }
    }
  };
  const watchHandle = watch$1(source, cb, baseWatchOptions);
  if (isInSSRComponentSetup) {
    if (ssrCleanup) {
      ssrCleanup.push(watchHandle);
    } else if (runsImmediately) {
      watchHandle();
    }
  }
  return watchHandle;
}
function instanceWatch(source, value, options) {
  const publicThis = this.proxy;
  const getter = isString(source) ? source.includes(".") ? createPathGetter(publicThis, source) : () => publicThis[source] : source.bind(publicThis, publicThis);
  let cb;
  if (isFunction(value)) {
    cb = value;
  } else {
    cb = value.handler;
    options = value;
  }
  const reset = setCurrentInstance(this);
  const res = doWatch(getter, cb.bind(publicThis), options);
  reset();
  return res;
}
function createPathGetter(ctx, path) {
  const segments = path.split(".");
  return () => {
    let cur = ctx;
    for (let i = 0; i < segments.length && cur; i++) {
      cur = cur[segments[i]];
    }
    return cur;
  };
}
const TeleportEndKey = /* @__PURE__ */ Symbol("_vte");
const isTeleport = (type) => type.__isTeleport;
const leaveCbKey = /* @__PURE__ */ Symbol("_leaveCb");
function findNonCommentChild(children) {
  let child = children[0];
  if (children.length > 1) {
    for (const c of children) {
      if (c.type !== Comment) {
        child = c;
        break;
      }
    }
  }
  return child;
}
function getInnerChild$1(vnode) {
  if (!isKeepAlive(vnode)) {
    if (isTeleport(vnode.type) && vnode.children) {
      return findNonCommentChild(vnode.children);
    }
    return vnode;
  }
  if (vnode.component) {
    return vnode.component.subTree;
  }
  const { shapeFlag, children } = vnode;
  if (children) {
    if (shapeFlag & 16) {
      return children[0];
    }
    if (shapeFlag & 32 && isFunction(children.default)) {
      return children.default();
    }
  }
}
function setTransitionHooks(vnode, hooks) {
  if (vnode.shapeFlag & 6 && vnode.component) {
    vnode.transition = hooks;
    const subTree = vnode.component.subTree;
    setTransitionHooks(
      isTeleport(subTree.type) ? getInnerChild$1(subTree) || subTree : subTree,
      hooks
    );
  } else if (vnode.shapeFlag & 128) {
    vnode.ssContent.transition = hooks.clone(vnode.ssContent);
    vnode.ssFallback.transition = hooks.clone(vnode.ssFallback);
  } else {
    vnode.transition = hooks;
  }
}
// @__NO_SIDE_EFFECTS__
function defineComponent(options, extraOptions) {
  return isFunction(options) ? (
    // #8236: extend call and options.name access are considered side-effects
    // by Rollup, so we have to wrap it in a pure-annotated IIFE.
    /* @__PURE__ */ (() => extend({ name: options.name }, extraOptions, { setup: options }))()
  ) : options;
}
function markAsyncBoundary(instance) {
  instance.ids = [instance.ids[0] + instance.ids[2]++ + "-", 0, 0];
}
function isTemplateRefKey(refs, key) {
  let desc;
  return !!((desc = Object.getOwnPropertyDescriptor(refs, key)) && !desc.configurable);
}
const pendingSetRefMap = /* @__PURE__ */ new WeakMap();
function setRef(rawRef, oldRawRef, parentSuspense, vnode, isUnmount = false) {
  if (isArray$1(rawRef)) {
    rawRef.forEach(
      (r, i) => setRef(
        r,
        oldRawRef && (isArray$1(oldRawRef) ? oldRawRef[i] : oldRawRef),
        parentSuspense,
        vnode,
        isUnmount
      )
    );
    return;
  }
  if (isAsyncWrapper(vnode) && !isUnmount) {
    if (vnode.shapeFlag & 512 && vnode.type.__asyncResolved && vnode.component.subTree.component) {
      setRef(rawRef, oldRawRef, parentSuspense, vnode.component.subTree);
    }
    return;
  }
  const refValue = vnode.shapeFlag & 4 ? getComponentPublicInstance(vnode.component) : vnode.el;
  const value = isUnmount ? null : refValue;
  const { i: owner, r: ref3 } = rawRef;
  const oldRef = oldRawRef && oldRawRef.r;
  const refs = owner.refs === EMPTY_OBJ ? owner.refs = {} : owner.refs;
  const setupState = owner.setupState;
  const rawSetupState = /* @__PURE__ */ toRaw(setupState);
  const canSetSetupRef = setupState === EMPTY_OBJ ? NO : (key) => {
    if (isTemplateRefKey(refs, key)) {
      return false;
    }
    return hasOwn(rawSetupState, key);
  };
  const canSetRef = (ref22, key) => {
    if (key && isTemplateRefKey(refs, key)) {
      return false;
    }
    return true;
  };
  if (oldRef != null && oldRef !== ref3) {
    invalidatePendingSetRef(oldRawRef);
    if (isString(oldRef)) {
      refs[oldRef] = null;
      if (canSetSetupRef(oldRef)) {
        setupState[oldRef] = null;
      }
    } else if (/* @__PURE__ */ isRef(oldRef)) {
      const oldRawRefAtom = oldRawRef;
      if (canSetRef(oldRef, oldRawRefAtom.k)) {
        oldRef.value = null;
      }
      if (oldRawRefAtom.k) refs[oldRawRefAtom.k] = null;
    }
  }
  if (isFunction(ref3)) {
    callWithErrorHandling(ref3, owner, 12, [value, refs]);
  } else {
    const _isString = isString(ref3);
    const _isRef = /* @__PURE__ */ isRef(ref3);
    if (_isString || _isRef) {
      const doSet = () => {
        if (rawRef.f) {
          const existing = _isString ? canSetSetupRef(ref3) ? setupState[ref3] : refs[ref3] : canSetRef() || !rawRef.k ? ref3.value : refs[rawRef.k];
          if (isUnmount) {
            isArray$1(existing) && remove(existing, refValue);
          } else {
            if (!isArray$1(existing)) {
              if (_isString) {
                refs[ref3] = [refValue];
                if (canSetSetupRef(ref3)) {
                  setupState[ref3] = refs[ref3];
                }
              } else {
                const newVal = [refValue];
                if (canSetRef(ref3, rawRef.k)) {
                  ref3.value = newVal;
                }
                if (rawRef.k) refs[rawRef.k] = newVal;
              }
            } else if (!existing.includes(refValue)) {
              existing.push(refValue);
            }
          }
        } else if (_isString) {
          refs[ref3] = value;
          if (canSetSetupRef(ref3)) {
            setupState[ref3] = value;
          }
        } else if (_isRef) {
          if (canSetRef(ref3, rawRef.k)) {
            ref3.value = value;
          }
          if (rawRef.k) refs[rawRef.k] = value;
        } else ;
      };
      if (value) {
        const job = () => {
          doSet();
          pendingSetRefMap.delete(rawRef);
        };
        job.id = -1;
        pendingSetRefMap.set(rawRef, job);
        queuePostRenderEffect(job, parentSuspense);
      } else {
        invalidatePendingSetRef(rawRef);
        doSet();
      }
    }
  }
}
function invalidatePendingSetRef(rawRef) {
  const pendingSetRef = pendingSetRefMap.get(rawRef);
  if (pendingSetRef) {
    pendingSetRef.flags |= 8;
    pendingSetRefMap.delete(rawRef);
  }
}
getGlobalThis().requestIdleCallback || ((cb) => setTimeout(cb, 1));
getGlobalThis().cancelIdleCallback || ((id) => clearTimeout(id));
const isAsyncWrapper = (i) => !!i.type.__asyncLoader;
const isKeepAlive = (vnode) => vnode.type.__isKeepAlive;
function onActivated(hook, target) {
  registerKeepAliveHook(hook, "a", target);
}
function onDeactivated(hook, target) {
  registerKeepAliveHook(hook, "da", target);
}
function registerKeepAliveHook(hook, type, target = currentInstance) {
  const wrappedHook = hook.__wdc || (hook.__wdc = () => {
    let current = target;
    while (current) {
      if (current.isDeactivated) {
        return;
      }
      current = current.parent;
    }
    return hook();
  });
  injectHook(type, wrappedHook, target);
  if (target) {
    let current = target.parent;
    while (current && current.parent) {
      if (isKeepAlive(current.parent.vnode)) {
        injectToKeepAliveRoot(wrappedHook, type, target, current);
      }
      current = current.parent;
    }
  }
}
function injectToKeepAliveRoot(hook, type, target, keepAliveRoot) {
  const injected = injectHook(
    type,
    hook,
    keepAliveRoot,
    true
    /* prepend */
  );
  onUnmounted(() => {
    remove(keepAliveRoot[type], injected);
  }, target);
}
function injectHook(type, hook, target = currentInstance, prepend = false) {
  if (target) {
    const hooks = target[type] || (target[type] = []);
    const wrappedHook = hook.__weh || (hook.__weh = (...args) => {
      pauseTracking();
      const reset = setCurrentInstance(target);
      const res = callWithAsyncErrorHandling(hook, target, type, args);
      reset();
      resetTracking();
      return res;
    });
    if (prepend) {
      hooks.unshift(wrappedHook);
    } else {
      hooks.push(wrappedHook);
    }
    return wrappedHook;
  }
}
const createHook = (lifecycle) => (hook, target = currentInstance) => {
  if (!isInSSRComponentSetup || lifecycle === "sp") {
    injectHook(lifecycle, (...args) => hook(...args), target);
  }
};
const onBeforeMount = createHook("bm");
const onMounted = createHook("m");
const onBeforeUpdate = createHook(
  "bu"
);
const onUpdated = createHook("u");
const onBeforeUnmount = createHook(
  "bum"
);
const onUnmounted = createHook("um");
const onServerPrefetch = createHook(
  "sp"
);
const onRenderTriggered = createHook("rtg");
const onRenderTracked = createHook("rtc");
function onErrorCaptured(hook, target = currentInstance) {
  injectHook("ec", hook, target);
}
const COMPONENTS = "components";
function resolveComponent(name, maybeSelfReference) {
  return resolveAsset(COMPONENTS, name, true, maybeSelfReference) || name;
}
const NULL_DYNAMIC_COMPONENT = /* @__PURE__ */ Symbol.for("v-ndc");
function resolveDynamicComponent(component) {
  if (isString(component)) {
    return resolveAsset(COMPONENTS, component, false) || component;
  } else {
    return component || NULL_DYNAMIC_COMPONENT;
  }
}
function resolveAsset(type, name, warnMissing = true, maybeSelfReference = false) {
  const instance = currentRenderingInstance || currentInstance;
  if (instance) {
    const Component = instance.type;
    {
      const selfName = getComponentName(
        Component,
        false
      );
      if (selfName && (selfName === name || selfName === camelize(name) || selfName === capitalize(camelize(name)))) {
        return Component;
      }
    }
    const res = (
      // local registration
      // check instance[type] first which is resolved for options API
      resolve(instance[type] || Component[type], name) || // global registration
      resolve(instance.appContext[type], name)
    );
    if (!res && maybeSelfReference) {
      return Component;
    }
    return res;
  }
}
function resolve(registry, name) {
  return registry && (registry[name] || registry[camelize(name)] || registry[capitalize(camelize(name))]);
}
function renderList(source, renderItem, cache, index) {
  let ret;
  const cached = cache;
  const sourceIsArray = isArray$1(source);
  if (sourceIsArray || isString(source)) {
    const sourceIsReactiveArray = sourceIsArray && /* @__PURE__ */ isReactive(source);
    let needsWrap = false;
    let isReadonlySource = false;
    if (sourceIsReactiveArray) {
      needsWrap = !/* @__PURE__ */ isShallow(source);
      isReadonlySource = /* @__PURE__ */ isReadonly(source);
      source = shallowReadArray(source);
    }
    ret = new Array(source.length);
    for (let i = 0, l = source.length; i < l; i++) {
      ret[i] = renderItem(
        needsWrap ? isReadonlySource ? toReadonly(toReactive(source[i])) : toReactive(source[i]) : source[i],
        i,
        void 0,
        cached
      );
    }
  } else if (typeof source === "number") {
    {
      ret = new Array(source);
      for (let i = 0; i < source; i++) {
        ret[i] = renderItem(i + 1, i, void 0, cached);
      }
    }
  } else if (isObject(source)) {
    if (source[Symbol.iterator]) {
      ret = Array.from(
        source,
        (item, i) => renderItem(item, i, void 0, cached)
      );
    } else {
      const keys = Object.keys(source);
      ret = new Array(keys.length);
      for (let i = 0, l = keys.length; i < l; i++) {
        const key = keys[i];
        ret[i] = renderItem(source[key], key, i, cached);
      }
    }
  } else {
    ret = [];
  }
  return ret;
}
const getPublicInstance = (i) => {
  if (!i) return null;
  if (isStatefulComponent(i)) return getComponentPublicInstance(i);
  return getPublicInstance(i.parent);
};
const publicPropertiesMap = (
  // Move PURE marker to new line to workaround compiler discarding it
  // due to type annotation
  /* @__PURE__ */ extend(/* @__PURE__ */ Object.create(null), {
    $: (i) => i,
    $el: (i) => i.vnode.el,
    $data: (i) => i.data,
    $props: (i) => i.props,
    $attrs: (i) => i.attrs,
    $slots: (i) => i.slots,
    $refs: (i) => i.refs,
    $parent: (i) => getPublicInstance(i.parent),
    $root: (i) => getPublicInstance(i.root),
    $host: (i) => i.ce,
    $emit: (i) => i.emit,
    $options: (i) => resolveMergedOptions(i),
    $forceUpdate: (i) => i.f || (i.f = () => {
      queueJob(i.update);
    }),
    $nextTick: (i) => i.n || (i.n = nextTick.bind(i.proxy)),
    $watch: (i) => instanceWatch.bind(i)
  })
);
const hasSetupBinding = (state, key) => state !== EMPTY_OBJ && !state.__isScriptSetup && hasOwn(state, key);
const PublicInstanceProxyHandlers = {
  get({ _: instance }, key) {
    if (key === "__v_skip") {
      return true;
    }
    const { ctx, setupState, data, props, accessCache, type, appContext } = instance;
    if (key[0] !== "$") {
      const n = accessCache[key];
      if (n !== void 0) {
        switch (n) {
          case 1:
            return setupState[key];
          case 2:
            return data[key];
          case 4:
            return ctx[key];
          case 3:
            return props[key];
        }
      } else if (hasSetupBinding(setupState, key)) {
        accessCache[key] = 1;
        return setupState[key];
      } else if (data !== EMPTY_OBJ && hasOwn(data, key)) {
        accessCache[key] = 2;
        return data[key];
      } else if (hasOwn(props, key)) {
        accessCache[key] = 3;
        return props[key];
      } else if (ctx !== EMPTY_OBJ && hasOwn(ctx, key)) {
        accessCache[key] = 4;
        return ctx[key];
      } else if (shouldCacheAccess) {
        accessCache[key] = 0;
      }
    }
    const publicGetter = publicPropertiesMap[key];
    let cssModule, globalProperties;
    if (publicGetter) {
      if (key === "$attrs") {
        track(instance.attrs, "get", "");
      }
      return publicGetter(instance);
    } else if (
      // css module (injected by vue-loader)
      (cssModule = type.__cssModules) && (cssModule = cssModule[key])
    ) {
      return cssModule;
    } else if (ctx !== EMPTY_OBJ && hasOwn(ctx, key)) {
      accessCache[key] = 4;
      return ctx[key];
    } else if (
      // global properties
      globalProperties = appContext.config.globalProperties, hasOwn(globalProperties, key)
    ) {
      {
        return globalProperties[key];
      }
    } else ;
  },
  set({ _: instance }, key, value) {
    const { data, setupState, ctx } = instance;
    if (hasSetupBinding(setupState, key)) {
      setupState[key] = value;
      return true;
    } else if (data !== EMPTY_OBJ && hasOwn(data, key)) {
      data[key] = value;
      return true;
    } else if (hasOwn(instance.props, key)) {
      return false;
    }
    if (key[0] === "$" && key.slice(1) in instance) {
      return false;
    } else {
      {
        ctx[key] = value;
      }
    }
    return true;
  },
  has({
    _: { data, setupState, accessCache, ctx, appContext, props, type }
  }, key) {
    let cssModules;
    return !!(accessCache[key] || data !== EMPTY_OBJ && key[0] !== "$" && hasOwn(data, key) || hasSetupBinding(setupState, key) || hasOwn(props, key) || hasOwn(ctx, key) || hasOwn(publicPropertiesMap, key) || hasOwn(appContext.config.globalProperties, key) || (cssModules = type.__cssModules) && cssModules[key]);
  },
  defineProperty(target, key, descriptor) {
    if (descriptor.get != null) {
      target._.accessCache[key] = 0;
    } else if (hasOwn(descriptor, "value")) {
      this.set(target, key, descriptor.value, null);
    }
    return Reflect.defineProperty(target, key, descriptor);
  }
};
function normalizePropsOrEmits(props) {
  return isArray$1(props) ? props.reduce(
    (normalized, p2) => (normalized[p2] = null, normalized),
    {}
  ) : props;
}
let shouldCacheAccess = true;
function applyOptions(instance) {
  const options = resolveMergedOptions(instance);
  const publicThis = instance.proxy;
  const ctx = instance.ctx;
  shouldCacheAccess = false;
  if (options.beforeCreate) {
    callHook(options.beforeCreate, instance, "bc");
  }
  const {
    // state
    data: dataOptions,
    computed: computedOptions,
    methods,
    watch: watchOptions,
    provide: provideOptions,
    inject: injectOptions,
    // lifecycle
    created,
    beforeMount,
    mounted,
    beforeUpdate,
    updated,
    activated,
    deactivated,
    beforeDestroy,
    beforeUnmount,
    destroyed,
    unmounted,
    render,
    renderTracked,
    renderTriggered,
    errorCaptured,
    serverPrefetch,
    // public API
    expose,
    inheritAttrs,
    // assets
    components,
    directives,
    filters
  } = options;
  const checkDuplicateProperties = null;
  if (injectOptions) {
    resolveInjections(injectOptions, ctx, checkDuplicateProperties);
  }
  if (methods) {
    for (const key in methods) {
      const methodHandler = methods[key];
      if (isFunction(methodHandler)) {
        {
          ctx[key] = methodHandler.bind(publicThis);
        }
      }
    }
  }
  if (dataOptions) {
    const data = dataOptions.call(publicThis, publicThis);
    if (!isObject(data)) ;
    else {
      instance.data = /* @__PURE__ */ reactive(data);
    }
  }
  shouldCacheAccess = true;
  if (computedOptions) {
    for (const key in computedOptions) {
      const opt = computedOptions[key];
      const get = isFunction(opt) ? opt.bind(publicThis, publicThis) : isFunction(opt.get) ? opt.get.bind(publicThis, publicThis) : NOOP;
      const set = !isFunction(opt) && isFunction(opt.set) ? opt.set.bind(publicThis) : NOOP;
      const c = computed({
        get,
        set
      });
      Object.defineProperty(ctx, key, {
        enumerable: true,
        configurable: true,
        get: () => c.value,
        set: (v) => c.value = v
      });
    }
  }
  if (watchOptions) {
    for (const key in watchOptions) {
      createWatcher(watchOptions[key], ctx, publicThis, key);
    }
  }
  if (provideOptions) {
    const provides = isFunction(provideOptions) ? provideOptions.call(publicThis) : provideOptions;
    Reflect.ownKeys(provides).forEach((key) => {
      provide(key, provides[key]);
    });
  }
  if (created) {
    callHook(created, instance, "c");
  }
  function registerLifecycleHook(register, hook) {
    if (isArray$1(hook)) {
      hook.forEach((_hook) => register(_hook.bind(publicThis)));
    } else if (hook) {
      register(hook.bind(publicThis));
    }
  }
  registerLifecycleHook(onBeforeMount, beforeMount);
  registerLifecycleHook(onMounted, mounted);
  registerLifecycleHook(onBeforeUpdate, beforeUpdate);
  registerLifecycleHook(onUpdated, updated);
  registerLifecycleHook(onActivated, activated);
  registerLifecycleHook(onDeactivated, deactivated);
  registerLifecycleHook(onErrorCaptured, errorCaptured);
  registerLifecycleHook(onRenderTracked, renderTracked);
  registerLifecycleHook(onRenderTriggered, renderTriggered);
  registerLifecycleHook(onBeforeUnmount, beforeUnmount);
  registerLifecycleHook(onUnmounted, unmounted);
  registerLifecycleHook(onServerPrefetch, serverPrefetch);
  if (isArray$1(expose)) {
    if (expose.length) {
      const exposed = instance.exposed || (instance.exposed = {});
      expose.forEach((key) => {
        Object.defineProperty(exposed, key, {
          get: () => publicThis[key],
          set: (val) => publicThis[key] = val,
          enumerable: true
        });
      });
    } else if (!instance.exposed) {
      instance.exposed = {};
    }
  }
  if (render && instance.render === NOOP) {
    instance.render = render;
  }
  if (inheritAttrs != null) {
    instance.inheritAttrs = inheritAttrs;
  }
  if (components) instance.components = components;
  if (directives) instance.directives = directives;
  if (serverPrefetch) {
    markAsyncBoundary(instance);
  }
}
function resolveInjections(injectOptions, ctx, checkDuplicateProperties = NOOP) {
  if (isArray$1(injectOptions)) {
    injectOptions = normalizeInject(injectOptions);
  }
  for (const key in injectOptions) {
    const opt = injectOptions[key];
    let injected;
    if (isObject(opt)) {
      if ("default" in opt) {
        injected = inject(
          opt.from || key,
          opt.default,
          true
        );
      } else {
        injected = inject(opt.from || key);
      }
    } else {
      injected = inject(opt);
    }
    if (/* @__PURE__ */ isRef(injected)) {
      Object.defineProperty(ctx, key, {
        enumerable: true,
        configurable: true,
        get: () => injected.value,
        set: (v) => injected.value = v
      });
    } else {
      ctx[key] = injected;
    }
  }
}
function callHook(hook, instance, type) {
  callWithAsyncErrorHandling(
    isArray$1(hook) ? hook.map((h2) => h2.bind(instance.proxy)) : hook.bind(instance.proxy),
    instance,
    type
  );
}
function createWatcher(raw, ctx, publicThis, key) {
  let getter = key.includes(".") ? createPathGetter(publicThis, key) : () => publicThis[key];
  if (isString(raw)) {
    const handler = ctx[raw];
    if (isFunction(handler)) {
      {
        watch(getter, handler);
      }
    }
  } else if (isFunction(raw)) {
    {
      watch(getter, raw.bind(publicThis));
    }
  } else if (isObject(raw)) {
    if (isArray$1(raw)) {
      raw.forEach((r) => createWatcher(r, ctx, publicThis, key));
    } else {
      const handler = isFunction(raw.handler) ? raw.handler.bind(publicThis) : ctx[raw.handler];
      if (isFunction(handler)) {
        watch(getter, handler, raw);
      }
    }
  } else ;
}
function resolveMergedOptions(instance) {
  const base = instance.type;
  const { mixins, extends: extendsOptions } = base;
  const {
    mixins: globalMixins,
    optionsCache: cache,
    config: { optionMergeStrategies }
  } = instance.appContext;
  const cached = cache.get(base);
  let resolved;
  if (cached) {
    resolved = cached;
  } else if (!globalMixins.length && !mixins && !extendsOptions) {
    {
      resolved = base;
    }
  } else {
    resolved = {};
    if (globalMixins.length) {
      globalMixins.forEach(
        (m) => mergeOptions$1(resolved, m, optionMergeStrategies, true)
      );
    }
    mergeOptions$1(resolved, base, optionMergeStrategies);
  }
  if (isObject(base)) {
    cache.set(base, resolved);
  }
  return resolved;
}
function mergeOptions$1(to, from, strats, asMixin = false) {
  const { mixins, extends: extendsOptions } = from;
  if (extendsOptions) {
    mergeOptions$1(to, extendsOptions, strats, true);
  }
  if (mixins) {
    mixins.forEach(
      (m) => mergeOptions$1(to, m, strats, true)
    );
  }
  for (const key in from) {
    if (asMixin && key === "expose") ;
    else {
      const strat = internalOptionMergeStrats[key] || strats && strats[key];
      to[key] = strat ? strat(to[key], from[key]) : from[key];
    }
  }
  return to;
}
const internalOptionMergeStrats = {
  data: mergeDataFn,
  props: mergeEmitsOrPropsOptions,
  emits: mergeEmitsOrPropsOptions,
  // objects
  methods: mergeObjectOptions,
  computed: mergeObjectOptions,
  // lifecycle
  beforeCreate: mergeAsArray,
  created: mergeAsArray,
  beforeMount: mergeAsArray,
  mounted: mergeAsArray,
  beforeUpdate: mergeAsArray,
  updated: mergeAsArray,
  beforeDestroy: mergeAsArray,
  beforeUnmount: mergeAsArray,
  destroyed: mergeAsArray,
  unmounted: mergeAsArray,
  activated: mergeAsArray,
  deactivated: mergeAsArray,
  errorCaptured: mergeAsArray,
  serverPrefetch: mergeAsArray,
  // assets
  components: mergeObjectOptions,
  directives: mergeObjectOptions,
  // watch
  watch: mergeWatchOptions,
  // provide / inject
  provide: mergeDataFn,
  inject: mergeInject
};
function mergeDataFn(to, from) {
  if (!from) {
    return to;
  }
  if (!to) {
    return from;
  }
  return function mergedDataFn() {
    return extend(
      isFunction(to) ? to.call(this, this) : to,
      isFunction(from) ? from.call(this, this) : from
    );
  };
}
function mergeInject(to, from) {
  return mergeObjectOptions(normalizeInject(to), normalizeInject(from));
}
function normalizeInject(raw) {
  if (isArray$1(raw)) {
    const res = {};
    for (let i = 0; i < raw.length; i++) {
      res[raw[i]] = raw[i];
    }
    return res;
  }
  return raw;
}
function mergeAsArray(to, from) {
  return to ? [...new Set([].concat(to, from))] : from;
}
function mergeObjectOptions(to, from) {
  return to ? extend(/* @__PURE__ */ Object.create(null), to, from) : from;
}
function mergeEmitsOrPropsOptions(to, from) {
  if (to) {
    if (isArray$1(to) && isArray$1(from)) {
      return [.../* @__PURE__ */ new Set([...to, ...from])];
    }
    return extend(
      /* @__PURE__ */ Object.create(null),
      normalizePropsOrEmits(to),
      normalizePropsOrEmits(from != null ? from : {})
    );
  } else {
    return from;
  }
}
function mergeWatchOptions(to, from) {
  if (!to) return from;
  if (!from) return to;
  const merged = extend(/* @__PURE__ */ Object.create(null), to);
  for (const key in from) {
    merged[key] = mergeAsArray(to[key], from[key]);
  }
  return merged;
}
function createAppContext() {
  return {
    app: null,
    config: {
      isNativeTag: NO,
      performance: false,
      globalProperties: {},
      optionMergeStrategies: {},
      errorHandler: void 0,
      warnHandler: void 0,
      compilerOptions: {}
    },
    mixins: [],
    components: {},
    directives: {},
    provides: /* @__PURE__ */ Object.create(null),
    optionsCache: /* @__PURE__ */ new WeakMap(),
    propsCache: /* @__PURE__ */ new WeakMap(),
    emitsCache: /* @__PURE__ */ new WeakMap()
  };
}
let uid$1 = 0;
function createAppAPI(render, hydrate) {
  return function createApp2(rootComponent, rootProps = null) {
    if (!isFunction(rootComponent)) {
      rootComponent = extend({}, rootComponent);
    }
    if (rootProps != null && !isObject(rootProps)) {
      rootProps = null;
    }
    const context = createAppContext();
    const installedPlugins = /* @__PURE__ */ new WeakSet();
    const pluginCleanupFns = [];
    let isMounted = false;
    const app = context.app = {
      _uid: uid$1++,
      _component: rootComponent,
      _props: rootProps,
      _container: null,
      _context: context,
      _instance: null,
      version,
      get config() {
        return context.config;
      },
      set config(v) {
      },
      use(plugin, ...options) {
        if (installedPlugins.has(plugin)) ;
        else if (plugin && isFunction(plugin.install)) {
          installedPlugins.add(plugin);
          plugin.install(app, ...options);
        } else if (isFunction(plugin)) {
          installedPlugins.add(plugin);
          plugin(app, ...options);
        } else ;
        return app;
      },
      mixin(mixin) {
        {
          if (!context.mixins.includes(mixin)) {
            context.mixins.push(mixin);
          }
        }
        return app;
      },
      component(name, component) {
        if (!component) {
          return context.components[name];
        }
        context.components[name] = component;
        return app;
      },
      directive(name, directive) {
        if (!directive) {
          return context.directives[name];
        }
        context.directives[name] = directive;
        return app;
      },
      mount(rootContainer, isHydrate, namespace) {
        if (!isMounted) {
          const vnode = app._ceVNode || createVNode(rootComponent, rootProps);
          vnode.appContext = context;
          if (namespace === true) {
            namespace = "svg";
          } else if (namespace === false) {
            namespace = void 0;
          }
          {
            render(vnode, rootContainer, namespace);
          }
          isMounted = true;
          app._container = rootContainer;
          rootContainer.__vue_app__ = app;
          return getComponentPublicInstance(vnode.component);
        }
      },
      onUnmount(cleanupFn) {
        pluginCleanupFns.push(cleanupFn);
      },
      unmount() {
        if (isMounted) {
          callWithAsyncErrorHandling(
            pluginCleanupFns,
            app._instance,
            16
          );
          render(null, app._container);
          delete app._container.__vue_app__;
        }
      },
      provide(key, value) {
        context.provides[key] = value;
        return app;
      },
      runWithContext(fn) {
        const lastApp = currentApp;
        currentApp = app;
        try {
          return fn();
        } finally {
          currentApp = lastApp;
        }
      }
    };
    return app;
  };
}
let currentApp = null;
const getModelModifiers = (props, modelName) => {
  return modelName === "modelValue" || modelName === "model-value" ? props.modelModifiers : props[`${modelName}Modifiers`] || props[`${camelize(modelName)}Modifiers`] || props[`${hyphenate(modelName)}Modifiers`];
};
function emit(instance, event, ...rawArgs) {
  if (instance.isUnmounted) return;
  const props = instance.vnode.props || EMPTY_OBJ;
  let args = rawArgs;
  const isModelListener2 = event.startsWith("update:");
  const modifiers = isModelListener2 && getModelModifiers(props, event.slice(7));
  if (modifiers) {
    if (modifiers.trim) {
      args = rawArgs.map((a) => isString(a) ? a.trim() : a);
    }
    if (modifiers.number) {
      args = rawArgs.map(looseToNumber);
    }
  }
  let handlerName;
  let handler = props[handlerName = toHandlerKey(event)] || // also try camelCase event handler (#2249)
  props[handlerName = toHandlerKey(camelize(event))];
  if (!handler && isModelListener2) {
    handler = props[handlerName = toHandlerKey(hyphenate(event))];
  }
  if (handler) {
    callWithAsyncErrorHandling(
      handler,
      instance,
      6,
      args
    );
  }
  const onceHandler = props[handlerName + `Once`];
  if (onceHandler) {
    if (!instance.emitted) {
      instance.emitted = {};
    } else if (instance.emitted[handlerName]) {
      return;
    }
    instance.emitted[handlerName] = true;
    callWithAsyncErrorHandling(
      onceHandler,
      instance,
      6,
      args
    );
  }
}
const mixinEmitsCache = /* @__PURE__ */ new WeakMap();
function normalizeEmitsOptions(comp, appContext, asMixin = false) {
  const cache = asMixin ? mixinEmitsCache : appContext.emitsCache;
  const cached = cache.get(comp);
  if (cached !== void 0) {
    return cached;
  }
  const raw = comp.emits;
  let normalized = {};
  let hasExtends = false;
  if (!isFunction(comp)) {
    const extendEmits = (raw2) => {
      const normalizedFromExtend = normalizeEmitsOptions(raw2, appContext, true);
      if (normalizedFromExtend) {
        hasExtends = true;
        extend(normalized, normalizedFromExtend);
      }
    };
    if (!asMixin && appContext.mixins.length) {
      appContext.mixins.forEach(extendEmits);
    }
    if (comp.extends) {
      extendEmits(comp.extends);
    }
    if (comp.mixins) {
      comp.mixins.forEach(extendEmits);
    }
  }
  if (!raw && !hasExtends) {
    if (isObject(comp)) {
      cache.set(comp, null);
    }
    return null;
  }
  if (isArray$1(raw)) {
    raw.forEach((key) => normalized[key] = null);
  } else {
    extend(normalized, raw);
  }
  if (isObject(comp)) {
    cache.set(comp, normalized);
  }
  return normalized;
}
function isEmitListener(options, key) {
  if (!options || !isOn(key)) {
    return false;
  }
  key = key.slice(2);
  key = key === "Once" ? key : key.replace(/Once$/, "");
  return hasOwn(options, key[0].toLowerCase() + key.slice(1)) || hasOwn(options, hyphenate(key)) || hasOwn(options, key);
}
function markAttrsAccessed() {
}
function renderComponentRoot(instance) {
  const {
    type: Component,
    vnode,
    proxy,
    withProxy,
    propsOptions: [propsOptions],
    slots,
    attrs,
    emit: emit2,
    render,
    renderCache,
    props,
    data,
    setupState,
    ctx,
    inheritAttrs
  } = instance;
  const prev = setCurrentRenderingInstance(instance);
  let result;
  let fallthroughAttrs;
  try {
    if (vnode.shapeFlag & 4) {
      const proxyToUse = withProxy || proxy;
      const thisProxy = false ? new Proxy(proxyToUse, {
        get(target, key, receiver) {
          warn$1(
            `Property '${String(
              key
            )}' was accessed via 'this'. Avoid using 'this' in templates.`
          );
          return Reflect.get(target, key, receiver);
        }
      }) : proxyToUse;
      result = normalizeVNode(
        render.call(
          thisProxy,
          proxyToUse,
          renderCache,
          false ? /* @__PURE__ */ shallowReadonly(props) : props,
          setupState,
          data,
          ctx
        )
      );
      fallthroughAttrs = attrs;
    } else {
      const render2 = Component;
      if (false) ;
      result = normalizeVNode(
        render2.length > 1 ? render2(
          false ? /* @__PURE__ */ shallowReadonly(props) : props,
          false ? {
            get attrs() {
              markAttrsAccessed();
              return /* @__PURE__ */ shallowReadonly(attrs);
            },
            slots,
            emit: emit2
          } : { attrs, slots, emit: emit2 }
        ) : render2(
          false ? /* @__PURE__ */ shallowReadonly(props) : props,
          null
        )
      );
      fallthroughAttrs = Component.props ? attrs : getFunctionalFallthrough(attrs);
    }
  } catch (err) {
    blockStack.length = 0;
    handleError(err, instance, 1);
    result = createVNode(Comment);
  }
  let root = result;
  if (fallthroughAttrs && inheritAttrs !== false) {
    const keys = Object.keys(fallthroughAttrs);
    const { shapeFlag } = root;
    if (keys.length) {
      if (shapeFlag & (1 | 6)) {
        if (propsOptions && keys.some(isModelListener)) {
          fallthroughAttrs = filterModelListeners(
            fallthroughAttrs,
            propsOptions
          );
        }
        root = cloneVNode(root, fallthroughAttrs, false, true);
      }
    }
  }
  if (vnode.dirs) {
    root = cloneVNode(root, null, false, true);
    root.dirs = root.dirs ? root.dirs.concat(vnode.dirs) : vnode.dirs;
  }
  if (vnode.transition) {
    const child = isTeleport(root.type) ? getInnerChild$1(root) || root : root;
    setTransitionHooks(child, vnode.transition);
  }
  {
    result = root;
  }
  setCurrentRenderingInstance(prev);
  return result;
}
const getFunctionalFallthrough = (attrs) => {
  let res;
  for (const key in attrs) {
    if (key === "class" || key === "style" || isOn(key)) {
      (res || (res = {}))[key] = attrs[key];
    }
  }
  return res;
};
const filterModelListeners = (attrs, props) => {
  const res = {};
  for (const key in attrs) {
    if (!isModelListener(key) || !(key.slice(9) in props)) {
      res[key] = attrs[key];
    }
  }
  return res;
};
function shouldUpdateComponent(prevVNode, nextVNode, optimized) {
  const { props: prevProps, children: prevChildren, component } = prevVNode;
  const { props: nextProps, children: nextChildren, patchFlag } = nextVNode;
  const emits = component.emitsOptions;
  if (nextVNode.dirs || nextVNode.transition) {
    return true;
  }
  if (optimized && patchFlag >= 0) {
    if (patchFlag & 1024) {
      return true;
    }
    if (patchFlag & 16) {
      if (!prevProps) {
        return !!nextProps;
      }
      return hasPropsChanged(prevProps, nextProps, emits);
    } else if (patchFlag & 8) {
      const dynamicProps = nextVNode.dynamicProps;
      for (let i = 0; i < dynamicProps.length; i++) {
        const key = dynamicProps[i];
        if (hasPropValueChanged(nextProps, prevProps, key) && !isEmitListener(emits, key)) {
          return true;
        }
      }
    }
  } else {
    if (prevChildren || nextChildren) {
      if (!nextChildren || !nextChildren.$stable) {
        return true;
      }
    }
    if (prevProps === nextProps) {
      return false;
    }
    if (!prevProps) {
      return !!nextProps;
    }
    if (!nextProps) {
      return true;
    }
    return hasPropsChanged(prevProps, nextProps, emits);
  }
  return false;
}
function hasPropsChanged(prevProps, nextProps, emitsOptions) {
  const nextKeys = Object.keys(nextProps);
  if (nextKeys.length !== Object.keys(prevProps).length) {
    return true;
  }
  for (let i = 0; i < nextKeys.length; i++) {
    const key = nextKeys[i];
    if (hasPropValueChanged(nextProps, prevProps, key) && !isEmitListener(emitsOptions, key)) {
      return true;
    }
  }
  return false;
}
function hasPropValueChanged(nextProps, prevProps, key) {
  const nextProp = nextProps[key];
  const prevProp = prevProps[key];
  if (key === "style" && isObject(nextProp) && isObject(prevProp)) {
    return !looseEqual(nextProp, prevProp);
  }
  return nextProp !== prevProp;
}
function updateHOCHostEl({ vnode, parent, suspense }, el) {
  while (parent) {
    const root = parent.subTree;
    if (root.suspense && root.suspense.activeBranch === vnode) {
      root.suspense.vnode.el = root.el = el;
      vnode = root;
    }
    if (root === vnode) {
      (vnode = parent.vnode).el = el;
      parent = parent.parent;
    } else {
      break;
    }
  }
  if (suspense && suspense.activeBranch === vnode) {
    suspense.vnode.el = el;
  }
}
const internalObjectProto = {};
const createInternalObject = () => Object.create(internalObjectProto);
const isInternalObject = (obj) => Object.getPrototypeOf(obj) === internalObjectProto;
function initProps(instance, rawProps, isStateful, isSSR = false) {
  const props = {};
  const attrs = createInternalObject();
  instance.propsDefaults = /* @__PURE__ */ Object.create(null);
  setFullProps(instance, rawProps, props, attrs);
  for (const key in instance.propsOptions[0]) {
    if (!(key in props)) {
      props[key] = void 0;
    }
  }
  if (isStateful) {
    instance.props = isSSR ? props : /* @__PURE__ */ shallowReactive(props);
  } else {
    if (!instance.type.props) {
      instance.props = attrs;
    } else {
      instance.props = props;
    }
  }
  instance.attrs = attrs;
}
function updateProps(instance, rawProps, rawPrevProps, optimized) {
  const {
    props,
    attrs,
    vnode: { patchFlag }
  } = instance;
  const rawCurrentProps = /* @__PURE__ */ toRaw(props);
  const [options] = instance.propsOptions;
  let hasAttrsChanged = false;
  if (
    // always force full diff in dev
    // - #1942 if hmr is enabled with sfc component
    // - vite#872 non-sfc component used by sfc component
    (optimized || patchFlag > 0) && !(patchFlag & 16)
  ) {
    if (patchFlag & 8) {
      const propsToUpdate = instance.vnode.dynamicProps;
      for (let i = 0; i < propsToUpdate.length; i++) {
        let key = propsToUpdate[i];
        if (isEmitListener(instance.emitsOptions, key)) {
          continue;
        }
        const value = rawProps[key];
        if (options) {
          if (hasOwn(attrs, key)) {
            if (value !== attrs[key]) {
              attrs[key] = value;
              hasAttrsChanged = true;
            }
          } else {
            const camelizedKey = camelize(key);
            props[camelizedKey] = resolvePropValue(
              options,
              rawCurrentProps,
              camelizedKey,
              value,
              instance,
              false
            );
          }
        } else {
          if (value !== attrs[key]) {
            attrs[key] = value;
            hasAttrsChanged = true;
          }
        }
      }
    }
  } else {
    if (setFullProps(instance, rawProps, props, attrs)) {
      hasAttrsChanged = true;
    }
    let kebabKey;
    for (const key in rawCurrentProps) {
      if (!rawProps || // for camelCase
      !hasOwn(rawProps, key) && // it's possible the original props was passed in as kebab-case
      // and converted to camelCase (#955)
      ((kebabKey = hyphenate(key)) === key || !hasOwn(rawProps, kebabKey))) {
        if (options) {
          if (rawPrevProps && // for camelCase
          (rawPrevProps[key] !== void 0 || // for kebab-case
          rawPrevProps[kebabKey] !== void 0)) {
            props[key] = resolvePropValue(
              options,
              rawCurrentProps,
              key,
              void 0,
              instance,
              true
            );
          }
        } else {
          delete props[key];
        }
      }
    }
    if (attrs !== rawCurrentProps) {
      for (const key in attrs) {
        if (!rawProps || !hasOwn(rawProps, key) && true) {
          delete attrs[key];
          hasAttrsChanged = true;
        }
      }
    }
  }
  if (hasAttrsChanged) {
    trigger(instance.attrs, "set", "");
  }
}
function setFullProps(instance, rawProps, props, attrs) {
  const [options, needCastKeys] = instance.propsOptions;
  let hasAttrsChanged = false;
  let rawCastValues;
  if (rawProps) {
    for (let key in rawProps) {
      if (isReservedProp(key)) {
        continue;
      }
      const value = rawProps[key];
      let camelKey;
      if (options && hasOwn(options, camelKey = camelize(key))) {
        if (!needCastKeys || !needCastKeys.includes(camelKey)) {
          props[camelKey] = value;
        } else {
          (rawCastValues || (rawCastValues = {}))[camelKey] = value;
        }
      } else if (!isEmitListener(instance.emitsOptions, key)) {
        if (!(key in attrs) || value !== attrs[key]) {
          attrs[key] = value;
          hasAttrsChanged = true;
        }
      }
    }
  }
  if (needCastKeys) {
    const rawCurrentProps = /* @__PURE__ */ toRaw(props);
    const castValues = rawCastValues || EMPTY_OBJ;
    for (let i = 0; i < needCastKeys.length; i++) {
      const key = needCastKeys[i];
      props[key] = resolvePropValue(
        options,
        rawCurrentProps,
        key,
        castValues[key],
        instance,
        !hasOwn(castValues, key)
      );
    }
  }
  return hasAttrsChanged;
}
function resolvePropValue(options, props, key, value, instance, isAbsent) {
  const opt = options[key];
  if (opt != null) {
    const hasDefault = hasOwn(opt, "default");
    if (hasDefault && value === void 0) {
      const defaultValue = opt.default;
      if (opt.type !== Function && !opt.skipFactory && isFunction(defaultValue)) {
        const { propsDefaults } = instance;
        if (key in propsDefaults) {
          value = propsDefaults[key];
        } else {
          const reset = setCurrentInstance(instance);
          value = propsDefaults[key] = defaultValue.call(
            null,
            props
          );
          reset();
        }
      } else {
        value = defaultValue;
      }
      if (instance.ce) {
        instance.ce._setProp(key, value);
      }
    }
    if (opt[
      0
      /* shouldCast */
    ]) {
      if (isAbsent && !hasDefault) {
        value = false;
      } else if (opt[
        1
        /* shouldCastTrue */
      ] && (value === "" || value === hyphenate(key))) {
        value = true;
      }
    }
  }
  return value;
}
const mixinPropsCache = /* @__PURE__ */ new WeakMap();
function normalizePropsOptions(comp, appContext, asMixin = false) {
  const cache = asMixin ? mixinPropsCache : appContext.propsCache;
  const cached = cache.get(comp);
  if (cached) {
    return cached;
  }
  const raw = comp.props;
  const normalized = {};
  const needCastKeys = [];
  let hasExtends = false;
  if (!isFunction(comp)) {
    const extendProps = (raw2) => {
      hasExtends = true;
      const [props, keys] = normalizePropsOptions(raw2, appContext, true);
      extend(normalized, props);
      if (keys) needCastKeys.push(...keys);
    };
    if (!asMixin && appContext.mixins.length) {
      appContext.mixins.forEach(extendProps);
    }
    if (comp.extends) {
      extendProps(comp.extends);
    }
    if (comp.mixins) {
      comp.mixins.forEach(extendProps);
    }
  }
  if (!raw && !hasExtends) {
    if (isObject(comp)) {
      cache.set(comp, EMPTY_ARR);
    }
    return EMPTY_ARR;
  }
  if (isArray$1(raw)) {
    for (let i = 0; i < raw.length; i++) {
      const normalizedKey = camelize(raw[i]);
      if (validatePropName(normalizedKey)) {
        normalized[normalizedKey] = EMPTY_OBJ;
      }
    }
  } else if (raw) {
    for (const key in raw) {
      const normalizedKey = camelize(key);
      if (validatePropName(normalizedKey)) {
        const opt = raw[key];
        const prop = normalized[normalizedKey] = isArray$1(opt) || isFunction(opt) ? { type: opt } : extend({}, opt);
        const propType = prop.type;
        let shouldCast = false;
        let shouldCastTrue = true;
        if (isArray$1(propType)) {
          for (let index = 0; index < propType.length; ++index) {
            const type = propType[index];
            const typeName = isFunction(type) && type.name;
            if (typeName === "Boolean") {
              shouldCast = true;
              break;
            } else if (typeName === "String") {
              shouldCastTrue = false;
            }
          }
        } else {
          shouldCast = isFunction(propType) && propType.name === "Boolean";
        }
        prop[
          0
          /* shouldCast */
        ] = shouldCast;
        prop[
          1
          /* shouldCastTrue */
        ] = shouldCastTrue;
        if (shouldCast || hasOwn(prop, "default")) {
          needCastKeys.push(normalizedKey);
        }
      }
    }
  }
  const res = [normalized, needCastKeys];
  if (isObject(comp)) {
    cache.set(comp, res);
  }
  return res;
}
function validatePropName(key) {
  if (key[0] !== "$" && !isReservedProp(key)) {
    return true;
  }
  return false;
}
const isInternalKey = (key) => key === "_" || key === "_ctx" || key === "$stable";
const normalizeSlotValue = (value) => isArray$1(value) ? value.map(normalizeVNode) : [normalizeVNode(value)];
const normalizeSlot$1 = (key, rawSlot, ctx) => {
  if (rawSlot._n) {
    return rawSlot;
  }
  const normalized = withCtx((...args) => {
    if (false) ;
    return normalizeSlotValue(rawSlot(...args));
  }, ctx);
  normalized._c = false;
  return normalized;
};
const normalizeObjectSlots = (rawSlots, slots, instance) => {
  const ctx = rawSlots._ctx;
  for (const key in rawSlots) {
    if (isInternalKey(key)) continue;
    const value = rawSlots[key];
    if (isFunction(value)) {
      slots[key] = normalizeSlot$1(key, value, ctx);
    } else if (value != null) {
      const normalized = normalizeSlotValue(value);
      slots[key] = () => normalized;
    }
  }
};
const normalizeVNodeSlots = (instance, children) => {
  const normalized = normalizeSlotValue(children);
  instance.slots.default = () => normalized;
};
const assignSlots = (slots, children, optimized) => {
  for (const key in children) {
    if (optimized || !isInternalKey(key)) {
      slots[key] = children[key];
    }
  }
};
const initSlots = (instance, children, optimized) => {
  const slots = instance.slots = createInternalObject();
  if (instance.vnode.shapeFlag & 32) {
    const type = children._;
    if (type) {
      assignSlots(slots, children, optimized);
      if (optimized) {
        def(slots, "_", type, true);
      }
    } else {
      normalizeObjectSlots(children, slots);
    }
  } else if (children) {
    normalizeVNodeSlots(instance, children);
  }
};
const updateSlots = (instance, children, optimized) => {
  const { vnode, slots } = instance;
  let needDeletionCheck = true;
  let deletionComparisonTarget = EMPTY_OBJ;
  if (vnode.shapeFlag & 32) {
    const type = children._;
    if (type) {
      if (optimized && type === 1) {
        needDeletionCheck = false;
      } else {
        assignSlots(slots, children, optimized);
      }
    } else {
      needDeletionCheck = !children.$stable;
      normalizeObjectSlots(children, slots);
    }
    deletionComparisonTarget = children;
  } else if (children) {
    normalizeVNodeSlots(instance, children);
    deletionComparisonTarget = { default: 1 };
  }
  if (needDeletionCheck) {
    for (const key in slots) {
      if (!isInternalKey(key) && deletionComparisonTarget[key] == null) {
        delete slots[key];
      }
    }
  }
};
const queuePostRenderEffect = queueEffectWithSuspense;
function createRenderer(options) {
  return baseCreateRenderer(options);
}
function baseCreateRenderer(options, createHydrationFns) {
  const target = getGlobalThis();
  target.__VUE__ = true;
  const {
    insert: hostInsert,
    remove: hostRemove,
    patchProp: hostPatchProp,
    createElement: hostCreateElement,
    createText: hostCreateText,
    createComment: hostCreateComment,
    setText: hostSetText,
    setElementText: hostSetElementText,
    parentNode: hostParentNode,
    nextSibling: hostNextSibling,
    setScopeId: hostSetScopeId = NOOP,
    insertStaticContent: hostInsertStaticContent
  } = options;
  const patch = (n1, n2, container, anchor = null, parentComponent = null, parentSuspense = null, namespace = void 0, slotScopeIds = null, optimized = !!n2.dynamicChildren) => {
    if (n1 === n2) {
      return;
    }
    if (n1 && !isSameVNodeType(n1, n2)) {
      anchor = getNextHostNode(n1);
      unmount(n1, parentComponent, parentSuspense, true);
      n1 = null;
    }
    if (n2.patchFlag === -2) {
      optimized = false;
      n2.dynamicChildren = null;
    }
    const { type, ref: ref3, shapeFlag } = n2;
    switch (type) {
      case Text:
        processText(n1, n2, container, anchor);
        break;
      case Comment:
        processCommentNode(n1, n2, container, anchor);
        break;
      case Static:
        if (n1 == null) {
          mountStaticNode(n2, container, anchor, namespace);
        }
        break;
      case Fragment:
        processFragment(
          n1,
          n2,
          container,
          anchor,
          parentComponent,
          parentSuspense,
          namespace,
          slotScopeIds,
          optimized
        );
        break;
      default:
        if (shapeFlag & 1) {
          processElement(
            n1,
            n2,
            container,
            anchor,
            parentComponent,
            parentSuspense,
            namespace,
            slotScopeIds,
            optimized
          );
        } else if (shapeFlag & 6) {
          processComponent(
            n1,
            n2,
            container,
            anchor,
            parentComponent,
            parentSuspense,
            namespace,
            slotScopeIds,
            optimized
          );
        } else if (shapeFlag & 64) {
          type.process(
            n1,
            n2,
            container,
            anchor,
            parentComponent,
            parentSuspense,
            namespace,
            slotScopeIds,
            optimized,
            internals
          );
        } else if (shapeFlag & 128) {
          type.process(
            n1,
            n2,
            container,
            anchor,
            parentComponent,
            parentSuspense,
            namespace,
            slotScopeIds,
            optimized,
            internals
          );
        } else ;
    }
    if (ref3 != null && parentComponent) {
      setRef(ref3, n1 && n1.ref, parentSuspense, n2 || n1, !n2);
    } else if (ref3 == null && n1 && n1.ref != null) {
      setRef(n1.ref, null, parentSuspense, n1, true);
    }
  };
  const processText = (n1, n2, container, anchor) => {
    if (n1 == null) {
      hostInsert(
        n2.el = hostCreateText(n2.children),
        container,
        anchor
      );
    } else {
      const el = n2.el = n1.el;
      if (n2.children !== n1.children) {
        hostSetText(el, n2.children);
      }
    }
  };
  const processCommentNode = (n1, n2, container, anchor) => {
    if (n1 == null) {
      hostInsert(
        n2.el = hostCreateComment(n2.children || ""),
        container,
        anchor
      );
    } else {
      n2.el = n1.el;
    }
  };
  const mountStaticNode = (n2, container, anchor, namespace) => {
    [n2.el, n2.anchor] = hostInsertStaticContent(
      n2.children,
      container,
      anchor,
      namespace,
      n2.el,
      n2.anchor
    );
  };
  const moveStaticNode = ({ el, anchor }, container, nextSibling) => {
    let next;
    while (el && el !== anchor) {
      next = hostNextSibling(el);
      hostInsert(el, container, nextSibling);
      el = next;
    }
    hostInsert(anchor, container, nextSibling);
  };
  const removeStaticNode = ({ el, anchor }) => {
    let next;
    while (el && el !== anchor) {
      next = hostNextSibling(el);
      hostRemove(el);
      el = next;
    }
    hostRemove(anchor);
  };
  const processElement = (n1, n2, container, anchor, parentComponent, parentSuspense, namespace, slotScopeIds, optimized) => {
    if (n2.type === "svg") {
      namespace = "svg";
    } else if (n2.type === "math") {
      namespace = "mathml";
    }
    if (n1 == null) {
      mountElement(
        n2,
        container,
        anchor,
        parentComponent,
        parentSuspense,
        namespace,
        slotScopeIds,
        optimized
      );
    } else {
      const customElement = n1.el && n1.el._isVueCE ? n1.el : null;
      try {
        if (customElement) {
          customElement._beginPatch();
        }
        patchElement(
          n1,
          n2,
          parentComponent,
          parentSuspense,
          namespace,
          slotScopeIds,
          optimized
        );
      } finally {
        if (customElement) {
          customElement._endPatch();
        }
      }
    }
  };
  const mountElement = (vnode, container, anchor, parentComponent, parentSuspense, namespace, slotScopeIds, optimized) => {
    let el;
    let vnodeHook;
    const { props, shapeFlag, transition, dirs } = vnode;
    el = vnode.el = hostCreateElement(
      vnode.type,
      namespace,
      props && props.is,
      props
    );
    if (shapeFlag & 8) {
      hostSetElementText(el, vnode.children);
    } else if (shapeFlag & 16) {
      mountChildren(
        vnode.children,
        el,
        null,
        parentComponent,
        parentSuspense,
        resolveChildrenNamespace(vnode, namespace),
        slotScopeIds,
        optimized
      );
    }
    if (dirs) {
      invokeDirectiveHook(vnode, null, parentComponent, "created");
    }
    setScopeId(el, vnode, vnode.scopeId, slotScopeIds, parentComponent);
    if (props) {
      for (const key in props) {
        if (key !== "value" && !isReservedProp(key)) {
          hostPatchProp(el, key, null, props[key], namespace, parentComponent);
        }
      }
      if ("value" in props) {
        hostPatchProp(el, "value", null, props.value, namespace);
      }
      if (vnodeHook = props.onVnodeBeforeMount) {
        invokeVNodeHook(vnodeHook, parentComponent, vnode);
      }
    }
    if (dirs) {
      invokeDirectiveHook(vnode, null, parentComponent, "beforeMount");
    }
    const needCallTransitionHooks = needTransition(parentSuspense, transition);
    if (needCallTransitionHooks) {
      transition.beforeEnter(el);
    }
    hostInsert(el, container, anchor);
    if ((vnodeHook = props && props.onVnodeMounted) || needCallTransitionHooks || dirs) {
      queuePostRenderEffect(() => {
        try {
          vnodeHook && invokeVNodeHook(vnodeHook, parentComponent, vnode);
          needCallTransitionHooks && transition.enter(el);
          dirs && invokeDirectiveHook(vnode, null, parentComponent, "mounted");
        } finally {
        }
      }, parentSuspense);
    }
  };
  const setScopeId = (el, vnode, scopeId, slotScopeIds, parentComponent) => {
    if (scopeId) {
      hostSetScopeId(el, scopeId);
    }
    if (slotScopeIds) {
      for (let i = 0; i < slotScopeIds.length; i++) {
        hostSetScopeId(el, slotScopeIds[i]);
      }
    }
    if (parentComponent) {
      let subTree = parentComponent.subTree;
      if (vnode === subTree || isSuspense(subTree.type) && (subTree.ssContent === vnode || subTree.ssFallback === vnode)) {
        const parentVNode = parentComponent.vnode;
        setScopeId(
          el,
          parentVNode,
          parentVNode.scopeId,
          parentVNode.slotScopeIds,
          parentComponent.parent
        );
      }
    }
  };
  const mountChildren = (children, container, anchor, parentComponent, parentSuspense, namespace, slotScopeIds, optimized, start = 0) => {
    for (let i = start; i < children.length; i++) {
      const child = children[i] = optimized ? cloneIfMounted(children[i]) : normalizeVNode(children[i]);
      patch(
        null,
        child,
        container,
        anchor,
        parentComponent,
        parentSuspense,
        namespace,
        slotScopeIds,
        optimized
      );
    }
  };
  const patchElement = (n1, n2, parentComponent, parentSuspense, namespace, slotScopeIds, optimized) => {
    const el = n2.el = n1.el;
    let { patchFlag, dynamicChildren, dirs } = n2;
    patchFlag |= n1.patchFlag & 16;
    const oldProps = n1.props || EMPTY_OBJ;
    const newProps = n2.props || EMPTY_OBJ;
    let vnodeHook;
    parentComponent && toggleRecurse(parentComponent, false);
    if (vnodeHook = newProps.onVnodeBeforeUpdate) {
      invokeVNodeHook(vnodeHook, parentComponent, n2, n1);
    }
    if (dirs) {
      invokeDirectiveHook(n2, n1, parentComponent, "beforeUpdate");
    }
    parentComponent && toggleRecurse(parentComponent, true);
    if (
      // #6385 the old vnode may be a user-wrapped non-isomorphic block
      // Force full diff when block metadata is unstable.
      dynamicChildren && (!n1.dynamicChildren || n1.dynamicChildren.length !== dynamicChildren.length)
    ) {
      patchFlag = 0;
      optimized = false;
      dynamicChildren = null;
    }
    if (oldProps.innerHTML && newProps.innerHTML == null || oldProps.textContent && newProps.textContent == null) {
      hostSetElementText(el, "");
    }
    if (dynamicChildren) {
      patchBlockChildren(
        n1.dynamicChildren,
        dynamicChildren,
        el,
        parentComponent,
        parentSuspense,
        resolveChildrenNamespace(n2, namespace),
        slotScopeIds
      );
    } else if (!optimized) {
      patchChildren(
        n1,
        n2,
        el,
        null,
        parentComponent,
        parentSuspense,
        resolveChildrenNamespace(n2, namespace),
        slotScopeIds,
        false
      );
    }
    if (patchFlag > 0) {
      if (patchFlag & 16) {
        patchProps(el, oldProps, newProps, parentComponent, namespace);
      } else {
        if (patchFlag & 2) {
          if (oldProps.class !== newProps.class) {
            hostPatchProp(el, "class", null, newProps.class, namespace);
          }
        }
        if (patchFlag & 4) {
          hostPatchProp(el, "style", oldProps.style, newProps.style, namespace);
        }
        if (patchFlag & 8) {
          const propsToUpdate = n2.dynamicProps;
          for (let i = 0; i < propsToUpdate.length; i++) {
            const key = propsToUpdate[i];
            const prev = oldProps[key];
            const next = newProps[key];
            if (next !== prev || key === "value") {
              hostPatchProp(el, key, prev, next, namespace, parentComponent);
            }
          }
        }
      }
      if (patchFlag & 1) {
        if (n1.children !== n2.children) {
          hostSetElementText(el, n2.children);
        }
      }
    } else if (!optimized && dynamicChildren == null) {
      patchProps(el, oldProps, newProps, parentComponent, namespace);
    }
    if ((vnodeHook = newProps.onVnodeUpdated) || dirs) {
      queuePostRenderEffect(() => {
        vnodeHook && invokeVNodeHook(vnodeHook, parentComponent, n2, n1);
        dirs && invokeDirectiveHook(n2, n1, parentComponent, "updated");
      }, parentSuspense);
    }
  };
  const patchBlockChildren = (oldChildren, newChildren, fallbackContainer, parentComponent, parentSuspense, namespace, slotScopeIds) => {
    for (let i = 0; i < newChildren.length; i++) {
      const oldVNode = oldChildren[i];
      const newVNode = newChildren[i];
      const container = (
        // oldVNode may be an errored async setup() component inside Suspense
        // which will not have a mounted element
        oldVNode.el && // - In the case of a Fragment, we need to provide the actual parent
        // of the Fragment itself so it can move its children.
        (oldVNode.type === Fragment || // - In the case of different nodes, there is going to be a replacement
        // which also requires the correct parent container
        !isSameVNodeType(oldVNode, newVNode) || // - In the case of a component, it could contain anything.
        oldVNode.shapeFlag & (6 | 64 | 128)) ? hostParentNode(oldVNode.el) : (
          // In other cases, the parent container is not actually used so we
          // just pass the block element here to avoid a DOM parentNode call.
          fallbackContainer
        )
      );
      patch(
        oldVNode,
        newVNode,
        container,
        null,
        parentComponent,
        parentSuspense,
        namespace,
        slotScopeIds,
        true
      );
    }
  };
  const patchProps = (el, oldProps, newProps, parentComponent, namespace) => {
    if (oldProps !== newProps) {
      if (oldProps !== EMPTY_OBJ) {
        for (const key in oldProps) {
          if (!isReservedProp(key) && !(key in newProps)) {
            hostPatchProp(
              el,
              key,
              oldProps[key],
              null,
              namespace,
              parentComponent
            );
          }
        }
      }
      for (const key in newProps) {
        if (isReservedProp(key)) continue;
        const next = newProps[key];
        const prev = oldProps[key];
        if (next !== prev && key !== "value") {
          hostPatchProp(el, key, prev, next, namespace, parentComponent);
        }
      }
      if ("value" in newProps) {
        hostPatchProp(el, "value", oldProps.value, newProps.value, namespace);
      }
    }
  };
  const processFragment = (n1, n2, container, anchor, parentComponent, parentSuspense, namespace, slotScopeIds, optimized) => {
    const fragmentStartAnchor = n2.el = n1 ? n1.el : hostCreateText("");
    const fragmentEndAnchor = n2.anchor = n1 ? n1.anchor : hostCreateText("");
    let { patchFlag, dynamicChildren, slotScopeIds: fragmentSlotScopeIds } = n2;
    if (fragmentSlotScopeIds) {
      slotScopeIds = slotScopeIds ? slotScopeIds.concat(fragmentSlotScopeIds) : fragmentSlotScopeIds;
    }
    if (n1 == null) {
      hostInsert(fragmentStartAnchor, container, anchor);
      hostInsert(fragmentEndAnchor, container, anchor);
      mountChildren(
        // #10007
        // such fragment like `<></>` will be compiled into
        // a fragment which doesn't have a children.
        // In this case fallback to an empty array
        n2.children || [],
        container,
        fragmentEndAnchor,
        parentComponent,
        parentSuspense,
        namespace,
        slotScopeIds,
        optimized
      );
    } else {
      if (patchFlag > 0 && patchFlag & 64 && dynamicChildren && // #2715 the previous fragment could've been a BAILed one as a result
      // of renderSlot() with no valid children
      n1.dynamicChildren && n1.dynamicChildren.length === dynamicChildren.length) {
        patchBlockChildren(
          n1.dynamicChildren,
          dynamicChildren,
          container,
          parentComponent,
          parentSuspense,
          namespace,
          slotScopeIds
        );
        if (
          // #2080 if the stable fragment has a key, it's a <template v-for> that may
          //  get moved around. Make sure all root level vnodes inherit el.
          // #2134 or if it's a component root, it may also get moved around
          // as the component is being moved.
          n2.key != null || parentComponent && n2 === parentComponent.subTree
        ) {
          traverseStaticChildren(
            n1,
            n2,
            true
            /* shallow */
          );
        }
      } else {
        patchChildren(
          n1,
          n2,
          container,
          fragmentEndAnchor,
          parentComponent,
          parentSuspense,
          namespace,
          slotScopeIds,
          optimized
        );
      }
    }
  };
  const processComponent = (n1, n2, container, anchor, parentComponent, parentSuspense, namespace, slotScopeIds, optimized) => {
    n2.slotScopeIds = slotScopeIds;
    if (n1 == null) {
      if (n2.shapeFlag & 512) {
        parentComponent.ctx.activate(
          n2,
          container,
          anchor,
          namespace,
          optimized
        );
      } else {
        mountComponent(
          n2,
          container,
          anchor,
          parentComponent,
          parentSuspense,
          namespace,
          optimized
        );
      }
    } else {
      updateComponent(n1, n2, optimized);
    }
  };
  const mountComponent = (initialVNode, container, anchor, parentComponent, parentSuspense, namespace, optimized) => {
    const instance = initialVNode.component = createComponentInstance(
      initialVNode,
      parentComponent,
      parentSuspense
    );
    if (isKeepAlive(initialVNode)) {
      instance.ctx.renderer = internals;
    }
    {
      setupComponent(instance, false, optimized);
    }
    if (instance.asyncDep) {
      parentSuspense && parentSuspense.registerDep(instance, setupRenderEffect, optimized);
      if (!initialVNode.el) {
        const placeholder = instance.subTree = createVNode(Comment);
        processCommentNode(null, placeholder, container, anchor);
        initialVNode.placeholder = placeholder.el;
      }
    } else {
      setupRenderEffect(
        instance,
        initialVNode,
        container,
        anchor,
        parentSuspense,
        namespace,
        optimized
      );
    }
  };
  const updateComponent = (n1, n2, optimized) => {
    const instance = n2.component = n1.component;
    if (shouldUpdateComponent(n1, n2, optimized)) {
      if (instance.asyncDep && !instance.asyncResolved) {
        updateComponentPreRender(instance, n2, optimized);
        return;
      } else {
        instance.next = n2;
        instance.update();
      }
    } else {
      n2.el = n1.el;
      instance.vnode = n2;
    }
  };
  const setupRenderEffect = (instance, initialVNode, container, anchor, parentSuspense, namespace, optimized) => {
    const componentUpdateFn = () => {
      if (!instance.isMounted) {
        let vnodeHook;
        const { el, props } = initialVNode;
        const { bm, m, parent, root, type } = instance;
        const isAsyncWrapperVNode = isAsyncWrapper(initialVNode);
        toggleRecurse(instance, false);
        if (bm) {
          invokeArrayFns(bm);
        }
        if (!isAsyncWrapperVNode && (vnodeHook = props && props.onVnodeBeforeMount)) {
          invokeVNodeHook(vnodeHook, parent, initialVNode);
        }
        toggleRecurse(instance, true);
        {
          if (root.ce && root.ce._hasShadowRoot()) {
            root.ce._injectChildStyle(
              type,
              instance.parent ? instance.parent.type : void 0
            );
          }
          const subTree = instance.subTree = renderComponentRoot(instance);
          patch(
            null,
            subTree,
            container,
            anchor,
            instance,
            parentSuspense,
            namespace
          );
          initialVNode.el = subTree.el;
        }
        if (m) {
          queuePostRenderEffect(m, parentSuspense);
        }
        if (!isAsyncWrapperVNode && (vnodeHook = props && props.onVnodeMounted)) {
          const scopedInitialVNode = initialVNode;
          queuePostRenderEffect(
            () => invokeVNodeHook(vnodeHook, parent, scopedInitialVNode),
            parentSuspense
          );
        }
        if (initialVNode.shapeFlag & 256 || parent && isAsyncWrapper(parent.vnode) && parent.vnode.shapeFlag & 256) {
          instance.a && queuePostRenderEffect(instance.a, parentSuspense);
        }
        instance.isMounted = true;
        initialVNode = container = anchor = null;
      } else {
        let { next, bu, u, parent, vnode } = instance;
        {
          const nonHydratedAsyncRoot = locateNonHydratedAsyncRoot(instance);
          if (nonHydratedAsyncRoot) {
            if (next) {
              next.el = vnode.el;
              updateComponentPreRender(instance, next, optimized);
            }
            nonHydratedAsyncRoot.asyncDep.then(() => {
              queuePostRenderEffect(() => {
                if (!instance.isUnmounted) update();
              }, parentSuspense);
            });
            return;
          }
        }
        let originNext = next;
        let vnodeHook;
        toggleRecurse(instance, false);
        if (next) {
          next.el = vnode.el;
          updateComponentPreRender(instance, next, optimized);
        } else {
          next = vnode;
        }
        if (bu) {
          invokeArrayFns(bu);
        }
        if (vnodeHook = next.props && next.props.onVnodeBeforeUpdate) {
          invokeVNodeHook(vnodeHook, parent, next, vnode);
        }
        toggleRecurse(instance, true);
        const nextTree = renderComponentRoot(instance);
        const prevTree = instance.subTree;
        instance.subTree = nextTree;
        patch(
          prevTree,
          nextTree,
          // parent may have changed if it's in a teleport
          hostParentNode(prevTree.el),
          // anchor may have changed if it's in a fragment
          getNextHostNode(prevTree),
          instance,
          parentSuspense,
          namespace
        );
        next.el = nextTree.el;
        if (originNext === null) {
          updateHOCHostEl(instance, nextTree.el);
        }
        if (u) {
          queuePostRenderEffect(u, parentSuspense);
        }
        if (vnodeHook = next.props && next.props.onVnodeUpdated) {
          queuePostRenderEffect(
            () => invokeVNodeHook(vnodeHook, parent, next, vnode),
            parentSuspense
          );
        }
      }
    };
    instance.scope.on();
    const effect2 = instance.effect = new ReactiveEffect(componentUpdateFn);
    instance.scope.off();
    const update = instance.update = effect2.run.bind(effect2);
    const job = instance.job = effect2.runIfDirty.bind(effect2);
    job.i = instance;
    job.id = instance.uid;
    effect2.scheduler = () => queueJob(job);
    toggleRecurse(instance, true);
    update();
  };
  const updateComponentPreRender = (instance, nextVNode, optimized) => {
    nextVNode.component = instance;
    const prevProps = instance.vnode.props;
    instance.vnode = nextVNode;
    instance.next = null;
    updateProps(instance, nextVNode.props, prevProps, optimized);
    updateSlots(instance, nextVNode.children, optimized);
    pauseTracking();
    flushPreFlushCbs(instance);
    resetTracking();
  };
  const patchChildren = (n1, n2, container, anchor, parentComponent, parentSuspense, namespace, slotScopeIds, optimized = false) => {
    const c1 = n1 && n1.children;
    const prevShapeFlag = n1 ? n1.shapeFlag : 0;
    const c2 = n2.children;
    const { patchFlag, shapeFlag } = n2;
    if (patchFlag > 0) {
      if (patchFlag & 128) {
        patchKeyedChildren(
          c1,
          c2,
          container,
          anchor,
          parentComponent,
          parentSuspense,
          namespace,
          slotScopeIds,
          optimized
        );
        return;
      } else if (patchFlag & 256) {
        patchUnkeyedChildren(
          c1,
          c2,
          container,
          anchor,
          parentComponent,
          parentSuspense,
          namespace,
          slotScopeIds,
          optimized
        );
        return;
      }
    }
    if (shapeFlag & 8) {
      if (prevShapeFlag & 16) {
        unmountChildren(c1, parentComponent, parentSuspense);
      }
      if (c2 !== c1) {
        hostSetElementText(container, c2);
      }
    } else {
      if (prevShapeFlag & 16) {
        if (shapeFlag & 16) {
          patchKeyedChildren(
            c1,
            c2,
            container,
            anchor,
            parentComponent,
            parentSuspense,
            namespace,
            slotScopeIds,
            optimized
          );
        } else {
          unmountChildren(c1, parentComponent, parentSuspense, true);
        }
      } else {
        if (prevShapeFlag & 8) {
          hostSetElementText(container, "");
        }
        if (shapeFlag & 16) {
          mountChildren(
            c2,
            container,
            anchor,
            parentComponent,
            parentSuspense,
            namespace,
            slotScopeIds,
            optimized
          );
        }
      }
    }
  };
  const patchUnkeyedChildren = (c1, c2, container, anchor, parentComponent, parentSuspense, namespace, slotScopeIds, optimized) => {
    c1 = c1 || EMPTY_ARR;
    c2 = c2 || EMPTY_ARR;
    const oldLength = c1.length;
    const newLength = c2.length;
    const commonLength = Math.min(oldLength, newLength);
    let i;
    for (i = 0; i < commonLength; i++) {
      const nextChild = c2[i] = optimized ? cloneIfMounted(c2[i]) : normalizeVNode(c2[i]);
      patch(
        c1[i],
        nextChild,
        container,
        null,
        parentComponent,
        parentSuspense,
        namespace,
        slotScopeIds,
        optimized
      );
    }
    if (oldLength > newLength) {
      unmountChildren(
        c1,
        parentComponent,
        parentSuspense,
        true,
        false,
        commonLength
      );
    } else {
      mountChildren(
        c2,
        container,
        anchor,
        parentComponent,
        parentSuspense,
        namespace,
        slotScopeIds,
        optimized,
        commonLength
      );
    }
  };
  const patchKeyedChildren = (c1, c2, container, parentAnchor, parentComponent, parentSuspense, namespace, slotScopeIds, optimized) => {
    let i = 0;
    const l2 = c2.length;
    let e1 = c1.length - 1;
    let e2 = l2 - 1;
    while (i <= e1 && i <= e2) {
      const n1 = c1[i];
      const n2 = c2[i] = optimized ? cloneIfMounted(c2[i]) : normalizeVNode(c2[i]);
      if (isSameVNodeType(n1, n2)) {
        patch(
          n1,
          n2,
          container,
          null,
          parentComponent,
          parentSuspense,
          namespace,
          slotScopeIds,
          optimized
        );
      } else {
        break;
      }
      i++;
    }
    while (i <= e1 && i <= e2) {
      const n1 = c1[e1];
      const n2 = c2[e2] = optimized ? cloneIfMounted(c2[e2]) : normalizeVNode(c2[e2]);
      if (isSameVNodeType(n1, n2)) {
        patch(
          n1,
          n2,
          container,
          null,
          parentComponent,
          parentSuspense,
          namespace,
          slotScopeIds,
          optimized
        );
      } else {
        break;
      }
      e1--;
      e2--;
    }
    if (i > e1) {
      if (i <= e2) {
        const nextPos = e2 + 1;
        const anchor = nextPos < l2 ? c2[nextPos].el : parentAnchor;
        while (i <= e2) {
          patch(
            null,
            c2[i] = optimized ? cloneIfMounted(c2[i]) : normalizeVNode(c2[i]),
            container,
            anchor,
            parentComponent,
            parentSuspense,
            namespace,
            slotScopeIds,
            optimized
          );
          i++;
        }
      }
    } else if (i > e2) {
      while (i <= e1) {
        unmount(c1[i], parentComponent, parentSuspense, true);
        i++;
      }
    } else {
      const s1 = i;
      const s2 = i;
      const keyToNewIndexMap = /* @__PURE__ */ new Map();
      for (i = s2; i <= e2; i++) {
        const nextChild = c2[i] = optimized ? cloneIfMounted(c2[i]) : normalizeVNode(c2[i]);
        if (nextChild.key != null) {
          keyToNewIndexMap.set(nextChild.key, i);
        }
      }
      let j;
      let patched = 0;
      const toBePatched = e2 - s2 + 1;
      let moved = false;
      let maxNewIndexSoFar = 0;
      const newIndexToOldIndexMap = new Array(toBePatched);
      for (i = 0; i < toBePatched; i++) newIndexToOldIndexMap[i] = 0;
      for (i = s1; i <= e1; i++) {
        const prevChild = c1[i];
        if (patched >= toBePatched) {
          unmount(prevChild, parentComponent, parentSuspense, true);
          continue;
        }
        let newIndex;
        if (prevChild.key != null) {
          newIndex = keyToNewIndexMap.get(prevChild.key);
        } else {
          for (j = s2; j <= e2; j++) {
            if (newIndexToOldIndexMap[j - s2] === 0 && isSameVNodeType(prevChild, c2[j])) {
              newIndex = j;
              break;
            }
          }
        }
        if (newIndex === void 0) {
          unmount(prevChild, parentComponent, parentSuspense, true);
        } else {
          newIndexToOldIndexMap[newIndex - s2] = i + 1;
          if (newIndex >= maxNewIndexSoFar) {
            maxNewIndexSoFar = newIndex;
          } else {
            moved = true;
          }
          patch(
            prevChild,
            c2[newIndex],
            container,
            null,
            parentComponent,
            parentSuspense,
            namespace,
            slotScopeIds,
            optimized
          );
          patched++;
        }
      }
      const increasingNewIndexSequence = moved ? getSequence(newIndexToOldIndexMap) : EMPTY_ARR;
      j = increasingNewIndexSequence.length - 1;
      for (i = toBePatched - 1; i >= 0; i--) {
        const nextIndex = s2 + i;
        const nextChild = c2[nextIndex];
        const anchorVNode = c2[nextIndex + 1];
        const anchor = nextIndex + 1 < l2 ? (
          // #13559, #14173 fallback to el placeholder for unresolved async component
          anchorVNode.el || resolveAsyncComponentPlaceholder(anchorVNode)
        ) : parentAnchor;
        if (newIndexToOldIndexMap[i] === 0) {
          patch(
            null,
            nextChild,
            container,
            anchor,
            parentComponent,
            parentSuspense,
            namespace,
            slotScopeIds,
            optimized
          );
        } else if (moved) {
          if (j < 0 || i !== increasingNewIndexSequence[j]) {
            move(nextChild, container, anchor, 2);
          } else {
            j--;
          }
        }
      }
    }
  };
  const move = (vnode, container, anchor, moveType, parentSuspense = null) => {
    const { el, type, transition, children, shapeFlag } = vnode;
    if (shapeFlag & 6) {
      move(vnode.component.subTree, container, anchor, moveType);
      return;
    }
    if (shapeFlag & 128) {
      vnode.suspense.move(container, anchor, moveType);
      return;
    }
    if (shapeFlag & 64) {
      type.move(vnode, container, anchor, internals);
      return;
    }
    if (type === Fragment) {
      hostInsert(el, container, anchor);
      for (let i = 0; i < children.length; i++) {
        move(children[i], container, anchor, moveType);
      }
      hostInsert(vnode.anchor, container, anchor);
      return;
    }
    if (type === Static) {
      moveStaticNode(vnode, container, anchor);
      return;
    }
    const needTransition2 = moveType !== 2 && shapeFlag & 1 && transition;
    if (needTransition2) {
      if (moveType === 0) {
        if (transition.persisted && !el[leaveCbKey]) {
          hostInsert(el, container, anchor);
        } else {
          transition.beforeEnter(el);
          hostInsert(el, container, anchor);
          queuePostRenderEffect(() => transition.enter(el), parentSuspense);
        }
      } else {
        const { leave, delayLeave, afterLeave } = transition;
        const remove22 = () => {
          if (vnode.ctx.isUnmounted) {
            hostRemove(el);
          } else {
            hostInsert(el, container, anchor);
          }
        };
        const performLeave = () => {
          const wasLeaving = el._isLeaving || !!el[leaveCbKey];
          if (el._isLeaving) {
            el[leaveCbKey](
              true
              /* cancelled */
            );
          }
          if (transition.persisted && !wasLeaving) {
            remove22();
          } else {
            leave(el, () => {
              remove22();
              afterLeave && afterLeave();
            });
          }
        };
        if (delayLeave) {
          delayLeave(el, remove22, performLeave);
        } else {
          performLeave();
        }
      }
    } else {
      hostInsert(el, container, anchor);
    }
  };
  const unmount = (vnode, parentComponent, parentSuspense, doRemove = false, optimized = false) => {
    const {
      type,
      props,
      ref: ref3,
      children,
      dynamicChildren,
      shapeFlag,
      patchFlag,
      dirs,
      cacheIndex,
      memo
    } = vnode;
    if (patchFlag === -2) {
      optimized = false;
    }
    if (ref3 != null) {
      pauseTracking();
      setRef(ref3, null, parentSuspense, vnode, true);
      resetTracking();
    }
    if (cacheIndex != null) {
      parentComponent.renderCache[cacheIndex] = void 0;
    }
    if (shapeFlag & 256) {
      parentComponent.ctx.deactivate(vnode);
      return;
    }
    const shouldInvokeDirs = shapeFlag & 1 && dirs;
    const shouldInvokeVnodeHook = !isAsyncWrapper(vnode);
    let vnodeHook;
    if (shouldInvokeVnodeHook && (vnodeHook = props && props.onVnodeBeforeUnmount)) {
      invokeVNodeHook(vnodeHook, parentComponent, vnode);
    }
    if (shapeFlag & 6) {
      unmountComponent(vnode.component, parentSuspense, doRemove);
    } else {
      if (shapeFlag & 128) {
        vnode.suspense.unmount(parentSuspense, doRemove);
        return;
      }
      if (shouldInvokeDirs) {
        invokeDirectiveHook(vnode, null, parentComponent, "beforeUnmount");
      }
      if (shapeFlag & 64) {
        vnode.type.remove(
          vnode,
          parentComponent,
          parentSuspense,
          internals,
          doRemove
        );
      } else if (dynamicChildren && // #5154
      // when v-once is used inside a block, setBlockTracking(-1) marks the
      // parent block with hasOnce: true
      // so that it doesn't take the fast path during unmount - otherwise
      // components nested in v-once are never unmounted.
      !dynamicChildren.hasOnce && // #1153: fast path should not be taken for non-stable (v-for) fragments
      (type !== Fragment || patchFlag > 0 && patchFlag & 64)) {
        unmountChildren(
          dynamicChildren,
          parentComponent,
          parentSuspense,
          false,
          true
        );
      } else if (type === Fragment && patchFlag & (128 | 256) || !optimized && shapeFlag & 16) {
        unmountChildren(children, parentComponent, parentSuspense);
      }
      if (doRemove) {
        remove2(vnode);
      }
    }
    const shouldInvalidateMemo = memo != null && cacheIndex == null;
    if (shouldInvokeVnodeHook && (vnodeHook = props && props.onVnodeUnmounted) || shouldInvokeDirs || shouldInvalidateMemo) {
      queuePostRenderEffect(() => {
        vnodeHook && invokeVNodeHook(vnodeHook, parentComponent, vnode);
        shouldInvokeDirs && invokeDirectiveHook(vnode, null, parentComponent, "unmounted");
        if (shouldInvalidateMemo) {
          vnode.el = null;
        }
      }, parentSuspense);
    }
  };
  const remove2 = (vnode) => {
    const { type, el, anchor, transition } = vnode;
    if (type === Fragment) {
      {
        removeFragment(el, anchor);
      }
      return;
    }
    if (type === Static) {
      removeStaticNode(vnode);
      return;
    }
    const performRemove = () => {
      hostRemove(el);
      if (transition && !transition.persisted && transition.afterLeave) {
        transition.afterLeave();
      }
    };
    if (vnode.shapeFlag & 1 && transition && !transition.persisted) {
      const { leave, delayLeave } = transition;
      const performLeave = () => leave(el, performRemove);
      if (delayLeave) {
        delayLeave(vnode.el, performRemove, performLeave);
      } else {
        performLeave();
      }
    } else {
      performRemove();
    }
  };
  const removeFragment = (cur, end) => {
    let next;
    while (cur !== end) {
      next = hostNextSibling(cur);
      hostRemove(cur);
      cur = next;
    }
    hostRemove(end);
  };
  const unmountComponent = (instance, parentSuspense, doRemove) => {
    const { bum, scope, job, subTree, um, m, a } = instance;
    invalidateMount(m);
    invalidateMount(a);
    if (bum) {
      invokeArrayFns(bum);
    }
    scope.stop();
    if (job) {
      job.flags |= 8;
      unmount(subTree, instance, parentSuspense, doRemove);
    }
    if (um) {
      queuePostRenderEffect(um, parentSuspense);
    }
    queuePostRenderEffect(() => {
      instance.isUnmounted = true;
    }, parentSuspense);
  };
  const unmountChildren = (children, parentComponent, parentSuspense, doRemove = false, optimized = false, start = 0) => {
    for (let i = start; i < children.length; i++) {
      unmount(children[i], parentComponent, parentSuspense, doRemove, optimized);
    }
  };
  const getNextHostNode = (vnode) => {
    if (vnode.shapeFlag & 6) {
      return getNextHostNode(vnode.component.subTree);
    }
    if (vnode.shapeFlag & 128) {
      return vnode.suspense.next();
    }
    const el = hostNextSibling(vnode.anchor || vnode.el);
    const teleportEnd = el && el[TeleportEndKey];
    return teleportEnd ? hostNextSibling(teleportEnd) : el;
  };
  let isFlushing = false;
  const render = (vnode, container, namespace) => {
    let instance;
    if (vnode == null) {
      if (container._vnode) {
        unmount(container._vnode, null, null, true);
        instance = container._vnode.component;
      }
    } else {
      patch(
        container._vnode || null,
        vnode,
        container,
        null,
        null,
        null,
        namespace
      );
    }
    container._vnode = vnode;
    if (!isFlushing) {
      isFlushing = true;
      flushPreFlushCbs(instance);
      flushPostFlushCbs();
      isFlushing = false;
    }
  };
  const internals = {
    p: patch,
    um: unmount,
    m: move,
    r: remove2,
    mt: mountComponent,
    mc: mountChildren,
    pc: patchChildren,
    pbc: patchBlockChildren,
    n: getNextHostNode,
    o: options
  };
  let hydrate;
  return {
    render,
    hydrate,
    createApp: createAppAPI(render)
  };
}
function resolveChildrenNamespace({ type, props }, currentNamespace) {
  return currentNamespace === "svg" && type === "foreignObject" || currentNamespace === "mathml" && type === "annotation-xml" && props && props.encoding && props.encoding.includes("html") ? void 0 : currentNamespace;
}
function toggleRecurse({ effect: effect2, job }, allowed) {
  if (allowed) {
    effect2.flags |= 32;
    job.flags |= 4;
  } else {
    effect2.flags &= -33;
    job.flags &= -5;
  }
}
function needTransition(parentSuspense, transition) {
  return (!parentSuspense || parentSuspense && !parentSuspense.pendingBranch) && transition && !transition.persisted;
}
function traverseStaticChildren(n1, n2, shallow = false) {
  const ch1 = n1.children;
  const ch2 = n2.children;
  if (isArray$1(ch1) && isArray$1(ch2)) {
    for (let i = 0; i < ch1.length; i++) {
      const c1 = ch1[i];
      let c2 = ch2[i];
      if (c2.shapeFlag & 1 && !c2.dynamicChildren) {
        if (c2.patchFlag <= 0 || c2.patchFlag === 32) {
          c2 = ch2[i] = cloneIfMounted(ch2[i]);
          c2.el = c1.el;
        }
        if (!shallow && c2.patchFlag !== -2)
          traverseStaticChildren(c1, c2);
      }
      if (c2.type === Text) {
        if (c2.patchFlag === -1) {
          c2 = ch2[i] = cloneIfMounted(c2);
        }
        c2.el = c1.el;
      }
      if (c2.type === Comment && !c2.el) {
        c2.el = c1.el;
      }
    }
  }
}
function getSequence(arr) {
  const p2 = arr.slice();
  const result = [0];
  let i, j, u, v, c;
  const len = arr.length;
  for (i = 0; i < len; i++) {
    const arrI = arr[i];
    if (arrI !== 0) {
      j = result[result.length - 1];
      if (arr[j] < arrI) {
        p2[i] = j;
        result.push(i);
        continue;
      }
      u = 0;
      v = result.length - 1;
      while (u < v) {
        c = u + v >> 1;
        if (arr[result[c]] < arrI) {
          u = c + 1;
        } else {
          v = c;
        }
      }
      if (arrI < arr[result[u]]) {
        if (u > 0) {
          p2[i] = result[u - 1];
        }
        result[u] = i;
      }
    }
  }
  u = result.length;
  v = result[u - 1];
  while (u-- > 0) {
    result[u] = v;
    v = p2[v];
  }
  return result;
}
function locateNonHydratedAsyncRoot(instance) {
  const subComponent = instance.subTree.component;
  if (subComponent) {
    if (subComponent.asyncDep && !subComponent.asyncResolved) {
      return subComponent;
    } else {
      return locateNonHydratedAsyncRoot(subComponent);
    }
  }
}
function invalidateMount(hooks) {
  if (hooks) {
    for (let i = 0; i < hooks.length; i++)
      hooks[i].flags |= 8;
  }
}
function resolveAsyncComponentPlaceholder(anchorVnode) {
  if (anchorVnode.placeholder) {
    return anchorVnode.placeholder;
  }
  const instance = anchorVnode.component;
  if (instance) {
    return resolveAsyncComponentPlaceholder(instance.subTree);
  }
  return null;
}
const isSuspense = (type) => type.__isSuspense;
function queueEffectWithSuspense(fn, suspense) {
  if (suspense && suspense.pendingBranch) {
    if (isArray$1(fn)) {
      suspense.effects.push(...fn);
    } else {
      suspense.effects.push(fn);
    }
  } else {
    queuePostFlushCb(fn);
  }
}
const Fragment = /* @__PURE__ */ Symbol.for("v-fgt");
const Text = /* @__PURE__ */ Symbol.for("v-txt");
const Comment = /* @__PURE__ */ Symbol.for("v-cmt");
const Static = /* @__PURE__ */ Symbol.for("v-stc");
const blockStack = [];
let currentBlock = null;
function openBlock(disableTracking = false) {
  blockStack.push(currentBlock = disableTracking ? null : []);
}
function closeBlock() {
  blockStack.pop();
  currentBlock = blockStack[blockStack.length - 1] || null;
}
let isBlockTreeEnabled = 1;
function setBlockTracking(value, inVOnce = false) {
  isBlockTreeEnabled += value;
  if (value < 0 && currentBlock && inVOnce) {
    currentBlock.hasOnce = true;
  }
}
function setupBlock(vnode) {
  vnode.dynamicChildren = isBlockTreeEnabled > 0 ? currentBlock || EMPTY_ARR : null;
  closeBlock();
  if (isBlockTreeEnabled > 0 && currentBlock) {
    currentBlock.push(vnode);
  }
  return vnode;
}
function createElementBlock(type, props, children, patchFlag, dynamicProps, shapeFlag) {
  return setupBlock(
    createBaseVNode(
      type,
      props,
      children,
      patchFlag,
      dynamicProps,
      shapeFlag,
      true
    )
  );
}
function createBlock(type, props, children, patchFlag, dynamicProps) {
  return setupBlock(
    createVNode(
      type,
      props,
      children,
      patchFlag,
      dynamicProps,
      true
    )
  );
}
function isVNode(value) {
  return value ? value.__v_isVNode === true : false;
}
function isSameVNodeType(n1, n2) {
  return n1.type === n2.type && n1.key === n2.key;
}
const normalizeKey = ({ key }) => key != null ? key : null;
const normalizeRef = ({
  ref: ref3,
  ref_key,
  ref_for
}) => {
  if (typeof ref3 === "number") {
    ref3 = "" + ref3;
  }
  return ref3 != null ? isString(ref3) || /* @__PURE__ */ isRef(ref3) || isFunction(ref3) ? { i: currentRenderingInstance, r: ref3, k: ref_key, f: !!ref_for } : ref3 : null;
};
function createBaseVNode(type, props = null, children = null, patchFlag = 0, dynamicProps = null, shapeFlag = type === Fragment ? 0 : 1, isBlockNode = false, needFullChildrenNormalization = false) {
  const vnode = {
    __v_isVNode: true,
    __v_skip: true,
    type,
    props,
    key: props && normalizeKey(props),
    ref: props && normalizeRef(props),
    scopeId: currentScopeId,
    slotScopeIds: null,
    children,
    component: null,
    suspense: null,
    ssContent: null,
    ssFallback: null,
    dirs: null,
    transition: null,
    el: null,
    anchor: null,
    target: null,
    targetStart: null,
    targetAnchor: null,
    staticCount: 0,
    shapeFlag,
    patchFlag,
    dynamicProps,
    dynamicChildren: null,
    appContext: null,
    ctx: currentRenderingInstance
  };
  if (needFullChildrenNormalization) {
    normalizeChildren(vnode, children);
    if (shapeFlag & 128) {
      type.normalize(vnode);
    }
  } else if (children) {
    vnode.shapeFlag |= isString(children) ? 8 : 16;
  }
  if (isBlockTreeEnabled > 0 && // avoid a block node from tracking itself
  !isBlockNode && // has current parent block
  currentBlock && // presence of a patch flag indicates this node needs patching on updates.
  // component nodes also should always be patched, because even if the
  // component doesn't need to update, it needs to persist the instance on to
  // the next vnode so that it can be properly unmounted later.
  (vnode.patchFlag > 0 || shapeFlag & 6) && // the EVENTS flag is only for hydration and if it is the only flag, the
  // vnode should not be considered dynamic due to handler caching.
  vnode.patchFlag !== 32) {
    currentBlock.push(vnode);
  }
  return vnode;
}
const createVNode = _createVNode;
function _createVNode(type, props = null, children = null, patchFlag = 0, dynamicProps = null, isBlockNode = false) {
  if (!type || type === NULL_DYNAMIC_COMPONENT) {
    type = Comment;
  }
  if (isVNode(type)) {
    const cloned = cloneVNode(
      type,
      props,
      true
      /* mergeRef: true */
    );
    if (children) {
      normalizeChildren(cloned, children);
    }
    if (isBlockTreeEnabled > 0 && !isBlockNode && currentBlock) {
      if (cloned.shapeFlag & 6) {
        currentBlock[currentBlock.indexOf(type)] = cloned;
      } else {
        currentBlock.push(cloned);
      }
    }
    cloned.patchFlag = -2;
    return cloned;
  }
  if (isClassComponent(type)) {
    type = type.__vccOpts;
  }
  if (props) {
    props = guardReactiveProps(props);
    let { class: klass, style } = props;
    if (klass && !isString(klass)) {
      props.class = normalizeClass(klass);
    }
    if (isObject(style)) {
      if (/* @__PURE__ */ isProxy(style) && !isArray$1(style)) {
        style = extend({}, style);
      }
      props.style = normalizeStyle(style);
    }
  }
  const shapeFlag = isString(type) ? 1 : isSuspense(type) ? 128 : isTeleport(type) ? 64 : isObject(type) ? 4 : isFunction(type) ? 2 : 0;
  return createBaseVNode(
    type,
    props,
    children,
    patchFlag,
    dynamicProps,
    shapeFlag,
    isBlockNode,
    true
  );
}
function guardReactiveProps(props) {
  if (!props) return null;
  return /* @__PURE__ */ isProxy(props) || isInternalObject(props) ? extend({}, props) : props;
}
function cloneVNode(vnode, extraProps, mergeRef = false, cloneTransition = false) {
  const { props, ref: ref3, patchFlag, children, transition } = vnode;
  const mergedProps = extraProps ? mergeProps(props || {}, extraProps) : props;
  const cloned = {
    __v_isVNode: true,
    __v_skip: true,
    type: vnode.type,
    props: mergedProps,
    key: mergedProps && normalizeKey(mergedProps),
    ref: extraProps && extraProps.ref ? (
      // #2078 in the case of <component :is="vnode" ref="extra"/>
      // if the vnode itself already has a ref, cloneVNode will need to merge
      // the refs so the single vnode can be set on multiple refs
      mergeRef && ref3 ? isArray$1(ref3) ? ref3.concat(normalizeRef(extraProps)) : [ref3, normalizeRef(extraProps)] : normalizeRef(extraProps)
    ) : ref3,
    scopeId: vnode.scopeId,
    slotScopeIds: vnode.slotScopeIds,
    children,
    target: vnode.target,
    targetStart: vnode.targetStart,
    targetAnchor: vnode.targetAnchor,
    staticCount: vnode.staticCount,
    shapeFlag: vnode.shapeFlag,
    // if the vnode is cloned with extra props, we can no longer assume its
    // existing patch flag to be reliable and need to add the FULL_PROPS flag.
    // note: preserve flag for fragments since they use the flag for children
    // fast paths only.
    patchFlag: extraProps && vnode.type !== Fragment ? patchFlag === -1 ? 16 : patchFlag | 16 : patchFlag,
    dynamicProps: vnode.dynamicProps,
    dynamicChildren: vnode.dynamicChildren,
    appContext: vnode.appContext,
    dirs: vnode.dirs,
    transition,
    // These should technically only be non-null on mounted VNodes. However,
    // they *should* be copied for kept-alive vnodes. So we just always copy
    // them since them being non-null during a mount doesn't affect the logic as
    // they will simply be overwritten.
    component: vnode.component,
    suspense: vnode.suspense,
    ssContent: vnode.ssContent && cloneVNode(vnode.ssContent),
    ssFallback: vnode.ssFallback && cloneVNode(vnode.ssFallback),
    placeholder: vnode.placeholder,
    el: vnode.el,
    anchor: vnode.anchor,
    ctx: vnode.ctx,
    ce: vnode.ce
  };
  if (transition && cloneTransition) {
    setTransitionHooks(
      cloned,
      transition.clone(cloned)
    );
  }
  return cloned;
}
function createTextVNode(text = " ", flag = 0) {
  return createVNode(Text, null, text, flag);
}
function createStaticVNode(content, numberOfNodes) {
  const vnode = createVNode(Static, null, content);
  vnode.staticCount = numberOfNodes;
  return vnode;
}
function createCommentVNode(text = "", asBlock = false) {
  return asBlock ? (openBlock(), createBlock(Comment, null, text)) : createVNode(Comment, null, text);
}
function normalizeVNode(child) {
  if (child == null || typeof child === "boolean") {
    return createVNode(Comment);
  } else if (isArray$1(child)) {
    return createVNode(
      Fragment,
      null,
      // #3666, avoid reference pollution when reusing vnode
      child.slice()
    );
  } else if (isVNode(child)) {
    return cloneIfMounted(child);
  } else {
    return createVNode(Text, null, String(child));
  }
}
function cloneIfMounted(child) {
  return child.el === null && child.patchFlag !== -1 || child.memo ? child : cloneVNode(child);
}
function normalizeChildren(vnode, children) {
  let type = 0;
  const { shapeFlag } = vnode;
  if (children == null) {
    children = null;
  } else if (isArray$1(children)) {
    type = 16;
  } else if (typeof children === "object") {
    if (shapeFlag & (1 | 64)) {
      const slot = children.default;
      if (slot) {
        slot._c && (slot._d = false);
        normalizeChildren(vnode, slot());
        slot._c && (slot._d = true);
      }
      return;
    } else {
      type = 32;
      const slotFlag = children._;
      if (!slotFlag && !isInternalObject(children)) {
        children._ctx = currentRenderingInstance;
      } else if (slotFlag === 3 && currentRenderingInstance) {
        if (currentRenderingInstance.slots._ === 1) {
          children._ = 1;
        } else {
          children._ = 2;
          vnode.patchFlag |= 1024;
        }
      }
    }
  } else if (isFunction(children)) {
    if (shapeFlag & (1 | 64)) {
      normalizeChildren(vnode, { default: children });
      return;
    }
    children = { default: children, _ctx: currentRenderingInstance };
    type = 32;
  } else {
    children = String(children);
    if (shapeFlag & 64) {
      type = 16;
      children = [createTextVNode(children)];
    } else {
      type = 8;
    }
  }
  vnode.children = children;
  vnode.shapeFlag |= type;
}
function mergeProps(...args) {
  const ret = {};
  for (let i = 0; i < args.length; i++) {
    const toMerge = args[i];
    for (const key in toMerge) {
      if (key === "class") {
        if (ret.class !== toMerge.class) {
          ret.class = normalizeClass([ret.class, toMerge.class]);
        }
      } else if (key === "style") {
        ret.style = normalizeStyle([ret.style, toMerge.style]);
      } else if (isOn(key)) {
        const existing = ret[key];
        const incoming = toMerge[key];
        if (incoming && existing !== incoming && !(isArray$1(existing) && existing.includes(incoming))) {
          ret[key] = existing ? [].concat(existing, incoming) : incoming;
        } else if (incoming == null && existing == null && // mergeProps({ 'onUpdate:modelValue': undefined }) should not retain
        // the model listener.
        !isModelListener(key)) {
          ret[key] = incoming;
        }
      } else if (key !== "") {
        ret[key] = toMerge[key];
      }
    }
  }
  return ret;
}
function invokeVNodeHook(hook, instance, vnode, prevVNode = null) {
  callWithAsyncErrorHandling(hook, instance, 7, [
    vnode,
    prevVNode
  ]);
}
const emptyAppContext = createAppContext();
let uid = 0;
function createComponentInstance(vnode, parent, suspense) {
  const type = vnode.type;
  const appContext = (parent ? parent.appContext : vnode.appContext) || emptyAppContext;
  const instance = {
    uid: uid++,
    vnode,
    type,
    parent,
    appContext,
    root: null,
    // to be immediately set
    next: null,
    subTree: null,
    // will be set synchronously right after creation
    effect: null,
    update: null,
    // will be set synchronously right after creation
    job: null,
    scope: new EffectScope(
      true
      /* detached */
    ),
    render: null,
    proxy: null,
    exposed: null,
    exposeProxy: null,
    withProxy: null,
    provides: parent ? parent.provides : Object.create(appContext.provides),
    ids: parent ? parent.ids : ["", 0, 0],
    accessCache: null,
    renderCache: [],
    // local resolved assets
    components: null,
    directives: null,
    // resolved props and emits options
    propsOptions: normalizePropsOptions(type, appContext),
    emitsOptions: normalizeEmitsOptions(type, appContext),
    // emit
    emit: null,
    // to be set immediately
    emitted: null,
    // props default value
    propsDefaults: EMPTY_OBJ,
    // inheritAttrs
    inheritAttrs: type.inheritAttrs,
    // state
    ctx: EMPTY_OBJ,
    data: EMPTY_OBJ,
    props: EMPTY_OBJ,
    attrs: EMPTY_OBJ,
    slots: EMPTY_OBJ,
    refs: EMPTY_OBJ,
    setupState: EMPTY_OBJ,
    setupContext: null,
    // suspense related
    suspense,
    suspenseId: suspense ? suspense.pendingId : 0,
    asyncDep: null,
    asyncResolved: false,
    // lifecycle hooks
    // not using enums here because it results in computed properties
    isMounted: false,
    isUnmounted: false,
    isDeactivated: false,
    bc: null,
    c: null,
    bm: null,
    m: null,
    bu: null,
    u: null,
    um: null,
    bum: null,
    da: null,
    a: null,
    rtg: null,
    rtc: null,
    ec: null,
    sp: null
  };
  {
    instance.ctx = { _: instance };
  }
  instance.root = parent ? parent.root : instance;
  instance.emit = emit.bind(null, instance);
  if (vnode.ce) {
    vnode.ce(instance);
  }
  return instance;
}
let currentInstance = null;
const getCurrentInstance = () => currentInstance || currentRenderingInstance;
let internalSetCurrentInstance;
let setInSSRSetupState;
{
  const g = getGlobalThis();
  const registerGlobalSetter = (key, setter) => {
    let setters;
    if (!(setters = g[key])) setters = g[key] = [];
    setters.push(setter);
    return (v) => {
      if (setters.length > 1) setters.forEach((set) => set(v));
      else setters[0](v);
    };
  };
  internalSetCurrentInstance = registerGlobalSetter(
    `__VUE_INSTANCE_SETTERS__`,
    (v) => currentInstance = v
  );
  setInSSRSetupState = registerGlobalSetter(
    `__VUE_SSR_SETTERS__`,
    (v) => isInSSRComponentSetup = v
  );
}
const setCurrentInstance = (instance) => {
  const prev = currentInstance;
  internalSetCurrentInstance(instance);
  instance.scope.on();
  return () => {
    instance.scope.off();
    internalSetCurrentInstance(prev);
  };
};
const unsetCurrentInstance = () => {
  currentInstance && currentInstance.scope.off();
  internalSetCurrentInstance(null);
};
function isStatefulComponent(instance) {
  return instance.vnode.shapeFlag & 4;
}
let isInSSRComponentSetup = false;
function setupComponent(instance, isSSR = false, optimized = false) {
  isSSR && setInSSRSetupState(isSSR);
  const { props, children } = instance.vnode;
  const isStateful = isStatefulComponent(instance);
  initProps(instance, props, isStateful, isSSR);
  initSlots(instance, children, optimized || isSSR);
  const setupResult = isStateful ? setupStatefulComponent(instance, isSSR) : void 0;
  isSSR && setInSSRSetupState(false);
  return setupResult;
}
function setupStatefulComponent(instance, isSSR) {
  const Component = instance.type;
  instance.accessCache = /* @__PURE__ */ Object.create(null);
  instance.proxy = new Proxy(instance.ctx, PublicInstanceProxyHandlers);
  const { setup } = Component;
  if (setup) {
    pauseTracking();
    const setupContext = instance.setupContext = setup.length > 1 ? createSetupContext(instance) : null;
    const reset = setCurrentInstance(instance);
    const setupResult = callWithErrorHandling(
      setup,
      instance,
      0,
      [
        instance.props,
        setupContext
      ]
    );
    const isAsyncSetup = isPromise(setupResult);
    resetTracking();
    reset();
    if ((isAsyncSetup || instance.sp) && !isAsyncWrapper(instance)) {
      markAsyncBoundary(instance);
    }
    if (isAsyncSetup) {
      setupResult.then(unsetCurrentInstance, unsetCurrentInstance);
      if (isSSR) {
        return setupResult.then((resolvedResult) => {
          setInSSRSetupState(true);
          try {
            handleSetupResult(instance, resolvedResult, isSSR);
          } finally {
            setInSSRSetupState(false);
          }
        }).catch((e) => {
          handleError(e, instance, 0);
        });
      } else {
        instance.asyncDep = setupResult;
      }
    } else {
      handleSetupResult(instance, setupResult);
    }
  } else {
    finishComponentSetup(instance);
  }
}
function handleSetupResult(instance, setupResult, isSSR) {
  if (isFunction(setupResult)) {
    if (instance.type.__ssrInlineRender) {
      instance.ssrRender = setupResult;
    } else {
      instance.render = setupResult;
    }
  } else if (isObject(setupResult)) {
    instance.setupState = proxyRefs(setupResult);
  } else ;
  finishComponentSetup(instance);
}
function finishComponentSetup(instance, isSSR, skipOptions) {
  const Component = instance.type;
  if (!instance.render) {
    instance.render = Component.render || NOOP;
  }
  {
    const reset = setCurrentInstance(instance);
    pauseTracking();
    try {
      applyOptions(instance);
    } finally {
      resetTracking();
      reset();
    }
  }
}
const attrsProxyHandlers = {
  get(target, key) {
    track(target, "get", "");
    return target[key];
  }
};
function createSetupContext(instance) {
  const expose = (exposed) => {
    instance.exposed = exposed || {};
  };
  {
    return {
      attrs: new Proxy(instance.attrs, attrsProxyHandlers),
      slots: instance.slots,
      emit: instance.emit,
      expose
    };
  }
}
function getComponentPublicInstance(instance) {
  if (instance.exposed) {
    return instance.exposeProxy || (instance.exposeProxy = new Proxy(proxyRefs(markRaw(instance.exposed)), {
      get(target, key) {
        if (key in target) {
          return target[key];
        } else if (key in publicPropertiesMap) {
          return publicPropertiesMap[key](instance);
        }
      },
      has(target, key) {
        return key in target || key in publicPropertiesMap;
      }
    }));
  } else {
    return instance.proxy;
  }
}
const classifyRE = /(?:^|[-_])\w/g;
const classify = (str) => str.replace(classifyRE, (c) => c.toUpperCase()).replace(/[-_]/g, "");
function getComponentName(Component, includeInferred = true) {
  return isFunction(Component) ? Component.displayName || Component.name : Component.name || includeInferred && Component.__name;
}
function formatComponentName(instance, Component, isRoot = false) {
  let name = getComponentName(Component);
  if (!name && Component.__file) {
    const match = Component.__file.match(/([^/\\]+)\.\w+$/);
    if (match) {
      name = match[1];
    }
  }
  if (!name && instance) {
    const inferFromRegistry = (registry) => {
      for (const key in registry) {
        if (registry[key] === Component) {
          return key;
        }
      }
    };
    name = inferFromRegistry(instance.components) || instance.parent && inferFromRegistry(
      instance.parent.type.components
    ) || inferFromRegistry(instance.appContext.components);
  }
  return name ? classify(name) : isRoot ? `App` : `Anonymous`;
}
function isClassComponent(value) {
  return isFunction(value) && "__vccOpts" in value;
}
const computed = (getterOrOptions, debugOptions) => {
  const c = /* @__PURE__ */ computed$1(getterOrOptions, debugOptions, isInSSRComponentSetup);
  return c;
};
function h(type, propsOrChildren, children) {
  try {
    setBlockTracking(-1);
    const l = arguments.length;
    if (l === 2) {
      if (isObject(propsOrChildren) && !isArray$1(propsOrChildren)) {
        if (isVNode(propsOrChildren)) {
          return createVNode(type, null, [propsOrChildren]);
        }
        return createVNode(type, propsOrChildren);
      } else {
        return createVNode(type, null, propsOrChildren);
      }
    } else {
      if (l > 3) {
        children = Array.prototype.slice.call(arguments, 2);
      } else if (l === 3 && isVNode(children)) {
        children = [children];
      }
      return createVNode(type, propsOrChildren, children);
    }
  } finally {
    setBlockTracking(1);
  }
}
const version = "3.5.41";
/**
* @vue/runtime-dom v3.5.41
* (c) 2018-present Yuxi (Evan) You and Vue contributors
* @license MIT
**/
let policy = void 0;
const tt = typeof window !== "undefined" && window.trustedTypes;
if (tt) {
  try {
    policy = /* @__PURE__ */ tt.createPolicy("vue", {
      createHTML: (val) => val
    });
  } catch (e) {
  }
}
const unsafeToTrustedHTML = policy ? (val) => policy.createHTML(val) : (val) => val;
const svgNS = "http://www.w3.org/2000/svg";
const mathmlNS = "http://www.w3.org/1998/Math/MathML";
const doc = typeof document !== "undefined" ? document : null;
const templateContainer = doc && /* @__PURE__ */ doc.createElement("template");
const nodeOps = {
  insert: (child, parent, anchor) => {
    parent.insertBefore(child, anchor || null);
  },
  remove: (child) => {
    const parent = child.parentNode;
    if (parent) {
      parent.removeChild(child);
    }
  },
  createElement: (tag, namespace, is, props) => {
    const el = namespace === "svg" ? doc.createElementNS(svgNS, tag) : namespace === "mathml" ? doc.createElementNS(mathmlNS, tag) : is ? doc.createElement(tag, { is }) : doc.createElement(tag);
    if (tag === "select" && props && props.multiple != null) {
      el.setAttribute("multiple", props.multiple);
    }
    return el;
  },
  createText: (text) => doc.createTextNode(text),
  createComment: (text) => doc.createComment(text),
  setText: (node, text) => {
    node.nodeValue = text;
  },
  setElementText: (el, text) => {
    el.textContent = text;
  },
  parentNode: (node) => node.parentNode,
  nextSibling: (node) => node.nextSibling,
  querySelector: (selector) => doc.querySelector(selector),
  setScopeId(el, id) {
    el.setAttribute(id, "");
  },
  // __UNSAFE__
  // Reason: innerHTML.
  // Static content here can only come from compiled templates.
  // As long as the user only uses trusted templates, this is safe.
  insertStaticContent(content, parent, anchor, namespace, start, end) {
    const before = anchor ? anchor.previousSibling : parent.lastChild;
    if (start && (start === end || start.nextSibling)) {
      while (true) {
        parent.insertBefore(start.cloneNode(true), anchor);
        if (start === end || !(start = start.nextSibling)) break;
      }
    } else {
      templateContainer.innerHTML = unsafeToTrustedHTML(
        namespace === "svg" ? `<svg>${content}</svg>` : namespace === "mathml" ? `<math>${content}</math>` : content
      );
      const template = templateContainer.content;
      if (namespace === "svg" || namespace === "mathml") {
        const wrapper = template.firstChild;
        while (wrapper.firstChild) {
          template.appendChild(wrapper.firstChild);
        }
        template.removeChild(wrapper);
      }
      parent.insertBefore(template, anchor);
    }
    return [
      // first
      before ? before.nextSibling : parent.firstChild,
      // last
      anchor ? anchor.previousSibling : parent.lastChild
    ];
  }
};
const vtcKey = /* @__PURE__ */ Symbol("_vtc");
function patchClass(el, value, isSVG) {
  const transitionClasses = el[vtcKey];
  if (transitionClasses) {
    value = (value ? [value, ...transitionClasses] : [...transitionClasses]).join(" ");
  }
  if (value == null) {
    el.removeAttribute("class");
  } else if (isSVG) {
    el.setAttribute("class", value);
  } else {
    el.className = value;
  }
}
const vShowOriginalDisplay = /* @__PURE__ */ Symbol("_vod");
const vShowHidden = /* @__PURE__ */ Symbol("_vsh");
const vShow = {
  // used for prop mismatch check during hydration
  name: "show",
  beforeMount(el, { value }, { transition }) {
    el[vShowOriginalDisplay] = el.style.display === "none" ? "" : el.style.display;
    if (transition && value) {
      transition.beforeEnter(el);
    } else {
      setDisplay(el, value);
    }
  },
  mounted(el, { value }, { transition }) {
    if (transition && value) {
      transition.enter(el);
    }
  },
  updated(el, { value, oldValue }, { transition }) {
    if (!value === !oldValue) return;
    if (transition) {
      if (value) {
        transition.beforeEnter(el);
        setDisplay(el, true);
        transition.enter(el);
      } else {
        transition.leave(el, () => {
          setDisplay(el, false);
        });
      }
    } else {
      setDisplay(el, value);
    }
  },
  beforeUnmount(el, { value }) {
    setDisplay(el, value);
  }
};
function setDisplay(el, value) {
  el.style.display = value ? el[vShowOriginalDisplay] : "none";
  el[vShowHidden] = !value;
}
const CSS_VAR_TEXT = /* @__PURE__ */ Symbol("");
const displayRE = /(?:^|;)\s*display\s*:/;
function patchStyle(el, prev, next) {
  const style = el.style;
  const isCssString = isString(next);
  let hasControlledDisplay = false;
  if (next && !isCssString) {
    if (prev) {
      if (!isString(prev)) {
        for (const key in prev) {
          if (next[key] == null) {
            setStyle(style, key, "");
          }
        }
      } else {
        for (const prevStyle of prev.split(";")) {
          const key = prevStyle.slice(0, prevStyle.indexOf(":")).trim();
          if (next[key] == null) {
            setStyle(style, key, "");
          }
        }
      }
    }
    for (const key in next) {
      if (key === "display") {
        hasControlledDisplay = true;
      }
      const value = next[key];
      if (value != null) {
        if (!shouldPreserveTextareaResizeStyle(
          el,
          key,
          !isString(prev) && prev ? prev[key] : void 0,
          value
        )) {
          setStyle(style, key, value);
        }
      } else {
        setStyle(style, key, "");
      }
    }
  } else {
    if (isCssString) {
      if (prev !== next) {
        const cssVarText = style[CSS_VAR_TEXT];
        if (cssVarText) {
          next += ";" + cssVarText;
        }
        style.cssText = next;
        hasControlledDisplay = displayRE.test(next);
      }
    } else if (prev) {
      el.removeAttribute("style");
    }
  }
  if (vShowOriginalDisplay in el) {
    el[vShowOriginalDisplay] = hasControlledDisplay ? style.display : "";
    if (el[vShowHidden]) {
      style.display = "none";
    }
  }
}
const importantRE = /\s*!important$/;
function setStyle(style, name, val) {
  if (isArray$1(val)) {
    val.forEach((v) => setStyle(style, name, v));
  } else {
    if (val == null) val = "";
    if (name.startsWith("--")) {
      style.setProperty(name, val);
    } else {
      const prefixed = autoPrefix(style, name);
      if (importantRE.test(val)) {
        style.setProperty(
          hyphenate(prefixed),
          val.replace(importantRE, ""),
          "important"
        );
      } else {
        style[prefixed] = val;
      }
    }
  }
}
const prefixes = ["Webkit", "Moz", "ms"];
const prefixCache = {};
function autoPrefix(style, rawName) {
  const cached = prefixCache[rawName];
  if (cached) {
    return cached;
  }
  let name = camelize(rawName);
  if (name !== "filter" && name in style) {
    return prefixCache[rawName] = name;
  }
  name = capitalize(name);
  for (let i = 0; i < prefixes.length; i++) {
    const prefixed = prefixes[i] + name;
    if (prefixed in style) {
      return prefixCache[rawName] = prefixed;
    }
  }
  return rawName;
}
function shouldPreserveTextareaResizeStyle(el, key, prev, next) {
  return el.tagName === "TEXTAREA" && (key === "width" || key === "height") && isString(next) && prev === next;
}
const xlinkNS = "http://www.w3.org/1999/xlink";
function patchAttr(el, key, value, isSVG, instance, isBoolean = isSpecialBooleanAttr(key)) {
  if (isSVG && key.startsWith("xlink:")) {
    if (value == null) {
      el.removeAttributeNS(xlinkNS, key.slice(6, key.length));
    } else {
      el.setAttributeNS(xlinkNS, key, value);
    }
  } else {
    if (value == null || isBoolean && !includeBooleanAttr(value)) {
      el.removeAttribute(key);
    } else {
      el.setAttribute(
        key,
        isBoolean ? "" : isSymbol(value) ? String(value) : value
      );
    }
  }
}
function patchDOMProp(el, key, value, parentComponent, attrName) {
  if (key === "innerHTML" || key === "textContent") {
    if (value != null) {
      el[key] = key === "innerHTML" ? unsafeToTrustedHTML(value) : value;
    }
    return;
  }
  const tag = el.tagName;
  if (key === "value" && tag !== "PROGRESS" && // custom elements may use _value internally
  !tag.includes("-")) {
    const oldValue = tag === "OPTION" ? el.getAttribute("value") || "" : el.value;
    const newValue = value == null ? (
      // #11647: value should be set as empty string for null and undefined,
      // but <input type="checkbox"> should be set as 'on'.
      el.type === "checkbox" ? "on" : ""
    ) : String(value);
    if (oldValue !== newValue || !("_value" in el)) {
      el.value = newValue;
    }
    if (value == null) {
      el.removeAttribute(key);
    }
    el._value = value;
    return;
  }
  let needRemove = false;
  if (value === "" || value == null) {
    const type = typeof el[key];
    if (type === "boolean") {
      value = includeBooleanAttr(value);
    } else if (value == null && type === "string") {
      value = "";
      needRemove = true;
    } else if (type === "number") {
      value = 0;
      needRemove = true;
    }
  }
  try {
    el[key] = value;
  } catch (e) {
  }
  needRemove && el.removeAttribute(attrName || key);
}
function addEventListener(el, event, handler, options) {
  el.addEventListener(event, handler, options);
}
function removeEventListener(el, event, handler, options) {
  el.removeEventListener(event, handler, options);
}
const veiKey = /* @__PURE__ */ Symbol("_vei");
function patchEvent(el, rawName, prevValue, nextValue, instance = null) {
  const invokers = el[veiKey] || (el[veiKey] = {});
  const existingInvoker = invokers[rawName];
  if (nextValue && existingInvoker) {
    existingInvoker.value = nextValue;
  } else {
    const [name, options] = parseName(rawName);
    if (nextValue) {
      const invoker = invokers[rawName] = createInvoker(
        nextValue,
        instance
      );
      addEventListener(el, name, invoker, options);
    } else if (existingInvoker) {
      removeEventListener(el, name, existingInvoker, options);
      invokers[rawName] = void 0;
    }
  }
}
const optionsModifierRE = /(Once|Passive|Capture)$/;
const optionsModifierEventRE = /^on:?(?:Once|Passive|Capture)$/;
function parseName(name) {
  let options;
  let m;
  while ((m = name.match(optionsModifierRE)) && !optionsModifierEventRE.test(name)) {
    if (!options) options = {};
    name = name.slice(0, name.length - m[1].length);
    options[m[1].toLowerCase()] = true;
  }
  const event = name[2] === ":" ? name.slice(3) : hyphenate(name.slice(2));
  return [event, options];
}
let cachedNow = 0;
const p = /* @__PURE__ */ Promise.resolve();
const getNow = () => cachedNow || (p.then(() => cachedNow = 0), cachedNow = Date.now());
function createInvoker(initialValue, instance) {
  const invoker = (e) => {
    if (!e._vts) {
      e._vts = Date.now();
    } else if (e._vts <= invoker.attached) {
      return;
    }
    const value = invoker.value;
    if (isArray$1(value)) {
      const originalStop = e.stopImmediatePropagation;
      e.stopImmediatePropagation = () => {
        originalStop.call(e);
        e._stopped = true;
      };
      const handlers = value.slice();
      const args = [e];
      for (let i = 0; i < handlers.length; i++) {
        if (e._stopped) {
          break;
        }
        const handler = handlers[i];
        if (handler) {
          callWithAsyncErrorHandling(
            handler,
            instance,
            5,
            args
          );
        }
      }
    } else {
      callWithAsyncErrorHandling(
        value,
        instance,
        5,
        [e]
      );
    }
  };
  invoker.value = initialValue;
  invoker.attached = getNow();
  return invoker;
}
const isNativeOn = (key) => key.charCodeAt(0) === 111 && key.charCodeAt(1) === 110 && // lowercase letter
key.charCodeAt(2) > 96 && key.charCodeAt(2) < 123;
const patchProp = (el, key, prevValue, nextValue, namespace, parentComponent) => {
  const isSVG = namespace === "svg";
  if (key === "class") {
    patchClass(el, nextValue, isSVG);
  } else if (key === "style") {
    patchStyle(el, prevValue, nextValue);
  } else if (isOn(key)) {
    if (!isModelListener(key)) {
      patchEvent(el, key, prevValue, nextValue, parentComponent);
    }
  } else if (key[0] === "." ? (key = key.slice(1), true) : key[0] === "^" ? (key = key.slice(1), false) : shouldSetAsProp(el, key, nextValue, isSVG)) {
    patchDOMProp(el, key, nextValue);
    if (!el.tagName.includes("-") && (key === "value" || key === "checked" || key === "selected")) {
      patchAttr(el, key, nextValue, isSVG, parentComponent, key !== "value");
    }
  } else if (
    // #11081 force set props for possible async custom element
    el._isVueCE && // #12408 check if it's declared prop or it's async custom element
    (shouldSetAsPropForVueCE(el, key) || // @ts-expect-error _def is private
    el._def.__asyncLoader && (/[A-Z]/.test(key) || !isString(nextValue)))
  ) {
    patchDOMProp(el, camelize(key), nextValue, parentComponent, key);
  } else {
    if (key === "true-value") {
      el._trueValue = nextValue;
    } else if (key === "false-value") {
      el._falseValue = nextValue;
    }
    patchAttr(el, key, nextValue, isSVG);
  }
};
function shouldSetAsProp(el, key, value, isSVG) {
  if (isSVG) {
    if (key === "innerHTML" || key === "textContent") {
      return true;
    }
    if (key in el && isNativeOn(key) && isFunction(value)) {
      return true;
    }
    return false;
  }
  if (key === "spellcheck" || key === "draggable" || key === "translate" || key === "autocorrect") {
    return false;
  }
  if (key === "sandbox" && el.tagName === "IFRAME") {
    return false;
  }
  if (key === "form") {
    return false;
  }
  if (key === "list" && el.tagName === "INPUT") {
    return false;
  }
  if (key === "type" && el.tagName === "TEXTAREA") {
    return false;
  }
  if (key === "width" || key === "height") {
    const tag = el.tagName;
    if (tag === "IMG" || tag === "VIDEO" || tag === "CANVAS" || tag === "SOURCE") {
      return false;
    }
  }
  if (isNativeOn(key) && isString(value)) {
    return false;
  }
  return key in el;
}
function shouldSetAsPropForVueCE(el, key) {
  const props = (
    // @ts-expect-error _def is private
    el._def.props
  );
  if (!props) {
    return false;
  }
  const camelKey = camelize(key);
  return Array.isArray(props) ? props.some((prop) => camelize(prop) === camelKey) : Object.keys(props).some((prop) => camelize(prop) === camelKey);
}
const getModelAssigner = (vnode) => {
  const fn = vnode.props["onUpdate:modelValue"] || false;
  return isArray$1(fn) ? (value) => invokeArrayFns(fn, value) : fn;
};
function onCompositionStart(e) {
  e.target.composing = true;
}
function onCompositionEnd(e) {
  const target = e.target;
  if (target.composing) {
    target.composing = false;
    target.dispatchEvent(new Event("input"));
  }
}
const assignKey = /* @__PURE__ */ Symbol("_assign");
const initialValueKey = /* @__PURE__ */ Symbol("_initialValue");
function castValue(value, trim, number) {
  if (trim) value = value.trim();
  if (number) value = looseToNumber(value);
  return value;
}
const vModelText = {
  created(el, { modifiers: { lazy, trim, number } }, vnode) {
    if (el.parentNode) {
      if (el.type === "text") {
        el[initialValueKey] = el.defaultValue.replace(/[\r\n]/g, "");
      } else if (el.type === "textarea") {
        el[initialValueKey] = el.defaultValue.replace(/\r\n?/g, "\n");
      }
    }
    el[assignKey] = getModelAssigner(vnode);
    const castToNumber = number || vnode.props && vnode.props.type === "number";
    addEventListener(el, lazy ? "change" : "input", (e) => {
      if (e.target.composing) return;
      el[assignKey](castValue(el.value, trim, castToNumber));
    });
    if (trim || castToNumber) {
      addEventListener(el, "change", () => {
        el.value = castValue(el.value, trim, castToNumber);
      });
    }
    if (!lazy) {
      addEventListener(el, "compositionstart", onCompositionStart);
      addEventListener(el, "compositionend", onCompositionEnd);
      addEventListener(el, "change", onCompositionEnd);
    }
  },
  // set value on mounted so it's after min/max for type="range"
  mounted(el, { value, modifiers: { trim, number } }) {
    const newValue = value == null ? "" : value;
    const initialValue = el[initialValueKey];
    delete el[initialValueKey];
    if (initialValue !== void 0 && (el.type === "text" || el.type === "textarea") && el.value !== initialValue) {
      el[assignKey](castValue(el.value, trim, number));
    } else {
      el.value = newValue;
    }
  },
  beforeUpdate(el, { value, oldValue, modifiers: { lazy, trim, number } }, vnode) {
    el[assignKey] = getModelAssigner(vnode);
    if (el.composing) return;
    const elValue = (number || el.type === "number") && !/^0\d/.test(el.value) ? looseToNumber(el.value) : el.value;
    const newValue = value == null ? "" : value;
    if (elValue === newValue) {
      return;
    }
    const rootNode = el.getRootNode();
    if ((rootNode instanceof Document || rootNode instanceof ShadowRoot) && rootNode.activeElement === el && el.type !== "range") {
      if (lazy && value === oldValue) {
        return;
      }
      if (trim && el.value.trim() === newValue) {
        return;
      }
    }
    el.value = newValue;
  }
};
const vModelCheckbox = {
  // #4096 array checkboxes need to be deep traversed
  deep: true,
  created(el, _, vnode) {
    el[assignKey] = getModelAssigner(vnode);
    addEventListener(el, "change", () => {
      const modelValue = el._modelValue;
      const elementValue = getValue(el);
      const checked = el.checked;
      const assign2 = el[assignKey];
      if (isArray$1(modelValue)) {
        const index = looseIndexOf(modelValue, elementValue);
        const found = index !== -1;
        if (checked && !found) {
          assign2(modelValue.concat(elementValue));
        } else if (!checked && found) {
          const filtered = [...modelValue];
          filtered.splice(index, 1);
          assign2(filtered);
        }
      } else if (isSet(modelValue)) {
        const cloned = new Set(modelValue);
        if (checked) {
          cloned.add(elementValue);
        } else {
          cloned.delete(elementValue);
        }
        assign2(cloned);
      } else {
        assign2(getCheckboxValue(el, checked));
      }
    });
  },
  // set initial checked on mount to wait for true-value/false-value
  mounted: setChecked,
  beforeUpdate(el, binding, vnode) {
    el[assignKey] = getModelAssigner(vnode);
    setChecked(el, binding, vnode);
  }
};
function setChecked(el, { value, oldValue }, vnode) {
  el._modelValue = value;
  let checked;
  if (isArray$1(value)) {
    checked = looseIndexOf(value, vnode.props.value) > -1;
  } else if (isSet(value)) {
    checked = value.has(vnode.props.value);
  } else {
    if (value === oldValue) return;
    checked = looseEqual(value, getCheckboxValue(el, true));
  }
  if (el.checked !== checked) {
    el.checked = checked;
  }
}
const vModelSelect = {
  // <select multiple> value need to be deep traversed
  deep: true,
  created(el, { value, modifiers: { number } }, vnode) {
    el._modelValue = value;
    addEventListener(el, "change", () => {
      const selectedVal = Array.prototype.filter.call(el.options, (o) => o.selected).map(
        (o) => number ? looseToNumber(getValue(o)) : getValue(o)
      );
      el[assignKey](
        el.multiple ? isSet(el._modelValue) ? new Set(selectedVal) : selectedVal : selectedVal[0]
      );
      el._assigning = true;
      nextTick(() => {
        el._assigning = false;
      });
    });
    el[assignKey] = getModelAssigner(vnode);
  },
  // set value in mounted & updated because <select> relies on its children
  // <option>s.
  mounted(el, { value }) {
    setSelected(el, value);
  },
  beforeUpdate(el, { value }, vnode) {
    el._modelValue = value;
    el[assignKey] = getModelAssigner(vnode);
  },
  updated(el, { value }) {
    if (!el._assigning) {
      setSelected(el, value);
    }
  }
};
function setSelected(el, value) {
  const isMultiple = el.multiple;
  const isArrayValue = isArray$1(value);
  if (isMultiple && !isArrayValue && !isSet(value)) {
    return;
  }
  for (let i = 0, l = el.options.length; i < l; i++) {
    const option = el.options[i];
    const optionValue = getValue(option);
    if (isMultiple) {
      if (isArrayValue) {
        const optionType = typeof optionValue;
        if (optionType === "string" || optionType === "number") {
          option.selected = value.some((v) => String(v) === String(optionValue));
        } else {
          option.selected = looseIndexOf(value, optionValue) > -1;
        }
      } else {
        option.selected = value.has(optionValue);
      }
    } else if (looseEqual(getValue(option), value)) {
      if (el.selectedIndex !== i) el.selectedIndex = i;
      return;
    }
  }
  if (!isMultiple && el.selectedIndex !== -1) {
    el.selectedIndex = -1;
  }
}
function getValue(el) {
  return "_value" in el ? el._value : el.value;
}
function getCheckboxValue(el, checked) {
  const key = checked ? "_trueValue" : "_falseValue";
  return key in el ? el[key] : checked;
}
const systemModifiers = ["ctrl", "shift", "alt", "meta"];
const modifierGuards = {
  stop: (e) => e.stopPropagation(),
  prevent: (e) => e.preventDefault(),
  self: (e) => e.target !== e.currentTarget,
  ctrl: (e) => !e.ctrlKey,
  shift: (e) => !e.shiftKey,
  alt: (e) => !e.altKey,
  meta: (e) => !e.metaKey,
  left: (e) => "button" in e && e.button !== 0,
  middle: (e) => "button" in e && e.button !== 1,
  right: (e) => "button" in e && e.button !== 2,
  exact: (e, modifiers) => systemModifiers.some((m) => e[`${m}Key`] && !modifiers.includes(m))
};
const withModifiers = (fn, modifiers) => {
  if (!fn) return fn;
  const cache = fn._withMods || (fn._withMods = {});
  const cacheKey = modifiers.join(".");
  return cache[cacheKey] || (cache[cacheKey] = (event, ...args) => {
    for (let i = 0; i < modifiers.length; i++) {
      const guard = modifierGuards[modifiers[i]];
      if (guard && guard(event, modifiers)) return;
    }
    return fn(event, ...args);
  });
};
const keyNames = {
  esc: "escape",
  space: " ",
  up: "arrow-up",
  left: "arrow-left",
  right: "arrow-right",
  down: "arrow-down",
  delete: "backspace"
};
const withKeys = (fn, modifiers) => {
  const cache = fn._withKeys || (fn._withKeys = {});
  const cacheKey = modifiers.join(".");
  return cache[cacheKey] || (cache[cacheKey] = (event) => {
    if (!("key" in event)) {
      return;
    }
    const eventKey = hyphenate(event.key);
    if (modifiers.some(
      (k) => k === eventKey || keyNames[k] === eventKey
    )) {
      return fn(event);
    }
  });
};
const rendererOptions = /* @__PURE__ */ extend({ patchProp }, nodeOps);
let renderer;
function ensureRenderer() {
  return renderer || (renderer = createRenderer(rendererOptions));
}
const createApp = (...args) => {
  const app = ensureRenderer().createApp(...args);
  const { mount } = app;
  app.mount = (containerOrSelector) => {
    const container = normalizeContainer(containerOrSelector);
    if (!container) return;
    const component = app._component;
    if (!isFunction(component) && !component.render && !component.template) {
      component.template = container.innerHTML;
    }
    if (container.nodeType === 1) {
      container.textContent = "";
    }
    const proxy = mount(container, false, resolveRootNamespace(container));
    if (container instanceof Element) {
      container.removeAttribute("v-cloak");
      container.setAttribute("data-v-app", "");
    }
    return proxy;
  };
  return app;
};
function resolveRootNamespace(container) {
  if (container instanceof SVGElement) {
    return "svg";
  }
  if (typeof MathMLElement === "function" && container instanceof MathMLElement) {
    return "mathml";
  }
}
function normalizeContainer(container) {
  if (isString(container)) {
    const res = document.querySelector(container);
    return res;
  }
  return container;
}
/*!
 * vue-router v4.6.4
 * (c) 2025 Eduardo San Martin Morote
 * @license MIT
 */
const isBrowser = typeof document !== "undefined";
function isRouteComponent(component) {
  return typeof component === "object" || "displayName" in component || "props" in component || "__vccOpts" in component;
}
function isESModule(obj) {
  return obj.__esModule || obj[Symbol.toStringTag] === "Module" || obj.default && isRouteComponent(obj.default);
}
const assign = Object.assign;
function applyToParams(fn, params) {
  const newParams = {};
  for (const key in params) {
    const value = params[key];
    newParams[key] = isArray(value) ? value.map(fn) : fn(value);
  }
  return newParams;
}
const noop = () => {
};
const isArray = Array.isArray;
function mergeOptions(defaults, partialOptions) {
  const options = {};
  for (const key in defaults) options[key] = key in partialOptions ? partialOptions[key] : defaults[key];
  return options;
}
const HASH_RE = /#/g;
const AMPERSAND_RE = /&/g;
const SLASH_RE = /\//g;
const EQUAL_RE = /=/g;
const IM_RE = /\?/g;
const PLUS_RE = /\+/g;
const ENC_BRACKET_OPEN_RE = /%5B/g;
const ENC_BRACKET_CLOSE_RE = /%5D/g;
const ENC_CARET_RE = /%5E/g;
const ENC_BACKTICK_RE = /%60/g;
const ENC_CURLY_OPEN_RE = /%7B/g;
const ENC_PIPE_RE = /%7C/g;
const ENC_CURLY_CLOSE_RE = /%7D/g;
const ENC_SPACE_RE = /%20/g;
function commonEncode(text) {
  return text == null ? "" : encodeURI("" + text).replace(ENC_PIPE_RE, "|").replace(ENC_BRACKET_OPEN_RE, "[").replace(ENC_BRACKET_CLOSE_RE, "]");
}
function encodeHash(text) {
  return commonEncode(text).replace(ENC_CURLY_OPEN_RE, "{").replace(ENC_CURLY_CLOSE_RE, "}").replace(ENC_CARET_RE, "^");
}
function encodeQueryValue(text) {
  return commonEncode(text).replace(PLUS_RE, "%2B").replace(ENC_SPACE_RE, "+").replace(HASH_RE, "%23").replace(AMPERSAND_RE, "%26").replace(ENC_BACKTICK_RE, "`").replace(ENC_CURLY_OPEN_RE, "{").replace(ENC_CURLY_CLOSE_RE, "}").replace(ENC_CARET_RE, "^");
}
function encodeQueryKey(text) {
  return encodeQueryValue(text).replace(EQUAL_RE, "%3D");
}
function encodePath(text) {
  return commonEncode(text).replace(HASH_RE, "%23").replace(IM_RE, "%3F");
}
function encodeParam(text) {
  return encodePath(text).replace(SLASH_RE, "%2F");
}
function decode(text) {
  if (text == null) return null;
  try {
    return decodeURIComponent("" + text);
  } catch (err) {
  }
  return "" + text;
}
const TRAILING_SLASH_RE = /\/$/;
const removeTrailingSlash = (path) => path.replace(TRAILING_SLASH_RE, "");
function parseURL(parseQuery$1, location2, currentLocation = "/") {
  let path, query = {}, searchString = "", hash = "";
  const hashPos = location2.indexOf("#");
  let searchPos = location2.indexOf("?");
  searchPos = hashPos >= 0 && searchPos > hashPos ? -1 : searchPos;
  if (searchPos >= 0) {
    path = location2.slice(0, searchPos);
    searchString = location2.slice(searchPos, hashPos > 0 ? hashPos : location2.length);
    query = parseQuery$1(searchString.slice(1));
  }
  if (hashPos >= 0) {
    path = path || location2.slice(0, hashPos);
    hash = location2.slice(hashPos, location2.length);
  }
  path = resolveRelativePath(path != null ? path : location2, currentLocation);
  return {
    fullPath: path + searchString + hash,
    path,
    query,
    hash: decode(hash)
  };
}
function stringifyURL(stringifyQuery$1, location2) {
  const query = location2.query ? stringifyQuery$1(location2.query) : "";
  return location2.path + (query && "?") + query + (location2.hash || "");
}
function stripBase(pathname, base) {
  if (!base || !pathname.toLowerCase().startsWith(base.toLowerCase())) return pathname;
  return pathname.slice(base.length) || "/";
}
function isSameRouteLocation(stringifyQuery$1, a, b) {
  const aLastIndex = a.matched.length - 1;
  const bLastIndex = b.matched.length - 1;
  return aLastIndex > -1 && aLastIndex === bLastIndex && isSameRouteRecord(a.matched[aLastIndex], b.matched[bLastIndex]) && isSameRouteLocationParams(a.params, b.params) && stringifyQuery$1(a.query) === stringifyQuery$1(b.query) && a.hash === b.hash;
}
function isSameRouteRecord(a, b) {
  return (a.aliasOf || a) === (b.aliasOf || b);
}
function isSameRouteLocationParams(a, b) {
  if (Object.keys(a).length !== Object.keys(b).length) return false;
  for (var key in a) if (!isSameRouteLocationParamsValue(a[key], b[key])) return false;
  return true;
}
function isSameRouteLocationParamsValue(a, b) {
  return isArray(a) ? isEquivalentArray(a, b) : isArray(b) ? isEquivalentArray(b, a) : a?.valueOf() === b?.valueOf();
}
function isEquivalentArray(a, b) {
  return isArray(b) ? a.length === b.length && a.every((value, i) => value === b[i]) : a.length === 1 && a[0] === b;
}
function resolveRelativePath(to, from) {
  if (to.startsWith("/")) return to;
  if (!to) return from;
  const fromSegments = from.split("/");
  const toSegments = to.split("/");
  const lastToSegment = toSegments[toSegments.length - 1];
  if (lastToSegment === ".." || lastToSegment === ".") toSegments.push("");
  let position = fromSegments.length - 1;
  let toPosition;
  let segment;
  for (toPosition = 0; toPosition < toSegments.length; toPosition++) {
    segment = toSegments[toPosition];
    if (segment === ".") continue;
    if (segment === "..") {
      if (position > 1) position--;
    } else break;
  }
  return fromSegments.slice(0, position).join("/") + "/" + toSegments.slice(toPosition).join("/");
}
const START_LOCATION_NORMALIZED = {
  path: "/",
  name: void 0,
  params: {},
  query: {},
  hash: "",
  fullPath: "/",
  matched: [],
  meta: {},
  redirectedFrom: void 0
};
let NavigationType = /* @__PURE__ */ function(NavigationType$1) {
  NavigationType$1["pop"] = "pop";
  NavigationType$1["push"] = "push";
  return NavigationType$1;
}({});
let NavigationDirection = /* @__PURE__ */ function(NavigationDirection$1) {
  NavigationDirection$1["back"] = "back";
  NavigationDirection$1["forward"] = "forward";
  NavigationDirection$1["unknown"] = "";
  return NavigationDirection$1;
}({});
function normalizeBase(base) {
  if (!base) if (isBrowser) {
    const baseEl = document.querySelector("base");
    base = baseEl && baseEl.getAttribute("href") || "/";
    base = base.replace(/^\w+:\/\/[^\/]+/, "");
  } else base = "/";
  if (base[0] !== "/" && base[0] !== "#") base = "/" + base;
  return removeTrailingSlash(base);
}
const BEFORE_HASH_RE = /^[^#]+#/;
function createHref(base, location2) {
  return base.replace(BEFORE_HASH_RE, "#") + location2;
}
function getElementPosition(el, offset) {
  const docRect = document.documentElement.getBoundingClientRect();
  const elRect = el.getBoundingClientRect();
  return {
    behavior: offset.behavior,
    left: elRect.left - docRect.left - (offset.left || 0),
    top: elRect.top - docRect.top - (offset.top || 0)
  };
}
const computeScrollPosition = () => ({
  left: window.scrollX,
  top: window.scrollY
});
function scrollToPosition(position) {
  let scrollToOptions;
  if ("el" in position) {
    const positionEl = position.el;
    const isIdSelector = typeof positionEl === "string" && positionEl.startsWith("#");
    const el = typeof positionEl === "string" ? isIdSelector ? document.getElementById(positionEl.slice(1)) : document.querySelector(positionEl) : positionEl;
    if (!el) {
      return;
    }
    scrollToOptions = getElementPosition(el, position);
  } else scrollToOptions = position;
  if ("scrollBehavior" in document.documentElement.style) window.scrollTo(scrollToOptions);
  else window.scrollTo(scrollToOptions.left != null ? scrollToOptions.left : window.scrollX, scrollToOptions.top != null ? scrollToOptions.top : window.scrollY);
}
function getScrollKey(path, delta) {
  return (history.state ? history.state.position - delta : -1) + path;
}
const scrollPositions = /* @__PURE__ */ new Map();
function saveScrollPosition(key, scrollPosition) {
  scrollPositions.set(key, scrollPosition);
}
function getSavedScrollPosition(key) {
  const scroll = scrollPositions.get(key);
  scrollPositions.delete(key);
  return scroll;
}
function isRouteLocation(route) {
  return typeof route === "string" || route && typeof route === "object";
}
function isRouteName(name) {
  return typeof name === "string" || typeof name === "symbol";
}
let ErrorTypes = /* @__PURE__ */ function(ErrorTypes$1) {
  ErrorTypes$1[ErrorTypes$1["MATCHER_NOT_FOUND"] = 1] = "MATCHER_NOT_FOUND";
  ErrorTypes$1[ErrorTypes$1["NAVIGATION_GUARD_REDIRECT"] = 2] = "NAVIGATION_GUARD_REDIRECT";
  ErrorTypes$1[ErrorTypes$1["NAVIGATION_ABORTED"] = 4] = "NAVIGATION_ABORTED";
  ErrorTypes$1[ErrorTypes$1["NAVIGATION_CANCELLED"] = 8] = "NAVIGATION_CANCELLED";
  ErrorTypes$1[ErrorTypes$1["NAVIGATION_DUPLICATED"] = 16] = "NAVIGATION_DUPLICATED";
  return ErrorTypes$1;
}({});
const NavigationFailureSymbol = Symbol("");
({
  [ErrorTypes.MATCHER_NOT_FOUND]({ location: location2, currentLocation }) {
    return `No match for
 ${JSON.stringify(location2)}${currentLocation ? "\nwhile being at\n" + JSON.stringify(currentLocation) : ""}`;
  },
  [ErrorTypes.NAVIGATION_GUARD_REDIRECT]({ from, to }) {
    return `Redirected from "${from.fullPath}" to "${stringifyRoute(to)}" via a navigation guard.`;
  },
  [ErrorTypes.NAVIGATION_ABORTED]({ from, to }) {
    return `Navigation aborted from "${from.fullPath}" to "${to.fullPath}" via a navigation guard.`;
  },
  [ErrorTypes.NAVIGATION_CANCELLED]({ from, to }) {
    return `Navigation cancelled from "${from.fullPath}" to "${to.fullPath}" with a new navigation.`;
  },
  [ErrorTypes.NAVIGATION_DUPLICATED]({ from, to }) {
    return `Avoided redundant navigation to current location: "${from.fullPath}".`;
  }
});
function createRouterError(type, params) {
  return assign(/* @__PURE__ */ new Error(), {
    type,
    [NavigationFailureSymbol]: true
  }, params);
}
function isNavigationFailure(error, type) {
  return error instanceof Error && NavigationFailureSymbol in error && (type == null || !!(error.type & type));
}
const propertiesToLog = [
  "params",
  "query",
  "hash"
];
function stringifyRoute(to) {
  if (typeof to === "string") return to;
  if (to.path != null) return to.path;
  const location2 = {};
  for (const key of propertiesToLog) if (key in to) location2[key] = to[key];
  return JSON.stringify(location2, null, 2);
}
function parseQuery(search) {
  const query = {};
  if (search === "" || search === "?") return query;
  const searchParams = (search[0] === "?" ? search.slice(1) : search).split("&");
  for (let i = 0; i < searchParams.length; ++i) {
    const searchParam = searchParams[i].replace(PLUS_RE, " ");
    const eqPos = searchParam.indexOf("=");
    const key = decode(eqPos < 0 ? searchParam : searchParam.slice(0, eqPos));
    const value = eqPos < 0 ? null : decode(searchParam.slice(eqPos + 1));
    if (key in query) {
      let currentValue = query[key];
      if (!isArray(currentValue)) currentValue = query[key] = [currentValue];
      currentValue.push(value);
    } else query[key] = value;
  }
  return query;
}
function stringifyQuery(query) {
  let search = "";
  for (let key in query) {
    const value = query[key];
    key = encodeQueryKey(key);
    if (value == null) {
      if (value !== void 0) search += (search.length ? "&" : "") + key;
      continue;
    }
    (isArray(value) ? value.map((v) => v && encodeQueryValue(v)) : [value && encodeQueryValue(value)]).forEach((value$1) => {
      if (value$1 !== void 0) {
        search += (search.length ? "&" : "") + key;
        if (value$1 != null) search += "=" + value$1;
      }
    });
  }
  return search;
}
function normalizeQuery(query) {
  const normalizedQuery = {};
  for (const key in query) {
    const value = query[key];
    if (value !== void 0) normalizedQuery[key] = isArray(value) ? value.map((v) => v == null ? null : "" + v) : value == null ? value : "" + value;
  }
  return normalizedQuery;
}
const matchedRouteKey = Symbol("");
const viewDepthKey = Symbol("");
const routerKey = Symbol("");
const routeLocationKey = Symbol("");
const routerViewLocationKey = Symbol("");
function useCallbacks() {
  let handlers = [];
  function add(handler) {
    handlers.push(handler);
    return () => {
      const i = handlers.indexOf(handler);
      if (i > -1) handlers.splice(i, 1);
    };
  }
  function reset() {
    handlers = [];
  }
  return {
    add,
    list: () => handlers.slice(),
    reset
  };
}
function guardToPromiseFn(guard, to, from, record, name, runWithContext = (fn) => fn()) {
  const enterCallbackArray = record && (record.enterCallbacks[name] = record.enterCallbacks[name] || []);
  return () => new Promise((resolve2, reject) => {
    const next = (valid) => {
      if (valid === false) reject(createRouterError(ErrorTypes.NAVIGATION_ABORTED, {
        from,
        to
      }));
      else if (valid instanceof Error) reject(valid);
      else if (isRouteLocation(valid)) reject(createRouterError(ErrorTypes.NAVIGATION_GUARD_REDIRECT, {
        from: to,
        to: valid
      }));
      else {
        if (enterCallbackArray && record.enterCallbacks[name] === enterCallbackArray && typeof valid === "function") enterCallbackArray.push(valid);
        resolve2();
      }
    };
    const guardReturn = runWithContext(() => guard.call(record && record.instances[name], to, from, next));
    let guardCall = Promise.resolve(guardReturn);
    if (guard.length < 3) guardCall = guardCall.then(next);
    guardCall.catch((err) => reject(err));
  });
}
function extractComponentsGuards(matched, guardType, to, from, runWithContext = (fn) => fn()) {
  const guards = [];
  for (const record of matched) {
    for (const name in record.components) {
      let rawComponent = record.components[name];
      if (guardType !== "beforeRouteEnter" && !record.instances[name]) continue;
      if (isRouteComponent(rawComponent)) {
        const guard = (rawComponent.__vccOpts || rawComponent)[guardType];
        guard && guards.push(guardToPromiseFn(guard, to, from, record, name, runWithContext));
      } else {
        let componentPromise = rawComponent();
        guards.push(() => componentPromise.then((resolved) => {
          if (!resolved) throw new Error(`Couldn't resolve component "${name}" at "${record.path}"`);
          const resolvedComponent = isESModule(resolved) ? resolved.default : resolved;
          record.mods[name] = resolved;
          record.components[name] = resolvedComponent;
          const guard = (resolvedComponent.__vccOpts || resolvedComponent)[guardType];
          return guard && guardToPromiseFn(guard, to, from, record, name, runWithContext)();
        }));
      }
    }
  }
  return guards;
}
function extractChangingRecords(to, from) {
  const leavingRecords = [];
  const updatingRecords = [];
  const enteringRecords = [];
  const len = Math.max(from.matched.length, to.matched.length);
  for (let i = 0; i < len; i++) {
    const recordFrom = from.matched[i];
    if (recordFrom) if (to.matched.find((record) => isSameRouteRecord(record, recordFrom))) updatingRecords.push(recordFrom);
    else leavingRecords.push(recordFrom);
    const recordTo = to.matched[i];
    if (recordTo) {
      if (!from.matched.find((record) => isSameRouteRecord(record, recordTo))) enteringRecords.push(recordTo);
    }
  }
  return [
    leavingRecords,
    updatingRecords,
    enteringRecords
  ];
}
/*!
 * vue-router v4.6.4
 * (c) 2025 Eduardo San Martin Morote
 * @license MIT
 */
let createBaseLocation = () => location.protocol + "//" + location.host;
function createCurrentLocation(base, location$1) {
  const { pathname, search, hash } = location$1;
  const hashPos = base.indexOf("#");
  if (hashPos > -1) {
    let slicePos = hash.includes(base.slice(hashPos)) ? base.slice(hashPos).length : 1;
    let pathFromHash = hash.slice(slicePos);
    if (pathFromHash[0] !== "/") pathFromHash = "/" + pathFromHash;
    return stripBase(pathFromHash, "");
  }
  return stripBase(pathname, base) + search + hash;
}
function useHistoryListeners(base, historyState, currentLocation, replace) {
  let listeners = [];
  let teardowns = [];
  let pauseState = null;
  const popStateHandler = ({ state }) => {
    const to = createCurrentLocation(base, location);
    const from = currentLocation.value;
    const fromState = historyState.value;
    let delta = 0;
    if (state) {
      currentLocation.value = to;
      historyState.value = state;
      if (pauseState && pauseState === from) {
        pauseState = null;
        return;
      }
      delta = fromState ? state.position - fromState.position : 0;
    } else replace(to);
    listeners.forEach((listener) => {
      listener(currentLocation.value, from, {
        delta,
        type: NavigationType.pop,
        direction: delta ? delta > 0 ? NavigationDirection.forward : NavigationDirection.back : NavigationDirection.unknown
      });
    });
  };
  function pauseListeners() {
    pauseState = currentLocation.value;
  }
  function listen(callback) {
    listeners.push(callback);
    const teardown = () => {
      const index = listeners.indexOf(callback);
      if (index > -1) listeners.splice(index, 1);
    };
    teardowns.push(teardown);
    return teardown;
  }
  function beforeUnloadListener() {
    if (document.visibilityState === "hidden") {
      const { history: history$1 } = window;
      if (!history$1.state) return;
      history$1.replaceState(assign({}, history$1.state, { scroll: computeScrollPosition() }), "");
    }
  }
  function destroy() {
    for (const teardown of teardowns) teardown();
    teardowns = [];
    window.removeEventListener("popstate", popStateHandler);
    window.removeEventListener("pagehide", beforeUnloadListener);
    document.removeEventListener("visibilitychange", beforeUnloadListener);
  }
  window.addEventListener("popstate", popStateHandler);
  window.addEventListener("pagehide", beforeUnloadListener);
  document.addEventListener("visibilitychange", beforeUnloadListener);
  return {
    pauseListeners,
    listen,
    destroy
  };
}
function buildState(back, current, forward, replaced = false, computeScroll = false) {
  return {
    back,
    current,
    forward,
    replaced,
    position: window.history.length,
    scroll: computeScroll ? computeScrollPosition() : null
  };
}
function useHistoryStateNavigation(base) {
  const { history: history$1, location: location$1 } = window;
  const currentLocation = { value: createCurrentLocation(base, location$1) };
  const historyState = { value: history$1.state };
  if (!historyState.value) changeLocation(currentLocation.value, {
    back: null,
    current: currentLocation.value,
    forward: null,
    position: history$1.length - 1,
    replaced: true,
    scroll: null
  }, true);
  function changeLocation(to, state, replace$1) {
    const hashIndex = base.indexOf("#");
    const url = hashIndex > -1 ? (location$1.host && document.querySelector("base") ? base : base.slice(hashIndex)) + to : createBaseLocation() + base + to;
    try {
      history$1[replace$1 ? "replaceState" : "pushState"](state, "", url);
      historyState.value = state;
    } catch (err) {
      console.error(err);
      location$1[replace$1 ? "replace" : "assign"](url);
    }
  }
  function replace(to, data) {
    changeLocation(to, assign({}, history$1.state, buildState(historyState.value.back, to, historyState.value.forward, true), data, { position: historyState.value.position }), true);
    currentLocation.value = to;
  }
  function push(to, data) {
    const currentState = assign({}, historyState.value, history$1.state, {
      forward: to,
      scroll: computeScrollPosition()
    });
    changeLocation(currentState.current, currentState, true);
    changeLocation(to, assign({}, buildState(currentLocation.value, to, null), { position: currentState.position + 1 }, data), false);
    currentLocation.value = to;
  }
  return {
    location: currentLocation,
    state: historyState,
    push,
    replace
  };
}
function createWebHistory(base) {
  base = normalizeBase(base);
  const historyNavigation = useHistoryStateNavigation(base);
  const historyListeners = useHistoryListeners(base, historyNavigation.state, historyNavigation.location, historyNavigation.replace);
  function go(delta, triggerListeners = true) {
    if (!triggerListeners) historyListeners.pauseListeners();
    history.go(delta);
  }
  const routerHistory = assign({
    location: "",
    base,
    go,
    createHref: createHref.bind(null, base)
  }, historyNavigation, historyListeners);
  Object.defineProperty(routerHistory, "location", {
    enumerable: true,
    get: () => historyNavigation.location.value
  });
  Object.defineProperty(routerHistory, "state", {
    enumerable: true,
    get: () => historyNavigation.state.value
  });
  return routerHistory;
}
function createWebHashHistory(base) {
  base = location.host ? base || location.pathname + location.search : "";
  if (!base.includes("#")) base += "#";
  return createWebHistory(base);
}
let TokenType = /* @__PURE__ */ function(TokenType$1) {
  TokenType$1[TokenType$1["Static"] = 0] = "Static";
  TokenType$1[TokenType$1["Param"] = 1] = "Param";
  TokenType$1[TokenType$1["Group"] = 2] = "Group";
  return TokenType$1;
}({});
var TokenizerState = /* @__PURE__ */ function(TokenizerState$1) {
  TokenizerState$1[TokenizerState$1["Static"] = 0] = "Static";
  TokenizerState$1[TokenizerState$1["Param"] = 1] = "Param";
  TokenizerState$1[TokenizerState$1["ParamRegExp"] = 2] = "ParamRegExp";
  TokenizerState$1[TokenizerState$1["ParamRegExpEnd"] = 3] = "ParamRegExpEnd";
  TokenizerState$1[TokenizerState$1["EscapeNext"] = 4] = "EscapeNext";
  return TokenizerState$1;
}(TokenizerState || {});
const ROOT_TOKEN = {
  type: TokenType.Static,
  value: ""
};
const VALID_PARAM_RE = /[a-zA-Z0-9_]/;
function tokenizePath(path) {
  if (!path) return [[]];
  if (path === "/") return [[ROOT_TOKEN]];
  if (!path.startsWith("/")) throw new Error(`Invalid path "${path}"`);
  function crash(message) {
    throw new Error(`ERR (${state})/"${buffer}": ${message}`);
  }
  let state = TokenizerState.Static;
  let previousState = state;
  const tokens = [];
  let segment;
  function finalizeSegment() {
    if (segment) tokens.push(segment);
    segment = [];
  }
  let i = 0;
  let char;
  let buffer = "";
  let customRe = "";
  function consumeBuffer() {
    if (!buffer) return;
    if (state === TokenizerState.Static) segment.push({
      type: TokenType.Static,
      value: buffer
    });
    else if (state === TokenizerState.Param || state === TokenizerState.ParamRegExp || state === TokenizerState.ParamRegExpEnd) {
      if (segment.length > 1 && (char === "*" || char === "+")) crash(`A repeatable param (${buffer}) must be alone in its segment. eg: '/:ids+.`);
      segment.push({
        type: TokenType.Param,
        value: buffer,
        regexp: customRe,
        repeatable: char === "*" || char === "+",
        optional: char === "*" || char === "?"
      });
    } else crash("Invalid state to consume buffer");
    buffer = "";
  }
  function addCharToBuffer() {
    buffer += char;
  }
  while (i < path.length) {
    char = path[i++];
    if (char === "\\" && state !== TokenizerState.ParamRegExp) {
      previousState = state;
      state = TokenizerState.EscapeNext;
      continue;
    }
    switch (state) {
      case TokenizerState.Static:
        if (char === "/") {
          if (buffer) consumeBuffer();
          finalizeSegment();
        } else if (char === ":") {
          consumeBuffer();
          state = TokenizerState.Param;
        } else addCharToBuffer();
        break;
      case TokenizerState.EscapeNext:
        addCharToBuffer();
        state = previousState;
        break;
      case TokenizerState.Param:
        if (char === "(") state = TokenizerState.ParamRegExp;
        else if (VALID_PARAM_RE.test(char)) addCharToBuffer();
        else {
          consumeBuffer();
          state = TokenizerState.Static;
          if (char !== "*" && char !== "?" && char !== "+") i--;
        }
        break;
      case TokenizerState.ParamRegExp:
        if (char === ")") if (customRe[customRe.length - 1] == "\\") customRe = customRe.slice(0, -1) + char;
        else state = TokenizerState.ParamRegExpEnd;
        else customRe += char;
        break;
      case TokenizerState.ParamRegExpEnd:
        consumeBuffer();
        state = TokenizerState.Static;
        if (char !== "*" && char !== "?" && char !== "+") i--;
        customRe = "";
        break;
      default:
        crash("Unknown state");
        break;
    }
  }
  if (state === TokenizerState.ParamRegExp) crash(`Unfinished custom RegExp for param "${buffer}"`);
  consumeBuffer();
  finalizeSegment();
  return tokens;
}
const BASE_PARAM_PATTERN = "[^/]+?";
const BASE_PATH_PARSER_OPTIONS = {
  sensitive: false,
  strict: false,
  start: true,
  end: true
};
var PathScore = /* @__PURE__ */ function(PathScore$1) {
  PathScore$1[PathScore$1["_multiplier"] = 10] = "_multiplier";
  PathScore$1[PathScore$1["Root"] = 90] = "Root";
  PathScore$1[PathScore$1["Segment"] = 40] = "Segment";
  PathScore$1[PathScore$1["SubSegment"] = 30] = "SubSegment";
  PathScore$1[PathScore$1["Static"] = 40] = "Static";
  PathScore$1[PathScore$1["Dynamic"] = 20] = "Dynamic";
  PathScore$1[PathScore$1["BonusCustomRegExp"] = 10] = "BonusCustomRegExp";
  PathScore$1[PathScore$1["BonusWildcard"] = -50] = "BonusWildcard";
  PathScore$1[PathScore$1["BonusRepeatable"] = -20] = "BonusRepeatable";
  PathScore$1[PathScore$1["BonusOptional"] = -8] = "BonusOptional";
  PathScore$1[PathScore$1["BonusStrict"] = 0.7000000000000001] = "BonusStrict";
  PathScore$1[PathScore$1["BonusCaseSensitive"] = 0.25] = "BonusCaseSensitive";
  return PathScore$1;
}(PathScore || {});
const REGEX_CHARS_RE = /[.+*?^${}()[\]/\\]/g;
function tokensToParser(segments, extraOptions) {
  const options = assign({}, BASE_PATH_PARSER_OPTIONS, extraOptions);
  const score = [];
  let pattern = options.start ? "^" : "";
  const keys = [];
  for (const segment of segments) {
    const segmentScores = segment.length ? [] : [PathScore.Root];
    if (options.strict && !segment.length) pattern += "/";
    for (let tokenIndex = 0; tokenIndex < segment.length; tokenIndex++) {
      const token = segment[tokenIndex];
      let subSegmentScore = PathScore.Segment + (options.sensitive ? PathScore.BonusCaseSensitive : 0);
      if (token.type === TokenType.Static) {
        if (!tokenIndex) pattern += "/";
        pattern += token.value.replace(REGEX_CHARS_RE, "\\$&");
        subSegmentScore += PathScore.Static;
      } else if (token.type === TokenType.Param) {
        const { value, repeatable, optional, regexp } = token;
        keys.push({
          name: value,
          repeatable,
          optional
        });
        const re$1 = regexp ? regexp : BASE_PARAM_PATTERN;
        if (re$1 !== BASE_PARAM_PATTERN) {
          subSegmentScore += PathScore.BonusCustomRegExp;
          try {
            `${re$1}`;
          } catch (err) {
            throw new Error(`Invalid custom RegExp for param "${value}" (${re$1}): ` + err.message);
          }
        }
        let subPattern = repeatable ? `((?:${re$1})(?:/(?:${re$1}))*)` : `(${re$1})`;
        if (!tokenIndex) subPattern = optional && segment.length < 2 ? `(?:/${subPattern})` : "/" + subPattern;
        if (optional) subPattern += "?";
        pattern += subPattern;
        subSegmentScore += PathScore.Dynamic;
        if (optional) subSegmentScore += PathScore.BonusOptional;
        if (repeatable) subSegmentScore += PathScore.BonusRepeatable;
        if (re$1 === ".*") subSegmentScore += PathScore.BonusWildcard;
      }
      segmentScores.push(subSegmentScore);
    }
    score.push(segmentScores);
  }
  if (options.strict && options.end) {
    const i = score.length - 1;
    score[i][score[i].length - 1] += PathScore.BonusStrict;
  }
  if (!options.strict) pattern += "/?";
  if (options.end) pattern += "$";
  else if (options.strict && !pattern.endsWith("/")) pattern += "(?:/|$)";
  const re = new RegExp(pattern, options.sensitive ? "" : "i");
  function parse(path) {
    const match = path.match(re);
    const params = {};
    if (!match) return null;
    for (let i = 1; i < match.length; i++) {
      const value = match[i] || "";
      const key = keys[i - 1];
      params[key.name] = value && key.repeatable ? value.split("/") : value;
    }
    return params;
  }
  function stringify(params) {
    let path = "";
    let avoidDuplicatedSlash = false;
    for (const segment of segments) {
      if (!avoidDuplicatedSlash || !path.endsWith("/")) path += "/";
      avoidDuplicatedSlash = false;
      for (const token of segment) if (token.type === TokenType.Static) path += token.value;
      else if (token.type === TokenType.Param) {
        const { value, repeatable, optional } = token;
        const param = value in params ? params[value] : "";
        if (isArray(param) && !repeatable) throw new Error(`Provided param "${value}" is an array but it is not repeatable (* or + modifiers)`);
        const text = isArray(param) ? param.join("/") : param;
        if (!text) if (optional) {
          if (segment.length < 2) if (path.endsWith("/")) path = path.slice(0, -1);
          else avoidDuplicatedSlash = true;
        } else throw new Error(`Missing required param "${value}"`);
        path += text;
      }
    }
    return path || "/";
  }
  return {
    re,
    score,
    keys,
    parse,
    stringify
  };
}
function compareScoreArray(a, b) {
  let i = 0;
  while (i < a.length && i < b.length) {
    const diff = b[i] - a[i];
    if (diff) return diff;
    i++;
  }
  if (a.length < b.length) return a.length === 1 && a[0] === PathScore.Static + PathScore.Segment ? -1 : 1;
  else if (a.length > b.length) return b.length === 1 && b[0] === PathScore.Static + PathScore.Segment ? 1 : -1;
  return 0;
}
function comparePathParserScore(a, b) {
  let i = 0;
  const aScore = a.score;
  const bScore = b.score;
  while (i < aScore.length && i < bScore.length) {
    const comp = compareScoreArray(aScore[i], bScore[i]);
    if (comp) return comp;
    i++;
  }
  if (Math.abs(bScore.length - aScore.length) === 1) {
    if (isLastScoreNegative(aScore)) return 1;
    if (isLastScoreNegative(bScore)) return -1;
  }
  return bScore.length - aScore.length;
}
function isLastScoreNegative(score) {
  const last = score[score.length - 1];
  return score.length > 0 && last[last.length - 1] < 0;
}
const PATH_PARSER_OPTIONS_DEFAULTS = {
  strict: false,
  end: true,
  sensitive: false
};
function createRouteRecordMatcher(record, parent, options) {
  const parser = tokensToParser(tokenizePath(record.path), options);
  const matcher = assign(parser, {
    record,
    parent,
    children: [],
    alias: []
  });
  if (parent) {
    if (!matcher.record.aliasOf === !parent.record.aliasOf) parent.children.push(matcher);
  }
  return matcher;
}
function createRouterMatcher(routes, globalOptions) {
  const matchers = [];
  const matcherMap = /* @__PURE__ */ new Map();
  globalOptions = mergeOptions(PATH_PARSER_OPTIONS_DEFAULTS, globalOptions);
  function getRecordMatcher(name) {
    return matcherMap.get(name);
  }
  function addRoute(record, parent, originalRecord) {
    const isRootAdd = !originalRecord;
    const mainNormalizedRecord = normalizeRouteRecord(record);
    mainNormalizedRecord.aliasOf = originalRecord && originalRecord.record;
    const options = mergeOptions(globalOptions, record);
    const normalizedRecords = [mainNormalizedRecord];
    if ("alias" in record) {
      const aliases = typeof record.alias === "string" ? [record.alias] : record.alias;
      for (const alias of aliases) normalizedRecords.push(normalizeRouteRecord(assign({}, mainNormalizedRecord, {
        components: originalRecord ? originalRecord.record.components : mainNormalizedRecord.components,
        path: alias,
        aliasOf: originalRecord ? originalRecord.record : mainNormalizedRecord
      })));
    }
    let matcher;
    let originalMatcher;
    for (const normalizedRecord of normalizedRecords) {
      const { path } = normalizedRecord;
      if (parent && path[0] !== "/") {
        const parentPath = parent.record.path;
        const connectingSlash = parentPath[parentPath.length - 1] === "/" ? "" : "/";
        normalizedRecord.path = parent.record.path + (path && connectingSlash + path);
      }
      matcher = createRouteRecordMatcher(normalizedRecord, parent, options);
      if (originalRecord) {
        originalRecord.alias.push(matcher);
      } else {
        originalMatcher = originalMatcher || matcher;
        if (originalMatcher !== matcher) originalMatcher.alias.push(matcher);
        if (isRootAdd && record.name && !isAliasRecord(matcher)) {
          removeRoute(record.name);
        }
      }
      if (isMatchable(matcher)) insertMatcher(matcher);
      if (mainNormalizedRecord.children) {
        const children = mainNormalizedRecord.children;
        for (let i = 0; i < children.length; i++) addRoute(children[i], matcher, originalRecord && originalRecord.children[i]);
      }
      originalRecord = originalRecord || matcher;
    }
    return originalMatcher ? () => {
      removeRoute(originalMatcher);
    } : noop;
  }
  function removeRoute(matcherRef) {
    if (isRouteName(matcherRef)) {
      const matcher = matcherMap.get(matcherRef);
      if (matcher) {
        matcherMap.delete(matcherRef);
        matchers.splice(matchers.indexOf(matcher), 1);
        matcher.children.forEach(removeRoute);
        matcher.alias.forEach(removeRoute);
      }
    } else {
      const index = matchers.indexOf(matcherRef);
      if (index > -1) {
        matchers.splice(index, 1);
        if (matcherRef.record.name) matcherMap.delete(matcherRef.record.name);
        matcherRef.children.forEach(removeRoute);
        matcherRef.alias.forEach(removeRoute);
      }
    }
  }
  function getRoutes() {
    return matchers;
  }
  function insertMatcher(matcher) {
    const index = findInsertionIndex(matcher, matchers);
    matchers.splice(index, 0, matcher);
    if (matcher.record.name && !isAliasRecord(matcher)) matcherMap.set(matcher.record.name, matcher);
  }
  function resolve2(location$1, currentLocation) {
    let matcher;
    let params = {};
    let path;
    let name;
    if ("name" in location$1 && location$1.name) {
      matcher = matcherMap.get(location$1.name);
      if (!matcher) throw createRouterError(ErrorTypes.MATCHER_NOT_FOUND, { location: location$1 });
      name = matcher.record.name;
      params = assign(pickParams(currentLocation.params, matcher.keys.filter((k) => !k.optional).concat(matcher.parent ? matcher.parent.keys.filter((k) => k.optional) : []).map((k) => k.name)), location$1.params && pickParams(location$1.params, matcher.keys.map((k) => k.name)));
      path = matcher.stringify(params);
    } else if (location$1.path != null) {
      path = location$1.path;
      matcher = matchers.find((m) => m.re.test(path));
      if (matcher) {
        params = matcher.parse(path);
        name = matcher.record.name;
      }
    } else {
      matcher = currentLocation.name ? matcherMap.get(currentLocation.name) : matchers.find((m) => m.re.test(currentLocation.path));
      if (!matcher) throw createRouterError(ErrorTypes.MATCHER_NOT_FOUND, {
        location: location$1,
        currentLocation
      });
      name = matcher.record.name;
      params = assign({}, currentLocation.params, location$1.params);
      path = matcher.stringify(params);
    }
    const matched = [];
    let parentMatcher = matcher;
    while (parentMatcher) {
      matched.unshift(parentMatcher.record);
      parentMatcher = parentMatcher.parent;
    }
    return {
      name,
      path,
      params,
      matched,
      meta: mergeMetaFields(matched)
    };
  }
  routes.forEach((route) => addRoute(route));
  function clearRoutes() {
    matchers.length = 0;
    matcherMap.clear();
  }
  return {
    addRoute,
    resolve: resolve2,
    removeRoute,
    clearRoutes,
    getRoutes,
    getRecordMatcher
  };
}
function pickParams(params, keys) {
  const newParams = {};
  for (const key of keys) if (key in params) newParams[key] = params[key];
  return newParams;
}
function normalizeRouteRecord(record) {
  const normalized = {
    path: record.path,
    redirect: record.redirect,
    name: record.name,
    meta: record.meta || {},
    aliasOf: record.aliasOf,
    beforeEnter: record.beforeEnter,
    props: normalizeRecordProps(record),
    children: record.children || [],
    instances: {},
    leaveGuards: /* @__PURE__ */ new Set(),
    updateGuards: /* @__PURE__ */ new Set(),
    enterCallbacks: {},
    components: "components" in record ? record.components || null : record.component && { default: record.component }
  };
  Object.defineProperty(normalized, "mods", { value: {} });
  return normalized;
}
function normalizeRecordProps(record) {
  const propsObject = {};
  const props = record.props || false;
  if ("component" in record) propsObject.default = props;
  else for (const name in record.components) propsObject[name] = typeof props === "object" ? props[name] : props;
  return propsObject;
}
function isAliasRecord(record) {
  while (record) {
    if (record.record.aliasOf) return true;
    record = record.parent;
  }
  return false;
}
function mergeMetaFields(matched) {
  return matched.reduce((meta, record) => assign(meta, record.meta), {});
}
function findInsertionIndex(matcher, matchers) {
  let lower = 0;
  let upper = matchers.length;
  while (lower !== upper) {
    const mid = lower + upper >> 1;
    if (comparePathParserScore(matcher, matchers[mid]) < 0) upper = mid;
    else lower = mid + 1;
  }
  const insertionAncestor = getInsertionAncestor(matcher);
  if (insertionAncestor) {
    upper = matchers.lastIndexOf(insertionAncestor, upper - 1);
  }
  return upper;
}
function getInsertionAncestor(matcher) {
  let ancestor = matcher;
  while (ancestor = ancestor.parent) if (isMatchable(ancestor) && comparePathParserScore(matcher, ancestor) === 0) return ancestor;
}
function isMatchable({ record }) {
  return !!(record.name || record.components && Object.keys(record.components).length || record.redirect);
}
function useLink(props) {
  const router2 = inject(routerKey);
  const currentRoute = inject(routeLocationKey);
  const route = computed(() => {
    const to = unref(props.to);
    return router2.resolve(to);
  });
  const activeRecordIndex = computed(() => {
    const { matched } = route.value;
    const { length } = matched;
    const routeMatched = matched[length - 1];
    const currentMatched = currentRoute.matched;
    if (!routeMatched || !currentMatched.length) return -1;
    const index = currentMatched.findIndex(isSameRouteRecord.bind(null, routeMatched));
    if (index > -1) return index;
    const parentRecordPath = getOriginalPath(matched[length - 2]);
    return length > 1 && getOriginalPath(routeMatched) === parentRecordPath && currentMatched[currentMatched.length - 1].path !== parentRecordPath ? currentMatched.findIndex(isSameRouteRecord.bind(null, matched[length - 2])) : index;
  });
  const isActive = computed(() => activeRecordIndex.value > -1 && includesParams(currentRoute.params, route.value.params));
  const isExactActive = computed(() => activeRecordIndex.value > -1 && activeRecordIndex.value === currentRoute.matched.length - 1 && isSameRouteLocationParams(currentRoute.params, route.value.params));
  function navigate(e = {}) {
    if (guardEvent(e)) {
      const p2 = router2[unref(props.replace) ? "replace" : "push"](unref(props.to)).catch(noop);
      if (props.viewTransition && typeof document !== "undefined" && "startViewTransition" in document) document.startViewTransition(() => p2);
      return p2;
    }
    return Promise.resolve();
  }
  return {
    route,
    href: computed(() => route.value.href),
    isActive,
    isExactActive,
    navigate
  };
}
function preferSingleVNode(vnodes) {
  return vnodes.length === 1 ? vnodes[0] : vnodes;
}
const RouterLinkImpl = /* @__PURE__ */ defineComponent({
  name: "RouterLink",
  compatConfig: { MODE: 3 },
  props: {
    to: {
      type: [String, Object],
      required: true
    },
    replace: Boolean,
    activeClass: String,
    exactActiveClass: String,
    custom: Boolean,
    ariaCurrentValue: {
      type: String,
      default: "page"
    },
    viewTransition: Boolean
  },
  useLink,
  setup(props, { slots }) {
    const link = /* @__PURE__ */ reactive(useLink(props));
    const { options } = inject(routerKey);
    const elClass = computed(() => ({
      [getLinkClass(props.activeClass, options.linkActiveClass, "router-link-active")]: link.isActive,
      [getLinkClass(props.exactActiveClass, options.linkExactActiveClass, "router-link-exact-active")]: link.isExactActive
    }));
    return () => {
      const children = slots.default && preferSingleVNode(slots.default(link));
      return props.custom ? children : h("a", {
        "aria-current": link.isExactActive ? props.ariaCurrentValue : null,
        href: link.href,
        onClick: link.navigate,
        class: elClass.value
      }, children);
    };
  }
});
const RouterLink = RouterLinkImpl;
function guardEvent(e) {
  if (e.metaKey || e.altKey || e.ctrlKey || e.shiftKey) return;
  if (e.defaultPrevented) return;
  if (e.button !== void 0 && e.button !== 0) return;
  if (e.currentTarget && e.currentTarget.getAttribute) {
    const target = e.currentTarget.getAttribute("target");
    if (/\b_blank\b/i.test(target)) return;
  }
  if (e.preventDefault) e.preventDefault();
  return true;
}
function includesParams(outer, inner) {
  for (const key in inner) {
    const innerValue = inner[key];
    const outerValue = outer[key];
    if (typeof innerValue === "string") {
      if (innerValue !== outerValue) return false;
    } else if (!isArray(outerValue) || outerValue.length !== innerValue.length || innerValue.some((value, i) => value.valueOf() !== outerValue[i].valueOf())) return false;
  }
  return true;
}
function getOriginalPath(record) {
  return record ? record.aliasOf ? record.aliasOf.path : record.path : "";
}
const getLinkClass = (propClass, globalClass, defaultClass) => propClass != null ? propClass : globalClass != null ? globalClass : defaultClass;
const RouterViewImpl = /* @__PURE__ */ defineComponent({
  name: "RouterView",
  inheritAttrs: false,
  props: {
    name: {
      type: String,
      default: "default"
    },
    route: Object
  },
  compatConfig: { MODE: 3 },
  setup(props, { attrs, slots }) {
    const injectedRoute = inject(routerViewLocationKey);
    const routeToDisplay = computed(() => props.route || injectedRoute.value);
    const injectedDepth = inject(viewDepthKey, 0);
    const depth = computed(() => {
      let initialDepth = unref(injectedDepth);
      const { matched } = routeToDisplay.value;
      let matchedRoute;
      while ((matchedRoute = matched[initialDepth]) && !matchedRoute.components) initialDepth++;
      return initialDepth;
    });
    const matchedRouteRef = computed(() => routeToDisplay.value.matched[depth.value]);
    provide(viewDepthKey, computed(() => depth.value + 1));
    provide(matchedRouteKey, matchedRouteRef);
    provide(routerViewLocationKey, routeToDisplay);
    const viewRef = /* @__PURE__ */ ref();
    watch(() => [
      viewRef.value,
      matchedRouteRef.value,
      props.name
    ], ([instance, to, name], [oldInstance, from, oldName]) => {
      if (to) {
        to.instances[name] = instance;
        if (from && from !== to && instance && instance === oldInstance) {
          if (!to.leaveGuards.size) to.leaveGuards = from.leaveGuards;
          if (!to.updateGuards.size) to.updateGuards = from.updateGuards;
        }
      }
      if (instance && to && (!from || !isSameRouteRecord(to, from) || !oldInstance)) (to.enterCallbacks[name] || []).forEach((callback) => callback(instance));
    }, { flush: "post" });
    return () => {
      const route = routeToDisplay.value;
      const currentName = props.name;
      const matchedRoute = matchedRouteRef.value;
      const ViewComponent = matchedRoute && matchedRoute.components[currentName];
      if (!ViewComponent) return normalizeSlot(slots.default, {
        Component: ViewComponent,
        route
      });
      const routePropsOption = matchedRoute.props[currentName];
      const routeProps = routePropsOption ? routePropsOption === true ? route.params : typeof routePropsOption === "function" ? routePropsOption(route) : routePropsOption : null;
      const onVnodeUnmounted = (vnode) => {
        if (vnode.component.isUnmounted) matchedRoute.instances[currentName] = null;
      };
      const component = h(ViewComponent, assign({}, routeProps, attrs, {
        onVnodeUnmounted,
        ref: viewRef
      }));
      return normalizeSlot(slots.default, {
        Component: component,
        route
      }) || component;
    };
  }
});
function normalizeSlot(slot, data) {
  if (!slot) return null;
  const slotContent = slot(data);
  return slotContent.length === 1 ? slotContent[0] : slotContent;
}
const RouterView = RouterViewImpl;
function createRouter(options) {
  const matcher = createRouterMatcher(options.routes, options);
  const parseQuery$1 = options.parseQuery || parseQuery;
  const stringifyQuery$1 = options.stringifyQuery || stringifyQuery;
  const routerHistory = options.history;
  const beforeGuards = useCallbacks();
  const beforeResolveGuards = useCallbacks();
  const afterGuards = useCallbacks();
  const currentRoute = /* @__PURE__ */ shallowRef(START_LOCATION_NORMALIZED);
  let pendingLocation = START_LOCATION_NORMALIZED;
  if (isBrowser && options.scrollBehavior && "scrollRestoration" in history) history.scrollRestoration = "manual";
  const normalizeParams = applyToParams.bind(null, (paramValue) => "" + paramValue);
  const encodeParams = applyToParams.bind(null, encodeParam);
  const decodeParams = applyToParams.bind(null, decode);
  function addRoute(parentOrRoute, route) {
    let parent;
    let record;
    if (isRouteName(parentOrRoute)) {
      parent = matcher.getRecordMatcher(parentOrRoute);
      record = route;
    } else record = parentOrRoute;
    return matcher.addRoute(record, parent);
  }
  function removeRoute(name) {
    const recordMatcher = matcher.getRecordMatcher(name);
    if (recordMatcher) matcher.removeRoute(recordMatcher);
  }
  function getRoutes() {
    return matcher.getRoutes().map((routeMatcher) => routeMatcher.record);
  }
  function hasRoute(name) {
    return !!matcher.getRecordMatcher(name);
  }
  function resolve2(rawLocation, currentLocation) {
    currentLocation = assign({}, currentLocation || currentRoute.value);
    if (typeof rawLocation === "string") {
      const locationNormalized = parseURL(parseQuery$1, rawLocation, currentLocation.path);
      const matchedRoute$1 = matcher.resolve({ path: locationNormalized.path }, currentLocation);
      const href$1 = routerHistory.createHref(locationNormalized.fullPath);
      return assign(locationNormalized, matchedRoute$1, {
        params: decodeParams(matchedRoute$1.params),
        hash: decode(locationNormalized.hash),
        redirectedFrom: void 0,
        href: href$1
      });
    }
    let matcherLocation;
    if (rawLocation.path != null) {
      matcherLocation = assign({}, rawLocation, { path: parseURL(parseQuery$1, rawLocation.path, currentLocation.path).path });
    } else {
      const targetParams = assign({}, rawLocation.params);
      for (const key in targetParams) if (targetParams[key] == null) delete targetParams[key];
      matcherLocation = assign({}, rawLocation, { params: encodeParams(targetParams) });
      currentLocation.params = encodeParams(currentLocation.params);
    }
    const matchedRoute = matcher.resolve(matcherLocation, currentLocation);
    const hash = rawLocation.hash || "";
    matchedRoute.params = normalizeParams(decodeParams(matchedRoute.params));
    const fullPath = stringifyURL(stringifyQuery$1, assign({}, rawLocation, {
      hash: encodeHash(hash),
      path: matchedRoute.path
    }));
    const href = routerHistory.createHref(fullPath);
    return assign({
      fullPath,
      hash,
      query: stringifyQuery$1 === stringifyQuery ? normalizeQuery(rawLocation.query) : rawLocation.query || {}
    }, matchedRoute, {
      redirectedFrom: void 0,
      href
    });
  }
  function locationAsObject(to) {
    return typeof to === "string" ? parseURL(parseQuery$1, to, currentRoute.value.path) : assign({}, to);
  }
  function checkCanceledNavigation(to, from) {
    if (pendingLocation !== to) return createRouterError(ErrorTypes.NAVIGATION_CANCELLED, {
      from,
      to
    });
  }
  function push(to) {
    return pushWithRedirect(to);
  }
  function replace(to) {
    return push(assign(locationAsObject(to), { replace: true }));
  }
  function handleRedirectRecord(to, from) {
    const lastMatched = to.matched[to.matched.length - 1];
    if (lastMatched && lastMatched.redirect) {
      const { redirect } = lastMatched;
      let newTargetLocation = typeof redirect === "function" ? redirect(to, from) : redirect;
      if (typeof newTargetLocation === "string") {
        newTargetLocation = newTargetLocation.includes("?") || newTargetLocation.includes("#") ? newTargetLocation = locationAsObject(newTargetLocation) : { path: newTargetLocation };
        newTargetLocation.params = {};
      }
      return assign({
        query: to.query,
        hash: to.hash,
        params: newTargetLocation.path != null ? {} : to.params
      }, newTargetLocation);
    }
  }
  function pushWithRedirect(to, redirectedFrom) {
    const targetLocation = pendingLocation = resolve2(to);
    const from = currentRoute.value;
    const data = to.state;
    const force = to.force;
    const replace$1 = to.replace === true;
    const shouldRedirect = handleRedirectRecord(targetLocation, from);
    if (shouldRedirect) return pushWithRedirect(assign(locationAsObject(shouldRedirect), {
      state: typeof shouldRedirect === "object" ? assign({}, data, shouldRedirect.state) : data,
      force,
      replace: replace$1
    }), redirectedFrom || targetLocation);
    const toLocation = targetLocation;
    toLocation.redirectedFrom = redirectedFrom;
    let failure;
    if (!force && isSameRouteLocation(stringifyQuery$1, from, targetLocation)) {
      failure = createRouterError(ErrorTypes.NAVIGATION_DUPLICATED, {
        to: toLocation,
        from
      });
      handleScroll(from, from, true, false);
    }
    return (failure ? Promise.resolve(failure) : navigate(toLocation, from)).catch((error) => isNavigationFailure(error) ? isNavigationFailure(error, ErrorTypes.NAVIGATION_GUARD_REDIRECT) ? error : markAsReady(error) : triggerError(error, toLocation, from)).then((failure$1) => {
      if (failure$1) {
        if (isNavigationFailure(failure$1, ErrorTypes.NAVIGATION_GUARD_REDIRECT)) {
          return pushWithRedirect(assign({ replace: replace$1 }, locationAsObject(failure$1.to), {
            state: typeof failure$1.to === "object" ? assign({}, data, failure$1.to.state) : data,
            force
          }), redirectedFrom || toLocation);
        }
      } else failure$1 = finalizeNavigation(toLocation, from, true, replace$1, data);
      triggerAfterEach(toLocation, from, failure$1);
      return failure$1;
    });
  }
  function checkCanceledNavigationAndReject(to, from) {
    const error = checkCanceledNavigation(to, from);
    return error ? Promise.reject(error) : Promise.resolve();
  }
  function runWithContext(fn) {
    const app = installedApps.values().next().value;
    return app && typeof app.runWithContext === "function" ? app.runWithContext(fn) : fn();
  }
  function navigate(to, from) {
    let guards;
    const [leavingRecords, updatingRecords, enteringRecords] = extractChangingRecords(to, from);
    guards = extractComponentsGuards(leavingRecords.reverse(), "beforeRouteLeave", to, from);
    for (const record of leavingRecords) record.leaveGuards.forEach((guard) => {
      guards.push(guardToPromiseFn(guard, to, from));
    });
    const canceledNavigationCheck = checkCanceledNavigationAndReject.bind(null, to, from);
    guards.push(canceledNavigationCheck);
    return runGuardQueue(guards).then(() => {
      guards = [];
      for (const guard of beforeGuards.list()) guards.push(guardToPromiseFn(guard, to, from));
      guards.push(canceledNavigationCheck);
      return runGuardQueue(guards);
    }).then(() => {
      guards = extractComponentsGuards(updatingRecords, "beforeRouteUpdate", to, from);
      for (const record of updatingRecords) record.updateGuards.forEach((guard) => {
        guards.push(guardToPromiseFn(guard, to, from));
      });
      guards.push(canceledNavigationCheck);
      return runGuardQueue(guards);
    }).then(() => {
      guards = [];
      for (const record of enteringRecords) if (record.beforeEnter) if (isArray(record.beforeEnter)) for (const beforeEnter of record.beforeEnter) guards.push(guardToPromiseFn(beforeEnter, to, from));
      else guards.push(guardToPromiseFn(record.beforeEnter, to, from));
      guards.push(canceledNavigationCheck);
      return runGuardQueue(guards);
    }).then(() => {
      to.matched.forEach((record) => record.enterCallbacks = {});
      guards = extractComponentsGuards(enteringRecords, "beforeRouteEnter", to, from, runWithContext);
      guards.push(canceledNavigationCheck);
      return runGuardQueue(guards);
    }).then(() => {
      guards = [];
      for (const guard of beforeResolveGuards.list()) guards.push(guardToPromiseFn(guard, to, from));
      guards.push(canceledNavigationCheck);
      return runGuardQueue(guards);
    }).catch((err) => isNavigationFailure(err, ErrorTypes.NAVIGATION_CANCELLED) ? err : Promise.reject(err));
  }
  function triggerAfterEach(to, from, failure) {
    afterGuards.list().forEach((guard) => runWithContext(() => guard(to, from, failure)));
  }
  function finalizeNavigation(toLocation, from, isPush, replace$1, data) {
    const error = checkCanceledNavigation(toLocation, from);
    if (error) return error;
    const isFirstNavigation = from === START_LOCATION_NORMALIZED;
    const state = !isBrowser ? {} : history.state;
    if (isPush) if (replace$1 || isFirstNavigation) routerHistory.replace(toLocation.fullPath, assign({ scroll: isFirstNavigation && state && state.scroll }, data));
    else routerHistory.push(toLocation.fullPath, data);
    currentRoute.value = toLocation;
    handleScroll(toLocation, from, isPush, isFirstNavigation);
    markAsReady();
  }
  let removeHistoryListener;
  function setupListeners() {
    if (removeHistoryListener) return;
    removeHistoryListener = routerHistory.listen((to, _from, info) => {
      if (!router2.listening) return;
      const toLocation = resolve2(to);
      const shouldRedirect = handleRedirectRecord(toLocation, router2.currentRoute.value);
      if (shouldRedirect) {
        pushWithRedirect(assign(shouldRedirect, {
          replace: true,
          force: true
        }), toLocation).catch(noop);
        return;
      }
      pendingLocation = toLocation;
      const from = currentRoute.value;
      if (isBrowser) saveScrollPosition(getScrollKey(from.fullPath, info.delta), computeScrollPosition());
      navigate(toLocation, from).catch((error) => {
        if (isNavigationFailure(error, ErrorTypes.NAVIGATION_ABORTED | ErrorTypes.NAVIGATION_CANCELLED)) return error;
        if (isNavigationFailure(error, ErrorTypes.NAVIGATION_GUARD_REDIRECT)) {
          pushWithRedirect(assign(locationAsObject(error.to), { force: true }), toLocation).then((failure) => {
            if (isNavigationFailure(failure, ErrorTypes.NAVIGATION_ABORTED | ErrorTypes.NAVIGATION_DUPLICATED) && !info.delta && info.type === NavigationType.pop) routerHistory.go(-1, false);
          }).catch(noop);
          return Promise.reject();
        }
        if (info.delta) routerHistory.go(-info.delta, false);
        return triggerError(error, toLocation, from);
      }).then((failure) => {
        failure = failure || finalizeNavigation(toLocation, from, false);
        if (failure) {
          if (info.delta && !isNavigationFailure(failure, ErrorTypes.NAVIGATION_CANCELLED)) routerHistory.go(-info.delta, false);
          else if (info.type === NavigationType.pop && isNavigationFailure(failure, ErrorTypes.NAVIGATION_ABORTED | ErrorTypes.NAVIGATION_DUPLICATED)) routerHistory.go(-1, false);
        }
        triggerAfterEach(toLocation, from, failure);
      }).catch(noop);
    });
  }
  let readyHandlers = useCallbacks();
  let errorListeners = useCallbacks();
  let ready;
  function triggerError(error, to, from) {
    markAsReady(error);
    const list = errorListeners.list();
    if (list.length) list.forEach((handler) => handler(error, to, from));
    else {
      console.error(error);
    }
    return Promise.reject(error);
  }
  function isReady() {
    if (ready && currentRoute.value !== START_LOCATION_NORMALIZED) return Promise.resolve();
    return new Promise((resolve$1, reject) => {
      readyHandlers.add([resolve$1, reject]);
    });
  }
  function markAsReady(err) {
    if (!ready) {
      ready = !err;
      setupListeners();
      readyHandlers.list().forEach(([resolve$1, reject]) => err ? reject(err) : resolve$1());
      readyHandlers.reset();
    }
    return err;
  }
  function handleScroll(to, from, isPush, isFirstNavigation) {
    const { scrollBehavior } = options;
    if (!isBrowser || !scrollBehavior) return Promise.resolve();
    const scrollPosition = !isPush && getSavedScrollPosition(getScrollKey(to.fullPath, 0)) || (isFirstNavigation || !isPush) && history.state && history.state.scroll || null;
    return nextTick().then(() => scrollBehavior(to, from, scrollPosition)).then((position) => position && scrollToPosition(position)).catch((err) => triggerError(err, to, from));
  }
  const go = (delta) => routerHistory.go(delta);
  let started;
  const installedApps = /* @__PURE__ */ new Set();
  const router2 = {
    currentRoute,
    listening: true,
    addRoute,
    removeRoute,
    clearRoutes: matcher.clearRoutes,
    hasRoute,
    getRoutes,
    resolve: resolve2,
    options,
    push,
    replace,
    go,
    back: () => go(-1),
    forward: () => go(1),
    beforeEach: beforeGuards.add,
    beforeResolve: beforeResolveGuards.add,
    afterEach: afterGuards.add,
    onError: errorListeners.add,
    isReady,
    install(app) {
      app.component("RouterLink", RouterLink);
      app.component("RouterView", RouterView);
      app.config.globalProperties.$router = router2;
      Object.defineProperty(app.config.globalProperties, "$route", {
        enumerable: true,
        get: () => unref(currentRoute)
      });
      if (isBrowser && !started && currentRoute.value === START_LOCATION_NORMALIZED) {
        started = true;
        push(routerHistory.location).catch((err) => {
        });
      }
      const reactiveRoute = {};
      for (const key in START_LOCATION_NORMALIZED) Object.defineProperty(reactiveRoute, key, {
        get: () => currentRoute.value[key],
        enumerable: true
      });
      app.provide(routerKey, router2);
      app.provide(routeLocationKey, /* @__PURE__ */ shallowReactive(reactiveRoute));
      app.provide(routerViewLocationKey, currentRoute);
      const unmountApp = app.unmount;
      installedApps.add(app);
      app.unmount = function() {
        installedApps.delete(app);
        if (installedApps.size < 1) {
          pendingLocation = START_LOCATION_NORMALIZED;
          removeHistoryListener && removeHistoryListener();
          removeHistoryListener = null;
          currentRoute.value = START_LOCATION_NORMALIZED;
          started = false;
          ready = false;
        }
        unmountApp();
      };
    }
  };
  function runGuardQueue(guards) {
    return guards.reduce((promise, guard) => promise.then(() => runWithContext(guard)), Promise.resolve());
  }
  return router2;
}
const _hoisted_1$g = {
  class: "lg-defs",
  width: "0",
  height: "0",
  "aria-hidden": "true",
  focusable: "false",
  xmlns: "http://www.w3.org/2000/svg"
};
const _hoisted_2$f = {
  id: "lg-refract",
  x: "-35%",
  y: "-35%",
  width: "170%",
  height: "170%",
  "color-interpolation-filters": "sRGB"
};
const _hoisted_3$d = ["href"];
const _hoisted_4$d = {
  in: "EDGE_INTENSITY",
  result: "EDGE_MASK"
};
const _hoisted_5$c = ["tableValues"];
const _hoisted_6$b = ["scale"];
const _hoisted_7$b = ["scale"];
const _hoisted_8$9 = ["scale"];
const _hoisted_9$9 = {
  id: "lg-caustic",
  x: "-20%",
  y: "-20%",
  width: "140%",
  height: "140%",
  "color-interpolation-filters": "sRGB"
};
const _hoisted_10$9 = ["scale"];
const _hoisted_11$9 = {
  id: "lg-orb-light",
  x: "-25%",
  y: "-25%",
  width: "150%",
  height: "150%",
  "color-interpolation-filters": "sRGB"
};
const _hoisted_12$9 = ["specularConstant"];
const _sfc_main$h = /* @__PURE__ */ defineComponent({
  __name: "LiquidGlassDefs",
  props: {
    refraction: {},
    specular: {},
    aberration: {},
    lensMap: {}
  },
  setup(__props) {
    const props = __props;
    const cssRefraction = /* @__PURE__ */ ref(14);
    const cssSpecular = /* @__PURE__ */ ref(0.55);
    const cssAberration = /* @__PURE__ */ ref(2);
    const glassOff = /* @__PURE__ */ ref(false);
    function readVars() {
      const el = document.documentElement;
      const cs = getComputedStyle(el);
      const r = parseFloat(cs.getPropertyValue("--lg-refraction"));
      const s = parseFloat(cs.getPropertyValue("--lg-specular"));
      const a = parseFloat(cs.getPropertyValue("--lg-aberration"));
      if (Number.isFinite(r)) cssRefraction.value = r;
      if (Number.isFinite(s)) cssSpecular.value = s;
      if (Number.isFinite(a)) cssAberration.value = a;
      glassOff.value = el.getAttribute("data-glass") === "off";
    }
    let observer = null;
    onMounted(() => {
      readVars();
      observer = new MutationObserver(readVars);
      observer.observe(document.documentElement, {
        attributes: true,
        attributeFilter: ["style", "data-glass", "data-theme", "data-motion"]
      });
    });
    onBeforeUnmount(() => {
      observer?.disconnect();
      observer = null;
    });
    const refraction = computed(() => props.refraction ?? cssRefraction.value);
    const specular = computed(() => props.specular ?? cssSpecular.value);
    const aberration = computed(() => props.aberration ?? cssAberration.value);
    const scaleR = computed(() => glassOff.value ? 0 : Math.max(0, refraction.value));
    const scaleG = computed(() => glassOff.value ? 0 : Math.max(0, scaleR.value - aberration.value * 0.05));
    const scaleB = computed(() => glassOff.value ? 0 : Math.max(0, scaleR.value - aberration.value * 0.1));
    const edgeMaskTableValues = computed(() => {
      const ab = aberration.value;
      const t2 = Math.min(1, Math.max(0, ab * 0.05));
      return `0 ${t2} 1`;
    });
    const causticScale = computed(() => glassOff.value ? 0 : 12 + refraction.value * 1.6);
    const orbSpecular = computed(() => (0.5 + specular.value * 1.2).toFixed(2));
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("svg", _hoisted_1$g, [
        createBaseVNode("defs", null, [
          createBaseVNode("filter", _hoisted_2$f, [
            createBaseVNode("feImage", {
              x: "0",
              y: "0",
              width: "100%",
              height: "100%",
              preserveAspectRatio: "xMidYMid slice",
              href: __props.lensMap || void 0,
              result: "DISPLACEMENT_MAP"
            }, null, 8, _hoisted_3$d),
            _cache[0] || (_cache[0] = createBaseVNode("feColorMatrix", {
              in: "DISPLACEMENT_MAP",
              type: "matrix",
              values: "0.3 0.3 0.3 0 0\n                  0.3 0.3 0.3 0 0\n                  0.3 0.3 0.3 0 0\n                  0   0   0   1 0",
              result: "EDGE_INTENSITY"
            }, null, -1)),
            createBaseVNode("feComponentTransfer", _hoisted_4$d, [
              createBaseVNode("feFuncA", {
                type: "discrete",
                tableValues: edgeMaskTableValues.value
              }, null, 8, _hoisted_5$c)
            ]),
            createBaseVNode("feDisplacementMap", {
              in: "SourceGraphic",
              in2: "DISPLACEMENT_MAP",
              scale: scaleR.value,
              xChannelSelector: "R",
              yChannelSelector: "B",
              result: "RED_DISPLACED"
            }, null, 8, _hoisted_6$b),
            _cache[1] || (_cache[1] = createBaseVNode("feColorMatrix", {
              in: "RED_DISPLACED",
              type: "matrix",
              values: "1 0 0 0 0\n                  0 0 0 0 0\n                  0 0 0 0 0\n                  0 0 0 1 0",
              result: "RED_CHANNEL"
            }, null, -1)),
            createBaseVNode("feDisplacementMap", {
              in: "SourceGraphic",
              in2: "DISPLACEMENT_MAP",
              scale: scaleG.value,
              xChannelSelector: "R",
              yChannelSelector: "B",
              result: "GREEN_DISPLACED"
            }, null, 8, _hoisted_7$b),
            _cache[2] || (_cache[2] = createBaseVNode("feColorMatrix", {
              in: "GREEN_DISPLACED",
              type: "matrix",
              values: "0 0 0 0 0\n                  0 1 0 0 0\n                  0 0 0 0 0\n                  0 0 0 1 0",
              result: "GREEN_CHANNEL"
            }, null, -1)),
            createBaseVNode("feDisplacementMap", {
              in: "SourceGraphic",
              in2: "DISPLACEMENT_MAP",
              scale: scaleB.value,
              xChannelSelector: "R",
              yChannelSelector: "B",
              result: "BLUE_DISPLACED"
            }, null, 8, _hoisted_8$9),
            _cache[3] || (_cache[3] = createStaticVNode('<feColorMatrix in="BLUE_DISPLACED" type="matrix" values="0 0 0 0 0\n                  0 0 0 0 0\n                  0 0 1 0 0\n                  0 0 0 1 0" result="BLUE_CHANNEL" data-v-6594b546></feColorMatrix><feBlend in="RED_CHANNEL" in2="GREEN_CHANNEL" mode="screen" result="RG_COMBINED" data-v-6594b546></feBlend><feBlend in="RG_COMBINED" in2="BLUE_CHANNEL" mode="screen" result="RGB_DISPLACED" data-v-6594b546></feBlend><feComposite in="RGB_DISPLACED" in2="EDGE_MASK" operator="in" result="RGB_DISPLACED_MASKED" data-v-6594b546></feComposite><feComposite in="SourceGraphic" in2="EDGE_MASK" operator="in" result="RGB_SOURCE_MASKED" data-v-6594b546></feComposite><feComposite in="RGB_DISPLACED_MASKED" in2="RGB_SOURCE_MASKED" operator="arithmetic" k1="0" k2="1" k3="1" k4="0" data-v-6594b546></feComposite>', 6))
          ]),
          createBaseVNode("filter", _hoisted_9$9, [
            _cache[4] || (_cache[4] = createBaseVNode("feTurbulence", {
              type: "fractalNoise",
              baseFrequency: "0.014 0.021",
              numOctaves: "2",
              seed: "41",
              stitchTiles: "stitch",
              result: "cNoise"
            }, null, -1)),
            createBaseVNode("feDisplacementMap", {
              in: "SourceGraphic",
              in2: "cNoise",
              scale: causticScale.value,
              xChannelSelector: "R",
              yChannelSelector: "B"
            }, null, 8, _hoisted_10$9),
            _cache[5] || (_cache[5] = createStaticVNode('<feGaussianBlur stdDeviation="5" result="cBlur" data-v-6594b546></feGaussianBlur><feComponentTransfer in="cBlur" data-v-6594b546><feFuncR type="linear" slope="1.5" intercept="-0.16" data-v-6594b546></feFuncR><feFuncG type="linear" slope="1.5" intercept="-0.16" data-v-6594b546></feFuncG><feFuncB type="linear" slope="1.5" intercept="-0.12" data-v-6594b546></feFuncB></feComponentTransfer>', 2))
          ]),
          createBaseVNode("filter", _hoisted_11$9, [
            _cache[7] || (_cache[7] = createBaseVNode("feGaussianBlur", {
              in: "SourceAlpha",
              stdDeviation: "3.5",
              result: "orbAlpha"
            }, null, -1)),
            createBaseVNode("feSpecularLighting", {
              in: "orbAlpha",
              surfaceScale: "4.5",
              specularConstant: orbSpecular.value,
              specularExponent: "22",
              "lighting-color": "#ffffff",
              result: "orbSpec"
            }, [..._cache[6] || (_cache[6] = [
              createBaseVNode("fePointLight", {
                x: "-14",
                y: "-22",
                z: "58"
              }, null, -1)
            ])], 8, _hoisted_12$9),
            _cache[8] || (_cache[8] = createBaseVNode("feComposite", {
              in: "orbSpec",
              in2: "SourceAlpha",
              operator: "in",
              result: "orbSpecIn"
            }, null, -1)),
            _cache[9] || (_cache[9] = createBaseVNode("feComposite", {
              in: "SourceGraphic",
              in2: "orbSpecIn",
              operator: "arithmetic",
              k1: "0",
              k2: "1",
              k3: "1",
              k4: "0"
            }, null, -1))
          ])
        ])
      ]);
    };
  }
});
const _export_sfc = (sfc, props) => {
  const target = sfc.__vccOpts || sfc;
  for (const [key, val] of props) {
    target[key] = val;
  }
  return target;
};
const LiquidGlassDefs = /* @__PURE__ */ _export_sfc(_sfc_main$h, [["__scopeId", "data-v-6594b546"]]);
const GLASS_LIMITS = {
  blur: { min: 8, max: 40 },
  opacity: { min: 0.3, max: 0.96 },
  saturate: { min: 1, max: 2.2 },
  refraction: { min: 0, max: 40 },
  specular: { min: 0, max: 1 },
  aberration: { min: 0, max: 8 },
  radius: { min: 0, max: 28 }
};
const DEFAULT_APPEARANCE = {
  theme: "auto",
  liquidGlass: true,
  material: "acrylic",
  blur: 22,
  opacity: 0.62,
  saturate: 1.6,
  refraction: 14,
  specular: 0.55,
  aberration: 2,
  accent: "#2B6EE0",
  radius: 16,
  reduceMotion: false
};
const THEME_MODES = ["light", "dark", "auto"];
const MATERIALS = ["acrylic", "mica", "blur", "none"];
function clampNum(v, range, fallback) {
  const n = typeof v === "number" ? v : Number(v);
  if (!Number.isFinite(n)) return fallback;
  return Math.min(range.max, Math.max(range.min, n));
}
function normalizeAccent(input, fallback = DEFAULT_APPEARANCE.accent) {
  if (typeof input !== "string") return fallback;
  const s = input.trim();
  if (/^#[0-9a-fA-F]{3}$/.test(s)) {
    return `#${s[1]}${s[1]}${s[2]}${s[2]}${s[3]}${s[3]}`.toLowerCase();
  }
  if (/^#[0-9a-fA-F]{6}$/.test(s)) return s.toLowerCase();
  return fallback;
}
function clampAppearance(input, base = DEFAULT_APPEARANCE) {
  const src = input || {};
  return {
    theme: THEME_MODES.includes(src.theme) ? src.theme : base.theme,
    liquidGlass: typeof src.liquidGlass === "boolean" ? src.liquidGlass : base.liquidGlass,
    material: MATERIALS.includes(src.material) ? src.material : base.material,
    blur: clampNum(src.blur, GLASS_LIMITS.blur, base.blur),
    opacity: clampNum(src.opacity, GLASS_LIMITS.opacity, base.opacity),
    saturate: clampNum(src.saturate, GLASS_LIMITS.saturate, base.saturate),
    refraction: clampNum(src.refraction, GLASS_LIMITS.refraction, base.refraction),
    specular: clampNum(src.specular, GLASS_LIMITS.specular, base.specular),
    aberration: clampNum(src.aberration, GLASS_LIMITS.aberration, base.aberration),
    accent: normalizeAccent(src.accent, base.accent),
    radius: clampNum(src.radius, GLASS_LIMITS.radius, base.radius),
    reduceMotion: typeof src.reduceMotion === "boolean" ? src.reduceMotion : base.reduceMotion
  };
}
function hexToRgb(hex) {
  const s = normalizeAccent(hex);
  return {
    r: parseInt(s.slice(1, 3), 16),
    g: parseInt(s.slice(3, 5), 16),
    b: parseInt(s.slice(5, 7), 16)
  };
}
function relativeLuminance(hex) {
  const { r, g, b } = hexToRgb(hex);
  const lin = (c) => {
    const v = c / 255;
    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
}
function onAccentColor(hex) {
  return relativeLuminance(hex) > 0.45 ? "#101418" : "#ffffff";
}
function shade(hex, amount) {
  const { r, g, b } = hexToRgb(hex);
  const f = (c) => Math.round(amount >= 0 ? c + (255 - c) * amount : c * (1 + amount)).toString(16).padStart(2, "0");
  return `#${f(r)}${f(g)}${f(b)}`;
}
function glassCssVars(a, theme, effectiveMaterial = "none") {
  const cfg = clampAppearance(a);
  const dark = theme === "dark";
  const hasOsBlur = effectiveMaterial !== "none";
  const tintAlpha = hasOsBlur ? cfg.opacity * 0.55 : cfg.opacity;
  const glassRgb = dark ? "22, 26, 33" : "255, 255, 255";
  const veilRgb = dark ? "10, 12, 16" : "236, 241, 248";
  const accent = normalizeAccent(cfg.accent);
  return {
    "--lg-enabled": cfg.liquidGlass ? "1" : "0",
    "--lg-blur": `${cfg.blur}px`,
    "--lg-blur-strong": `${Math.round(cfg.blur * 1.6)}px`,
    "--lg-sat": `${cfg.saturate}`,
    "--lg-radius": `${cfg.radius}px`,
    "--lg-radius-sm": `${Math.max(4, Math.round(cfg.radius * 0.6))}px`,
    "--lg-radius-pill": "999px",
    "--lg-refraction": `${cfg.refraction}`,
    "--lg-specular": `${cfg.specular}`,
    "--lg-aberration": `${cfg.aberration}`,
    // 玻璃主体
    "--lg-tint": `rgba(${glassRgb}, ${tintAlpha.toFixed(3)})`,
    "--lg-tint-weak": `rgba(${glassRgb}, ${(tintAlpha * 0.55).toFixed(3)})`,
    "--lg-tint-strong": `rgba(${glassRgb}, ${Math.min(0.98, tintAlpha * 1.25).toFixed(3)})`,
    "--lg-veil": `rgba(${veilRgb}, ${dark ? 0.72 : 0.86})`,
    // 玻璃关闭时的不透明兜底色（投影/低配场景）
    "--lg-solid": dark ? "#171b22" : "#ffffff",
    // 边缘光 / 镜面高光
    "--lg-rim": dark ? `rgba(255, 255, 255, ${(0.14 + cfg.specular * 0.22).toFixed(3)})` : `rgba(255, 255, 255, ${(0.5 + cfg.specular * 0.45).toFixed(3)})`,
    "--lg-rim-bottom": dark ? "rgba(255, 255, 255, 0.06)" : "rgba(255, 255, 255, 0.28)",
    "--lg-hairline": dark ? "rgba(255, 255, 255, 0.10)" : "rgba(16, 24, 40, 0.08)",
    "--lg-specular-color": dark ? `rgba(255, 255, 255, ${(cfg.specular * 0.3).toFixed(3)})` : `rgba(255, 255, 255, ${(cfg.specular * 0.85).toFixed(3)})`,
    // 投影：亮色用冷灰，暗色用纯黑加深
    "--lg-shadow": dark ? "0 18px 48px rgba(0, 0, 0, 0.55), 0 2px 8px rgba(0, 0, 0, 0.4)" : "0 18px 44px rgba(16, 32, 64, 0.16), 0 2px 8px rgba(16, 32, 64, 0.08)",
    "--lg-shadow-sm": dark ? "0 6px 18px rgba(0, 0, 0, 0.45)" : "0 6px 18px rgba(16, 32, 64, 0.12)",
    // 文本 / 分隔线
    "--lg-text": dark ? "#eef2f8" : "#141a22",
    "--lg-text-secondary": dark ? "rgba(238, 242, 248, 0.66)" : "rgba(20, 26, 34, 0.62)",
    "--lg-text-disabled": dark ? "rgba(238, 242, 248, 0.36)" : "rgba(20, 26, 34, 0.34)",
    "--lg-divider": dark ? "rgba(255, 255, 255, 0.08)" : "rgba(16, 24, 40, 0.07)",
    "--lg-hover": dark ? "rgba(255, 255, 255, 0.08)" : "rgba(16, 32, 64, 0.05)",
    // 主题色族
    "--lg-accent": accent,
    "--lg-accent-hover": dark ? shade(accent, 0.16) : shade(accent, -0.12),
    "--lg-accent-soft": dark ? `${accent}33` : `${accent}1f`,
    "--lg-accent-on": onAccentColor(accent),
    // 动效时长（降动效时归零，CSS 里统一引用）
    "--lg-anim": cfg.reduceMotion ? "0ms" : "220ms",
    "--lg-anim-fast": cfg.reduceMotion ? "0ms" : "120ms",
    "--lg-spring": cfg.reduceMotion ? "linear" : "cubic-bezier(0.34, 1.46, 0.44, 0.96)"
  };
}
function makeLensMap(size = 128) {
  const canvas = document.createElement("canvas");
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext("2d");
  if (!ctx) return "";
  const img = ctx.createImageData(size, size);
  const cx = (size - 1) / 2;
  const cy = (size - 1) / 2;
  const R = size * 0.46;
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const dx = x - cx;
      const dy = y - cy;
      const dist = Math.sqrt(dx * dx + dy * dy);
      const i = (y * size + x) * 4;
      if (dist < R) {
        const nx = dx / R;
        const ny = dy / R;
        img.data[i] = clamp255(128 + nx * 127);
        img.data[i + 1] = clamp255(128 + ny * 127);
        img.data[i + 2] = clamp255(128 + ny * 127);
        img.data[i + 3] = 255;
      } else {
        img.data[i] = 128;
        img.data[i + 1] = 128;
        img.data[i + 2] = 128;
        img.data[i + 3] = 255;
      }
    }
  }
  ctx.putImageData(img, 0, 0);
  return canvas.toDataURL("image/png");
}
function clamp255(v) {
  return Math.max(0, Math.min(255, Math.round(v)));
}
const _sfc_main$g = /* @__PURE__ */ defineComponent({
  __name: "App",
  setup(__props) {
    const lensMap = /* @__PURE__ */ ref("");
    function applyAppearance(snap) {
      const el = document.documentElement;
      const vars = glassCssVars(snap.appearance, snap.theme, snap.effectiveMaterial);
      for (const [k, v] of Object.entries(vars)) {
        el.style.setProperty(k, v);
      }
      el.setAttribute("data-theme", snap.theme);
      el.setAttribute("data-glass", snap.appearance.liquidGlass ? "on" : "off");
      el.setAttribute("data-motion", snap.appearance.reduceMotion ? "reduced" : "full");
    }
    let unsub = null;
    onMounted(async () => {
      lensMap.value = makeLensMap(128);
      try {
        const snap = await window.sidekick.appearance.get();
        applyAppearance(snap);
        unsub = window.sidekick.appearance.onChanged(applyAppearance);
      } catch (e) {
        console.warn("[App] 外观初始化失败（将使用兜底 token）:", e);
      }
    });
    onUnmounted(() => {
      unsub?.();
    });
    return (_ctx, _cache) => {
      const _component_router_view = resolveComponent("router-view");
      return openBlock(), createElementBlock(Fragment, null, [
        createVNode(LiquidGlassDefs, { "lens-map": lensMap.value }, null, 8, ["lens-map"]),
        createVNode(_component_router_view)
      ], 64);
    };
  }
});
const __vite_glob_0_0 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3cpath%20d='M5%2019l1-4L16%205l3%203L9%2018l-4%201z'/%3e%3cline%20x1='14'%20y1='7'%20x2='17'%20y2='10'/%3e%3c/svg%3e";
const __vite_glob_0_1 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3crect%20x='5'%20y='4'%20width='14'%20height='16'%20rx='2'/%3e%3cline%20x1='8'%20y1='9'%20x2='16'%20y2='9'/%3e%3cline%20x1='8'%20y1='13'%20x2='16'%20y2='13'/%3e%3ccircle%20cx='16'%20cy='7'%20r='1.4'/%3e%3c/svg%3e";
const __vite_glob_0_2 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3crect%20x='3'%20y='7'%20width='18'%20height='12'%20rx='2.5'/%3e%3ccircle%20cx='12'%20cy='13'%20r='3.5'/%3e%3cpath%20d='M9%207l1.5-2.5h3L15%207'/%3e%3c/svg%3e";
const __vite_glob_0_3 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3crect%20x='4'%20y='6'%20width='16'%20height='12'%20rx='2'/%3e%3cline%20x1='8'%20y1='10'%20x2='16'%20y2='10'/%3e%3cline%20x1='8'%20y1='14'%20x2='13'%20y2='14'/%3e%3c/svg%3e";
const __vite_glob_0_4 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3cpath%20d='M10%2013a5%205%200%200%200%207%200l2-2a5%205%200%200%200-7-7l-1%201'/%3e%3cpath%20d='M14%2011a5%205%200%200%200-7%200l-2%202a5%205%200%200%200%207%207l1-1'/%3e%3c/svg%3e";
const __vite_glob_0_5 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3crect%20x='5'%20y='11'%20width='14'%20height='9'%20rx='2'/%3e%3cpath%20d='M8%2011V8a4%204%200%200%201%208%200v3'/%3e%3c/svg%3e";
const __vite_glob_0_6 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3crect%20x='6'%20y='3'%20width='12'%20height='18'%20rx='2'/%3e%3ccircle%20cx='9.5'%20cy='8'%20r='1.5'/%3e%3cpath%20d='M8%2017l3-3%202%202%201.5-1.5L17%2017'/%3e%3c/svg%3e";
const __vite_glob_0_7 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3cpath%20d='M7%208V4h10v4'/%3e%3crect%20x='4'%20y='8'%20width='16'%20height='8'%20rx='1.5'/%3e%3crect%20x='7'%20y='13'%20width='10'%20height='6'%20rx='1'/%3e%3cline%20x1='7'%20y1='11'%20x2='17'%20y2='11'/%3e%3c/svg%3e";
const __vite_glob_0_8 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3ccircle%20cx='12'%20cy='12'%20r='8'/%3e%3ccircle%20cx='12'%20cy='12'%20r='3'/%3e%3c/svg%3e";
const __vite_glob_0_9 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3cpath%20d='M6%209a6%206%200%200%201%2012%200c0%204%201.5%205%202%206H4c.5-1%202-2%202-6z'/%3e%3cpath%20d='M10%2019a2%202%200%200%200%204%200'/%3e%3c/svg%3e";
const __vite_glob_0_10 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3cline%20x1='5'%20y1='8'%20x2='19'%20y2='8'/%3e%3ccircle%20cx='9'%20cy='8'%20r='2'/%3e%3cline%20x1='5'%20y1='16'%20x2='19'%20y2='16'/%3e%3ccircle%20cx='15'%20cy='16'%20r='2'/%3e%3c/svg%3e";
const __vite_glob_0_11 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3crect%20x='4'%20y='5'%20width='16'%20height='14'%20rx='2'/%3e%3cline%20x1='15'%20y1='6'%20x2='15'%20y2='18'/%3e%3c/svg%3e";
const __vite_glob_0_12 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3crect%20x='4'%20y='5'%20width='16'%20height='14'%20rx='2'/%3e%3cline%20x1='7'%20y1='9'%20x2='17'%20y2='9'/%3e%3cline%20x1='7'%20y1='13'%20x2='14'%20y2='13'/%3e%3cline%20x1='7'%20y1='16'%20x2='11'%20y2='16'/%3e%3c/svg%3e";
const __vite_glob_0_13 = "data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2024%2024'%20fill='none'%20stroke='currentColor'%20stroke-width='2'%20stroke-linecap='round'%20stroke-linejoin='round'%3e%3crect%20x='7'%20y='9'%20width='10'%20height='11'%20rx='2'/%3e%3cline%20x1='10.5'%20y1='9'%20x2='10.5'%20y2='5'/%3e%3cline%20x1='13.5'%20y1='9'%20x2='13.5'%20y2='5'/%3e%3cline%20x1='10'%20y1='13.5'%20x2='14'%20y2='13.5'/%3e%3c/svg%3e";
const _hoisted_1$f = { class: "panel-content" };
const _hoisted_2$e = { class: "ime-current" };
const _hoisted_3$c = { class: "ime-info" };
const _hoisted_4$c = { class: "ime-value" };
const _hoisted_5$b = { class: "lock-row" };
const _sfc_main$f = /* @__PURE__ */ defineComponent({
  __name: "ImePanel",
  setup(__props) {
    const imeMode = /* @__PURE__ */ ref("cn");
    const imeLocale = /* @__PURE__ */ ref("zh-CN");
    const capsLock = /* @__PURE__ */ ref(false);
    const numLock = /* @__PURE__ */ ref(false);
    let unsub = null;
    function applyState(s) {
      imeMode.value = s.mode;
      imeLocale.value = s.locale;
      capsLock.value = !!s.capsLock;
      numLock.value = !!s.numLock;
    }
    async function loadState() {
      try {
        const s = await window.sidekick.ime.getState();
        applyState(s);
      } catch {
      }
    }
    async function toggle() {
      try {
        const s = await window.sidekick.ime.toggle();
        applyState(s);
      } catch (e) {
        console.error(e);
      }
    }
    onMounted(() => {
      loadState();
      unsub = window.sidekick.ime.onChanged((s) => {
        applyState(s);
      });
    });
    onUnmounted(() => {
      if (unsub) unsub();
    });
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$f, [
        createBaseVNode("div", _hoisted_2$e, [
          createBaseVNode("div", {
            class: normalizeClass(["ime-indicator", imeMode.value])
          }, toDisplayString(imeMode.value === "cn" ? "中" : "EN"), 3),
          createBaseVNode("div", _hoisted_3$c, [
            _cache[0] || (_cache[0] = createBaseVNode("span", { class: "ime-label" }, "当前输入法", -1)),
            createBaseVNode("span", _hoisted_4$c, toDisplayString(imeLocale.value), 1)
          ])
        ]),
        createBaseVNode("button", {
          class: "btn-toggle",
          onClick: toggle
        }, "切换输入法"),
        createBaseVNode("div", _hoisted_5$b, [
          createBaseVNode("span", {
            class: normalizeClass(["lock-chip", { on: capsLock.value }])
          }, "Caps " + toDisplayString(capsLock.value ? "开" : "关"), 3),
          createBaseVNode("span", {
            class: normalizeClass(["lock-chip", { on: numLock.value }])
          }, "Num " + toDisplayString(numLock.value ? "开" : "关"), 3)
        ]),
        _cache[1] || (_cache[1] = createBaseVNode("div", { class: "ime-hint" }, "点击按钮或侧边栏图标可快速切换中英文输入法", -1))
      ]);
    };
  }
});
const ImePanel = /* @__PURE__ */ _export_sfc(_sfc_main$f, [["__scopeId", "data-v-b8644ae5"]]);
const _hoisted_1$e = { class: "panel-content" };
const _hoisted_2$d = {
  key: 0,
  class: "recording-status"
};
const _hoisted_3$b = { class: "rec-indicator" };
const _hoisted_4$b = { class: "rec-timer" };
const _hoisted_5$a = { class: "rec-actions" };
const _hoisted_6$a = ["disabled"];
const _hoisted_7$a = ["src"];
const _hoisted_8$8 = {
  key: 1,
  class: "longshot-window-select"
};
const _hoisted_9$8 = { class: "select-header" };
const _hoisted_10$8 = ["src"];
const _hoisted_11$8 = { class: "window-list" };
const _hoisted_12$8 = ["onClick"];
const _hoisted_13$8 = { class: "window-title" };
const _hoisted_14$8 = { class: "window-meta" };
const _hoisted_15$7 = {
  key: 0,
  class: "window-empty"
};
const _hoisted_16$7 = {
  key: 2,
  class: "longshot-countdown"
};
const _hoisted_17$6 = { class: "countdown-overlay" };
const _hoisted_18$6 = { class: "countdown-number" };
const _hoisted_19$5 = {
  key: 3,
  class: "longshot-status"
};
const _hoisted_20$5 = { class: "shot-indicator" };
const _hoisted_21$5 = { class: "shot-text" };
const _hoisted_22$5 = {
  key: 0,
  class: "progress-bar"
};
const _hoisted_23$5 = {
  key: 1,
  class: "shot-meta"
};
const _hoisted_24$3 = { key: 0 };
const _hoisted_25$3 = {
  key: 4,
  class: "action-grid"
};
const _hoisted_26$3 = ["src"];
const _hoisted_27$3 = ["src"];
const _hoisted_28$3 = ["src"];
const _hoisted_29$3 = ["src"];
const _hoisted_30$3 = {
  key: 5,
  class: "recorder-options"
};
const _hoisted_31$3 = { class: "opt-row" };
const _hoisted_32$3 = { class: "opt-row" };
const _hoisted_33$3 = {
  key: 0,
  class: "opt-row slider"
};
const _hoisted_34$3 = { class: "opt-val" };
const _hoisted_35$3 = {
  key: 1,
  class: "opt-row slider"
};
const _hoisted_36$3 = { class: "opt-val" };
const _hoisted_37$3 = {
  key: 6,
  class: "recent-section"
};
const _hoisted_38$3 = { class: "file-list" };
const _hoisted_39$3 = ["onClick"];
const _hoisted_40$3 = { class: "file-name" };
const _hoisted_41$3 = { class: "file-time" };
const _sfc_main$e = /* @__PURE__ */ defineComponent({
  __name: "CapturePanel",
  setup(__props) {
    const recording = /* @__PURE__ */ ref(false);
    const starting = /* @__PURE__ */ ref(false);
    const paused = /* @__PURE__ */ ref(false);
    const elapsed = /* @__PURE__ */ ref(0);
    const optMic = /* @__PURE__ */ ref(false);
    const optSystem = /* @__PURE__ */ ref(true);
    const optMicVol = /* @__PURE__ */ ref(1.2);
    const optSysVol = /* @__PURE__ */ ref(0.8);
    const longshotStep = /* @__PURE__ */ ref("idle");
    const longshotStatus = /* @__PURE__ */ ref("准备中...");
    const longshotProgress = /* @__PURE__ */ ref(0);
    const longshotFrameCount = /* @__PURE__ */ ref(0);
    const longshotTitle = /* @__PURE__ */ ref("");
    const availableWindows = /* @__PURE__ */ ref([]);
    const countdownValue = /* @__PURE__ */ ref(3);
    const countdownTimer = /* @__PURE__ */ ref(null);
    const recentFiles = /* @__PURE__ */ ref([]);
    let unsubStatus = null;
    let unsubLongshot = null;
    let unsubCountdown = null;
    function icon(name) {
      return new URL((/* @__PURE__ */ Object.assign({ "../assets/icons/annotate.svg": __vite_glob_0_0, "../assets/icons/bell.svg": __vite_glob_0_1, "../assets/icons/capture.svg": __vite_glob_0_2, "../assets/icons/ime.svg": __vite_glob_0_3, "../assets/icons/link.svg": __vite_glob_0_4, "../assets/icons/lock.svg": __vite_glob_0_5, "../assets/icons/longshot.svg": __vite_glob_0_6, "../assets/icons/printer.svg": __vite_glob_0_7, "../assets/icons/record.svg": __vite_glob_0_8, "../assets/icons/reminder.svg": __vite_glob_0_9, "../assets/icons/settings.svg": __vite_glob_0_10, "../assets/icons/sidebar.svg": __vite_glob_0_11, "../assets/icons/taskmgr.svg": __vite_glob_0_12, "../assets/icons/usb.svg": __vite_glob_0_13 }))[`../assets/icons/${name}.svg`], import.meta.url).href;
    }
    function formatTime(sec) {
      const m = Math.floor(sec / 60);
      const s = sec % 60;
      return `${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`;
    }
    onMounted(() => {
      loadOptions();
      unsubStatus = window.sidekick.recorder.onStatusChanged((status) => {
        recording.value = status.recording || false;
        starting.value = status.starting || false;
        paused.value = status.paused || false;
        elapsed.value = status.elapsed || 0;
        if (!status.recording && !status.starting && status.filepath) {
          const name = status.filepath.split(/[\\/]/).pop() || "";
          recentFiles.value.unshift({
            name,
            path: status.filepath,
            time: (/* @__PURE__ */ new Date()).toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit" }),
            type: "video"
          });
          if (recentFiles.value.length > 5) recentFiles.value.pop();
        }
      });
      unsubLongshot = window.sidekick.longshot.onProgress((data) => {
        longshotFrameCount.value = data.frameIndex || 0;
        longshotTitle.value = data.title || "";
        if (data.status === "capturing") {
          longshotStep.value = "running";
          longshotStatus.value = "正在滚动截图...";
          longshotProgress.value = Math.min(90, (data.frameIndex || 0) / 20 * 100);
        } else if (data.status === "stitching") {
          longshotStep.value = "running";
          longshotStatus.value = "正在拼接图像...";
          longshotProgress.value = 95;
        } else if (data.status === "done") {
          longshotStatus.value = "长截图完成";
          longshotProgress.value = 100;
          if (data.filepath) {
            addRecent(data.filepath, "image");
            window.sidekick.notification.show({
              title: "长截图完成",
              message: "已保存并复制到剪贴板",
              duration: 4e3
            }).catch(() => {
            });
          }
          setTimeout(() => {
            longshotStep.value = "idle";
            longshotProgress.value = 0;
          }, 2e3);
        } else if (data.status === "error") {
          longshotStep.value = "idle";
          longshotStatus.value = "长截图失败: " + (data.error || "未知错误");
          longshotProgress.value = 0;
        }
      });
      unsubCountdown = window.sidekick.longshot.onCountdown((n) => {
        countdownValue.value = n;
        if (n > 0) {
          longshotStep.value = "countdown";
        } else if (n === 0) {
          longshotStep.value = "running";
          longshotStatus.value = "正在滚动截图...";
        }
      });
    });
    onUnmounted(() => {
      if (unsubStatus) unsubStatus();
      if (unsubLongshot) unsubLongshot();
      if (unsubCountdown) unsubCountdown();
      if (countdownTimer.value) clearInterval(countdownTimer.value);
    });
    async function captureRegion() {
      try {
        const result = await window.sidekick.capture.region({ mode: "region" });
        if (result?.success && result.filepath) {
          addRecent(result.filepath, "image");
        }
      } catch (e) {
        console.error(e);
      }
    }
    async function annotate() {
      try {
        const result = await window.sidekick.capture.annotate({ mode: "annotate" });
        if (result?.success && result.filepath) {
          addRecent(result.filepath, "image");
        }
      } catch (e) {
        console.error(e);
      }
    }
    async function startLongshot() {
      if (longshotStep.value !== "idle") return;
      try {
        longshotStep.value = "select";
        longshotStatus.value = "正在获取窗口列表...";
        const result = await window.sidekick.longshot.selectWindow();
        if (result?.success && result.windows) {
          availableWindows.value = result.windows;
        } else {
          availableWindows.value = [];
        }
      } catch (e) {
        console.error(e);
        longshotStep.value = "idle";
      }
    }
    async function confirmWindow(win) {
      try {
        longshotStep.value = "countdown";
        countdownValue.value = 3;
        const result = await window.sidekick.longshot.start({ window: win });
        if (!result) {
          longshotStep.value = "idle";
          longshotStatus.value = "长截图启动失败";
        }
      } catch (e) {
        console.error(e);
        longshotStep.value = "idle";
      }
    }
    function cancelLongshot() {
      longshotStep.value = "idle";
      availableWindows.value = [];
    }
    async function stopLongshot() {
      try {
        await window.sidekick.longshot.stop();
        longshotStatus.value = "正在停止...";
      } catch (e) {
        console.error(e);
      }
    }
    async function startRecording() {
      if (recording.value || starting.value) return;
      try {
        starting.value = true;
        const result = await window.sidekick.recorder.start({ fps: 15, mic: optMic.value });
        if (!result?.success) {
          starting.value = false;
          console.error("录屏启动失败:", result?.error);
        }
      } catch (e) {
        starting.value = false;
        console.error(e);
      }
    }
    async function stopRecording() {
      try {
        await window.sidekick.recorder.stop();
      } catch (e) {
        console.error(e);
      }
    }
    async function pauseRecording() {
      try {
        await window.sidekick.recorder.pause();
      } catch (e) {
        console.error(e);
      }
    }
    async function resumeRecording() {
      try {
        await window.sidekick.recorder.resume();
      } catch (e) {
        console.error(e);
      }
    }
    async function loadOptions() {
      try {
        const cfg = await window.sidekick.config.get();
        optMic.value = !!cfg.recorder.mic;
        optSystem.value = cfg.recorder.systemAudio !== false;
        optMicVol.value = cfg.recorder.micVolume ?? 1.2;
        optSysVol.value = cfg.recorder.systemVolume ?? 0.8;
      } catch {
      }
    }
    function setOpt(key, value) {
      window.sidekick.config.set(key, value).catch(() => {
      });
    }
    function addRecent(path, type) {
      const name = path.split(/[\\/]/).pop() || "";
      recentFiles.value.unshift({
        name,
        path,
        time: (/* @__PURE__ */ new Date()).toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit" }),
        type
      });
      if (recentFiles.value.length > 5) recentFiles.value.pop();
    }
    async function openFile(path) {
      try {
        await window.sidekick.shell.showItemInFolder(path);
      } catch (e) {
        console.error(e);
      }
    }
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$e, [
        recording.value || starting.value ? (openBlock(), createElementBlock("div", _hoisted_2$d, [
          createBaseVNode("div", _hoisted_3$b, [
            _cache[8] || (_cache[8] = createBaseVNode("span", { class: "rec-dot" }, null, -1)),
            createBaseVNode("span", _hoisted_4$b, toDisplayString(starting.value ? "启动中..." : paused.value ? "已暂停 " + formatTime(elapsed.value) : formatTime(elapsed.value)), 1)
          ]),
          createBaseVNode("div", _hoisted_5$a, [
            recording.value && !paused.value ? (openBlock(), createElementBlock("button", {
              key: 0,
              class: "pause-btn",
              onClick: pauseRecording
            }, "暂停")) : createCommentVNode("", true),
            recording.value && paused.value ? (openBlock(), createElementBlock("button", {
              key: 1,
              class: "pause-btn resume",
              onClick: resumeRecording
            }, "继续")) : createCommentVNode("", true),
            createBaseVNode("button", {
              class: "stop-btn",
              onClick: stopRecording,
              disabled: starting.value
            }, [
              createBaseVNode("img", {
                src: icon("record")
              }, null, 8, _hoisted_7$a),
              createBaseVNode("span", null, toDisplayString(starting.value ? "请稍候..." : "停止录屏"), 1)
            ], 8, _hoisted_6$a)
          ])
        ])) : longshotStep.value === "select" ? (openBlock(), createElementBlock("div", _hoisted_8$8, [
          createBaseVNode("div", _hoisted_9$8, [
            createBaseVNode("img", {
              src: icon("longshot")
            }, null, 8, _hoisted_10$8),
            _cache[9] || (_cache[9] = createBaseVNode("span", null, "选择要截图的窗口", -1))
          ]),
          createBaseVNode("div", _hoisted_11$8, [
            (openBlock(true), createElementBlock(Fragment, null, renderList(availableWindows.value, (w) => {
              return openBlock(), createElementBlock("div", {
                key: w.handle,
                class: "window-item",
                onClick: ($event) => confirmWindow(w)
              }, [
                createBaseVNode("div", _hoisted_13$8, toDisplayString(w.title), 1),
                createBaseVNode("div", _hoisted_14$8, toDisplayString(w.name) + " • PID " + toDisplayString(w.pid) + " • " + toDisplayString(w.width) + "x" + toDisplayString(w.height), 1)
              ], 8, _hoisted_12$8);
            }), 128)),
            availableWindows.value.length === 0 ? (openBlock(), createElementBlock("div", _hoisted_15$7, [..._cache[10] || (_cache[10] = [
              createBaseVNode("p", null, "未检测到可见窗口", -1),
              createBaseVNode("p", { class: "hint" }, "请确保目标窗口未被最小化", -1)
            ])])) : createCommentVNode("", true)
          ]),
          createBaseVNode("button", {
            class: "cancel-btn",
            onClick: cancelLongshot
          }, "取消")
        ])) : longshotStep.value === "countdown" ? (openBlock(), createElementBlock("div", _hoisted_16$7, [
          createBaseVNode("div", _hoisted_17$6, [
            createBaseVNode("div", _hoisted_18$6, toDisplayString(countdownValue.value), 1),
            _cache[11] || (_cache[11] = createBaseVNode("div", { class: "countdown-hint" }, "请在倒计时结束后保持目标窗口可见", -1))
          ])
        ])) : longshotStep.value === "running" ? (openBlock(), createElementBlock("div", _hoisted_19$5, [
          createBaseVNode("div", _hoisted_20$5, [
            _cache[12] || (_cache[12] = createBaseVNode("span", { class: "shot-dot" }, null, -1)),
            createBaseVNode("span", _hoisted_21$5, toDisplayString(longshotStatus.value), 1)
          ]),
          longshotProgress.value > 0 ? (openBlock(), createElementBlock("div", _hoisted_22$5, [
            createBaseVNode("div", {
              class: "progress-fill",
              style: normalizeStyle({ width: longshotProgress.value + "%" })
            }, null, 4)
          ])) : createCommentVNode("", true),
          longshotFrameCount.value > 0 ? (openBlock(), createElementBlock("div", _hoisted_23$5, [
            createTextVNode(" 已截取 " + toDisplayString(longshotFrameCount.value) + " 帧 ", 1),
            longshotTitle.value ? (openBlock(), createElementBlock("span", _hoisted_24$3, " — " + toDisplayString(longshotTitle.value), 1)) : createCommentVNode("", true)
          ])) : createCommentVNode("", true),
          createBaseVNode("button", {
            class: "stop-btn",
            onClick: stopLongshot
          }, [..._cache[13] || (_cache[13] = [
            createBaseVNode("span", null, "停止长截图", -1)
          ])])
        ])) : (openBlock(), createElementBlock("div", _hoisted_25$3, [
          createBaseVNode("button", {
            class: "action-btn",
            onClick: captureRegion
          }, [
            createBaseVNode("img", {
              src: icon("capture")
            }, null, 8, _hoisted_26$3),
            _cache[14] || (_cache[14] = createBaseVNode("span", null, "区域截图", -1))
          ]),
          createBaseVNode("button", {
            class: "action-btn",
            onClick: annotate
          }, [
            createBaseVNode("img", {
              src: icon("annotate")
            }, null, 8, _hoisted_27$3),
            _cache[15] || (_cache[15] = createBaseVNode("span", null, "批注", -1))
          ]),
          createBaseVNode("button", {
            class: "action-btn",
            onClick: startLongshot
          }, [
            createBaseVNode("img", {
              src: icon("longshot")
            }, null, 8, _hoisted_28$3),
            _cache[16] || (_cache[16] = createBaseVNode("span", null, "长截图", -1))
          ]),
          createBaseVNode("button", {
            class: "action-btn",
            onClick: startRecording
          }, [
            createBaseVNode("img", {
              src: icon("record")
            }, null, 8, _hoisted_29$3),
            _cache[17] || (_cache[17] = createBaseVNode("span", null, "开始录屏", -1))
          ])
        ])),
        !recording.value && !starting.value ? (openBlock(), createElementBlock("div", _hoisted_30$3, [
          createBaseVNode("label", _hoisted_31$3, [
            withDirectives(createBaseVNode("input", {
              type: "checkbox",
              "onUpdate:modelValue": _cache[0] || (_cache[0] = ($event) => optMic.value = $event),
              onChange: _cache[1] || (_cache[1] = ($event) => setOpt("recorder.mic", optMic.value))
            }, null, 544), [
              [vModelCheckbox, optMic.value]
            ]),
            _cache[18] || (_cache[18] = createBaseVNode("span", null, "录制麦克风（人声）", -1))
          ]),
          createBaseVNode("label", _hoisted_32$3, [
            withDirectives(createBaseVNode("input", {
              type: "checkbox",
              "onUpdate:modelValue": _cache[2] || (_cache[2] = ($event) => optSystem.value = $event),
              onChange: _cache[3] || (_cache[3] = ($event) => setOpt("recorder.systemAudio", optSystem.value))
            }, null, 544), [
              [vModelCheckbox, optSystem.value]
            ]),
            _cache[19] || (_cache[19] = createBaseVNode("span", null, "录制系统声音（课件/视频）", -1))
          ]),
          optMic.value ? (openBlock(), createElementBlock("div", _hoisted_33$3, [
            _cache[20] || (_cache[20] = createBaseVNode("span", { class: "opt-label" }, "麦克风音量", -1)),
            withDirectives(createBaseVNode("input", {
              type: "range",
              min: "0.5",
              max: "2",
              step: "0.1",
              "onUpdate:modelValue": _cache[4] || (_cache[4] = ($event) => optMicVol.value = $event),
              onChange: _cache[5] || (_cache[5] = ($event) => setOpt("recorder.micVolume", optMicVol.value))
            }, null, 544), [
              [
                vModelText,
                optMicVol.value,
                void 0,
                { number: true }
              ]
            ]),
            createBaseVNode("span", _hoisted_34$3, toDisplayString(optMicVol.value.toFixed(1)), 1)
          ])) : createCommentVNode("", true),
          optSystem.value ? (openBlock(), createElementBlock("div", _hoisted_35$3, [
            _cache[21] || (_cache[21] = createBaseVNode("span", { class: "opt-label" }, "系统音量", -1)),
            withDirectives(createBaseVNode("input", {
              type: "range",
              min: "0.5",
              max: "2",
              step: "0.1",
              "onUpdate:modelValue": _cache[6] || (_cache[6] = ($event) => optSysVol.value = $event),
              onChange: _cache[7] || (_cache[7] = ($event) => setOpt("recorder.systemVolume", optSysVol.value))
            }, null, 544), [
              [
                vModelText,
                optSysVol.value,
                void 0,
                { number: true }
              ]
            ]),
            createBaseVNode("span", _hoisted_36$3, toDisplayString(optSysVol.value.toFixed(1)), 1)
          ])) : createCommentVNode("", true)
        ])) : createCommentVNode("", true),
        recentFiles.value.length > 0 ? (openBlock(), createElementBlock("div", _hoisted_37$3, [
          _cache[22] || (_cache[22] = createBaseVNode("h4", null, "最近文件", -1)),
          createBaseVNode("div", _hoisted_38$3, [
            (openBlock(true), createElementBlock(Fragment, null, renderList(recentFiles.value, (f) => {
              return openBlock(), createElementBlock("div", {
                class: "file-item",
                key: f.path,
                onClick: ($event) => openFile(f.path)
              }, [
                createBaseVNode("span", {
                  class: normalizeClass(["file-type", f.type])
                }, toDisplayString(f.type === "video" ? "录屏" : "截图"), 3),
                createBaseVNode("span", _hoisted_40$3, toDisplayString(f.name), 1),
                createBaseVNode("span", _hoisted_41$3, toDisplayString(f.time), 1)
              ], 8, _hoisted_39$3);
            }), 128))
          ])
        ])) : createCommentVNode("", true)
      ]);
    };
  }
});
const CapturePanel = /* @__PURE__ */ _export_sfc(_sfc_main$e, [["__scopeId", "data-v-3d87d810"]]);
const _hoisted_1$d = { class: "panel-content" };
const _hoisted_2$c = { class: "usb-header" };
const _hoisted_3$a = { class: "usb-status" };
const _hoisted_4$a = ["disabled"];
const _hoisted_5$9 = {
  key: 0,
  class: "spin-icon",
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  "stroke-width": "2"
};
const _hoisted_6$9 = { key: 1 };
const _hoisted_7$9 = {
  key: 0,
  class: "scan-hint"
};
const _hoisted_8$7 = {
  key: 1,
  class: "error-hint"
};
const _hoisted_9$7 = {
  key: 2,
  class: "usb-list"
};
const _hoisted_10$7 = ["src"];
const _hoisted_11$7 = { class: "drive-info" };
const _hoisted_12$7 = { class: "drive-header" };
const _hoisted_13$7 = { class: "drive-letter" };
const _hoisted_14$7 = { class: "drive-label" };
const _hoisted_15$6 = {
  key: 0,
  class: "drive-size"
};
const _hoisted_16$6 = ["onClick"];
const _hoisted_17$5 = {
  key: 3,
  class: "usb-empty"
};
const _hoisted_18$5 = ["src"];
const _hoisted_19$4 = {
  key: 4,
  class: "event-log"
};
const _hoisted_20$4 = { class: "event-drive" };
const _hoisted_21$4 = { class: "event-time" };
const _hoisted_22$4 = {
  key: 5,
  class: "diag-section"
};
const _hoisted_23$4 = { class: "diag-content" };
const _sfc_main$d = /* @__PURE__ */ defineComponent({
  __name: "UsbPanel",
  setup(__props) {
    const drives = /* @__PURE__ */ ref([]);
    const enabled = /* @__PURE__ */ ref(true);
    const scanning = /* @__PURE__ */ ref(false);
    const error = /* @__PURE__ */ ref("");
    const events = /* @__PURE__ */ ref([]);
    const diag = /* @__PURE__ */ ref(null);
    let unsubArrived = null;
    let unsubRemoved = null;
    function icon(name) {
      return new URL((/* @__PURE__ */ Object.assign({ "../assets/icons/annotate.svg": __vite_glob_0_0, "../assets/icons/bell.svg": __vite_glob_0_1, "../assets/icons/capture.svg": __vite_glob_0_2, "../assets/icons/ime.svg": __vite_glob_0_3, "../assets/icons/link.svg": __vite_glob_0_4, "../assets/icons/lock.svg": __vite_glob_0_5, "../assets/icons/longshot.svg": __vite_glob_0_6, "../assets/icons/printer.svg": __vite_glob_0_7, "../assets/icons/record.svg": __vite_glob_0_8, "../assets/icons/reminder.svg": __vite_glob_0_9, "../assets/icons/settings.svg": __vite_glob_0_10, "../assets/icons/sidebar.svg": __vite_glob_0_11, "../assets/icons/taskmgr.svg": __vite_glob_0_12, "../assets/icons/usb.svg": __vite_glob_0_13 }))[`../assets/icons/${name}.svg`], import.meta.url).href;
    }
    function typeLabel(type) {
      if (type === "cdrom") return "光驱";
      if (type === "usb") return "USB";
      return "U盘";
    }
    function formatTime() {
      return (/* @__PURE__ */ new Date()).toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit" });
    }
    async function loadDevices() {
      try {
        const list = await window.sidekick.usb.list();
        drives.value = list || [];
      } catch (e) {
        console.error("[UsbPanel] loadDevices failed:", e);
      }
    }
    async function refresh() {
      if (scanning.value) return;
      scanning.value = true;
      error.value = "";
      try {
        const result = await window.sidekick.usb.scan();
        if (result && result.drives) {
          drives.value = result.drives;
          if (result.drives.length === 0) {
            await loadDevices();
          }
        } else {
          await loadDevices();
        }
      } catch (e) {
        error.value = "扫描失败: " + (e.message || "未知错误");
        console.error("[UsbPanel] refresh error:", e);
      } finally {
        scanning.value = false;
      }
    }
    async function loadDiagnostics() {
      try {
        diag.value = await window.sidekick.usb.getDiagnostics();
      } catch {
      }
    }
    onMounted(async () => {
      try {
        const cfg = await window.sidekick.config.get();
        enabled.value = cfg.usb.enabled;
      } catch {
      }
      await loadDevices();
      await loadDiagnostics();
      unsubArrived = window.sidekick.usb.onArrived((d) => {
        if (!drives.value.find((x) => x.drive === d.drive)) {
          drives.value.push({ drive: d.drive, label: d.label, type: d.type, size: d.size });
        }
        events.value.unshift({ action: "arrived", drive: d.drive, time: formatTime() });
        if (events.value.length > 5) events.value.pop();
      });
      unsubRemoved = window.sidekick.usb.onRemoved((d) => {
        drives.value = drives.value.filter((x) => x.drive !== d.drive);
        events.value.unshift({ action: "removed", drive: d.drive, time: formatTime() });
        if (events.value.length > 5) events.value.pop();
      });
    });
    onUnmounted(() => {
      if (unsubArrived) unsubArrived();
      if (unsubRemoved) unsubRemoved();
    });
    async function openDrive(drive) {
      try {
        await window.sidekick.shell.openPath(drive + "\\");
      } catch (e) {
        console.error(e);
      }
    }
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$d, [
        createBaseVNode("div", _hoisted_2$c, [
          createBaseVNode("div", _hoisted_3$a, [
            _cache[0] || (_cache[0] = createBaseVNode("span", { class: "status-label" }, "USB 监控状态", -1)),
            createBaseVNode("span", {
              class: normalizeClass(["status-badge", enabled.value ? "on" : "off"])
            }, toDisplayString(enabled.value ? "运行中" : "已停止"), 3)
          ]),
          createBaseVNode("button", {
            class: normalizeClass(["refresh-btn", { spinning: scanning.value }]),
            onClick: refresh,
            disabled: scanning.value
          }, [
            scanning.value ? (openBlock(), createElementBlock("svg", _hoisted_5$9, [..._cache[1] || (_cache[1] = [
              createBaseVNode("path", { d: "M23 4v6h-6M1 20v-6h6M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15" }, null, -1)
            ])])) : (openBlock(), createElementBlock("span", _hoisted_6$9, "刷新"))
          ], 10, _hoisted_4$a)
        ]),
        scanning.value ? (openBlock(), createElementBlock("div", _hoisted_7$9, [..._cache[2] || (_cache[2] = [
          createBaseVNode("span", { class: "pulse-dot" }, null, -1),
          createBaseVNode("span", null, "正在扫描 USB 设备...", -1)
        ])])) : createCommentVNode("", true),
        error.value ? (openBlock(), createElementBlock("div", _hoisted_8$7, [
          _cache[3] || (_cache[3] = createBaseVNode("span", { class: "error-icon" }, "!", -1)),
          createBaseVNode("span", null, toDisplayString(error.value), 1)
        ])) : createCommentVNode("", true),
        drives.value.length > 0 ? (openBlock(), createElementBlock("div", _hoisted_9$7, [
          createBaseVNode("h4", null, "已连接设备 (" + toDisplayString(drives.value.length) + ")", 1),
          (openBlock(true), createElementBlock(Fragment, null, renderList(drives.value, (d) => {
            return openBlock(), createElementBlock("div", {
              class: "drive-item",
              key: d.drive
            }, [
              createBaseVNode("img", {
                src: icon("usb"),
                class: "drive-icon"
              }, null, 8, _hoisted_10$7),
              createBaseVNode("div", _hoisted_11$7, [
                createBaseVNode("div", _hoisted_12$7, [
                  createBaseVNode("span", _hoisted_13$7, toDisplayString(d.drive), 1),
                  createBaseVNode("span", {
                    class: normalizeClass(["drive-type", d.type])
                  }, toDisplayString(typeLabel(d.type)), 3)
                ]),
                createBaseVNode("span", _hoisted_14$7, toDisplayString(d.label || "USB 存储设备"), 1),
                d.size ? (openBlock(), createElementBlock("span", _hoisted_15$6, toDisplayString(d.size), 1)) : createCommentVNode("", true)
              ]),
              createBaseVNode("button", {
                class: "open-btn",
                onClick: ($event) => openDrive(d.drive),
                title: "在资源管理器中打开"
              }, [..._cache[4] || (_cache[4] = [
                createBaseVNode("span", null, "打开", -1)
              ])], 8, _hoisted_16$6)
            ]);
          }), 128))
        ])) : (openBlock(), createElementBlock("div", _hoisted_17$5, [
          createBaseVNode("img", {
            src: icon("usb"),
            class: "empty-icon"
          }, null, 8, _hoisted_18$5),
          _cache[5] || (_cache[5] = createBaseVNode("p", null, "暂无 USB 设备连接", -1)),
          _cache[6] || (_cache[6] = createBaseVNode("p", { class: "empty-hint" }, "插入 U 盘后将自动显示，或点击刷新手动检测", -1))
        ])),
        events.value.length > 0 ? (openBlock(), createElementBlock("div", _hoisted_19$4, [
          _cache[7] || (_cache[7] = createBaseVNode("h4", null, "最近事件", -1)),
          (openBlock(true), createElementBlock(Fragment, null, renderList(events.value, (e, i) => {
            return openBlock(), createElementBlock("div", {
              class: "event-item",
              key: i
            }, [
              createBaseVNode("span", {
                class: normalizeClass(["event-icon", e.action])
              }, toDisplayString(e.action === "arrived" ? "+" : "-"), 3),
              createBaseVNode("span", _hoisted_20$4, toDisplayString(e.drive), 1),
              createBaseVNode("span", _hoisted_21$4, toDisplayString(e.time), 1)
            ]);
          }), 128))
        ])) : createCommentVNode("", true),
        diag.value ? (openBlock(), createElementBlock("details", _hoisted_22$4, [
          _cache[8] || (_cache[8] = createBaseVNode("summary", null, "诊断信息", -1)),
          createBaseVNode("pre", _hoisted_23$4, toDisplayString(JSON.stringify(diag.value, null, 2)), 1)
        ])) : createCommentVNode("", true)
      ]);
    };
  }
});
const UsbPanel = /* @__PURE__ */ _export_sfc(_sfc_main$d, [["__scopeId", "data-v-1143585c"]]);
const _hoisted_1$c = { class: "panel-content" };
const _hoisted_2$b = {
  key: 0,
  class: "printer-list"
};
const _hoisted_3$9 = ["src"];
const _hoisted_4$9 = { class: "printer-info" };
const _hoisted_5$8 = { class: "printer-name" };
const _hoisted_6$8 = {
  key: 1,
  class: "printer-empty"
};
const _hoisted_7$8 = ["src"];
const _sfc_main$c = /* @__PURE__ */ defineComponent({
  __name: "PrinterPanel",
  setup(__props) {
    const printers = /* @__PURE__ */ ref([]);
    let unsub = null;
    function icon(name) {
      return new URL((/* @__PURE__ */ Object.assign({ "../assets/icons/annotate.svg": __vite_glob_0_0, "../assets/icons/bell.svg": __vite_glob_0_1, "../assets/icons/capture.svg": __vite_glob_0_2, "../assets/icons/ime.svg": __vite_glob_0_3, "../assets/icons/link.svg": __vite_glob_0_4, "../assets/icons/lock.svg": __vite_glob_0_5, "../assets/icons/longshot.svg": __vite_glob_0_6, "../assets/icons/printer.svg": __vite_glob_0_7, "../assets/icons/record.svg": __vite_glob_0_8, "../assets/icons/reminder.svg": __vite_glob_0_9, "../assets/icons/settings.svg": __vite_glob_0_10, "../assets/icons/sidebar.svg": __vite_glob_0_11, "../assets/icons/taskmgr.svg": __vite_glob_0_12, "../assets/icons/usb.svg": __vite_glob_0_13 }))[`../assets/icons/${name}.svg`], import.meta.url).href;
    }
    function stateText(state) {
      const map = {
        ok: "正常",
        out_of_paper: "缺纸",
        jammed: "卡纸",
        offline: "离线",
        low_ink: "墨量低",
        unknown: "未知"
      };
      return map[state] || "未知";
    }
    onMounted(async () => {
      try {
        const list = await window.sidekick.printer.getStatus();
        printers.value = list || [];
      } catch {
      }
      unsub = window.sidekick.printer.onChanged((status) => {
        const idx = printers.value.findIndex((p2) => p2.name === status.name);
        if (idx >= 0) {
          printers.value[idx] = status;
        } else {
          printers.value.push(status);
        }
      });
    });
    onUnmounted(() => {
      if (unsub) unsub();
    });
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$c, [
        printers.value.length > 0 ? (openBlock(), createElementBlock("div", _hoisted_2$b, [
          (openBlock(true), createElementBlock(Fragment, null, renderList(printers.value, (p2) => {
            return openBlock(), createElementBlock("div", {
              class: "printer-item",
              key: p2.name
            }, [
              createBaseVNode("img", {
                src: icon("printer"),
                class: "printer-icon"
              }, null, 8, _hoisted_3$9),
              createBaseVNode("div", _hoisted_4$9, [
                createBaseVNode("span", _hoisted_5$8, toDisplayString(p2.name), 1),
                createBaseVNode("span", {
                  class: normalizeClass(["printer-state", p2.state])
                }, toDisplayString(stateText(p2.state)), 3)
              ])
            ]);
          }), 128))
        ])) : (openBlock(), createElementBlock("div", _hoisted_6$8, [
          createBaseVNode("img", {
            src: icon("printer"),
            class: "empty-icon"
          }, null, 8, _hoisted_7$8),
          _cache[0] || (_cache[0] = createBaseVNode("p", null, "未检测到打印机", -1))
        ]))
      ]);
    };
  }
});
const PrinterPanel = /* @__PURE__ */ _export_sfc(_sfc_main$c, [["__scopeId", "data-v-13959e26"]]);
const _hoisted_1$b = { class: "panel-content" };
const _hoisted_2$a = {
  key: 0,
  class: "link-list"
};
const _hoisted_3$8 = ["onClick"];
const _hoisted_4$8 = ["src"];
const _hoisted_5$7 = { class: "link-name" };
const _hoisted_6$7 = {
  key: 1,
  class: "edit-form"
};
const _hoisted_7$7 = { class: "edit-actions" };
const _hoisted_8$6 = ["onClick"];
const _hoisted_9$6 = {
  key: 2,
  class: "link-actions"
};
const _hoisted_10$6 = ["onClick", "disabled"];
const _hoisted_11$6 = ["onClick", "disabled"];
const _hoisted_12$6 = ["onClick"];
const _hoisted_13$6 = ["onClick"];
const _hoisted_14$6 = {
  key: 1,
  class: "empty-state"
};
const _hoisted_15$5 = ["src"];
const _hoisted_16$5 = { class: "add-section" };
const _hoisted_17$4 = {
  key: 1,
  class: "add-form"
};
const _hoisted_18$4 = {
  key: 0,
  class: "form-error"
};
const _sfc_main$b = /* @__PURE__ */ defineComponent({
  __name: "LinksPanel",
  setup(__props) {
    const links = /* @__PURE__ */ ref([]);
    const showAddForm = /* @__PURE__ */ ref(false);
    const newName = /* @__PURE__ */ ref("");
    const newUrl = /* @__PURE__ */ ref("");
    const errorMsg = /* @__PURE__ */ ref("");
    const editingId = /* @__PURE__ */ ref(null);
    const editName = /* @__PURE__ */ ref("");
    const editUrl = /* @__PURE__ */ ref("");
    watch([showAddForm, editingId], ([showForm, editId]) => {
      window.__keepSidebarOpen = showForm || editId !== null;
    });
    function icon(name) {
      return new URL((/* @__PURE__ */ Object.assign({ "../assets/icons/annotate.svg": __vite_glob_0_0, "../assets/icons/bell.svg": __vite_glob_0_1, "../assets/icons/capture.svg": __vite_glob_0_2, "../assets/icons/ime.svg": __vite_glob_0_3, "../assets/icons/link.svg": __vite_glob_0_4, "../assets/icons/lock.svg": __vite_glob_0_5, "../assets/icons/longshot.svg": __vite_glob_0_6, "../assets/icons/printer.svg": __vite_glob_0_7, "../assets/icons/record.svg": __vite_glob_0_8, "../assets/icons/reminder.svg": __vite_glob_0_9, "../assets/icons/settings.svg": __vite_glob_0_10, "../assets/icons/sidebar.svg": __vite_glob_0_11, "../assets/icons/taskmgr.svg": __vite_glob_0_12, "../assets/icons/usb.svg": __vite_glob_0_13 }))[`../assets/icons/${name}.svg`], import.meta.url).href;
    }
    async function loadLinks() {
      try {
        const cfg = await window.sidekick.config.get();
        links.value = cfg.links.filter((l) => l.enabled && l.url && l.url.trim() !== "");
      } catch (e) {
        console.error("[Links] Load failed:", e);
      }
    }
    async function openLink(url) {
      if (!url) return;
      const validated = normalizeUrl(url);
      if (!validated) {
        showError("链接地址无效");
        return;
      }
      try {
        await window.sidekick.shell.openExternal(validated);
      } catch (e) {
        console.error(e);
      }
    }
    function normalizeUrl(url) {
      const trimmed = url.trim();
      if (!trimmed) return null;
      if (/^[a-zA-Z0-9][-a-zA-Z0-9]*(\.[a-zA-Z][-a-zA-Z0-9]*)+$/.test(trimmed)) {
        return `https://${trimmed}`;
      }
      if (/^https?:\/\//i.test(trimmed)) return trimmed;
      if (trimmed.includes(".") && !trimmed.includes(" ")) {
        return `https://${trimmed}`;
      }
      return null;
    }
    function showError(msg) {
      errorMsg.value = msg;
      setTimeout(() => errorMsg.value = "", 3e3);
    }
    async function addLink() {
      const name = newName.value.trim();
      const rawUrl = newUrl.value.trim();
      if (!name) {
        showError("请输入链接名称");
        return;
      }
      if (!rawUrl) {
        showError("请输入链接地址");
        return;
      }
      const url = normalizeUrl(rawUrl);
      if (!url) {
        showError("链接格式不正确，请检查是否有空格或无效字符");
        return;
      }
      if (links.value.some((l) => l.url === url)) {
        showError("该链接已存在");
        return;
      }
      const newLink = {
        id: `l${Date.now()}`,
        name,
        url,
        enabled: true
      };
      links.value.push(newLink);
      await saveAll();
      newName.value = "";
      newUrl.value = "";
      showAddForm.value = false;
      errorMsg.value = "";
    }
    function cancelAdd() {
      showAddForm.value = false;
      newName.value = "";
      newUrl.value = "";
      errorMsg.value = "";
    }
    function focusUrl() {
      nextTick(() => {
        const el = document.querySelector('.add-form input[placeholder^="https"]');
        el?.focus();
      });
    }
    function startEdit(link) {
      editingId.value = link.id;
      editName.value = link.name;
      editUrl.value = link.url;
      nextTick(() => {
        const input = document.querySelector(".edit-form input");
        input?.focus();
      });
    }
    function cancelEdit() {
      editingId.value = null;
      editName.value = "";
      editUrl.value = "";
    }
    async function saveEdit(index) {
      const name = editName.value.trim();
      const rawUrl = editUrl.value.trim();
      if (!name) {
        showError("名称不能为空");
        return;
      }
      const url = normalizeUrl(rawUrl);
      if (!url) {
        showError("链接格式不正确");
        return;
      }
      links.value[index].name = name;
      links.value[index].url = url;
      await saveAll();
      editingId.value = null;
    }
    async function removeLink(index) {
      const name = links.value[index].name;
      links.value.splice(index, 1);
      await saveAll();
      console.log(`[Links] Removed: ${name}`);
    }
    function moveUp(index) {
      if (index <= 0) return;
      const tmp = links.value[index];
      links.value[index] = links.value[index - 1];
      links.value[index - 1] = tmp;
      saveAll();
    }
    function moveDown(index) {
      if (index >= links.value.length - 1) return;
      const tmp = links.value[index];
      links.value[index] = links.value[index + 1];
      links.value[index + 1] = tmp;
      saveAll();
    }
    async function saveAll() {
      try {
        const allLinks = links.value.map((l) => ({ ...l, enabled: true }));
        await window.sidekick.config.set("links", allLinks);
      } catch (e) {
        console.error("[Links] Save failed:", e);
        showError("保存失败,请重试");
      }
    }
    onMounted(loadLinks);
    onUnmounted(() => {
      window.__keepSidebarOpen = false;
    });
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$b, [
        links.value.length > 0 ? (openBlock(), createElementBlock("div", _hoisted_2$a, [
          (openBlock(true), createElementBlock(Fragment, null, renderList(links.value, (link, index) => {
            return openBlock(), createElementBlock("div", {
              class: normalizeClass(["link-item", { edit: editingId.value === link.id }]),
              key: link.id
            }, [
              editingId.value !== link.id ? (openBlock(), createElementBlock("div", {
                key: 0,
                class: "link-body",
                onClick: ($event) => openLink(link.url)
              }, [
                createBaseVNode("img", {
                  src: icon("link"),
                  class: "link-icon"
                }, null, 8, _hoisted_4$8),
                createBaseVNode("span", _hoisted_5$7, toDisplayString(link.name), 1)
              ], 8, _hoisted_3$8)) : (openBlock(), createElementBlock("div", _hoisted_6$7, [
                withDirectives(createBaseVNode("input", {
                  ref_for: true,
                  ref: "editInput",
                  "onUpdate:modelValue": _cache[0] || (_cache[0] = ($event) => editName.value = $event),
                  placeholder: "链接名称",
                  class: "form-input"
                }, null, 512), [
                  [vModelText, editName.value]
                ]),
                withDirectives(createBaseVNode("input", {
                  "onUpdate:modelValue": _cache[1] || (_cache[1] = ($event) => editUrl.value = $event),
                  placeholder: "https://...",
                  class: "form-input"
                }, null, 512), [
                  [vModelText, editUrl.value]
                ]),
                createBaseVNode("div", _hoisted_7$7, [
                  createBaseVNode("button", {
                    class: "btn-save",
                    onClick: ($event) => saveEdit(index)
                  }, "保存", 8, _hoisted_8$6),
                  createBaseVNode("button", {
                    class: "btn-cancel",
                    onClick: cancelEdit
                  }, "取消")
                ])
              ])),
              editingId.value !== link.id ? (openBlock(), createElementBlock("div", _hoisted_9$6, [
                createBaseVNode("button", {
                  class: "btn-action up",
                  onClick: withModifiers(($event) => moveUp(index), ["stop"]),
                  disabled: index === 0,
                  title: "上移"
                }, "▲", 8, _hoisted_10$6),
                createBaseVNode("button", {
                  class: "btn-action down",
                  onClick: withModifiers(($event) => moveDown(index), ["stop"]),
                  disabled: index === links.value.length - 1,
                  title: "下移"
                }, "▼", 8, _hoisted_11$6),
                createBaseVNode("button", {
                  class: "btn-action edit",
                  onClick: withModifiers(($event) => startEdit(link), ["stop"]),
                  title: "编辑"
                }, "✎", 8, _hoisted_12$6),
                createBaseVNode("button", {
                  class: "btn-action delete",
                  onClick: withModifiers(($event) => removeLink(index), ["stop"]),
                  title: "删除"
                }, "✕", 8, _hoisted_13$6)
              ])) : createCommentVNode("", true)
            ], 2);
          }), 128))
        ])) : (openBlock(), createElementBlock("div", _hoisted_14$6, [
          createBaseVNode("img", {
            src: icon("link"),
            class: "empty-icon"
          }, null, 8, _hoisted_15$5),
          _cache[5] || (_cache[5] = createBaseVNode("p", null, "暂无快捷链接", -1))
        ])),
        createBaseVNode("div", _hoisted_16$5, [
          !showAddForm.value ? (openBlock(), createElementBlock("button", {
            key: 0,
            class: "btn-add",
            onClick: _cache[2] || (_cache[2] = ($event) => showAddForm.value = true)
          }, [..._cache[6] || (_cache[6] = [
            createBaseVNode("span", { class: "add-icon" }, "+", -1),
            createBaseVNode("span", null, "添加链接", -1)
          ])])) : (openBlock(), createElementBlock("div", _hoisted_17$4, [
            withDirectives(createBaseVNode("input", {
              ref: "addInput",
              "onUpdate:modelValue": _cache[3] || (_cache[3] = ($event) => newName.value = $event),
              placeholder: "链接名称 (如: 希沃白板)",
              class: "form-input",
              onKeyup: withKeys(focusUrl, ["enter"])
            }, null, 544), [
              [vModelText, newName.value]
            ]),
            withDirectives(createBaseVNode("input", {
              ref: "urlInput",
              "onUpdate:modelValue": _cache[4] || (_cache[4] = ($event) => newUrl.value = $event),
              placeholder: "https://www.seewo.com",
              class: "form-input",
              onKeyup: withKeys(addLink, ["enter"])
            }, null, 544), [
              [vModelText, newUrl.value]
            ]),
            errorMsg.value ? (openBlock(), createElementBlock("div", _hoisted_18$4, toDisplayString(errorMsg.value), 1)) : createCommentVNode("", true),
            createBaseVNode("div", { class: "add-actions" }, [
              createBaseVNode("button", {
                class: "btn-save",
                onClick: addLink
              }, "保存"),
              createBaseVNode("button", {
                class: "btn-cancel",
                onClick: cancelAdd
              }, "取消")
            ])
          ]))
        ])
      ]);
    };
  }
});
const LinksPanel = /* @__PURE__ */ _export_sfc(_sfc_main$b, [["__scopeId", "data-v-1cb37a4b"]]);
const _hoisted_1$a = { class: "panel-content" };
const _hoisted_2$9 = { class: "due-bell" };
const _hoisted_3$7 = ["src"];
const _hoisted_4$7 = { class: "due-note" };
const _hoisted_5$6 = { class: "due-actions" };
const _hoisted_6$6 = {
  key: 1,
  class: "form-card"
};
const _hoisted_7$6 = { class: "form-header" };
const _hoisted_8$5 = { class: "form-title" };
const _hoisted_9$5 = { class: "form-body" };
const _hoisted_10$5 = { class: "form-field" };
const _hoisted_11$5 = { class: "form-field" };
const _hoisted_12$5 = { class: "kind-select" };
const _hoisted_13$5 = ["onClick"];
const _hoisted_14$5 = {
  key: 0,
  class: "form-field"
};
const _hoisted_15$4 = {
  key: 1,
  class: "form-field"
};
const _hoisted_16$4 = { class: "interval-row" };
const _hoisted_17$3 = {
  key: 2,
  class: "form-field hint"
};
const _hoisted_18$3 = { class: "form-actions" };
const _hoisted_19$3 = ["disabled"];
const _hoisted_20$3 = {
  key: 2,
  class: "sound-card"
};
const _hoisted_21$3 = { class: "sound-toggle" };
const _hoisted_22$3 = { class: "sound-body" };
const _hoisted_23$3 = { class: "sound-field" };
const _hoisted_24$2 = { class: "preset-select" };
const _hoisted_25$2 = ["onClick"];
const _hoisted_26$2 = { class: "sound-field" };
const _hoisted_27$2 = ["value"];
const _hoisted_28$2 = { class: "sound-field" };
const _hoisted_29$2 = { class: "sound-field" };
const _hoisted_30$2 = { class: "mp3-row" };
const _hoisted_31$2 = {
  key: 0,
  class: "mp3-path"
};
const _hoisted_32$2 = {
  key: 3,
  class: "quick-bar"
};
const _hoisted_33$2 = ["onClick"];
const _hoisted_34$2 = {
  key: 4,
  class: "reminder-list"
};
const _hoisted_35$2 = ["src"];
const _hoisted_36$2 = { class: "reminder-info" };
const _hoisted_37$2 = { class: "reminder-note" };
const _hoisted_38$2 = { class: "reminder-time" };
const _hoisted_39$2 = { class: "reminder-actions" };
const _hoisted_40$2 = ["onClick"];
const _hoisted_41$2 = ["onClick"];
const _hoisted_42$2 = {
  key: 5,
  class: "reminder-empty"
};
const _hoisted_43$2 = ["src"];
const _sfc_main$a = /* @__PURE__ */ defineComponent({
  __name: "ReminderPanel",
  setup(__props) {
    const reminders = /* @__PURE__ */ ref([]);
    const showForm = /* @__PURE__ */ ref(false);
    const editingId = /* @__PURE__ */ ref(null);
    const formNote = /* @__PURE__ */ ref("");
    const formKind = /* @__PURE__ */ ref("once");
    const formTime = /* @__PURE__ */ ref("");
    const formMinutes = /* @__PURE__ */ ref(30);
    const noteInput = /* @__PURE__ */ ref(null);
    const showDueOverlay = /* @__PURE__ */ ref(false);
    const dueItem = /* @__PURE__ */ ref(null);
    const now = /* @__PURE__ */ ref(Date.now());
    const showSoundSettings = /* @__PURE__ */ ref(false);
    const soundConfig = /* @__PURE__ */ ref({
      preset: "default",
      mp3Path: null,
      volume: 0.8,
      repeat: 3,
      repeatInterval: 800
    });
    const kindOptions = [
      { value: "once", label: "一次性" },
      { value: "interval", label: "周期性" },
      { value: "hourly", label: "每小时" }
    ];
    const quickOptions = [
      { label: "5分钟后", minutes: 5 },
      { label: "10分钟后", minutes: 10 },
      { label: "30分钟后", minutes: 30 },
      { label: "1小时后", minutes: 60 }
    ];
    const soundPresets = [
      { value: "default", label: "默认" },
      { value: "bell", label: "叮咚" },
      { value: "alarm", label: "闹钟" },
      { value: "custom", label: "自定义" }
    ];
    let unsubDue = null;
    let timerId = null;
    const canSave = computed(() => {
      if (!formNote.value.trim()) return false;
      if (formKind.value === "once" && !formTime.value) return false;
      if (formKind.value === "interval" && (!formMinutes.value || formMinutes.value < 1)) return false;
      return true;
    });
    const sortedReminders = computed(() => [...reminders.value].sort((a, b) => a.at - b.at));
    function icon(name) {
      return new URL((/* @__PURE__ */ Object.assign({ "../assets/icons/annotate.svg": __vite_glob_0_0, "../assets/icons/bell.svg": __vite_glob_0_1, "../assets/icons/capture.svg": __vite_glob_0_2, "../assets/icons/ime.svg": __vite_glob_0_3, "../assets/icons/link.svg": __vite_glob_0_4, "../assets/icons/lock.svg": __vite_glob_0_5, "../assets/icons/longshot.svg": __vite_glob_0_6, "../assets/icons/printer.svg": __vite_glob_0_7, "../assets/icons/record.svg": __vite_glob_0_8, "../assets/icons/reminder.svg": __vite_glob_0_9, "../assets/icons/settings.svg": __vite_glob_0_10, "../assets/icons/sidebar.svg": __vite_glob_0_11, "../assets/icons/taskmgr.svg": __vite_glob_0_12, "../assets/icons/usb.svg": __vite_glob_0_13 }))[`../assets/icons/${name}.svg`], import.meta.url).href;
    }
    function isDue(r) {
      if (r.snoozedUntil && now.value < r.snoozedUntil) return false;
      return now.value >= r.at;
    }
    function isSoon(r) {
      if (isDue(r)) return false;
      const diff = r.at - now.value;
      return diff > 0 && diff <= 3e5;
    }
    function formatAbsTime(at) {
      const d = new Date(at);
      return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
    }
    function formatRelative(ms) {
      if (ms <= 0) return "已到期";
      const m = Math.ceil(ms / 6e4);
      if (m < 1) return "即将到期";
      if (m < 60) return `${m}分钟后`;
      const h2 = Math.floor(m / 60);
      const rm = m % 60;
      if (rm === 0) return `${h2}小时后`;
      return `${h2}小时${rm}分钟后`;
    }
    function formatTimeFull(r) {
      if (r.kind === "interval" && r.repeatMin) return `每${r.repeatMin}分钟 下次${formatAbsTime(r.at)}`;
      if (r.kind === "hourly") return `每小时 下次${formatAbsTime(r.at)}`;
      const d = new Date(r.at);
      const today = /* @__PURE__ */ new Date();
      const isToday = d.toDateString() === today.toDateString();
      const abs = isToday ? formatAbsTime(r.at) : `${d.getMonth() + 1}/${d.getDate()} ${formatAbsTime(r.at)}`;
      return `${abs} (${formatRelative(r.at - now.value)})`;
    }
    function openForm(edit) {
      showForm.value = true;
      if (edit) {
        editingId.value = edit.id;
        formNote.value = edit.note || "";
        formKind.value = edit.kind;
        if (edit.kind === "once") {
          const d = new Date(edit.at);
          formTime.value = `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
        }
        if (edit.kind === "interval") formMinutes.value = edit.repeatMin || 30;
      } else {
        editingId.value = null;
        formNote.value = "";
        formKind.value = "once";
        const nd = /* @__PURE__ */ new Date();
        nd.setMinutes(nd.getMinutes() + 5);
        formTime.value = `${String(nd.getHours()).padStart(2, "0")}:${String(nd.getMinutes()).padStart(2, "0")}`;
        formMinutes.value = 30;
      }
      nextTick(() => noteInput.value?.focus());
    }
    function closeForm() {
      showForm.value = false;
      editingId.value = null;
    }
    function editReminder(r) {
      openForm(r);
    }
    async function saveReminder() {
      if (!canSave.value) return;
      const note = formNote.value.trim();
      let at = Date.now();
      let repeatMin;
      if (formKind.value === "once") {
        const [h2, m] = formTime.value.split(":").map(Number);
        const target = /* @__PURE__ */ new Date();
        target.setHours(h2, m, 0, 0);
        if (target.getTime() <= Date.now()) target.setDate(target.getDate() + 1);
        at = target.getTime();
      } else if (formKind.value === "interval") {
        repeatMin = formMinutes.value;
        at = Date.now() + repeatMin * 6e4;
      } else if (formKind.value === "hourly") {
        const nh = /* @__PURE__ */ new Date();
        nh.setHours(nh.getHours() + 1, 0, 0, 0);
        at = nh.getTime();
      }
      if (editingId.value) {
        await window.sidekick.reminder.remove(editingId.value);
        reminders.value = reminders.value.filter((r2) => r2.id !== editingId.value);
      }
      const r = { id: editingId.value || `r${Date.now()}`, kind: formKind.value, at, note, repeatMin };
      await window.sidekick.reminder.add(r);
      reminders.value.push(r);
      closeForm();
    }
    async function quickAdd(minutes, label) {
      const r = { id: `r${Date.now()}`, kind: "once", at: Date.now() + minutes * 6e4, note: `${label}的提醒` };
      await window.sidekick.reminder.add(r);
      reminders.value.push(r);
    }
    async function remove2(id) {
      await window.sidekick.reminder.remove(id);
      reminders.value = reminders.value.filter((r) => r.id !== id);
    }
    function dismissDue() {
      showDueOverlay.value = false;
      dueItem.value = null;
    }
    async function snooze(minutes) {
      if (!dueItem.value) return;
      const r = dueItem.value;
      r.snoozedUntil = Date.now() + minutes * 6e4;
      await window.sidekick.reminder.add(r);
      dismissDue();
    }
    async function loadReminders() {
      try {
        reminders.value = await window.sidekick.reminder.list() || [];
      } catch {
      }
    }
    async function loadSoundConfig() {
      try {
        const cfg = await window.sidekick.config.get();
        if (cfg.reminderSound) {
          soundConfig.value = { ...soundConfig.value, ...cfg.reminderSound };
        }
      } catch {
      }
    }
    function setPreset(preset) {
      soundConfig.value.preset = preset;
      saveSoundConfig();
    }
    function setVolume(v) {
      soundConfig.value.volume = Math.max(0, Math.min(1, v));
      saveSoundConfig();
    }
    async function saveSoundConfig() {
      try {
        await window.sidekick.config.set("reminderSound", { ...soundConfig.value });
      } catch {
      }
    }
    async function selectMp3() {
      try {
        const path = await window.sidekick.reminder.selectSound();
        if (path) {
          soundConfig.value.mp3Path = path;
          soundConfig.value.preset = "custom";
          saveSoundConfig();
        }
      } catch {
      }
    }
    function clearMp3() {
      soundConfig.value.mp3Path = null;
      soundConfig.value.preset = "default";
      saveSoundConfig();
    }
    function basename(path) {
      const parts = path.replace(/\\/g, "/").split("/");
      return parts[parts.length - 1] || path;
    }
    async function playTest() {
      try {
        await window.sidekick.reminder.playTest({ ...soundConfig.value });
      } catch {
        playLocalTest();
      }
    }
    function playLocalTest() {
      try {
        const AudioCtx = window.AudioContext || window.webkitAudioContext;
        if (!AudioCtx) return;
        const ctx = new AudioCtx();
        const vol = soundConfig.value.volume;
        const count = soundConfig.value.repeat;
        const interval = soundConfig.value.repeatInterval;
        for (let i = 0; i < count; i++) {
          const t = ctx.currentTime + i * (interval / 1e3);
          const osc = ctx.createOscillator();
          const gain = ctx.createGain();
          osc.connect(gain);
          gain.connect(ctx.destination);
          const freqs = soundConfig.value.preset === "alarm" ? [880, 660, 880] : soundConfig.value.preset === "bell" ? [1047, 1319, 1568] : [880, 1100, 1320];
          osc.frequency.value = freqs[i % freqs.length] || 880;
          gain.gain.setValueAtTime(vol * 0.8, t);
          gain.gain.exponentialRampToValueAtTime(0.01, t + 0.3);
          osc.start(t);
          osc.stop(t + 0.35);
        }
        setTimeout(() => ctx.close(), count * interval + 500);
      } catch {
      }
    }
    onMounted(() => {
      loadReminders();
      loadSoundConfig();
      timerId = setInterval(() => {
        now.value = Date.now();
      }, 3e4);
      unsubDue = window.sidekick.reminder.onDue((r) => {
        dueItem.value = r;
        showDueOverlay.value = true;
        playLocalTest();
      });
    });
    onUnmounted(() => {
      if (timerId) clearInterval(timerId);
      if (unsubDue) unsubDue();
    });
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$a, [
        showDueOverlay.value ? (openBlock(), createElementBlock("div", {
          key: 0,
          class: "due-overlay",
          onClick: dismissDue
        }, [
          createBaseVNode("div", {
            class: "due-card",
            onClick: _cache[2] || (_cache[2] = withModifiers(() => {
            }, ["stop"]))
          }, [
            createBaseVNode("div", _hoisted_2$9, [
              createBaseVNode("img", {
                src: icon("bell"),
                alt: "提醒"
              }, null, 8, _hoisted_3$7)
            ]),
            _cache[10] || (_cache[10] = createBaseVNode("div", { class: "due-title" }, "提醒时间到", -1)),
            createBaseVNode("div", _hoisted_4$7, toDisplayString(dueItem.value?.note || "您有一条提醒"), 1),
            createBaseVNode("div", _hoisted_5$6, [
              createBaseVNode("button", {
                class: "due-btn primary",
                onClick: _cache[0] || (_cache[0] = ($event) => snooze(5))
              }, "5分钟后再次提醒"),
              createBaseVNode("button", {
                class: "due-btn",
                onClick: _cache[1] || (_cache[1] = ($event) => snooze(15))
              }, "15分钟后"),
              createBaseVNode("button", {
                class: "due-btn text",
                onClick: dismissDue
              }, "我知道了")
            ])
          ])
        ])) : createCommentVNode("", true),
        showForm.value ? (openBlock(), createElementBlock("div", _hoisted_6$6, [
          createBaseVNode("div", _hoisted_7$6, [
            createBaseVNode("span", _hoisted_8$5, toDisplayString(editingId.value ? "编辑提醒" : "添加提醒"), 1),
            createBaseVNode("button", {
              class: "form-close",
              onClick: closeForm
            }, "×")
          ]),
          createBaseVNode("div", _hoisted_9$5, [
            createBaseVNode("div", _hoisted_10$5, [
              _cache[11] || (_cache[11] = createBaseVNode("label", null, "提醒内容", -1)),
              withDirectives(createBaseVNode("input", {
                "onUpdate:modelValue": _cache[3] || (_cache[3] = ($event) => formNote.value = $event),
                type: "text",
                placeholder: "例如：下节课是数学课",
                maxlength: "60",
                ref_key: "noteInput",
                ref: noteInput
              }, null, 512), [
                [vModelText, formNote.value]
              ])
            ]),
            createBaseVNode("div", _hoisted_11$5, [
              _cache[12] || (_cache[12] = createBaseVNode("label", null, "提醒类型", -1)),
              createBaseVNode("div", _hoisted_12$5, [
                (openBlock(), createElementBlock(Fragment, null, renderList(kindOptions, (k) => {
                  return createBaseVNode("button", {
                    key: k.value,
                    class: normalizeClass({ active: formKind.value === k.value }),
                    onClick: ($event) => formKind.value = k.value
                  }, toDisplayString(k.label), 11, _hoisted_13$5);
                }), 64))
              ])
            ]),
            formKind.value === "once" ? (openBlock(), createElementBlock("div", _hoisted_14$5, [
              _cache[13] || (_cache[13] = createBaseVNode("label", null, "提醒时间", -1)),
              withDirectives(createBaseVNode("input", {
                "onUpdate:modelValue": _cache[4] || (_cache[4] = ($event) => formTime.value = $event),
                type: "time"
              }, null, 512), [
                [vModelText, formTime.value]
              ])
            ])) : createCommentVNode("", true),
            formKind.value === "interval" ? (openBlock(), createElementBlock("div", _hoisted_15$4, [
              _cache[15] || (_cache[15] = createBaseVNode("label", null, "重复间隔", -1)),
              createBaseVNode("div", _hoisted_16$4, [
                withDirectives(createBaseVNode("input", {
                  "onUpdate:modelValue": _cache[5] || (_cache[5] = ($event) => formMinutes.value = $event),
                  type: "number",
                  min: "1",
                  max: "1440"
                }, null, 512), [
                  [
                    vModelText,
                    formMinutes.value,
                    void 0,
                    { number: true }
                  ]
                ]),
                _cache[14] || (_cache[14] = createBaseVNode("span", null, "分钟", -1))
              ])
            ])) : createCommentVNode("", true),
            formKind.value === "hourly" ? (openBlock(), createElementBlock("div", _hoisted_17$3, "将每个整点触发提醒")) : createCommentVNode("", true)
          ]),
          createBaseVNode("div", _hoisted_18$3, [
            createBaseVNode("button", {
              class: "btn-save",
              disabled: !canSave.value,
              onClick: saveReminder
            }, toDisplayString(editingId.value ? "保存" : "添加"), 9, _hoisted_19$3)
          ])
        ])) : createCommentVNode("", true),
        !showForm.value ? (openBlock(), createElementBlock("div", _hoisted_20$3, [
          createBaseVNode("div", {
            class: "sound-header",
            onClick: _cache[6] || (_cache[6] = ($event) => showSoundSettings.value = !showSoundSettings.value)
          }, [
            _cache[16] || (_cache[16] = createBaseVNode("span", { class: "sound-title" }, "铃声设置", -1)),
            createBaseVNode("span", _hoisted_21$3, toDisplayString(showSoundSettings.value ? "▾" : "▸"), 1)
          ]),
          withDirectives(createBaseVNode("div", _hoisted_22$3, [
            createBaseVNode("div", _hoisted_23$3, [
              _cache[17] || (_cache[17] = createBaseVNode("label", null, "铃声类型", -1)),
              createBaseVNode("div", _hoisted_24$2, [
                (openBlock(), createElementBlock(Fragment, null, renderList(soundPresets, (p2) => {
                  return createBaseVNode("button", {
                    key: p2.value,
                    class: normalizeClass({ active: soundConfig.value.preset === p2.value }),
                    onClick: ($event) => setPreset(p2.value)
                  }, toDisplayString(p2.label), 11, _hoisted_25$2);
                }), 64))
              ])
            ]),
            createBaseVNode("div", _hoisted_26$2, [
              createBaseVNode("label", null, "音量 " + toDisplayString(Math.round(soundConfig.value.volume * 100)) + "%", 1),
              createBaseVNode("input", {
                type: "range",
                min: "0",
                max: "100",
                value: Math.round(soundConfig.value.volume * 100),
                onInput: _cache[7] || (_cache[7] = (e) => setVolume(Number(e.target.value) / 100)),
                class: "volume-slider"
              }, null, 40, _hoisted_27$2)
            ]),
            createBaseVNode("div", _hoisted_28$2, [
              createBaseVNode("label", null, "重复次数 " + toDisplayString(soundConfig.value.repeat) + " 次", 1),
              withDirectives(createBaseVNode("input", {
                type: "range",
                min: "1",
                max: "5",
                "onUpdate:modelValue": _cache[8] || (_cache[8] = ($event) => soundConfig.value.repeat = $event),
                onChange: saveSoundConfig,
                class: "volume-slider"
              }, null, 544), [
                [
                  vModelText,
                  soundConfig.value.repeat,
                  void 0,
                  { number: true }
                ]
              ])
            ]),
            createBaseVNode("div", _hoisted_29$2, [
              _cache[18] || (_cache[18] = createBaseVNode("label", null, "自定义铃声", -1)),
              createBaseVNode("div", _hoisted_30$2, [
                createBaseVNode("button", {
                  class: "btn-mp3",
                  onClick: selectMp3
                }, toDisplayString(soundConfig.value.mp3Path ? "更换 MP3" : "选择 MP3 文件"), 1),
                soundConfig.value.mp3Path ? (openBlock(), createElementBlock("button", {
                  key: 0,
                  class: "btn-mp3 clear",
                  onClick: clearMp3
                }, "清除")) : createCommentVNode("", true)
              ]),
              soundConfig.value.mp3Path ? (openBlock(), createElementBlock("div", _hoisted_31$2, toDisplayString(basename(soundConfig.value.mp3Path)), 1)) : createCommentVNode("", true)
            ]),
            createBaseVNode("div", { class: "sound-field" }, [
              createBaseVNode("button", {
                class: "btn-test",
                onClick: playTest
              }, [..._cache[19] || (_cache[19] = [
                createBaseVNode("span", { class: "test-icon" }, "▶", -1),
                createTextVNode(" 试听铃声 ", -1)
              ])])
            ])
          ], 512), [
            [vShow, showSoundSettings.value]
          ])
        ])) : createCommentVNode("", true),
        !showForm.value ? (openBlock(), createElementBlock("div", _hoisted_32$2, [
          (openBlock(), createElementBlock(Fragment, null, renderList(quickOptions, (q) => {
            return createBaseVNode("button", {
              key: q.label,
              class: "quick-chip",
              onClick: ($event) => quickAdd(q.minutes, q.label)
            }, toDisplayString(q.label), 9, _hoisted_33$2);
          }), 64)),
          createBaseVNode("button", {
            class: "quick-chip custom",
            onClick: _cache[9] || (_cache[9] = ($event) => openForm())
          }, "自定义")
        ])) : createCommentVNode("", true),
        sortedReminders.value.length > 0 ? (openBlock(), createElementBlock("div", _hoisted_34$2, [
          (openBlock(true), createElementBlock(Fragment, null, renderList(sortedReminders.value, (r) => {
            return openBlock(), createElementBlock("div", {
              class: normalizeClass(["reminder-item", { due: isDue(r), soon: isSoon(r) }]),
              key: r.id
            }, [
              createBaseVNode("img", {
                src: icon("reminder"),
                class: "reminder-icon"
              }, null, 8, _hoisted_35$2),
              createBaseVNode("div", _hoisted_36$2, [
                createBaseVNode("span", _hoisted_37$2, toDisplayString(r.note || "提醒"), 1),
                createBaseVNode("span", _hoisted_38$2, toDisplayString(formatTimeFull(r)), 1)
              ]),
              createBaseVNode("div", _hoisted_39$2, [
                createBaseVNode("button", {
                  class: "btn-icon",
                  onClick: ($event) => editReminder(r),
                  title: "编辑"
                }, "✎", 8, _hoisted_40$2),
                createBaseVNode("button", {
                  class: "btn-icon",
                  onClick: ($event) => remove2(r.id),
                  title: "删除"
                }, "×", 8, _hoisted_41$2)
              ])
            ], 2);
          }), 128))
        ])) : (openBlock(), createElementBlock("div", _hoisted_42$2, [
          createBaseVNode("img", {
            src: icon("bell"),
            class: "empty-icon"
          }, null, 8, _hoisted_43$2),
          _cache[20] || (_cache[20] = createBaseVNode("p", null, "暂无提醒", -1)),
          _cache[21] || (_cache[21] = createBaseVNode("p", { class: "empty-hint" }, "点击上方按钮快速添加", -1))
        ]))
      ]);
    };
  }
});
const ReminderPanel = /* @__PURE__ */ _export_sfc(_sfc_main$a, [["__scopeId", "data-v-cd2736f7"]]);
const _hoisted_1$9 = { class: "panel-content" };
const _hoisted_2$8 = ["src"];
const _sfc_main$9 = /* @__PURE__ */ defineComponent({
  __name: "TaskMgrPanel",
  setup(__props) {
    function icon(name) {
      return new URL((/* @__PURE__ */ Object.assign({ "../assets/icons/annotate.svg": __vite_glob_0_0, "../assets/icons/bell.svg": __vite_glob_0_1, "../assets/icons/capture.svg": __vite_glob_0_2, "../assets/icons/ime.svg": __vite_glob_0_3, "../assets/icons/link.svg": __vite_glob_0_4, "../assets/icons/lock.svg": __vite_glob_0_5, "../assets/icons/longshot.svg": __vite_glob_0_6, "../assets/icons/printer.svg": __vite_glob_0_7, "../assets/icons/record.svg": __vite_glob_0_8, "../assets/icons/reminder.svg": __vite_glob_0_9, "../assets/icons/settings.svg": __vite_glob_0_10, "../assets/icons/sidebar.svg": __vite_glob_0_11, "../assets/icons/taskmgr.svg": __vite_glob_0_12, "../assets/icons/usb.svg": __vite_glob_0_13 }))[`../assets/icons/${name}.svg`], import.meta.url).href;
    }
    async function openTaskMgr() {
      try {
        await window.sidekick.app.openTaskMgr();
      } catch (e) {
        console.error(e);
      }
    }
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$9, [
        _cache[1] || (_cache[1] = createBaseVNode("p", { class: "hint" }, "点击下方按钮打开 Windows 任务管理器", -1)),
        createBaseVNode("button", {
          class: "btn-taskmgr",
          onClick: openTaskMgr
        }, [
          createBaseVNode("img", {
            src: icon("taskmgr")
          }, null, 8, _hoisted_2$8),
          _cache[0] || (_cache[0] = createBaseVNode("span", null, "打开任务管理器", -1))
        ])
      ]);
    };
  }
});
const TaskMgrPanel = /* @__PURE__ */ _export_sfc(_sfc_main$9, [["__scopeId", "data-v-c0e79cee"]]);
const _hoisted_1$8 = { class: "panel-content" };
const _hoisted_2$7 = ["src"];
const _hoisted_3$6 = ["src"];
const _hoisted_4$6 = ["src"];
const _hoisted_5$5 = {
  key: 0,
  class: "diag-panel"
};
const _hoisted_6$5 = { class: "diag-header" };
const _hoisted_7$5 = { class: "diag-time" };
const _hoisted_8$4 = { class: "diag-section" };
const _hoisted_9$4 = { class: "diag-info-grid" };
const _hoisted_10$4 = { class: "diag-info-item" };
const _hoisted_11$4 = { class: "info-value" };
const _hoisted_12$4 = { class: "diag-info-item" };
const _hoisted_13$4 = { class: "info-value" };
const _hoisted_14$4 = { class: "diag-info-item" };
const _hoisted_15$3 = { class: "info-value" };
const _hoisted_16$3 = { class: "diag-info-item" };
const _hoisted_17$2 = { class: "info-value" };
const _hoisted_18$2 = { class: "diag-info-item" };
const _hoisted_19$2 = { class: "info-value" };
const _hoisted_20$2 = { class: "diag-info-item" };
const _hoisted_21$2 = { class: "info-value" };
const _hoisted_22$2 = {
  key: 0,
  class: "diag-section"
};
const _hoisted_23$2 = { class: "diag-section-title" };
const _hoisted_24$1 = { class: "diag-screen-list" };
const _hoisted_25$1 = { class: "screen-tag" };
const _hoisted_26$1 = { class: "screen-res" };
const _hoisted_27$1 = { class: "screen-scale" };
const _hoisted_28$1 = { class: "diag-section" };
const _hoisted_29$1 = { class: "diag-memory-bar" };
const _hoisted_30$1 = { class: "diag-memory-text" };
const _hoisted_31$1 = {
  key: 1,
  class: "diag-section"
};
const _hoisted_32$1 = { class: "diag-service-list" };
const _hoisted_33$1 = { class: "svc-name" };
const _hoisted_34$1 = { class: "svc-msg" };
const _hoisted_35$1 = {
  key: 2,
  class: "diag-section"
};
const _hoisted_36$1 = { class: "diag-feature-list" };
const _hoisted_37$1 = { class: "diag-feature-item" };
const _hoisted_38$1 = { class: "diag-feature-item" };
const _hoisted_39$1 = { class: "diag-feature-item" };
const _hoisted_40$1 = {
  key: 3,
  class: "diag-section"
};
const _hoisted_41$1 = { class: "diag-section-title" };
const _hoisted_42$1 = { class: "diag-error-list" };
const _hoisted_43$1 = { class: "about" };
const _hoisted_44$1 = { class: "version" };
const _sfc_main$8 = /* @__PURE__ */ defineComponent({
  __name: "SettingsPanel",
  setup(__props) {
    const autoLaunch = /* @__PURE__ */ ref(true);
    const diagRunning = /* @__PURE__ */ ref(false);
    const diagResult = /* @__PURE__ */ ref(null);
    const memoryPercent = /* @__PURE__ */ ref(0);
    function icon(name) {
      return new URL((/* @__PURE__ */ Object.assign({ "../assets/icons/annotate.svg": __vite_glob_0_0, "../assets/icons/bell.svg": __vite_glob_0_1, "../assets/icons/capture.svg": __vite_glob_0_2, "../assets/icons/ime.svg": __vite_glob_0_3, "../assets/icons/link.svg": __vite_glob_0_4, "../assets/icons/lock.svg": __vite_glob_0_5, "../assets/icons/longshot.svg": __vite_glob_0_6, "../assets/icons/printer.svg": __vite_glob_0_7, "../assets/icons/record.svg": __vite_glob_0_8, "../assets/icons/reminder.svg": __vite_glob_0_9, "../assets/icons/settings.svg": __vite_glob_0_10, "../assets/icons/sidebar.svg": __vite_glob_0_11, "../assets/icons/taskmgr.svg": __vite_glob_0_12, "../assets/icons/usb.svg": __vite_glob_0_13 }))[`../assets/icons/${name}.svg`], import.meta.url).href;
    }
    function openSettings() {
      window.sidekick.window.openSettings();
    }
    async function toggleAutoLaunch() {
      autoLaunch.value = !autoLaunch.value;
      await window.sidekick.power.setAutoLaunch(autoLaunch.value);
    }
    async function runDiagnostics() {
      if (diagRunning.value) return;
      diagRunning.value = true;
      diagResult.value = null;
      try {
        const result = await window.sidekick.diagnostic.runFull();
        if (result) {
          diagResult.value = result;
          if (result.system?.memory?.heapTotal > 0) {
            memoryPercent.value = Math.round(
              result.system.memory.heapUsed / result.system.memory.heapTotal * 100
            );
          }
        }
      } catch (e) {
        console.error("Diagnostics failed:", e);
        diagResult.value = {
          timestamp: (/* @__PURE__ */ new Date()).toLocaleString("zh-CN"),
          version: "2.0.0",
          system: {
            os: "unknown",
            osVersion: "诊断失败",
            platform: "",
            arch: "",
            electron: "",
            chrome: "",
            node: "",
            uptime: 0,
            memory: { rss: 0, heapTotal: 1, heapUsed: 0, external: 0 },
            screens: []
          },
          services: [{ name: "诊断服务", status: "error", message: "无法获取诊断信息: " + String(e) }],
          features: { notification: true, clipboard: true, desktopCapturer: true, autoLaunch: null },
          config: {},
          recentErrors: [String(e)],
          logPath: ""
        };
      } finally {
        diagRunning.value = false;
      }
    }
    async function exportDiagPack() {
      try {
        const path = await window.sidekick.diagnostic.exportPack();
        if (path) {
          await window.sidekick.notification.show({
            title: "诊断包已导出",
            message: path,
            duration: 5e3
          });
        }
      } catch (e) {
        console.error("Export failed:", e);
        await window.sidekick.notification.show({
          title: "导出失败",
          message: String(e),
          duration: 3e3
        });
      }
    }
    function closeDiagnostics() {
      diagResult.value = null;
    }
    function formatUptime(sec) {
      const h2 = Math.floor(sec / 3600);
      const m = Math.floor(sec % 3600 / 60);
      const s = Math.floor(sec % 60);
      return `${h2}h ${m}m ${s}s`;
    }
    function formatBytes(bytes) {
      if (bytes < 1024) return bytes + " B";
      if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + " KB";
      if (bytes < 1024 * 1024 * 1024) return (bytes / 1024 / 1024).toFixed(1) + " MB";
      return (bytes / 1024 / 1024 / 1024).toFixed(2) + " GB";
    }
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$8, [
        createBaseVNode("div", {
          class: "setting-item",
          onClick: openSettings
        }, [
          createBaseVNode("img", {
            src: icon("settings")
          }, null, 8, _hoisted_2$7),
          _cache[0] || (_cache[0] = createBaseVNode("span", null, "打开设置窗口", -1))
        ]),
        createBaseVNode("div", {
          class: "setting-item",
          onClick: toggleAutoLaunch
        }, [
          createBaseVNode("img", {
            src: icon("lock")
          }, null, 8, _hoisted_3$6),
          createBaseVNode("span", null, toDisplayString(autoLaunch.value ? "关闭" : "启用") + "开机自启", 1)
        ]),
        createBaseVNode("div", {
          class: normalizeClass(["setting-item", { running: diagRunning.value }]),
          onClick: runDiagnostics
        }, [
          createBaseVNode("img", {
            src: icon("reminder")
          }, null, 8, _hoisted_4$6),
          createBaseVNode("span", null, toDisplayString(diagRunning.value ? "诊断中..." : "运行诊断"), 1)
        ], 2),
        diagResult.value ? (openBlock(), createElementBlock("div", _hoisted_5$5, [
          createBaseVNode("div", _hoisted_6$5, [
            _cache[1] || (_cache[1] = createBaseVNode("span", { class: "diag-title" }, "诊断结果", -1)),
            createBaseVNode("span", _hoisted_7$5, toDisplayString(diagResult.value.timestamp), 1)
          ]),
          createBaseVNode("div", _hoisted_8$4, [
            _cache[8] || (_cache[8] = createBaseVNode("div", { class: "diag-section-title" }, "系统信息", -1)),
            createBaseVNode("div", _hoisted_9$4, [
              createBaseVNode("div", _hoisted_10$4, [
                _cache[2] || (_cache[2] = createBaseVNode("span", { class: "info-label" }, "版本", -1)),
                createBaseVNode("span", _hoisted_11$4, toDisplayString(diagResult.value.version), 1)
              ]),
              createBaseVNode("div", _hoisted_12$4, [
                _cache[3] || (_cache[3] = createBaseVNode("span", { class: "info-label" }, "系统", -1)),
                createBaseVNode("span", _hoisted_13$4, toDisplayString(diagResult.value.system.osVersion || diagResult.value.system.platform), 1)
              ]),
              createBaseVNode("div", _hoisted_14$4, [
                _cache[4] || (_cache[4] = createBaseVNode("span", { class: "info-label" }, "架构", -1)),
                createBaseVNode("span", _hoisted_15$3, toDisplayString(diagResult.value.system.arch), 1)
              ]),
              createBaseVNode("div", _hoisted_16$3, [
                _cache[5] || (_cache[5] = createBaseVNode("span", { class: "info-label" }, "Electron", -1)),
                createBaseVNode("span", _hoisted_17$2, toDisplayString(diagResult.value.system.electron), 1)
              ]),
              createBaseVNode("div", _hoisted_18$2, [
                _cache[6] || (_cache[6] = createBaseVNode("span", { class: "info-label" }, "Node", -1)),
                createBaseVNode("span", _hoisted_19$2, toDisplayString(diagResult.value.system.node), 1)
              ]),
              createBaseVNode("div", _hoisted_20$2, [
                _cache[7] || (_cache[7] = createBaseVNode("span", { class: "info-label" }, "运行时间", -1)),
                createBaseVNode("span", _hoisted_21$2, toDisplayString(formatUptime(diagResult.value.system.uptime)), 1)
              ])
            ])
          ]),
          diagResult.value.system.screens?.length ? (openBlock(), createElementBlock("div", _hoisted_22$2, [
            createBaseVNode("div", _hoisted_23$2, "显示器 (" + toDisplayString(diagResult.value.system.screens.length) + " 个)", 1),
            createBaseVNode("div", _hoisted_24$1, [
              (openBlock(true), createElementBlock(Fragment, null, renderList(diagResult.value.system.screens, (s, i) => {
                return openBlock(), createElementBlock("div", {
                  key: i,
                  class: normalizeClass(["diag-screen-item", { primary: s.primary }])
                }, [
                  createBaseVNode("span", _hoisted_25$1, toDisplayString(s.primary ? "主屏" : `副屏${i}`), 1),
                  createBaseVNode("span", _hoisted_26$1, toDisplayString(s.bounds.width) + "x" + toDisplayString(s.bounds.height), 1),
                  createBaseVNode("span", _hoisted_27$1, toDisplayString(s.scaleFactor) + "x", 1)
                ], 2);
              }), 128))
            ])
          ])) : createCommentVNode("", true),
          createBaseVNode("div", _hoisted_28$1, [
            _cache[9] || (_cache[9] = createBaseVNode("div", { class: "diag-section-title" }, "内存", -1)),
            createBaseVNode("div", _hoisted_29$1, [
              createBaseVNode("div", {
                class: "diag-memory-fill",
                style: normalizeStyle({ width: memoryPercent.value + "%" })
              }, null, 4)
            ]),
            createBaseVNode("div", _hoisted_30$1, toDisplayString(formatBytes(diagResult.value.system.memory.heapUsed)) + " / " + toDisplayString(formatBytes(diagResult.value.system.memory.heapTotal)), 1)
          ]),
          diagResult.value.services?.length ? (openBlock(), createElementBlock("div", _hoisted_31$1, [
            _cache[10] || (_cache[10] = createBaseVNode("div", { class: "diag-section-title" }, "服务状态", -1)),
            createBaseVNode("div", _hoisted_32$1, [
              (openBlock(true), createElementBlock(Fragment, null, renderList(diagResult.value.services, (svc) => {
                return openBlock(), createElementBlock("div", {
                  key: svc.name,
                  class: normalizeClass(["diag-service-item", svc.status])
                }, [
                  createBaseVNode("span", {
                    class: normalizeClass(["svc-dot", svc.status])
                  }, null, 2),
                  createBaseVNode("span", _hoisted_33$1, toDisplayString(svc.name), 1),
                  createBaseVNode("span", _hoisted_34$1, toDisplayString(svc.message), 1)
                ], 2);
              }), 128))
            ])
          ])) : createCommentVNode("", true),
          diagResult.value.features ? (openBlock(), createElementBlock("div", _hoisted_35$1, [
            _cache[14] || (_cache[14] = createBaseVNode("div", { class: "diag-section-title" }, "功能可用性", -1)),
            createBaseVNode("div", _hoisted_36$1, [
              createBaseVNode("div", _hoisted_37$1, [
                createBaseVNode("span", {
                  class: normalizeClass(["feat-dot", diagResult.value.features.notification ? "ok" : "warn"])
                }, null, 2),
                _cache[11] || (_cache[11] = createBaseVNode("span", null, "系统通知", -1))
              ]),
              createBaseVNode("div", _hoisted_38$1, [
                createBaseVNode("span", {
                  class: normalizeClass(["feat-dot", diagResult.value.features.clipboard ? "ok" : "warn"])
                }, null, 2),
                _cache[12] || (_cache[12] = createBaseVNode("span", null, "剪贴板", -1))
              ]),
              createBaseVNode("div", _hoisted_39$1, [
                createBaseVNode("span", {
                  class: normalizeClass(["feat-dot", diagResult.value.features.desktopCapturer ? "ok" : "warn"])
                }, null, 2),
                _cache[13] || (_cache[13] = createBaseVNode("span", null, "屏幕捕获", -1))
              ])
            ])
          ])) : createCommentVNode("", true),
          diagResult.value.recentErrors?.length ? (openBlock(), createElementBlock("div", _hoisted_40$1, [
            createBaseVNode("div", _hoisted_41$1, "最近错误 (" + toDisplayString(diagResult.value.recentErrors.length) + " 条)", 1),
            createBaseVNode("div", _hoisted_42$1, [
              (openBlock(true), createElementBlock(Fragment, null, renderList(diagResult.value.recentErrors, (err, i) => {
                return openBlock(), createElementBlock("div", {
                  key: i,
                  class: "diag-error-item"
                }, toDisplayString(err), 1);
              }), 128))
            ])
          ])) : createCommentVNode("", true),
          createBaseVNode("div", { class: "diag-actions" }, [
            createBaseVNode("button", {
              class: "btn-export",
              onClick: exportDiagPack
            }, "导出诊断包"),
            createBaseVNode("button", {
              class: "btn-close",
              onClick: closeDiagnostics
            }, "关闭")
          ])
        ])) : createCommentVNode("", true),
        createBaseVNode("div", _hoisted_43$1, [
          createBaseVNode("span", _hoisted_44$1, "v" + toDisplayString(diagResult.value?.version || "2.0.0"), 1)
        ])
      ]);
    };
  }
});
const SettingsPanel = /* @__PURE__ */ _export_sfc(_sfc_main$8, [["__scopeId", "data-v-fc062609"]]);
const _hoisted_1$7 = ["onClick", "title"];
const _hoisted_2$6 = ["src", "alt"];
const _hoisted_3$5 = ["title"];
const _hoisted_4$5 = { class: "dock-arrow" };
const _hoisted_5$4 = { class: "panel-header" };
const _hoisted_6$4 = { class: "panel-title" };
const _hoisted_7$4 = { class: "panel-body" };
const RAIL_WIDTH = 72;
const PANEL_WIDTH = 380;
const _sfc_main$7 = /* @__PURE__ */ defineComponent({
  __name: "SidebarApp",
  setup(__props) {
    const EXPANDED_WIDTH = RAIL_WIDTH + PANEL_WIDTH;
    const isExpanded = /* @__PURE__ */ ref(false);
    const isDocked = /* @__PURE__ */ ref(false);
    const activePanel = /* @__PURE__ */ ref("");
    const imeMode = /* @__PURE__ */ ref("cn");
    const sx = /* @__PURE__ */ ref(0);
    const sy = /* @__PURE__ */ ref(0);
    const lensMap = /* @__PURE__ */ ref("");
    let collapseTimer = null;
    const panelTitle = computed(() => {
      const titles = {
        ime: "输入法",
        capture: "截图与批注",
        usb: "USB 设备",
        printer: "打印机",
        links: "快捷链接",
        reminder: "定时提醒",
        taskmgr: "任务管理",
        settings: "设置"
      };
      return titles[activePanel.value] || "";
    });
    const currentPanel = /* @__PURE__ */ shallowRef(null);
    const panelMap = {
      ime: ImePanel,
      capture: CapturePanel,
      usb: UsbPanel,
      printer: PrinterPanel,
      links: LinksPanel,
      reminder: ReminderPanel,
      taskmgr: TaskMgrPanel,
      settings: SettingsPanel
    };
    const railItems = computed(() => [
      {
        id: "ime",
        panel: "ime",
        title: "输入法切换",
        icon: getIconPath("ime"),
        action: () => toggleIme(),
        badge: imeMode.value === "cn" ? "中" : "EN",
        badgeClass: imeMode.value === "cn" ? "badge-cn" : "badge-en"
      },
      { id: "capture", panel: "capture", title: "区域截图", icon: getIconPath("capture"), action: () => expandPanel("capture") },
      { id: "annotate", panel: "capture", title: "批注", icon: getIconPath("annotate"), action: () => expandPanel("capture") },
      { id: "longshot", panel: "capture", title: "长截图", icon: getIconPath("longshot"), action: () => expandPanel("capture") },
      { id: "record", panel: "capture", title: "录屏", icon: getIconPath("record"), action: () => expandPanel("capture") },
      { id: "usb", panel: "usb", title: "USB 监控", icon: getIconPath("usb"), action: () => expandPanel("usb") },
      { id: "printer", panel: "printer", title: "打印机", icon: getIconPath("printer"), action: () => expandPanel("printer") },
      { id: "taskmgr", panel: "taskmgr", title: "任务管理器", icon: getIconPath("taskmgr"), action: () => openTaskMgr() },
      { id: "link", panel: "links", title: "快捷链接", icon: getIconPath("link"), action: () => expandPanel("links") },
      { id: "bell", panel: "reminder", title: "定时提醒", icon: getIconPath("bell"), action: () => expandPanel("reminder") },
      { id: "lock", panel: "settings", title: "锁屏/电源", icon: getIconPath("lock"), action: () => expandPanel("settings") },
      { id: "settings", panel: "settings", title: "设置", icon: getIconPath("settings"), action: () => expandPanel("settings") }
    ]);
    watch(isExpanded, async (expanded) => {
      if (isDocked.value) return;
      try {
        if (expanded) {
          await window.sidekick.window.resize(EXPANDED_WIDTH, 0);
        } else {
          await window.sidekick.window.resize(RAIL_WIDTH, 0);
        }
      } catch (e) {
        console.warn("[Sidebar] Resize IPC failed:", e);
      }
    });
    function getIconPath(name) {
      return new URL((/* @__PURE__ */ Object.assign({ "../assets/icons/annotate.svg": __vite_glob_0_0, "../assets/icons/bell.svg": __vite_glob_0_1, "../assets/icons/capture.svg": __vite_glob_0_2, "../assets/icons/ime.svg": __vite_glob_0_3, "../assets/icons/link.svg": __vite_glob_0_4, "../assets/icons/lock.svg": __vite_glob_0_5, "../assets/icons/longshot.svg": __vite_glob_0_6, "../assets/icons/printer.svg": __vite_glob_0_7, "../assets/icons/record.svg": __vite_glob_0_8, "../assets/icons/reminder.svg": __vite_glob_0_9, "../assets/icons/settings.svg": __vite_glob_0_10, "../assets/icons/sidebar.svg": __vite_glob_0_11, "../assets/icons/taskmgr.svg": __vite_glob_0_12, "../assets/icons/usb.svg": __vite_glob_0_13 }))[`../assets/icons/${name}.svg`], import.meta.url).href;
    }
    function onMouseEnter() {
      if (isDocked.value) return;
      if (collapseTimer) clearTimeout(collapseTimer);
    }
    function onMouseLeave() {
      if (isDocked.value) return;
      sx.value = 0;
      sy.value = 0;
      if (isExpanded.value) {
        if (window.__keepSidebarOpen) return;
        collapseTimer = setTimeout(() => {
          if (window.__keepSidebarOpen) return;
          collapse();
        }, 5e3);
      }
    }
    function onSidebarMove(e) {
      const r = e.currentTarget.getBoundingClientRect();
      sx.value = Math.max(-100, Math.min(100, (e.clientX - r.left - r.width / 2) / r.width * 100));
      sy.value = Math.max(-100, Math.min(100, (e.clientY - r.top - r.height / 2) / r.height * 100));
    }
    function expandPanel(name) {
      if (isDocked.value) {
        undock();
        setTimeout(() => {
          activePanel.value = name;
          currentPanel.value = panelMap[name];
          isExpanded.value = true;
        }, 350);
        return;
      }
      activePanel.value = name;
      currentPanel.value = panelMap[name];
      isExpanded.value = true;
    }
    function collapse() {
      isExpanded.value = false;
      activePanel.value = "";
      currentPanel.value = null;
    }
    async function toggleDock() {
      if (isDocked.value) {
        await undock();
      } else {
        await dock();
      }
    }
    async function dock() {
      isDocked.value = true;
      isExpanded.value = false;
      activePanel.value = "";
      currentPanel.value = null;
      try {
        await window.sidekick.window.dock();
      } catch (e) {
        console.warn("[Sidebar] dock failed:", e);
      }
    }
    async function undock() {
      isDocked.value = false;
      try {
        await window.sidekick.window.undock();
      } catch (e) {
        console.warn("[Sidebar] undock failed:", e);
      }
    }
    function onRailClick(item) {
      if (isDocked.value) {
        undock();
        setTimeout(() => item.action(), 350);
      } else {
        item.action();
      }
    }
    function onRailDblClick(event) {
      const path = event.composedPath();
      const hitItem = path.some((el) => el instanceof Element && el.classList.contains("rail-item"));
      const hitToggle = path.some((el) => el instanceof Element && el.classList.contains("dock-toggle"));
      if (hitItem || hitToggle) return;
      if (isDocked.value) {
        undock();
      } else {
        dock();
      }
    }
    async function toggleIme() {
      try {
        const state = await window.sidekick.ime.toggle();
        imeMode.value = state.mode;
      } catch (e) {
        console.error("IME toggle failed:", e);
      }
    }
    async function openTaskMgr() {
      try {
        await window.sidekick.app.openTaskMgr();
      } catch (e) {
        console.error("Open taskmgr failed:", e);
      }
    }
    let imeUnsub = null;
    onMounted(async () => {
      lensMap.value = makeLensMap(128);
      try {
        const state = await window.sidekick.ime.getState();
        imeMode.value = state.mode;
      } catch {
      }
      imeUnsub = window.sidekick.ime.onChanged((state) => {
        imeMode.value = state.mode;
      });
    });
    onUnmounted(() => {
      if (imeUnsub) imeUnsub();
    });
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", {
        class: normalizeClass(["sidebar", { docked: isDocked.value }]),
        style: normalizeStyle({ "--lg-mx": String(sx.value), "--lg-my": String(sy.value) }),
        onMouseenter: onMouseEnter,
        onMouseleave: onMouseLeave,
        onMousemove: onSidebarMove
      }, [
        createVNode(LiquidGlassDefs, { "lens-map": lensMap.value }, null, 8, ["lens-map"]),
        createBaseVNode("div", {
          class: normalizeClass(["rail lg-glass lg-thin lg-rim", { docked: isDocked.value }]),
          onDblclick: onRailDblClick
        }, [
          createBaseVNode("div", {
            class: normalizeClass(["rail-inner", { docked: isDocked.value }])
          }, [
            (openBlock(true), createElementBlock(Fragment, null, renderList(railItems.value, (item) => {
              return openBlock(), createElementBlock("div", {
                class: normalizeClass(["rail-item", { active: activePanel.value === item.panel && !isDocked.value }]),
                key: item.id,
                onClick: ($event) => onRailClick(item),
                title: item.title
              }, [
                createBaseVNode("img", {
                  src: item.icon,
                  alt: item.title,
                  class: "rail-icon"
                }, null, 8, _hoisted_2$6),
                item.badge && !isDocked.value ? (openBlock(), createElementBlock("span", {
                  key: 0,
                  class: normalizeClass(["rail-badge", item.badgeClass])
                }, toDisplayString(item.badge), 3)) : createCommentVNode("", true)
              ], 10, _hoisted_1$7);
            }), 128))
          ], 2),
          createBaseVNode("div", {
            class: normalizeClass(["dock-toggle", { docked: isDocked.value }]),
            onClick: toggleDock,
            title: isDocked.value ? "展开侧边栏" : "收起侧边栏"
          }, [
            createBaseVNode("span", _hoisted_4$5, toDisplayString(isDocked.value ? "↑" : "↓"), 1)
          ], 10, _hoisted_3$5)
        ], 34),
        withDirectives(createBaseVNode("div", {
          class: normalizeClass(["panel lg-glass lg-thin lg-rim", { expanded: isExpanded.value }])
        }, [
          createBaseVNode("div", _hoisted_5$4, [
            createBaseVNode("span", _hoisted_6$4, toDisplayString(panelTitle.value), 1),
            createBaseVNode("button", {
              class: "panel-close",
              onClick: collapse
            }, "×")
          ]),
          createBaseVNode("div", _hoisted_7$4, [
            currentPanel.value ? (openBlock(), createBlock(resolveDynamicComponent(currentPanel.value), { key: 0 })) : createCommentVNode("", true)
          ])
        ], 2), [
          [vShow, isExpanded.value && !isDocked.value]
        ])
      ], 38);
    };
  }
});
const SidebarApp = /* @__PURE__ */ _export_sfc(_sfc_main$7, [["__scopeId", "data-v-92633577"]]);
const _hoisted_1$6 = { class: "oobe" };
const _hoisted_2$5 = { class: "oobe-progress" };
const _hoisted_3$4 = { class: "step-num" };
const _hoisted_4$4 = { class: "step-label" };
const _hoisted_5$3 = { class: "oobe-body" };
const _hoisted_6$3 = {
  key: 0,
  class: "step-content"
};
const _hoisted_7$3 = ["src"];
const _hoisted_8$3 = {
  key: 1,
  class: "step-content"
};
const _hoisted_9$3 = { class: "role-cards" };
const _hoisted_10$3 = ["src"];
const _hoisted_11$3 = ["src"];
const _hoisted_12$3 = {
  key: 2,
  class: "step-content"
};
const _hoisted_13$3 = { class: "env-list" };
const _hoisted_14$3 = { class: "env-name" };
const _hoisted_15$2 = {
  key: 3,
  class: "step-content"
};
const _hoisted_16$2 = { class: "pref-list" };
const _hoisted_17$1 = ["onUpdate:modelValue"];
const _hoisted_18$1 = ["src"];
const _hoisted_19$1 = {
  key: 4,
  class: "step-content"
};
const _hoisted_20$1 = { class: "toggle-row" };
const _hoisted_21$1 = {
  key: 5,
  class: "step-content"
};
const _hoisted_22$1 = ["src"];
const _hoisted_23$1 = { class: "oobe-nav" };
const _sfc_main$6 = /* @__PURE__ */ defineComponent({
  __name: "OobeApp",
  setup(__props) {
    const currentStep = /* @__PURE__ */ ref(0);
    const role = /* @__PURE__ */ ref(null);
    const autoLaunch = /* @__PURE__ */ ref(true);
    const steps = [
      { id: 0, title: "欢迎" },
      { id: 1, title: "角色" },
      { id: 2, title: "检测" },
      { id: 3, title: "功能" },
      { id: 4, title: "自启" },
      { id: 5, title: "完成" }
    ];
    const envChecklist = /* @__PURE__ */ ref([
      { key: "screen", label: "屏幕分辨率", status: "ok", text: "检测中..." },
      { key: "ime", label: "输入法服务", status: "ok", text: "检测中..." },
      { key: "printer", label: "打印机", status: "ok", text: "检测中..." },
      { key: "usb", label: "USB 监控", status: "ok", text: "检测中..." }
    ]);
    const prefs = /* @__PURE__ */ ref([
      { key: "ime", label: "输入法切换", icon: "ime", enabled: true },
      { key: "usb", label: "USB 监控", icon: "usb", enabled: true },
      { key: "shot", label: "截图与批注", icon: "capture", enabled: true },
      { key: "recorder", label: "录屏", icon: "record", enabled: false },
      { key: "printer", label: "打印机监控", icon: "printer", enabled: true }
    ]);
    function getIconPath(name) {
      return new URL((/* @__PURE__ */ Object.assign({ "../assets/icons/annotate.svg": __vite_glob_0_0, "../assets/icons/bell.svg": __vite_glob_0_1, "../assets/icons/capture.svg": __vite_glob_0_2, "../assets/icons/ime.svg": __vite_glob_0_3, "../assets/icons/link.svg": __vite_glob_0_4, "../assets/icons/lock.svg": __vite_glob_0_5, "../assets/icons/longshot.svg": __vite_glob_0_6, "../assets/icons/printer.svg": __vite_glob_0_7, "../assets/icons/record.svg": __vite_glob_0_8, "../assets/icons/reminder.svg": __vite_glob_0_9, "../assets/icons/settings.svg": __vite_glob_0_10, "../assets/icons/sidebar.svg": __vite_glob_0_11, "../assets/icons/taskmgr.svg": __vite_glob_0_12, "../assets/icons/usb.svg": __vite_glob_0_13 }))[`../assets/icons/${name}.svg`], import.meta.url).href;
    }
    function prevStep() {
      if (currentStep.value > 0) currentStep.value--;
    }
    function nextStep() {
      if (currentStep.value < 5) currentStep.value++;
    }
    async function skip() {
      await window.sidekick.oobe.setState({ completed: false, skipped: true, lastStepIndex: currentStep.value });
      await window.sidekick.oobe.closeAndOpenMain();
    }
    async function finish() {
      const prefMap = {};
      for (const p2 of prefs.value) {
        prefMap[p2.key] = p2.enabled;
      }
      await window.sidekick.oobe.setState({
        completed: true,
        completedAt: (/* @__PURE__ */ new Date()).toISOString(),
        skipped: false,
        role: role.value,
        lastStepIndex: 5,
        prefs: prefMap
      });
      if (autoLaunch.value) {
        await window.sidekick.power.setAutoLaunch(true);
      }
      await window.sidekick.oobe.closeAndOpenMain();
    }
    onMounted(async () => {
      try {
        const state = await window.sidekick.oobe.getState();
        if (state.lastStepIndex > 0) {
          currentStep.value = state.lastStepIndex;
        }
        if (state.role) role.value = state.role;
      } catch {
      }
      setTimeout(async () => {
        try {
          const displays = await window.sidekick.display.list();
          if (displays && displays.length > 0) {
            const d = displays[0];
            envChecklist.value[0].text = `${d.workArea.width}×${d.workArea.height} @ ${d.scaleFactor}x`;
            envChecklist.value[0].status = "ok";
          }
        } catch {
          envChecklist.value[0].text = "检测失败";
          envChecklist.value[0].status = "warn";
        }
        try {
          const ime = await window.sidekick.ime.getState();
          envChecklist.value[1].text = ime.mode === "cn" ? "中文" : "英文";
          envChecklist.value[1].status = "ok";
        } catch {
          envChecklist.value[1].text = "降级模式";
          envChecklist.value[1].status = "warn";
        }
        envChecklist.value[2].text = "就绪";
        envChecklist.value[3].text = "就绪";
      }, 500);
    });
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$6, [
        createBaseVNode("div", _hoisted_2$5, [
          (openBlock(), createElementBlock(Fragment, null, renderList(steps, (s, i) => {
            return createBaseVNode("div", {
              key: s.id,
              class: normalizeClass(["step-dot", { active: i === currentStep.value, done: i < currentStep.value }])
            }, [
              createBaseVNode("span", _hoisted_3$4, toDisplayString(i < currentStep.value ? "✓" : i + 1), 1),
              createBaseVNode("span", _hoisted_4$4, toDisplayString(s.title), 1)
            ], 2);
          }), 64))
        ]),
        createBaseVNode("div", _hoisted_5$3, [
          currentStep.value === 0 ? (openBlock(), createElementBlock("div", _hoisted_6$3, [
            createBaseVNode("img", {
              src: getIconPath("sidebar"),
              class: "welcome-icon"
            }, null, 8, _hoisted_7$3),
            _cache[3] || (_cache[3] = createBaseVNode("h1", null, "欢迎使用希沃侧边快捷键工具", -1)),
            _cache[4] || (_cache[4] = createBaseVNode("p", null, "一键管理输入法、截图、USB、打印机等常用教学工具", -1)),
            _cache[5] || (_cache[5] = createBaseVNode("p", { class: "hint" }, "本向导将在 3 分钟内完成环境检测与偏好设置", -1))
          ])) : createCommentVNode("", true),
          currentStep.value === 1 ? (openBlock(), createElementBlock("div", _hoisted_8$3, [
            _cache[8] || (_cache[8] = createBaseVNode("h2", null, "请选择您的角色", -1)),
            createBaseVNode("div", _hoisted_9$3, [
              createBaseVNode("div", {
                class: normalizeClass(["role-card", { selected: role.value === "teacher" }]),
                onClick: _cache[0] || (_cache[0] = ($event) => role.value = "teacher")
              }, [
                createBaseVNode("img", {
                  src: getIconPath("bell"),
                  class: "role-icon"
                }, null, 8, _hoisted_10$3),
                _cache[6] || (_cache[6] = createBaseVNode("span", null, "教师", -1))
              ], 2),
              createBaseVNode("div", {
                class: normalizeClass(["role-card", { selected: role.value === "admin" }]),
                onClick: _cache[1] || (_cache[1] = ($event) => role.value = "admin")
              }, [
                createBaseVNode("img", {
                  src: getIconPath("settings"),
                  class: "role-icon"
                }, null, 8, _hoisted_11$3),
                _cache[7] || (_cache[7] = createBaseVNode("span", null, "管理员", -1))
              ], 2)
            ])
          ])) : createCommentVNode("", true),
          currentStep.value === 2 ? (openBlock(), createElementBlock("div", _hoisted_12$3, [
            _cache[9] || (_cache[9] = createBaseVNode("h2", null, "环境检测", -1)),
            createBaseVNode("div", _hoisted_13$3, [
              (openBlock(true), createElementBlock(Fragment, null, renderList(envChecklist.value, (item) => {
                return openBlock(), createElementBlock("div", {
                  class: "env-item",
                  key: item.key
                }, [
                  createBaseVNode("span", _hoisted_14$3, toDisplayString(item.label), 1),
                  createBaseVNode("span", {
                    class: normalizeClass(["env-status", item.status])
                  }, toDisplayString(item.text), 3)
                ]);
              }), 128))
            ])
          ])) : createCommentVNode("", true),
          currentStep.value === 3 ? (openBlock(), createElementBlock("div", _hoisted_15$2, [
            _cache[10] || (_cache[10] = createBaseVNode("h2", null, "选择需要启用的功能", -1)),
            createBaseVNode("div", _hoisted_16$2, [
              (openBlock(true), createElementBlock(Fragment, null, renderList(prefs.value, (p2) => {
                return openBlock(), createElementBlock("label", {
                  class: "pref-item",
                  key: p2.key
                }, [
                  withDirectives(createBaseVNode("input", {
                    type: "checkbox",
                    "onUpdate:modelValue": ($event) => p2.enabled = $event
                  }, null, 8, _hoisted_17$1), [
                    [vModelCheckbox, p2.enabled]
                  ]),
                  createBaseVNode("img", {
                    src: getIconPath(p2.icon),
                    class: "pref-icon"
                  }, null, 8, _hoisted_18$1),
                  createBaseVNode("span", null, toDisplayString(p2.label), 1)
                ]);
              }), 128))
            ])
          ])) : createCommentVNode("", true),
          currentStep.value === 4 ? (openBlock(), createElementBlock("div", _hoisted_19$1, [
            _cache[13] || (_cache[13] = createBaseVNode("h2", null, "开机自启动", -1)),
            _cache[14] || (_cache[14] = createBaseVNode("p", null, "建议启用,确保每次开机后侧边栏自动就绪", -1)),
            createBaseVNode("div", _hoisted_20$1, [
              _cache[12] || (_cache[12] = createBaseVNode("span", null, "开机自动启动", -1)),
              createBaseVNode("button", {
                class: normalizeClass(["toggle", { on: autoLaunch.value }]),
                onClick: _cache[2] || (_cache[2] = ($event) => autoLaunch.value = !autoLaunch.value)
              }, [..._cache[11] || (_cache[11] = [
                createBaseVNode("span", { class: "toggle-knob" }, null, -1)
              ])], 2)
            ])
          ])) : createCommentVNode("", true),
          currentStep.value === 5 ? (openBlock(), createElementBlock("div", _hoisted_21$1, [
            createBaseVNode("img", {
              src: getIconPath("bell"),
              class: "welcome-icon"
            }, null, 8, _hoisted_22$1),
            _cache[15] || (_cache[15] = createBaseVNode("h1", null, "设置完成!", -1)),
            _cache[16] || (_cache[16] = createBaseVNode("p", null, "侧边栏已就绪,鼠标移至屏幕边缘即可展开", -1)),
            _cache[17] || (_cache[17] = createBaseVNode("p", { class: "hint" }, "您可以随时在设置中修改配置", -1))
          ])) : createCommentVNode("", true)
        ]),
        createBaseVNode("div", _hoisted_23$1, [
          currentStep.value > 0 ? (openBlock(), createElementBlock("button", {
            key: 0,
            class: "btn-secondary",
            onClick: prevStep
          }, "上一步")) : createCommentVNode("", true),
          currentStep.value < 5 ? (openBlock(), createElementBlock("button", {
            key: 1,
            class: "btn-primary",
            onClick: nextStep
          }, "下一步")) : createCommentVNode("", true),
          currentStep.value === 5 ? (openBlock(), createElementBlock("button", {
            key: 2,
            class: "btn-primary",
            onClick: finish
          }, "开始使用")) : createCommentVNode("", true),
          createBaseVNode("button", {
            class: "btn-skip",
            onClick: skip
          }, "跳过")
        ])
      ]);
    };
  }
});
const OobeApp = /* @__PURE__ */ _export_sfc(_sfc_main$6, [["__scopeId", "data-v-dacf426a"]]);
const _hoisted_1$5 = {
  key: 0,
  class: "size-label"
};
const _hoisted_2$4 = {
  key: 2,
  class: "hint-bar"
};
const JITTER_PX = 4;
const JITTER_MS = 80;
const _sfc_main$5 = /* @__PURE__ */ defineComponent({
  __name: "OverlayApp",
  setup(__props) {
    const isSelecting = /* @__PURE__ */ ref(false);
    const hasSelected = /* @__PURE__ */ ref(false);
    const startX = /* @__PURE__ */ ref(0);
    const startY = /* @__PURE__ */ ref(0);
    const endX = /* @__PURE__ */ ref(0);
    const endY = /* @__PURE__ */ ref(0);
    let lastMoveX = 0;
    let lastMoveY = 0;
    let lastMoveTime = 0;
    const selLeft = computed(() => Math.min(startX.value, endX.value));
    const selTop = computed(() => Math.min(startY.value, endY.value));
    const selWidth = computed(() => Math.abs(endX.value - startX.value));
    const selHeight = computed(() => Math.abs(endY.value - startY.value));
    const selectionStyle = computed(() => ({
      left: selLeft.value + "px",
      top: selTop.value + "px",
      width: selWidth.value + "px",
      height: selHeight.value + "px"
    }));
    const actionBarStyle = computed(() => {
      let top = selTop.value + selHeight.value + 8;
      if (top + 40 > window.innerHeight) {
        top = selTop.value - 44;
      }
      let left = selLeft.value + selWidth.value / 2;
      left = Math.max(120, Math.min(left, window.innerWidth - 120));
      return {
        left: left + "px",
        top: top + "px",
        transform: "translateX(-50%)"
      };
    });
    let unsubInit = null;
    onMounted(() => {
      window.sidekick.overlay.ready();
      unsubInit = window.sidekick.overlay.onInit((_init) => {
      });
      document.addEventListener("keydown", onKey);
      document.addEventListener("contextmenu", preventDefault);
    });
    onUnmounted(() => {
      if (unsubInit) unsubInit();
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("contextmenu", preventDefault);
    });
    function onKey(e) {
      if (e.key === "Escape") {
        cancelSelection();
      }
    }
    function preventDefault(e) {
      e.preventDefault();
    }
    function onMouseDown(e) {
      if (hasSelected.value && !isInSelection(e.clientX, e.clientY)) {
        resetSelection();
      }
      if (hasSelected.value) return;
      beginSelection(e.clientX, e.clientY);
    }
    function onMouseMove(e) {
      if (!isSelecting.value) return;
      updateSelection(e.clientX, e.clientY);
    }
    function onMouseUp(_e) {
      finishSelection();
    }
    function onTouchStart(e) {
      if (e.touches.length !== 1) return;
      e.preventDefault();
      const t = e.touches[0];
      if (hasSelected.value && !isInSelection(t.clientX, t.clientY)) {
        resetSelection();
      }
      if (hasSelected.value) return;
      beginSelection(t.clientX, t.clientY);
    }
    function onTouchMove(e) {
      if (!isSelecting.value || e.touches.length !== 1) return;
      e.preventDefault();
      const t = e.touches[0];
      updateSelection(t.clientX, t.clientY);
    }
    function onTouchEnd(_e) {
      finishSelection();
    }
    function beginSelection(x, y) {
      isSelecting.value = true;
      startX.value = x;
      startY.value = y;
      endX.value = x;
      endY.value = y;
      lastMoveX = x;
      lastMoveY = y;
      lastMoveTime = Date.now();
    }
    function updateSelection(x, y) {
      const now = Date.now();
      const dx = Math.abs(x - lastMoveX);
      const dy = Math.abs(y - lastMoveY);
      const dt = now - lastMoveTime;
      if (dx < JITTER_PX && dy < JITTER_PX && dt < JITTER_MS) {
        return;
      }
      endX.value = x;
      endY.value = y;
      lastMoveX = x;
      lastMoveY = y;
      lastMoveTime = now;
    }
    function finishSelection() {
      if (!isSelecting.value) return;
      isSelecting.value = false;
      if (selWidth.value > 5 && selHeight.value > 5) {
        hasSelected.value = true;
      } else {
        hasSelected.value = false;
      }
    }
    function isInSelection(x, y) {
      return x >= selLeft.value && x <= selLeft.value + selWidth.value && y >= selTop.value && y <= selTop.value + selHeight.value;
    }
    function confirmSelection() {
      window.sidekick.overlay.sendRegion({
        x: selLeft.value,
        y: selTop.value,
        width: selWidth.value,
        height: selHeight.value
      });
    }
    function resetSelection() {
      hasSelected.value = false;
      isSelecting.value = false;
      startX.value = 0;
      startY.value = 0;
      endX.value = 0;
      endY.value = 0;
    }
    function cancelSelection() {
      window.sidekick.overlay.cancel();
    }
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", {
        class: "overlay-root",
        onMousedown: onMouseDown,
        onMousemove: onMouseMove,
        onMouseup: onMouseUp,
        onTouchstart: onTouchStart,
        onTouchmove: onTouchMove,
        onTouchend: onTouchEnd
      }, [
        _cache[5] || (_cache[5] = createBaseVNode("div", { class: "overlay-mask" }, null, -1)),
        isSelecting.value || hasSelected.value ? (openBlock(), createElementBlock("div", {
          key: 0,
          class: "selection-box",
          style: normalizeStyle(selectionStyle.value)
        }, [
          _cache[0] || (_cache[0] = createBaseVNode("div", { class: "corner tl" }, null, -1)),
          _cache[1] || (_cache[1] = createBaseVNode("div", { class: "corner tr" }, null, -1)),
          _cache[2] || (_cache[2] = createBaseVNode("div", { class: "corner bl" }, null, -1)),
          _cache[3] || (_cache[3] = createBaseVNode("div", { class: "corner br" }, null, -1)),
          isSelecting.value ? (openBlock(), createElementBlock("div", _hoisted_1$5, toDisplayString(Math.round(selWidth.value)) + " x " + toDisplayString(Math.round(selHeight.value)), 1)) : createCommentVNode("", true)
        ], 4)) : createCommentVNode("", true),
        hasSelected.value && !isSelecting.value ? (openBlock(), createElementBlock("div", {
          key: 1,
          class: "action-bar",
          style: normalizeStyle(actionBarStyle.value)
        }, [
          createBaseVNode("button", {
            class: "btn confirm",
            onClick: withModifiers(confirmSelection, ["stop"])
          }, "确认截图"),
          createBaseVNode("button", {
            class: "btn redo",
            onClick: withModifiers(resetSelection, ["stop"])
          }, "重新选择"),
          createBaseVNode("button", {
            class: "btn cancel",
            onClick: withModifiers(cancelSelection, ["stop"])
          }, "取消")
        ], 4)) : createCommentVNode("", true),
        !hasSelected.value ? (openBlock(), createElementBlock("div", _hoisted_2$4, [..._cache[4] || (_cache[4] = [
          createBaseVNode("span", { class: "hint-text" }, "拖拽选择截图区域", -1),
          createBaseVNode("span", { class: "hint-key" }, "ESC 取消", -1)
        ])])) : createCommentVNode("", true)
      ], 32);
    };
  }
});
const OverlayApp = /* @__PURE__ */ _export_sfc(_sfc_main$5, [["__scopeId", "data-v-aaf061b8"]]);
const _hoisted_1$4 = {
  key: 0,
  class: "transparent-indicator"
};
const _hoisted_2$3 = ["width", "height"];
const _hoisted_3$3 = {
  key: 2,
  class: "toolbar"
};
const _hoisted_4$3 = { class: "tool-group" };
const _hoisted_5$2 = ["onClick", "title"];
const _hoisted_6$2 = { class: "tool-icon" };
const _hoisted_7$2 = { class: "color-group" };
const _hoisted_8$2 = ["onClick"];
const _hoisted_9$2 = { class: "size-group" };
const _hoisted_10$2 = ["onClick", "title"];
const _hoisted_11$2 = {
  key: 0,
  class: "bg-control"
};
const _hoisted_12$2 = ["title"];
const _hoisted_13$2 = {
  key: 1,
  class: "divider"
};
const _hoisted_14$2 = ["disabled"];
const MAX_UNDO = 30;
const _sfc_main$4 = /* @__PURE__ */ defineComponent({
  __name: "AnnotateApp",
  setup(__props) {
    const canvasRef = /* @__PURE__ */ ref(null);
    const textInputRef = /* @__PURE__ */ ref(null);
    const ready = /* @__PURE__ */ ref(false);
    const isTransparent = /* @__PURE__ */ ref(false);
    const bgOpacity = /* @__PURE__ */ ref(0);
    const dipWidth = /* @__PURE__ */ ref(window.innerWidth);
    const dipHeight = /* @__PURE__ */ ref(window.innerHeight);
    const canvasWidth = /* @__PURE__ */ ref(0);
    const canvasHeight = /* @__PURE__ */ ref(0);
    const scaleFactor = /* @__PURE__ */ ref(1);
    const currentTool = /* @__PURE__ */ ref("pen");
    const currentColor = /* @__PURE__ */ ref("#e74c3c");
    const currentSize = /* @__PURE__ */ ref(3);
    const isDrawing = /* @__PURE__ */ ref(false);
    const startX = /* @__PURE__ */ ref(0);
    const startY = /* @__PURE__ */ ref(0);
    let savedImageData = null;
    const undoStack = [];
    const textEditing = /* @__PURE__ */ ref(false);
    const textInput = /* @__PURE__ */ ref("");
    const textX = /* @__PURE__ */ ref(0);
    const textY = /* @__PURE__ */ ref(0);
    const textInputStyle = /* @__PURE__ */ ref({});
    const tools = [
      { id: "pen", label: "画笔", icon: "✏️" },
      { id: "highlighter", label: "高亮笔", icon: "🖊️" },
      { id: "arrow", label: "箭头", icon: "➤" },
      { id: "rect", label: "矩形", icon: "▭" },
      { id: "text", label: "文字", icon: "T" },
      { id: "eraser", label: "橡皮擦", icon: "🧹" }
    ];
    const colors = ["#e74c3c", "#2B6EE0", "#27ae60", "#f39c12", "#ffffff", "#000000"];
    const sizes = [
      { value: 2, label: "细" },
      { value: 4, label: "中" },
      { value: 8, label: "粗" }
    ];
    const cursorStyle = computed(() => {
      if (currentTool.value === "text") return "text";
      if (currentTool.value === "eraser") return "cell";
      return "crosshair";
    });
    const rootStyle = computed(() => {
      return {
        width: dipWidth.value + "px",
        height: dipHeight.value + "px",
        backgroundColor: isTransparent.value ? "transparent" : "#000"
      };
    });
    let unsubInit = null;
    onMounted(() => {
      window.sidekick.overlay.ready();
      unsubInit = window.sidekick.overlay.onInit((init) => {
        if (init.mode !== "annotate") return;
        scaleFactor.value = init.scaleFactor || 1;
        dipWidth.value = init.dipWidth || window.innerWidth;
        dipHeight.value = init.dipHeight || window.innerHeight;
        canvasWidth.value = Math.round(dipWidth.value * scaleFactor.value);
        canvasHeight.value = Math.round(dipHeight.value * scaleFactor.value);
        if (init.transparent) {
          isTransparent.value = true;
          ready.value = true;
          nextTick(() => initCanvas());
          return;
        }
        if (init.screenshotDataUrl) {
          isTransparent.value = true;
          ready.value = true;
          nextTick(() => initCanvas());
        }
      });
      document.addEventListener("keydown", onKey);
    });
    onUnmounted(() => {
      if (unsubInit) unsubInit();
      document.removeEventListener("keydown", onKey);
    });
    function initCanvas() {
      const canvas = canvasRef.value;
      if (!canvas) return;
      const ctx = canvas.getContext("2d");
      if (!ctx) return;
      ctx.lineCap = "round";
      ctx.lineJoin = "round";
    }
    function toggleBg() {
      bgOpacity.value = bgOpacity.value > 0 ? 0 : 40;
    }
    function onKey(e) {
      if (e.key === "Escape") {
        if (textEditing.value) {
          cancelText();
        } else {
          cancel();
        }
      }
      if (e.ctrlKey && e.key === "z") {
        e.preventDefault();
        undo();
      }
    }
    function getCanvasCoords(e) {
      const canvas = canvasRef.value;
      const rect = canvas.getBoundingClientRect();
      const scaleX = canvas.width / rect.width;
      const scaleY = canvas.height / rect.height;
      return {
        x: (e.clientX - rect.left) * scaleX,
        y: (e.clientY - rect.top) * scaleY
      };
    }
    function saveUndoState() {
      const canvas = canvasRef.value;
      if (!canvas) return;
      const ctx = canvas.getContext("2d");
      if (!ctx) return;
      const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      undoStack.push(imageData);
      if (undoStack.length > MAX_UNDO) {
        undoStack.shift();
      }
    }
    function onMouseDown(e) {
      if (!ready.value) return;
      const { x, y } = getCanvasCoords(e);
      if (currentTool.value === "text") {
        textEditing.value = true;
        textInput.value = "";
        textX.value = e.clientX;
        textY.value = e.clientY;
        textInputStyle.value = {
          left: e.clientX + "px",
          top: e.clientY - 14 + "px",
          color: currentColor.value,
          fontSize: currentSize.value * 6 + "px"
        };
        nextTick(() => textInputRef.value?.focus());
        return;
      }
      saveUndoState();
      isDrawing.value = true;
      startX.value = x;
      startY.value = y;
      const ctx = canvasRef.value.getContext("2d");
      if (currentTool.value === "pen" || currentTool.value === "highlighter" || currentTool.value === "eraser") {
        ctx.beginPath();
        ctx.moveTo(x, y);
        if (currentTool.value === "highlighter") {
          ctx.globalAlpha = 0.35;
          ctx.lineWidth = currentSize.value * 5;
        } else if (currentTool.value === "eraser") {
          ctx.globalCompositeOperation = "destination-out";
          ctx.lineWidth = currentSize.value * 8;
        } else {
          ctx.globalAlpha = 1;
          ctx.lineWidth = currentSize.value;
        }
        ctx.strokeStyle = currentColor.value;
      } else if (currentTool.value === "arrow" || currentTool.value === "rect") {
        savedImageData = ctx.getImageData(0, 0, canvasRef.value.width, canvasRef.value.height);
      }
    }
    function onMouseMove(e) {
      if (!isDrawing.value || !ready.value) return;
      const { x, y } = getCanvasCoords(e);
      const ctx = canvasRef.value.getContext("2d");
      if (currentTool.value === "pen" || currentTool.value === "highlighter" || currentTool.value === "eraser") {
        ctx.lineTo(x, y);
        ctx.stroke();
      } else if (currentTool.value === "arrow") {
        if (savedImageData) ctx.putImageData(savedImageData, 0, 0);
        ctx.globalAlpha = 1;
        ctx.globalCompositeOperation = "source-over";
        ctx.lineWidth = currentSize.value;
        ctx.strokeStyle = currentColor.value;
        drawArrow(ctx, startX.value, startY.value, x, y);
      } else if (currentTool.value === "rect") {
        if (savedImageData) ctx.putImageData(savedImageData, 0, 0);
        ctx.globalAlpha = 1;
        ctx.globalCompositeOperation = "source-over";
        ctx.lineWidth = currentSize.value;
        ctx.strokeStyle = currentColor.value;
        ctx.strokeRect(
          Math.min(startX.value, x),
          Math.min(startY.value, y),
          Math.abs(x - startX.value),
          Math.abs(y - startY.value)
        );
      }
    }
    function onMouseUp(e) {
      if (!isDrawing.value) return;
      isDrawing.value = false;
      const ctx = canvasRef.value.getContext("2d");
      ctx.globalAlpha = 1;
      ctx.globalCompositeOperation = "source-over";
      if (currentTool.value === "arrow" || currentTool.value === "rect") {
        savedImageData = null;
      }
    }
    function drawArrow(ctx, x1, y1, x2, y2) {
      const headLen = 20;
      const angle = Math.atan2(y2 - y1, x2 - x1);
      ctx.beginPath();
      ctx.moveTo(x1, y1);
      ctx.lineTo(x2, y2);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(x2, y2);
      ctx.lineTo(x2 - headLen * Math.cos(angle - Math.PI / 6), y2 - headLen * Math.sin(angle - Math.PI / 6));
      ctx.moveTo(x2, y2);
      ctx.lineTo(x2 - headLen * Math.cos(angle + Math.PI / 6), y2 - headLen * Math.sin(angle + Math.PI / 6));
      ctx.stroke();
    }
    function commitText() {
      if (!textEditing.value || !textInput.value.trim()) {
        textEditing.value = false;
        return;
      }
      const canvas = canvasRef.value;
      if (!canvas) {
        textEditing.value = false;
        return;
      }
      const ctx = canvas.getContext("2d");
      saveUndoState();
      const fontSize = currentSize.value * 6;
      ctx.globalAlpha = 1;
      ctx.globalCompositeOperation = "source-over";
      ctx.fillStyle = currentColor.value;
      ctx.font = `bold ${fontSize}px "Microsoft YaHei UI", "Segoe UI", sans-serif`;
      ctx.textBaseline = "top";
      const rect = canvas.getBoundingClientRect();
      const scaleX = canvas.width / rect.width;
      const scaleY = canvas.height / rect.height;
      const cx = textX.value * scaleX;
      const cy = (textY.value - 14) * scaleY;
      ctx.fillText(textInput.value, cx, cy);
      textEditing.value = false;
      textInput.value = "";
    }
    function cancelText() {
      textEditing.value = false;
      textInput.value = "";
    }
    function undo() {
      if (undoStack.length === 0) return;
      const canvas = canvasRef.value;
      if (!canvas) return;
      const ctx = canvas.getContext("2d");
      const imageData = undoStack.pop();
      ctx.putImageData(imageData, 0, 0);
    }
    function save() {
      const canvas = canvasRef.value;
      if (!canvas) return;
      const dataUrl = canvas.toDataURL("image/png");
      window.sidekick.overlay.saveAnnotate(dataUrl);
    }
    function cancel() {
      window.sidekick.overlay.cancel();
    }
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", {
        class: normalizeClass(["annotate-root", { "with-bg": bgOpacity.value > 0 }]),
        style: normalizeStyle(rootStyle.value)
      }, [
        isTransparent.value ? (openBlock(), createElementBlock("div", _hoisted_1$4, [..._cache[2] || (_cache[2] = [
          createBaseVNode("span", { class: "indicator-dot" }, null, -1),
          createBaseVNode("span", { class: "indicator-text" }, "透明绘图模式", -1)
        ])])) : createCommentVNode("", true),
        bgOpacity.value > 0 ? (openBlock(), createElementBlock("div", {
          key: 1,
          class: "bg-mask",
          style: normalizeStyle({ opacity: bgOpacity.value / 100 })
        }, null, 4)) : createCommentVNode("", true),
        createBaseVNode("canvas", {
          ref_key: "canvasRef",
          ref: canvasRef,
          width: canvasWidth.value,
          height: canvasHeight.value,
          class: "draw-canvas",
          style: normalizeStyle({ width: dipWidth.value + "px", height: dipHeight.value + "px", cursor: cursorStyle.value }),
          onMousedown: onMouseDown,
          onMousemove: onMouseMove,
          onMouseup: onMouseUp
        }, null, 44, _hoisted_2$3),
        ready.value ? (openBlock(), createElementBlock("div", _hoisted_3$3, [
          createBaseVNode("div", _hoisted_4$3, [
            (openBlock(), createElementBlock(Fragment, null, renderList(tools, (t) => {
              return createBaseVNode("button", {
                key: t.id,
                class: normalizeClass(["tool-btn", { active: currentTool.value === t.id }]),
                onClick: ($event) => currentTool.value = t.id,
                title: t.label
              }, [
                createBaseVNode("span", _hoisted_6$2, toDisplayString(t.icon), 1)
              ], 10, _hoisted_5$2);
            }), 64))
          ]),
          _cache[6] || (_cache[6] = createBaseVNode("div", { class: "divider" }, null, -1)),
          createBaseVNode("div", _hoisted_7$2, [
            (openBlock(), createElementBlock(Fragment, null, renderList(colors, (c) => {
              return createBaseVNode("button", {
                key: c,
                class: normalizeClass(["color-btn", { active: currentColor.value === c }]),
                style: normalizeStyle({ background: c }),
                onClick: ($event) => currentColor.value = c
              }, null, 14, _hoisted_8$2);
            }), 64))
          ]),
          _cache[7] || (_cache[7] = createBaseVNode("div", { class: "divider" }, null, -1)),
          createBaseVNode("div", _hoisted_9$2, [
            (openBlock(), createElementBlock(Fragment, null, renderList(sizes, (s) => {
              return createBaseVNode("button", {
                key: s.value,
                class: normalizeClass(["size-btn", { active: currentSize.value === s.value }]),
                onClick: ($event) => currentSize.value = s.value,
                title: s.label
              }, [
                createBaseVNode("span", {
                  class: "size-dot",
                  style: normalizeStyle({ width: s.value + "px", height: s.value + "px" })
                }, null, 4)
              ], 10, _hoisted_10$2);
            }), 64))
          ]),
          _cache[8] || (_cache[8] = createBaseVNode("div", { class: "divider" }, null, -1)),
          isTransparent.value ? (openBlock(), createElementBlock("div", _hoisted_11$2, [
            createBaseVNode("button", {
              class: normalizeClass(["bg-toggle", { active: bgOpacity.value > 0 }]),
              onClick: toggleBg,
              title: bgOpacity.value > 0 ? "关闭背景遮罩" : "开启背景遮罩"
            }, [
              createBaseVNode("span", null, toDisplayString(bgOpacity.value > 0 ? "遮罩开" : "遮罩关"), 1)
            ], 10, _hoisted_12$2),
            bgOpacity.value > 0 ? withDirectives((openBlock(), createElementBlock("input", {
              key: 0,
              type: "range",
              class: "bg-slider",
              min: "0",
              max: "80",
              "onUpdate:modelValue": _cache[0] || (_cache[0] = ($event) => bgOpacity.value = $event)
            }, null, 512)), [
              [
                vModelText,
                bgOpacity.value,
                void 0,
                { number: true }
              ]
            ]) : createCommentVNode("", true)
          ])) : createCommentVNode("", true),
          isTransparent.value ? (openBlock(), createElementBlock("div", _hoisted_13$2)) : createCommentVNode("", true),
          createBaseVNode("button", {
            class: "action-btn undo",
            onClick: undo,
            disabled: undoStack.length === 0
          }, [..._cache[3] || (_cache[3] = [
            createBaseVNode("span", null, "撤销", -1)
          ])], 8, _hoisted_14$2),
          createBaseVNode("button", {
            class: "action-btn save",
            onClick: save
          }, [..._cache[4] || (_cache[4] = [
            createBaseVNode("span", null, "保存", -1)
          ])]),
          createBaseVNode("button", {
            class: "action-btn cancel",
            onClick: cancel
          }, [..._cache[5] || (_cache[5] = [
            createBaseVNode("span", null, "取消", -1)
          ])])
        ])) : createCommentVNode("", true),
        textEditing.value ? withDirectives((openBlock(), createElementBlock("input", {
          key: 3,
          ref_key: "textInputRef",
          ref: textInputRef,
          "onUpdate:modelValue": _cache[1] || (_cache[1] = ($event) => textInput.value = $event),
          class: "text-input",
          style: normalizeStyle(textInputStyle.value),
          onBlur: commitText,
          onKeydown: [
            withKeys(commitText, ["enter"]),
            withKeys(cancelText, ["escape"])
          ]
        }, null, 36)), [
          [vModelText, textInput.value]
        ]) : createCommentVNode("", true)
      ], 6);
    };
  }
});
const AnnotateApp = /* @__PURE__ */ _export_sfc(_sfc_main$4, [["__scopeId", "data-v-47ffb7bd"]]);
const _hoisted_1$3 = { class: "recorder-root" };
const _sfc_main$3 = /* @__PURE__ */ defineComponent({
  __name: "RecorderApp",
  setup(__props) {
    let mediaRecorder = null;
    let chunks = [];
    let stream = null;
    let micStream = null;
    let audioCtx = null;
    let unsubStart = null;
    let unsubStop = null;
    let unsubPause = null;
    let unsubResume = null;
    function clamp(n, min, max) {
      return Math.min(max, Math.max(min, n));
    }
    onMounted(() => {
      window.sidekick.overlay.ready();
      unsubStart = window.sidekick.recorder.onPageStart(async (opts) => {
        try {
          const fps = opts?.fps || 15;
          const wantMic = !!opts?.mic;
          const wantSystem = opts?.systemAudio !== false;
          const micVolume = clamp(opts?.micVolume ?? 1.2, 0.5, 2);
          const systemVolume = clamp(opts?.systemVolume ?? 0.8, 0.5, 2);
          const displayStream = await navigator.mediaDevices.getDisplayMedia({
            video: { frameRate: { ideal: fps, max: 30 } },
            audio: wantSystem
          });
          let haveAudio = false;
          const AudioCtx = window.AudioContext || window.webkitAudioContext;
          audioCtx = new AudioCtx();
          if (audioCtx.state === "suspended") {
            try {
              await audioCtx.resume();
            } catch {
            }
          }
          const dest = audioCtx.createMediaStreamDestination();
          const sysTrack = displayStream.getAudioTracks()[0];
          if (sysTrack) {
            const sysSrc = audioCtx.createMediaStreamSource(new MediaStream([sysTrack]));
            const sysGain = audioCtx.createGain();
            sysGain.gain.value = systemVolume;
            sysSrc.connect(sysGain).connect(dest);
            haveAudio = true;
          }
          if (wantMic) {
            try {
              micStream = await navigator.mediaDevices.getUserMedia({ audio: true });
              const micTrack = micStream.getAudioTracks()[0];
              if (micTrack) {
                const micSrc = audioCtx.createMediaStreamSource(new MediaStream([micTrack]));
                const micGain = audioCtx.createGain();
                micGain.gain.value = micVolume;
                micSrc.connect(micGain).connect(dest);
                haveAudio = true;
              }
            } catch (e) {
              console.warn("[Recorder] Mic unavailable, recording without microphone:", e);
            }
          }
          const videoTracks = displayStream.getVideoTracks();
          const finalTracks = [...videoTracks];
          if (haveAudio) {
            finalTracks.push(...dest.stream.getAudioTracks());
          }
          stream = new MediaStream(finalTracks);
          const mimeType = getSupportedMimeType();
          mediaRecorder = new MediaRecorder(stream, {
            mimeType,
            videoBitsPerSecond: 25e5
          });
          chunks = [];
          mediaRecorder.ondataavailable = (e) => {
            if (e.data.size > 0) {
              chunks.push(e.data);
            }
          };
          mediaRecorder.onstop = () => {
            const blob = new Blob(chunks, { type: mimeType });
            blob.arrayBuffer().then((buffer) => {
              window.sidekick.recorder.sendComplete(buffer, mimeType);
              cleanup();
            }).catch((err) => {
              console.error("Failed to convert blob:", err);
              window.sidekick.recorder.sendComplete(new ArrayBuffer(0), mimeType);
              cleanup();
            });
          };
          mediaRecorder.onerror = (e) => {
            console.error("MediaRecorder error:", e);
            window.sidekick.recorder.sendComplete(new ArrayBuffer(0), mimeType);
            cleanup();
          };
          videoTracks.forEach((track2) => {
            track2.onended = () => {
              if (mediaRecorder && mediaRecorder.state !== "inactive") {
                mediaRecorder.stop();
              }
            };
          });
          mediaRecorder.start(1e3);
          window.sidekick.recorder.sendStarted();
          console.log("[Recorder] Started", { fps, wantMic, wantSystem, micVolume, systemVolume, mimeType });
        } catch (e) {
          console.error("[Recorder] Failed to start:", e);
          window.sidekick.recorder.sendComplete(new ArrayBuffer(0), "video/webm");
          cleanup();
        }
      });
      unsubStop = window.sidekick.recorder.onPageStop(() => {
        if (mediaRecorder && mediaRecorder.state !== "inactive") {
          mediaRecorder.stop();
        }
        console.log("[Recorder] Stopping...");
      });
      unsubPause = window.sidekick.recorder.onPagePause(() => {
        try {
          if (mediaRecorder && mediaRecorder.state === "recording") mediaRecorder.pause();
        } catch (e) {
          console.warn("[Recorder] pause failed:", e);
        }
      });
      unsubResume = window.sidekick.recorder.onPageResume(() => {
        try {
          if (mediaRecorder && mediaRecorder.state === "paused") mediaRecorder.resume();
        } catch (e) {
          console.warn("[Recorder] resume failed:", e);
        }
      });
    });
    function getSupportedMimeType() {
      const candidates = [
        "video/webm;codecs=vp9",
        "video/webm;codecs=vp8",
        "video/webm",
        "video/mp4"
      ];
      for (const t of candidates) {
        if (MediaRecorder.isTypeSupported(t)) return t;
      }
      return "video/webm";
    }
    function cleanup() {
      if (stream) {
        stream.getTracks().forEach((t) => t.stop());
        stream = null;
      }
      if (micStream) {
        micStream.getTracks().forEach((t) => t.stop());
        micStream = null;
      }
      if (audioCtx) {
        try {
          audioCtx.close();
        } catch {
        }
        audioCtx = null;
      }
      mediaRecorder = null;
      chunks = [];
    }
    onUnmounted(() => {
      if (unsubStart) unsubStart();
      if (unsubStop) unsubStop();
      if (unsubPause) unsubPause();
      if (unsubResume) unsubResume();
      cleanup();
    });
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$3);
    };
  }
});
const RecorderApp = /* @__PURE__ */ _export_sfc(_sfc_main$3, [["__scopeId", "data-v-805ecf13"]]);
const FAN_START_DEG = 4;
const FAN_SWEEP_DEG = 92;
const FLOATBALL_ACTIONS = [
  "capture",
  "annotate",
  "longshot",
  "record",
  "ime",
  "taskmgr",
  "sidebar",
  "settings"
];
function fanItemOffset(index, count, radius, dir, startDeg = FAN_START_DEG, sweepDeg = FAN_SWEEP_DEG) {
  const startRad = startDeg * Math.PI / 180;
  const sweepRad = sweepDeg * Math.PI / 180;
  const angle = count <= 1 ? startRad + sweepRad / 2 : startRad + sweepRad / (count - 1) * index;
  return {
    x: dir.x * radius * Math.cos(angle),
    y: dir.y * radius * Math.sin(angle)
  };
}
const _hoisted_1$2 = { class: "appearance-panel" };
const _hoisted_2$2 = { class: "row" };
const _hoisted_3$2 = { class: "segmented" };
const _hoisted_4$2 = ["onClick"];
const _hoisted_5$1 = { class: "row" };
const _hoisted_6$1 = ["disabled", "value"];
const _hoisted_7$1 = { class: "slider-head" };
const _hoisted_8$1 = { class: "val" };
const _hoisted_9$1 = ["value"];
const _hoisted_10$1 = { class: "slider-head" };
const _hoisted_11$1 = { class: "val" };
const _hoisted_12$1 = ["value"];
const _hoisted_13$1 = { class: "slider-head" };
const _hoisted_14$1 = { class: "val" };
const _hoisted_15$1 = ["value"];
const _hoisted_16$1 = { class: "slider-head" };
const _hoisted_17 = { class: "val" };
const _hoisted_18 = ["value"];
const _hoisted_19 = { class: "slider-head" };
const _hoisted_20 = { class: "val" };
const _hoisted_21 = ["value"];
const _hoisted_22 = { class: "slider-head" };
const _hoisted_23 = { class: "val" };
const _hoisted_24 = ["value"];
const _hoisted_25 = { class: "slider-row" };
const _hoisted_26 = { class: "slider-head" };
const _hoisted_27 = { class: "val" };
const _hoisted_28 = ["value"];
const _hoisted_29 = { class: "row" };
const _hoisted_30 = ["value"];
const _hoisted_31 = { class: "row" };
const _hoisted_32 = { class: "row" };
const _hoisted_33 = { class: "slider-row" };
const _hoisted_34 = { class: "slider-head" };
const _hoisted_35 = { class: "val" };
const _hoisted_36 = ["value"];
const _hoisted_37 = { class: "slider-row" };
const _hoisted_38 = { class: "slider-head" };
const _hoisted_39 = { class: "val" };
const _hoisted_40 = ["value"];
const _hoisted_41 = { class: "slider-row" };
const _hoisted_42 = { class: "slider-head" };
const _hoisted_43 = { class: "val" };
const _hoisted_44 = ["value"];
const _hoisted_45 = { class: "slider-row" };
const _hoisted_46 = { class: "slider-head" };
const _hoisted_47 = { class: "val" };
const _hoisted_48 = ["value"];
const _hoisted_49 = { class: "row" };
const _hoisted_50 = ["value"];
const _hoisted_51 = { class: "row" };
const _hoisted_52 = ["value"];
const _hoisted_53 = { class: "actions-block" };
const _hoisted_54 = { class: "actions-grid" };
const _hoisted_55 = ["onClick"];
const _sfc_main$2 = /* @__PURE__ */ defineComponent({
  __name: "AppearancePanel",
  setup(__props) {
    const themes = [
      { value: "light", label: "亮色" },
      { value: "dark", label: "暗色" },
      { value: "auto", label: "跟随系统" }
    ];
    const actionOptions = FLOATBALL_ACTIONS.map((id) => ({
      id,
      label: id === "capture" ? "区域截图" : id === "annotate" ? "批注" : id === "longshot" ? "长截图" : id === "record" ? "录屏" : id === "ime" ? "输入法" : id === "taskmgr" ? "任务管理" : id === "sidebar" ? "侧边栏" : id === "settings" ? "设置" : id
    }));
    const appearance = /* @__PURE__ */ reactive({
      theme: "auto",
      liquidGlass: true,
      material: "acrylic",
      blur: 22,
      opacity: 0.62,
      saturate: 1.6,
      refraction: 14,
      specular: 0.55,
      aberration: 2,
      accent: "#2B6EE0",
      radius: 16,
      reduceMotion: false
    });
    const floatBall = /* @__PURE__ */ reactive({
      enabled: true,
      size: 56,
      idleOpacity: 0.55,
      idleDelayMs: 4e3,
      snapThreshold: 24,
      x: -1,
      y: -1,
      actions: ["capture", "annotate", "record", "ime", "longshot", "sidebar"],
      hotkey: "Alt+Q",
      doubleClick: "toggleSidebar"
    });
    function num(e, min, max) {
      const v = parseFloat(e.target.value);
      if (!Number.isFinite(v)) return min;
      return Math.min(max, Math.max(min, v));
    }
    function commitAppearance(key, value) {
      appearance[key] = value;
      window.sidekick.appearance.set({ [key]: value }).catch(() => {
      });
    }
    function commitFloatBall(key, value) {
      floatBall[key] = value;
      window.sidekick.config.set(`floatBall.${key}`, value).catch(() => {
      });
    }
    function toggleAction(id) {
      const set = new Set(floatBall.actions);
      if (set.has(id)) set.delete(id);
      else {
        if (set.size >= 8) return;
        set.add(id);
      }
      const next = FLOATBALL_ACTIONS.filter((a) => set.has(a));
      floatBall.actions = next;
      window.sidekick.config.set("floatBall.actions", next).catch(() => {
      });
    }
    function resetAll() {
      window.sidekick.appearance.set({}).catch(() => {
      });
      const defA = {
        theme: "auto",
        liquidGlass: true,
        material: "acrylic",
        blur: 22,
        opacity: 0.62,
        saturate: 1.6,
        refraction: 14,
        specular: 0.55,
        aberration: 2,
        accent: "#2B6EE0",
        radius: 16,
        reduceMotion: false
      };
      window.sidekick.appearance.set(defA).catch(() => {
      });
      const defF = {
        enabled: true,
        size: 56,
        idleOpacity: 0.55,
        idleDelayMs: 4e3,
        snapThreshold: 24,
        actions: ["capture", "annotate", "record", "ime", "longshot", "sidebar"],
        hotkey: "Alt+Q",
        doubleClick: "toggleSidebar"
      };
      for (const [k, v] of Object.entries(defF)) {
        window.sidekick.config.set(`floatBall.${k}`, v).catch(() => {
        });
      }
      Object.assign(appearance, defA);
      Object.assign(floatBall, defF);
    }
    onMounted(async () => {
      try {
        const cfg = await window.sidekick.config.get();
        Object.assign(appearance, cfg.appearance);
        Object.assign(floatBall, cfg.floatBall);
      } catch (e) {
        console.warn("[AppearancePanel] load config failed:", e);
      }
    });
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$2, [
        _cache[43] || (_cache[43] = createBaseVNode("h3", null, "外观", -1)),
        createBaseVNode("div", _hoisted_2$2, [
          _cache[18] || (_cache[18] = createBaseVNode("span", { class: "label" }, "主题", -1)),
          createBaseVNode("div", _hoisted_3$2, [
            (openBlock(), createElementBlock(Fragment, null, renderList(themes, (t) => {
              return createBaseVNode("button", {
                key: t.value,
                class: normalizeClass({ active: appearance.theme === t.value }),
                onClick: ($event) => commitAppearance("theme", t.value)
              }, toDisplayString(t.label), 11, _hoisted_4$2);
            }), 64))
          ])
        ]),
        createBaseVNode("div", _hoisted_5$1, [
          _cache[20] || (_cache[20] = createBaseVNode("span", { class: "label" }, "液态玻璃", -1)),
          createBaseVNode("button", {
            class: normalizeClass(["lg-switch", { "lg-on": appearance.liquidGlass }]),
            onClick: _cache[0] || (_cache[0] = ($event) => commitAppearance("liquidGlass", !appearance.liquidGlass))
          }, [..._cache[19] || (_cache[19] = [
            createBaseVNode("span", { class: "knob" }, null, -1)
          ])], 2)
        ]),
        createBaseVNode("div", {
          class: normalizeClass(["row", { disabled: !appearance.liquidGlass }])
        }, [
          _cache[22] || (_cache[22] = createBaseVNode("span", { class: "label" }, "窗口级材质", -1)),
          createBaseVNode("select", {
            disabled: !appearance.liquidGlass,
            value: appearance.material,
            onChange: _cache[1] || (_cache[1] = ($event) => commitAppearance("material", $event.target.value))
          }, [..._cache[21] || (_cache[21] = [
            createBaseVNode("option", { value: "acrylic" }, "亚克力 (Win11 推荐)", -1),
            createBaseVNode("option", { value: "mica" }, "云母 (更省电)", -1),
            createBaseVNode("option", { value: "blur" }, "模糊 (Win10)", -1),
            createBaseVNode("option", { value: "none" }, "无 (仅 CSS 玻璃)", -1)
          ])], 40, _hoisted_6$1)
        ], 2),
        createBaseVNode("div", {
          class: normalizeClass(["slider-row", { disabled: !appearance.liquidGlass }])
        }, [
          createBaseVNode("div", _hoisted_7$1, [
            _cache[23] || (_cache[23] = createBaseVNode("span", null, "模糊强度", -1)),
            createBaseVNode("span", _hoisted_8$1, toDisplayString(appearance.blur) + "px", 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "8",
            max: "40",
            step: "1",
            value: appearance.blur,
            onInput: _cache[2] || (_cache[2] = ($event) => commitAppearance("blur", num($event, 8, 40)))
          }, null, 40, _hoisted_9$1)
        ], 2),
        createBaseVNode("div", {
          class: normalizeClass(["slider-row", { disabled: !appearance.liquidGlass }])
        }, [
          createBaseVNode("div", _hoisted_10$1, [
            _cache[24] || (_cache[24] = createBaseVNode("span", null, "玻璃不透明度", -1)),
            createBaseVNode("span", _hoisted_11$1, toDisplayString(appearance.opacity.toFixed(2)), 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "0.3",
            max: "0.96",
            step: "0.02",
            value: appearance.opacity,
            onInput: _cache[3] || (_cache[3] = ($event) => commitAppearance("opacity", num($event, 0.3, 0.96)))
          }, null, 40, _hoisted_12$1)
        ], 2),
        createBaseVNode("div", {
          class: normalizeClass(["slider-row", { disabled: !appearance.liquidGlass }])
        }, [
          createBaseVNode("div", _hoisted_13$1, [
            _cache[25] || (_cache[25] = createBaseVNode("span", null, "饱和增强", -1)),
            createBaseVNode("span", _hoisted_14$1, toDisplayString(appearance.saturate.toFixed(2)), 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "1",
            max: "2.2",
            step: "0.05",
            value: appearance.saturate,
            onInput: _cache[4] || (_cache[4] = ($event) => commitAppearance("saturate", num($event, 1, 2.2)))
          }, null, 40, _hoisted_15$1)
        ], 2),
        createBaseVNode("div", {
          class: normalizeClass(["slider-row", { disabled: !appearance.liquidGlass }])
        }, [
          createBaseVNode("div", _hoisted_16$1, [
            _cache[26] || (_cache[26] = createBaseVNode("span", null, "折射强度", -1)),
            createBaseVNode("span", _hoisted_17, toDisplayString(appearance.refraction), 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "0",
            max: "40",
            step: "1",
            value: appearance.refraction,
            onInput: _cache[5] || (_cache[5] = ($event) => commitAppearance("refraction", num($event, 0, 40)))
          }, null, 40, _hoisted_18)
        ], 2),
        createBaseVNode("div", {
          class: normalizeClass(["slider-row", { disabled: !appearance.liquidGlass }])
        }, [
          createBaseVNode("div", _hoisted_19, [
            _cache[27] || (_cache[27] = createBaseVNode("span", null, "镜面高光", -1)),
            createBaseVNode("span", _hoisted_20, toDisplayString(appearance.specular.toFixed(2)), 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "0",
            max: "1",
            step: "0.05",
            value: appearance.specular,
            onInput: _cache[6] || (_cache[6] = ($event) => commitAppearance("specular", num($event, 0, 1)))
          }, null, 40, _hoisted_21)
        ], 2),
        createBaseVNode("div", {
          class: normalizeClass(["slider-row", { disabled: !appearance.liquidGlass }])
        }, [
          createBaseVNode("div", _hoisted_22, [
            _cache[28] || (_cache[28] = createBaseVNode("span", null, "色散（彩虹边）", -1)),
            createBaseVNode("span", _hoisted_23, toDisplayString(appearance.aberration.toFixed(1)), 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "0",
            max: "8",
            step: "0.5",
            value: appearance.aberration,
            onInput: _cache[7] || (_cache[7] = ($event) => commitAppearance("aberration", num($event, 0, 8)))
          }, null, 40, _hoisted_24)
        ], 2),
        createBaseVNode("div", _hoisted_25, [
          createBaseVNode("div", _hoisted_26, [
            _cache[29] || (_cache[29] = createBaseVNode("span", null, "圆角", -1)),
            createBaseVNode("span", _hoisted_27, toDisplayString(appearance.radius) + "px", 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "0",
            max: "28",
            step: "1",
            value: appearance.radius,
            onInput: _cache[8] || (_cache[8] = ($event) => commitAppearance("radius", num($event, 0, 28)))
          }, null, 40, _hoisted_28)
        ]),
        createBaseVNode("div", _hoisted_29, [
          _cache[30] || (_cache[30] = createBaseVNode("span", { class: "label" }, "主题色", -1)),
          createBaseVNode("input", {
            class: "color-input",
            type: "color",
            value: appearance.accent,
            onInput: _cache[9] || (_cache[9] = ($event) => commitAppearance("accent", $event.target.value))
          }, null, 40, _hoisted_30)
        ]),
        createBaseVNode("div", _hoisted_31, [
          _cache[32] || (_cache[32] = createBaseVNode("span", { class: "label" }, "降低动效", -1)),
          createBaseVNode("button", {
            class: normalizeClass(["lg-switch", { "lg-on": appearance.reduceMotion }]),
            onClick: _cache[10] || (_cache[10] = ($event) => commitAppearance("reduceMotion", !appearance.reduceMotion))
          }, [..._cache[31] || (_cache[31] = [
            createBaseVNode("span", { class: "knob" }, null, -1)
          ])], 2)
        ]),
        _cache[44] || (_cache[44] = createStaticVNode('<div class="preview" data-v-a00b727e><div class="preview-card lg-glass lg-solidish" data-v-a00b727e><span class="lg-title" data-v-a00b727e>玻璃预览</span><span class="lg-sub" data-v-a00b727e>液态玻璃 · 边缘光 · 折射</span><div class="preview-btns" data-v-a00b727e><button class="lg-btn" data-v-a00b727e>按钮</button><button class="lg-btn lg-primary" data-v-a00b727e>主操作</button></div><span class="lg-chip lg-on" data-v-a00b727e>标签</span></div></div><h3 data-v-a00b727e>悬浮球</h3>', 2)),
        createBaseVNode("div", _hoisted_32, [
          _cache[34] || (_cache[34] = createBaseVNode("span", { class: "label" }, "启用悬浮球", -1)),
          createBaseVNode("button", {
            class: normalizeClass(["lg-switch", { "lg-on": floatBall.enabled }]),
            onClick: _cache[11] || (_cache[11] = ($event) => commitFloatBall("enabled", !floatBall.enabled))
          }, [..._cache[33] || (_cache[33] = [
            createBaseVNode("span", { class: "knob" }, null, -1)
          ])], 2)
        ]),
        createBaseVNode("div", _hoisted_33, [
          createBaseVNode("div", _hoisted_34, [
            _cache[35] || (_cache[35] = createBaseVNode("span", null, "球体大小", -1)),
            createBaseVNode("span", _hoisted_35, toDisplayString(floatBall.size) + "px", 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "40",
            max: "96",
            step: "2",
            value: floatBall.size,
            onInput: _cache[12] || (_cache[12] = ($event) => commitFloatBall("size", num($event, 40, 96)))
          }, null, 40, _hoisted_36)
        ]),
        createBaseVNode("div", _hoisted_37, [
          createBaseVNode("div", _hoisted_38, [
            _cache[36] || (_cache[36] = createBaseVNode("span", null, "空闲淡出后不透明度", -1)),
            createBaseVNode("span", _hoisted_39, toDisplayString(floatBall.idleOpacity.toFixed(2)), 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "0.15",
            max: "1",
            step: "0.05",
            value: floatBall.idleOpacity,
            onInput: _cache[13] || (_cache[13] = ($event) => commitFloatBall("idleOpacity", num($event, 0.15, 1)))
          }, null, 40, _hoisted_40)
        ]),
        createBaseVNode("div", _hoisted_41, [
          createBaseVNode("div", _hoisted_42, [
            _cache[37] || (_cache[37] = createBaseVNode("span", null, "空闲淡出延迟", -1)),
            createBaseVNode("span", _hoisted_43, toDisplayString(floatBall.idleDelayMs) + "ms", 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "0",
            max: "60000",
            step: "500",
            value: floatBall.idleDelayMs,
            onInput: _cache[14] || (_cache[14] = ($event) => commitFloatBall("idleDelayMs", num($event, 0, 6e4)))
          }, null, 40, _hoisted_44)
        ]),
        createBaseVNode("div", _hoisted_45, [
          createBaseVNode("div", _hoisted_46, [
            _cache[38] || (_cache[38] = createBaseVNode("span", null, "贴边吸附阈值", -1)),
            createBaseVNode("span", _hoisted_47, toDisplayString(floatBall.snapThreshold) + "px", 1)
          ]),
          createBaseVNode("input", {
            class: "lg-range",
            type: "range",
            min: "0",
            max: "120",
            step: "2",
            value: floatBall.snapThreshold,
            onInput: _cache[15] || (_cache[15] = ($event) => commitFloatBall("snapThreshold", num($event, 0, 120)))
          }, null, 40, _hoisted_48)
        ]),
        createBaseVNode("div", _hoisted_49, [
          _cache[39] || (_cache[39] = createBaseVNode("span", { class: "label" }, "唤出热键", -1)),
          createBaseVNode("input", {
            class: "text-input",
            type: "text",
            value: floatBall.hotkey,
            onChange: _cache[16] || (_cache[16] = ($event) => commitFloatBall("hotkey", $event.target.value.trim()))
          }, null, 40, _hoisted_50)
        ]),
        createBaseVNode("div", _hoisted_51, [
          _cache[41] || (_cache[41] = createBaseVNode("span", { class: "label" }, "双击球体", -1)),
          createBaseVNode("select", {
            value: floatBall.doubleClick,
            onChange: _cache[17] || (_cache[17] = ($event) => commitFloatBall("doubleClick", $event.target.value))
          }, [..._cache[40] || (_cache[40] = [
            createBaseVNode("option", { value: "toggleSidebar" }, "切换侧边栏", -1),
            createBaseVNode("option", { value: "capture" }, "区域截图", -1),
            createBaseVNode("option", { value: "none" }, "无", -1)
          ])], 40, _hoisted_52)
        ]),
        createBaseVNode("div", _hoisted_53, [
          _cache[42] || (_cache[42] = createBaseVNode("span", { class: "label" }, "扇形菜单动作（最多 8 个）", -1)),
          createBaseVNode("div", _hoisted_54, [
            (openBlock(true), createElementBlock(Fragment, null, renderList(unref(actionOptions), (act) => {
              return openBlock(), createElementBlock("button", {
                key: act.id,
                class: normalizeClass(["action-chip lg-chip", { "lg-on": floatBall.actions.includes(act.id) }]),
                onClick: ($event) => toggleAction(act.id)
              }, toDisplayString(act.label), 11, _hoisted_55);
            }), 128))
          ])
        ]),
        createBaseVNode("div", { class: "footer-row" }, [
          createBaseVNode("button", {
            class: "lg-btn",
            onClick: resetAll
          }, "恢复默认")
        ])
      ]);
    };
  }
});
const AppearancePanel = /* @__PURE__ */ _export_sfc(_sfc_main$2, [["__scopeId", "data-v-a00b727e"]]);
const _hoisted_1$1 = { class: "settings-app" };
const _hoisted_2$1 = { class: "settings-body" };
const _hoisted_3$1 = { class: "settings-section" };
const _hoisted_4$1 = { class: "setting-row" };
const _hoisted_5 = { class: "setting-row" };
const _hoisted_6 = { class: "settings-section" };
const _hoisted_7 = { class: "setting-row" };
const _hoisted_8 = { class: "setting-row" };
const _hoisted_9 = { class: "settings-section" };
const _hoisted_10 = { class: "setting-row" };
const _hoisted_11 = { class: "setting-row" };
const _hoisted_12 = { class: "setting-row" };
const _hoisted_13 = { class: "settings-section" };
const _hoisted_14 = { class: "setting-row" };
const _hoisted_15 = { class: "settings-section" };
const _hoisted_16 = { class: "setting-row" };
const _sfc_main$1 = /* @__PURE__ */ defineComponent({
  __name: "SettingsApp",
  setup(__props) {
    const autoLaunch = /* @__PURE__ */ ref(false);
    const sidebarSide = /* @__PURE__ */ ref("right");
    const imeSlot1 = /* @__PURE__ */ ref("Microsoft Pinyin");
    const imeSlot2 = /* @__PURE__ */ ref("US");
    const captureHotkey = /* @__PURE__ */ ref("Ctrl+Shift+A");
    const captureFormat = /* @__PURE__ */ ref("PNG");
    const captureDir = /* @__PURE__ */ ref("");
    const usbEnabled = /* @__PURE__ */ ref(true);
    const printerInterval = /* @__PURE__ */ ref(10);
    async function loadConfig() {
      try {
        const cfg = await window.sidekick.config.get();
        sidebarSide.value = cfg.display.sidebarSide;
        imeSlot1.value = cfg.ime.slot1;
        imeSlot2.value = cfg.ime.slot2;
        captureHotkey.value = cfg.capture.hotkey;
        captureFormat.value = cfg.capture.format;
        captureDir.value = cfg.capture.dir;
        usbEnabled.value = cfg.usb.enabled;
        printerInterval.value = cfg.printer.pollIntervalSec;
      } catch (e) {
        console.error("Load config failed:", e);
      }
    }
    async function toggleAutoLaunch() {
      autoLaunch.value = !autoLaunch.value;
      await window.sidekick.power.setAutoLaunch(autoLaunch.value);
    }
    async function save() {
      try {
        await window.sidekick.config.set("display.sidebarSide", sidebarSide.value);
        await window.sidekick.config.set("ime.slot1", imeSlot1.value);
        await window.sidekick.config.set("ime.slot2", imeSlot2.value);
        await window.sidekick.config.set("capture.hotkey", captureHotkey.value);
        await window.sidekick.config.set("capture.format", captureFormat.value);
        await window.sidekick.config.set("capture.dir", captureDir.value);
        await window.sidekick.config.set("usb.enabled", usbEnabled.value);
        await window.sidekick.config.set("printer.pollIntervalSec", printerInterval.value);
        alert("设置已保存");
      } catch (e) {
        alert("保存失败: " + e);
      }
    }
    onMounted(loadConfig);
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", _hoisted_1$1, [
        _cache[27] || (_cache[27] = createBaseVNode("div", { class: "settings-header" }, [
          createBaseVNode("h1", null, "设置")
        ], -1)),
        createBaseVNode("div", _hoisted_2$1, [
          createBaseVNode("section", _hoisted_3$1, [
            _cache[12] || (_cache[12] = createBaseVNode("h3", null, "通用", -1)),
            createBaseVNode("div", _hoisted_4$1, [
              _cache[9] || (_cache[9] = createBaseVNode("span", null, "开机自启动", -1)),
              createBaseVNode("button", {
                class: normalizeClass(["toggle", { on: autoLaunch.value }]),
                onClick: toggleAutoLaunch
              }, [..._cache[8] || (_cache[8] = [
                createBaseVNode("span", { class: "toggle-knob" }, null, -1)
              ])], 2)
            ]),
            createBaseVNode("div", _hoisted_5, [
              _cache[11] || (_cache[11] = createBaseVNode("span", null, "侧边栏位置", -1)),
              withDirectives(createBaseVNode("select", {
                "onUpdate:modelValue": _cache[0] || (_cache[0] = ($event) => sidebarSide.value = $event)
              }, [..._cache[10] || (_cache[10] = [
                createBaseVNode("option", { value: "right" }, "右侧", -1),
                createBaseVNode("option", { value: "left" }, "左侧", -1)
              ])], 512), [
                [vModelSelect, sidebarSide.value]
              ])
            ])
          ]),
          createBaseVNode("section", _hoisted_6, [
            _cache[15] || (_cache[15] = createBaseVNode("h3", null, "输入法", -1)),
            createBaseVNode("div", _hoisted_7, [
              _cache[13] || (_cache[13] = createBaseVNode("span", null, "槽位 1", -1)),
              withDirectives(createBaseVNode("input", {
                "onUpdate:modelValue": _cache[1] || (_cache[1] = ($event) => imeSlot1.value = $event),
                class: "text-input"
              }, null, 512), [
                [vModelText, imeSlot1.value]
              ])
            ]),
            createBaseVNode("div", _hoisted_8, [
              _cache[14] || (_cache[14] = createBaseVNode("span", null, "槽位 2", -1)),
              withDirectives(createBaseVNode("input", {
                "onUpdate:modelValue": _cache[2] || (_cache[2] = ($event) => imeSlot2.value = $event),
                class: "text-input"
              }, null, 512), [
                [vModelText, imeSlot2.value]
              ])
            ])
          ]),
          createBaseVNode("section", _hoisted_9, [
            _cache[20] || (_cache[20] = createBaseVNode("h3", null, "截图与批注", -1)),
            createBaseVNode("div", _hoisted_10, [
              _cache[16] || (_cache[16] = createBaseVNode("span", null, "截图快捷键", -1)),
              withDirectives(createBaseVNode("input", {
                "onUpdate:modelValue": _cache[3] || (_cache[3] = ($event) => captureHotkey.value = $event),
                class: "text-input"
              }, null, 512), [
                [vModelText, captureHotkey.value]
              ])
            ]),
            createBaseVNode("div", _hoisted_11, [
              _cache[18] || (_cache[18] = createBaseVNode("span", null, "保存格式", -1)),
              withDirectives(createBaseVNode("select", {
                "onUpdate:modelValue": _cache[4] || (_cache[4] = ($event) => captureFormat.value = $event)
              }, [..._cache[17] || (_cache[17] = [
                createBaseVNode("option", { value: "PNG" }, "PNG", -1),
                createBaseVNode("option", { value: "JPG" }, "JPG", -1)
              ])], 512), [
                [vModelSelect, captureFormat.value]
              ])
            ]),
            createBaseVNode("div", _hoisted_12, [
              _cache[19] || (_cache[19] = createBaseVNode("span", null, "保存目录", -1)),
              withDirectives(createBaseVNode("input", {
                "onUpdate:modelValue": _cache[5] || (_cache[5] = ($event) => captureDir.value = $event),
                class: "text-input"
              }, null, 512), [
                [vModelText, captureDir.value]
              ])
            ])
          ]),
          createBaseVNode("section", _hoisted_13, [
            _cache[23] || (_cache[23] = createBaseVNode("h3", null, "USB 监控", -1)),
            createBaseVNode("div", _hoisted_14, [
              _cache[22] || (_cache[22] = createBaseVNode("span", null, "启用 USB 监控", -1)),
              createBaseVNode("button", {
                class: normalizeClass(["toggle", { on: usbEnabled.value }]),
                onClick: _cache[6] || (_cache[6] = ($event) => usbEnabled.value = !usbEnabled.value)
              }, [..._cache[21] || (_cache[21] = [
                createBaseVNode("span", { class: "toggle-knob" }, null, -1)
              ])], 2)
            ])
          ]),
          createBaseVNode("section", _hoisted_15, [
            _cache[25] || (_cache[25] = createBaseVNode("h3", null, "打印机", -1)),
            createBaseVNode("div", _hoisted_16, [
              _cache[24] || (_cache[24] = createBaseVNode("span", null, "轮询间隔 (秒)", -1)),
              withDirectives(createBaseVNode("input", {
                "onUpdate:modelValue": _cache[7] || (_cache[7] = ($event) => printerInterval.value = $event),
                type: "number",
                min: "5",
                max: "60",
                class: "text-input"
              }, null, 512), [
                [
                  vModelText,
                  printerInterval.value,
                  void 0,
                  { number: true }
                ]
              ])
            ])
          ]),
          createVNode(AppearancePanel),
          _cache[26] || (_cache[26] = createBaseVNode("section", { class: "settings-section" }, [
            createBaseVNode("h3", null, "关于"),
            createBaseVNode("div", { class: "about-info" }, [
              createBaseVNode("p", null, "希沃侧边快捷键工具 v2.0.0"),
              createBaseVNode("p", { class: "muted" }, "Electron + Vue 3")
            ])
          ], -1))
        ]),
        createBaseVNode("div", { class: "settings-footer" }, [
          createBaseVNode("button", {
            class: "btn-save",
            onClick: save
          }, "保存设置")
        ])
      ]);
    };
  }
});
const SettingsApp = /* @__PURE__ */ _export_sfc(_sfc_main$1, [["__scopeId", "data-v-9515a432"]]);
const _hoisted_1 = ["title", "onClick"];
const _hoisted_2 = ["src"];
const _hoisted_3 = {
  key: 0,
  class: "fb-rec-ring"
};
const _hoisted_4 = {
  key: 1,
  class: "fb-ime-badge"
};
const _sfc_main = /* @__PURE__ */ defineComponent({
  __name: "FloatBallApp",
  setup(__props) {
    const ICONS = {
      capture: "capture",
      annotate: "annotate",
      longshot: "longshot",
      record: "record",
      ime: "ime",
      taskmgr: "taskmgr",
      sidebar: "sidebar",
      settings: "settings"
    };
    const TITLES = {
      capture: "区域截图",
      annotate: "批注",
      longshot: "长截图",
      record: "录屏",
      ime: "切换输入法",
      taskmgr: "任务管理器",
      sidebar: "侧边栏",
      settings: "设置"
    };
    const layout = /* @__PURE__ */ ref(null);
    const actionList = /* @__PURE__ */ ref([]);
    const idleDelayMs = /* @__PURE__ */ ref(4e3);
    const idleOpacity = /* @__PURE__ */ ref(0.55);
    const doubleClick = /* @__PURE__ */ ref("toggleSidebar");
    const isExpanded = /* @__PURE__ */ ref(false);
    const idle = /* @__PURE__ */ ref(false);
    const dragging = /* @__PURE__ */ ref(false);
    const recordActive = /* @__PURE__ */ ref(false);
    const recElapsed = /* @__PURE__ */ ref(0);
    const imeBadge = /* @__PURE__ */ ref("");
    const lensMap = /* @__PURE__ */ ref("");
    const mx = /* @__PURE__ */ ref(0);
    const my = /* @__PURE__ */ ref(0);
    let idleTimer = null;
    let recTimer = null;
    let lastClickThrough = true;
    const items = computed(
      () => actionList.value.map((id) => ({
        id,
        icon: iconFor(id),
        title: TITLES[id] || id
      }))
    );
    const orbStyle = computed(() => {
      const l = layout.value;
      if (!l) return {};
      const op = idle.value ? idleOpacity.value : 1;
      return {
        left: `${l.ballOffset.x}px`,
        top: `${l.ballOffset.y}px`,
        width: `${l.ballSize}px`,
        height: `${l.ballSize}px`,
        opacity: String(op),
        "--lg-mx": String(mx.value),
        "--lg-my": String(my.value)
      };
    });
    function itemStyle(i) {
      const l = layout.value;
      if (!l) return {};
      const itemSize = l.itemSize;
      const cx = l.ballOffset.x + l.ballSize / 2;
      const cy = l.ballOffset.y + l.ballSize / 2;
      if (!l.expanded) {
        return {
          left: `${cx - itemSize / 2}px`,
          top: `${cy - itemSize / 2}px`,
          width: `${itemSize}px`,
          height: `${itemSize}px`,
          opacity: "0",
          transform: "scale(0.2)",
          pointerEvents: "none"
        };
      }
      const off = fanItemOffset(i, actionList.value.length, l.radius, l.dir, FAN_START_DEG, FAN_SWEEP_DEG);
      return {
        left: `${cx + off.x - itemSize / 2}px`,
        top: `${cy + off.y - itemSize / 2}px`,
        width: `${itemSize}px`,
        height: `${itemSize}px`,
        opacity: "1",
        transform: "scale(1)",
        pointerEvents: "auto"
      };
    }
    const recText = computed(() => {
      const s = recElapsed.value;
      const m = Math.floor(s / 60);
      const sec = s % 60;
      return `${m}:${sec.toString().padStart(2, "0")}`;
    });
    function iconFor(id) {
      return new URL((/* @__PURE__ */ Object.assign({ "../assets/icons/annotate.svg": __vite_glob_0_0, "../assets/icons/bell.svg": __vite_glob_0_1, "../assets/icons/capture.svg": __vite_glob_0_2, "../assets/icons/ime.svg": __vite_glob_0_3, "../assets/icons/link.svg": __vite_glob_0_4, "../assets/icons/lock.svg": __vite_glob_0_5, "../assets/icons/longshot.svg": __vite_glob_0_6, "../assets/icons/printer.svg": __vite_glob_0_7, "../assets/icons/record.svg": __vite_glob_0_8, "../assets/icons/reminder.svg": __vite_glob_0_9, "../assets/icons/settings.svg": __vite_glob_0_10, "../assets/icons/sidebar.svg": __vite_glob_0_11, "../assets/icons/taskmgr.svg": __vite_glob_0_12, "../assets/icons/usb.svg": __vite_glob_0_13 }))[`../assets/icons/${ICONS[id] || id}.svg`], import.meta.url).href;
    }
    async function loadConfig() {
      try {
        const cfg = await window.sidekick.config.get();
        actionList.value = cfg.floatBall.actions || [];
        idleDelayMs.value = cfg.floatBall.idleDelayMs;
        idleOpacity.value = cfg.floatBall.idleOpacity;
        doubleClick.value = cfg.floatBall.doubleClick;
      } catch (e) {
        console.warn("[FloatBall] load config failed:", e);
      }
    }
    function resetIdle() {
      idle.value = false;
      if (idleTimer) clearTimeout(idleTimer);
      if (idleDelayMs.value > 0) {
        idleTimer = setTimeout(() => {
          idle.value = true;
        }, idleDelayMs.value);
      }
    }
    function setClickThrough(on) {
      if (on === lastClickThrough) return;
      lastClickThrough = on;
      try {
        window.sidekick.floatball.setClickThrough(on);
      } catch {
      }
    }
    function onEnter() {
      if (dragging.value) return;
      resetIdle();
      if (!isExpanded.value) window.sidekick.floatball.expand();
    }
    function onLeave() {
      if (dragging.value) return;
      if (isExpanded.value && items.value.length === 0) {
        window.sidekick.floatball.collapse();
      }
    }
    function onMove(e) {
      resetIdle();
      if (isExpanded.value) {
        const t = e.target;
        const interactive = !!t.closest(".fb-interactive");
        setClickThrough(!interactive);
      }
    }
    function onOrbDown(e) {
      dragging.value = true;
      const grab = { x: e.offsetX, y: e.offsetY };
      window.sidekick.floatball.expand();
      window.sidekick.floatball.dragStart(grab);
    }
    function onOrbUp() {
      if (!dragging.value) return;
      dragging.value = false;
      window.sidekick.floatball.dragEnd();
    }
    function onOrbMove(e) {
      const r = e.currentTarget.getBoundingClientRect();
      const nx = (e.clientX - r.left - r.width / 2) / r.width * 100;
      const ny = (e.clientY - r.top - r.height / 2) / r.height * 100;
      mx.value = Math.max(-100, Math.min(100, nx));
      my.value = Math.max(-100, Math.min(100, ny));
    }
    function onOrbLeave() {
      if (dragging.value) return;
      mx.value = 0;
      my.value = 0;
    }
    function onOrbDblClick() {
      if (doubleClick.value === "toggleSidebar") window.sidekick.floatball.action("sidebar");
      else if (doubleClick.value === "capture") window.sidekick.floatball.action("capture");
    }
    function onItemClick(id) {
      window.sidekick.floatball.action(id);
      setTimeout(() => {
        window.sidekick.floatball.collapse();
      }, 150);
    }
    let unLayout = null;
    let unIme = null;
    onMounted(async () => {
      await loadConfig();
      lensMap.value = makeLensMap(128);
      resetIdle();
      unLayout = window.sidekick.floatball.onLayout((l) => {
        layout.value = l;
        isExpanded.value = l.expanded;
        if (l.expanded) {
          lastClickThrough = false;
          setClickThrough(true);
        } else {
          setClickThrough(false);
        }
      });
      try {
        const st = await window.sidekick.ime.getState();
        imeBadge.value = st.mode === "cn" ? "中" : "EN";
        unIme = window.sidekick.ime.onChanged((s) => {
          imeBadge.value = s.mode === "cn" ? "中" : "EN";
        });
      } catch {
      }
      try {
        window.sidekick.recorder.onStatusChanged((s) => {
          if (s?.recording) {
            recordActive.value = true;
            recElapsed.value = s.elapsed || 0;
            if (!recTimer) recTimer = setInterval(() => {
              recElapsed.value++;
            }, 1e3);
          } else {
            recordActive.value = false;
            if (recTimer) {
              clearInterval(recTimer);
              recTimer = null;
            }
          }
        });
      } catch {
      }
    });
    onUnmounted(() => {
      if (idleTimer) clearTimeout(idleTimer);
      if (recTimer) clearInterval(recTimer);
      unLayout?.();
      unIme?.();
    });
    return (_ctx, _cache) => {
      return openBlock(), createElementBlock("div", {
        class: normalizeClass(["fb-root", { expanded: isExpanded.value, idle: idle.value, dragging: dragging.value }]),
        onMouseenter: onEnter,
        onMouseleave: onLeave,
        onMousemove: onMove
      }, [
        createVNode(LiquidGlassDefs, { "lens-map": lensMap.value }, null, 8, ["lens-map"]),
        (openBlock(true), createElementBlock(Fragment, null, renderList(items.value, (it, i) => {
          return openBlock(), createElementBlock("button", {
            key: it.id,
            class: normalizeClass(["fb-item lg-glass lg-item lg-liquid fb-interactive", { show: isExpanded.value }]),
            style: normalizeStyle(itemStyle(i)),
            title: it.title,
            onClick: ($event) => onItemClick(it.id)
          }, [
            createBaseVNode("img", {
              src: it.icon,
              class: "fb-item-icon",
              draggable: "false"
            }, null, 8, _hoisted_2)
          ], 14, _hoisted_1);
        }), 128)),
        createBaseVNode("div", {
          class: normalizeClass(["fb-orb fb-interactive", { dragging: dragging.value }]),
          style: normalizeStyle(orbStyle.value),
          onPointerdown: onOrbDown,
          onPointerup: onOrbUp,
          onPointercancel: onOrbUp,
          onDblclick: onOrbDblClick,
          onMousemove: onOrbMove,
          onMouseleave: onOrbLeave
        }, [
          _cache[0] || (_cache[0] = createBaseVNode("div", { class: "fb-orb-ring" }, null, -1)),
          _cache[1] || (_cache[1] = createBaseVNode("div", { class: "fb-orb-core" }, null, -1)),
          recordActive.value ? (openBlock(), createElementBlock("span", _hoisted_3, toDisplayString(recText.value), 1)) : createCommentVNode("", true),
          imeBadge.value ? (openBlock(), createElementBlock("span", _hoisted_4, toDisplayString(imeBadge.value), 1)) : createCommentVNode("", true)
        ], 38)
      ], 34);
    };
  }
});
const FloatBallApp = /* @__PURE__ */ _export_sfc(_sfc_main, [["__scopeId", "data-v-2363d101"]]);
const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: "/sidebar", component: SidebarApp },
    { path: "/oobe", component: OobeApp },
    { path: "/overlay", component: OverlayApp },
    { path: "/annotate", component: AnnotateApp },
    { path: "/recorder", component: RecorderApp },
    { path: "/settings", component: SettingsApp },
    { path: "/floatball", component: FloatBallApp },
    { path: "/", redirect: "/sidebar" }
  ]
});
createApp(_sfc_main$g).use(router).mount("#app");
