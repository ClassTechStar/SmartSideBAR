// 临时冒烟测试: 用打包的 Electron 30 二进制以 RUN_AS_NODE 模式加载 appbar.node
const path = require('path')
const nodePath = path.join(__dirname, '..', 'dist', 'win-unpacked', 'resources', 'app.asar.unpacked', 'out', 'main', 'appbar.node')
try {
  const m = require(nodePath)
  console.log('ABI_OK', JSON.stringify(m.EDGE), JSON.stringify(m.getTaskbarPos()))
} catch (e) {
  console.log('ABI_FAIL', e.message)
  process.exit(2)
}
