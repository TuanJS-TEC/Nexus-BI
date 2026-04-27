<template>
  <div class="chart-container glass-card">
    <Toolbar class="chart-toolbar">
      <template #start>
        <div class="chart-title-wrap">
          <h3 class="chart-title">Biểu Đồ</h3>
          <Tag :value="`Tự động: ${autoChartLabel}`" severity="info" rounded />
          <Tag :value="projectionLabel" severity="secondary" rounded />
        </div>
      </template>
      <template #end>
        <div class="chart-actions">
          <Button
            icon="pi pi-search-minus"
            label="Reset Zoom"
            text
            size="small"
            :disabled="!hasData || store.isLoading"
            @click="resetZoom"
          />
          <Button
            icon="pi pi-image"
            label="Export PNG"
            outlined
            size="small"
            :disabled="!hasData || store.isLoading"
            @click="exportChartAsPng"
          />
        </div>
      </template>
    </Toolbar>

    <div class="chart-loading" v-if="store.isLoading">
      <ProgressSpinner strokeWidth="4" animationDuration=".9s" aria-label="Đang tải biểu đồ" />
      <span class="loading-text">Đang dựng biểu đồ từ dữ liệu OLAP...</span>
    </div>

    <div class="chart-empty" v-else-if="!hasData">
      <Message severity="warn" :closable="false">Chưa có dữ liệu để hiển thị biểu đồ</Message>
    </div>

    <v-chart
      v-else
      ref="chartRef"
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
import { BarChart, PieChart, HeatmapChart } from 'echarts/charts'
import 'echarts-gl'
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  DataZoomComponent,
  VisualMapComponent,
} from 'echarts/components'
import VChart from 'vue-echarts'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Tag from 'primevue/tag'
import Toolbar from 'primevue/toolbar'
import { useOlapStore } from '@/stores/olapStore'

use([
  CanvasRenderer,
  BarChart,
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
const chartRef = ref<InstanceType<typeof VChart> | null>(null)
type ChartType = 'combo' | 'bar' | 'hbar' | 'stacked' | 'pie' | 'donut' | 'heatmap' | 'bar3d'

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

function isTimeDimensionActive(): boolean {
  return store.activeDimension === 'ThoiGian'
}

function isTemporalLevel(level: string | undefined): boolean {
  return ['nam', 'quy', 'thang'].includes(String(level ?? '').toLowerCase())
}

function isNumericLike(value: unknown): boolean {
  return toNumericValue(value) !== null
}

function shouldUseDualAxis(measures: string[]): boolean {
  if (measures.length < 2) return false
  const hasRateLike = measures.some(m => /ty\s*le|%|rate|ratio/i.test(m))
  const hasQuantityLike = measures.some(m => /so\s*luong|qty|count|sl/i.test(m))
  const hasMoneyLike = measures.some(m => /tong\s*tien|doanh\s*thu|gia\s*tri|amount|revenue|sales/i.test(m))
  return (hasRateLike && hasQuantityLike) || (hasMoneyLike && hasQuantityLike)
}

function inferCubeDimensionCount(): number {
  const fromMeta = store.selectedCubeInfo?.DimensionCount
  if (typeof fromMeta === 'number' && Number.isFinite(fromMeta) && fromMeta > 0) return fromMeta
  const cube = String(store.selectedCube ?? '')
  const match = cube.match(/_(\d)D_/i)
  return match ? Number(match[1]) : 0
}

const autoChartType = computed<ChartType>(() => {
  const r = result.value
  if (!r?.Success || !r.Rows.length) return 'bar'

  const measureCols = r.Columns.filter(col => isMeasureColumn(col, r.Rows))
  const dimensionCols = r.Columns.filter(col => !measureCols.includes(col))
  const dimensionDepth = dimensionCols.length
  const categoryCol = pickCategoryColumn(dimensionCols, store.currentLevel) ?? dimensionCols[0] ?? r.Columns[0]
  const categories = r.Rows.map(row => String(row[categoryCol] ?? '').trim()).filter(Boolean)
  const categoryCount = categories.length
  const temporalX = isTimeDimensionActive() && isTemporalLevel(store.currentLevel)
  const op = String(store.lastOperation ?? '')
  const cubeDimensionCount = inferCubeDimensionCount()

  // Business rule: inventory + location should stay grouped bar in demo flow.
  if (isInventoryLocationScenario()) return 'bar'
  if (op === 'Pivot' && dimensionDepth >= 2) return 'heatmap'
  // Hard rule from product requirement:
  // only cubes with 3D metadata/name are allowed to render 3D charts.
  if (cubeDimensionCount >= 3 && dimensionDepth >= 3) return 'bar3d'
  if (op === 'SliceDice' && measureCols.length === 1 && categoryCount >= 2 && categoryCount <= 5) return 'donut'
  if (dimensionDepth === 2) return 'bar'

  if (temporalX) {
    if (op === 'DrillDown' || op === 'RollUp') return 'bar'
    if (shouldUseDualAxis(measureCols)) return 'combo'
    return 'bar'
  }

  // Rule: long labels / many categories should use horizontal bar.
  if (categoryCount >= 10 || isCustomerDimensionActive()) return 'hbar'
  return 'bar'
})

const autoChartLabel = computed(() => {
  const map: Record<ChartType, string> = {
    combo: 'Cột kép',
    bar: 'Cột',
    hbar: 'Cột ngang',
    stacked: 'Chồng lớp',
    pie: 'Tròn',
    donut: 'Donut',
    heatmap: 'Heatmap',
    bar3d: 'Bar 3D',
  }
  return map[autoChartType.value] ?? 'Cột'
})

const projectionLabel = computed(() => {
  const cubeDim = store.selectedCubeInfo?.DimensionCount ?? 0
  const rows = result.value?.Rows ?? []
  const cols = result.value?.Columns ?? []
  if (!rows.length || !cols.length) return `Cube ${cubeDim}D`
  const measureCols = cols.filter(col => isMeasureColumn(col, rows))
  const rawQueryDim = Math.max(0, cols.length - measureCols.length)
  const queryDim = cubeDim > 0 ? Math.min(rawQueryDim, cubeDim) : rawQueryDim
  return `Cube ${cubeDim}D • Query ${queryDim}D`
})

const preferHorizontalBar = computed(() =>
  autoChartType.value === 'hbar'
)

const isGroupedInventoryLocation = computed(() =>
  isInventoryLocationScenario() && autoChartType.value === 'bar'
)

function isMeasureColumn(columnName: string, rows: Array<Record<string, unknown>>): boolean {
  const normalizedCol = String(columnName ?? '').replace(/\[|\]/g, '').trim().toLowerCase()
  const knownMeasures = (store.selectedCubeInfo?.Measures ?? [])
    .map(m => String(m ?? '').replace(/\[|\]/g, '').trim().toLowerCase())
  if (knownMeasures.some(m => normalizedCol.includes(m) || m.includes(normalizedCol))) return true
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

function isHiddenDimensionLabel(label: string): boolean {
  const normalized = String(label ?? '').trim().toLowerCase()
  return normalized === 'n/a'
    || normalized === 'na'
    || normalized === '(blank)'
    || normalized === 'null'
    || normalized === 'undefined'
}

function collectNumericValues(rows: Array<Record<string, unknown>>, measureCols: string[]): number[] {
  const values: number[] = []
  rows.forEach((row) => {
    measureCols.forEach((measure) => {
      const n = toNumericValue(row[measure])
      if (n !== null) values.push(n)
    })
  })
  return values
}

function toNumericValue(value: unknown): number | null {
  if (value === null || value === undefined) return null
  if (typeof value === 'number') return Number.isFinite(value) ? value : null
  const raw = String(value).trim()
  if (!raw) return null
  const normalized = raw
    .replace(/\s+/g, '')
    .replace(/,(?=\d{3}\b)/g, '')
    .replace(/\.(?=\d{3}\b)/g, '')
    .replace(',', '.')
  const n = Number(normalized)
  return Number.isFinite(n) ? n : null
}

function toNumericDimensionValue(value: unknown): number | null {
  const direct = toNumericValue(value)
  if (direct !== null) return direct
  const raw = String(value ?? '').trim()
  if (!raw) return null
  const cleaned = raw.replace(/[^\d,.\-]/g, '')
  return toNumericValue(cleaned)
}

function formatCompactVi(value: number, fractionDigits = 1): string {
  const abs = Math.abs(value)
  if (abs >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(fractionDigits)} Tỉ`
  if (abs >= 1_000_000) return `${(value / 1_000_000).toFixed(fractionDigits)} Tr`
  return value.toLocaleString('vi-VN')
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

function gradientColorByIndex(index: number, palette: string[]) {
  const from = palette[index % palette.length]
  const to = palette[(index + 1) % palette.length]
  return {
    type: 'linear',
    x: 0,
    y: 0,
    x2: 0,
    y2: 1,
    colorStops: [
      { offset: 0, color: from },
      { offset: 1, color: to },
    ],
  }
}

function normalizeMeasureKey(name: string): string {
  return String(name ?? '')
    .replace(/\[MEASURES\]\.\[([^\]]+)\]/i, '$1')
    .replace(/\[|\]/g, '')
    .trim()
    .toLowerCase()
}

function topCategoriesByMagnitude(
  rows: Array<Record<string, unknown>>,
  column: string,
  measure: string,
  limit: number
): string[] {
  const totals = new Map<string, number>()
  rows.forEach((row) => {
    const key = normalizeDimensionLabel(row[column], column)
    const value = toNumericValue(row[measure])
    if (!key || value === null) return
    totals.set(key, (totals.get(key) ?? 0) + Math.abs(value))
  })
  return Array.from(totals.entries())
    .sort((a, b) => b[1] - a[1])
    .slice(0, limit)
    .map(item => item[0])
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

  const sourceRows = result.value.Rows
  const cols = result.value.Columns
  const measureCols = cols.filter(col => isMeasureColumn(col, sourceRows))
  const dimensionCols = cols.filter(col => !measureCols.includes(col))
  const rows = sourceRows.filter((row) =>
    !dimensionCols.some((dimensionCol) => {
      const label = normalizeDimensionLabel(row[dimensionCol], dimensionCol)
      return isHiddenDimensionLabel(label)
    })
  )
  if (!rows.length) return {}
  const categoryCol = pickCategoryColumn(dimensionCols, store.currentLevel)
    ?? dimensionCols[0]
    ?? cols[0]
  const secondDimensionCol = dimensionCols.find(col => col !== categoryCol)
  const thirdDimensionCol = dimensionCols.find(col => col !== categoryCol && col !== secondDimensionCol)

  const categoriesRaw = rows.map(r => String(r[categoryCol] ?? '').trim())
  const categories = categoriesRaw.map((value, index) =>
    formatLevelLabel(value || `Member ${index + 1}`, store.currentLevel)
  )
  const hasQuantitySeries = measureCols.some(col => col.toLowerCase().includes('so luong'))
  const maxVisibleSeries = autoChartType.value === 'stacked'
      ? 4
      : 10

  const palette = ['#003f5b', '#2b4b7d', '#5f5195', '#98509d', '#cc4c91', '#f25375', '#ff6f4e', '#ff9913']
  const palette3D = ['#5918b3', '#8021ac', '#a52a9e', '#c53589', '#df4671', '#f25e55', '#fd7a36', '#ff9913']
  const selectedMeasureKey = normalizeMeasureKey(store.selectedMeasure ?? '')

  const resolveSeriesType = (_columnName: string): 'bar' => {
    if (autoChartType.value === 'bar' || autoChartType.value === 'hbar') return 'bar'
    if (autoChartType.value === 'stacked') return 'bar'
    return 'bar'
  }

  const rankedMeasureCols = [...measureCols]
    .map(col => ({
      col,
      isSelected: selectedMeasureKey
        ? normalizeMeasureKey(col) === selectedMeasureKey
        : false,
      score: rows.reduce((sum, row) => {
        const n = toNumericValue(row[col])
        return n !== null ? sum + Math.abs(n) : sum
      }, 0),
    }))
    .sort((a, b) => {
      if (a.isSelected && !b.isSelected) return -1
      if (!a.isSelected && b.isSelected) return 1
      return b.score - a.score
    })

  const displayMeasureCols = rankedMeasureCols.slice(0, maxVisibleSeries).map(item => item.col)

  const hiddenSeriesCount = Math.max(0, measureCols.length - displayMeasureCols.length)
  const valueSpread = computeSpreadRatio(collectNumericValues(rows, displayMeasureCols.length ? displayMeasureCols : measureCols))
  const shouldEnableDetailZoom = categories.length > 12 || hiddenSeriesCount > 0 || valueSpread >= 25
  const useDualAxis = autoChartType.value === 'combo'
    && shouldUseDualAxis(displayMeasureCols)
    && hasQuantitySeries

  // Multi-dimensional mode: represent 2D/3D explicitly when row-set includes >= 2 dimensions.
  if (categoryCol && secondDimensionCol) {
    const mainMeasure = displayMeasureCols[0] ?? measureCols[0]
    if (!mainMeasure) return {}
    const xCategories = [...new Set(rows.map(r => normalizeDimensionLabel(r[categoryCol], categoryCol)))]
    const yGroups = [...new Set(rows.map(r => normalizeDimensionLabel(r[secondDimensionCol], secondDimensionCol)))]
    const matrixCells = xCategories.length * yGroups.length
    const highCardinalityMatrix = yGroups.length > 6 || xCategories.length > 12 || matrixCells > 48
    const matrixValues = rows
      .map(row => toNumericValue(row[mainMeasure]))
      .filter((v): v is number => v !== null)
    const isSkewed2D = computeSpreadRatio(matrixValues) >= 25
    const numericYRatio = yGroups.length
      ? yGroups.filter(item => isNumericLike(item)).length / yGroups.length
      : 0
    const xNumericRatio = xCategories.length
      ? xCategories.filter(item => isNumericLike(item)).length / xCategories.length
      : 0
    const yIsContinuousNumeric = numericYRatio >= 0.8
      && yGroups.length >= 12
      && !/\[nam\]|\[quy\]|\[thang\]|year|quarter|month/i.test(secondDimensionCol)
    const xIsContinuousNumeric = xNumericRatio >= 0.8
      && xCategories.length >= 12
      && !/\[nam\]|\[quy\]|\[thang\]|year|quarter|month/i.test(categoryCol)
    const xIsTemporalAxis = /\[nam\]|\[quy\]|\[thang\]|year|quarter|month/i.test(categoryCol)

    if (thirdDimensionCol && autoChartType.value === 'bar3d') {
      const xTop = topCategoriesByMagnitude(rows, categoryCol, mainMeasure, 10)
      const yTop = topCategoriesByMagnitude(rows, secondDimensionCol, mainMeasure, 8)
      const zTop = topCategoriesByMagnitude(rows, thirdDimensionCol, mainMeasure, 6)

      const limitedRows = rows.filter((row) => {
        const x = normalizeDimensionLabel(row[categoryCol], categoryCol)
        const y = normalizeDimensionLabel(row[secondDimensionCol], secondDimensionCol)
        const z = normalizeDimensionLabel(row[thirdDimensionCol], thirdDimensionCol)
        return xTop.includes(x) && yTop.includes(y) && zTop.includes(z)
      })

      const xLimited = [...new Set(limitedRows.map(r => normalizeDimensionLabel(r[categoryCol], categoryCol)))]
      const yLimited = [...new Set(limitedRows.map(r => normalizeDimensionLabel(r[secondDimensionCol], secondDimensionCol)))]
      const zLimited = [...new Set(limitedRows.map(r => normalizeDimensionLabel(r[thirdDimensionCol], thirdDimensionCol)))]

      const xIndex = new Map(xLimited.map((item, idx) => [item, idx]))
      const yIndex = new Map(yLimited.map((item, idx) => [item, idx]))
      const zIndex = new Map(zLimited.map((item, idx) => [item, idx]))
      const points: Array<[number, number, number, number]> = []
      let min = Number.POSITIVE_INFINITY
      let max = Number.NEGATIVE_INFINITY

      limitedRows.forEach((row) => {
        const x = normalizeDimensionLabel(row[categoryCol], categoryCol)
        const y = normalizeDimensionLabel(row[secondDimensionCol], secondDimensionCol)
        const z = normalizeDimensionLabel(row[thirdDimensionCol], thirdDimensionCol)
        const value = toNumericValue(row[mainMeasure])
        if (value === null) return
        const xi = xIndex.get(x)
        const yi = yIndex.get(y)
        const zi = zIndex.get(z)
        if (xi === undefined || yi === undefined || zi === undefined) return
        min = Math.min(min, value)
        max = Math.max(max, value)
        points.push([xi, yi, zi, value])
      })

      const thirdValues = limitedRows
        .map(row => toNumericValue(row[thirdDimensionCol]))
        .filter((v): v is number => v !== null)
      const thirdNumericRatio = limitedRows.length
        ? thirdValues.length / limitedRows.length
        : 0
      const thirdIsContinuousNumeric = thirdNumericRatio >= 0.7
        || /gia|price|amount|value|cost/i.test(thirdDimensionCol)

      if (thirdIsContinuousNumeric) {
        type ScatterPoint = [number, number, number, number]
        const scatterPoints: ScatterPoint[] = []
        let zMin = Number.POSITIVE_INFINITY
        let zMax = Number.NEGATIVE_INFINITY
        let vMin = Number.POSITIVE_INFINITY
        let vMax = Number.NEGATIVE_INFINITY

        limitedRows.forEach((row) => {
          const x = normalizeDimensionLabel(row[categoryCol], categoryCol)
          const y = normalizeDimensionLabel(row[secondDimensionCol], secondDimensionCol)
          const z = toNumericValue(row[thirdDimensionCol])
          const value = toNumericValue(row[mainMeasure])
          if (z === null || value === null) return
          const xi = xIndex.get(x)
          const yi = yIndex.get(y)
          if (xi === undefined || yi === undefined) return
          zMin = Math.min(zMin, z)
          zMax = Math.max(zMax, z)
          vMin = Math.min(vMin, value)
          vMax = Math.max(vMax, value)
          scatterPoints.push([xi, yi, z, value])
        })

        const valueSpan = Number.isFinite(vMax - vMin) && vMax > vMin ? (vMax - vMin) : 1

        return {
          backgroundColor: 'transparent',
          tooltip: {
            formatter: (params: { data: ScatterPoint }) => {
              const [x, y, z, v] = params.data
              return `${xLimited[x]}<br/>${yLimited[y]}<br/>${thirdDimensionCol}: ${z.toLocaleString('vi-VN')}<br/><strong>${mainMeasure}: ${v.toLocaleString('vi-VN')}</strong>`
            },
          },
          title: {
            text: `3D Scatter (Top ${xLimited.length} x ${yLimited.length})`,
            subtext: 'Kéo để xoay, cuộn để zoom. Màu + kích thước điểm biểu diễn mức độ measure.',
            left: 12,
            top: 8,
            textStyle: { fontSize: 12, fontWeight: 600, color: '#475569' },
            subtextStyle: { color: '#64748b', fontSize: 11 },
          },
          visualMap: {
            min: Number.isFinite(vMin) ? vMin : 0,
            max: Number.isFinite(vMax) ? vMax : 0,
            calculable: true,
            inRange: { color: palette3D },
            textStyle: { color: '#64748b' },
            left: 12,
            top: 40,
          },
          xAxis3D: { type: 'category', data: xLimited, axisLabel: { interval: 0 } },
          yAxis3D: { type: 'category', data: yLimited, axisLabel: { interval: 0 } },
          zAxis3D: {
            type: 'value',
            min: Number.isFinite(zMin) ? zMin : undefined,
            max: Number.isFinite(zMax) ? zMax : undefined,
            axisLabel: { formatter: (val: number) => formatCompactVi(val) },
          },
          grid3D: {
            boxWidth: 200,
            boxDepth: 145,
            boxHeight: 120,
            light: { main: { intensity: 1.1 }, ambient: { intensity: 0.52 } },
            viewControl: {
              projection: 'perspective',
              autoRotate: false,
              alpha: 26,
              beta: 34,
              distance: 300,
              rotateSensitivity: 1.4,
              zoomSensitivity: 1.2,
              panSensitivity: 1,
            },
          },
          series: [
            {
              type: 'scatter3D',
              data: scatterPoints,
              encode: { x: 0, y: 1, z: 2, value: 3 },
              symbolSize: (data: ScatterPoint) => {
                const value = data?.[3] ?? 0
                const normalized = Math.max(0, Math.min(1, (value - vMin) / valueSpan))
                return 8 + normalized * 16
              },
              itemStyle: { opacity: 0.92 },
              emphasis: { itemStyle: { color: '#f59e0b' } },
            },
          ],
        }
      }

      return {
        backgroundColor: 'transparent',
        tooltip: {
          formatter: (params: { data: [number, number, number, number] }) => {
            const [x, y, z, v] = params.data
            return `${xLimited[x]}<br/>${yLimited[y]}<br/>${zLimited[z]}<br/><strong>${mainMeasure}: ${v.toLocaleString('vi-VN')}</strong>`
          },
        },
        title: {
          text: `3D (Top ${xLimited.length} x ${yLimited.length} x ${zLimited.length})`,
          subtext: 'Kéo để xoay, cuộn để zoom. Màu sắc biểu diễn mức độ measure.',
          left: 12,
          top: 8,
          textStyle: { fontSize: 12, fontWeight: 600, color: '#475569' },
          subtextStyle: { color: '#64748b', fontSize: 11 },
        },
        visualMap: {
          min: Number.isFinite(min) ? min : 0,
          max: Number.isFinite(max) ? max : 0,
          calculable: true,
          inRange: { color: palette3D },
          textStyle: { color: '#64748b' },
          left: 12,
          top: 40,
        },
        xAxis3D: { type: 'category', data: xLimited, axisLabel: { interval: 0 } },
        yAxis3D: { type: 'category', data: yLimited, axisLabel: { interval: 0 } },
        zAxis3D: { type: 'category', data: zLimited, axisLabel: { interval: 0 } },
        grid3D: {
          boxWidth: 180,
          boxDepth: 130,
          boxHeight: 110,
          light: { main: { intensity: 1.1 }, ambient: { intensity: 0.5 } },
          viewControl: {
            projection: 'perspective',
            autoRotate: false,
            alpha: 28,
            beta: 32,
            distance: 320,
            rotateSensitivity: 1.5,
            zoomSensitivity: 1.2,
            panSensitivity: 1,
          },
        },
        series: [
          {
            type: 'bar3D',
            shading: 'lambert',
            data: points,
            encode: { x: 0, y: 1, z: 2, value: 3 },
            itemStyle: { opacity: 0.95 },
            bevelSize: 0.2,
            minHeight: 0.4,
            emphasis: { itemStyle: { color: '#f59e0b' } },
          },
        ],
      }
    }

    // Mixed 2D (category + continuous numeric) should use scatter instead of dense heatmap block.
    const hasSingleContinuousAxis = (xIsContinuousNumeric || yIsContinuousNumeric) && !(xIsContinuousNumeric && yIsContinuousNumeric)
    if (hasSingleContinuousAxis) {
      const categoryDimensionCol = xIsContinuousNumeric ? secondDimensionCol : categoryCol
      const numericDimensionCol = xIsContinuousNumeric ? categoryCol : secondDimensionCol
      const categoryGroups = [...new Set(rows.map(r => normalizeDimensionLabel(r[categoryDimensionCol], categoryDimensionCol)))]
      const categoryIndex = new Map(categoryGroups.map((item, idx) => [item, idx]))
      type Scatter2DPoint = [number, number, number]
      const scatterData: Scatter2DPoint[] = []
      let minMeasure = Number.POSITIVE_INFINITY
      let maxMeasure = Number.NEGATIVE_INFINITY

      rows.forEach((row) => {
        const category = normalizeDimensionLabel(row[categoryDimensionCol], categoryDimensionCol)
        const numeric = toNumericDimensionValue(row[numericDimensionCol])
        const value = toNumericValue(row[mainMeasure])
        if (numeric === null || value === null) return
        const ci = categoryIndex.get(category)
        if (ci === undefined) return
        minMeasure = Math.min(minMeasure, value)
        maxMeasure = Math.max(maxMeasure, value)
        scatterData.push([ci, numeric, value])
      })

      if (!scatterData.length) {
        // If numeric parsing fails for this shape, continue to legacy 2D renderer.
      } else {
        const valueSpan = Number.isFinite(maxMeasure - minMeasure) && maxMeasure > minMeasure ? (maxMeasure - minMeasure) : 1

        return {
          backgroundColor: 'transparent',
          title: {
            text: `Phân tán 2D theo ${mainMeasure}`,
            subtext: 'Màu + kích thước điểm biểu diễn mức độ measure.',
            left: 12,
            top: 8,
            textStyle: { fontSize: 12, fontWeight: 600, color: '#475569' },
            subtextStyle: { color: '#64748b', fontSize: 11 },
          },
          tooltip: {
            formatter: (params: { data: Scatter2DPoint }) => {
              const [c, n, v] = params.data
              return `${categoryGroups[c]}<br/>${numericDimensionCol}: ${formatCompactVi(n)}<br/><strong>${mainMeasure}: ${v.toLocaleString('vi-VN')}</strong>`
            },
          },
          grid: { top: 48, right: 18, bottom: 54, left: 90, containLabel: true },
          xAxis: {
            type: 'category',
            data: categoryGroups,
            axisLabel: { color: '#64748b', fontSize: 11, rotate: categoryGroups.length > 8 ? 18 : 0 },
          },
          yAxis: {
            type: 'value',
            axisLabel: { color: '#64748b', formatter: (val: number) => formatCompactVi(val) },
          },
          visualMap: {
            min: Number.isFinite(minMeasure) ? minMeasure : 0,
            max: Number.isFinite(maxMeasure) ? maxMeasure : 0,
            calculable: true,
            orient: 'horizontal',
            left: 'center',
            bottom: -2,
            textStyle: { color: '#64748b' },
            inRange: { color: palette },
          },
          dataZoom: [
            ...buildCategoryDataZoom('x', categoryGroups.length > 10),
            { type: 'inside', yAxisIndex: 0, filterMode: 'none' },
          ],
          series: [
            {
              type: 'scatter',
              data: scatterData,
              symbolSize: (data: Scatter2DPoint) => {
                const value = data?.[2] ?? 0
                const normalized = Math.max(0, Math.min(1, (value - minMeasure) / valueSpan))
                return 8 + normalized * 14
              },
              itemStyle: { opacity: 0.9 },
              encode: { x: 0, y: 1, value: 2 },
            },
          ],
        }
      }
    }

    // Dense matrix with continuous numeric Y (e.g. Gia) is hard to read as heatmap.
    // Aggregate by temporal X to keep chart readable and clickable.
    if (yIsContinuousNumeric && xIsTemporalAxis) {
      const aggregatedByX = new Map<string, number>()
      rows.forEach((row) => {
        const x = normalizeDimensionLabel(row[categoryCol], categoryCol)
        const value = toNumericValue(row[mainMeasure])
        if (!x || value === null) return
        aggregatedByX.set(x, (aggregatedByX.get(x) ?? 0) + value)
      })
      const aggCategories = Array.from(aggregatedByX.keys())
      const aggValues = aggCategories.map(cat => aggregatedByX.get(cat) ?? 0)

      return {
        backgroundColor: 'transparent',
        title: {
          text: `Tổng hợp ${mainMeasure} theo ${levelMapFromColumn(categoryCol)}`,
          left: 12,
          top: 8,
          textStyle: { fontSize: 12, fontWeight: 600, color: '#475569' },
        },
        tooltip: {
          trigger: 'axis',
          axisPointer: { type: 'shadow' },
        },
        grid: { top: 36, right: 20, bottom: 52, left: 80, containLabel: true },
        xAxis: {
          type: 'category',
          data: aggCategories,
          axisLabel: { color: '#64748b', fontSize: 11, rotate: aggCategories.length > 10 ? 25 : 0 },
        },
        yAxis: {
          type: 'value',
          min: 0,
          axisLabel: { color: '#64748b', formatter: (val: number) => formatCompactVi(val) },
        },
        dataZoom: buildCategoryDataZoom('x', aggCategories.length > 12),
        series: [
          {
            name: mainMeasure,
            type: 'bar',
            data: aggValues,
            itemStyle: {
              color: (_: unknown) => gradientColorByIndex(0, palette),
            },
            barMaxWidth: 42,
          },
        ],
      }
    }

    if ((autoChartType.value === 'heatmap' || highCardinalityMatrix || isSkewed2D) && !isGroupedInventoryLocation.value) {
      const matrix = new Map<string, number>()
      rows.forEach(row => {
        const x = normalizeDimensionLabel(row[categoryCol], categoryCol)
        const keyY = normalizeDimensionLabel(row[secondDimensionCol], secondDimensionCol)
        const value = toNumericValue(row[mainMeasure])
        if (value !== null) matrix.set(`${x}__${keyY}`, value)
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
          text: 'Biểu diễn heatmap 2D',
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
        xAxis: {
          type: 'category',
          data: xCategories,
          axisLabel: { color: '#64748b', fontSize: 11, rotate: xCategories.length > 8 ? 25 : 0 },
        },
        yAxis: { type: 'category', data: yGroups, axisLabel: { color: '#64748b', fontSize: 11 } },
        visualMap: {
          min: Number.isFinite(min) ? min : 0,
          max: Number.isFinite(max) ? max : 0,
          calculable: true,
          orient: 'horizontal',
          left: 'center',
          bottom: -2,
          textStyle: { color: '#64748b' },
          inRange: { color: palette },
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
      const value = toNumericValue(row[mainMeasure])
      const xIndex = xCategories.indexOf(x)
      if (xIndex < 0 || value === null) return
      groupedData.get(y)![xIndex] = value
    })

    return {
      backgroundColor: 'transparent',
      title: {
        text: `Biểu diễn 2D theo ${mainMeasure}`,
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
              formatter: (val: number) => formatCompactVi(val),
            },
          }
        : { type: 'category', data: xCategories, axisLabel: { color: '#64748b', fontSize: 11 } },
      yAxis: preferHorizontalBar.value
        ? { type: 'category', data: xCategories, axisLabel: { color: '#64748b', fontSize: 11 } }
        : {
        type: 'value',
        axisLabel: {
          color: '#64748b',
          formatter: (val: number) => formatCompactVi(val),
        },
      },
      series: yGroups.map((group, i) => ({
        name: group,
        type: 'bar',
        data: groupedData.get(group) ?? [],
        itemStyle: { color: gradientColorByIndex(i, palette) },
      })),
      dataZoom: buildCategoryDataZoom(
        preferHorizontalBar.value ? 'y' : 'x',
        xCategories.length > 10 || yGroups.length > 8 || shouldEnableDetailZoom
      ),
    }
  }

  const series = displayMeasureCols.map((col, i) => {
    const chartType = resolveSeriesType(col)
    const isStacked = autoChartType.value === 'stacked'
    return {
      name: col,
      type: chartType,
      stack: isStacked ? 'total' : undefined,
      yAxisIndex: useDualAxis && col.toLowerCase().includes('so luong') ? 1 : 0,
      data: rows.map(r => {
        return toNumericValue(r[col])
      }),
      itemStyle: displayMeasureCols.length === 1
        ? {
            color: (params: { dataIndex: number }) => gradientColorByIndex(params.dataIndex, palette),
          }
        : { color: gradientColorByIndex(i, palette) },
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
      .map(p => ({ ...p, value: toNumericValue(p.value) ?? 0 }))
      .filter(p => Number.isFinite(p.value))
      .sort((a, b) => b.value - a.value)

    const maxSlices = 5
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
        const value = toNumericValue(rows[rowIdx]?.[measure])
        if (value !== null) {
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
        inRange: { color: palette },
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
    const rankMeasure = displayMeasureCols[0]
    const indexedRows = rows.map((row, idx) => ({ row, label: categories[idx] ?? `Member ${idx + 1}` }))
    const sortedRows = rankMeasure
      ? [...indexedRows].sort((a, b) => (toNumericValue(b.row[rankMeasure]) ?? 0) - (toNumericValue(a.row[rankMeasure]) ?? 0))
      : indexedRows
    const limitedRows = sortedRows.slice(0, 10)
    const hCategories = limitedRows.map(item => item.label)

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
          formatter: (val: number) => formatCompactVi(val),
        },
        splitLine: { lineStyle: { color: '#1e293b' } },
      },
      yAxis: {
        type: 'category',
        data: hCategories,
        axisLine: { lineStyle: { color: '#334155' } },
        axisLabel: { color: '#64748b', fontSize: 11 },
      },
      dataZoom: buildCategoryDataZoom('y', shouldEnableDetailZoom || hCategories.length > 10),
      series: displayMeasureCols.map((col, i) => ({
        name: col,
        type: 'bar',
        data: limitedRows.map(item => {
          return toNumericValue(item.row[col])
        }),
        itemStyle: displayMeasureCols.length === 1
          ? {
              color: (params: { dataIndex: number }) => gradientColorByIndex(params.dataIndex, palette),
            }
          : { color: gradientColorByIndex(i, palette) },
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
            : formatCompactVi(item.value, 2)
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
      pageIconColor: palette[0],
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
        formatter: (val: number) => formatCompactVi(val),
      },
      splitLine: { lineStyle: { color: '#1e293b' } },
    },
    ...(useDualAxis
      ? {
          yAxis: [
            {
              type: 'value',
              axisLine: { lineStyle: { color: '#334155' } },
              axisLabel: {
                color: '#64748b',
                formatter: (val: number) => formatCompactVi(val),
              },
              splitLine: { lineStyle: { color: '#1e293b' } },
            },
            {
              type: 'value',
              position: 'right',
              axisLine: { lineStyle: { color: palette[2] } },
              axisLabel: {
                color: palette[1],
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

function levelMapFromColumn(column: string): string {
  if (/\[thang\]|month/i.test(column)) return 'Tháng'
  if (/\[quy\]|quarter/i.test(column)) return 'Quý'
  if (/\[nam\]|year/i.test(column)) return 'Năm'
  return 'Thời gian'
}

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

function resetZoom() {
  const instance = getChartInstance()
  if (!instance) return
  instance.dispatchAction({ type: 'dataZoom', start: 0, end: 100 })
}

function exportChartAsPng() {
  const instance = getChartInstance()
  if (!instance) return
  const dataUrl = instance.getDataURL({
    type: 'png',
    pixelRatio: 2,
    backgroundColor: '#ffffff',
  })
  const link = document.createElement('a')
  link.href = dataUrl
  link.download = `olap-chart-${Date.now()}.png`
  link.click()
}

function getChartInstance(): {
  dispatchAction: (payload: Record<string, unknown>) => void
  getDataURL: (options: Record<string, unknown>) => string
} | null {
  return ((chartRef.value as unknown as { chart?: unknown })?.chart as {
    dispatchAction: (payload: Record<string, unknown>) => void
    getDataURL: (options: Record<string, unknown>) => string
  } | undefined) ?? null
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

.chart-toolbar {
  border: none;
  border-bottom: 1px solid var(--border);
  border-radius: 0;
  background: transparent;
  padding: 0.8rem 1rem 0.65rem;
}

.chart-title-wrap {
  display: flex;
  align-items: center;
  gap: 0.65rem;
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

.chart-actions {
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
  height: 460px;
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
  padding: 0 1rem 1rem;
}

.chart-loading {
  height: 280px;
  padding: 0 1rem 1rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
}

.loading-text {
  font-size: 0.82rem;
  color: var(--text-muted);
}

@media (max-width: 768px) {
  .chart-toolbar {
    padding: 0.75rem;
  }
  .chart-title-wrap {
    width: 100%;
    justify-content: space-between;
  }
  .chart-actions {
    width: 100%;
    justify-content: flex-end;
  }
  .chart {
    height: 380px;
    padding: 0 0.75rem 0.75rem;
  }
}

@media (max-width: 480px) {
  .chart {
    height: 340px;
    padding: 0 0.5rem 0.5rem;
  }
}
</style>
