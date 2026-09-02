// src/renderer/composables/useGlass.ts - 渲染层液态玻璃辅助
// makeLensMap: 生成圆形折射位移贴图 (data URL), 供 LiquidGlassDefs 的 feImage 使用。
// 从 v1.1 编译产物重建。

function clamp255(v: number): number {
  return Math.max(0, Math.min(255, Math.round(v)))
}

export function makeLensMap(size = 128): string {
  const canvas = document.createElement('canvas')
  canvas.width = size
  canvas.height = size
  const ctx = canvas.getContext('2d')
  if (!ctx) return ''
  const img = ctx.createImageData(size, size)
  const cx = (size - 1) / 2
  const cy = (size - 1) / 2
  const R = size * 0.46
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const dx = x - cx
      const dy = y - cy
      const dist = Math.sqrt(dx * dx + dy * dy)
      const i = (y * size + x) * 4
      if (dist < R) {
        const nx = dx / R
        const ny = dy / R
        img.data[i] = clamp255(128 + nx * 127)
        img.data[i + 1] = clamp255(128 + ny * 127)
        img.data[i + 2] = clamp255(128 + ny * 127)
        img.data[i + 3] = 255
      } else {
        img.data[i] = 128
        img.data[i + 1] = 128
        img.data[i + 2] = 128
        img.data[i + 3] = 255
      }
    }
  }
  ctx.putImageData(img, 0, 0)
  return canvas.toDataURL('image/png')
}
