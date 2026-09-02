<template>
  <!--
    绝对定位 + 0 尺寸: SVG 只提供 <defs>, 绝不能参与布局。
    不能用 display:none —— Chromium 下隐藏的 SVG 里的滤镜会失效。
  -->
  <svg class="lg-defs" width="0" height="0" aria-hidden="true" focusable="false" xmlns="http://www.w3.org/2000/svg">
    <defs>
      <!-- lg-refract: RGB 三通道分别位移采样 -> screen 混合 -> 边缘 mask 内合成 (rdev 色散折射) -->
      <filter id="lg-refract" x="-35%" y="-35%" width="170%" height="170%" color-interpolation-filters="sRGB">
        <feImage x="0" y="0" width="100%" height="100%" preserveAspectRatio="xMidYMid slice" :href="lensMap || undefined" result="DISPLACEMENT_MAP" />
        <feColorMatrix in="DISPLACEMENT_MAP" type="matrix" values="0.3 0.3 0.3 0 0
                  0.3 0.3 0.3 0 0
                  0.3 0.3 0.3 0 0
                  0   0   0   1 0" result="EDGE_INTENSITY" />
        <feComponentTransfer in="EDGE_INTENSITY" result="EDGE_MASK">
          <feFuncA type="discrete" :tableValues="edgeMaskTableValues" />
        </feComponentTransfer>
        <feDisplacementMap in="SourceGraphic" in2="DISPLACEMENT_MAP" :scale="scaleR" xChannelSelector="R" yChannelSelector="B" result="RED_DISPLACED" />
        <feColorMatrix in="RED_DISPLACED" type="matrix" values="1 0 0 0 0
                  0 0 0 0 0
                  0 0 0 0 0
                  0 0 0 1 0" result="RED_CHANNEL" />
        <feDisplacementMap in="SourceGraphic" in2="DISPLACEMENT_MAP" :scale="scaleG" xChannelSelector="R" yChannelSelector="B" result="GREEN_DISPLACED" />
        <feColorMatrix in="GREEN_DISPLACED" type="matrix" values="0 0 0 0 0
                  0 1 0 0 0
                  0 0 0 0 0
                  0 0 0 1 0" result="GREEN_CHANNEL" />
        <feDisplacementMap in="SourceGraphic" in2="DISPLACEMENT_MAP" :scale="scaleB" xChannelSelector="R" yChannelSelector="B" result="BLUE_DISPLACED" />
        <feColorMatrix in="BLUE_DISPLACED" type="matrix" values="0 0 0 0 0
                  0 0 0 0 0
                  0 0 1 0 0
                  0 0 0 1 0" result="BLUE_CHANNEL" />
        <feBlend in="RED_CHANNEL" in2="GREEN_CHANNEL" mode="screen" result="RG_COMBINED" />
        <feBlend in="RG_COMBINED" in2="BLUE_CHANNEL" mode="screen" result="RGB_DISPLACED" />
        <feComposite in="RGB_DISPLACED" in2="EDGE_MASK" operator="in" result="RGB_DISPLACED_MASKED" />
        <feComposite in="SourceGraphic" in2="EDGE_MASK" operator="in" result="RGB_SOURCE_MASKED" />
        <feComposite in="RGB_DISPLACED_MASKED" in2="RGB_SOURCE_MASKED" operator="arithmetic" k1="0" k2="1" k3="1" k4="0" />
      </filter>

      <!-- lg-caustic: 湍流打散条纹渐变 -> 液态焦散 -->
      <filter id="lg-caustic" x="-20%" y="-20%" width="140%" height="140%" color-interpolation-filters="sRGB">
        <feTurbulence type="fractalNoise" baseFrequency="0.014 0.021" numOctaves="2" seed="41" stitchTiles="stitch" result="cNoise" />
        <feDisplacementMap in="SourceGraphic" in2="cNoise" :scale="causticScale" xChannelSelector="R" yChannelSelector="B" />
        <feGaussianBlur stdDeviation="5" result="cBlur" />
        <feComponentTransfer in="cBlur">
          <feFuncR type="linear" slope="1.5" intercept="-0.16" />
          <feFuncG type="linear" slope="1.5" intercept="-0.16" />
          <feFuncB type="linear" slope="1.5" intercept="-0.12" />
        </feComponentTransfer>
      </filter>

      <!-- lg-orb-light: 镜面高光 (feSpecularLighting + 点光源) -->
      <filter id="lg-orb-light" x="-25%" y="-25%" width="150%" height="150%" color-interpolation-filters="sRGB">
        <feGaussianBlur in="SourceAlpha" stdDeviation="3.5" result="orbAlpha" />
        <feSpecularLighting in="orbAlpha" surfaceScale="4.5" :specularConstant="orbSpecular" specularExponent="22" lighting-color="#ffffff" result="orbSpec">
          <fePointLight x="-14" y="-22" z="58" />
        </feSpecularLighting>
        <feComposite in="orbSpec" in2="SourceAlpha" operator="in" result="orbSpecIn" />
        <feComposite in="SourceGraphic" in2="orbSpecIn" operator="arithmetic" k1="0" k2="1" k3="1" k4="0" />
      </filter>
    </defs>
  </svg>
</template>

<script setup lang="ts">
// 液态玻璃 SVG 滤镜 defs (从 v1.1 编译产物重建)
// refraction/specular/aberration 优先取 props, 否则跟随 <html style> 上的 --lg-* 变量,
// 玻璃关闭 (data-glass=off) 时全部 scale 归零 —— 色散滤镜必须摘掉,
// scale=0 时 screen 混合仍会提亮装饰层。
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'

const props = defineProps<{
  refraction?: number
  specular?: number
  aberration?: number
  lensMap?: string
}>()

const cssRefraction = ref(14)
const cssSpecular = ref(0.55)
const cssAberration = ref(2)
const glassOff = ref(false)

function readVars() {
  const el = document.documentElement
  const cs = getComputedStyle(el)
  const r = parseFloat(cs.getPropertyValue('--lg-refraction'))
  const s = parseFloat(cs.getPropertyValue('--lg-specular'))
  const a = parseFloat(cs.getPropertyValue('--lg-aberration'))
  if (Number.isFinite(r)) cssRefraction.value = r
  if (Number.isFinite(s)) cssSpecular.value = s
  if (Number.isFinite(a)) cssAberration.value = a
  glassOff.value = el.getAttribute('data-glass') === 'off'
}

let observer: MutationObserver | null = null
onMounted(() => {
  readVars()
  observer = new MutationObserver(readVars)
  observer.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['style', 'data-glass', 'data-theme', 'data-motion']
  })
})
onBeforeUnmount(() => {
  observer?.disconnect()
  observer = null
})

const refraction = computed(() => props.refraction ?? cssRefraction.value)
const specular = computed(() => props.specular ?? cssSpecular.value)
const aberration = computed(() => props.aberration ?? cssAberration.value)
const scaleR = computed(() => (glassOff.value ? 0 : Math.max(0, refraction.value)))
const scaleG = computed(() => (glassOff.value ? 0 : Math.max(0, scaleR.value - aberration.value * 0.05)))
const scaleB = computed(() => (glassOff.value ? 0 : Math.max(0, scaleR.value - aberration.value * 0.1)))
const edgeMaskTableValues = computed(() => {
  const ab = aberration.value
  const t2 = Math.min(1, Math.max(0, ab * 0.05))
  return `0 ${t2} 1`
})
const causticScale = computed(() => (glassOff.value ? 0 : 12 + refraction.value * 1.6))
const orbSpecular = computed(() => (0.5 + specular.value * 1.2).toFixed(2))
</script>

<style>
.lg-defs {
  position: absolute;
  top: 0;
  left: 0;
  width: 0;
  height: 0;
  overflow: hidden;
  pointer-events: none;
}
</style>
