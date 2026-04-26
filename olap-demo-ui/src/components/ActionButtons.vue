<template>
  <div class="action-panel glass-card" :class="{ compact: compactMode }">
    <h3 v-if="!compactMode" class="panel-title">Drill / Roll</h3>
    <p v-if="!compactMode" class="panel-desc">Tính năng 1: chọn chiều cần điều hướng rồi click member trên bảng/biểu đồ để drill down.</p>

    <div class="dimension-picker">
      <button class="dim-btn" :class="{ active: store.activeDimension === 'ThoiGian' }" @click="setActiveDimension('ThoiGian')">Thời Gian</button>
      <button class="dim-btn" :class="{ active: store.activeDimension === 'KhachHang' }" @click="setActiveDimension('KhachHang')">Khách Hàng</button>
      <button
        class="dim-btn"
        :class="{ active: store.activeDimension === 'DiaDiem' }"
        :disabled="!store.cubeHasStore"
        @click="setActiveDimension('DiaDiem')"
      >
        Địa Điểm
      </button>
    </div>

    <div class="member-picker" v-if="customerTypeCandidates.length && store.activeDimension === 'KhachHang' && store.currentLevel === 'LoaiKH'">
      <span class="axis-tag">Chọn loại KH:</span>
      <button
        v-for="member in customerTypeCandidates"
        :key="`kh-${member}`"
        class="dim-chip dim-chip-add"
        @click="pickCustomerType(member)"
      >
        {{ member }}
      </button>
    </div>

    <div v-if="!compactMode" class="dimension-manager">
      <div class="dim-list">
        <span class="axis-tag">Chiều active:</span>
        <button
          v-for="dim in store.activeDimensions"
          :key="`active-${dim}`"
          class="dim-chip"
          @click="store.removeDimension(dim)"
          :disabled="store.activeDimensions.length <= 1"
          title="Bỏ chiều khỏi biểu đồ"
        >
          {{ levelMap[dim] ?? dim }} ×
        </button>
      </div>
      <div class="dim-list" v-if="store.availableAddDimensions.length">
        <span class="axis-tag">Thêm chiều:</span>
        <button
          v-for="dim in store.availableAddDimensions"
          :key="`add-${dim}`"
          class="dim-chip dim-chip-add"
          @click="store.addDimension(dim)"
          title="Thêm chiều vào biểu đồ"
        >
          + {{ levelMap[dim] ?? dim }}
        </button>
      </div>
    </div>

    <div class="btn-grid">
      <!-- Drill Down -->
      <button
        class="olap-btn btn-drill"
        :disabled="!guards.DrillDown.enabled || store.isLoading"
        :class="{ active: store.lastOperation === 'DrillDown' }"
        @click="store.runOperation('DrillDown')"
        title="Drill Down: đi sâu vào cấp chi tiết hơn (Năm → Quý → Tháng)"
      >
        <span class="btn-label">Drill Down</span>
        <span v-if="!compactMode" class="btn-sub">{{ drillLabel }}</span>
        <span v-if="!compactMode && guards.DrillDown.reason" class="btn-reason">{{ guards.DrillDown.reason }}</span>
      </button>

      <!-- Roll Up -->
      <button
        class="olap-btn btn-rollup"
        :disabled="!guards.RollUp.enabled || store.isLoading"
        :class="{ active: store.lastOperation === 'RollUp' }"
        @click="store.runOperation('RollUp')"
        title="Roll Up: gộp lên cấp tổng hợp hơn (Tháng → Quý → Năm)"
      >
        <span class="btn-label">Roll Up</span>
        <span v-if="!compactMode" class="btn-sub">{{ rollupLabel }}</span>
        <span v-if="!compactMode && guards.RollUp.reason" class="btn-reason">{{ guards.RollUp.reason }}</span>
      </button>

    </div>

    <!-- Loading indicator -->
    <div v-if="store.isLoading" class="loading-bar">
      <div class="loading-progress"></div>
    </div>

    <!-- Axis info -->
    <div class="axis-info" v-if="!compactMode && store.colAxis">
      <span class="axis-tag">Chiều drill: <strong>{{ activeDimensionLabel }}</strong></span>
      <span class="axis-tag">Level hiện tại: <strong>{{ currentLevelLabel }}</strong></span>
    </div>

    <!-- Operation badge -->
    <div class="op-badge" v-if="!compactMode && store.lastOperation">
      Phép cuối: <strong>{{ store.lastOperation }}</strong>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useOlapStore } from '@/stores/olapStore'
import type { DrillDimension } from '@/stores/olapStore'

const store = useOlapStore()
const guards = computed(() => store.operationGuards)
const props = withDefaults(defineProps<{ compact?: boolean }>(), { compact: false })
const compactMode = computed(() => props.compact)

const levelMap: Record<string, string> = {
  Nam: 'Năm', Quy: 'Quý', Thang: 'Tháng',
  LoaiKH: 'Loại KH', TenKH: 'Tên KH',
  Bang: 'Bang', ThanhPho: 'Thành phố',
  ThoiGian: 'Thời Gian', KhachHang: 'Khách Hàng', DiaDiem: 'Địa Điểm', MatHang: 'Mặt Hàng',
}

const drillLabel = computed(() => {
  if (store.activeDimension === 'KhachHang') {
    const next = { LoaiKH: 'TenKH', TenKH: '—' }[store.currentLevel as 'LoaiKH' | 'TenKH']
    return `${levelMap[store.currentLevel]} → ${levelMap[next ?? 'LoaiKH'] ?? '—'}`
  }
  if (store.activeDimension === 'DiaDiem') {
    const next = { Bang: 'ThanhPho', ThanhPho: '—' }[store.currentLevel as 'Bang' | 'ThanhPho']
    return `${levelMap[store.currentLevel]} → ${levelMap[next ?? 'Bang'] ?? '—'}`
  }
  const next = { Nam: 'Quy', Quy: 'Thang', Thang: '—' }[store.currentLevel as 'Nam' | 'Quy' | 'Thang']
  return `${levelMap[store.currentLevel]} → ${levelMap[next ?? 'Nam'] ?? '—'}`
})

const rollupLabel = computed(() => {
  if (store.activeDimension === 'KhachHang') {
    const prev = { LoaiKH: '—', TenKH: 'LoaiKH' }[store.currentLevel as 'LoaiKH' | 'TenKH']
    return `${levelMap[store.currentLevel]} → ${levelMap[prev ?? 'LoaiKH'] ?? '—'}`
  }
  if (store.activeDimension === 'DiaDiem') {
    const prev = { Bang: '—', ThanhPho: 'Bang' }[store.currentLevel as 'Bang' | 'ThanhPho']
    return `${levelMap[store.currentLevel]} → ${levelMap[prev ?? 'Bang'] ?? '—'}`
  }
  const prev = { Nam: '—', Quy: 'Nam', Thang: 'Quy' }[store.currentLevel as 'Nam' | 'Quy' | 'Thang']
  return `${levelMap[store.currentLevel]} → ${levelMap[prev ?? 'Nam'] ?? '—'}`
})

const activeDimensionLabel = computed(() =>
  store.activeDimension === 'ThoiGian' ? 'Thời Gian' : (store.activeDimension === 'KhachHang' ? 'Khách Hàng' : 'Địa Điểm')
)
const currentLevelLabel = computed(() => levelMap[store.currentLevel] ?? store.currentLevel)

const customerTypeCandidates = computed(() => {
  const rows = store.resultData?.Rows ?? []
  const cols = store.resultData?.Columns ?? []
  if (!rows.length || !cols.length) return []
  const typeCol = cols.find(c => /\[loai\s*kh\]|loaikh|customer\s*type/i.test(c)) ?? cols[0]
  const values = rows
    .map(r => String(r[typeCol] ?? '').trim())
    .filter(Boolean)
    .filter(v => !/^\d{4}$/.test(v))
  return [...new Set(values)].slice(0, 12)
})

function pickCustomerType(member: string) {
  if (!member) return
  store.setContextByMember(member, [member])
}

function setActiveDimension(dimension: DrillDimension) {
  if (dimension === 'DiaDiem' && !store.cubeHasStore) {
    store.errorMessage = 'Cube hiện tại không hỗ trợ chiều Địa Điểm.'
    return
  }
  store.activeDimension = dimension
}
</script>

<style scoped>
.action-panel {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.action-panel.compact {
  padding: 0.45rem;
  gap: 0.35rem;
  border-radius: 10px;
}

.panel-title {
  font-size: 1rem;
  font-weight: 700;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0 0 0.5rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid var(--border);
}

.panel-desc {
  margin: -0.25rem 0 0;
  color: var(--text-muted);
  font-size: 0.78rem;
  line-height: 1.4;
}

.dimension-picker {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.dim-btn {
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 0.35rem 0.65rem;
  background: var(--bg-input);
  color: var(--text-muted);
  font-size: 0.75rem;
  cursor: pointer;
}
.dim-btn:disabled {
  cursor: not-allowed;
  opacity: 0.45;
}
.dim-btn.active {
  background: var(--color-brand-primary);
  color: #fff;
  border-color: var(--color-brand-primary);
}

.btn-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.action-panel.compact .btn-grid {
  display: flex;
  align-items: stretch;
  flex-wrap: nowrap;
  gap: 0.35rem;
}

.olap-btn {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.2rem;
  padding: 0.9rem 0.5rem;
  border: 1px solid transparent;
  border-radius: 12px;
  cursor: pointer;
  font-family: inherit;
  transition: all 0.2s ease;
  position: relative;
  overflow: hidden;
}

.action-panel.compact .olap-btn {
  min-width: 94px;
  padding: 0.42rem 0.45rem;
  border-radius: 8px;
  justify-content: center;
}

.olap-btn::before {
  content: '';
  position: absolute;
  inset: 0;
  opacity: 0;
  transition: opacity 0.2s;
  background: linear-gradient(135deg, rgba(255,255,255,0.1), transparent);
}

.olap-btn:hover:not(:disabled)::before { opacity: 1; }
.olap-btn:hover:not(:disabled) { transform: translateY(-2px); box-shadow: 0 8px 18px rgba(70, 132, 50, 0.22); }
.olap-btn:active:not(:disabled) { transform: translateY(0); }
.olap-btn:disabled { opacity: 0.4; cursor: not-allowed; }

.btn-label { font-size: 0.85rem; font-weight: 700; }
.btn-sub { font-size: 0.7rem; opacity: 0.7; }
.action-panel.compact .btn-label { font-size: 0.72rem; line-height: 1.1; }
.btn-reason {
  margin-top: 0.2rem;
  font-size: 0.64rem;
  text-align: center;
  line-height: 1.25;
  opacity: 0.85;
}

/* Colors per operation */
.btn-drill  { background: linear-gradient(135deg, var(--color-brand-primary), #5a9a41); color: #fff; border-color: var(--color-brand-primary); }
.btn-rollup { background: linear-gradient(135deg, #5a9a41, var(--palette-positive-400)); color: #fff; border-color: #5a9a41; }

.olap-btn.active { box-shadow: 0 0 0 2px #fff, 0 0 0 4px rgba(255,255,255,0.4); }

/* Loading bar */
.loading-bar {
  height: 3px;
  background: rgba(255,255,255,0.1);
  border-radius: 4px;
  overflow: hidden;
}

.loading-progress {
  height: 100%;
  width: 40%;
  background: linear-gradient(90deg, var(--color-brand-primary), var(--color-interaction-active));
  border-radius: 4px;
  animation: loading 1s ease-in-out infinite;
}

@keyframes loading {
  0%   { transform: translateX(-100%); }
  100% { transform: translateX(300%); }
}

.axis-info {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.axis-tag {
  background: var(--bg-input);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 0.3rem 0.6rem;
  font-size: 0.78rem;
  color: var(--text-muted);
}

.op-badge {
  text-align: center;
  font-size: 0.8rem;
  color: var(--text-muted);
  padding: 0.4rem;
  background: var(--bg-input);
  border-radius: 6px;
}

.dimension-manager {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
}

.member-picker {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
}

.dim-list {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
}

.dim-chip {
  border: 1px solid var(--border);
  border-radius: 999px;
  padding: 0.2rem 0.55rem;
  font-size: 0.72rem;
  background: #fff;
  color: var(--text-muted);
  cursor: pointer;
}

.dim-chip-add {
  border-color: color-mix(in srgb, var(--color-brand-primary) 45%, var(--border));
  color: var(--color-brand-primary);
}

@media (max-width: 768px) {
  .action-panel { padding: 1rem; }
  .btn-grid { grid-template-columns: 1fr; }
}
</style>
