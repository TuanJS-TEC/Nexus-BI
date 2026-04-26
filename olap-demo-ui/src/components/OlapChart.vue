<template>
  <div class="chart-container glass-card">
    <div class="chart-header">
      <h3 class="chart-title">Biểu Đồ</h3>
      <div class="chart-controls">
        <button
          v-for="t in chartTypes"
          :key="t.type"
          class="chart-type-btn"
          :class="{ active: activeType === t.type }"
          @click="activeType = t.type"
        >
          {{ t.label }}
        </button>
      </div>
    </div>

    <div class="chart-loading" v-if="store.isLoading">
      <div class="skeleton-chart"></div>
    </div>

    <div class="chart-empty" v-else-if="!hasData">
      <p>Chưa có dữ liệu để hiển thị biểu đồ</p>
    </div>

    <v-chart
      v-else
      :key="chartRenderKey"
      class="chart"
      :option="chartOption"
      :autoresize="true"
      :update-options="chartUpdateOptions"
      @click="onChartClick"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { BarChart, LineChart, PieChart, HeatmapChart } from 'echarts/charts'
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  DataZoomComponent,
  VisualMapComponent,
} from 'echarts/components'
import VChart from 'vue-echarts'
import { useOlapStore } from '@/stores/olapStore'

use([
  CanvasRenderer,
  BarChart,
  LineChart,
  PieChart,
  HeatmapChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  DataZoomComponent,
  VisualMapComponent,
])

const store = useOlapStore()
type ChartType = 'combo' | 'bar' | 'line' | 'area' | 'stacked' | 'pie' | 'donut' | 'heatmap'
const activeType = ref<ChartType>('combo')

const chartTypes = [
  { type: 'combo' as const, label: 'Kết hợp' },
  { type: 'bar' as const, label: 'Cột' },
  { type: 'line' as const, label: 'Đường' },
  { type: 'area' as const, label: 'Miền' },
  { type: 'stacked' as const, label: 'Chồng lớp' },
  { type: 'pie' as const, label: 'Tròn' },
  { type: 'donut' as const, label: 'Donut' },
  { type: 'heatmap' as const, label: 'Heatmap' },
]

const result = computed(() => store.resultData)

const hasData = computed(() =>
  result.value?.Success && result.value.Rows.length > 0
)

const chartUpdateOptions = {
  notMerge: true,
  lazyUpdate: false,
}

const chartRenderKey = computed(() => {
  const r = result.value
  if (!r) return activeType.value
  return [
    activeType.value,
    r.Columns.join('|'),
    r.Rows.length,
  ].join('::')
})

const chartOption = computed(() => {
  if (!hasData.value || !result.value) return {}

  const rows = result.value.Rows
  const cols = result.value.Columns
  const dimCol = cols[0] // first col is dimension
  const measureCols = cols.slice(1)

  const categories = rows.map(r => String(r[dimCol] ?? ''))
  const hasQuantitySeries = measureCols.some(col =>
    col.toLowerCase().includes('so luong')
  )
  const maxVisibleSeries = 12

  const palette = ['#5B7E3C', '#A2CB8B', '#E8F5BD', '#C44545', '#468432', '#9AD872']

  const resolveSeriesType = (columnName: string): 'bar' | 'line' => {
    if (activeType.value === 'bar' || activeType.value === 'line') return activeType.value
    if (activeType.value === 'area') return 'line'
    if (activeType.value === 'stacked') return 'bar'
    return columnName.toLowerCase().includes('so luong') ? 'bar' : 'line'
  }

  const rankedMeasureCols = [...measureCols]
    .map(col => ({
      col,
      score: rows.reduce((sum, row) => {
        const n = Number(row[col])
        return Number.isFinite(n) ? sum + Math.abs(n) : sum
      }, 0),
    }))
    .sort((a, b) => b.score - a.score)

  const displayMeasureCols = rankedMeasureCols
    .slice(0, maxVisibleSeries)
    .map(item => item.col)

  const hiddenSeriesCount = Math.max(0, measureCols.length - displayMeasureCols.length)

  const series = displayMeasureCols.map((col, i) => {
    const chartType = resolveSeriesType(col)
    const isArea = activeType.value === 'area'
    const isStacked = activeType.value === 'stacked'
    return {
      name: col,
      type: chartType,
      stack: isStacked ? 'total' : undefined,
      smooth: chartType === 'line',
      yAxisIndex: col.toLowerCase().includes('so luong') ? 1 : 0,
      data: rows.map(r => {
        const v = r[col]
        return v === null || v === undefined ? null : Number(v)
      }),
      itemStyle: { color: palette[i % palette.length] },
      lineStyle: chartType === 'line'
        ? { width: 2, color: palette[i % palette.length] }
        : undefined,
      areaStyle: chartType === 'line' && (isArea || activeType.value === 'combo')
        ? { color: palette[i % palette.length], opacity: isArea ? 0.18 : 0.08 }
        : undefined,
    }
  })

  // Pie/Donut: suitable for comparing category contributions on a selected measure.
  if (activeType.value === 'pie' || activeType.value === 'donut') {
    const pieMeasure = displayMeasureCols[0] ?? measureCols[0]
    if (!pieMeasure) return {}
    const pieValues = rows
      .map(r => ({
        name: String(r[dimCol] ?? ''),
        value: Number(r[pieMeasure] ?? 0),
      }))
      .filter(p => Number.isFinite(p.value))
      .sort((a, b) => b.value - a.value)

    const maxSlices = 10
    const visible = pieValues.slice(0, maxSlices)
    const hidden = pieValues.slice(maxSlices)
    const othersValue = hidden.reduce((sum, item) => sum + item.value, 0)
    const data = othersValue > 0 ? [...visible, { name: 'Khác', value: othersValue }] : visible

    return {
      backgroundColor: 'transparent',
      title: {
        text: `Tỷ trọng theo ${pieMeasure}`,
        left: 12,
        top: 8,
        textStyle: { fontSize: 12, fontWeight: 600, color: '#475569' },
      },
      tooltip: {
        trigger: 'item',
        formatter: '{b}<br/>{c} ({d}%)',
      },
      legend: {
        type: 'scroll',
        orient: 'vertical',
        right: 8,
        top: 24,
        bottom: 8,
        textStyle: { color: '#64748b', fontSize: 11 },
      },
      series: [
        {
          name: pieMeasure,
          type: 'pie',
          radius: activeType.value === 'donut' ? ['42%', '68%'] : '66%',
          center: ['40%', '54%'],
          data,
          label: { formatter: '{b}: {d}%', color: '#475569', fontSize: 11 },
          emphasis: {
            itemStyle: {
              shadowBlur: 16,
              shadowOffsetX: 0,
              shadowColor: 'rgba(15, 23, 42, 0.25)',
            },
          },
        },
      ],
    }
  }

  // Heatmap matrix: dimension members vs measure columns.
  if (activeType.value === 'heatmap') {
    const xLabels = displayMeasureCols.length ? displayMeasureCols : measureCols
    if (!xLabels.length) return {}
    const yLabels = categories
    const heatData: Array<[number, number, number]> = []

    let min = Number.POSITIVE_INFINITY
    let max = Number.NEGATIVE_INFINITY
    yLabels.forEach((_, rowIdx) => {
      xLabels.forEach((measure, colIdx) => {
        const value = Number(rows[rowIdx]?.[measure] ?? 0)
        if (Number.isFinite(value)) {
          min = Math.min(min, value)
          max = Math.max(max, value)
          heatData.push([colIdx, rowIdx, value])
        }
      })
    })

    return {
      backgroundColor: 'transparent',
      tooltip: {
        position: 'top',
        formatter: (params: { data: [number, number, number] }) => {
          const [x, y, v] = params.data
          return `${yLabels[y]}<br/>${xLabels[x]}: <strong>${v.toLocaleString('vi-VN')}</strong>`
        },
      },
      grid: { top: 16, left: 110, right: 16, bottom: 24, containLabel: true },
      xAxis: {
        type: 'category',
        data: xLabels,
        axisLabel: { color: '#64748b', fontSize: 11, rotate: xLabels.length > 6 ? 20 : 0 },
        splitArea: { show: true },
      },
      yAxis: {
        type: 'category',
        data: yLabels,
        axisLabel: { color: '#64748b', fontSize: 11 },
        splitArea: { show: true },
      },
      visualMap: {
        min: Number.isFinite(min) ? min : 0,
        max: Number.isFinite(max) ? max : 0,
        calculable: true,
        orient: 'horizontal',
        left: 'center',
        bottom: -2,
        textStyle: { color: '#64748b' },
        inRange: { color: ['#E8F5BD', '#A2CB8B', '#5B7E3C', '#468432'] },
      },
      series: [
        {
          type: 'heatmap',
          data: heatData,
          label: { show: false },
          emphasis: { itemStyle: { borderColor: '#fff', borderWidth: 1 } },
        },
      ],
    }
  }

  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      backgroundColor: 'rgba(15, 15, 30, 0.9)',
      borderColor: '#333',
      textStyle: { color: '#e2e8f0' },
      formatter: (params: unknown[]) => {
        const p = params as Array<{ name: string; seriesName: string; value: number | null }>
        let html = `<strong>${p[0]?.name}</strong><br/>`
        p.forEach(item => {
          const val = item.value === null ? '—'
            : item.value >= 1_000_000 ? (item.value / 1_000_000).toFixed(2) + ' Tr'
            : item.value.toLocaleString('vi-VN')
          html += `${item.seriesName}: <strong>${val}</strong><br/>`
        })
        return html
      },
    },
    title: hiddenSeriesCount > 0
      ? {
          text: `Đang hiển thị ${displayMeasureCols.length}/${measureCols.length} series lớn nhất`,
          right: 12,
          top: 6,
          textStyle: { fontSize: 11, fontWeight: 500, color: '#64748b' },
        }
      : undefined,
    legend: {
      type: 'scroll',
      textStyle: { color: '#94a3b8' },
      pageTextStyle: { color: '#64748b' },
      pageIconColor: '#5B7E3C',
      pageIconInactiveColor: '#cbd5e1',
      bottom: 0,
    },
    grid: { top: 20, right: 20, bottom: 60, left: 80, containLabel: true },
    xAxis: {
      type: 'category',
      data: categories,
      axisLine: { lineStyle: { color: '#334155' } },
      axisLabel: {
        color: '#64748b',
        rotate: categories.length > 8 ? 30 : 0,
        fontSize: 11,
      },
    },
    yAxis: {
      type: 'value',
      axisLine: { lineStyle: { color: '#334155' } },
      axisLabel: {
        color: '#64748b',
        formatter: (val: number) =>
          val >= 1_000_000 ? (val / 1_000_000).toFixed(1) + 'Tr' : val.toLocaleString('vi-VN'),
      },
      splitLine: { lineStyle: { color: '#1e293b' } },
    },
    ...(hasQuantitySeries
      ? {
          yAxis: [
            {
              type: 'value',
              axisLine: { lineStyle: { color: '#334155' } },
              axisLabel: {
                color: '#64748b',
                formatter: (val: number) =>
                  val >= 1_000_000 ? (val / 1_000_000).toFixed(1) + 'Tr' : val.toLocaleString('vi-VN'),
              },
              splitLine: { lineStyle: { color: '#1e293b' } },
            },
            {
              type: 'value',
              position: 'right',
              axisLine: { lineStyle: { color: '#A2CB8B' } },
              axisLabel: {
                color: '#5B7E3C',
                formatter: (val: number) => val.toLocaleString('vi-VN'),
              },
              splitLine: { show: false },
            },
          ],
        }
      : {}),
    dataZoom: categories.length > 20
      ? [{ type: 'inside' }, { type: 'slider', bottom: 30, height: 20 }]
      : [],
    series,
  }
})

async function onChartClick(params: { name?: string }) {
  const member = String(params?.name ?? '').trim()
  if (!member || store.isLoading) return
  if (store.canDrillDown) {
    await store.drillByMember(member)
    return
  }
  store.setContextByMember(member)
}
</script>

<style scoped>
.chart-container {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  overflow: hidden;
}

.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.75rem;
  padding: 1.25rem 1.5rem 0;
}

.chart-title {
  font-size: 1rem;
  font-weight: 700;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0;
}

.chart-controls {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.chart-type-btn {
  padding: 0.35rem 0.75rem;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--bg-input);
  color: var(--text-muted);
  font-family: inherit;
  font-size: 0.78rem;
  cursor: pointer;
  transition: all 0.2s;
}

.chart-type-btn.active,
.chart-type-btn:hover {
  background: var(--accent);
  color: #fff;
  border-color: var(--accent);
}

.chart {
  height: 320px;
  padding: 0 1rem 1rem;
}

.chart-empty {
  height: 200px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  color: var(--text-muted);
}

.chart-loading {
  height: 280px;
  padding: 0 1rem 1rem;
}

.skeleton-chart {
  width: 100%;
  height: 100%;
  border-radius: 10px;
  background: linear-gradient(90deg, #e2e8f0 25%, #f1f5f9 50%, #e2e8f0 75%);
  background-size: 220% 100%;
  animation: shimmer 1.1s infinite linear;
}

@keyframes shimmer {
  from { background-position: 220% 0; }
  to { background-position: -220% 0; }
}

@media (max-width: 768px) {
  .chart-header { padding: 1rem 1rem 0; }
  .chart-controls { width: 100%; }
  .chart-type-btn { flex: 1; }
  .chart {
    height: 280px;
    padding: 0 0.75rem 0.75rem;
  }
}

@media (max-width: 480px) {
  .chart {
    height: 250px;
    padding: 0 0.5rem 0.5rem;
  }
}
</style>
