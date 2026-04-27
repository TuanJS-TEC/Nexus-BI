<template>
  <div class="overview-chart">
    <div class="overview-head">
      <h3 class="overview-title">{{ title }}</h3>
      <p class="overview-note">{{ description }}</p>
      <span class="overview-cube">{{ cube }}</span>
    </div>

    <div v-if="loading" class="overview-state">Đang tải dữ liệu biểu đồ...</div>
    <div v-else-if="error" class="overview-state error">{{ error }}</div>
    <div v-else-if="!chartOption" class="overview-state">Chưa có dữ liệu để hiển thị.</div>
    <v-chart
      v-else
      class="chart"
      :option="chartOption"
      :autoresize="true"
      :update-options="{ notMerge: true, lazyUpdate: false }"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import VChart from 'vue-echarts'
import { olapApi } from '@/api/olap'
import type { OlapResult } from '@/types/olap'

use([CanvasRenderer, BarChart, GridComponent, TooltipComponent, LegendComponent])

const props = defineProps<{
  title: string
  description: string
  cube: string
}>()

const loading = ref(false)
const error = ref('')
const result = ref<OlapResult | null>(null)

const chartOption = computed(() => {
  const r = result.value
  if (!r?.Success || !r.Rows.length || !r.Columns.length) return null

  const [dimensionCol, ...measureCols] = r.Columns
  if (!dimensionCol || !measureCols.length) return null

  const palette = ['#003d5c', '#31497e', '#674f95', '#a14e9a', '#d44c8d', '#f9596f', '#ff7a47', '#ffa600']
  const categories = r.Rows.map(row => String(row[dimensionCol] ?? ''))
  const temporalX = /\[nam\]|\[quy\]|\[thang\]|year|quarter|month/i.test(dimensionCol)
  const hasQuantitySeries = measureCols.some(col => col.toLowerCase().includes('so luong'))
  const hasRateSeries = measureCols.some(col => /ty\s*le|%|rate|ratio/i.test(col))
  const useCombo = temporalX && hasQuantitySeries && hasRateSeries && measureCols.length >= 2
  const useHorizontalBar = !temporalX && categories.length >= 10
  const displayMeasureCols = measureCols.slice(0, temporalX ? 5 : 10)
  const indexedRows = r.Rows.map((row, idx) => ({ row, label: categories[idx] ?? `Member ${idx + 1}` }))
  const sortedRows = useHorizontalBar && displayMeasureCols[0]
    ? [...indexedRows].sort((a, b) => Number(b.row[displayMeasureCols[0]] ?? 0) - Number(a.row[displayMeasureCols[0]] ?? 0))
    : indexedRows
  const limitedRows = useHorizontalBar ? sortedRows.slice(0, 10) : indexedRows
  const displayCategories = limitedRows.map(item => item.label)

  const series = displayMeasureCols.map((col, idx) => ({
    name: col,
    type: 'bar',
    yAxisIndex: col.toLowerCase().includes('so luong') ? 1 : 0,
    itemStyle: { color: palette[idx % palette.length] },
    lineStyle: temporalX && idx === 0 ? { color: palette[idx % palette.length], width: 2 } : undefined,
    data: limitedRows.map(item => {
      const n = Number(item.row[col])
      return Number.isFinite(n) ? n : null
    }),
  }))

  return {
    tooltip: { trigger: 'axis' },
    legend: { bottom: 0, textStyle: { color: '#64748b' } },
    grid: { top: 18, left: 56, right: 18, bottom: 56, containLabel: true },
    xAxis: useHorizontalBar
      ? { type: 'value', min: 0, axisLabel: { color: '#64748b' } }
      : {
          type: 'category',
          data: displayCategories,
          axisLabel: { rotate: displayCategories.length > 8 ? 30 : 0, color: '#64748b' },
        },
    yAxis: useHorizontalBar
      ? { type: 'category', data: displayCategories, axisLabel: { color: '#64748b' } }
      : (useCombo && hasQuantitySeries
      ? [
          { type: 'value', min: 0, axisLabel: { color: '#64748b' } },
          {
            type: 'value',
            position: 'right',
            axisLine: { lineStyle: { color: palette[2] } },
            axisLabel: { formatter: (v: number) => v.toLocaleString('vi-VN'), color: palette[1] },
            splitLine: { show: false },
          },
        ]
      : { type: 'value', min: 0, axisLabel: { color: '#64748b' } }),
    series,
  }
})

async function loadOverview() {
  loading.value = true
  error.value = ''
  try {
    result.value = await olapApi.getDefaultQuery(props.cube)
    if (!result.value?.Success) {
      error.value = result.value?.Error || 'Không thể tải dữ liệu biểu đồ.'
    }
  } catch (e: unknown) {
    error.value = (e as Error).message || 'Không thể tải dữ liệu biểu đồ.'
  } finally {
    loading.value = false
  }
}

onMounted(loadOverview)
watch(() => props.cube, loadOverview)
</script>

<style scoped>
.overview-chart {
  display: flex;
  flex-direction: column;
}

.overview-head {
  padding: 0.8rem 1rem 0.4rem;
}

.overview-title {
  margin: 0;
  font-size: 0.9rem;
  font-weight: 700;
}

.overview-note {
  margin: 0.3rem 0 0;
  font-size: 0.78rem;
  color: var(--text-muted);
}

.overview-cube {
  display: inline-block;
  margin-top: 0.35rem;
  font-size: 0.72rem;
  color: var(--text-muted);
  background: var(--bg-input);
  border: 1px solid var(--border);
  border-radius: 999px;
  padding: 0.15rem 0.55rem;
}

.overview-state {
  min-height: 220px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-muted);
  text-align: center;
  padding: 1rem;
}

.overview-state.error {
  color: var(--color-kpi-critical);
}

.chart {
  height: 320px;
  padding: 0 0.8rem 0.6rem;
}
</style>
