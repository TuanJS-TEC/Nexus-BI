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
import { BarChart, LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import VChart from 'vue-echarts'
import { olapApi } from '@/api/olap'
import type { OlapResult } from '@/types/olap'

use([CanvasRenderer, BarChart, LineChart, GridComponent, TooltipComponent, LegendComponent])

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

  const categories = r.Rows.map(row => String(row[dimensionCol] ?? ''))
  const hasQuantitySeries = measureCols.some(col => col.toLowerCase().includes('so luong'))
  const series = measureCols.map((col, idx) => ({
    name: col,
    type: idx === 0 ? 'line' : 'bar',
    smooth: idx === 0,
    yAxisIndex: col.toLowerCase().includes('so luong') ? 1 : 0,
    data: r.Rows.map(row => {
      const n = Number(row[col])
      return Number.isFinite(n) ? n : null
    }),
  }))

  return {
    tooltip: { trigger: 'axis' },
    legend: { bottom: 0 },
    grid: { top: 18, left: 56, right: 18, bottom: 56, containLabel: true },
    xAxis: {
      type: 'category',
      data: categories,
      axisLabel: { rotate: categories.length > 8 ? 30 : 0 },
    },
    yAxis: hasQuantitySeries
      ? [
          { type: 'value' },
          {
            type: 'value',
            position: 'right',
            axisLabel: { formatter: (v: number) => v.toLocaleString('vi-VN') },
            splitLine: { show: false },
          },
        ]
      : { type: 'value' },
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
