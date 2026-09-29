import { defineConfig, externalizeDepsPlugin } from 'electron-vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import { copyFileSync, existsSync, readFileSync } from 'fs'

// A3: 从 package.json 读取版本号, 构建期注入 __APP_VERSION__
const pkg = JSON.parse(readFileSync(resolve(__dirname, 'package.json'), 'utf-8'))
const appVersion: string = pkg.version

// 原生模块复制插件: 构建时将 native/build/Release/appbar.node 复制到 out/main/
function nativeModulePlugin(): any {
  return {
    name: 'copy-native-modules',
    closeBundle() {
      const src = resolve(__dirname, 'native/build/Release/appbar.node')
      const dst = resolve(__dirname, 'out/main/appbar.node')
      if (existsSync(src)) {
        copyFileSync(src, dst)
        console.log('[native] Copied appbar.node → out/main/')
      }
    }
  }
}

export default defineConfig({
  main: {
    plugins: [externalizeDepsPlugin(), nativeModulePlugin()],
    define: {
      __APP_VERSION__: JSON.stringify(appVersion)
    },
    build: {
      outDir: 'out/main',
      rollupOptions: {
        input: {
          index: resolve(__dirname, 'src/main/main.ts')
        }
      }
    },
    resolve: {
      alias: {
        '@shared': resolve(__dirname, 'src/shared'),
        '@main': resolve(__dirname, 'src/main')
      }
    }
  },
  preload: {
    plugins: [externalizeDepsPlugin()],
    define: {
      __APP_VERSION__: JSON.stringify(appVersion)
    },
    build: {
      outDir: 'out/preload',
      rollupOptions: {
        input: {
          index: resolve(__dirname, 'src/main/preload.ts')
        }
      }
    }
  },
  renderer: {
    plugins: [vue()],
    define: {
      __APP_VERSION__: JSON.stringify(appVersion)
    },
    root: resolve(__dirname, 'src/renderer'),
    build: {
      outDir: 'out/renderer',
      rollupOptions: {
        input: {
          index: resolve(__dirname, 'src/renderer/index.html')
        }
      }
    },
    resolve: {
      alias: {
        '@': resolve(__dirname, 'src/renderer'),
        '@shared': resolve(__dirname, 'src/shared')
      }
    }
  }
})
