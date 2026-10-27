<script setup lang="ts">
import { ArrowUpRight } from '@lucide/vue'
import type { Component } from 'vue'

withDefaults(defineProps<{
  label: string
  value: string | number
  detail?: string
  icon: Component
  tone?: 'blue' | 'green' | 'amber' | 'red' | 'slate'
  clickable?: boolean
}>(), { detail: '', tone: 'blue', clickable: false })
defineEmits<{ select: [] }>()
</script>

<template>
  <component :is="clickable ? 'button' : 'div'" class="metric" :class="[`metric--${tone}`, { clickable }]" type="button" @click="clickable && $emit('select')">
    <div class="metric__top">
      <span class="metric__icon"><component :is="icon" :size="19" /></span>
      <ArrowUpRight v-if="clickable" class="metric__arrow" :size="16" />
    </div>
    <strong>{{ value }}</strong>
    <span class="metric__label">{{ label }}</span>
    <small v-if="detail">{{ detail }}</small>
  </component>
</template>

<style scoped>
.metric { display: grid; min-width: 0; width: 100%; min-height: 145px; align-content: start; border: 1px solid #cfe3f7; border-radius: 15px; padding: 15px; text-align: left; color: #0f172a; background: #fff; box-shadow: 0 7px 20px rgba(30, 64, 175, .045); }
.metric.clickable { transition: transform .15s, border-color .15s, box-shadow .15s; }
.metric.clickable:hover { transform: translateY(-2px); border-color: #60a5fa; box-shadow: 0 10px 25px rgba(30, 64, 175, .1); }
.metric__top { display: flex; align-items: center; justify-content: space-between; }
.metric__icon { display: grid; place-items: center; width: 36px; height: 36px; border-radius: 10px; color: #1d4ed8; background: #eaf2ff; }
.metric__arrow { color: #94a3b8; }
.metric strong { margin-top: 13px; overflow: hidden; text-overflow: ellipsis; font-size: 24px; line-height: 1; }
.metric__label { margin-top: 6px; color: #334155; font-size: 13px; font-weight: 700; }
.metric small { margin-top: 5px; overflow: hidden; color: #64748b; font-size: 11px; text-overflow: ellipsis; white-space: nowrap; }
.metric--green .metric__icon { color: #15803d; background: #ecfdf3; }
.metric--amber .metric__icon { color: #a16207; background: #fffbeb; }
.metric--red .metric__icon { color: #b91c1c; background: #fef2f2; }
.metric--slate .metric__icon { color: #475569; background: #f1f5f9; }
</style>

