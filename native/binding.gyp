{
  "targets": [
    {
      "target_name": "appbar",
      "sources": ["../src/native/appbar.cc"],
      "include_dirs": ["<!@(node -p \"require('node-addon-api').include\")"],
      "defines": ["NAPI_DISABLE_CPP_EXCEPTIONS"],
      "cflags!": ["-fno-exceptions"],
      "cflags_cc!": ["-fno-exceptions"],
      "conditions": [
        ["OS=='win'", {
          "libraries": ["-lshell32.lib", "-luser32.lib"],
          "msvs_settings": {
            "VCCLCompilerTool": {
              "AdditionalOptions": ["/utf-8"]
            }
          }
        }]
      ]
    }
  ]
}
