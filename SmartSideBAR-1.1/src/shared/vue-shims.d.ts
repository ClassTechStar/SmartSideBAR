// vue-shims.d.ts - Vue SFC 类型声明
declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<{}, {}, any>
  export default component
}

// Node.js 进程扩展
declare namespace NodeJS {
  interface Process {
    env: {
      NODE_ENV?: string
      [key: string]: string | undefined
    }
  }
}
