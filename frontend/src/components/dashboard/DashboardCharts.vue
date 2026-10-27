<script setup lang="ts">
import { computed } from 'vue'
import type { EChartsCoreOption } from 'echarts/core'
import BaseChart from '../charts/BaseChart.vue'
import type { ActivityPoint, DistributionPoint, ShareActivity, TopUser } from '../../types/api'
import { actionLabels, formatTime } from '../../utils/format'

const props = defineProps<{ activity: ActivityPoint[]; distribution: DistributionPoint[]; topUsers: TopUser[]; shares: ShareActivity[] }>()
const colors = ['#1d4ed8', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2', '#64748b']

const common = { textStyle: { fontFamily: 'Segoe UI, Arial, sans-serif', color: '#475569' }, animationDuration: 350 }
const activityOption = computed<EChartsCoreOption>(() => ({
  ...common,
  grid: { left: 38, right: 18, top: 20, bottom: 32 },
  tooltip: { trigger: 'axis', valueFormatter: (value: unknown) => `${String(value)} eventos` },
  xAxis: { type: 'category', boundaryGap: false, data: props.activity.map(x => formatTime(x.bucketUtc)), axisLine: { lineStyle: { color: '#cbd5e1' } }, axisTick: { show: false } },
  yAxis: { type: 'value', minInterval: 1, splitLine: { lineStyle: { color: '#e8eff7' } } },
  series: [{ type: 'line', smooth: .28, symbolSize: 7, data: props.activity.map(x => x.count), lineStyle: { width: 3, color: '#1d4ed8' }, itemStyle: { color: '#1d4ed8' }, areaStyle: { color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1, colorStops: [{ offset: 0, color: 'rgba(59,130,246,.28)' }, { offset: 1, color: 'rgba(59,130,246,.02)' }] } } }],
}))

const distributionOption = computed<EChartsCoreOption>(() => ({
  ...common,
  tooltip: { trigger: 'item' },
  legend: { bottom: 0, left: 'center', itemWidth: 9, itemHeight: 9, textStyle: { fontSize: 11 } },
  color: colors,
  series: [{ type: 'pie', radius: ['46%', '70%'], center: ['50%', '43%'], avoidLabelOverlap: true, itemStyle: { borderColor: '#fff', borderWidth: 3, borderRadius: 4 }, label: { show: false }, data: props.distribution.map(x => ({ name: actionLabels[x.action] ?? x.action, value: x.count })) }],
}))

const usersOption = computed<EChartsCoreOption>(() => ({
  ...common,
  grid: { left: 95, right: 20, top: 14, bottom: 24 },
  tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
  xAxis: { type: 'value', minInterval: 1, splitLine: { lineStyle: { color: '#e8eff7' } } },
  yAxis: { type: 'category', inverse: true, data: props.topUsers.map(x => x.user), axisTick: { show: false }, axisLine: { show: false } },
  series: [{ type: 'bar', data: props.topUsers.map(x => x.count), barWidth: 14, itemStyle: { color: '#3b82f6', borderRadius: [0, 5, 5, 0] } }],
}))

const sharesOption = computed<EChartsCoreOption>(() => ({
  ...common,
  grid: { left: 110, right: 20, top: 14, bottom: 24 },
  tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
  xAxis: { type: 'value', minInterval: 1, splitLine: { lineStyle: { color: '#e8eff7' } } },
  yAxis: { type: 'category', inverse: true, data: props.shares.map(x => x.share), axisTick: { show: false }, axisLine: { show: false } },
  series: [{ type: 'bar', data: props.shares.map(x => x.count), barWidth: 14, itemStyle: { color: '#0891b2', borderRadius: [0, 5, 5, 0] } }],
}))
</script>

<template>
  <section class="charts">
    <article class="panel chart-card chart-card--wide">
      <div class="chart-head"><div><h2>Actividad registrada</h2><p>Eventos por intervalo en el período seleccionado</p></div></div>
      <BaseChart :option="activityOption" />
    </article>
    <article class="panel chart-card">
      <div class="chart-head"><div><h2>Distribución</h2><p>Operaciones interpretadas</p></div></div>
      <BaseChart :option="distributionOption" />
    </article>
    <article class="panel chart-card">
      <div class="chart-head"><div><h2>Cuentas con más actividad</h2><p>No implica identidad personal</p></div></div>
      <BaseChart :option="usersOption" />
    </article>
    <article class="panel chart-card">
      <div class="chart-head"><div><h2>Actividad por recurso</h2><p>Carpetas compartidas observadas</p></div></div>
      <BaseChart :option="sharesOption" />
    </article>
  </section>
</template>

<style scoped>
.charts { display: grid; grid-template-columns: 1.5fr 1fr 1fr; gap: 14px; }
.chart-card { min-width: 0; padding: 17px; }
.chart-card--wide { grid-column: 1 / -1; }
.chart-head h2 { margin: 0; font-size: 15px; }.chart-head p { margin: 4px 0 0; color: #64748b; font-size: 12px; }
@media (max-width: 1200px) { .charts { grid-template-columns: 1fr 1fr; }.chart-card--wide { grid-column: 1 / -1; } }
@media (max-width: 700px) { .charts { grid-template-columns: 1fr; }.chart-card--wide { grid-column: auto; } }
</style>

