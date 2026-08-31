// src/native/appbar.cc - Windows AppBar API (SHAppBarMessage) N-API binding
//
// Exposed to Node.js:
//   register(hwnd, callbackMsg): bool          - ABM_NEW
//   remove(hwnd): void                         - ABM_REMOVE
//   queryAndSetPos(hwnd, edge, x, y, w, h): { x, y, w, h }
//                                              - ABM_QUERYPOS + ABM_SETPOS (NO MoveWindow)
//   getTaskbarPos(): { edge, left, top, right, bottom }
//
// edge: 0=ABE_LEFT, 1=ABE_TOP, 2=ABE_RIGHT, 3=ABE_BOTTOM
// Coordinates are in PHYSICAL PIXELS (the system's native unit).
// The Electron BrowserWindow should be positioned separately in DIP via setBounds.

#include <napi.h>
#ifdef _WIN32
  #ifndef WIN32_LEAN_AND_MEAN
  #define WIN32_LEAN_AND_MEAN
  #endif
  #include <windows.h>
  #include <shellapi.h>
#endif

// ---- register(hwnd, callbackMsg) -> bool ------------------------------------------
Napi::Value Register(const Napi::CallbackInfo& info) {
#ifdef _WIN32
  if (info.Length() < 2 || !info[0].IsNumber() || !info[1].IsNumber()) {
    Napi::TypeError::New(info.Env(), "register(hwnd, callbackMsg) needs 2 number args").ThrowAsJavaScriptException();
    return info.Env().Undefined();
  }
  APPBARDATA abd = {};
  abd.cbSize  = sizeof(APPBARDATA);
  abd.hWnd    = reinterpret_cast<HWND>(static_cast<int64_t>(info[0].As<Napi::Number>().DoubleValue()));
  abd.uCallbackMessage = info[1].As<Napi::Number>().Uint32Value();
  UINT_PTR result = SHAppBarMessage(ABM_NEW, &abd);
  return Napi::Boolean::New(info.Env(), result != 0);
#else
  return Napi::Boolean::New(info.Env(), false);
#endif
}

// ---- remove(hwnd) ----------------------------------------------------------------
Napi::Value Remove(const Napi::CallbackInfo& info) {
#ifdef _WIN32
  if (info.Length() >= 1 && info[0].IsNumber()) {
    APPBARDATA abd = {};
    abd.cbSize = sizeof(APPBARDATA);
    abd.hWnd   = reinterpret_cast<HWND>(static_cast<int64_t>(info[0].As<Napi::Number>().DoubleValue()));
    SHAppBarMessage(ABM_REMOVE, &abd);
  }
#endif
  return info.Env().Undefined();
}

// ---- queryAndSetPos(hwnd, edge, x, y, w, h) -> { x, y, w, h } -------------------
// Calls ABM_QUERYPOS + ABM_SETPOS. Does NOT call MoveWindow.
// The system adjusts WorkArea; the caller positions the Electron window separately.
Napi::Value QueryAndSetPos(const Napi::CallbackInfo& info) {
#ifdef _WIN32
  if (info.Length() < 6) {
    Napi::TypeError::New(info.Env(), "queryAndSetPos(hwnd, edge, x, y, w, h) needs 6 args").ThrowAsJavaScriptException();
    return info.Env().Undefined();
  }
  APPBARDATA abd = {};
  abd.cbSize = sizeof(APPBARDATA);
  abd.hWnd   = reinterpret_cast<HWND>(static_cast<int64_t>(info[0].As<Napi::Number>().DoubleValue()));
  abd.uEdge  = info[1].As<Napi::Number>().Uint32Value();

  int w = info[4].As<Napi::Number>().Int32Value();
  int h = info[5].As<Napi::Number>().Int32Value();

  abd.rc.left   = info[2].As<Napi::Number>().Int32Value();
  abd.rc.top    = info[3].As<Napi::Number>().Int32Value();
  abd.rc.right  = abd.rc.left + w;
  abd.rc.bottom = abd.rc.top  + h;

  // Query: system adjusts rect (accounts for taskbar / other AppBars)
  SHAppBarMessage(ABM_QUERYPOS, &abd);

  // Restore size (system only adjusts the edge interval, not the size)
  switch (abd.uEdge) {
    case ABE_LEFT:   abd.rc.right  = abd.rc.left + w; break;
    case ABE_RIGHT:  abd.rc.left   = abd.rc.right - w; break;
    case ABE_TOP:    abd.rc.bottom = abd.rc.top + h;   break;
    case ABE_BOTTOM: abd.rc.top    = abd.rc.bottom - h; break;
  }

  // Set: system updates its internal WorkArea
  SHAppBarMessage(ABM_SETPOS, &abd);

  Napi::Object result = Napi::Object::New(info.Env());
  result.Set("x", Napi::Number::New(info.Env(), abd.rc.left));
  result.Set("y", Napi::Number::New(info.Env(), abd.rc.top));
  result.Set("w", Napi::Number::New(info.Env(), abd.rc.right  - abd.rc.left));
  result.Set("h", Napi::Number::New(info.Env(), abd.rc.bottom - abd.rc.top));
  return result;
#else
  return info.Env().Undefined();
#endif
}

// ---- getTaskbarPos() -> { edge, left, top, right, bottom } ------------------------
Napi::Value GetTaskbarPos(const Napi::CallbackInfo& info) {
#ifdef _WIN32
  APPBARDATA abd = {};
  abd.cbSize = sizeof(APPBARDATA);
  SHAppBarMessage(ABM_GETTASKBARPOS, &abd);
  Napi::Object result = Napi::Object::New(info.Env());
  result.Set("edge",   Napi::Number::New(info.Env(), abd.uEdge));
  result.Set("left",   Napi::Number::New(info.Env(), abd.rc.left));
  result.Set("top",    Napi::Number::New(info.Env(), abd.rc.top));
  result.Set("right",  Napi::Number::New(info.Env(), abd.rc.right));
  result.Set("bottom", Napi::Number::New(info.Env(), abd.rc.bottom));
  return result;
#else
  return info.Env().Undefined();
#endif
}

// ---- module init -----------------------------------------------------------------
Napi::Object Init(Napi::Env env, Napi::Object exports) {
  exports.Set("register",      Napi::Function::New(env, Register));
  exports.Set("remove",        Napi::Function::New(env, Remove));
  exports.Set("queryAndSetPos",Napi::Function::New(env, QueryAndSetPos));
  exports.Set("getTaskbarPos", Napi::Function::New(env, GetTaskbarPos));

  Napi::Object edges = Napi::Object::New(env);
  edges.Set("LEFT",   Napi::Number::New(env, ABE_LEFT));
  edges.Set("TOP",    Napi::Number::New(env, ABE_TOP));
  edges.Set("RIGHT",  Napi::Number::New(env, ABE_RIGHT));
  edges.Set("BOTTOM", Napi::Number::New(env, ABE_BOTTOM));
  exports.Set("EDGE", edges);

  return exports;
}
NODE_API_MODULE(appbar, Init)
