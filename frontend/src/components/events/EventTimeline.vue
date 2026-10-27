<script setup lang="ts">
import type { AuditEvent } from '../../types/api'
import { actionLabels, fileName, formatDateTime } from '../../utils/format'

defineProps<{ events: AuditEvent[] }>()
</script>

<template>
  <ol class="timeline">
    <li v-for="event in events" :key="event.id">
      <span class="dot" :class="event.interpretedAction.toLowerCase()" />
      <div class="when">{{ formatDateTime(event.eventTimestampUtc) }}</div>
      <div class="event-main">
        <strong>{{ actionLabels[event.interpretedAction] ?? event.interpretedAction }}</strong>
        <span>{{ fileName(event.destinationPath ?? event.filePath ?? event.sourcePath) }}</span>
        <small>{{ event.smbUser ?? 'Cuenta no informada' }} · {{ event.clientIp ?? 'IP no informada' }} · {{ event.operation }}</small>
      </div>
      <span class="result" :class="event.operationResult.toLowerCase() === 'ok' ? 'ok' : 'failed'">{{ event.operationResult }}</span>
    </li>
  </ol>
</template>

<style scoped>
.timeline{display:grid;gap:0;margin:0;padding:0;list-style:none}.timeline li{position:relative;display:grid;grid-template-columns:14px 145px 1fr auto;gap:10px;align-items:start;padding:12px 3px;border-bottom:1px solid #e8eef6}.timeline li:last-child{border-bottom:0}.dot{width:9px;height:9px;margin-top:5px;border-radius:50%;background:#64748b;box-shadow:0 0 0 4px #f1f5f9}.dot.creation{background:#16a34a}.dot.write{background:#2563eb}.dot.deletion{background:#dc2626}.dot.rename,.dot.move{background:#f59e0b}.when{color:#64748b;font-size:11px}.event-main strong,.event-main span,.event-main small{display:block}.event-main strong{font-size:12px}.event-main span{margin-top:2px;color:#334155;font-size:12px;word-break:break-word}.event-main small{margin-top:3px;color:#64748b}.result{border-radius:999px;padding:3px 7px;background:#f1f5f9;color:#475569;font-size:9px;font-weight:800}.result.ok{background:#ecfdf3;color:#15803d}.result.failed{background:#fef2f2;color:#b91c1c}@media(max-width:680px){.timeline li{grid-template-columns:14px 1fr auto}.when{grid-column:2 / -1}.event-main{grid-column:2}.result{grid-column:3;grid-row:2}}
</style>
