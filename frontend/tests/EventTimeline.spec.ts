import { mount } from '@vue/test-utils'
import EventTimeline from '../src/components/events/EventTimeline.vue'
import type { AuditEvent } from '../src/types/api'

const event: AuditEvent = {
  id: 'one', eventTimestampUtc: '2026-10-27T12:00:00Z', smbUser: 'soporte', clientIp: 'client.example.invalid',
  operation: 'renameat', interpretedAction: 'Rename', interpretationConfidence: 'Confirmed', operationResult: 'ok',
  shareName: 'PRUEBA_AUDITORIA_SMB', filePath: '/volume1/PRUEBA_AUDITORIA_SMB/nuevo.txt',
  sourcePath: '/volume1/PRUEBA_AUDITORIA_SMB/anterior.txt', destinationPath: '/volume1/PRUEBA_AUDITORIA_SMB/nuevo.txt',
  fileExtension: '.txt', objectType: 'file', isAuxiliaryStream: false, isRecyclePath: false,
  rawMessage: 'ugreen_syslog', sourceName: 'Journal:test', sourceKind: 'Journal', journalCursor: 'cursor', bootId: 'boot',
  metadataJson: '{}', dataMode: 'Live', parserVersion: 'phase2-ugos-v2',
}

describe('EventTimeline', () => {
  it('shows evidence without converting a technical rename into another action', () => {
    const wrapper = mount(EventTimeline, { props: { events: [event] } })
    expect(wrapper.text()).toContain('Renombrado')
    expect(wrapper.text()).toContain('renameat')
    expect(wrapper.text()).toContain('nuevo.txt')
    expect(wrapper.text()).not.toContain('Descarga')
  })
})
