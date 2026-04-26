import { defineStore } from 'pinia'
import { ref, computed, watch } from 'vue'
import { olapApi } from '@/api/olap'
import type { OlapRequest, OlapResult, MetadataResult, CubeInfo } from '@/types/olap'

export type OlapLevel = 'Nam' | 'Quy' | 'Thang'
export type OperationType = '' | 'DrillDown' | 'RollUp' | 'Slice' | 'Dice' | 'Pivot' | 'DefaultQuery'
export type SafeOperationType = Exclude<OperationType, '' | 'DefaultQuery'>
export type RiskLevel = 'low' | 'medium' | 'high'

export interface OperationGuard {
  enabled: boolean
  reason?: string
  warnings: string[]
  requiresConfirm: boolean
  risk: RiskLevel
}

function parseCubeNameLocally(cubeName: string): CubeInfo {
  const tokens = cubeName.split('_')
  const hasProduct = tokens.includes('MH')
  const hasCustomer = tokens.includes('KH')
  const hasStore = tokens.includes('CH')
  const hasTime = tokens.includes('TG')
  const dimToken = tokens.find(t => t.endsWith('D'))
  const dimensionCount = dimToken ? Number(dimToken.replace('D', '')) || 0 : 0
  const fact = cubeName.includes('TonKho') ? 'Fact_TonKho' : 'Fact_BanHang'
  const dimensions = [
    ...(hasCustomer ? ['Khach Hang (KH)'] : []),
    ...(hasProduct ? ['Mat Hang (MH)'] : []),
    ...(hasStore ? ['Cua Hang (CH)'] : []),
    ...(hasTime ? ['Thoi Gian (TG)'] : []),
  ]
  const measures = fact === 'Fact_TonKho' ? ['So Luong Trong Kho'] : ['Tong Tien', 'So Luong Dat']
  return {
    Name: cubeName,
    Fact: fact,
    DimensionCount: dimensionCount,
    Dimensions: dimensions,
    Measures: measures,
    Description: `${fact} ${dimensionCount}D voi ${dimensions.join(', ') || 'khong co chieu'}.`,
    Capabilities: {
      HasTime: hasTime,
      HasProduct: hasProduct,
      HasCustomer: hasCustomer,
      HasStore: hasStore,
      AllowDrillDown: hasTime,
      AllowRollUp: hasTime,
      AllowSlice: hasTime,
      AllowDice: hasTime,
      AllowPivot: hasTime,
    },
  }
}

interface DrillContext {
  year?: number
  quarter?: number
  productKey?: string
  customerKey?: string
  storeKey?: string
}

function parseYear(text: string): number | undefined {
  const match = text.match(/\b(19|20)\d{2}\b/)
  if (!match) return undefined
  const y = Number(match[0])
  return Number.isFinite(y) ? y : undefined
}

function parseQuarter(text: string): number | undefined {
  const normalized = text.trim().toUpperCase()

  // Match common numeric formats: "Q1", "Quy 1", "Quarter 1", "&[1]"
  const numericMatch = normalized.match(/(?:\bQ(?:U?Y|UARTER)?\s*|&\[)([1-4])(?:\b|\])/)
  if (numericMatch) {
    const q = Number(numericMatch[1])
    if (Number.isFinite(q)) return q
  }

  // Match roman numeral formats: "Quy I", "Quarter IV"
  const romanMatch = normalized.match(/\b(?:Q(?:U?Y|UARTER)?)\s*(I{1,3}|IV)\b/)
  if (romanMatch) {
    const roman = romanMatch[1]
    if (roman === 'I') return 1
    if (roman === 'II') return 2
    if (roman === 'III') return 3
    if (roman === 'IV') return 4
  }

  return undefined
}

function extractMemberCode(text: string): string {
  const match = text.match(/([A-Z]{2}\d+)/i)
  return match?.[1]?.toUpperCase() ?? text.trim()
}

export const useOlapStore = defineStore('olap', () => {
  const currentLevel = ref<OlapLevel>('Nam')
  const rowAxis = ref('Nam')
  const colAxis = ref('Ma MH')
  const selectedCube = ref('Cube4BanHang_3D_KH_MH_TG_01')
  const selectedMeasure = ref('Tong Tien')
  const includeSoLuong = ref(true)

  const drillContext = ref<DrillContext>({})
  const selectedMemberLabel = ref<string>('')

  const resultData = ref<OlapResult | null>(null)
  const metadata = ref<MetadataResult | null>(null)
  const isLoading = ref(false)
  const lastOperation = ref<OperationType>('')
  const errorMessage = ref('')
  const pendingOperation = ref<SafeOperationType | null>(null)
  const pendingWarning = ref('')
  const pendingRisk = ref<RiskLevel>('low')
  const lastQueryMs = ref<number | null>(null)
  const lastResultAt = ref<Date | null>(null)
  const metadataLoadedAt = ref<Date | null>(null)
  const tableDensity = ref<'compact' | 'comfortable'>('compact')

  const canDrillDown = computed(() => currentLevel.value !== 'Thang')
  const canRollUp = computed(() => currentLevel.value !== 'Nam')
  const selectedCubeInfo = computed<CubeInfo>(() => {
    const fromApi = metadata.value?.CubeInfos?.find(c => c.Name === selectedCube.value)
    return fromApi ?? parseCubeNameLocally(selectedCube.value)
  })
  const cubeHasTime = computed(() => selectedCubeInfo.value.Capabilities.HasTime)
  const cubeHasProduct = computed(() => selectedCubeInfo.value.Capabilities.HasProduct)
  const cubeHasCustomer = computed(() => selectedCubeInfo.value.Capabilities.HasCustomer)
  const cubeHasStore = computed(() => selectedCubeInfo.value.Capabilities.HasStore)

  const availablePivotAxes = computed(() => {
    const list: string[] = []
    if (cubeHasProduct.value) list.push('Ma MH')
    if (cubeHasCustomer.value) list.push('Ma KH')
    if (cubeHasStore.value) list.push('Ma CH')
    return list
  })

  const canUseTimeOperations = computed(() => cubeHasTime.value)
  const hasDimensionFilter = computed(() =>
    !!drillContext.value.year ||
    !!drillContext.value.productKey ||
    !!drillContext.value.customerKey ||
    !!drillContext.value.storeKey
  )

  const operationGuards = computed<Record<SafeOperationType, OperationGuard>>(() => {
    const guards: Record<SafeOperationType, OperationGuard> = {
      DrillDown: { enabled: true, warnings: [], requiresConfirm: false, risk: 'low' },
      RollUp: { enabled: true, warnings: [], requiresConfirm: false, risk: 'low' },
      Slice: { enabled: true, warnings: [], requiresConfirm: false, risk: 'low' },
      Dice: { enabled: true, warnings: [], requiresConfirm: false, risk: 'low' },
      Pivot: { enabled: true, warnings: [], requiresConfirm: false, risk: 'low' },
    }

    if (!selectedCubeInfo.value.Capabilities.AllowDrillDown) {
      guards.DrillDown = { ...guards.DrillDown, enabled: false, reason: 'Cube hiện tại không hỗ trợ DrillDown (thiếu TG).', risk: 'high' }
    }
    if (!selectedCubeInfo.value.Capabilities.AllowRollUp) {
      guards.RollUp = { ...guards.RollUp, enabled: false, reason: 'Cube hiện tại không hỗ trợ RollUp (thiếu TG).', risk: 'high' }
    }
    if (!selectedCubeInfo.value.Capabilities.AllowSlice) {
      guards.Slice = { ...guards.Slice, enabled: false, reason: 'Cube hiện tại không hỗ trợ Slice (thiếu TG).', risk: 'high' }
    }
    if (!selectedCubeInfo.value.Capabilities.AllowDice) {
      guards.Dice = { ...guards.Dice, enabled: false, reason: 'Cube hiện tại không hỗ trợ Dice (thiếu TG).', risk: 'high' }
    }
    if (!selectedCubeInfo.value.Capabilities.AllowPivot) {
      guards.Pivot = { ...guards.Pivot, enabled: false, reason: 'Cube hiện tại không hỗ trợ Pivot (thiếu TG).', risk: 'high' }
    }

    if (!canUseTimeOperations.value) return guards
    if (!canDrillDown.value) guards.DrillDown = { ...guards.DrillDown, enabled: false, reason: 'Đang ở mức thấp nhất (Tháng).' }
    if (!canRollUp.value) guards.RollUp = { ...guards.RollUp, enabled: false, reason: 'Đang ở mức cao nhất (Năm).' }
    if (!drillContext.value.year) guards.Slice = { ...guards.Slice, enabled: false, reason: 'Slice cần chọn 1 năm trên biểu đồ/bảng trước.' }
    if (!hasDimensionFilter.value) guards.Dice = { ...guards.Dice, enabled: false, reason: 'Dice cần ít nhất 1 ngữ cảnh đã click.' }
    if (!drillContext.value.year) guards.Pivot = { ...guards.Pivot, enabled: false, reason: 'Pivot cần chọn Năm trước khi đổi trục.' }
    return guards
  })

  function buildRequest(): OlapRequest {
    return {
      Cube: selectedCube.value,
      Measure: selectedMeasure.value,
      ProductKey: drillContext.value.productKey,
      CustomerKey: drillContext.value.customerKey,
      StoreKey: drillContext.value.storeKey,
      Year: drillContext.value.year,
      Quarter: drillContext.value.quarter,
      RowLevel: currentLevel.value,
      ColLevel: colAxis.value,
      IncludeSoLuong: includeSoLuong.value,
    }
  }

  async function executeOperation(call: () => Promise<OlapResult>, opName: OperationType, onSuccess?: (res: OlapResult) => void) {
    isLoading.value = true
    errorMessage.value = ''
    const start = performance.now()
    try {
      const res = await call()
      if (res.Success) {
        resultData.value = res
        lastResultAt.value = new Date()
        lastOperation.value = opName
        onSuccess?.(res)
      } else {
        errorMessage.value = res.Error || `Lỗi ${opName}`
      }
    } catch (e: unknown) {
      errorMessage.value = (e as Error).message
    } finally {
      lastQueryMs.value = Math.round(performance.now() - start)
      isLoading.value = false
    }
  }

  async function loadMetadata() {
    try {
      metadata.value = await olapApi.getMetadata(selectedCube.value)
      metadataLoadedAt.value = new Date()
      if (metadata.value?.Measures?.length && !metadata.value.Measures.includes(selectedMeasure.value)) {
        selectedMeasure.value = metadata.value.Measures[0]
      }
      const firstAxis = availablePivotAxes.value[0]
      colAxis.value = firstAxis ?? 'Thang'
    } catch (e) {
      console.error('Không thể tải metadata', e)
    }
  }

  function clearPendingOperation() {
    pendingOperation.value = null
    pendingWarning.value = ''
    pendingRisk.value = 'low'
  }

  function requestOperation(op: SafeOperationType): boolean {
    const guard = operationGuards.value[op]
    if (!guard.enabled) {
      clearPendingOperation()
      errorMessage.value = guard.reason || 'Thao tác bị khóa.'
      return false
    }
    if (guard.requiresConfirm) {
      pendingOperation.value = op
      pendingWarning.value = guard.warnings.join(' ')
      pendingRisk.value = guard.risk
      return false
    }
    clearPendingOperation()
    return true
  }

  function setContextByMember(memberLabel: string) {
    selectedMemberLabel.value = memberLabel
    const normalized = String(memberLabel ?? '').trim()
    if (!normalized) return

    const year = parseYear(normalized)
    if (currentLevel.value === 'Nam' && year) {
      drillContext.value.year = year
      drillContext.value.quarter = undefined
      return
    }
    if (currentLevel.value === 'Quy') {
      const q = parseQuarter(normalized)
      if (q) {
        drillContext.value.quarter = q
      }
      if (year) {
        drillContext.value.year = year
      }
      return
    }
    if (colAxis.value === 'Ma MH') {
      drillContext.value.productKey = extractMemberCode(normalized)
    } else if (colAxis.value === 'Ma KH') {
      drillContext.value.customerKey = extractMemberCode(normalized)
    } else if (colAxis.value === 'Ma CH') {
      drillContext.value.storeKey = extractMemberCode(normalized)
    }
  }

  async function drillByMember(memberLabel: string) {
    if (!canDrillDown.value) return
    setContextByMember(memberLabel)
    await doDrillDown()
  }

  async function doDrillDown() {
    if (!canUseTimeOperations.value || !canDrillDown.value) return
    await executeOperation(
      () => olapApi.drill(buildRequest()),
      'DrillDown',
      (res) => {
        currentLevel.value = res.CurrentLevel as OlapLevel
      },
    )
  }

  async function doRollUp() {
    if (!canUseTimeOperations.value || !canRollUp.value) return
    await executeOperation(
      () => olapApi.rollup(buildRequest()),
      'RollUp',
      (res) => {
        currentLevel.value = res.CurrentLevel as OlapLevel
        if (res.CurrentLevel === 'Quy') {
          drillContext.value.quarter = undefined
        } else if (res.CurrentLevel === 'Nam') {
          drillContext.value.quarter = undefined
        }
      },
    )
  }

  async function doSlice() {
    if (!canUseTimeOperations.value) return
    await executeOperation(() => olapApi.sliceDice({ ...buildRequest(), IsDice: false }), 'Slice')
  }

  async function doDice() {
    if (!canUseTimeOperations.value) return
    await executeOperation(() => olapApi.sliceDice({ ...buildRequest(), IsDice: true }), 'Dice')
  }

  async function doPivot() {
    if (!canUseTimeOperations.value) return
    const axisOptions = availablePivotAxes.value
    if (axisOptions.length > 0) {
      colAxis.value = colAxis.value === 'Thang' ? axisOptions[0] : 'Thang'
    } else {
      colAxis.value = 'Thang'
    }
    await executeOperation(() => olapApi.pivot({ ...buildRequest(), ColLevel: colAxis.value }), 'Pivot')
  }

  async function loadDefaultQuery() {
    await executeOperation(
      () => olapApi.getDefaultQuery(selectedCube.value),
      'DefaultQuery',
      () => {
        currentLevel.value = 'Nam'
        drillContext.value = {}
        selectedMemberLabel.value = ''
      },
    )
  }

  async function runOperation(op: SafeOperationType) {
    if (!requestOperation(op)) return
    if (op === 'DrillDown') await doDrillDown()
    if (op === 'RollUp') await doRollUp()
    if (op === 'Slice') await doSlice()
    if (op === 'Dice') await doDice()
    if (op === 'Pivot') await doPivot()
  }

  async function confirmPendingOperation() {
    const op = pendingOperation.value
    if (!op) return
    clearPendingOperation()
    await runOperation(op)
  }

  watch(selectedMeasure, (measure) => {
    if (measure === 'So Luong Dat' && includeSoLuong.value) {
      includeSoLuong.value = false
    }
  }, { immediate: true })

  watch(selectedCube, async () => {
    await loadMetadata()
    await loadDefaultQuery()
  })

  return {
    currentLevel, rowAxis, colAxis,
    selectedCube, selectedMeasure,
    includeSoLuong,
    selectedMemberLabel, drillContext,
    resultData, metadata, isLoading, lastOperation, errorMessage,
    pendingOperation, pendingWarning, pendingRisk,
    lastQueryMs, lastResultAt, metadataLoadedAt, tableDensity,
    canDrillDown, canRollUp,
    selectedCubeInfo,
    cubeHasTime, cubeHasProduct, cubeHasCustomer, cubeHasStore,
    canUseTimeOperations, availablePivotAxes, hasDimensionFilter, operationGuards,
    loadMetadata, doDrillDown, doRollUp, doSlice, doDice, doPivot,
    loadDefaultQuery, runOperation, requestOperation, clearPendingOperation, confirmPendingOperation,
    drillByMember, setContextByMember,
  }
})
