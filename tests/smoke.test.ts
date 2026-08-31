// P0-4: Smoke test - verify testing infrastructure works and core invariants hold
import { describe, it, expect } from 'vitest'
import { isAllowedChannel, IPC_CHANNELS, ALLOWED_CHANNELS } from '../src/shared/ipc-channels'

describe('IPC Channel Registry', () => {
  it('should allow registered channels', () => {
    expect(isAllowedChannel('config:get')).toBe(true)
    expect(isAllowedChannel('config:set')).toBe(true)
    expect(isAllowedChannel('ime:toggle')).toBe(true)
    expect(isAllowedChannel('capture:region')).toBe(true)
    expect(isAllowedChannel('overlay:ready')).toBe(true)
  })

  it('should reject unregistered channels', () => {
    expect(isAllowedChannel('__proto__')).toBe(false)
    expect(isAllowedChannel('evil:channel')).toBe(false)
    expect(isAllowedChannel('')).toBe(false)
    expect(isAllowedChannel('constructor')).toBe(false)
  })

  it('should have consistent key-value mapping (each key equals its value)', () => {
    for (const [key, value] of Object.entries(IPC_CHANNELS)) {
      expect(key).toBe(value)
    }
  })

  it('should have ALLOWED_CHANNELS set match IPC_CHANNELS keys', () => {
    expect(ALLOWED_CHANNELS.size).toBe(Object.keys(IPC_CHANNELS).length)
  })
})

describe('Type Integrity', () => {
  it('should export expected channel count', () => {
    // The IPC_CHANNELS object should have a reasonable number of entries
    // This catches accidental deletion of channels
    const count = Object.keys(IPC_CHANNELS).length
    expect(count).toBeGreaterThan(30)
    expect(count).toBeLessThan(100)
  })
})
