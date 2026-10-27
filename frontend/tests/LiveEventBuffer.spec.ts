import { describe, expect, it } from 'vitest'
import { LiveEventBuffer } from '../src/utils/liveEventBuffer'
import type { AuditEvent } from '../src/types/api'

function event(id: string, timestamp: string): AuditEvent {
  return {
    id, eventTimestampUtc: timestamp, smbUser: 'soporte', clientIp: 'client.example.invalid', operation: 'pwrite',
    interpretedAction: 'Write', interpretationConfidence: 'Confirmed', operationResult: 'ok', shareName: 'PRUEBA_AUDITORIA_SMB',
    filePath: '/volume1/PRUEBA_AUDITORIA_SMB/test.txt', sourcePath: null, destinationPath: null, fileExtension: '.txt',
    objectType: 'file', isAuxiliaryStream: false, isRecyclePath: false, rawMessage: 'message', sourceName: 'Journal:test',
    sourceKind: 'Journal', journalCursor: `cursor-${id}`, bootId: 'boot', metadataJson: '{}', dataMode: 'Live', parserVersion: 'phase2-ugos-v2',
  }
}

describe('LiveEventBuffer', () => {
  it('deduplicates replayed SSE events by durable event id', () => {
    const buffer = new LiveEventBuffer(100)
    buffer.push(event('one', '2026-10-27T12:00:00Z'))
    buffer.push(event('one', '2026-10-27T12:00:00Z'))

    expect(buffer.visible()).toHaveLength(1)
  })

  it('buffers while paused and reveals events when resumed', () => {
    const buffer = new LiveEventBuffer(100)
    buffer.pause()
    buffer.push(event('one', '2026-10-27T12:00:00Z'))

    expect(buffer.visible()).toHaveLength(0)
    expect(buffer.pendingCount()).toBe(1)
    buffer.resume()
    expect(buffer.visible()).toHaveLength(1)
  })

  it('bounds pending events while the screen is paused', () => {
    const buffer = new LiveEventBuffer(2)
    buffer.pause()
    buffer.push(event('one', '2026-10-27T12:00:00Z'))
    buffer.push(event('two', '2026-10-27T12:01:00Z'))
    buffer.push(event('three', '2026-10-27T12:02:00Z'))

    expect(buffer.pendingCount()).toBe(2)
    buffer.resume()
    expect(buffer.visible().map(item => item.id)).toEqual(['three', 'two'])
  })
})
