import type { AuditEvent } from '../types/api'

export class LiveEventBuffer {
  private readonly events: AuditEvent[] = []
  private readonly pending: AuditEvent[] = []
  private readonly ids = new Set<string>()
  private paused = false

  constructor(private readonly limit: number) {}

  push(event: AuditEvent): boolean {
    if (this.ids.has(event.id)) return false
    this.ids.add(event.id)
    if (this.paused) {
      this.pending.push(event)
      if (this.pending.length > this.limit) {
        const removed = this.pending.shift()
        if (removed) this.ids.delete(removed.id)
      }
    }
    else this.insert(event)
    return true
  }

  pause(): void { this.paused = true }

  resume(): void {
    this.paused = false
    for (const event of this.pending) this.insert(event)
    this.pending.length = 0
  }

  visible(): readonly AuditEvent[] { return this.events }
  pendingCount(): number { return this.pending.length }
  isPaused(): boolean { return this.paused }

  private insert(event: AuditEvent): void {
    this.events.push(event)
    this.events.sort((left, right) => right.eventTimestampUtc.localeCompare(left.eventTimestampUtc))
    if (this.events.length <= this.limit) return
    for (const removed of this.events.splice(this.limit)) this.ids.delete(removed.id)
  }
}
