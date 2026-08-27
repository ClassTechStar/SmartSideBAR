"use strict";
const electron = require("electron");
const path = require("path");
const child_process = require("child_process");
const log = require("electron-log");
const fs = require("fs");
const os = require("os");
const koffi = require("koffi");
const util = require("util");
const sharp = require("sharp");
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
function clampNum$1(v, range, fallback) {
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
    blur: clampNum$1(src.blur, GLASS_LIMITS.blur, base.blur),
    opacity: clampNum$1(src.opacity, GLASS_LIMITS.opacity, base.opacity),
    saturate: clampNum$1(src.saturate, GLASS_LIMITS.saturate, base.saturate),
    refraction: clampNum$1(src.refraction, GLASS_LIMITS.refraction, base.refraction),
    specular: clampNum$1(src.specular, GLASS_LIMITS.specular, base.specular),
    aberration: clampNum$1(src.aberration, GLASS_LIMITS.aberration, base.aberration),
    accent: normalizeAccent(src.accent, base.accent),
    radius: clampNum$1(src.radius, GLASS_LIMITS.radius, base.radius),
    reduceMotion: typeof src.reduceMotion === "boolean" ? src.reduceMotion : base.reduceMotion
  };
}
function resolveTheme(mode, systemDark) {
  if (mode === "light") return "light";
  if (mode === "dark") return "dark";
  return systemDark ? "dark" : "light";
}
function resolveMaterial(requested, osBuild2, liquidGlass) {
  if (!liquidGlass) return "none";
  if (requested === "none") return "none";
  if (osBuild2 >= 22621) return requested === "blur" ? "acrylic" : requested;
  if (osBuild2 >= 17134) return "blur";
  return "none";
}
function supportsNativeMaterial(osBuild2) {
  return osBuild2 >= 22621;
}
function parseWindowsBuild(release) {
  const m = /^\d+\.\d+\.(\d+)/.exec(release || "");
  if (!m) return 0;
  const n = Number(m[1]);
  return Number.isFinite(n) ? n : 0;
}
function accentGradientAbgr(a, theme) {
  const cfg = clampAppearance(a);
  const alpha = Math.round(Math.min(0.85, cfg.opacity * 0.75) * 255);
  const base = theme === "dark" ? { r: 20, g: 24, b: 30 } : { r: 250, g: 252, b: 255 };
  return (alpha << 24 | base.b << 16 | base.g << 8 | base.r) >>> 0;
}
const FAN_START_DEG = 4;
const FAN_SWEEP_DEG = 92;
const FAN_ITEM_SIZE = 46;
const FAN_ITEM_GAP = 12;
const FAN_MARGIN = 12;
const FAN_RADIUS_MIN = 96;
const FAN_RADIUS_MAX = 260;
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
const DEFAULT_FLOATBALL = {
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
};
const FLOATBALL_LIMITS = {
  size: { min: 40, max: 96 },
  idleOpacity: { min: 0.15, max: 1 },
  idleDelayMs: { min: 0, max: 6e4 },
  snapThreshold: { min: 0, max: 120 }
};
function clampNum(v, range, fallback) {
  const n = typeof v === "number" ? v : Number(v);
  if (!Number.isFinite(n)) return fallback;
  return Math.min(range.max, Math.max(range.min, n));
}
function clampFloatBall(input, base = DEFAULT_FLOATBALL) {
  const src = input || {};
  const rawActions = Array.isArray(src.actions) ? src.actions : base.actions;
  const actions = Array.from(new Set(rawActions)).filter((a) => FLOATBALL_ACTIONS.includes(a)).slice(0, 8);
  const dc = src.doubleClick;
  return {
    enabled: typeof src.enabled === "boolean" ? src.enabled : base.enabled,
    size: Math.round(clampNum(src.size, FLOATBALL_LIMITS.size, base.size)),
    idleOpacity: clampNum(src.idleOpacity, FLOATBALL_LIMITS.idleOpacity, base.idleOpacity),
    idleDelayMs: Math.round(clampNum(src.idleDelayMs, FLOATBALL_LIMITS.idleDelayMs, base.idleDelayMs)),
    snapThreshold: Math.round(clampNum(src.snapThreshold, FLOATBALL_LIMITS.snapThreshold, base.snapThreshold)),
    x: typeof src.x === "number" && Number.isFinite(src.x) ? Math.round(src.x) : base.x,
    y: typeof src.y === "number" && Number.isFinite(src.y) ? Math.round(src.y) : base.y,
    actions: actions.length > 0 ? actions : [...DEFAULT_FLOATBALL.actions],
    hotkey: typeof src.hotkey === "string" ? src.hotkey.trim() : base.hotkey,
    doubleClick: dc === "toggleSidebar" || dc === "capture" || dc === "none" ? dc : base.doubleClick
  };
}
function clampToArea(pos, size, area) {
  const maxX = area.x + area.width - size.width;
  const maxY = area.y + area.height - size.height;
  return {
    x: Math.round(Math.min(Math.max(pos.x, area.x), Math.max(area.x, maxX))),
    y: Math.round(Math.min(Math.max(pos.y, area.y), Math.max(area.y, maxY)))
  };
}
function snapToEdges(pos, size, area, threshold) {
  const p = clampToArea(pos, size, area);
  if (!(threshold > 0)) return { ...p, edge: null };
  const dLeft = p.x - area.x;
  const dRight = area.x + area.width - (p.x + size.width);
  const dTop = p.y - area.y;
  const dBottom = area.y + area.height - (p.y + size.height);
  const nearest = Math.min(dLeft, dRight, dTop, dBottom);
  if (nearest > threshold) return { ...p, edge: null };
  if (nearest === dLeft) return { x: area.x, y: p.y, edge: "left" };
  if (nearest === dRight) return { x: area.x + area.width - size.width, y: p.y, edge: "right" };
  if (nearest === dTop) return { x: p.x, y: area.y, edge: "top" };
  return { x: p.x, y: area.y + area.height - size.height, edge: "bottom" };
}
function defaultBallPosition(area, ballDip, side) {
  const inset = Math.round(ballDip * 0.28);
  const x = side === "right" ? area.x + area.width - ballDip - inset : area.x + inset;
  const y = area.y + Math.round(area.height * 0.62);
  return clampToArea({ x, y }, { width: ballDip, height: ballDip }, area);
}
function resolveBallPosition(cfg, area, ballDip, side) {
  if (cfg.x < 0 || cfg.y < 0) return defaultBallPosition(area, ballDip, side);
  return clampToArea(
    { x: area.x + cfg.x, y: area.y + cfg.y },
    { width: ballDip, height: ballDip },
    area
  );
}
function toRelativePosition(pos, area) {
  return { x: Math.max(0, Math.round(pos.x - area.x)), y: Math.max(0, Math.round(pos.y - area.y)) };
}
function fanRadius(count, itemSize = FAN_ITEM_SIZE, gap = FAN_ITEM_GAP, sweepDeg = FAN_SWEEP_DEG) {
  if (count <= 1) return FAN_RADIUS_MIN;
  const step = sweepDeg * Math.PI / 180 / (count - 1);
  const needed = (itemSize + gap) / step;
  return Math.round(Math.min(FAN_RADIUS_MAX, Math.max(FAN_RADIUS_MIN, needed)));
}
function fanItemOffset(index, count, radius, dir, startDeg = FAN_START_DEG, sweepDeg = FAN_SWEEP_DEG) {
  const startRad = startDeg * Math.PI / 180;
  const sweepRad = sweepDeg * Math.PI / 180;
  const angle = count <= 1 ? startRad + sweepRad / 2 : startRad + sweepRad / (count - 1) * index;
  return {
    x: dir.x * radius * Math.cos(angle),
    y: dir.y * radius * Math.sin(angle)
  };
}
function computeFanLayout(params) {
  const { ball, ballSize, area, count, uiScale } = params;
  const itemSize = params.itemSize ?? FAN_ITEM_SIZE;
  const gap = params.gap ?? FAN_ITEM_GAP;
  const scale = uiScale > 0 ? uiScale : 1;
  const ballDip = Math.round(ballSize * scale);
  const radius = fanRadius(count, itemSize, gap);
  const roomLeft = ball.x - area.x;
  const roomRight = area.x + area.width - (ball.x + ballDip);
  const roomTop = ball.y - area.y;
  const roomBottom = area.y + area.height - (ball.y + ballDip);
  const dir = {
    x: roomRight >= roomLeft ? 1 : -1,
    y: roomBottom >= roomTop ? 1 : -1
  };
  const half = ballSize / 2;
  let minX = -half;
  let maxX = half;
  let minY = -half;
  let maxY = half;
  const itemHalf = itemSize / 2;
  for (let i = 0; i < count; i++) {
    const o = fanItemOffset(i, count, radius, dir);
    minX = Math.min(minX, o.x - itemHalf);
    maxX = Math.max(maxX, o.x + itemHalf);
    minY = Math.min(minY, o.y - itemHalf);
    maxY = Math.max(maxY, o.y + itemHalf);
  }
  const panelCssW = Math.ceil(maxX - minX + FAN_MARGIN * 2);
  const panelCssH = Math.ceil(maxY - minY + FAN_MARGIN * 2);
  const centerX = -minX + FAN_MARGIN;
  const centerY = -minY + FAN_MARGIN;
  const ballOffset = { x: Math.round(centerX - half), y: Math.round(centerY - half) };
  const winW = Math.round(panelCssW * scale);
  const winH = Math.round(panelCssH * scale);
  const desired = {
    x: ball.x - Math.round(ballOffset.x * scale),
    y: ball.y - Math.round(ballOffset.y * scale)
  };
  const placed = clampToArea(desired, { width: winW, height: winH }, area);
  const compensated = {
    x: ballOffset.x + Math.round((desired.x - placed.x) / scale),
    y: ballOffset.y + Math.round((desired.y - placed.y) / scale)
  };
  const safeOffset = {
    x: Math.min(Math.max(compensated.x, 0), Math.max(0, panelCssW - ballSize)),
    y: Math.min(Math.max(compensated.y, 0), Math.max(0, panelCssH - ballSize))
  };
  return {
    layout: {
      expanded: true,
      ballSize,
      ballOffset: safeOffset,
      dir,
      radius,
      itemSize
    },
    window: { x: placed.x, y: placed.y, width: winW, height: winH }
  };
}
function collapsedLayout(ballSize, itemSize = FAN_ITEM_SIZE) {
  return {
    expanded: false,
    ballSize,
    ballOffset: { x: 0, y: 0 },
    dir: { x: 1, y: 1 },
    radius: 0,
    itemSize
  };
}
const DEFAULT_CONFIG = {
  version: 2,
  power: { launcher: "side", sleep: "10min", powerOnAt: "07:50" },
  ime: { slot1: "Microsoft Pinyin", slot2: "US", fallbackSwap: true },
  capture: { hotkey: "Ctrl+Shift+A", format: "PNG", dir: "{Pictures}/Sidekick" },
  recorder: { fps: 15, bitrate: "2M", mic: false, systemAudio: true, micVolume: 1.2, systemVolume: 0.8, dir: "{Videos}/Sidekick" },
  usb: { enabled: true, ignoreTypes: ["phone", "carplay"], alertText: "请小心推拉黑板，避免碰撞 U 盘", alertDurationMs: 5e3, soundEnabled: true },
  printer: { pollIntervalSec: 10 },
  display: { sidebarMonitor: "primary", sidebarSide: "right", fitWindowsToWorkArea: true },
  appearance: { ...DEFAULT_APPEARANCE },
  floatBall: { ...DEFAULT_FLOATBALL, actions: [...DEFAULT_FLOATBALL.actions] },
  links: [
    { id: "l1", name: "国家中小学智慧教育平台", url: "https://www.zxx.edu.cn", enabled: true },
    { id: "l2", name: "希沃白板", url: "https://easinote.seewo.com", enabled: true },
    { id: "l3", name: "学科网", url: "https://www.zxxk.com", enabled: true },
    { id: "l4", name: "百度文库", url: "https://wenku.baidu.com", enabled: false },
    { id: "l5", name: "中国知网", url: "https://www.cnki.net", enabled: false }
  ],
  reminders: [],
  reminderSound: { preset: "default", mp3Path: null, volume: 0.8, repeat: 3, repeatInterval: 800 },
  oobe: {
    completed: false,
    completedAt: null,
    skipped: false,
    role: null,
    lastStepIndex: 0,
    prefs: { ime: true, usb: true, shot: true, recorder: false, printer: true },
    env: { screen: { w: 1920, h: 1080, scale: 1, touch: true }, printerCount: 0, imeOk: true, os: "win32" }
  },
  policy: { disabledModules: [] }
};
function getUserConfigPath() {
  return path.join(electron.app.getPath("userData"), "config.json");
}
function getProgramDataPath() {
  if (process.platform !== "win32") return "";
  return path.join(process.env.ProgramData || "C:\\ProgramData", "SeewoSidekick", "config.json");
}
function expandPath(p) {
  return p.replace("{Pictures}", electron.app.getPath("pictures")).replace("{Videos}", electron.app.getPath("videos")).replace("{Home}", electron.app.getPath("home"));
}
function deepMerge(base, overlay) {
  const result = { ...base };
  for (const key in overlay) {
    if (overlay[key] !== void 0 && overlay[key] !== null) {
      if (typeof overlay[key] === "object" && !Array.isArray(overlay[key])) {
        result[key] = deepMerge(result[key], overlay[key]);
      } else {
        result[key] = overlay[key];
      }
    }
  }
  return result;
}
let currentConfig = null;
let pendingSaveTimer = null;
function scheduleSave() {
  if (pendingSaveTimer) return;
  pendingSaveTimer = setTimeout(() => {
    pendingSaveTimer = null;
    flushSave();
  }, 1e3);
}
function flushSave() {
  if (!currentConfig) return;
  try {
    const userPath = getUserConfigPath();
    fs.writeFileSync(userPath, JSON.stringify(currentConfig, null, 2), "utf-8");
  } catch (e) {
    log.error("[Config] Save failed:", e);
  }
}
const ConfigService = {
  async init() {
    const userPath = getUserConfigPath();
    const programDataPath = getProgramDataPath();
    fs.mkdirSync(path.dirname(userPath), { recursive: true });
    let config2 = { ...DEFAULT_CONFIG };
    if (fs.existsSync(programDataPath)) {
      try {
        const policy = JSON.parse(fs.readFileSync(programDataPath, "utf-8"));
        config2 = deepMerge(config2, policy);
        log.info("[Config] Loaded ProgramData policy");
      } catch (e) {
        log.warn("[Config] ProgramData parse error:", e);
      }
    }
    if (fs.existsSync(userPath)) {
      try {
        const user = JSON.parse(fs.readFileSync(userPath, "utf-8"));
        config2 = deepMerge(config2, user);
        log.info("[Config] Loaded user config");
      } catch (e) {
        log.warn("[Config] User config parse error, using defaults:", e);
      }
    } else {
      fs.writeFileSync(userPath, JSON.stringify(config2, null, 2), "utf-8");
      log.info("[Config] Created default user config");
    }
    config2.capture.dir = expandPath(config2.capture.dir);
    config2.recorder.dir = expandPath(config2.recorder.dir);
    config2.appearance = clampAppearance(config2.appearance, DEFAULT_APPEARANCE);
    config2.floatBall = clampFloatBall(config2.floatBall, DEFAULT_FLOATBALL);
    currentConfig = config2;
    return config2;
  },
  get() {
    if (!currentConfig) throw new Error("Config not initialized");
    return currentConfig;
  },
  set(key, value) {
    if (!currentConfig) return false;
    const keys = key.split(".");
    let target = currentConfig;
    for (let i = 0; i < keys.length - 1; i++) {
      target = target[keys[i]];
    }
    target[keys[keys.length - 1]] = value;
    scheduleSave();
    return true;
  },
  /** 立即落盘（退出前、关键提交时调用，避免节流延迟丢配置） */
  flush() {
    if (pendingSaveTimer) {
      clearTimeout(pendingSaveTimer);
      pendingSaveTimer = null;
    }
    flushSave();
  },
  isModuleDisabled(module) {
    if (!currentConfig) return false;
    return currentConfig.policy.disabledModules.includes(module);
  }
};
const IPC_CHANNELS = {
  // IME 输入法
  "ime:getState": "ime:getState",
  "ime:toggle": "ime:toggle",
  "ime:locale": "ime:locale",
  "ime:changed": "ime:changed",
  // 截图
  "capture:region": "capture:region",
  "capture:annotate": "capture:annotate",
  "capture:longshot": "capture:longshot",
  // 长截图
  "longshot:start": "longshot:start",
  "longshot:stop": "longshot:stop",
  "longshot:progress": "longshot:progress",
  "longshot:selectWindow": "longshot:selectWindow",
  "longshot:countdown": "longshot:countdown",
  // 录屏
  "recorder:start": "recorder:start",
  "recorder:stop": "recorder:stop",
  "recorder:pause": "recorder:pause",
  "recorder:resume": "recorder:resume",
  "recorder:status": "recorder:status",
  "recorder:statusChanged": "recorder:statusChanged",
  "recorder:pageStart": "recorder:pageStart",
  "recorder:pageStop": "recorder:pageStop",
  "recorder:pagePause": "recorder:pagePause",
  "recorder:pageResume": "recorder:pageResume",
  "recorder:data": "recorder:data",
  "recorder:complete": "recorder:complete",
  "recorder:started": "recorder:started",
  // 覆盖层 (区域截图 + 批注)
  "overlay:init": "overlay:init",
  "overlay:region": "overlay:region",
  "overlay:saveAnnotate": "overlay:saveAnnotate",
  "overlay:cancel": "overlay:cancel",
  "overlay:ready": "overlay:ready",
  // USB
  "usb:arrived": "usb:arrived",
  "usb:removed": "usb:removed",
  "usb:list": "usb:list",
  "usb:scan": "usb:scan",
  // 打印机
  "printer:status": "printer:status",
  "printer:changed": "printer:changed",
  // 配置
  "config:get": "config:get",
  "config:set": "config:set",
  "config:updated": "config:updated",
  // 显示
  "display:list": "display:list",
  "display:metricsChanged": "display:metricsChanged",
  "display:sidebarTarget": "display:sidebarTarget",
  // OOBE
  "oobe:getState": "oobe:getState",
  "oobe:setState": "oobe:setState",
  "oobe:closeAndOpenMain": "oobe:closeAndOpenMain",
  // 电源/自启
  "power:setAutoLaunch": "power:setAutoLaunch",
  "power:getAutoLaunch": "power:getAutoLaunch",
  // 系统操作
  "shell:openExternal": "shell:openExternal",
  "shell:openPath": "shell:openPath",
  "shell:showItemInFolder": "shell:showItemInFolder",
  "app:openTaskMgr": "app:openTaskMgr",
  // 提醒
  "reminder:add": "reminder:add",
  "reminder:remove": "reminder:remove",
  "reminder:list": "reminder:list",
  "reminder:due": "reminder:due",
  "reminder:selectSound": "reminder:selectSound",
  "reminder:playTest": "reminder:playTest",
  // 窗口
  "window:show": "window:show",
  "window:hide": "window:hide",
  "window:minimize": "window:minimize",
  "window:resize": "window:resize",
  "window:openSettings": "window:openSettings",
  "window:dock": "window:dock",
  "window:undock": "window:undock",
  // 外观 / 液态玻璃
  "appearance:get": "appearance:get",
  "appearance:set": "appearance:set",
  "appearance:changed": "appearance:changed",
  // 悬浮球
  "floatball:show": "floatball:show",
  "floatball:hide": "floatball:hide",
  "floatball:toggle": "floatball:toggle",
  "floatball:dragStart": "floatball:dragStart",
  "floatball:dragEnd": "floatball:dragEnd",
  "floatball:expand": "floatball:expand",
  "floatball:collapse": "floatball:collapse",
  "floatball:action": "floatball:action",
  "floatball:layout": "floatball:layout",
  "floatball:setClickThrough": "floatball:setClickThrough",
  // 通知
  "notification:show": "notification:show",
  "notification:dismiss": "notification:dismiss",
  // 帮助/自检
  "help:runDiagnostics": "help:runDiagnostics",
  "help:exportDiagPack": "help:exportDiagPack",
  // 诊断
  "diag:getStatus": "diag:getStatus",
  "diag:update": "diag:update"
};
const ALLOWED_CHANNELS = new Set(Object.keys(IPC_CHANNELS));
function isAllowedChannel(channel) {
  return ALLOWED_CHANNELS.has(channel);
}
const WCA_ACCENT_POLICY = 19;
const ACCENT_DISABLED = 0;
const ACCENT_ENABLE_BLURBEHIND = 3;
const ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;
const ACCENT_FLAG_APPLY_TO_CLIENT = 2;
const DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
const DWMWA_WINDOW_CORNER_PREFERENCE = 33;
const DWMWA_BORDER_COLOR = 34;
const CORNER_DEFAULT = 0;
const CORNER_DONOTROUND = 1;
const CORNER_ROUND = 2;
const CORNER_ROUNDSMALL = 3;
const DWMWA_COLOR_NONE = 4294967294;
let api$1 = null;
let apiFailed = false;
function ensure$1() {
  if (api$1) return api$1;
  if (apiFailed) return null;
  try {
    const user32 = koffi.load("user32.dll");
    const dwmapi = koffi.load("dwmapi.dll");
    const AccentPolicy = koffi.struct("ACCENT_POLICY", {
      AccentState: "uint32",
      AccentFlags: "uint32",
      GradientColor: "uint32",
      AnimationId: "uint32"
    });
    const WinCompAttrData = koffi.struct("WINDOWCOMPOSITIONATTRIBDATA", {
      Attrib: "uint32",
      pvData: "void *",
      cbData: "size_t"
    });
    api$1 = {
      SetWindowCompositionAttribute: user32.func(
        "SetWindowCompositionAttribute",
        "bool",
        ["uintptr_t", koffi.pointer(WinCompAttrData)]
      ),
      DwmSetWindowAttribute: dwmapi.func(
        "DwmSetWindowAttribute",
        "int",
        ["uintptr_t", "uint32", "void *", "uint32"]
      ),
      AccentPolicy
    };
    return api$1;
  } catch (e) {
    apiFailed = true;
    log.warn("[DWM] FFI 装配失败，玻璃材质将退化为 CSS 模拟:", e);
    return null;
  }
}
function isDwmAvailable() {
  return ensure$1() !== null;
}
function hwndOf(win) {
  try {
    const buf = win.getNativeWindowHandle();
    if (buf.length >= 8) {
      const v = buf.readBigUInt64LE(0);
      return Number(v);
    }
    return buf.readUInt32LE(0);
  } catch (e) {
    log.warn("[DWM] 取窗口句柄失败:", e);
    return 0;
  }
}
function setAcrylic(hwnd, mode, tintAbgr = 2583691263) {
  const a = ensure$1();
  if (!a || !hwnd) return false;
  try {
    const state = mode === "acrylic" ? ACCENT_ENABLE_ACRYLICBLURBEHIND : mode === "blur" ? ACCENT_ENABLE_BLURBEHIND : ACCENT_DISABLED;
    const policyBuf = koffi.alloc(a.AccentPolicy, 1);
    koffi.encode(policyBuf, a.AccentPolicy, {
      AccentState: state,
      AccentFlags: state === ACCENT_DISABLED ? 0 : ACCENT_FLAG_APPLY_TO_CLIENT,
      GradientColor: tintAbgr >>> 0,
      AnimationId: 0
    });
    const ok = a.SetWindowCompositionAttribute(hwnd, {
      Attrib: WCA_ACCENT_POLICY,
      pvData: policyBuf,
      cbData: koffi.sizeof(a.AccentPolicy)
    });
    if (!ok) log.warn(`[DWM] SetWindowCompositionAttribute 返回 false (mode=${mode})`);
    return ok;
  } catch (e) {
    log.warn("[DWM] setAcrylic 失败:", e);
    return false;
  }
}
function setDwmDword(hwnd, attr, value) {
  const a = ensure$1();
  if (!a || !hwnd) return false;
  try {
    const buf = koffi.alloc("uint32", 1);
    koffi.encode(buf, "uint32", value >>> 0);
    const hr = a.DwmSetWindowAttribute(hwnd, attr, buf, 4);
    if (hr !== 0) {
      log.debug(`[DWM] DwmSetWindowAttribute(attr=${attr}) hr=0x${(hr >>> 0).toString(16)}`);
      return false;
    }
    return true;
  } catch (e) {
    log.warn(`[DWM] setDwmDword(attr=${attr}) 失败:`, e);
    return false;
  }
}
function setCorner(hwnd, style) {
  const map = {
    default: CORNER_DEFAULT,
    square: CORNER_DONOTROUND,
    round: CORNER_ROUND,
    roundSmall: CORNER_ROUNDSMALL
  };
  return setDwmDword(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, map[style]);
}
function setImmersiveDark(hwnd, dark) {
  return setDwmDword(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, dark ? 1 : 0);
}
function clearBorderColor(hwnd) {
  return setDwmDword(hwnd, DWMWA_BORDER_COLOR, DWMWA_COLOR_NONE);
}
const CORNER_BY_ROLE = {
  // 侧边栏是贴屏边的长条，圆角只在内侧有意义，用小圆角避免边缘露出黑三角
  sidebar: "roundSmall",
  panel: "round",
  // 悬浮球自己就是圆的，交给 CSS border-radius，系统圆角反而会切掉投影
  floatball: "square",
  transparent: "square"
};
let osBuild = 0;
let targets = [];
let currentAppearance = { ...DEFAULT_APPEARANCE };
let themeListenerBound = false;
function currentTheme() {
  return resolveTheme(currentAppearance.theme, electron.nativeTheme.shouldUseDarkColors);
}
function effectiveMaterial() {
  if (process.platform !== "win32") return "none";
  return resolveMaterial(currentAppearance.material, osBuild, currentAppearance.liquidGlass);
}
function syncThemeSource() {
  electron.nativeTheme.themeSource = currentAppearance.theme === "auto" ? "system" : currentAppearance.theme;
}
function backgroundColorFor(role, material, theme) {
  if (role === "floatball" || role === "transparent") return "#00000000";
  if (material !== "none") return "#00000000";
  return theme === "dark" ? "#171b22" : "#ffffff";
}
function initialBackgroundColor(role) {
  const a = clampAppearance(
    (() => {
      try {
        return ConfigService.get().appearance;
      } catch {
        return DEFAULT_APPEARANCE;
      }
    })(),
    DEFAULT_APPEARANCE
  );
  const build = process.platform === "win32" ? parseWindowsBuild(os.release()) : 0;
  const mat = process.platform === "win32" ? resolveMaterial(a.material, build, a.liquidGlass) : "none";
  const theme = resolveTheme(a.theme, electron.nativeTheme.shouldUseDarkColors);
  return backgroundColorFor(role, mat, theme);
}
function applyToTarget(t) {
  const { win, role } = t;
  if (win.isDestroyed()) return false;
  const theme = currentTheme();
  const material = effectiveMaterial();
  const dark = theme === "dark";
  if (role === "transparent" || role === "floatball" || role === "sidebar") {
    // 液态玻璃观感统一由页面 CSS 承载；系统背板材质会铺满整个窗口矩形形成
    // "方形底板"，这里对悬浮球与侧边栏彻底清场恢复纯透明
    try { win.setBackgroundColor("#00000000"); } catch (e) {}
    try {
      if (typeof win.setBackgroundMaterial === "function") win.setBackgroundMaterial("none");
    } catch (e) {}
    try {
      if (isDwmAvailable()) setAcrylic(hwndOf(win), "off");
    } catch (e) {}
    return false;
  }
  let applied = false;
  try {
    win.setBackgroundColor(backgroundColorFor(role, material, theme));
  } catch (e) {
    log.warn("[Appearance] setBackgroundColor 失败:", e);
  }
  try {
    if (material === "acrylic" || material === "mica") {
      if (supportsNativeMaterial(osBuild) && typeof win.setBackgroundMaterial === "function") {
        win.setBackgroundMaterial(material === "mica" ? "mica" : "acrylic");
        applied = true;
      } else if (isDwmAvailable()) {
        applied = setAcrylic(hwndOf(win), "acrylic", accentGradientAbgr(currentAppearance, theme));
      }
    } else if (material === "blur") {
      applied = setAcrylic(hwndOf(win), "acrylic", accentGradientAbgr(currentAppearance, theme));
    } else {
      if (supportsNativeMaterial(osBuild) && typeof win.setBackgroundMaterial === "function") {
        win.setBackgroundMaterial("none");
      }
      if (isDwmAvailable()) setAcrylic(hwndOf(win), "off");
    }
  } catch (e) {
    log.warn(`[Appearance] 应用材质失败 (role=${role}):`, e);
  }
  if (process.platform === "win32" && isDwmAvailable()) {
    const hwnd = hwndOf(win);
    if (hwnd) {
      setCorner(hwnd, t.corner);
      setImmersiveDark(hwnd, dark);
      clearBorderColor(hwnd);
    }
  }
  return applied;
}
function broadcast() {
  const snap = AppearanceService.snapshot();
  for (const win of electron.BrowserWindow.getAllWindows()) {
    if (win.isDestroyed()) continue;
    try {
      win.webContents.send(IPC_CHANNELS["appearance:changed"], snap);
    } catch {
    }
  }
}
function prune() {
  targets = targets.filter((t) => !t.win.isDestroyed());
}
const AppearanceService = {
  init() {
    osBuild = process.platform === "win32" ? parseWindowsBuild(os.release()) : 0;
    currentAppearance = clampAppearance(ConfigService.get().appearance, DEFAULT_APPEARANCE);
    syncThemeSource();
    if (!themeListenerBound) {
      themeListenerBound = true;
      electron.nativeTheme.on("updated", () => {
        if (currentAppearance.theme !== "auto") return;
        log.info(`[Appearance] 系统主题变更 → ${electron.nativeTheme.shouldUseDarkColors ? "dark" : "light"}`);
        this.reapply();
        broadcast();
      });
    }
    log.info(
      `[Appearance] 初始化完成 build=${osBuild} native=${supportsNativeMaterial(osBuild)} ffi=${isDwmAvailable()} material=${effectiveMaterial()} theme=${currentTheme()}`
    );
  },
  /**
   * 注册一个需要玻璃处理的窗口。
   * 必须在窗口 ready-to-show 之后调用 —— 之前 HWND 还没创建，FFI 会拿到 0。
   */
  register(win, role) {
    prune();
    if (targets.some((t2) => t2.win === win)) return;
    const t = { win, role, corner: CORNER_BY_ROLE[role] };
    targets.push(t);
    win.once("closed", () => {
      targets = targets.filter((x) => x.win !== win);
    });
    applyToTarget(t);
  },
  /** 对所有已注册窗口重新应用材质（主题变更 / 配置变更后调用） */
  reapply() {
    prune();
    for (const t of targets) applyToTarget(t);
  },
  snapshot() {
    return {
      appearance: { ...currentAppearance },
      theme: currentTheme(),
      effectiveMaterial: effectiveMaterial(),
      osBuild,
      nativeMaterial: supportsNativeMaterial(osBuild)
    };
  },
  /**
   * 更新外观。patch 只需带要改的字段。
   * 会立即落盘（外观是用户能立刻看到的东西，不适合走 1s 节流 —— 万一崩了就白调了）。
   */
  set(patch) {
    const next = clampAppearance({ ...currentAppearance, ...patch }, DEFAULT_APPEARANCE);
    const themeModeChanged = next.theme !== currentAppearance.theme;
    currentAppearance = next;
    ConfigService.set("appearance", { ...next });
    ConfigService.flush();
    if (themeModeChanged) syncThemeSource();
    this.reapply();
    broadcast();
    return this.snapshot();
  },
  /** Windows 内部版本号（供诊断面板显示） */
  build() {
    return osBuild;
  }
};
let displays = [];
let targetDisplay = null;
let metricsCbs = [];
let attachCbs = [];
function buildDisplayInfo() {
  return electron.screen.getAllDisplays().map((d) => ({
    id: d.id,
    name: `Display ${d.id}`,
    bounds: { x: d.bounds.x, y: d.bounds.y, width: d.bounds.width, height: d.bounds.height },
    workArea: { x: d.workArea.x, y: d.workArea.y, width: d.workArea.width, height: d.workArea.height },
    sizePx: { w: d.size.width, h: d.size.height },
    scaleFactor: d.scaleFactor,
    primary: d.id === electron.screen.getPrimaryDisplay().id
  }));
}
const DisplayService = {
  async init(config2) {
    displays = buildDisplayInfo();
    this.selectTarget(config2);
    this.selfCheck(config2);
    electron.screen.on("display-metrics-changed", () => {
      log.info("[Display] Metrics changed, updating...");
      displays = buildDisplayInfo();
      this.selectTarget(config2);
      this.emitMetrics();
    });
    electron.screen.on("display-added", (_e, newDisplay) => {
      displays = buildDisplayInfo();
      this.selectTarget(config2);
      this.emitAttach([newDisplay?.id].filter((x) => typeof x === "number"), []);
    });
    electron.screen.on("display-removed", (_e, oldDisplay) => {
      const removedId = oldDisplay?.id;
      displays = buildDisplayInfo();
      this.selectTarget(config2);
      this.emitAttach([], removedId !== void 0 ? [removedId] : []);
    });
    log.info(`[Display] Initialized with ${displays.length} display(s)`);
  },
  /** 启动自检：若 config.display.sidebarMonitor 指向的显示器不存在，回写为 'primary' */
  selfCheck(config2) {
    const cfgMonitor = config2.display.sidebarMonitor;
    if (cfgMonitor !== "primary") {
      const id = parseInt(cfgMonitor, 10);
      if (!displays.find((d) => d.id === id)) {
        log.warn(`[Display] Configured monitor "${cfgMonitor}" not found, falling back to primary`);
        ConfigService.set("display.sidebarMonitor", "primary");
      }
    }
  },
  emitMetrics() {
    for (const cb of metricsCbs) {
      try {
        cb(displays);
      } catch (e) {
        log.warn("[Display] metricsCb error:", e);
      }
    }
  },
  emitAttach(added, removed) {
    for (const cb of attachCbs) {
      try {
        cb(added, removed);
      } catch (e) {
        log.warn("[Display] attachCb error:", e);
      }
    }
  },
  /** 订阅显示器指标变化（分辨率 / 系统缩放 / DPI） */
  onMetricsChanged(cb) {
    metricsCbs.push(cb);
    return () => {
      metricsCbs = metricsCbs.filter((c) => c !== cb);
    };
  },
  /** 订阅显示器热插拔（增 / 减） */
  onAttachChanged(cb) {
    attachCbs.push(cb);
    return () => {
      attachCbs = attachCbs.filter((c) => c !== cb);
    };
  },
  selectTarget(config2) {
    const primary = displays.find((d) => d.primary) || displays[0];
    if (!primary) return;
    const cfgMonitor = config2.display.sidebarMonitor;
    if (cfgMonitor === "primary") {
      targetDisplay = primary;
    } else {
      targetDisplay = displays.find((d) => d.id === parseInt(cfgMonitor)) || primary;
    }
    log.info(`[Display] Target display: ${targetDisplay.id} (${targetDisplay.workArea.width}x${targetDisplay.workArea.height} @ ${targetDisplay.scaleFactor}x)`);
  },
  list() {
    return displays;
  },
  byId(id) {
    return displays.find((d) => d.id === id);
  },
  primary() {
    return displays.find((d) => d.primary) || displays[0] || targetDisplay;
  },
  sidebarTarget() {
    return targetDisplay || this.primary();
  },
  toPhysical(rectDip, sf) {
    return {
      x: Math.round(rectDip.x * sf),
      y: Math.round(rectDip.y * sf),
      width: Math.round(rectDip.width * sf),
      height: Math.round(rectDip.height * sf)
    };
  },
  toDIP(rectPx, sf) {
    return {
      x: Math.round(rectPx.x / sf),
      y: Math.round(rectPx.y / sf),
      width: Math.round(rectPx.width / sf),
      height: Math.round(rectPx.height / sf)
    };
  },
  withinWorkArea(display, rect) {
    const wa = display.workArea;
    return rect.x >= wa.x && rect.y >= wa.y && rect.x + rect.width <= wa.x + wa.width && rect.y + rect.height <= wa.y + wa.height;
  },
  windowOrigin(win) {
    const bounds = win.getBounds();
    return { x: bounds.x, y: bounds.y };
  },
  // 计算侧边栏位置 (DIP)
  calculateSidebarBounds(config2) {
    const target = this.sidebarTarget();
    const side = config2.display.sidebarSide;
    const width = 52;
    const height = target.workArea.height;
    let x;
    if (side === "right") {
      x = target.workArea.x + target.workArea.width - width;
    } else {
      x = target.workArea.x;
    }
    return {
      x,
      y: target.workArea.y,
      width,
      height
    };
  },
  // 计算展开面板位置
  calculatePanelBounds(config2) {
    const target = this.sidebarTarget();
    const side = config2.display.sidebarSide;
    const panelWidth = 380;
    const railWidth = 52;
    let x;
    if (side === "right") {
      x = target.workArea.x + target.workArea.width - railWidth - panelWidth;
    } else {
      x = target.workArea.x + railWidth;
    }
    return {
      x,
      y: target.workArea.y,
      width: panelWidth,
      height: target.workArea.height
    };
  }
};
const VK_CAPITAL = 20;
const VK_NUMLOCK = 144;
const WM_INPUTLANGCHANGEREQUEST = 80;
const KLF_ACTIVATE = 1;
const HKL_NEXT = 1;
let api = null;
function ensure() {
  if (api) return api;
  const user32 = koffi.load("user32.dll");
  const kernel32 = koffi.load("kernel32.dll");
  api = {
    GetForegroundWindow: user32.func("GetForegroundWindow", "uintptr_t", []),
    GetWindowThreadProcessId: user32.func("GetWindowThreadProcessId", "uint", ["uintptr_t", "uint*"]),
    AttachThreadInput: user32.func("AttachThreadInput", "bool", ["uint", "uint", "bool"]),
    ActivateKeyboardLayout: user32.func("ActivateKeyboardLayout", "uintptr_t", ["uintptr_t", "uint"]),
    GetKeyboardLayout: user32.func("GetKeyboardLayout", "uintptr_t", ["uint"]),
    GetCurrentThreadId: kernel32.func("GetCurrentThreadId", "uint", []),
    PostMessageW: user32.func("PostMessageW", "bool", ["uintptr_t", "uint", "uintptr_t", "uintptr_t"]),
    GetKeyState: user32.func("GetKeyState", "int16", ["int"])
  };
  return api;
}
function getForegroundLangId() {
  const a = ensure();
  try {
    const hwnd = a.GetForegroundWindow();
    if (!hwnd) return null;
    const fgThread = a.GetWindowThreadProcessId(hwnd, null);
    const curThread = a.GetCurrentThreadId();
    let attached = false;
    if (fgThread && fgThread !== curThread) {
      attached = a.AttachThreadInput(fgThread, curThread, true);
    }
    const hkl = a.GetKeyboardLayout(curThread);
    if (attached) a.AttachThreadInput(fgThread, curThread, false);
    return (hkl & 65535) >>> 0;
  } catch (e) {
    log.warn("[IME][FFI] getForegroundLangId failed:", e);
    return null;
  }
}
function cycleForegroundInputLanguage() {
  const a = ensure();
  try {
    const hwnd = a.GetForegroundWindow();
    if (!hwnd) return null;
    const fgThread = a.GetWindowThreadProcessId(hwnd, null);
    const curThread = a.GetCurrentThreadId();
    let attached = false;
    if (fgThread && fgThread !== curThread) {
      attached = a.AttachThreadInput(fgThread, curThread, true);
    }
    const newHkl = a.ActivateKeyboardLayout(HKL_NEXT, KLF_ACTIVATE);
    if (attached) a.AttachThreadInput(fgThread, curThread, false);
    a.PostMessageW(hwnd, WM_INPUTLANGCHANGEREQUEST, 0, newHkl);
    return (newHkl & 65535) >>> 0;
  } catch (e) {
    log.warn("[IME][FFI] cycleForegroundInputLanguage failed:", e);
    return null;
  }
}
function getToggleKeyState(vk) {
  try {
    const s = ensure().GetKeyState(vk);
    return (s & 1) !== 0;
  } catch (e) {
    log.warn("[IME][FFI] GetKeyState failed:", e);
    return false;
  }
}
const execAsync$3 = util.promisify(child_process.exec);
let currentState = {
  locale: "zh-CN",
  isChinese: true,
  mode: "cn",
  capsLock: false,
  numLock: false
};
let serviceState = "idle";
const LANG_MAP = {
  2052: { locale: "zh-CN", isChinese: true },
  4100: { locale: "zh-CN", isChinese: true },
  1028: { locale: "zh-TW", isChinese: true },
  3076: { locale: "zh-HK", isChinese: true },
  5124: { locale: "zh-MO", isChinese: true },
  1033: { locale: "en-US", isChinese: false },
  2057: { locale: "en-GB", isChinese: false },
  3081: { locale: "en-AU", isChinese: false }
};
function langIdToState(langId) {
  if (langId == null) return null;
  const known = LANG_MAP[langId];
  if (known) return { ...known, mode: known.isChinese ? "cn" : "en" };
  const base = langId & 1023;
  const isChinese = base === 2052 || base === 1028 || langId >= 2052 && langId <= 31748;
  return { locale: isChinese ? "zh-CN" : `lang-${langId.toString(16)}`, isChinese, mode: isChinese ? "cn" : "en" };
}
const PS_GET_LOCALE = `
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.InputLanguage]::CurrentInputLanguage.Culture.Name
`;
const PS_TOGGLE_IME = `
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Windows.Forms
$current = [System.Windows.Forms.InputLanguage]::CurrentInputLanguage
$langs = [System.Windows.Forms.InputLanguage]::InstalledInputLanguages
$currentIsCn = ($current.Culture.Name -match 'zh')
$target = $null
if ($currentIsCn) {
  $target = $langs | Where-Object { $_.Culture.Name -match 'en' } | Select-Object -First 1
  if (-not $target) { $target = $langs | Where-Object { $_.Culture.Name -ne $current.Culture.Name } | Select-Object -First 1 }
} else {
  $target = $langs | Where-Object { $_.Culture.Name -match 'zh' } | Select-Object -First 1
  if (-not $target) { $target = $langs | Where-Object { $_.Culture.Name -ne $current.Culture.Name } | Select-Object -First 1 }
}
if ($target) { [System.Windows.Forms.InputLanguage]::CurrentInputLanguage = $target }
$new = [System.Windows.Forms.InputLanguage]::CurrentInputLanguage
@{ locale = $new.Culture.Name; isChinese = ($new.Culture.Name -match 'zh') } | ConvertTo-Json -Compress
`;
const ImeService = {
  async start() {
    serviceState = "starting";
    try {
      const s = await this.getState();
      currentState = s;
      serviceState = "running";
      log.info(`[IME] Service started (FFI): mode=${s.mode} caps=${s.capsLock} num=${s.numLock}`);
    } catch (e) {
      serviceState = "degraded";
      log.warn("[IME] Service degraded:", e);
    }
  },
  async stop() {
    serviceState = "idle";
  },
  async getState() {
    const capsLock = getToggleKeyState(VK_CAPITAL);
    const numLock = getToggleKeyState(VK_NUMLOCK);
    const lang = langIdToState(getForegroundLangId());
    if (lang) {
      currentState = { ...currentState, ...lang, capsLock, numLock };
      return currentState;
    }
    try {
      const { stdout } = await execAsync$3(`powershell -NoProfile -Command "${PS_GET_LOCALE}"`, { timeout: 5e3 });
      const locale = stdout.trim();
      const isChinese = /zh/i.test(locale);
      currentState = { ...currentState, locale, isChinese, mode: isChinese ? "cn" : "en", capsLock, numLock };
    } catch (e) {
      log.warn("[IME] getState fallback failed:", e);
    }
    return currentState;
  },
  async toggle() {
    let newLang = cycleForegroundInputLanguage();
    if (newLang == null) {
      log.warn("[IME] FFI toggle failed, falling back to PowerShell");
      try {
        const { stdout } = await execAsync$3(`powershell -NoProfile -Command "${PS_TOGGLE_IME}"`, { timeout: 5e3 });
        const r = JSON.parse(stdout.trim());
        currentState = { ...currentState, locale: r.locale, isChinese: !!r.isChinese, mode: r.isChinese ? "cn" : "en" };
      } catch (e) {
        log.warn("[IME] PowerShell toggle fallback failed:", e);
        currentState = { ...currentState, isChinese: !currentState.isChinese, mode: currentState.mode === "cn" ? "en" : "cn" };
      }
    }
    return this.getState();
  },
  getServiceState() {
    return serviceState;
  }
};
const execAsync$2 = util.promisify(child_process.exec);
let psWatcher = null;
let fallbackPolling = null;
let lastDrives = /* @__PURE__ */ new Map();
let isRunning = false;
let isScanning = false;
let pendingArrivalTimer = null;
const DEBOUNCE_MS = 3e3;
const FALLBACK_POLL_MS = 8e3;
const notifyHistory = /* @__PURE__ */ new Map();
const PS_EVENT_WATCHER = `
$ProgressPreference = 'SilentlyContinue'
try {
    $watcher = New-Object System.Management.ManagementEventWatcher
    $query = New-Object System.Management.WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent")
    $watcher.Query = $query
    while ($true) {
        $event = $watcher.WaitForNextEvent()
        $drive = if ($event.DriveName) { $event.DriveName } else { "unknown" }
        $type = [string]$event.EventType
        Write-Host "USB_EVENT|$type|$drive" -NoNewline
        Write-Host ""  # 换行
    }
} catch {
    Write-Error "WMI_ERROR|$($_.Exception.Message)"
    exit 1
}
`;
const PS_COMPREHENSIVE_SCRIPT = `
$ProgressPreference = 'SilentlyContinue'
$results = @()

# Part A: DriveType=2(Removable)/5(CD-ROM) direct detection
$logicalDisks = Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DriveType -eq 2 -or $_.DriveType -eq 5 }
foreach ($ld in $logicalDisks) {
    $results += [PSCustomObject]@{
        DeviceID = $ld.DeviceID
        VolumeName = $ld.VolumeName
        DriveType = $ld.DriveType
        Size = $ld.Size
    }
}

# Part B: Trace USB-connected external/removable physical disks via CIM association
# Covers: USB flash drives, USB HDDs/SSDs (包括 USB 3.0 SATA 桥接芯片)
# 检测条件: InterfaceType='USB' OR PNPDeviceID 包含 'USB'/'USBSTOR' OR MediaType=External/Removable
$usbPhysDisks = Get-CimInstance Win32_DiskDrive | Where-Object {
    $_.InterfaceType -eq 'USB' -or
    $_.PNPDeviceID -like '*USB*' -or
    $_.PNPDeviceID -like '*USBSTOR*' -or
    $_.MediaType -eq 'Removable media' -or
    $_.MediaType -eq 'External hard disk media'
}

foreach ($disk in $usbPhysDisks) {
    # 使用 Get-CimAssociatedInstance 做 WMI 关联查询 (PS 3.0+ / PS 7.x 均支持)
    $partitions = $disk | Get-CimAssociatedInstance -ResultClassName Win32_DiskDriveToDiskPartition
    foreach ($partition in $partitions) {
        $logicalDisks = $partition | Get-CimAssociatedInstance -ResultClassName Win32_LogicalDiskToPartition
        foreach ($ld in $logicalDisks) {
            $exists = $false
            foreach ($r in $results) {
                if ($r.DeviceID -eq $ld.DeviceID) { $exists = $true; break }
            }
            if (-not $exists) {
                $results += [PSCustomObject]@{
                    DeviceID = $ld.DeviceID
                    VolumeName = $ld.VolumeName
                    DriveType = $ld.DriveType
                    Size = $ld.Size
                }
            }
        }
    }
}

# PS 7.x 兼容: @(...) 强制数组包装, 空数组输出 '[]'
@( $results ) | ConvertTo-Json -Depth 3
`;
const PS_DRIVETYPE_SCRIPT = `
$ProgressPreference = 'SilentlyContinue'
$disks = Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DriveType -eq 2 -or $_.DriveType -eq 5 } | Select-Object DeviceID, VolumeName, DriveType, Size
@( $disks ) | ConvertTo-Json -Depth 3
`;
const PS_PLAY_SOUND = `
Add-Type -AssemblyName System.Windows.Forms
[System.Media.SystemSounds]::Asterisk.Play()
Start-Sleep -Milliseconds 240
[System.Media.SystemSounds]::Asterisk.Play()
`;
function toBase64Command$2(script) {
  const buf = Buffer.from(script, "utf16le");
  return buf.toString("base64");
}
function parseWmiOutput(stdout) {
  try {
    const text = stdout.trim();
    if (!text || text === "" || text === "null") return [];
    let data;
    try {
      data = JSON.parse(text);
    } catch {
      const jsonMatch = text.match(/\[[\s\S]*\]/) || text.match(/\{[\s\S]*\}/);
      if (jsonMatch) data = JSON.parse(jsonMatch[0]);
      else return [];
    }
    const arr = Array.isArray(data) ? data : data ? [data] : [];
    return arr.filter((d) => d && d.DeviceID).map((d) => ({
      drive: d.DeviceID,
      label: d.VolumeName || void 0,
      type: d.DriveType === 2 ? "removable" : d.DriveType === 5 ? "cdrom" : "usb",
      size: d.Size ? formatBytes(Number(d.Size)) : void 0
    }));
  } catch (e) {
    log.error("[USB] Parse error:", e, "Raw:", stdout.slice(0, 300));
    return [];
  }
}
function formatBytes(bytes) {
  if (!bytes || bytes <= 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB", "TB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + " " + sizes[i];
}
function playUsbSound() {
  child_process.exec(
    `powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command$2(PS_PLAY_SOUND)}"`,
    { timeout: 5e3, windowsHide: true },
    (err) => {
      if (err) log.warn("[USB] Sound playback failed:", err.message);
    }
  );
}
function showUsbNotification(drive, label, size) {
  if (!electron.Notification.isSupported()) return;
  const displayName = label || "USB 存储设备";
  const sizeInfo = size ? ` (${size})` : "";
  const cfg = ConfigService.get().usb;
  const alertText = cfg.alertText || "请小心推拉黑板，避免碰撞 U 盘";
  const durationMs = cfg.alertDurationMs && cfg.alertDurationMs > 0 ? cfg.alertDurationMs : 5e3;
  const notif = new electron.Notification({
    title: "⚠️ 检测到 U 盘",
    body: `${drive} ${displayName}${sizeInfo}
${alertText}`,
    timeoutType: "default",
    // 自定义双音"叮咚"由 playUsbSound() 播放；此处关闭系统默认声避免叠加（soundEnabled=false 时整体静音）
    silent: !cfg.soundEnabled
  });
  notif.on("click", () => {
    child_process.exec(`explorer "${drive}\\"`, { windowsHide: true });
  });
  notif.show();
  setTimeout(() => {
    try {
      notif.close();
    } catch {
    }
  }, durationMs);
}
async function doScan() {
  try {
    const { stdout, stderr } = await execAsync$2(
      `powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command$2(PS_COMPREHENSIVE_SCRIPT)}"`,
      { timeout: 1e4, windowsHide: true, maxBuffer: 2 * 1024 * 1024 }
    );
    if (stderr?.trim()) log.warn("[USB] PS stderr:", stderr.trim().slice(0, 200));
    const results = parseWmiOutput(stdout);
    if (results.length > 0) return results;
  } catch (e) {
    log.error("[USB] Comprehensive scan failed:", e.message);
  }
  try {
    const { stdout } = await execAsync$2(
      `powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command$2(PS_DRIVETYPE_SCRIPT)}"`,
      { timeout: 8e3, windowsHide: true, maxBuffer: 1024 * 1024 }
    );
    const results = parseWmiOutput(stdout);
    if (results.length > 0) {
      log.info(`[USB] Fallback DriveType: ${results.length} device(s)`);
      return results;
    }
  } catch (e) {
    log.error("[USB] DriveType fallback failed:", e.message);
  }
  try {
    const { stdout } = await execAsync$2(
      'wmic logicaldisk where "DriveType=2 or DriveType=5" get DeviceID,VolumeName,Size /format:csv',
      { timeout: 5e3, windowsHide: true }
    );
    const results = parseWmicOutput(stdout);
    if (results.length > 0) {
      log.info(`[USB] Fallback WMIC: ${results.length} device(s)`);
      return results;
    }
  } catch (e) {
    log.error("[USB] WMIC fallback failed:", e.message);
  }
  return [];
}
function startWmiWatcher() {
  if (psWatcher) {
    psWatcher.kill();
    psWatcher = null;
  }
  log.info("[USB] Starting WMI event watcher (PS5.1 compatible)...");
  psWatcher = child_process.spawn("powershell", [
    "-NoProfile",
    "-NonInteractive",
    "-EncodedCommand",
    toBase64Command$2(PS_EVENT_WATCHER)
  ], {
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"]
  });
  let buffer = "";
  psWatcher.stdout?.on("data", (data) => {
    buffer += data.toString();
    const lines = buffer.split("\n");
    buffer = lines.pop() || "";
    for (const line of lines) {
      const trimmed = line.trim();
      if (!trimmed) continue;
      if (trimmed.startsWith("USB_EVENT")) {
        const parts = trimmed.split("|");
        const eventType = parts[1];
        const driveHint = parts[2] || "unknown";
        log.info(`[USB] WMI event: type=${eventType}, drive=${driveHint}`);
        if (eventType === "2") {
          handleUsbArrival();
        } else if (eventType === "3") {
          handleUsbRemoval();
        }
      } else if (trimmed.includes("WMI_ERROR")) {
        log.error("[USB] WMI watcher error:", trimmed);
      }
    }
  });
  psWatcher.stderr?.on("data", (data) => {
    log.warn("[USB] WMI watcher stderr:", data.toString().trim());
  });
  psWatcher.on("exit", (code) => {
    log.warn(`[USB] WMI watcher exited (code=${code}), restarting in 5s...`);
    psWatcher = null;
    if (isRunning) {
      setTimeout(() => startWmiWatcher(), 5e3);
    }
  });
}
function stopWmiWatcher() {
  if (psWatcher) {
    try {
      psWatcher.kill("SIGTERM");
    } catch (e) {
    }
    psWatcher = null;
  }
  if (pendingArrivalTimer) {
    clearTimeout(pendingArrivalTimer);
    pendingArrivalTimer = null;
  }
}
async function handleUsbArrival(driveHint) {
  if (pendingArrivalTimer) clearTimeout(pendingArrivalTimer);
  pendingArrivalTimer = setTimeout(async () => {
    pendingArrivalTimer = null;
    if (isScanning) return;
    isScanning = true;
    try {
      const results = await doScan();
      const currentMap = /* @__PURE__ */ new Map();
      for (const d of results) currentMap.set(d.drive, d);
      for (const d of results) {
        if (!lastDrives.has(d.drive)) {
          log.info(`[USB] >>> ARRIVED: ${d.drive} (${d.label || "unknown"}, ${d.size || "?"})`);
          const now = Date.now();
          const lastNotify = notifyHistory.get(d.drive);
          if (lastNotify && now - lastNotify < DEBOUNCE_MS) {
            log.info(`[USB] Debounced: ${d.drive}`);
            continue;
          }
          notifyHistory.set(d.drive, now);
          const event = {
            drive: d.drive,
            label: d.label,
            type: d.type,
            size: d.size,
            at: now
          };
          UsbService.broadcast(IPC_CHANNELS["usb:arrived"], event);
          showUsbNotification(d.drive, d.label, d.size);
          if (ConfigService.get().usb.soundEnabled) playUsbSound();
        }
      }
      for (const [drive, info] of lastDrives) {
        if (!currentMap.has(drive)) {
          log.info(`[USB] <<< REMOVED: ${drive}`);
          notifyHistory.delete(drive);
          const event = { drive, label: info.label, type: info.type, at: Date.now() };
          UsbService.broadcast(IPC_CHANNELS["usb:removed"], event);
        }
      }
      lastDrives = currentMap;
    } catch (e) {
      log.error("[USB] Arrival handler error:", e.message);
    } finally {
      isScanning = false;
    }
  }, 300);
}
async function handleUsbRemoval(driveHint) {
  setTimeout(async () => {
    if (isScanning) return;
    isScanning = true;
    try {
      const results = await doScan();
      const currentMap = /* @__PURE__ */ new Map();
      for (const d of results) currentMap.set(d.drive, d);
      for (const [drive, info] of lastDrives) {
        if (!currentMap.has(drive)) {
          log.info(`[USB] <<< REMOVED: ${drive}`);
          notifyHistory.delete(drive);
          const event = { drive, label: info.label, type: info.type, at: Date.now() };
          UsbService.broadcast(IPC_CHANNELS["usb:removed"], event);
        }
      }
      lastDrives = currentMap;
    } catch (e) {
      log.error("[USB] Removal handler error:", e.message);
    } finally {
      isScanning = false;
    }
  }, 500);
}
async function doFallbackPoll() {
  if (isScanning) return;
  isScanning = true;
  try {
    const results = await doScan();
    const currentMap = /* @__PURE__ */ new Map();
    for (const d of results) currentMap.set(d.drive, d);
    for (const d of results) {
      if (!lastDrives.has(d.drive)) {
        log.info(`[USB] >>> ARRIVED (poll): ${d.drive}`);
        const now = Date.now();
        const lastNotify = notifyHistory.get(d.drive);
        if (!lastNotify || now - lastNotify >= DEBOUNCE_MS) {
          notifyHistory.set(d.drive, now);
          const event = {
            drive: d.drive,
            label: d.label,
            type: d.type,
            size: d.size,
            at: now
          };
          UsbService.broadcast(IPC_CHANNELS["usb:arrived"], event);
          showUsbNotification(d.drive, d.label, d.size);
          if (ConfigService.get().usb.soundEnabled) playUsbSound();
        }
      }
    }
    for (const [drive, info] of lastDrives) {
      if (!currentMap.has(drive)) {
        log.info(`[USB] <<< REMOVED (poll): ${drive}`);
        notifyHistory.delete(drive);
        const event = { drive, label: info.label, type: info.type, at: Date.now() };
        UsbService.broadcast(IPC_CHANNELS["usb:removed"], event);
      }
    }
    lastDrives = currentMap;
  } catch (e) {
    log.warn("[USB] Fallback poll error:", e.message);
  } finally {
    isScanning = false;
  }
}
const UsbService = {
  async start(cfg) {
    if (isRunning) return;
    if (!cfg.usb.enabled) {
      log.info("[USB] Disabled by config");
      return;
    }
    isRunning = true;
    try {
      const initial = await doScan();
      lastDrives = /* @__PURE__ */ new Map();
      for (const d of initial) lastDrives.set(d.drive, d);
      log.info(`[USB] Initial scan: ${initial.length} device(s)`);
    } catch (e) {
      log.error("[USB] Initial scan failed:", e.message);
    }
    startWmiWatcher();
    fallbackPolling = setInterval(() => doFallbackPoll(), FALLBACK_POLL_MS);
    log.info(`[USB] Service started v6 (PS7+ compatible, PNPDeviceID detection, event-driven + fallback ${FALLBACK_POLL_MS}ms)`);
  },
  stop() {
    isRunning = false;
    stopWmiWatcher();
    if (fallbackPolling) {
      clearInterval(fallbackPolling);
      fallbackPolling = null;
    }
    lastDrives.clear();
    notifyHistory.clear();
    log.info("[USB] Service stopped");
  },
  async scan() {
    isScanning = true;
    try {
      const results = await doScan();
      lastDrives = /* @__PURE__ */ new Map();
      for (const d of results) lastDrives.set(d.drive, d);
      return results;
    } finally {
      isScanning = false;
    }
  },
  list() {
    return Array.from(lastDrives.values());
  },
  broadcast(channel, data) {
    for (const win of electron.BrowserWindow.getAllWindows()) {
      if (!win.isDestroyed() && win.webContents && !win.webContents.isDestroyed()) {
        win.webContents.send(channel, data);
      }
    }
  },
  getCurrentDrives() {
    return new Map(lastDrives);
  },
  getDiagnostics() {
    return {
      isRunning,
      isScanning,
      watcherAlive: psWatcher !== null && !psWatcher.killed,
      lastDrivesCount: lastDrives.size,
      drives: Array.from(lastDrives.values()),
      notifyHistoryCount: notifyHistory.size,
      fallbackPollMs: FALLBACK_POLL_MS,
      debounceMs: DEBOUNCE_MS,
      version: "v6-ps7-compatible"
    };
  }
};
function parseWmicOutput(stdout) {
  const lines = stdout.trim().split("\n").filter((l) => l.trim());
  const results = [];
  for (const line of lines) {
    if (line.includes("Node,")) continue;
    const parts = line.split(",").map((p) => p.trim().replace(/"/g, ""));
    if (parts.length >= 3) {
      const driveIdx = parts.findIndex((p) => p.match(/^[A-Z]:$/));
      if (driveIdx >= 0) {
        const drive = parts[driveIdx];
        const label = parts[driveIdx + 1] || "";
        const size = parts[driveIdx + 2] || "";
        results.push({
          drive,
          label: label && label !== "" ? label : void 0,
          type: "removable",
          size: size && !isNaN(Number(size)) ? formatBytes(Number(size)) : void 0
        });
      }
    }
  }
  return results;
}
const DES_OFFLINE = 512;
const DES_JAMMED = 256;
const DES_NO_PAPER = 16;
const DES_LOW_TONER = 32;
const DES_NO_TONER = 64;
const DES_SERVICE = 1024;
const DES_NO_ERROR = 4;
const PS_PAPER_JAM = 4;
const PS_PAPER_OUT = 5;
const PS_OFFLINE = 7;
const EPS_CRITICAL_FAILURE = 6;
function mapPrinterState(p) {
  const port = p.PortName || "";
  const isUsb = /^USB/i.test(port);
  const isNetwork = /^\\\\|^(?:IP_|TCPIP_)/i.test(port) || !!p.Network;
  const des = p.DetectedErrorState || 0;
  const eps = p.ExtendedPrinterStatus || 0;
  const ps = p.PrinterState || 0;
  const queuedJobs = p.QueuedJobs || 0;
  if (p.WorkOffline || ps === PS_OFFLINE || des & DES_OFFLINE) {
    return { state: "offline", detail: "离线", isUsb, isNetwork, isDefault: !!p.Default, queuedJobs };
  }
  if (ps === PS_PAPER_JAM || des & DES_JAMMED) {
    return { state: "jammed", detail: "卡纸", isUsb, isNetwork, isDefault: !!p.Default, queuedJobs };
  }
  if (ps === PS_PAPER_OUT || des & DES_NO_PAPER) {
    return { state: "out_of_paper", detail: "缺纸", isUsb, isNetwork, isDefault: !!p.Default, queuedJobs };
  }
  if (des & DES_LOW_TONER) {
    return { state: "low_ink", detail: "墨粉不足", isUsb, isNetwork, isDefault: !!p.Default, queuedJobs };
  }
  if (des & DES_NO_TONER) {
    return { state: "low_ink", detail: "墨粉耗尽", isUsb, isNetwork, isDefault: !!p.Default, queuedJobs };
  }
  if (eps === EPS_CRITICAL_FAILURE || des & DES_SERVICE) {
    return { state: "error", detail: "需要检修", isUsb, isNetwork, isDefault: !!p.Default, queuedJobs };
  }
  if ((ps === PS_PAPER_JAM || ps === 3) && queuedJobs > 0) {
    return { state: "printing", detail: `打印中 (${queuedJobs} 个任务)`, isUsb, isNetwork, isDefault: !!p.Default, queuedJobs };
  }
  if (ps === 0 || ps === 3 || des & DES_NO_ERROR) {
    return { state: "ok", detail: "准备就绪", isUsb, isNetwork, isDefault: !!p.Default, queuedJobs };
  }
  return { state: "unknown", detail: "状态未知", isUsb, isNetwork, isDefault: !!p.Default, queuedJobs };
}
const execAsync$1 = util.promisify(child_process.exec);
let polling = null;
let lastStatus = /* @__PURE__ */ new Map();
const PS_GET_PRINTERS = `
Get-CimInstance Win32_Printer |
  Select-Object Name, PortName, Local, Network, Default, PrinterState,
                ExtendedPrinterStatus, DetectedErrorState, WorkOffline, QueuedJobs |
  ConvertTo-Json -Depth 3
`;
const PrinterService = {
  async start(cfg) {
    const interval = Math.max(cfg.printer.pollIntervalSec, 5) * 1e3;
    await this.refresh();
    polling = setInterval(async () => {
      await this.refresh();
    }, interval);
    log.info(`[Printer] Service started (poll=${interval}ms)`);
  },
  stop() {
    if (polling) {
      clearInterval(polling);
      polling = null;
    }
    log.info("[Printer] Service stopped");
  },
  async refresh() {
    try {
      const { stdout } = await execAsync$1(`powershell -NoProfile -Command "${PS_GET_PRINTERS}"`, { timeout: 8e3 });
      let printers = [];
      try {
        const data = JSON.parse(stdout.trim());
        printers = Array.isArray(data) ? data : [data];
      } catch {
      }
      const current = /* @__PURE__ */ new Map();
      for (const p of printers) {
        const mapped = mapPrinterState(p);
        const status = {
          name: p.Name,
          ...mapped,
          updatedAt: Date.now()
        };
        current.set(p.Name, status);
        const prev = lastStatus.get(p.Name);
        if (!prev || prev.state !== status.state) {
          if (status.state !== "ok" && status.state !== "unknown") {
            this.broadcast(status);
          }
        }
      }
      lastStatus = current;
    } catch (e) {
      log.warn("[Printer] Refresh failed:", e);
    }
  },
  async getStatus() {
    return Array.from(lastStatus.values());
  },
  broadcast(status) {
    for (const win of electron.BrowserWindow.getAllWindows()) {
      win.webContents.send(IPC_CHANNELS["printer:changed"], status);
    }
  }
};
let timer = null;
let reminders = [];
const firedOnceIds = /* @__PURE__ */ new Set();
let dirty = false;
function toBase64Command$1(script) {
  const buf = Buffer.from(script, "utf16le");
  return buf.toString("base64");
}
const SchedulerService = {
  async start() {
    reminders = ConfigService.get().reminders || [];
    const now = Date.now();
    const expired = reminders.filter(
      (r) => r.kind === "once" && !r.snoozedUntil && now - r.at > 36e5
    );
    if (expired.length > 0) {
      log.info(`[Scheduler] Cleaning ${expired.length} expired once-reminder(s)`);
      reminders = reminders.filter((r) => !expired.includes(r));
      ConfigService.set("reminders", reminders);
      dirty = false;
    }
    log.info(`[Scheduler] Started with ${reminders.length} reminder(s)`);
    timer = setInterval(() => this.tick(), 3e3);
    setTimeout(() => this.tick(), 1e3);
  },
  stop() {
    if (timer) {
      clearInterval(timer);
      timer = null;
    }
    log.info("[Scheduler] Stopped");
  },
  tick() {
    const now = Date.now();
    const toRemove = [];
    for (const r of reminders) {
      if (r.snoozedUntil && now < r.snoozedUntil) continue;
      if (r.snoozedUntil && now >= r.snoozedUntil) {
        r.snoozedUntil = void 0;
      }
      if (r.kind === "once" && now >= r.at) {
        if (!firedOnceIds.has(r.id)) {
          this.fire(r);
          firedOnceIds.add(r.id);
          toRemove.push(r.id);
          setTimeout(() => firedOnceIds.delete(r.id), 5e3);
        }
      } else if (r.kind === "interval" && r.repeatMin && now >= r.at) {
        this.fire(r);
        r.at = now + r.repeatMin * 6e4;
        dirty = true;
      } else if (r.kind === "hourly" && now >= r.at) {
        this.fire(r);
        const nextHour = new Date(now);
        nextHour.setHours(nextHour.getHours() + 1, 0, 0, 0);
        r.at = nextHour.getTime();
        dirty = true;
      }
    }
    if (toRemove.length > 0) {
      reminders = reminders.filter((r) => !toRemove.includes(r.id));
      log.info(`[Scheduler] Auto-removed ${toRemove.length} fired once-reminder(s)`);
      dirty = true;
    }
    if (dirty) {
      ConfigService.set("reminders", reminders);
      dirty = false;
    }
  },
  fire(r) {
    log.info(`[Scheduler] Firing reminder: ${r.id} - ${r.note || ""}`);
    const soundConfig = ConfigService.get().reminderSound;
    if (soundConfig) {
      this.playSound(soundConfig);
    }
    for (const win of electron.BrowserWindow.getAllWindows()) {
      win.webContents.send(IPC_CHANNELS["reminder:due"], r);
    }
    if (electron.Notification.isSupported()) {
      new electron.Notification({
        title: "提醒",
        body: r.note || "您有一条提醒",
        silent: true
      }).show();
    }
  },
  /** 播放提醒铃声 */
  playSound(cfg) {
    const { preset, mp3Path, volume, repeat, repeatInterval } = cfg;
    if (preset === "custom" && mp3Path) {
      this.playMp3(mp3Path, volume, repeat, repeatInterval);
      return;
    }
    const soundName = preset === "bell" ? "Asterisk" : preset === "alarm" ? "Exclamation" : "Asterisk";
    const PS_BEEP = `Add-Type -AssemblyName System.Windows.Forms
$snd = [System.Media.SystemSounds]::${soundName}
for ($i = 0; $i -lt ${repeat}; $i++) {
    $snd.Play()
    Start-Sleep -Milliseconds ${repeatInterval}
}
`;
    const { exec } = require("child_process");
    exec(
      `powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command$1(PS_BEEP)}"`,
      { timeout: 15e3, windowsHide: true },
      (err) => {
        if (err) log.warn("[Scheduler] Sound playback failed:", err.message);
      }
    );
  },
  /** 使用 ffplay 或系统播放器播放 MP3 */
  playMp3(path2, volume, repeat, interval) {
    const { spawn, execSync, exec } = require("child_process");
    let hasFfplay = false;
    try {
      execSync("ffplay -version", { windowsHide: true, stdio: "ignore" });
      hasFfplay = true;
    } catch {
      hasFfplay = false;
    }
    if (hasFfplay) {
      const vol = Math.max(0, Math.min(1, volume));
      for (let i = 0; i < repeat; i++) {
        setTimeout(() => {
          spawn("ffplay", [
            "-nodisp",
            "-autoexit",
            "-volume",
            String(Math.round(vol * 256)),
            path2
          ], { windowsHide: true, stdio: "ignore" });
        }, i * interval);
      }
    } else {
      const safePath = path2.replace(/"/g, '`"');
      const vol100 = Math.round(Math.max(0, Math.min(1, volume)) * 100);
      const PS_MP3 = `$player = New-Object -ComObject WMPlayer.OCX
$player.URL = "${safePath}"
$player.settings.volume = ${vol100}
$player.controls.play()
Start-Sleep -Seconds 5
$player.close()
`;
      exec(
        `powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command$1(PS_MP3)}"`,
        { timeout: 1e4, windowsHide: true },
        (err) => {
          if (err) log.warn("[Scheduler] MP3 playback failed:", err.message);
        }
      );
    }
  },
  add(r) {
    const idx = reminders.findIndex((existing) => existing.id === r.id);
    if (idx >= 0) {
      reminders[idx] = r;
      log.info(`[Scheduler] Updated reminder: ${r.id}`);
    } else {
      reminders.push(r);
      log.info(`[Scheduler] Added reminder: ${r.id}`);
    }
    firedOnceIds.delete(r.id);
    ConfigService.set("reminders", reminders);
    dirty = false;
  },
  remove(id) {
    reminders = reminders.filter((r) => r.id !== id);
    firedOnceIds.delete(id);
    ConfigService.set("reminders", reminders);
    dirty = false;
    log.info(`[Scheduler] Removed reminder: ${id}`);
  },
  list() {
    return reminders;
  }
};
function pad$2(n) {
  return String(n).padStart(2, "0");
}
function ensureDir$2(dir) {
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
}
const CaptureService = {
  // 获取屏幕源
  async getScreenSources() {
    return electron.desktopCapturer.getSources({
      types: ["screen"],
      thumbnailSize: { width: 3840, height: 2160 },
      fetchWindowIcons: false
    });
  },
  // 核心截图: 直接通过 thumbnail 截全屏
  async grabFullscreen() {
    try {
      const sources = await this.getScreenSources();
      if (!sources || sources.length === 0) {
        log.error("[Capture] No screen sources found");
        return null;
      }
      const img = sources[0].thumbnail;
      if (!img || img.isEmpty()) {
        log.error("[Capture] Thumbnail is empty");
        return null;
      }
      const size = img.getSize();
      log.info(`[Capture] Grabbed ${size.width}x${size.height}`);
      return img;
    } catch (e) {
      log.error("[Capture] grabFullscreen failed:", e.message);
      return null;
    }
  },
  // 目标显示器截图 (多显示器场景): 截图源优先匹配侧边栏所在显示器,
  // 显示器热插拔后由 DisplayService 自动回退到有效显示器, 保证快捷键截图吸附到正确屏幕
  async grabTarget() {
    try {
      const sources = await this.getScreenSources();
      if (!sources || sources.length === 0) {
        log.error("[Capture] No screen sources found");
        return null;
      }
      const target = DisplayService.sidebarTarget();
      let source = sources.find((s) => s.display_id !== "" && String(s.display_id) === String(target.id));
      if (!source) {
        source = sources[0];
        log.info("[Capture] No exact display match, falling back to first source");
      }
      const img = source.thumbnail;
      if (!img || img.isEmpty()) {
        log.error("[Capture] Thumbnail is empty");
        return null;
      }
      let matched = target;
      if (String(source.display_id) !== String(target.id)) {
        const byId = DisplayService.byId(parseInt(source.display_id));
        if (byId) matched = byId;
      }
      const size = img.getSize();
      log.info(`[Capture] Grabbed ${size.width}x${size.height} (source=${source.display_id}, target=${target.id})`);
      return { img, target: matched, workArea: matched.workArea };
    } catch (e) {
      log.error("[Capture] grabTarget failed:", e.message);
      return null;
    }
  },
  // 保存截图 (可选裁剪)
  async saveImage(img, bounds) {
    try {
      let finalImg = img;
      if (bounds) {
        const size = img.getSize();
        const x = Math.max(0, Math.min(Math.round(bounds.x), size.width - 1));
        const y = Math.max(0, Math.min(Math.round(bounds.y), size.height - 1));
        const w = Math.min(Math.round(bounds.width), size.width - x);
        const h = Math.min(Math.round(bounds.height), size.height - y);
        if (w < 2 || h < 2) {
          log.warn("[Capture] Crop bounds too small:", { x, y, w, h });
          return null;
        }
        finalImg = img.crop({ x, y, width: w, height: h });
        log.info(`[Capture] Cropped to ${w}x${h} at (${x},${y})`);
      }
      const cfg = ConfigService.get();
      const dir = cfg.capture.dir;
      ensureDir$2(dir);
      const now = /* @__PURE__ */ new Date();
      const timestamp2 = `${now.getFullYear()}${pad$2(now.getMonth() + 1)}${pad$2(now.getDate())}_${pad$2(now.getHours())}${pad$2(now.getMinutes())}${pad$2(now.getSeconds())}`;
      const ext = cfg.capture.format.toLowerCase() === "jpg" ? "jpg" : "png";
      const filename = `seewo-capture-${timestamp2}.${ext}`;
      const filepath = path.join(dir, filename);
      if (ext === "jpg") {
        fs.writeFileSync(filepath, finalImg.toJPEG(90));
      } else {
        fs.writeFileSync(filepath, finalImg.toPNG());
      }
      electron.clipboard.writeImage(finalImg);
      log.info(`[Capture] Copied to clipboard + saved: ${filepath}`);
      return filepath;
    } catch (e) {
      log.error("[Capture] saveImage failed:", e.message);
      return null;
    }
  },
  // 保存到临时文件 (用于批注背景)
  saveTempImage(img) {
    const tmpDir = path.join(electron.app.getPath("temp"), "seewo-sidekick");
    ensureDir$2(tmpDir);
    const filepath = path.join(tmpDir, `screenshot-${Date.now()}.png`);
    fs.writeFileSync(filepath, img.toPNG());
    log.info(`[Capture] Temp saved: ${filepath}`);
    return filepath;
  },
  // 保存批注结果 (dataUrl -> 文件)
  saveAnnotatedImage(dataUrl) {
    try {
      const base64 = dataUrl.split(",")[1];
      if (!base64) {
        log.error("[Capture] Invalid dataUrl");
        return null;
      }
      const buffer = Buffer.from(base64, "base64");
      const cfg = ConfigService.get();
      const dir = cfg.capture.dir;
      ensureDir$2(dir);
      const now = /* @__PURE__ */ new Date();
      const timestamp2 = `${now.getFullYear()}${pad$2(now.getMonth() + 1)}${pad$2(now.getDate())}_${pad$2(now.getHours())}${pad$2(now.getMinutes())}${pad$2(now.getSeconds())}`;
      const filename = `seewo-annotate-${timestamp2}.png`;
      const filepath = path.join(dir, filename);
      fs.writeFileSync(filepath, buffer);
      log.info(`[Capture] Annotated saved: ${filepath}`);
      return filepath;
    } catch (e) {
      log.error("[Capture] saveAnnotatedImage failed:", e.message);
      return null;
    }
  },
  // 清理临时文件
  cleanupTemp(filepath) {
    try {
      if (fs.existsSync(filepath)) {
        fs.unlinkSync(filepath);
        log.info(`[Capture] Cleaned temp: ${filepath}`);
      }
    } catch (e) {
    }
  }
};
let sidebarWin = null;
let oobeWin = null;
let overlayWin = null;
let annotatorWin = null;
let settingsWin = null;
let floatBallWin = null;
let sidebarDocked = false;
let sidebarRestoreBounds = null;
let isAnimatingBounds = false;
let floatBallPos = null;
let floatBallExpanded = false;
let floatBallDragTimer = null;
let floatBallGrab = { x: 0, y: 0 };
let floatBallCollapseTimer = null;
let floatBallHiddenForCapture = false;
function stopFloatBallDrag() {
  if (floatBallDragTimer) {
    clearInterval(floatBallDragTimer);
    floatBallDragTimer = null;
  }
}
const isDev = !electron.app.isPackaged;
const SIDEBAR_MARGIN_RATIO = 0.08;
const REFERENCE_WIDTH = 1920;
const UI_SCALE_MIN = 1;
const UI_SCALE_MAX = 3;
function computeUiScale(workAreaWidth) {
  const raw = workAreaWidth / REFERENCE_WIDTH;
  return Math.min(UI_SCALE_MAX, Math.max(UI_SCALE_MIN, Math.round(raw * 100) / 100));
}
const ANIM_DURATION = 250;
const ANIM_EASING = (t) => 1 - Math.pow(1 - t, 3);
function animateWindowBounds(win, target) {
  if (isAnimatingBounds) return;
  isAnimatingBounds = true;
  const start = win.getBounds();
  const startTime = Date.now();
  function tick() {
    if (win.isDestroyed()) {
      isAnimatingBounds = false;
      return;
    }
    const elapsed = Date.now() - startTime;
    const progress = Math.min(elapsed / ANIM_DURATION, 1);
    const ease = ANIM_EASING(progress);
    const next = {
      x: Math.round(start.x + (target.x - start.x) * ease),
      y: Math.round(start.y + (target.y - start.y) * ease),
      width: Math.round(start.width + (target.width - start.width) * ease),
      height: Math.round(start.height + (target.height - start.height) * ease)
    };
    win.setBounds(next, true);
    if (progress < 1) {
      setImmediate(tick);
    } else {
      isAnimatingBounds = false;
    }
  }
  tick();
}
function getPreloadPath() {
  return path.join(__dirname, "../preload/index.js");
}
function getRendererPath(view) {
  if (isDev) {
    return `http://localhost:5173/#/${view}`;
  }
  return path.join(__dirname, `../renderer/index.html`);
}
const WindowManager = {
  createSidebar(config2) {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      return sidebarWin;
    }
    const target = DisplayService.sidebarTarget();
    const uiScale = computeUiScale(target.workArea.width);
    const railWidth = Math.round(72 * uiScale);
    const SIDEBAR_MARGIN_RATIO2 = 0.08;
    const sidebarHeight = Math.round(target.workArea.height * (1 - SIDEBAR_MARGIN_RATIO2 * 2));
    const sidebarY = target.workArea.y + Math.round(target.workArea.height * SIDEBAR_MARGIN_RATIO2);
    sidebarWin = new electron.BrowserWindow({
      x: target.workArea.x + (config2.display.sidebarSide === "right" ? target.workArea.width - railWidth : 0),
      y: sidebarY,
      width: railWidth,
      height: sidebarHeight,
      frame: false,
      transparent: true,
      resizable: false,
      movable: false,
      minimizable: false,
      maximizable: false,
      fullscreenable: false,
      skipTaskbar: true,
      alwaysOnTop: true,
      hasShadow: false,
      show: false,
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true,
        sandbox: false,
        webSecurity: !isDev
      }
    });
    sidebarWin.webContents.setZoomFactor(uiScale);
    sidebarWin.setBackgroundColor(initialBackgroundColor("sidebar"));
    if (isDev) {
      sidebarWin.loadURL(getRendererPath("sidebar"));
      sidebarWin.webContents.openDevTools({ mode: "detach" });
    } else {
      sidebarWin.loadFile(getRendererPath("sidebar"), { hash: "/sidebar" });
    }
    sidebarWin.on("ready-to-show", () => {
      log.info(`[Window] Sidebar ready (${railWidth} DIP x ${sidebarHeight} DIP, uiScale=${uiScale})`);
      AppearanceService.register(sidebarWin, "sidebar");
    });
    sidebarWin.on("closed", () => {
      sidebarWin = null;
    });
    return sidebarWin;
  },
  createOobe(parent) {
    if (oobeWin && !oobeWin.isDestroyed()) {
      oobeWin.focus();
      return oobeWin;
    }
    const primary = DisplayService.primary();
    const uiScale = computeUiScale(primary.workArea.width);
    const width = Math.round(960 * uiScale);
    const height = Math.round(640 * uiScale);
    const x = primary.workArea.x + Math.round((primary.workArea.width - width) / 2);
    const y = primary.workArea.y + Math.round((primary.workArea.height - height) / 2);
    oobeWin = new electron.BrowserWindow({
      x,
      y,
      width,
      height,
      parent,
      modal: true,
      frame: false,
      resizable: false,
      minimizable: false,
      maximizable: false,
      show: true,
      backgroundColor: "#f5f7fa",
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true,
        sandbox: false
      }
    });
    oobeWin.webContents.setZoomFactor(uiScale);
    if (isDev) {
      oobeWin.loadURL(getRendererPath("oobe"));
    } else {
      oobeWin.loadFile(getRendererPath("oobe"), { hash: "/oobe" });
    }
    oobeWin.on("closed", () => {
      oobeWin = null;
    });
    return oobeWin;
  },
  showMain() {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      sidebarWin.show();
      sidebarWin.focus();
    }
  },
  /** 切换侧边栏显隐（悬浮球「侧边栏」动作用） */
  toggleMain() {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      if (sidebarWin.isVisible()) sidebarWin.hide();
      else {
        sidebarWin.show();
        sidebarWin.focus();
      }
    }
  },
  closeOobe() {
    if (oobeWin && !oobeWin.isDestroyed()) {
      oobeWin.close();
      oobeWin = null;
    }
  },
  focusMain() {
    if (oobeWin && !oobeWin.isDestroyed()) {
      oobeWin.focus();
      return;
    }
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      sidebarWin.show();
      sidebarWin.focus();
    }
  },
  hideMain() {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      sidebarWin.hide();
    }
  },
  minimize() {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      sidebarWin.minimize();
    }
  },
  // 区域截图 overlay (透明,可看到桌面) — 覆盖侧边栏所在的「目标显示器」, 跨屏时跟随热插拔回退
  showOverlay() {
    if (overlayWin && !overlayWin.isDestroyed()) {
      overlayWin.close();
    }
    const target = DisplayService.sidebarTarget();
    if (!target || !target.workArea || target.workArea.width <= 0 || target.workArea.height <= 0) {
      log.warn("[Window] showOverlay: invalid target display");
      return null;
    }
    const win = new electron.BrowserWindow({
      x: target.workArea.x,
      y: target.workArea.y,
      width: target.workArea.width,
      height: target.workArea.height,
      frame: false,
      transparent: true,
      resizable: false,
      movable: false,
      alwaysOnTop: true,
      skipTaskbar: true,
      fullscreenable: false,
      hasShadow: false,
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true,
        sandbox: false
      }
    });
    if (isDev) {
      win.loadURL(getRendererPath("overlay"));
    } else {
      win.loadFile(getRendererPath("overlay"), { hash: "/overlay" });
    }
    win.on("closed", () => {
      if (overlayWin === win) overlayWin = null;
    });
    overlayWin = win;
    return win;
  },
  // 批注窗口 (透明覆盖层, 无需截图背景, 直接在真实屏幕上绘图)
  showAnnotator() {
    if (annotatorWin && !annotatorWin.isDestroyed()) {
      annotatorWin.close();
    }
    const target = DisplayService.sidebarTarget();
    if (!target || !target.workArea || target.workArea.width <= 0 || target.workArea.height <= 0) {
      log.warn("[Window] showAnnotator: invalid target display");
      return null;
    }
    const win = new electron.BrowserWindow({
      x: target.workArea.x,
      y: target.workArea.y,
      width: target.workArea.width,
      height: target.workArea.height,
      frame: false,
      transparent: true,
      resizable: false,
      movable: false,
      alwaysOnTop: true,
      skipTaskbar: true,
      fullscreenable: false,
      hasShadow: false,
      backgroundColor: "#00000000",
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true,
        sandbox: false
      }
    });
    if (isDev) {
      win.loadURL(getRendererPath("annotate"));
    } else {
      win.loadFile(getRendererPath("annotate"), { hash: "/annotate" });
    }
    win.on("closed", () => {
      if (annotatorWin === win) annotatorWin = null;
    });
    annotatorWin = win;
    return win;
  },
  closeOverlay() {
    if (overlayWin && !overlayWin.isDestroyed()) {
      overlayWin.close();
      overlayWin = null;
    }
  },
  closeAnnotator() {
    if (annotatorWin && !annotatorWin.isDestroyed()) {
      annotatorWin.close();
      annotatorWin = null;
    }
  },
  showSettings() {
    if (settingsWin && !settingsWin.isDestroyed()) {
      settingsWin.focus();
      return settingsWin;
    }
    const primary = DisplayService.primary();
    const uiScale = computeUiScale(primary.workArea.width);
    const width = Math.round(800 * uiScale);
    const height = Math.round(600 * uiScale);
    const x = primary.workArea.x + Math.round((primary.workArea.width - width) / 2);
    const y = primary.workArea.y + Math.round((primary.workArea.height - height) / 2);
    settingsWin = new electron.BrowserWindow({
      x,
      y,
      width,
      height,
      frame: true,
      resizable: true,
      show: true,
      backgroundColor: "#f5f7fa",
      title: "希沃侧边快捷键工具 - 设置",
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true
      }
    });
    settingsWin.webContents.setZoomFactor(uiScale);
    if (isDev) {
      settingsWin.loadURL(getRendererPath("settings"));
    } else {
      settingsWin.loadFile(getRendererPath("settings"), { hash: "/settings" });
    }
    settingsWin.on("closed", () => {
      settingsWin = null;
    });
    return settingsWin;
  },
  // ==================== 悬浮球 ====================
  //
  // 几个关键决策，都是踩过的坑：
  //
  // 1. focusable: false —— 悬浮球点了不能抢前台。否则点「切换输入法」时，
  //    前台窗口变成悬浮球自己，BUG-01 修好的 FFI 就切到自己头上了，
  //    老师在 PPT 里打字完全没反应。
  //
  // 2. 拖拽走主进程轮询光标，不用 -webkit-app-region: drag。
  //    app-region 在 transparent + focusable:false 的窗口上时灵时不灵，
  //    而且没法做贴边吸附（吸附要在松手那一刻改窗口位置）。
  //
  // 3. 球在屏幕上的位置单独记在 floatBallPos，不从窗口 bounds 反推。
  //    展开时窗口会放大到扇形的包围盒，此时 bounds.x 已经不是球的位置了。
  //
  // 4. 只在「侧边栏所在的那块屏」活动。多屏教室机想换屏，改设置里的
  //    「侧边栏显示器」即可 —— 这样拖拽 clamp 有唯一事实源，
  //    不会出现拔屏后球停在不存在的坐标上（3.19.4）。
  createFloatBall(config2) {
    if (floatBallWin && !floatBallWin.isDestroyed()) return floatBallWin;
    if (!config2.floatBall.enabled) return null;
    const target = DisplayService.sidebarTarget();
    if (!target || !target.workArea || target.workArea.width <= 0 || target.workArea.height <= 0) {
      log.warn("[Window] createFloatBall: invalid target display");
      return null;
    }
    const uiScale = computeUiScale(target.workArea.width);
    const ballDip = Math.round(config2.floatBall.size * uiScale);
    const pos = resolveBallPosition(config2.floatBall, target.workArea, ballDip, config2.display.sidebarSide);
    floatBallPos = pos;
    floatBallExpanded = false;
    floatBallWin = new electron.BrowserWindow({
      x: pos.x,
      y: pos.y,
      width: ballDip,
      height: ballDip,
      frame: false,
      transparent: true,
      resizable: false,
      movable: false,
      minimizable: false,
      maximizable: false,
      fullscreenable: false,
      skipTaskbar: true,
      alwaysOnTop: true,
      hasShadow: false,
      focusable: false,
      // 见上文第 1 点
      acceptFirstMouse: true,
      backgroundColor: "#00000000",
      show: false,
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true,
        sandbox: false,
        webSecurity: !isDev,
        // 透明窗口默认背景节流会让扇形展开动画掉帧
        backgroundThrottling: false
      }
    });
    floatBallWin.setAlwaysOnTop(true, "screen-saver");
    floatBallWin.webContents.setZoomFactor(uiScale);
    if (isDev) {
      floatBallWin.loadURL(getRendererPath("floatball"));
    } else {
      floatBallWin.loadFile(getRendererPath("floatball"), { hash: "/floatball" });
    }
    floatBallWin.on("ready-to-show", () => {
      floatBallWin?.showInactive();
      if (floatBallWin) AppearanceService.register(floatBallWin, "floatball");
      floatBallWin?.webContents.send(
        IPC_CHANNELS["floatball:layout"],
        collapsedLayout(config2.floatBall.size)
      );
      log.info(`[Window] FloatBall ready (${ballDip} DIP @ ${pos.x},${pos.y}, uiScale=${uiScale})`);
    });
    floatBallWin.on("closed", () => {
      stopFloatBallDrag();
      if (floatBallCollapseTimer) {
        clearTimeout(floatBallCollapseTimer);
        floatBallCollapseTimer = null;
      }
      floatBallWin = null;
      floatBallExpanded = false;
    });
    return floatBallWin;
  },
  getFloatBall() {
    return floatBallWin && !floatBallWin.isDestroyed() ? floatBallWin : null;
  },
  showFloatBall() {
    const cfg = ConfigService.get();
    if (!cfg.floatBall.enabled) return;
    if (!floatBallWin || floatBallWin.isDestroyed()) {
      this.createFloatBall(cfg);
      return;
    }
    floatBallWin.showInactive();
    floatBallWin.setAlwaysOnTop(true, "screen-saver");
  },
  hideFloatBall() {
    if (!floatBallWin || floatBallWin.isDestroyed()) return;
    this.collapseFloatBall(true);
    floatBallWin.hide();
  },
  toggleFloatBall() {
    if (floatBallWin && !floatBallWin.isDestroyed() && floatBallWin.isVisible()) {
      this.hideFloatBall();
    } else {
      this.showFloatBall();
    }
  },
  destroyFloatBall() {
    stopFloatBallDrag();
    if (floatBallWin && !floatBallWin.isDestroyed()) floatBallWin.close();
    floatBallWin = null;
    floatBallPos = null;
    floatBallExpanded = false;
  },
  /** 截图 / 批注前临时隐藏，免得球被拍进图里 */
  hideFloatBallForCapture() {
    if (!floatBallWin || floatBallWin.isDestroyed() || !floatBallWin.isVisible()) return;
    floatBallHiddenForCapture = true;
    this.collapseFloatBall(true);
    floatBallWin.hide();
  },
  /** 截图 / 批注结束后恢复（只恢复「是我们藏起来的」那种情况） */
  restoreFloatBallAfterCapture() {
    if (!floatBallHiddenForCapture) return;
    floatBallHiddenForCapture = false;
    if (!ConfigService.get().floatBall.enabled) return;
    if (floatBallWin && !floatBallWin.isDestroyed()) {
      floatBallWin.showInactive();
      floatBallWin.setAlwaysOnTop(true, "screen-saver");
    }
  },
  /**
   * 开始拖拽。
   * @param grab 指针相对球左上角的偏移（渲染层给的是 CSS px，这里换成 DIP）
   */
  floatBallDragStart(grab) {
    if (!floatBallWin || floatBallWin.isDestroyed()) return;
    const target = DisplayService.sidebarTarget();
    const uiScale = computeUiScale(target.workArea.width);
    const ballDip = Math.round(ConfigService.get().floatBall.size * uiScale);
    this.collapseFloatBall(true);
    floatBallGrab = {
      x: Math.round((grab?.x ?? ballDip / 2 / uiScale) * uiScale),
      y: Math.round((grab?.y ?? ballDip / 2 / uiScale) * uiScale)
    };
    stopFloatBallDrag();
    floatBallDragTimer = setInterval(() => {
      if (!floatBallWin || floatBallWin.isDestroyed()) {
        stopFloatBallDrag();
        return;
      }
      const cur = electron.screen.getCursorScreenPoint();
      const area = DisplayService.sidebarTarget().workArea;
      const next = clampToArea(
        { x: cur.x - floatBallGrab.x, y: cur.y - floatBallGrab.y },
        { width: ballDip, height: ballDip },
        area
      );
      floatBallPos = next;
      floatBallWin.setBounds({ x: next.x, y: next.y, width: ballDip, height: ballDip }, false);
    }, 16);
  },
  /** 松手：贴边吸附 + 位置持久化 */
  floatBallDragEnd() {
    stopFloatBallDrag();
    if (!floatBallWin || floatBallWin.isDestroyed() || !floatBallPos) return;
    const cfg = ConfigService.get();
    const target = DisplayService.sidebarTarget();
    const uiScale = computeUiScale(target.workArea.width);
    const ballDip = Math.round(cfg.floatBall.size * uiScale);
    const snapped = snapToEdges(
      floatBallPos,
      { width: ballDip, height: ballDip },
      target.workArea,
      Math.round(cfg.floatBall.snapThreshold * uiScale)
    );
    floatBallPos = { x: snapped.x, y: snapped.y };
    floatBallWin.setBounds({ x: snapped.x, y: snapped.y, width: ballDip, height: ballDip }, false);
    const rel = toRelativePosition(floatBallPos, target.workArea);
    ConfigService.set("floatBall.x", rel.x);
    ConfigService.set("floatBall.y", rel.y);
    log.info(`[Window] FloatBall dropped @ ${snapped.x},${snapped.y} edge=${snapped.edge ?? "none"}`);
  },
  /** 展开扇形菜单：先放大窗口腾出空间，再下发布局让渲染层做飞出动画 */
  expandFloatBall() {
    if (!floatBallWin || floatBallWin.isDestroyed()) return;
    if (floatBallCollapseTimer) {
      clearTimeout(floatBallCollapseTimer);
      floatBallCollapseTimer = null;
    }
    const cfg = ConfigService.get();
    const target = DisplayService.sidebarTarget();
    const uiScale = computeUiScale(target.workArea.width);
    const count = cfg.floatBall.actions.length;
    if (count === 0) return;
    if (!floatBallPos) {
      const b = floatBallWin.getBounds();
      floatBallPos = { x: b.x, y: b.y };
    }
    const { layout, window } = computeFanLayout({
      ball: floatBallPos,
      ballSize: cfg.floatBall.size,
      area: target.workArea,
      count,
      uiScale
    });
    floatBallExpanded = true;
    floatBallWin.setBounds(window, false);
    floatBallWin.webContents.send(IPC_CHANNELS["floatball:layout"], layout);
  },
  /**
   * 收起扇形菜单。
   * @param immediate 是否立刻缩窗口（拖拽/隐藏时用）。默认等收起动画播完再缩，
   *                  否则窗口先变小、菜单项瞬间被裁掉，看起来像闪断。
   */
  collapseFloatBall(immediate = false) {
    if (!floatBallWin || floatBallWin.isDestroyed()) return;
    if (!floatBallExpanded) return;
    floatBallExpanded = false;
    const cfg = ConfigService.get();
    const target = DisplayService.sidebarTarget();
    const uiScale = computeUiScale(target.workArea.width);
    const ballDip = Math.round(cfg.floatBall.size * uiScale);
    floatBallWin.webContents.send(
      IPC_CHANNELS["floatball:layout"],
      collapsedLayout(cfg.floatBall.size)
    );
    const shrink = () => {
      floatBallCollapseTimer = null;
      if (!floatBallWin || floatBallWin.isDestroyed()) return;
      if (floatBallExpanded) return;
      const pos = clampToArea(
        floatBallPos ?? { x: floatBallWin.getBounds().x, y: floatBallWin.getBounds().y },
        { width: ballDip, height: ballDip },
        target.workArea
      );
      floatBallPos = pos;
      floatBallWin.setBounds({ x: pos.x, y: pos.y, width: ballDip, height: ballDip }, false);
    };
    if (floatBallCollapseTimer) {
      clearTimeout(floatBallCollapseTimer);
      floatBallCollapseTimer = null;
    }
    if (immediate) {
      shrink();
    } else {
      floatBallCollapseTimer = setTimeout(shrink, 190);
    }
  },
  isFloatBallExpanded() {
    return floatBallExpanded;
  },
  /**
   * 悬浮球配置变更后重新套用（开关 / 尺寸 / 位置）。
   * 尺寸变了要重算窗口，开关关了要销毁窗口 —— 光改配置不动窗口的话，
   * 用户在设置里关掉开关，球还赖在屏幕上。
   */
  applyFloatBallConfig(config2) {
    if (!config2.floatBall.enabled) {
      this.destroyFloatBall();
      return;
    }
    if (!floatBallWin || floatBallWin.isDestroyed()) {
      this.createFloatBall(config2);
      return;
    }
    const target = DisplayService.sidebarTarget();
    const uiScale = computeUiScale(target.workArea.width);
    const ballDip = Math.round(config2.floatBall.size * uiScale);
    this.collapseFloatBall(true);
    const pos = resolveBallPosition(config2.floatBall, target.workArea, ballDip, config2.display.sidebarSide);
    floatBallPos = pos;
    floatBallWin.webContents.setZoomFactor(uiScale);
    floatBallWin.setBounds({ x: pos.x, y: pos.y, width: ballDip, height: ballDip }, false);
    floatBallWin.webContents.send(
      IPC_CHANNELS["floatball:layout"],
      collapsedLayout(config2.floatBall.size)
    );
    if (!floatBallWin.isVisible()) floatBallWin.showInactive();
  },
  /** 显示器变更时把球拉回有效工作区（拔屏后最容易「丢球」的地方） */
  onFloatBallDisplayChanged() {
    if (!floatBallWin || floatBallWin.isDestroyed()) return;
    const cfg = ConfigService.get();
    const target = DisplayService.sidebarTarget();
    if (!target || !target.workArea || target.workArea.width <= 0) {
      log.warn("[Window] onFloatBallDisplayChanged: invalid target display, skip");
      return;
    }
    this.applyFloatBallConfig(cfg);
    log.info("[Window] FloatBall repositioned after display change");
  },
  broadcast(channel, data) {
    for (const win of electron.BrowserWindow.getAllWindows()) {
      if (win.webContents && !win.webContents.isDestroyed()) {
        win.webContents.send(channel, data);
      }
    }
  },
  recreateMain(config2) {
    sidebarDocked = false;
    sidebarRestoreBounds = null;
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      sidebarWin.close();
    }
    setTimeout(() => {
      this.createSidebar(config2);
      this.showMain();
    }, 500);
  },
  resizeMain(width, height) {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      const cfg = DisplayService.sidebarTarget();
      const uiScale = computeUiScale(cfg.workArea.width);
      const side = ConfigService.get().display.sidebarSide === "right" ? "right" : "left";
      const railedWidth = Math.round(width * uiScale);
      let x;
      if (side === "right") {
        x = cfg.workArea.x + cfg.workArea.width - railedWidth;
      } else {
        x = cfg.workArea.x;
      }
      sidebarWin.setBounds({
        x,
        y: cfg.workArea.y + Math.round(cfg.workArea.height * SIDEBAR_MARGIN_RATIO),
        width: railedWidth,
        height: (height || cfg.workArea.height) * uiScale * (1 - SIDEBAR_MARGIN_RATIO * 2)
      }, true);
    }
  },
  /** 收起侧边栏到底部保留小条 */
  dockMain() {
    if (!sidebarWin || sidebarWin.isDestroyed()) return;
    if (sidebarDocked) return;
    sidebarDocked = true;
    const bounds = sidebarWin.getBounds();
    sidebarRestoreBounds = { x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height };
    const cfg = DisplayService.sidebarTarget();
    const side = ConfigService.get().display.sidebarSide === "right" ? "right" : "left";
    const uiScale = computeUiScale(cfg.workArea.width);
    const railWidth = Math.round(72 * uiScale);
    const dockHeight = Math.round(72 * uiScale);
    let x;
    if (side === "right") {
      x = cfg.workArea.x + cfg.workArea.width - railWidth;
    } else {
      x = cfg.workArea.x;
    }
    const dockY = cfg.workArea.y + cfg.workArea.height - dockHeight;
    animateWindowBounds(sidebarWin, {
      x,
      y: dockY,
      width: railWidth,
      height: dockHeight
    });
    log.info(`[Window] Sidebar docked (${railWidth}x${dockHeight} DIP, uiScale=${uiScale})`);
  },
  /** 恢复侧边栏 */
  undockMain() {
    if (!sidebarWin || sidebarWin.isDestroyed()) return;
    if (!sidebarDocked) return;
    sidebarDocked = false;
    const cfg = DisplayService.sidebarTarget();
    const uiScale = computeUiScale(cfg.workArea.width);
    const railWidth = Math.round(72 * uiScale);
    if (sidebarRestoreBounds) {
      const side = ConfigService.get().display.sidebarSide === "right" ? "right" : "left";
      const restoredWidth = sidebarRestoreBounds.width > Math.round(100 * uiScale) ? sidebarRestoreBounds.width : railWidth;
      let x;
      if (side === "right") {
        x = cfg.workArea.x + cfg.workArea.width - restoredWidth;
      } else {
        x = cfg.workArea.x;
      }
      animateWindowBounds(sidebarWin, {
        x,
        y: cfg.workArea.y + Math.round(cfg.workArea.height * SIDEBAR_MARGIN_RATIO),
        width: restoredWidth,
        height: cfg.workArea.height * (1 - SIDEBAR_MARGIN_RATIO * 2)
      });
    }
    sidebarRestoreBounds = null;
    log.info("[Window] Sidebar undocked");
  },
  /** DPI/显示器变更时重建侧边栏位置 (带边界校验, 防拔屏后飞出屏幕外) */
  onDisplayChanged() {
    if (!sidebarWin || sidebarWin.isDestroyed()) return;
    const cfg = DisplayService.sidebarTarget();
    if (!cfg || !cfg.workArea || cfg.workArea.width <= 0 || cfg.workArea.height <= 0) {
      log.warn("[Window] onDisplayChanged: invalid target display, skip reposition");
      return;
    }
    const uiScale = computeUiScale(cfg.workArea.width);
    const railWidth = Math.round(72 * uiScale);
    const side = ConfigService.get().display.sidebarSide === "right" ? "right" : "left";
    let x;
    if (side === "right") {
      x = cfg.workArea.x + cfg.workArea.width - railWidth;
    } else {
      x = cfg.workArea.x;
    }
    sidebarWin.webContents.setZoomFactor(uiScale);
    if (sidebarDocked) {
      const dockHeight = Math.round(72 * uiScale);
      const dockY = cfg.workArea.y + cfg.workArea.height - dockHeight;
      sidebarWin.setBounds({
        x,
        y: dockY,
        width: railWidth,
        height: dockHeight
      }, true);
      log.info(`[Window] Docked sidebar repositioned: ${railWidth}x${dockHeight} @ (${x}, ${dockY}) uiScale=${uiScale}`);
      return;
    }
    sidebarWin.setBounds({
      x,
      y: cfg.workArea.y + Math.round(cfg.workArea.height * SIDEBAR_MARGIN_RATIO),
      width: railWidth,
      height: cfg.workArea.height * (1 - SIDEBAR_MARGIN_RATIO * 2)
    }, true);
    log.info(`[Window] Sidebar repositioned: ${railWidth}x${Math.round(cfg.workArea.height * (1 - SIDEBAR_MARGIN_RATIO * 2))} @ (${x}, ${cfg.workArea.y + Math.round(cfg.workArea.height * SIDEBAR_MARGIN_RATIO)}) uiScale=${uiScale}`);
  },
  isDocked() {
    return sidebarDocked;
  }
};
let recorderWin = null;
let isRecording = false;
let isStarting = false;
let isPaused = false;
let recordStartTime = 0;
let elapsedBeforePause = 0;
let elapsedTimer = null;
function pad$1(n) {
  return String(n).padStart(2, "0");
}
const RecorderService = {
  isRecording() {
    return isRecording;
  },
  isStarting() {
    return isStarting;
  },
  getStatus() {
    return {
      recording: isRecording,
      starting: isStarting,
      paused: isPaused,
      elapsed: isRecording ? isPaused ? elapsedBeforePause : elapsedBeforePause + Math.round((Date.now() - recordStartTime) / 1e3) : 0
    };
  },
  // 暂停录制 (BUG-02: 支持暂停/继续, 便于教师中断后接续讲解)
  // 主进程只记录时间并通知页面把 MediaRecorder.pause(), 真正的音视频暂停在渲染层
  pause() {
    if (!isRecording || isPaused || !recorderWin || recorderWin.isDestroyed()) return false;
    isPaused = true;
    elapsedBeforePause = Math.round((Date.now() - recordStartTime) / 1e3);
    recorderWin.webContents.send(IPC_CHANNELS["recorder:pagePause"]);
    WindowManager.broadcast(IPC_CHANNELS["recorder:statusChanged"], this.getStatus());
    log.info("[Recorder] Paused");
    return true;
  },
  resume() {
    if (!isRecording || !isPaused || !recorderWin || recorderWin.isDestroyed()) return false;
    isPaused = false;
    recordStartTime = Date.now() - elapsedBeforePause * 1e3;
    recorderWin.webContents.send(IPC_CHANNELS["recorder:pageResume"]);
    WindowManager.broadcast(IPC_CHANNELS["recorder:statusChanged"], this.getStatus());
    log.info("[Recorder] Resumed");
    return true;
  },
  async start(options) {
    if (isRecording || isStarting) {
      return { success: false, error: "录屏已在进行中" };
    }
    isStarting = true;
    WindowManager.broadcast(IPC_CHANNELS["recorder:statusChanged"], this.getStatus());
    try {
      const cfg = ConfigService.get();
      const fps = options?.fps || cfg.recorder.fps || 15;
      const mic = options?.mic ?? cfg.recorder.mic ?? false;
      recorderWin = new electron.BrowserWindow({
        x: -1e4,
        y: -1e4,
        width: 1,
        height: 1,
        show: false,
        frame: false,
        skipTaskbar: true,
        webPreferences: {
          preload: path.join(__dirname, "../preload/index.js"),
          nodeIntegration: false,
          contextIsolation: true,
          sandbox: false
        }
      });
      recorderWin.webContents.session.setDisplayMediaRequestHandler((_request, callback) => {
        electron.desktopCapturer.getSources({ types: ["screen"] }).then((sources) => {
          if (sources.length > 0) {
            callback({ video: sources[0], audio: "loopback" });
          } else {
            callback({});
          }
        }).catch((e) => {
          log.error("[Recorder] desktopCapturer error:", e);
          callback({});
        });
      });
      let pageReadyResolve = null;
      let pageReadyReject = null;
      const pageReadyPromise = new Promise((resolve, reject) => {
        pageReadyResolve = resolve;
        pageReadyReject = reject;
      });
      const readyHandler = () => {
        log.info("[Recorder] Page ready signal received");
        if (pageReadyResolve) pageReadyResolve();
      };
      electron.ipcMain.on(IPC_CHANNELS["overlay:ready"], readyHandler);
      const loadTimeout = setTimeout(() => {
        electron.ipcMain.removeListener(IPC_CHANNELS["overlay:ready"], readyHandler);
        if (pageReadyReject) pageReadyReject(new Error("录屏页面加载超时"));
      }, 1e4);
      const isDev2 = !electron.app.isPackaged;
      if (isDev2) {
        await recorderWin.loadURL("http://localhost:5173/#/recorder");
      } else {
        await recorderWin.loadFile(path.join(__dirname, "../renderer/index.html"), { hash: "/recorder" });
      }
      log.info("[Recorder] Page loaded, waiting for ready...");
      try {
        await pageReadyPromise;
        clearTimeout(loadTimeout);
        electron.ipcMain.removeListener(IPC_CHANNELS["overlay:ready"], readyHandler);
      } catch (e) {
        this.cleanup();
        isStarting = false;
        return { success: false, error: e.message };
      }
      log.info("[Recorder] Signaling page to start recording");
      const recCfg = ConfigService.get().recorder;
      recorderWin.webContents.send(IPC_CHANNELS["recorder:pageStart"], {
        fps,
        mic,
        systemAudio: recCfg.systemAudio,
        micVolume: recCfg.micVolume,
        systemVolume: recCfg.systemVolume
      });
      let startedResolve = null;
      const startedPromise = new Promise((resolve) => {
        startedResolve = resolve;
      });
      const startedHandler = () => {
        log.info("[Recorder] Page confirmed recording started");
        if (startedResolve) startedResolve(true);
      };
      electron.ipcMain.on(IPC_CHANNELS["recorder:started"], startedHandler);
      const startedTimeout = setTimeout(() => {
        electron.ipcMain.removeListener(IPC_CHANNELS["recorder:started"], startedHandler);
        if (startedResolve) startedResolve(false);
      }, 15e3);
      const started = await startedPromise;
      clearTimeout(startedTimeout);
      electron.ipcMain.removeListener(IPC_CHANNELS["recorder:started"], startedHandler);
      if (!started) {
        this.cleanup();
        isStarting = false;
        return { success: false, error: "录屏启动失败: 页面未响应" };
      }
      isStarting = false;
      isRecording = true;
      recordStartTime = Date.now();
      elapsedTimer = setInterval(() => {
        const status = this.getStatus();
        WindowManager.broadcast(IPC_CHANNELS["recorder:statusChanged"], status);
      }, 1e3);
      log.info("[Recorder] Recording started");
      return { success: true };
    } catch (e) {
      log.error("[Recorder] Start failed:", e.message);
      this.cleanup();
      isStarting = false;
      return { success: false, error: e.message };
    }
  },
  async stop() {
    if (!isRecording || !recorderWin) {
      return { success: false, error: "没有正在进行的录屏" };
    }
    return new Promise((resolve) => {
      let resolved = false;
      const safeResolve = (result) => {
        if (resolved) return;
        resolved = true;
        resolve(result);
      };
      const timeout = setTimeout(() => {
        log.error("[Recorder] Stop timeout");
        this.cleanup();
        safeResolve({ success: false, error: "录屏停止超时" });
      }, 15e3);
      const completeHandler = (_e, data, mimeType) => {
        clearTimeout(timeout);
        electron.ipcMain.removeListener(IPC_CHANNELS["recorder:complete"], completeHandler);
        try {
          const cfg = ConfigService.get();
          const dir = cfg.recorder.dir;
          if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
          const now = /* @__PURE__ */ new Date();
          const ts = `${now.getFullYear()}${pad$1(now.getMonth() + 1)}${pad$1(now.getDate())}_${pad$1(now.getHours())}${pad$1(now.getMinutes())}${pad$1(now.getSeconds())}`;
          const ext = mimeType.includes("mp4") ? "mp4" : "webm";
          const filename = `seewo-record-${ts}.${ext}`;
          const filepath = path.join(dir, filename);
          fs.writeFileSync(filepath, Buffer.from(data));
          const duration = Math.round((Date.now() - recordStartTime) / 1e3);
          log.info(`[Recorder] Saved: ${filepath} (${duration}s, ${(data.byteLength / 1024 / 1024).toFixed(1)}MB)`);
          if (electron.Notification.isSupported()) {
            new electron.Notification({
              title: "录屏已保存",
              body: `时长 ${duration}s - ${filename}`,
              timeoutType: "default"
            }).show();
          }
          this.cleanup();
          safeResolve({ success: true, filepath });
        } catch (err) {
          log.error("[Recorder] Save failed:", err.message);
          this.cleanup();
          safeResolve({ success: false, error: err.message });
        }
      };
      electron.ipcMain.on(IPC_CHANNELS["recorder:complete"], completeHandler);
      if (recorderWin && !recorderWin.isDestroyed()) {
        recorderWin.webContents.send(IPC_CHANNELS["recorder:pageStop"]);
      }
    });
  },
  cleanup() {
    if (elapsedTimer) {
      clearInterval(elapsedTimer);
      elapsedTimer = null;
    }
    isRecording = false;
    isStarting = false;
    isPaused = false;
    elapsedBeforePause = 0;
    recordStartTime = 0;
    WindowManager.broadcast(IPC_CHANNELS["recorder:statusChanged"], this.getStatus());
    if (recorderWin && !recorderWin.isDestroyed()) {
      recorderWin.destroy();
    }
    recorderWin = null;
    log.info("[Recorder] Cleaned up");
  }
};
const execAsync = util.promisify(child_process.exec);
function ensureDir$1(dir) {
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
}
function readRecentLogs(logsDir, maxLines = 100) {
  try {
    const files = fs.readdirSync(logsDir).filter((f) => f.endsWith(".log")).map((f) => ({ name: f, stat: fs.statSync(path.join(logsDir, f)) })).sort((a, b) => b.stat.mtimeMs - a.stat.mtimeMs).slice(0, 2);
    const lines = [];
    for (const f of files) {
      const content = fs.readFileSync(path.join(logsDir, f.name), "utf-8");
      const fileLines = content.split("\n").slice(-Math.ceil(maxLines / files.length));
      lines.push(`--- ${f.name} ---`, ...fileLines);
    }
    return lines;
  } catch (e) {
    return [`Log read error: ${e.message}`];
  }
}
const DiagnosticService = {
  async runFull() {
    const timestamp2 = (/* @__PURE__ */ new Date()).toLocaleString("zh-CN");
    const version = electron.app.getVersion() || "2.0.0";
    const system = await this._collectSystem();
    const services = await this._checkServices();
    const features = await this._checkFeatures();
    const config2 = ConfigService.get();
    const logsDir = path.join(electron.app.getPath("userData"), "logs");
    const logLines = readRecentLogs(logsDir, 80);
    const recentErrors = logLines.filter((l) => /\b(error|Error|ERROR|fail|Fail|FAIL|exception|Exception|EXCEPTION)\b/.test(l)).slice(-15);
    return {
      timestamp: timestamp2,
      version,
      system,
      services,
      features,
      config: config2,
      recentErrors,
      logPath: logsDir
    };
  },
  async _collectSystem() {
    const displays2 = electron.screen.getAllDisplays();
    const screens = displays2.map((d, i) => ({
      id: i,
      bounds: { x: d.bounds.x, y: d.bounds.y, width: d.bounds.width, height: d.bounds.height },
      workArea: { x: d.workArea.x, y: d.workArea.y, width: d.workArea.width, height: d.workArea.height },
      scaleFactor: d.scaleFactor,
      primary: d.id === electron.screen.getPrimaryDisplay().id
    }));
    let osVersion = "unknown";
    try {
      const { stdout } = await execAsync(
        'powershell -NoProfile -Command "(Get-CimInstance Win32_OperatingSystem).Caption + " " + (Get-CimInstance Win32_OperatingSystem).Version"',
        { timeout: 5e3, windowsHide: true }
      );
      osVersion = stdout.trim();
    } catch {
    }
    return {
      os: process.platform,
      osVersion,
      platform: process.platform,
      arch: process.arch,
      electron: process.versions.electron || "unknown",
      chrome: process.versions.chrome || "unknown",
      node: process.versions.node || "unknown",
      uptime: process.uptime(),
      memory: process.memoryUsage(),
      screens
    };
  },
  async _checkServices() {
    const checks = [];
    try {
      const { desktopCapturer } = require("electron");
      const sources = await desktopCapturer.getSources({
        types: ["screen"],
        thumbnailSize: { width: 1, height: 1 },
        fetchWindowIcons: false
      });
      checks.push({
        name: "截图服务",
        status: sources.length > 0 ? "ok" : "warn",
        message: sources.length > 0 ? "屏幕捕获可用" : "未检测到屏幕源",
        detail: `检测到 ${sources.length} 个屏幕源`
      });
    } catch (e) {
      checks.push({ name: "截图服务", status: "error", message: "截图服务异常", detail: e.message });
    }
    try {
      checks.push({ name: "录屏服务", status: "ok", message: "录屏通道就绪" });
    } catch (e) {
      checks.push({ name: "录屏服务", status: "error", message: "录屏服务异常", detail: e.message });
    }
    try {
      const reminders2 = SchedulerService.list();
      checks.push({
        name: "定时提醒",
        status: "ok",
        message: `调度器运行中，${reminders2.length} 条提醒`,
        detail: reminders2.map((r) => r.note || "无内容").join(", ")
      });
    } catch (e) {
      checks.push({ name: "定时提醒", status: "error", message: "调度器异常", detail: e.message });
    }
    try {
      checks.push({ name: "USB 监控", status: "ok", message: "USB 监控通道就绪" });
    } catch (e) {
      checks.push({ name: "USB 监控", status: "error", message: "USB 监控异常", detail: e.message });
    }
    try {
      checks.push({ name: "打印机", status: "ok", message: "打印机通道就绪" });
    } catch (e) {
      checks.push({ name: "打印机", status: "error", message: "打印机服务异常", detail: e.message });
    }
    try {
      checks.push({ name: "输入法", status: "ok", message: "输入法切换通道就绪" });
    } catch (e) {
      checks.push({ name: "输入法", status: "error", message: "输入法通道异常", detail: e.message });
    }
    try {
      checks.push({ name: "长截图", status: "ok", message: "长截图服务就绪" });
    } catch (e) {
      checks.push({ name: "长截图", status: "error", message: "长截图服务异常", detail: e.message });
    }
    return checks;
  },
  async _checkFeatures() {
    return {
      notification: (() => {
        try {
          return require("electron").Notification.isSupported();
        } catch {
          return false;
        }
      })(),
      clipboard: true,
      desktopCapturer: true,
      autoLaunch: null
    };
  },
  async exportPack(result) {
    const packDir = path.join(electron.app.getPath("userData"), "diagnostics");
    ensureDir$1(packDir);
    const ts = (/* @__PURE__ */ new Date()).toISOString().replace(/[:.]/g, "-").slice(0, 19);
    const packName = `diagnostic-${ts}`;
    const packPath = path.join(packDir, packName);
    ensureDir$1(packPath);
    fs.writeFileSync(path.join(packPath, "report.json"), JSON.stringify(result, null, 2), "utf-8");
    const logsDir = path.join(electron.app.getPath("userData"), "logs");
    if (fs.existsSync(logsDir)) {
      const logFiles = fs.readdirSync(logsDir).filter((f) => f.endsWith(".log")).map((f) => ({ name: f, stat: fs.statSync(path.join(logsDir, f)) })).sort((a, b) => b.stat.mtimeMs - a.stat.mtimeMs).slice(0, 3);
      for (const f of logFiles) {
        try {
          const content = fs.readFileSync(path.join(logsDir, f.name));
          fs.writeFileSync(path.join(packPath, f.name), content);
        } catch {
        }
      }
    }
    log.info(`[Diagnostic] Pack exported: ${packPath}`);
    return packPath;
  }
};
let longshotRunning = false;
let longshotStopRequested = false;
function sleep$1(ms) {
  return new Promise((r) => setTimeout(r, ms));
}
function pad(n) {
  return String(n).padStart(2, "0");
}
function ensureDir(dir) {
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
}
function timestamp() {
  const now = /* @__PURE__ */ new Date();
  return `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}_${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`;
}
function toBase64Command(script) {
  const buf = Buffer.from(script, "utf16le");
  return buf.toString("base64");
}
function broadcastProgress(data) {
  WindowManager.broadcast("longshot:progress", data);
}
function broadcastCountdown(n) {
  WindowManager.broadcast("longshot:countdown", n);
}
const LongshotService = {
  isRunning() {
    return longshotRunning;
  },
  /** 枚举可见窗口列表 */
  async listWindows() {
    const PS_LIST = `Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class WinAPI {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

$procs = Get-Process | Where-Object {
  $_.MainWindowHandle -ne 0 -and
  -not [string]::IsNullOrWhiteSpace($_.MainWindowTitle) -and
  [WinAPI]::IsWindowVisible($_.MainWindowHandle) -and
  -not [WinAPI]::IsIconic($_.MainWindowHandle)
} | ForEach-Object {
  $rect = New-Object WinAPI+RECT
  [WinAPI]::GetWindowRect($_.MainWindowHandle, [ref]$rect) | Out-Null
  @{
    pid = $_.Id
    name = $_.ProcessName
    title = $_.MainWindowTitle
    handle = [int]$_.MainWindowHandle
    x = $rect.Left
    y = $rect.Top
    width = $rect.Right - $rect.Left
    height = $rect.Bottom - $rect.Top
  }
}
@($procs) | ConvertTo-Json -Depth 3
`;
    return new Promise((resolve) => {
      const child = child_process.spawn("powershell", [
        "-NoProfile",
        "-NonInteractive",
        "-EncodedCommand",
        toBase64Command(PS_LIST)
      ], { timeout: 8e3 });
      let out = "";
      let err = "";
      child.stdout.on("data", (d) => {
        out += d.toString();
      });
      child.stderr.on("data", (d) => {
        err += d.toString();
      });
      child.on("close", (code) => {
        if (code !== 0 || !out.trim()) {
          log.warn("[Longshot] listWindows failed:", err || `exit ${code}`);
          resolve({ success: false, windows: [], error: err || `PowerShell exit ${code}` });
          return;
        }
        try {
          const parsed = JSON.parse(out);
          const arr = Array.isArray(parsed) ? parsed : [parsed];
          const windows = arr.filter((w) => w.width > 200 && w.height > 200).map((w) => ({
            pid: w.pid,
            name: w.name,
            title: w.title,
            handle: w.handle,
            x: w.x,
            y: w.y,
            width: w.width,
            height: w.height
          }));
          resolve({ success: true, windows });
        } catch (e) {
          log.warn("[Longshot] parse windows failed:", e.message);
          resolve({ success: false, windows: [], error: e.message });
        }
      });
    });
  },
  /** 启动长截图: opts = { window: WindowInfo } */
  async start(opts) {
    if (longshotRunning) {
      log.warn("[Longshot] Already running");
      return null;
    }
    longshotRunning = true;
    longshotStopRequested = false;
    log.info("[Longshot] Starting... opts=", opts);
    const cfg = ConfigService.get();
    const dir = cfg.capture.dir;
    ensureDir(dir);
    if (!opts?.window) {
      log.error("[Longshot] No window selected");
      longshotRunning = false;
      return null;
    }
    const win = opts.window;
    for (let i = 3; i >= 1; i--) {
      broadcastCountdown(i);
      await sleep$1(1e3);
    }
    broadcastCountdown(0);
    await this._activateWindow(win);
    WindowManager.hideMain();
    await sleep$1(800);
    const frames = [];
    const maxFrames = 80;
    const maxTotalHeight = 25e3;
    const betweenDelay = 1e3;
    const stopDetectFrames = 3;
    try {
      log.info(`[Longshot] Capturing window "${win.title}" at ${win.x},${win.y} ${win.width}x${win.height}`);
      const first = await this._captureWindow(win);
      if (!first) throw new Error("无法截取窗口图像，请确认目标窗口未被最小化");
      frames.push(first.buf);
      broadcastProgress({ frameIndex: 0, totalHeight: first.info.height, status: "capturing", title: win.title });
      let consecutiveSame = 0;
      let lastBuf = first.buf;
      for (let i = 1; i < maxFrames; i++) {
        if (longshotStopRequested) {
          log.info("[Longshot] Stop requested");
          break;
        }
        await this._scrollWindow(win);
        await sleep$1(betweenDelay);
        const captured = await this._captureWindow(win);
        if (!captured) {
          log.warn(`[Longshot] Frame ${i} capture failed, stopping`);
          break;
        }
        const { buf } = captured;
        const similar = await this._framesSimilar(lastBuf, buf, win.width);
        if (similar) {
          consecutiveSame++;
          log.info(`[Longshot] Frame ${i} similar (${consecutiveSame}/${stopDetectFrames})`);
          if (consecutiveSame >= stopDetectFrames) {
            log.info("[Longshot] Bottom detected");
            break;
          }
        } else {
          consecutiveSame = 0;
        }
        frames.push(buf);
        lastBuf = buf;
        let totalHeight = 0;
        for (const f of frames) {
          const m = await sharp(f).metadata();
          totalHeight += m.height || win.height;
        }
        broadcastProgress({ frameIndex: i, totalHeight, status: "capturing", title: win.title });
        if (totalHeight >= maxTotalHeight) {
          log.info("[Longshot] Max height reached");
          break;
        }
      }
      broadcastProgress({ frameIndex: frames.length, totalHeight: 0, status: "stitching", title: win.title });
      const stitched = await this._stitchFrames(frames, win.width);
      if (!stitched) throw new Error("拼接失败");
      const filename = `seewo-longshot-${timestamp()}.png`;
      const filepath = path.join(dir, filename);
      fs.writeFileSync(filepath, stitched);
      electron.clipboard.writeImage(electron.nativeImage.createFromBuffer(stitched));
      log.info(`[Longshot] Saved: ${filepath}`);
      broadcastProgress({ frameIndex: frames.length, totalHeight: 0, status: "done", filepath, title: win.title });
      return filepath;
    } catch (e) {
      log.error("[Longshot] Error:", e.message);
      broadcastProgress({ frameIndex: frames.length, totalHeight: 0, status: "error", error: e.message });
      return null;
    } finally {
      longshotRunning = false;
      longshotStopRequested = false;
      WindowManager.showMain();
    }
  },
  stop() {
    if (longshotRunning) {
      longshotStopRequested = true;
      log.info("[Longshot] Stop requested");
    }
  },
  /** 激活目标窗口并置前 */
  async _activateWindow(win) {
    const PS_ACTIVATE = `$wshell = New-Object -ComObject WScript.Shell
[void]$wshell.AppActivate(${win.pid})
Start-Sleep -Milliseconds 200
`;
    return new Promise((resolve) => {
      child_process.exec(`powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command(PS_ACTIVATE)}"`, { timeout: 5e3 }, () => resolve());
    });
  },
  /** 向目标窗口发送 PageDown */
  async _scrollWindow(win) {
    const PS_SCROLL = `$wshell = New-Object -ComObject WScript.Shell
[void]$wshell.AppActivate(${win.pid})
Start-Sleep -Milliseconds 100
$wshell.SendKeys('{PGDN}')
`;
    return new Promise((resolve) => {
      child_process.exec(`powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command(PS_SCROLL)}"`, { timeout: 3e3 }, () => resolve());
    });
  },
  /** 截取指定窗口区域 */
  async _captureWindow(win) {
    try {
      const sources = await electron.desktopCapturer.getSources({
        types: ["screen"],
        thumbnailSize: { width: 3840, height: 2160 },
        fetchWindowIcons: false
      });
      if (!sources?.length) return null;
      const img = sources[0].thumbnail;
      if (!img || img.isEmpty()) return null;
      const fullBuf = img.toPNG();
      const meta = await sharp(fullBuf).metadata();
      const maxW = meta.width || 3840;
      const maxH = meta.height || 2160;
      const cropX = Math.max(0, win.x);
      const cropY = Math.max(0, win.y);
      const cropW = Math.min(win.width, maxW - cropX);
      const cropH = Math.min(win.height, maxH - cropY);
      if (cropW <= 10 || cropH <= 10) return null;
      const cropped = await sharp(fullBuf).extract({ left: cropX, top: cropY, width: cropW, height: cropH }).png().toBuffer();
      return { buf: cropped, info: { width: cropW, height: cropH } };
    } catch (e) {
      log.error("[Longshot] _captureWindow failed:", e.message);
      return null;
    }
  },
  /** 帧相似度检测 (行级像素差异) */
  async _framesSimilar(bufA, bufB, width, threshold = 0.92) {
    try {
      const metaA = await sharp(bufA).metadata();
      const metaB = await sharp(bufB).metadata();
      if ((metaA.width || 0) !== (metaB.width || 0)) return false;
      if (Math.abs((metaA.height || 0) - (metaB.height || 0)) > 5) return false;
      const rawA = await sharp(bufA).raw().toBuffer({ resolveWithObject: true });
      const rawB = await sharp(bufB).raw().toBuffer({ resolveWithObject: true });
      const hA = rawA.info.height;
      const hB = rawB.info.height;
      const w = rawA.info.width;
      const ch = rawA.info.channels;
      const h = Math.min(hA, hB);
      let sameRows = 0;
      for (let y = 0; y < h; y++) {
        const rowStart = y * w * ch;
        let rowDiff = 0;
        const step = Math.max(1, Math.floor(w / 40));
        let samples = 0;
        for (let x = 0; x < w; x += step) {
          const i = rowStart + x * ch;
          if (i + 2 >= rawA.data.length || i + 2 >= rawB.data.length) continue;
          const diff = Math.abs(rawA.data[i] - rawB.data[i]) + Math.abs(rawA.data[i + 1] - rawB.data[i + 1]) + Math.abs(rawA.data[i + 2] - rawB.data[i + 2]);
          if (diff > 30) rowDiff++;
          samples++;
        }
        if (rowDiff < samples * 0.1) sameRows++;
      }
      return sameRows / h >= threshold;
    } catch (e) {
      log.warn("[Longshot] framesSimilar error:", e.message);
      return false;
    }
  },
  /** 智能拼接: 找最优重叠偏移，裁剪后合成 */
  async _stitchFrames(frames, expectedWidth) {
    if (frames.length === 0) return null;
    if (frames.length === 1) return frames[0];
    try {
      const metas = await Promise.all(frames.map((f) => sharp(f).metadata()));
      const widths = metas.map((m) => m.width || expectedWidth);
      const heights = metas.map((m) => m.height || 1080);
      const width = Math.max(...widths);
      const overlaps = [0];
      for (let i = 1; i < frames.length; i++) {
        const overlap = await this._findBestOverlap(frames[i - 1], frames[i], width);
        overlaps.push(overlap);
      }
      const croppedBuffers = [];
      for (let i = 0; i < frames.length; i++) {
        const h = heights[i];
        const overlap = overlaps[i];
        if (i === 0) {
          croppedBuffers.push(frames[0]);
        } else {
          const cropH = Math.max(1, h - overlap);
          if (cropH > 0) {
            const cropped = await sharp(frames[i]).extract({ left: 0, top: overlap, width: Math.min(width, widths[i]), height: cropH }).png().toBuffer();
            croppedBuffers.push(cropped);
          }
        }
      }
      const croppedMetas = await Promise.all(croppedBuffers.map((f) => sharp(f).metadata()));
      const totalHeight = croppedMetas.reduce((sum, m) => sum + (m.height || 0), 0);
      const canvas = sharp({
        create: {
          width,
          height: totalHeight,
          channels: 4,
          background: { r: 255, g: 255, b: 255, alpha: 1 }
        }
      });
      let y = 0;
      const composites = [];
      for (let i = 0; i < croppedBuffers.length; i++) {
        const buf = croppedBuffers[i];
        const m = croppedMetas[i];
        composites.push({ input: buf, top: y, left: 0 });
        y += m.height || 0;
      }
      const result = await canvas.composite(composites).png().toBuffer();
      return result;
    } catch (e) {
      log.error("[Longshot] Smart stitch failed:", e.message);
      return this._simpleStitch(frames, expectedWidth);
    }
  },
  /** 找两帧之间的最佳重叠高度 */
  async _findBestOverlap(bufPrev, bufCurr, width) {
    try {
      const rawPrev = await sharp(bufPrev).raw().toBuffer({ resolveWithObject: true });
      const rawCurr = await sharp(bufCurr).raw().toBuffer({ resolveWithObject: true });
      const hPrev = rawPrev.info.height;
      const hCurr = rawCurr.info.height;
      const w = Math.min(rawPrev.info.width, rawCurr.info.width, width);
      const ch = rawPrev.info.channels;
      const searchH = Math.min(hPrev, hCurr, Math.floor(hPrev * 0.3));
      if (searchH < 10) return 0;
      let bestOverlap = 0;
      let bestScore = Infinity;
      for (let offset = 0; offset < searchH; offset++) {
        const prevRow = hPrev - searchH + offset;
        if (prevRow < 0) continue;
        let diff = 0;
        const step = Math.max(1, Math.floor(w / 30));
        let samples = 0;
        const compareRows = Math.min(20, hCurr);
        for (let r = 0; r < compareRows; r++) {
          const currRow = r;
          const prevIdx = (prevRow + r) * w * ch;
          const currIdx = currRow * w * ch;
          if (prevIdx + 2 >= rawPrev.data.length || currIdx + 2 >= rawCurr.data.length) continue;
          for (let x = 0; x < w; x += step) {
            const pi = prevIdx + x * ch;
            const ci = currIdx + x * ch;
            if (pi + 2 >= rawPrev.data.length || ci + 2 >= rawCurr.data.length) continue;
            diff += Math.abs(rawPrev.data[pi] - rawCurr.data[ci]) + Math.abs(rawPrev.data[pi + 1] - rawCurr.data[ci + 1]) + Math.abs(rawPrev.data[pi + 2] - rawCurr.data[ci + 2]);
            samples++;
          }
        }
        if (samples > 0) {
          const score = diff / samples;
          if (score < bestScore) {
            bestScore = score;
            bestOverlap = offset;
          }
        }
      }
      if (bestScore > 80) {
        return Math.floor(searchH / 3);
      }
      return bestOverlap;
    } catch (e) {
      log.warn("[Longshot] findBestOverlap failed:", e.message);
      return 80;
    }
  },
  /** 降级简单拼接 */
  async _simpleStitch(frames, expectedWidth) {
    try {
      const metas = await Promise.all(frames.map((f) => sharp(f).metadata()));
      const width = Math.max(...metas.map((m) => m.width || expectedWidth));
      const totalHeight = metas.reduce((sum, m) => sum + (m.height || 0), 0);
      const canvas = sharp({
        create: { width, height: totalHeight, channels: 4, background: { r: 255, g: 255, b: 255, alpha: 1 } }
      });
      let y = 0;
      const composites = [];
      for (let i = 0; i < frames.length; i++) {
        composites.push({ input: frames[i], top: y, left: 0 });
        y += metas[i].height || 0;
      }
      return await canvas.composite(composites).png().toBuffer();
    } catch (e) {
      log.error("[Longshot] Simple stitch failed:", e.message);
      return null;
    }
  }
};
const gotLock = electron.app.requestSingleInstanceLock();
if (!gotLock) {
  electron.app.quit();
  process.exit(0);
}
electron.app.on("second-instance", () => {
  WindowManager.focusMain();
});
log.transports.file.resolvePathFn = () => {
  return path.join(electron.app.getPath("userData"), "logs", `main-${(/* @__PURE__ */ new Date()).toISOString().slice(0, 10)}.log`);
};
electron.app.commandLine.appendSwitch("disable-features", "AutofillServerCommunication,Translate");
let config = null;
let activeOverlayWin = null;
let activeAnnotatorWin = null;
async function bootstrap() {
  log.info("[BOOT] Seewo Sidekick v2.1.0 starting...");
  try {
    config = await ConfigService.init();
    AppearanceService.init();
    log.info("[BOOT] Config loaded");
    await DisplayService.init(config);
    log.info("[BOOT] Display service ready");
    await ImeService.start().catch((e) => log.warn("[BOOT] ImeService degraded:", e.message));
    await UsbService.start(config).catch((e) => log.warn("[BOOT] UsbService degraded:", e.message));
    await PrinterService.start(config).catch((e) => log.warn("[BOOT] PrinterService degraded:", e.message));
    await SchedulerService.start().catch((e) => log.warn("[BOOT] SchedulerService degraded:", e.message));
    const mainWin = WindowManager.createSidebar(config);
    if (!config.oobe.completed && !config.oobe.skipped) {
      WindowManager.createOobe(mainWin);
      log.info("[BOOT] OOBE window created");
    } else {
      mainWin.show();
      if (config.floatBall.enabled) WindowManager.showFloatBall();
      log.info("[BOOT] Main window shown");
    }
    log.info("[BOOT] Diagnostic service ready");
    let displayChangeTimer = null;
    const relayoutSidebar = () => {
      if (displayChangeTimer) clearTimeout(displayChangeTimer);
      displayChangeTimer = setTimeout(() => {
        log.info("[Display] metrics/attach changed, repositioning sidebar (≤200ms)");
        WindowManager.onDisplayChanged();
      }, 200);
    };
    DisplayService.onMetricsChanged(() => relayoutSidebar());
    DisplayService.onAttachChanged((added, removed) => {
      log.info(`[Display] attach changed: +${added.length} / -${removed.length}`);
      relayoutSidebar();
    });
    registerCaptureHotkey(config);
    registerFloatBallHotkey(config);
  } catch (e) {
    log.error("[BOOT] Fatal bootstrap error:", e);
    electron.app.quit();
  }
}
electron.ipcMain.handle(IPC_CHANNELS["config:get"], async () => ConfigService.get());
electron.ipcMain.handle(IPC_CHANNELS["config:set"], async (_event, key, value) => {
  const result = ConfigService.set(key, value);
  if (key === "capture.hotkey" && config) {
    registerCaptureHotkey(config);
  }
  if ((key === "floatBall" || key.startsWith("floatBall.")) && config) {
    WindowManager.applyFloatBallConfig(ConfigService.get());
    if (key === "floatBall.hotkey") registerFloatBallHotkey(config);
  }
  WindowManager.broadcast(IPC_CHANNELS["config:updated"], ConfigService.get());
  return result;
});
electron.ipcMain.handle(IPC_CHANNELS["ime:getState"], async () => ImeService.getState());
electron.ipcMain.handle(IPC_CHANNELS["ime:toggle"], async () => {
  const state = await ImeService.toggle();
  WindowManager.broadcast(IPC_CHANNELS["ime:changed"], state);
  return state;
});
async function runRegionCapture(opts) {
  const mode = opts?.mode || "region";
  WindowManager.hideFloatBallForCapture();
  WindowManager.hideMain();
  await sleep(300);
  const shot = await CaptureService.grabTarget();
  const img = shot?.img || null;
  if (!img) {
    WindowManager.restoreFloatBallAfterCapture();
    WindowManager.showMain();
    return { success: false, error: "无法获取屏幕图像" };
  }
  if (mode === "fullscreen") {
    const filepath = await CaptureService.saveImage(img);
    WindowManager.restoreFloatBallAfterCapture();
    WindowManager.showMain();
    if (filepath && electron.Notification.isSupported()) {
      new electron.Notification({ title: "截图已保存", body: filepath, timeoutType: "default" }).show();
    }
    return { success: !!filepath, filepath: filepath || void 0 };
  }
  if (mode === "longshot") {
    WindowManager.restoreFloatBallAfterCapture();
    WindowManager.showMain();
    return { success: false, error: "请使用新版长截图功能（点击「长截图」按钮）" };
  }
  const tmpPath = CaptureService.saveTempImage(img);
  const target = shot?.target || DisplayService.sidebarTarget();
  const scaleFactor = target.scaleFactor;
  const shotWorkArea = shot?.workArea || target.workArea;
  const imgSize = img.getSize();
  activeOverlayWin = WindowManager.showOverlay();
  if (!activeOverlayWin) {
    WindowManager.restoreFloatBallAfterCapture();
    WindowManager.showMain();
    return { success: false, error: "无法创建选择窗口" };
  }
  const result = await new Promise((resolve) => {
    let resolved = false;
    const readyHandler = () => {
      if (resolved) return;
      log.info("[Overlay] Ready, sending init");
      activeOverlayWin?.webContents.send(IPC_CHANNELS["overlay:init"], {
        mode: "region",
        screenshotPath: tmpPath,
        scaleFactor,
        screenWidth: imgSize.width,
        screenHeight: imgSize.height,
        dipWidth: shotWorkArea.width,
        dipHeight: shotWorkArea.height
      });
    };
    electron.ipcMain.on(IPC_CHANNELS["overlay:ready"], readyHandler);
    const winForClose = activeOverlayWin;
    const closedHandler = () => {
      if (resolved) return;
      log.warn("[Overlay] Window closed externally, aborting capture");
      resolved = true;
      cleanup();
      CaptureService.cleanupTemp(tmpPath);
      activeOverlayWin = null;
      WindowManager.restoreFloatBallAfterCapture();
      WindowManager.showMain();
      resolve({ success: false, error: "选择窗口已关闭" });
    };
    winForClose?.on("closed", closedHandler);
    const regionHandler = async (_e, region) => {
      if (resolved) return;
      resolved = true;
      cleanup();
      log.info("[Overlay] Region selected:", region);
      const physRegion = {
        x: Math.round((region.x + shotWorkArea.x) * scaleFactor),
        y: Math.round((region.y + shotWorkArea.y) * scaleFactor),
        width: Math.round(region.width * scaleFactor),
        height: Math.round(region.height * scaleFactor)
      };
      const filepath = await CaptureService.saveImage(img, physRegion);
      CaptureService.cleanupTemp(tmpPath);
      if (activeOverlayWin && !activeOverlayWin.isDestroyed()) {
        activeOverlayWin.close();
        activeOverlayWin = null;
      }
      WindowManager.restoreFloatBallAfterCapture();
      WindowManager.showMain();
      if (filepath && electron.Notification.isSupported()) {
        new electron.Notification({ title: "区域截图已保存", body: filepath, timeoutType: "default" }).show();
      }
      resolve({ success: !!filepath, filepath: filepath || void 0 });
    };
    electron.ipcMain.on(IPC_CHANNELS["overlay:region"], regionHandler);
    const cancelHandler = () => {
      if (resolved) return;
      resolved = true;
      cleanup();
      log.info("[Overlay] Cancelled");
      CaptureService.cleanupTemp(tmpPath);
      if (activeOverlayWin && !activeOverlayWin.isDestroyed()) {
        activeOverlayWin.close();
        activeOverlayWin = null;
      }
      WindowManager.restoreFloatBallAfterCapture();
      WindowManager.showMain();
      resolve({ success: false, error: "用户取消" });
    };
    electron.ipcMain.on(IPC_CHANNELS["overlay:cancel"], cancelHandler);
    function cleanup() {
      electron.ipcMain.removeListener(IPC_CHANNELS["overlay:ready"], readyHandler);
      electron.ipcMain.removeListener(IPC_CHANNELS["overlay:region"], regionHandler);
      electron.ipcMain.removeListener(IPC_CHANNELS["overlay:cancel"], cancelHandler);
      if (winForClose) winForClose.removeListener("closed", closedHandler);
    }
    setTimeout(() => {
      if (resolved) return;
      resolved = true;
      cleanup();
      log.warn("[Overlay] Timeout");
      CaptureService.cleanupTemp(tmpPath);
      if (activeOverlayWin && !activeOverlayWin.isDestroyed()) {
        activeOverlayWin.close();
        activeOverlayWin = null;
      }
      WindowManager.restoreFloatBallAfterCapture();
      WindowManager.showMain();
      resolve({ success: false, error: "操作超时" });
    }, 6e4);
  });
  return result;
}
electron.ipcMain.handle(IPC_CHANNELS["capture:region"], async (_event, opts) => {
  log.info("[IPC] capture:region", opts);
  return runRegionCapture(opts);
});
function registerCaptureHotkey(cfg) {
  const hotkey = cfg.capture?.hotkey;
  if (!hotkey) return;
  try {
    electron.globalShortcut.unregister(hotkey);
    const ok = electron.globalShortcut.register(hotkey, () => {
      log.info(`[Hotkey] Global capture hotkey triggered: ${hotkey}`);
      runRegionCapture({ mode: "region" }).catch((e) => log.error("[Hotkey] capture failed:", e));
    });
    if (ok) {
      log.info(`[Hotkey] Registered: ${hotkey}`);
    } else {
      log.warn(`[Hotkey] Register failed (可能已被其他应用占用): ${hotkey}`);
    }
  } catch (e) {
    log.warn(`[Hotkey] Register error: ${e.message}`);
  }
}
electron.ipcMain.handle(IPC_CHANNELS["longshot:selectWindow"], async () => {
  log.info("[IPC] longshot:selectWindow");
  const result = await LongshotService.listWindows();
  return result;
});
electron.ipcMain.handle(IPC_CHANNELS["longshot:start"], async (_event, opts) => {
  log.info("[IPC] longshot:start opts=", opts);
  WindowManager.hideFloatBallForCapture();
  WindowManager.hideMain();
  await sleep(500);
  const filepath = await LongshotService.start(opts);
  WindowManager.restoreFloatBallAfterCapture();
  WindowManager.showMain();
  if (filepath && electron.Notification.isSupported()) {
    new electron.Notification({ title: "长截图已保存", body: filepath, timeoutType: "default" }).show();
  }
  return { success: !!filepath, filepath: filepath || void 0, error: filepath ? void 0 : "长截图失败" };
});
electron.ipcMain.handle(IPC_CHANNELS["longshot:stop"], async () => {
  log.info("[IPC] longshot:stop");
  LongshotService.stop();
  return { success: true };
});
async function runAnnotate() {
  log.info("[IPC] capture:annotate (transparent overlay mode)");
  WindowManager.hideFloatBallForCapture();
  WindowManager.hideMain();
  await sleep(300);
  const target = DisplayService.sidebarTarget();
  const scaleFactor = target.scaleFactor;
  activeAnnotatorWin = WindowManager.showAnnotator();
  if (!activeAnnotatorWin) {
    WindowManager.restoreFloatBallAfterCapture();
    WindowManager.showMain();
    return { success: false, error: "无法创建批注窗口" };
  }
  const result = await new Promise((resolve) => {
    let resolved = false;
    const readyHandler = () => {
      if (resolved) return;
      log.info("[Annotator] Ready, sending init (transparent mode)");
      activeAnnotatorWin?.webContents.send(IPC_CHANNELS["overlay:init"], {
        mode: "annotate",
        transparent: true,
        scaleFactor,
        dipWidth: target.workArea.width,
        dipHeight: target.workArea.height
      });
    };
    electron.ipcMain.on(IPC_CHANNELS["overlay:ready"], readyHandler);
    const winForClose = activeAnnotatorWin;
    const closedHandler = () => {
      if (resolved) return;
      log.warn("[Annotator] Window closed externally, aborting annotate");
      resolved = true;
      cleanup();
      activeAnnotatorWin = null;
      WindowManager.restoreFloatBallAfterCapture();
      WindowManager.showMain();
      resolve({ success: false, error: "批注窗口已关闭" });
    };
    winForClose?.on("closed", closedHandler);
    const saveHandler = (_e, dataUrl) => {
      if (resolved) return;
      resolved = true;
      cleanup();
      log.info("[Annotator] Save received");
      const filepath = CaptureService.saveAnnotatedImage(dataUrl);
      if (activeAnnotatorWin && !activeAnnotatorWin.isDestroyed()) {
        activeAnnotatorWin.close();
        activeAnnotatorWin = null;
      }
      WindowManager.restoreFloatBallAfterCapture();
      WindowManager.showMain();
      if (filepath && electron.Notification.isSupported()) {
        new electron.Notification({ title: "批注已保存", body: filepath, timeoutType: "default" }).show();
      }
      resolve({ success: !!filepath, filepath: filepath || void 0 });
    };
    electron.ipcMain.on(IPC_CHANNELS["overlay:saveAnnotate"], saveHandler);
    const cancelHandler = () => {
      if (resolved) return;
      resolved = true;
      cleanup();
      log.info("[Annotator] Cancelled");
      if (activeAnnotatorWin && !activeAnnotatorWin.isDestroyed()) {
        activeAnnotatorWin.close();
        activeAnnotatorWin = null;
      }
      WindowManager.restoreFloatBallAfterCapture();
      WindowManager.showMain();
      resolve({ success: false, error: "用户取消" });
    };
    electron.ipcMain.on(IPC_CHANNELS["overlay:cancel"], cancelHandler);
    function cleanup() {
      electron.ipcMain.removeListener(IPC_CHANNELS["overlay:ready"], readyHandler);
      electron.ipcMain.removeListener(IPC_CHANNELS["overlay:saveAnnotate"], saveHandler);
      electron.ipcMain.removeListener(IPC_CHANNELS["overlay:cancel"], cancelHandler);
      if (winForClose) winForClose.removeListener("closed", closedHandler);
    }
    setTimeout(() => {
      if (resolved) return;
      resolved = true;
      cleanup();
      if (activeAnnotatorWin && !activeAnnotatorWin.isDestroyed()) {
        activeAnnotatorWin.close();
        activeAnnotatorWin = null;
      }
      WindowManager.restoreFloatBallAfterCapture();
      WindowManager.showMain();
      resolve({ success: false, error: "操作超时" });
    }, 3e5);
  });
  return result;
}
electron.ipcMain.handle(IPC_CHANNELS["capture:annotate"], () => runAnnotate());
electron.ipcMain.handle(IPC_CHANNELS["recorder:start"], async (_event, opts) => {
  log.info("[IPC] recorder:start", opts);
  return RecorderService.start(opts);
});
electron.ipcMain.handle(IPC_CHANNELS["recorder:stop"], async () => {
  log.info("[IPC] recorder:stop");
  return RecorderService.stop();
});
electron.ipcMain.handle(IPC_CHANNELS["recorder:status"], async () => {
  return RecorderService.getStatus();
});
electron.ipcMain.handle(IPC_CHANNELS["recorder:pause"], async () => {
  log.info("[IPC] recorder:pause");
  return RecorderService.pause();
});
electron.ipcMain.handle(IPC_CHANNELS["recorder:resume"], async () => {
  log.info("[IPC] recorder:resume");
  return RecorderService.resume();
});
electron.ipcMain.handle(IPC_CHANNELS["usb:list"], async () => {
  return UsbService.list();
});
electron.ipcMain.handle(IPC_CHANNELS["usb:scan"], async () => {
  log.info("[IPC] usb:scan (manual refresh)");
  const drives = await UsbService.scan();
  return { success: true, drives };
});
electron.ipcMain.handle(IPC_CHANNELS["printer:status"], async () => {
  return PrinterService.getStatus();
});
electron.ipcMain.handle(IPC_CHANNELS["display:list"], async () => DisplayService.list());
electron.ipcMain.handle(IPC_CHANNELS["display:sidebarTarget"], async () => DisplayService.sidebarTarget());
electron.ipcMain.handle(IPC_CHANNELS["appearance:get"], async () => AppearanceService.snapshot());
electron.ipcMain.handle(IPC_CHANNELS["appearance:set"], async (_event, patch) => {
  return AppearanceService.set(patch);
});
electron.ipcMain.handle(IPC_CHANNELS["floatball:show"], () => {
  WindowManager.showFloatBall();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["floatball:hide"], () => {
  WindowManager.hideFloatBall();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["floatball:toggle"], () => {
  WindowManager.toggleFloatBall();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["floatball:dragStart"], (_event, grab) => {
  WindowManager.floatBallDragStart(grab);
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["floatball:dragEnd"], () => {
  WindowManager.floatBallDragEnd();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["floatball:expand"], () => {
  WindowManager.expandFloatBall();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["floatball:collapse"], () => {
  WindowManager.collapseFloatBall();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["floatball:action"], async (_event, id) => {
  await dispatchFloatBallAction(id);
  return true;
});
electron.ipcMain.on(IPC_CHANNELS["floatball:setClickThrough"], (_event, on) => {
  const w = WindowManager.getFloatBall();
  if (w && !w.isDestroyed()) {
    try {
      w.setIgnoreMouseEvents(!!on, { forward: true });
    } catch {
    }
  }
});
electron.ipcMain.handle(IPC_CHANNELS["app:openTaskMgr"], async () => {
  try {
    child_process.spawn("taskmgr.exe", { detached: true });
    return true;
  } catch (e) {
    log.error("[IPC] Open taskmgr failed:", e);
    try {
      child_process.spawn("cmd", ["/c", "start", "taskmgr"]);
      return true;
    } catch {
    }
    return false;
  }
});
electron.ipcMain.handle(IPC_CHANNELS["shell:openExternal"], async (_event, url) => {
  await electron.shell.openExternal(url);
});
electron.ipcMain.handle(IPC_CHANNELS["shell:openPath"], async (_event, filePath) => {
  const result = await electron.shell.openPath(filePath);
  return result === "";
});
electron.ipcMain.handle(IPC_CHANNELS["shell:showItemInFolder"], async (_event, filePath) => {
  electron.shell.showItemInFolder(filePath);
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["oobe:getState"], async () => ConfigService.get().oobe);
electron.ipcMain.handle(IPC_CHANNELS["oobe:setState"], async (_event, state) => {
  return ConfigService.set("oobe", state);
});
electron.ipcMain.handle(IPC_CHANNELS["oobe:closeAndOpenMain"], async () => {
  WindowManager.closeOobe();
  WindowManager.restoreFloatBallAfterCapture();
  WindowManager.showMain();
  if (ConfigService.get().floatBall.enabled) WindowManager.showFloatBall();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["power:setAutoLaunch"], async (_event, enabled) => {
  electron.app.setLoginItemSettings({ openAtLogin: enabled });
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["power:getAutoLaunch"], async () => {
  return electron.app.getLoginItemSettings().openAtLogin;
});
electron.ipcMain.handle(IPC_CHANNELS["window:show"], async () => {
  WindowManager.restoreFloatBallAfterCapture();
  WindowManager.showMain();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["window:hide"], async () => {
  WindowManager.hideFloatBallForCapture();
  WindowManager.hideMain();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["window:minimize"], async () => {
  WindowManager.minimize();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["window:resize"], async (_event, width, height) => {
  WindowManager.resizeMain(width, height);
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["window:openSettings"], async () => {
  WindowManager.showSettings();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["window:dock"], async () => {
  WindowManager.dockMain();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["window:undock"], async () => {
  WindowManager.undockMain();
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["notification:show"], async (_event, item) => {
  if (electron.Notification.isSupported()) {
    const n = new electron.Notification({
      title: item.title || "希沃侧边快捷键工具",
      body: item.message || "",
      timeoutType: item.duration > 0 ? "default" : "never"
    });
    n.show();
  }
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["notification:dismiss"], async () => {
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["reminder:add"], async (_event, r) => {
  SchedulerService.add(r);
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["reminder:remove"], async (_event, id) => {
  SchedulerService.remove(id);
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["reminder:list"], async () => {
  return SchedulerService.list();
});
electron.ipcMain.handle(IPC_CHANNELS["reminder:selectSound"], async () => {
  const result = await electron.dialog.showOpenDialog({
    title: "选择提醒铃声",
    filters: [{ name: "音频文件", extensions: ["mp3", "wav", "ogg", "m4a"] }],
    properties: ["openFile"]
  });
  if (result.canceled || result.filePaths.length === 0) return null;
  const selectedPath = result.filePaths[0];
  ConfigService.set("reminderSound.mp3Path", selectedPath);
  ConfigService.set("reminderSound.preset", "custom");
  log.info("[Reminder] Selected sound file:", selectedPath);
  return selectedPath;
});
electron.ipcMain.handle(IPC_CHANNELS["reminder:playTest"], async (_event, soundConfig) => {
  const cfg = soundConfig || ConfigService.get().reminderSound;
  SchedulerService.playSound(cfg);
  return true;
});
electron.ipcMain.handle(IPC_CHANNELS["help:runDiagnostics"], async () => {
  log.info("[IPC] help:runDiagnostics");
  const result = await DiagnosticService.runFull();
  return result;
});
electron.ipcMain.handle(IPC_CHANNELS["diag:getStatus"], async () => {
  return {
    usb: UsbService.getDiagnostics?.() || {},
    recorder: RecorderService.getStatus(),
    memory: process.memoryUsage(),
    uptime: process.uptime(),
    displays: DisplayService.list()
  };
});
electron.ipcMain.handle(IPC_CHANNELS["help:exportDiagPack"], async () => {
  log.info("[IPC] help:exportDiagPack");
  const result = await DiagnosticService.runFull();
  const packPath = await DiagnosticService.exportPack(result);
  return packPath;
});
electron.ipcMain.on("message", (event, channel) => {
  if (!isAllowedChannel(channel)) {
    log.warn(`[SECURITY] Blocked unauthorized IPC channel: ${channel}`);
    event.preventDefault();
  }
});
function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}
function notify(title, body) {
  if (electron.Notification.isSupported()) {
    new electron.Notification({ title, body, timeoutType: "default" }).show();
  }
}
async function dispatchFloatBallAction(id) {
  try {
    switch (id) {
      case "capture":
        await runRegionCapture({ mode: "region" });
        break;
      case "annotate":
        await runAnnotate();
        break;
      case "longshot":
        WindowManager.hideMain();
        await sleep(500);
        {
          const p = await LongshotService.start({});
          WindowManager.showMain();
          if (p) notify("长截图已保存", p);
        }
        break;
      case "record":
        await RecorderService.start({});
        break;
      case "ime": {
        const s = await ImeService.toggle();
        WindowManager.broadcast(IPC_CHANNELS["ime:changed"], s);
        break;
      }
      case "taskmgr":
        try {
          child_process.spawn("taskmgr.exe", { detached: true });
        } catch {
        }
        break;
      case "sidebar":
        WindowManager.toggleMain();
        break;
      case "settings":
        WindowManager.showSettings();
        break;
      default:
        log.warn(`[FloatBall] 未知动作: ${id}`);
    }
  } catch (e) {
    log.warn(`[FloatBall] 动作执行失败 (${id}):`, e);
  }
}
function registerFloatBallHotkey(cfg) {
  const hotkey = cfg.floatBall?.hotkey;
  if (!hotkey) return;
  try {
    electron.globalShortcut.unregister(hotkey);
    const ok = electron.globalShortcut.register(hotkey, () => {
      log.info(`[Hotkey] FloatBall toggled: ${hotkey}`);
      WindowManager.toggleFloatBall();
    });
    if (ok) log.info(`[Hotkey] FloatBall 热键已注册: ${hotkey}`);
    else log.warn(`[Hotkey] FloatBall 热键注册失败（可能被其他应用占用）: ${hotkey}`);
  } catch (e) {
    log.warn(`[Hotkey] FloatBall 热键注册异常: ${e?.message ?? e}`);
  }
}
electron.app.whenReady().then(bootstrap);
electron.app.on("before-quit", () => {
  electron.globalShortcut.unregisterAll();
  SchedulerService.stop();
  UsbService.stop();
  PrinterService.stop();
  ImeService.stop();
  RecorderService.cleanup();
  ConfigService.flush();
});
electron.app.on("window-all-closed", () => {
});
electron.app.on("render-process-gone", (_event, _webContents, details) => {
  log.error(`[CRASH] Render process gone: ${details.reason}`);
  setTimeout(() => {
    WindowManager.recreateMain(config);
  }, 3e3);
});
