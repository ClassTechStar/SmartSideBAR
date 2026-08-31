/* eslint-env node */
// P0-4: ESLint config - enables lint for .ts and .vue files
// Minimal ruleset: catch real errors, avoid style noise
module.exports = {
  root: true,
  env: {
    node: true,
    browser: true,
    es2022: true,
  },
  parser: '@typescript-eslint/parser',
  parserOptions: {
    ecmaVersion: 2022,
    sourceType: 'module',
  },
  plugins: ['@typescript-eslint'],
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
  ],
  ignorePatterns: [
    'out/',
    'dist/',
    'node_modules/',
    'patches/',
    'resources/',
    'tests/',
  ],
  rules: {
    // IPC handlers often have unused event params (_event)
    '@typescript-eslint/no-unused-vars': ['warn', { argsIgnorePattern: '^_' }],
    // Electron/WMI interop needs explicit any in many places
    '@typescript-eslint/no-explicit-any': 'off',
    // Allow require() in preload/main for CommonJS interop
    '@typescript-eslint/no-var-requires': 'off',
    // Same rule under the new typescript-eslint naming (lazy require('electron') etc.)
    '@typescript-eslint/no-require-imports': 'off',
    // Allow non-null assertions (common in DOM refs)
    '@typescript-eslint/no-non-null-assertion': 'off',
    // 主进程内嵌大量 PowerShell 模板脚本, \$ / \" 是防御性转义 (运行时等价),
    // 逐个移除只会产生对 PS 脚本的无意义改动噪音
    'no-useless-escape': 'off',
  },
  overrides: [
    {
      files: ['*.vue'],
      // 必须显式指定 vue-eslint-parser, 否则根级 @typescript-eslint/parser
      // 会直接解析整个 .vue 文件, <script setup lang="ts"> 的类型标注全部报解析错误
      parser: 'vue-eslint-parser',
      parserOptions: {
        parser: '@typescript-eslint/parser',
        ecmaVersion: 2022,
        sourceType: 'module',
      },
      extends: ['plugin:vue/vue3-recommended'],
      rules: {
        'vue/multi-word-component-names': 'off',
        // 巨型组件既有风格, 属 P2-10 重构范畴而非本次质量门
        'vue/max-attributes-per-line': 'off',
        'vue/singleline-html-element-content-newline': 'off',
        'vue/html-self-closing': 'off',
        'vue/attributes-order': 'off',
        'vue/first-attribute-linebreak': 'off',
        'vue/html-indent': 'off',
        'vue/html-closing-bracket-newline': 'off',
        'vue/attribute-hyphenation': 'off',
      },
    },
    {
      // *.d.ts 声明文件: DefineComponent<{}, {}, any> 等惯用法
      files: ['*.d.ts'],
      rules: {
        '@typescript-eslint/no-empty-object-type': 'off',
      },
    },
  ],
}
