<template>
  <div class="chart-container glass-card">
    <div class="chart-header">
      <h3 class="chart-title">Biểu Đồ</h3>
      <div class="chart-auto-badge">Tự động: {{ autoChartLabel }}</div>
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
import { computed } from 'vue'
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
  if (!r) return autoChartType.value
  return [
    autoChartType.value,
    r.Columns.join('|'),
    r.Rows.length,
  ].join('::')
})

function isCustomerDimensionActive(): boolean {
  return store.activeDimension === 'KhachHang'
}

function isInventoryLocationScenario(): boolean {
  return store.currentFact === 'TonKho' && store.activeDimension === 'DiaDiem'
}

const autoChartType = computed<ChartType>(() => {
  const r = result.value
  if (!r?.Success || !r.Rows.length) return 'bar'

  const measureCols = r.Columns.filter(col => isMeasureColumn(col, r.Rows))
  const dimensionCols = r.Columns.filter(col => !measureCols.includes(col))
  const dimensionDepth = dimensionCols.length

  // Business rule: inventory + location should stay grouped bar in demo flow.
  if (isInventoryLocationScenario()) return 'bar'
  if (dimensionDepth >= 3) return 'heatmap'
  if (dimensionDepth === 2) return 'bar'

  // Business rule: customer drill view prefers horizontal bar for readability.
  if (isCustomerDimensionActive()) return 'bar'

  if (store.activeDimension === 'ThoiGian') {
    // Time hierarchy is best represented as trend lines for Drill/Roll operations.
    return 'line'
  }

  if (store.lastOperation === 'DefaultQuery' && measureCols.length > 1) return 'combo'
  return 'bar'
})

const autoChartLabel = computed(() => {
  const map: Record<ChartType, string> = {
    combo: 'Kết hợp',
    bar: 'Cột',
    line: 'Đường',
    area: 'Miền',
    stacked: 'Chồng lớp',
    pie: 'Tròn',
    donut: 'Donut',
    heatmap: 'Heatmap',
  }
  return map[autoChartType.value] ?? 'Cột'
})

const preferHorizontalBar = computed(() =>
  autoChartType.value === 'bar' && isCustomerDimensionActive()
)

const isGroupedInventoryLocation = computed(() =>
  isInventoryLocationScenario() && autoChartType.value === 'bar'
)

function isMeasureColumn(columnName: string, rows: Array<Record<string, unknown>>): boolean {
  if (/\[MEASURES\]/i.test(columnName)) return true
  if (/\[(DIM|HIERARCHY)\]|\[NAM\]|\[QUY\]|\[THANG\]|\[LOAI\s*KH\]|\[TEN\s*KH\]|\[BANG\]|\[TEN\s*TP\]|MEMBER_CAPTION/i.test(columnName)) {
    return false
  }
  const sampleValues = rows
    .map(row => row[columnName])
    .filter(v => v !== null && v !== undefined)
    .slice(0, 12)

  if (!sampleValues.length) return false
  return sampleValues.every(v => Number.isFinite(Number(v)))
}

function pickCategoryColumn(
  dimensionCols: string[],
  currentLevel: string | undefined
): string | undefined {
  if (!dimensionCols.length) return undefined
  const level = String(currentLevel ?? '').toLowerCase()

  if (level === 'nam') {
    const yearCol = dimensionCols.find(c => /(\[nam\]|year|nam)/i.test(c))
    if (yearCol) return yearCol
  }
  if (level === 'quy') {
    const quarterCol = dimensionCols.find(c => /(\[quy\]|quarter|quy)/i.test(c))
    if (quarterCol) return quarterCol
  }
  if (level === 'thang') {
    const monthCol = dimensionCols.find(c => /(\[thang\]|month|thang)/i.test(c))
    if (monthCol) return monthCol
  }
  if (level === 'loaikh') {
    const customerTypeCol = dimensionCols.find(c => /(\[loai\s*kh\]|loaikh|customer\s*type)/i.test(c))
    if (customerTypeCol) return customerTypeCol
  }
  if (level === 'tenkh') {
    const customerNameCol = dimensionCols.find(c => /(\[ten\s*kh\]|tenkh|customer\s*name)/i.test(c))
    if (customerNameCol) return customerNameCol
  }
  if (level === 'bang') {
    const stateCol = dimensionCols.find(c => /(\[bang\]|state)/i.test(c))
    if (stateCol) return stateCol
  }
  if (level === 'thanhpho') {
    const cityCol = dimensionCols.find(c => /(\[ten\s*tp\]|thanhpho|city)/i.test(c))
    if (cityCol) return cityCol
  }

  const fallbackByDepth = [
    dimensionCols.find(c => /\[thang\]|month/i.test(c)),
    dimensionCols.find(c => /\[quy\]|quarter/i.test(c)),
    dimensionCols.find(c => /\[nam\]|year/i.test(c)),
  ].find(Boolean)
  return fallbackByDepth ?? dimensionCols[dimensionCols.length - 1]
}

function formatLevelLabel(rawValue: unknown, currentLevel: string | undefined): string {
  const raw = String(rawValue ?? '').trim()
  if (!raw) return raw
  const level = String(currentLevel ?? '').toLowerCase()

  if (level === 'quy') {
    const q = Number(raw)
    if (Number.isFinite(q) && q >= 1 && q <= 4) return `Quý ${q}`
    if (/^q(uy)?\s*[1-4]$/i.test(raw)) return raw.replace(/^q(uy)?\s*/i, 'Quý ')
    const simpleQuarter = raw.match(/\b([1-4])\b/)
    if (simpleQuarter) return `Quý ${simpleQuarter[1]}`
  }

  if (level === 'thang') {
    const m = Number(raw)
    if (Number.isFinite(m) && m >= 1 && m <= 12) return `Tháng ${m}`
    if (/^thang\s+\d{1,2}$/i.test(raw)) return raw.replace(/^thang/i, 'Tháng')
  }

  return raw
}

function normalizeDimensionLabel(rawValue: unknown, column: string): string {
  const raw = String(rawValue ?? '').trim()
  if (!raw) return 'N/A'
  if (/\[quy\]/i.test(column)) {
    const q = Number(raw)
    if (Number.isFinite(q) && q >= 1 && q <= 4) return `Quý ${q}`
  }
  if (/\[thang\]/i.test(column)) {
    const m = Number(raw)
    if (Number.isFinite(m) && m >= 1 && m <= 12) return `Tháng ${m}`
  }
  return raw
}

function collectNumericValues(rows: Array<Record<string, unknown>>, measureCols: string[]): number[] {
  const values: number[] = []
  rows.forEach((row) => {
    measureCols.forEach((measure) => {
      const n = Number(row[measure])
      if (Number.isFinite(n)) values.push(n)
    })
  })
  return values
}

function computeSpreadRatio(values: number[]): number {
  if (!values.length) return 1
  const absValues = values.map(v => Math.abs(v)).filter(v => v > 0)
  if (!absValues.length) return 1
  const min = Math.min(...absValues)
  const max = Math.max(...absValues)
  if (!Number.isFinite(min) || !Number.isFinite(max) || min <= 0) return 1
  return max / min
}

function buildCategoryDataZoom(axis: 'x' | 'y', enabled: boolean) {
  if (!enabled) return []
  if (axis === 'x') {
    return [
      { type: 'inside', xAxisIndex: 0, filterMode: 'none', zoomOnMouseWheel: true, moveOnMouseMove: true },
      { type: 'slider', xAxisIndex: 0, bottom: 30, height: 18, filterMode: 'none' },
    ]
  }
  return [
    { type: 'inside', yAxisIndex: 0, filterMode: 'none', zoomOnMouseWheel: true, moveOnMouseMove: true },
    { type: 'slider', yAxisIndex: 0, right: 8, width: 14, filterMode: 'none' },
  ]
}

const chartOption = computed(() => {
  if (!hasData.value || !result.value) return {}

  const rows = result.value.Rows
  const cols = result.value.Columns
  const measureCols = cols.filter(col => isMeasureColumn(col, rows))
  const dimensionCols = cols.filter(col => !measureCols.includes(col))
  const categoryCol = pickCategoryColumn(dimensionCols, store.currentLevel)
    ?? dimensionCols[0]
    ?? cols[0]
  const secondDimensionCol = dimensionCols.find(col => col !== categoryCol)
  const thirdDimensionCol = dimensionCols.find(col => col !== categoryCol && col !== secondDimensionCol)

  const categoriesRaw = rows.map(r => String(r[categoryCol] ?? '').trim())
  const categories = categoriesRaw.map((value, index) =>
    formatLevelLabel(value || `Member ${index + 1}`, store.currentLevel)
  )
  const hasQuantitySeries = measureCols.some(col =>
    col.toLowerCase().includes('so luong')
  )
  const maxVisibleSeries = 12

  const palette = ['#5B7E3C', '#A2CB8B', '#E8F5BD', '#C44545', '#468432', '#9AD872']

  const resolveSeriesType = (columnName: string): 'bar' | 'line' => {
    if (autoChartType.value === 'bar' || autoChartType.value === 'line') return autoChartType.value
    if (autoChartType.value === 'area') return 'line'
    if (autoChartType.value === 'stacked') return 'bar'
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
  const valueSpread = computeSpreadRatio(collectNumericValues(rows, displayMeasureCols.length ? displayMeasureCols : measureCols))
  const shouldEnableDetailZoom = categories.length > 12 || hiddenSeriesCount > 0 || valueSpread >= 25

  // Multi-dimensional mode: represent 2D/3D explicitly when row-set includes >= 2 dimensions.
  if (categoryCol && secondDimensionCol) {
    const mainMeasure = displayMeasureCols[0] ?? measureCols[0]
    if (!mainMeasure) return {}
    const xCategories = [...new Set(rows.map(r => normalizeDimensionLabel(r[categoryCol], categoryCol)))]
    const yGroups = [...new Set(rows.map(r => {
      const secondLabel = normalizeDimensionLabel(r[secondDimensionCol], secondDimensionCol)
      if (!thirdDimensionCol) return secondLabel
      const thirdLabel = normalizeDimensionLabel(r[thirdDimensionCol], thirdDimensionCol)
      return `${secondLabel} • ${thirdLabel}`
    }))]

    if (autoChartType.value === 'heatmap' && !isGroupedInventoryLocation.value) {
      const matrix = new Map<string, number>()
      rows.forEach(row => {
        const x = normalizeDimensionLabel(row[categoryCol], categoryCol)
        const secondLabel = normalizeDimensionLabel(row[secondDimensionCol], secondDimensionCol)
        const keyY = thirdDimensionCol
          ? `${secondLabel} • ${normalizeDimensionLabel(row[thirdDimensionCol], thirdDimensionCol)}`
          : secondLabel
        const value = Number(row[mainMeasure] ?? 0)
        if (Number.isFinite(value)) matrix.set(`${x}__${keyY}`, value)
      })
      const heatData: Array<[number, number, number]> = []
      let min = Number.POSITIVE_INFINITY
      let max = Number.NEGATIVE_INFINITY
      yGroups.forEach((group, rowIdx) => {
        xCategories.forEach((cat, colIdx) => {
          const value = matrix.get(`${cat}__${group}`) ?? 0
          min = Math.min(min, value)
          max = Math.max(max, value)
          heatData.push([colIdx, rowIdx, value])
        })
      })

      return {
        backgroundColor: 'transparent',
        title: {
          text: `Biểu diễn heatmap ${thirdDimensionCol ? '3D' : '2D'} theo ${mainMeasure}`,
          left: 12,
          top: 8,
          textStyle: { fontSize: 12, fontWeight: 600, color: '#475569' },
        },
        tooltip: {
          position: 'top',
          formatter: (params: { data: [number, number, number] }) => {
            const [x, y, v] = params.data
            return `${xCategories[x]}<br/>${yGroups[y]}<br/><strong>${mainMeasure}: ${v.toLocaleString('vi-VN')}</strong>`
          },
        },
        grid: { top: 34, left: 120, right: 16, bottom: 28, containLabel: true },
        xAxis: { type: 'category', data: xCategories, axisLabel: { color: '#64748b', fontSize: 11 } },
        yAxis: { type: 'category', data: yGroups, axisLabel: { color: '#64748b', fontSize: 11 } },
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
        dataZoom: [
          ...buildCategoryDataZoom('x', xCategories.length > 10 || yGroups.length > 8),
          ...buildCategoryDataZoom('y', yGroups.length > 10),
        ],
        series: [{ type: 'heatmap', data: heatData, label: { show: false } }],
      }
    }

    const groupedData = new Map<string, number[]>()
    yGroups.forEach(group => groupedData.set(group, Array(xCategories.length).fill(0)))
    rows.forEach(row => {
      const x = normalizeDimensionLabel(row[categoryCol], categoryCol)
      const y = normalizeDimensionLabel(row[secondDimensionCol], secondDimensionCol)
      const value = Number(row[mainMeasure] ?? 0)
      const xIndex = xCategories.indexOf(x)
      if (xIndex < 0 || !Number.isFinite(value)) return
      groupedData.get(y)![xIndex] = value
    })

    return {
      backgroundColor: 'transparent',
      title: {
        text: `Biểu diễn ${thirdDimensionCol ? '3D' : '2D'} theo ${mainMeasure}`,
        left: 12,
        top: 8,
        textStyle: { fontSize: 12, fontWeight: 600, color: '#475569' },
      },
      tooltip: { trigger: 'axis' },
      legend: { type: 'scroll', bottom: 0, textStyle: { color: '#94a3b8' } },
      grid: { top: 36, right: 20, bottom: 60, left: 80, containLabel: true },
      xAxis: preferHorizontalBar.value
        ? {
            type: 'value',
            axisLabel: {
              color: '#64748b',
              formatter: (val: number) => val.toLocaleString('vi-VN'),
            },
          }
        : { type: 'category', data: xCategories, axisLabel: { color: '#64748b', fontSize: 11 } },
      yAxis: preferHorizontalBar.value
        ? { type: 'category', data: xCategories, axisLabel: { color: '#64748b', fontSize: 11 } }
        : {
        type: 'value',
        axisLabel: {
          color: '#64748b',
          formatter: (val: number) => val.toLocaleString('vi-VN'),
        },
      },
      series: yGroups.map((group, i) => ({
        name: group,
        type: autoChartType.value === 'line' ? 'line' : 'bar',
        data: groupedData.get(group) ?? [],
        itemStyle: { color: palette[i % palette.length] },
        smooth: autoChartType.value === 'line',
      })),
      dataZoom: buildCategoryDataZoom(
        preferHorizontalBar.value ? 'y' : 'x',
        xCategories.length > 10 || yGroups.length > 8 || shouldEnableDetailZoom
      ),
    }
  }

  const series = displayMeasureCols.map((col, i) => {
    const chartType = resolveSeriesType(col)
    const isArea = autoChartType.value === 'area'
    const isStacked = autoChartType.value === 'stacked'
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
      areaStyle: chartType === 'line' && (isArea || autoChartType.value === 'combo')
        ? { color: palette[i % palette.length], opacity: isArea ? 0.18 : 0.08 }
        : undefined,
    }
  })

  // Pie/Donut: suitable for comparing category contributions on a selected measure.
  if (autoChartType.value === 'pie' || autoChartType.value === 'donut') {
    const pieMeasure = displayMeasureCols[0] ?? measureCols[0]
    if (!pieMeasure) return {}
    const pieValues = rows
      .map((r, idx) => ({
        name: categories[idx] ?? String(r[categoryCol] ?? ''),
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
          radius: autoChartType.value === 'donut' ? ['42%', '68%'] : '66%',
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
  if (autoChartType.value === 'heatmap') {
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

  if (preferHorizontalBar.value) {
    return {
      backgroundColor: 'transparent',
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' },
      },
      legend: {
        type: 'scroll',
        textStyle: { color: '#94a3b8' },
        bottom: 0,
      },
      grid: { top: 20, right: 20, bottom: 60, left: 120, containLabel: true },
      xAxis: {
        type: 'value',
        axisLabel: {
          color: '#64748b',
          formatter: (val: number) =>
            val >= 1_000_000 ? (val / 1_000_000).toFixed(1) + 'Tr' : val.toLocaleString('vi-VN'),
        },
        splitLine: { lineStyle: { color: '#1e293b' } },
      },
      yAxis: {
        type: 'category',
        data: categories,
        axisLine: { lineStyle: { color: '#334155' } },
        axisLabel: { color: '#64748b', fontSize: 11 },
      },
      dataZoom: buildCategoryDataZoom('y', shouldEnableDetailZoom || categories.length > 10),
      series: displayMeasureCols.map((col, i) => ({
        name: col,
        type: 'bar',
        data: rows.map(r => {
          const v = r[col]
          return v === null || v === undefined ? null : Number(v)
        }),
        itemStyle: { color: palette[i % palette.length] },
      })),
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
    dataZoom: buildCategoryDataZoom('x', shouldEnableDetailZoom || categories.length > 14),
    series,
  }
})

function extractMeasureFromSeriesName(seriesName: string): string {
  const raw = String(seriesName ?? '').trim()
  if (!raw) return ''
  const match = raw.match(/\[MEASURES\]\.\[([^\]]+)\]/i)
  if (match?.[1]) return match[1].trim()
  const normalized = raw.replace(/\[|\]/g, '').trim()
  const knownMeasures = store.selectedCubeInfo?.Measures ?? []
  return knownMeasures.includes(normalized) ? normalized : ''
}

type ChartClickParams = {
  name?: string
  dataIndex?: number
  seriesName?: string
  componentType?: string
}

async function onChartClick(params: ChartClickParams) {
  const clickedMeasure = extractMeasureFromSeriesName(String(params?.seriesName ?? ''))
  if (clickedMeasure) {
    // Clicking a series/legend first should lock the query scope to that measure.
    store.setSelectedMeasure(clickedMeasure)
    store.includeSoLuong = false
  }

  // Drill only when user clicks a concrete data point carrying category context.
  if (params?.componentType !== 'series' || typeof params?.dataIndex !== 'number') return
  const member = String(params?.name ?? '').trim()
  if (!member || store.isLoading) return
  const row = typeof params?.dataIndex === 'number'
    ? result.value?.Rows?.[params.dataIndex]
    : undefined
  const chartRows = result.value?.Rows ?? []
  const chartCols = result.value?.Columns ?? []
  const chartMeasureCols = chartCols.filter(col => isMeasureColumn(col, chartRows))
  const chartDimensionCols = chartCols.filter(col => !chartMeasureCols.includes(col))
  const rowHints = row
    ? chartDimensionCols
        .map(col => String(row[col] ?? '').trim())
        .filter(Boolean)
    : []
  store.setContextByMember(member, rowHints)
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
