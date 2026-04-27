import { defineStore } from 'pinia'
import { ref, computed, watch } from 'vue'
import { olapApi } from '@/api/olap'
import type { OlapRequest, OlapResult, MetadataResult, CubeInfo, CubeMappingItem } from '@/types/olap'

export type DrillDimension = 'ThoiGian' | 'KhachHang' | 'DiaDiem'
export type AnalyticalDimension = DrillDimension | 'MatHang'
export type TimeLevel = 'Nam' | 'Quy' | 'Thang'
export type CustomerLevel = 'LoaiKH' | 'TenKH'
export type LocationLevel = 'Bang' | 'ThanhPho'
export type OperationType = '' | 'DrillDown' | 'RollUp' | 'DefaultQuery' | 'Query'
export type FactType = 'BanHang' | 'TonKho'
const HOME_CUBE = 'Cube4BanHang_1D_TG'
const DIMENSION_TOKEN: Record<AnalyticalDimension, string> = {
  ThoiGian: 'TG',
  KhachHang: 'KH',
  DiaDiem: 'CH',
  MatHang: 'MH',
}
const FACT_DEFAULTS: Record<FactType, { cube: string; measure: string; includeSoLuong: boolean }> = {
  BanHang: { cube: 'Cube4BanHang_1D_TG', measure: 'Tong Tien', includeSoLuong: true },
  TonKho: { cube: 'Cube4TonKho_3D_MH_CH_TG_01', measure: 'So Luong Trong Kho', includeSoLuong: false },
}
const FACT_DIMENSION_DEFAULTS: Record<FactType, AnalyticalDimension[]> = {
  BanHang: ['ThoiGian'],
  TonKho: ['ThoiGian', 'DiaDiem', 'MatHang'],
}

type LevelState = {
  ThoiGian: TimeLevel
  KhachHang: CustomerLevel
  DiaDiem: LocationLevel
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

function normalizeFactLabel(value: unknown): FactType | null {
  const normalized = String(value ?? '').trim().toLowerCase()
  if (!normalized) return null
  if (normalized.includes('tonkho') || normalized.includes('ton kho')) return 'TonKho'
  if (normalized.includes('banhang') || normalized.includes('ban hang')) return 'BanHang'
  return null
}

function inferDimensionTokensFromCubeName(cubeName: string): string[] {
  const normalized = String(cubeName ?? '').toUpperCase()
  const tokens = new Set<string>()
  if (normalized.includes('_TG_')) tokens.add('TG')
  if (normalized.includes('_KH_')) tokens.add('KH')
  if (normalized.includes('_MH_')) tokens.add('MH')
  if (normalized.includes('_CH_')) tokens.add('CH')
  return Array.from(tokens).sort()
}

interface DrillContext {
  year?: number
  quarter?: number
  month?: number
  productKey?: string
  customerKey?: string
  storeKey?: string
  customerType?: string
  customerName?: string
  state?: string
  city?: string
}

const REQUEST_TIMEOUT_MS = 25_000

function withTimeout<T>(promise: Promise<T>, timeoutMs: number): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timer = window.setTimeout(() => {
      reject(new Error(`Request timeout sau ${Math.round(timeoutMs / 1000)} giay`))
    }, timeoutMs)

    promise
      .then((value) => {
        window.clearTimeout(timer)
        resolve(value)
      })
      .catch((error) => {
        window.clearTimeout(timer)
        reject(error)
      })
  })
}

function parseYear(text: string): number | undefined {
  const match = text.match(/\b(19|20)\d{2}\b/)
  if (!match) return undefined
  const y = Number(match[0])
  return Number.isFinite(y) ? y : undefined
}

function parseQuarter(text: string): number | undefined {
  const normalized = text
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim()
    .toUpperCase()

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

  // Fallback for plain labels like "1", "Quy-2", "Q_3 (2025)"
  const looseNumeric = normalized.match(/\b([1-4])\b/)
  if (looseNumeric) {
    const q = Number(looseNumeric[1])
    if (Number.isFinite(q)) return q
  }

  return undefined
}

function parseMonth(text: string): number | undefined {
  const normalized = text
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim()
    .toUpperCase()

  const monthMatch = normalized.match(/(?:\bTHANG\s*|MONTH\s*|&\[)(1[0-2]|[1-9])(?:\b|\])/)
  if (monthMatch) {
    const month = Number(monthMatch[1])
    if (Number.isFinite(month)) return month
  }

  const looseNumeric = normalized.match(/\b(1[0-2]|[1-9])\b/)
  if (looseNumeric) {
    const month = Number(looseNumeric[1])
    if (Number.isFinite(month)) return month
  }

  return undefined
}

function sanitizeCandidate(value: unknown): string {
  return String(value ?? '').trim()
}

function extractMemberCode(text: string): string {
  const match = text.match(/([A-Z]{2}\d+)/i)
  return match?.[1]?.toUpperCase() ?? text.trim()
}

function looksLikePureTimeToken(value: string): boolean {
  const normalized = value.trim()
  if (!normalized) return false
  if (/^\d{4}$/.test(normalized)) return true
  if (/^(q|quy|quarter)\s*[1-4]$/i.test(normalized)) return true
  if (/^[1-4]$/.test(normalized)) return true
  return false
}

function isCubeTimeCapable(cubeInfo: CubeInfo): boolean {
  return cubeInfo.Capabilities.HasTime
}

export const useOlapStore = defineStore('olap', () => {
  const activeDimension = ref<DrillDimension>('ThoiGian')
  const levelByDimension = ref<LevelState>({
    ThoiGian: 'Nam',
    KhachHang: 'LoaiKH',
    DiaDiem: 'Bang',
  })
  const rowAxis = ref('Nam')
  const colAxis = ref('Ma MH')
  const selectedCube = ref('Cube4BanHang_1D_TG')
  const activeDimensions = ref<AnalyticalDimension[]>(['ThoiGian'])
  const selectedMeasure = ref('Tong Tien')
  const includeSoLuong = ref(true)

  const drillContext = ref<DrillContext>({})
  const selectedMemberLabel = ref<string>('')

  const resultData = ref<OlapResult | null>(null)
  const metadata = ref<MetadataResult | null>(null)
  const isLoading = ref(false)
  const lastOperation = ref<OperationType>('')
  const errorMessage = ref('')
  const lastQueryMs = ref<number | null>(null)
  const lastResultAt = ref<Date | null>(null)
  const metadataLoadedAt = ref<Date | null>(null)
  const tableDensity = ref<'compact' | 'comfortable'>('compact')
  const cubeMappings = ref<CubeMappingItem[]>([])
  const pendingOverviewDimension = ref<DrillDimension | null>(null)

  const currentLevel = computed(() => levelByDimension.value[activeDimension.value])
  const canDrillDown = computed(() => {
    if (activeDimension.value === 'ThoiGian') return currentLevel.value !== 'Thang'
    if (activeDimension.value === 'KhachHang') return currentLevel.value !== 'TenKH'
    return currentLevel.value !== 'ThanhPho'
  })
  const canRollUp = computed(() => {
    if (activeDimension.value === 'ThoiGian') return currentLevel.value !== 'Nam'
    if (activeDimension.value === 'KhachHang') return currentLevel.value !== 'LoaiKH'
    return currentLevel.value !== 'Bang'
  })
  const selectedCubeInfo = computed<CubeInfo>(() => {
    const fromApi = metadata.value?.CubeInfos?.find(c => c.Name === selectedCube.value)
    return fromApi ?? parseCubeNameLocally(selectedCube.value)
  })
  const currentFact = computed<FactType>(() =>
    selectedCubeInfo.value.Fact === 'Fact_TonKho' ? 'TonKho' : 'BanHang'
  )
  const factDimensionScope = computed<AnalyticalDimension[]>(() => {
    const fact = currentFact.value
    const source = cubeMappings.value
      .filter(item => item.fact === fact)
      .flatMap(item => item.dimensions)
      .map(token => mapTokenToDimension(token))
      .filter((d): d is AnalyticalDimension => !!d)

    const unique = [...new Set(source)]
    return unique.length ? unique : FACT_DIMENSION_DEFAULTS[fact]
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

  const hasDrillSelection = computed(() => {
    if (activeDimension.value === 'ThoiGian') {
      if (currentLevel.value === 'Nam') return Number.isFinite(drillContext.value.year)
      if (currentLevel.value === 'Quy') return Number.isFinite(drillContext.value.quarter)
      return true
    }
    if (activeDimension.value === 'KhachHang') {
      if (currentLevel.value === 'LoaiKH') return !!String(drillContext.value.customerType ?? '').trim()
      return true
    }
    if (currentLevel.value === 'Bang') return !!String(drillContext.value.state ?? '').trim()
    return true
  })

  const operationGuards = computed(() => {
    const canUseDimension =
      (activeDimension.value === 'ThoiGian' && cubeHasTime.value) ||
      (activeDimension.value === 'KhachHang' && cubeHasCustomer.value) ||
      (activeDimension.value === 'DiaDiem' && cubeHasStore.value)
    return {
      DrillDown: {
        enabled: canUseDimension && canDrillDown.value && hasDrillSelection.value,
        reason: !canUseDimension
          ? 'Cube hiện tại không hỗ trợ chiều drill đang chọn.'
          : (!canDrillDown.value
              ? 'Đã ở mức thấp nhất của chiều.'
              : (!hasDrillSelection.value ? 'Hãy chọn member trước khi drill down.' : '')),
      },
      RollUp: {
        enabled: canUseDimension && canRollUp.value,
        reason: !canUseDimension
          ? 'Cube hiện tại không hỗ trợ chiều drill đang chọn.'
          : (!canRollUp.value ? 'Đã ở mức cao nhất của chiều.' : ''),
      },
    }
  })

  const removableDimensions = computed(() =>
    activeDimensions.value.filter(d => factDimensionScope.value.includes(d) && activeDimensions.value.length > 1)
  )

  const availableAddDimensions = computed(() =>
    factDimensionScope.value.filter(d => !activeDimensions.value.includes(d) && canResolveCube([...activeDimensions.value, d], currentFact.value))
  )

  function buildRequest(): OlapRequest {
    const dimensionLevels: Partial<Record<AnalyticalDimension, string>> = {
      ThoiGian: levelByDimension.value.ThoiGian,
      KhachHang: levelByDimension.value.KhachHang,
      DiaDiem: levelByDimension.value.DiaDiem,
      MatHang: 'MaMH',
    }
    return {
      Cube: selectedCube.value,
      Measure: selectedMeasure.value,
      ProductKey: drillContext.value.productKey,
      CustomerKey: drillContext.value.customerKey,
      StoreKey: drillContext.value.storeKey,
      CustomerType: drillContext.value.customerType,
      CustomerName: drillContext.value.customerName,
      State: drillContext.value.state,
      City: drillContext.value.city,
      Year: drillContext.value.year,
      Quarter: drillContext.value.quarter,
      Month: drillContext.value.month,
      ActiveDimension: activeDimension.value,
      ActiveDimensions: [...activeDimensions.value],
      DimensionLevels: dimensionLevels,
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
      const res = await withTimeout(call(), REQUEST_TIMEOUT_MS)
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
      const discoveredCubes = metadata.value?.Cubes ?? []
      const discoveredCubeInfos = metadata.value?.CubeInfos ?? []
      if (discoveredCubes.length > 0 && !discoveredCubes.includes(selectedCube.value)) {
        const preferTonKho = selectedCube.value.includes('TonKho')
        const preferredTimeCube = discoveredCubeInfos.find(info =>
          isCubeTimeCapable(info)
          && (preferTonKho
            ? info.Fact === 'Fact_TonKho'
            : info.Fact === 'Fact_BanHang')
        )?.Name
        selectedCube.value = preferredTimeCube ?? discoveredCubes[0]
        return
      }
      if (metadata.value?.Measures?.length && !metadata.value.Measures.includes(selectedMeasure.value)) {
        selectedMeasure.value = metadata.value.Measures[0]
      }
      const firstAxis = availablePivotAxes.value[0]
      colAxis.value = firstAxis ?? 'Thang'
      syncActiveDimensionsFromCube()
    } catch (e) {
      console.error('Không thể tải metadata', e)
    }
  }

  function syncActiveDimensionsFromCube() {
    const dims: AnalyticalDimension[] = []
    if (selectedCubeInfo.value.Capabilities.HasTime) dims.push('ThoiGian')
    if (selectedCubeInfo.value.Capabilities.HasCustomer) dims.push('KhachHang')
    if (selectedCubeInfo.value.Capabilities.HasStore) dims.push('DiaDiem')
    if (selectedCubeInfo.value.Capabilities.HasProduct) dims.push('MatHang')
    if (dims.length) activeDimensions.value = dims
    if (!activeDimensions.value.includes(activeDimension.value)) {
      const firstDrill = activeDimensions.value.find(d => d !== 'MatHang')
      if (firstDrill) activeDimension.value = firstDrill as DrillDimension
    }
  }

  async function loadCubeMappings() {
    try {
      cubeMappings.value = await olapApi.getCubeMappings()
    } catch {
      cubeMappings.value = []
    }
  }

  function normalizeTokens(tokens: string[]) {
    return [...new Set(tokens.map(t => t.trim().toUpperCase()).filter(Boolean))].sort()
  }

  function mapTokenToDimension(token: string): AnalyticalDimension | null {
    const normalized = token.trim().toUpperCase()
    if (normalized === 'TG') return 'ThoiGian'
    if (normalized === 'KH') return 'KhachHang'
    if (normalized === 'CH') return 'DiaDiem'
    if (normalized === 'MH') return 'MatHang'
    return null
  }

  function canResolveCube(dimensions: AnalyticalDimension[], fact: FactType): boolean {
    return !!resolveCubeByDimensions(dimensions, fact)
  }

  function resolveCubeByDimensions(dimensions: AnalyticalDimension[], fact: FactType): string | null {
    const expected = normalizeTokens(dimensions.map(d => DIMENSION_TOKEN[d]))
    const expectedCount = expected.length
    const expectedKey = expected.join('_')
    const preferredCubeByKey: Record<FactType, Record<string, string>> = {
      BanHang: {
        TG: 'Cube4BanHang_1D_TG',
        KH: 'Cube4BanHang_1D_KH_01',
        MH: 'Cube4BanHang_1D_MH',
        KH_TG: 'Cube4BanHang_2D_KH_TG_01',
        KH_MH: 'Cube4BanHang_2D_MH_KH_01',
        MH_TG: 'Cube4BanHang_2D_MH_TG_01',
      },
      TonKho: {
        TG: 'Cube4TonKho_1D_TG_01',
        CH_TG: 'Cube4TonKho_2D_CH_TG_01',
        MH_TG: 'Cube4TonKho_2D_MH_TG_01',
      },
    }
    const hardPreferred = preferredCubeByKey[fact]?.[expectedKey]
    if (hardPreferred) {
      const availableByName = new Set((metadata.value?.Cubes ?? []).map(c => c.toUpperCase()))
      const availableByInfo = new Set((metadata.value?.CubeInfos ?? []).map(c => c.Name.toUpperCase()))
      if (availableByName.has(hardPreferred.toUpperCase()) || availableByInfo.has(hardPreferred.toUpperCase())) {
        return hardPreferred
      }
    }

    const fromBackend = cubeMappings.value
      .filter(item => normalizeFactLabel(item.fact) === fact)
      .filter(item => {
        const tokens = normalizeTokens(item.dimensions)
        return tokens.length === expected.length && tokens.every((t, i) => t === expected[i])
      })
      .map(item => item.cube)

    const fromMetadata = (metadata.value?.CubeInfos ?? [])
      .filter(c => normalizeFactLabel(c.Fact) === fact)
      .filter(c => {
        const tokens = normalizeTokens([
          c.Capabilities.HasProduct ? 'MH' : '',
          c.Capabilities.HasTime ? 'TG' : '',
          c.Capabilities.HasCustomer ? 'KH' : '',
          c.Capabilities.HasStore ? 'CH' : '',
        ])
        return tokens.length === expected.length && tokens.every((t, i) => t === expected[i])
      })
      .map(c => c.Name)

    const fromCubeName = (metadata.value?.Cubes ?? [])
      .filter(name => normalizeFactLabel(name) === fact)
      .filter(name => name.endsWith('_01'))
      .filter(name => {
        const tokens = inferDimensionTokensFromCubeName(name)
        return tokens.length === expectedCount && tokens.every((t, i) => t === expected[i])
      })

    const candidates = [...new Set([...fromBackend, ...fromMetadata, ...fromCubeName])]
    if (candidates.length) {
      const exactDimension = candidates
        .filter(name => name.toUpperCase().includes(`_${expectedCount}D_`))
      const source = exactDimension.length ? exactDimension : candidates
      const timeOnly = expectedCount === 1 && expected[0] === 'TG'
        ? source.find(c => /_1D_TG_/i.test(c))
        : undefined
      if (timeOnly) return timeOnly
      const preferred = source.find(c => c.endsWith('_01'))
      return preferred ?? source.sort((a, b) => a.localeCompare(b))[0]
    }

    // Fallback: choose closest cube of same fact by capability, prioritizing time-only scenarios.
    const sameFactInfos = (metadata.value?.CubeInfos ?? [])
      .filter(c => normalizeFactLabel(c.Fact) === fact)
      .filter(c => c.Name?.endsWith('_01'))
    if (!sameFactInfos.length) return null

    const needTime = expected.includes('TG')
    const needCustomer = expected.includes('KH')
    const needProduct = expected.includes('MH')
    const needStore = expected.includes('CH')

    const capabilityMatches = sameFactInfos.filter(c =>
      (!needTime || c.Capabilities.HasTime)
      && (!needCustomer || c.Capabilities.HasCustomer)
      && (!needProduct || c.Capabilities.HasProduct)
      && (!needStore || c.Capabilities.HasStore)
    )

    const closest = (capabilityMatches.length ? capabilityMatches : sameFactInfos)
      .sort((a, b) => {
        const dimA = Number.isFinite(a.DimensionCount) ? a.DimensionCount : 99
        const dimB = Number.isFinite(b.DimensionCount) ? b.DimensionCount : 99
        if (dimA !== dimB) return dimA - dimB
        return a.Name.localeCompare(b.Name)
      })[0]

    return closest?.Name ?? null
  }

  async function syncCubeByDimensions(dimensions: AnalyticalDimension[] = activeDimensions.value): Promise<boolean> {
    const resolved = resolveCubeByDimensions(dimensions, currentFact.value)
    if (!resolved || resolved === selectedCube.value) return false
    cubeSwitchMode.value = 'preserve'
    selectedCube.value = resolved
    return true
  }

  const cubeSwitchMode = ref<'default' | 'preserve'>('default')

  async function queryCurrentView() {
    await executeOperation(
      () => olapApi.query(buildRequest()),
      'Query',
      () => {
        selectedMemberLabel.value = ''
      },
    )
  }

  function requestOperation(op: 'DrillDown' | 'RollUp'): boolean {
    const guard = operationGuards.value[op]
    if (!guard.enabled) {
      errorMessage.value = guard.reason || 'Thao tác bị khóa.'
      return false
    }
    return true
  }

  function setContextByMember(memberLabel: string, rowHints: Array<string | number> = []) {
    selectedMemberLabel.value = memberLabel
    const normalized = sanitizeCandidate(memberLabel)
    const hintValues = rowHints
      .map(sanitizeCandidate)
      .filter(Boolean)
    const candidates = [normalized, ...hintValues]
    if (!candidates.length) return

    const firstYear = candidates
      .map(parseYear)
      .find((y): y is number => Number.isFinite(y))
    const firstQuarter = candidates
      .map(parseQuarter)
      .find((q): q is number => Number.isFinite(q))

    if (activeDimension.value === 'ThoiGian') {
      const year = firstYear
      if (currentLevel.value === 'Nam' && year) {
        drillContext.value.year = year
        drillContext.value.quarter = undefined
        drillContext.value.month = undefined
        return
      }
      if (currentLevel.value === 'Quy') {
        const q = firstQuarter
        if (q) drillContext.value.quarter = q
        if (year) drillContext.value.year = year
        return
      }
      if (currentLevel.value === 'Thang') {
        const m = candidates
          .map(parseMonth)
          .find((month): month is number => Number.isFinite(month))
        if (m) drillContext.value.month = m
        if (firstQuarter) drillContext.value.quarter = firstQuarter
        if (year) drillContext.value.year = year
        return
      }
      return
    }
    if (activeDimension.value === 'KhachHang') {
      const customerCandidate = candidates.find(c => !looksLikePureTimeToken(c))
      if (!customerCandidate) return
      if (currentLevel.value === 'LoaiKH') {
        drillContext.value.customerType = customerCandidate
        drillContext.value.customerName = undefined
      } else {
        drillContext.value.customerName = customerCandidate
      }
      return
    }
    if (activeDimension.value === 'DiaDiem') {
      const locationCandidate = candidates.find(c => !looksLikePureTimeToken(c))
      if (!locationCandidate) return
      if (currentLevel.value === 'Bang') {
        drillContext.value.state = locationCandidate
        drillContext.value.city = undefined
      } else {
        drillContext.value.city = locationCandidate
      }
      return
    }
    const firstCandidate = candidates.find(Boolean)
    if (!firstCandidate) return
    if (colAxis.value === 'Ma MH') {
      drillContext.value.productKey = extractMemberCode(firstCandidate)
    } else if (colAxis.value === 'Ma KH') {
      drillContext.value.customerKey = extractMemberCode(firstCandidate)
    } else if (colAxis.value === 'Ma CH') {
      drillContext.value.storeKey = extractMemberCode(firstCandidate)
    }
  }

  async function drillByMember(memberLabel: string, rowHints: Array<string | number> = []) {
    if (!canDrillDown.value) return
    setContextByMember(memberLabel, rowHints)
    await doDrillDown()
  }

  async function doDrillDown() {
    if (!requestOperation('DrillDown')) return
    if (activeDimension.value === 'ThoiGian' && currentLevel.value === 'Nam' && !drillContext.value.year) {
      errorMessage.value = 'Hãy chọn một Năm trước khi drill down.'
      return
    }
    if (activeDimension.value === 'ThoiGian' && currentLevel.value === 'Quy' && !drillContext.value.quarter) {
      errorMessage.value = 'Hãy chọn một Quý trước khi drill down.'
      return
    }
    if (activeDimension.value === 'KhachHang' && currentLevel.value === 'LoaiKH' && !drillContext.value.customerType) {
      errorMessage.value = 'Hãy chọn một Loại KH trước khi drill down.'
      return
    }
    if (activeDimension.value === 'DiaDiem' && currentLevel.value === 'Bang' && !drillContext.value.state) {
      errorMessage.value = 'Hãy chọn một Bang trước khi drill down.'
      return
    }
    await executeOperation(
      () => olapApi.drill(buildRequest()),
      'DrillDown',
      (res) => {
        levelByDimension.value[activeDimension.value] = res.CurrentLevel as never
      },
    )
  }

  async function doRollUp() {
    if (!requestOperation('RollUp')) return
    await executeOperation(
      () => olapApi.rollup(buildRequest()),
      'RollUp',
      (res) => {
        const previousLevel = currentLevel.value
        levelByDimension.value[activeDimension.value] = res.CurrentLevel as never
        if (activeDimension.value === 'ThoiGian' && previousLevel === 'Thang') {
          drillContext.value.month = undefined
        } else if (activeDimension.value === 'ThoiGian' && previousLevel === 'Quy') {
          drillContext.value.quarter = undefined
          drillContext.value.month = undefined
        } else if (activeDimension.value === 'KhachHang' && previousLevel === 'TenKH') {
          drillContext.value.customerName = undefined
        } else if (activeDimension.value === 'DiaDiem' && previousLevel === 'ThanhPho') {
          drillContext.value.city = undefined
        }
      },
    )
  }

  async function loadDefaultQuery() {
    await executeOperation(
      () => olapApi.getDefaultQuery(selectedCube.value),
      'DefaultQuery',
      () => {
        levelByDimension.value = { ThoiGian: 'Nam', KhachHang: 'LoaiKH', DiaDiem: 'Bang' }
        drillContext.value = {}
        selectedMemberLabel.value = ''
      },
    )
  }

  async function loadOverviewForActiveDimension() {
    if (activeDimension.value === 'KhachHang') {
      if (!cubeHasCustomer.value) return
      levelByDimension.value.KhachHang = 'LoaiKH'
      drillContext.value.customerType = undefined
      drillContext.value.customerName = undefined
      await executeOperation(
        () => olapApi.query({
          ...buildRequest(),
          ActiveDimension: 'KhachHang',
          RowLevel: 'LoaiKH',
          CustomerName: undefined,
          CustomerType: undefined,
        }),
        'Query',
        () => {
          selectedMemberLabel.value = ''
        },
      )
      return
    }

    if (activeDimension.value === 'DiaDiem') {
      if (!cubeHasStore.value) return
      levelByDimension.value.DiaDiem = 'Bang'
      drillContext.value.state = undefined
      drillContext.value.city = undefined
      await executeOperation(
        () => olapApi.query({
          ...buildRequest(),
          ActiveDimension: 'DiaDiem',
          RowLevel: 'Bang',
          City: undefined,
          State: undefined,
        }),
        'Query',
        () => {
          selectedMemberLabel.value = ''
        },
      )
    }
  }

  function setSelectedMeasure(measure: string) {
    const normalized = String(measure ?? '').trim()
    if (!normalized) return
    selectedMeasure.value = normalized
  }

  function setSelectedCube(cube: string) {
    const normalized = String(cube ?? '').trim()
    if (!normalized) return
    selectedCube.value = normalized
  }

  function selectFact(fact: FactType) {
    const defaults = FACT_DEFAULTS[fact]
    activeDimensions.value = [...FACT_DIMENSION_DEFAULTS[fact]]
    activeDimension.value = (activeDimensions.value.find(d => d !== 'MatHang') ?? 'ThoiGian') as DrillDimension
    drillContext.value = {}
    selectedMemberLabel.value = ''
    errorMessage.value = ''
    selectedMeasure.value = defaults.measure
    includeSoLuong.value = defaults.includeSoLuong
    const resolved = resolveCubeByDimensions(activeDimensions.value, fact)
    cubeSwitchMode.value = 'default'
    selectedCube.value = resolved ?? defaults.cube
  }

  async function addDimension(dimension: AnalyticalDimension) {
    if (!factDimensionScope.value.includes(dimension)) return
    if (activeDimensions.value.includes(dimension)) return
    const next = [...activeDimensions.value, dimension]
    if (!canResolveCube(next, currentFact.value)) {
      errorMessage.value = 'Không tìm thấy cube phù hợp cho tập chiều mới.'
      return
    }
    activeDimensions.value = next
    if (dimension !== 'MatHang') {
      levelByDimension.value[dimension] = (dimension === 'ThoiGian' ? 'Nam' : (dimension === 'KhachHang' ? 'LoaiKH' : 'Bang')) as never
      activeDimension.value = dimension
    }
    const changedCube = await syncCubeByDimensions(next)
    if (!changedCube) {
      await queryCurrentView()
    }
  }

  async function removeDimension(dimension: AnalyticalDimension) {
    if (!activeDimensions.value.includes(dimension)) return
    if (activeDimensions.value.length <= 1) {
      errorMessage.value = 'Không thể bỏ chiều khi chỉ còn 1 chiều.'
      return
    }
    const next = activeDimensions.value.filter(d => d !== dimension)
    if (!canResolveCube(next, currentFact.value)) {
      errorMessage.value = 'Không tìm thấy cube phù hợp sau khi bỏ chiều.'
      return
    }
    if (dimension === 'ThoiGian') {
      drillContext.value.year = undefined
      drillContext.value.quarter = undefined
      drillContext.value.month = undefined
    } else if (dimension === 'KhachHang') {
      drillContext.value.customerType = undefined
      drillContext.value.customerName = undefined
      drillContext.value.customerKey = undefined
    } else if (dimension === 'DiaDiem') {
      drillContext.value.state = undefined
      drillContext.value.city = undefined
      drillContext.value.storeKey = undefined
    }
    if (dimension === 'MatHang') {
      drillContext.value.productKey = undefined
    }
    activeDimensions.value = next
    if (!next.includes(activeDimension.value)) {
      activeDimension.value = (next.find(d => d !== 'MatHang') ?? 'ThoiGian') as DrillDimension
    }
    const changedCube = await syncCubeByDimensions(next)
    if (!changedCube) {
      await queryCurrentView()
    }
  }

  async function runOperation(op: 'DrillDown' | 'RollUp') {
    if (op === 'DrillDown') await doDrillDown()
    if (op === 'RollUp') await doRollUp()
  }

  async function goHomeDashboard() {
    activeDimension.value = 'ThoiGian'
    levelByDimension.value = { ThoiGian: 'Nam', KhachHang: 'LoaiKH', DiaDiem: 'Bang' }
    drillContext.value = {}
    selectedMemberLabel.value = ''
    errorMessage.value = ''
    selectFact('BanHang')

    if (selectedCube.value !== HOME_CUBE) return

    await loadMetadata()
    await loadDefaultQuery()
  }

  watch(selectedMeasure, async (measure, prev) => {
    if (measure === 'So Luong Dat' && includeSoLuong.value) {
      includeSoLuong.value = false
    }
    if (!prev || measure === prev || isLoading.value) return
    await queryCurrentView()
  }, { immediate: true })

  watch(selectedCube, async () => {
    await loadMetadata()
    if (cubeSwitchMode.value === 'preserve') {
      cubeSwitchMode.value = 'default'
      await queryCurrentView()
      return
    }
    await loadDefaultQuery()
  })

  watch(activeDimension, async (next, prev) => {
    if (next === prev) return
    if (isLoading.value) {
      pendingOverviewDimension.value = next
      return
    }
    await loadOverviewForActiveDimension()
  })

  watch(isLoading, async (loading) => {
    if (loading) return
    const pending = pendingOverviewDimension.value
    if (!pending) return
    if (pending !== activeDimension.value) {
      pendingOverviewDimension.value = null
      return
    }
    pendingOverviewDimension.value = null
    await loadOverviewForActiveDimension()
  })

  return {
    currentLevel, rowAxis, colAxis,
    selectedCube, selectedMeasure,
    includeSoLuong,
    selectedMemberLabel, drillContext,
    resultData, metadata, isLoading, lastOperation, errorMessage,
    activeDimension, levelByDimension,
    lastQueryMs, lastResultAt, metadataLoadedAt, tableDensity,
    canDrillDown, canRollUp,
    selectedCubeInfo,
    cubeHasTime, cubeHasProduct, cubeHasCustomer, cubeHasStore, currentFact,
    availablePivotAxes, operationGuards,
    activeDimensions, availableAddDimensions, removableDimensions,
    loadMetadata, doDrillDown, doRollUp,
    loadOverviewForActiveDimension,
    loadCubeMappings, addDimension, removeDimension,
    queryCurrentView,
    loadDefaultQuery, runOperation, requestOperation, goHomeDashboard,
    drillByMember, setContextByMember, setSelectedMeasure, setSelectedCube, selectFact,
  }
})
