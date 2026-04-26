<template>
  <div class="action-panel glass-card" :class="{ compact: compactMode }">
    <h3 v-if="!compactMode" class="panel-title">Thao Tác OLAP</h3>
    <p v-if="!compactMode" class="panel-desc">Chế độ an toàn: chỉ cho phép chạy truy vấn hợp lệ với ngữ cảnh lọc hiện tại.</p>

    <div v-if="!compactMode" class="safety-list">
      <span class="safety-chip ok">An toàn: tự khóa thao tác dễ lỗi</span>
      <span class="safety-chip warn">Cảnh báo trước khi chạy tác vụ nặng</span>
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

      <!-- Slice -->
      <button
        class="olap-btn btn-slice"
        :disabled="!guards.Slice.enabled || store.isLoading"
        :class="{ active: store.lastOperation === 'Slice' }"
        @click="store.runOperation('Slice')"
        title="Slice: lọc 1 chiều duy nhất (cần chọn Năm)"
      >
        <span class="btn-label">Slice</span>
        <span v-if="!compactMode" class="btn-sub">1 chiều</span>
        <span v-if="!compactMode && guards.Slice.reason" class="btn-reason">{{ guards.Slice.reason }}</span>
      </button>

      <!-- Dice -->
      <button
        class="olap-btn btn-dice"
        :disabled="!guards.Dice.enabled || store.isLoading"
        :class="{ active: store.lastOperation === 'Dice' }"
        @click="store.runOperation('Dice')"
        title="Dice: lọc nhiều chiều cùng lúc"
      >
        <span class="btn-label">Dice</span>
        <span v-if="!compactMode" class="btn-sub">Nhiều chiều</span>
        <span v-if="!compactMode && guards.Dice.reason" class="btn-reason">{{ guards.Dice.reason }}</span>
      </button>

      <!-- Pivot -->
      <button
        class="olap-btn btn-pivot"
        :disabled="!guards.Pivot.enabled || store.isLoading"
        :class="{ active: store.lastOperation === 'Pivot' }"
        @click="store.runOperation('Pivot')"
        title="Pivot: hoán đổi trục hàng / cột"
      >
        <span class="btn-label">Pivot</span>
        <span v-if="!compactMode" class="btn-sub">Đổi trục</span>
        <span v-if="!compactMode && guards.Pivot.reason" class="btn-reason">{{ guards.Pivot.reason }}</span>
      </button>
    </div>

    <div v-if="store.pendingOperation" class="confirm-box" :class="[`risk-${store.pendingRisk}`, { compact: compactMode }]">
      <div class="confirm-title">
        Cảnh báo trước khi chạy <strong>{{ store.pendingOperation }}</strong>
      </div>
      <p class="confirm-text">{{ store.pendingWarning || 'Truy vấn có thể ảnh hưởng hiệu năng hệ thống.' }}</p>
      <div class="confirm-actions">
        <button class="confirm-btn confirm-run" @click="store.confirmPendingOperation()" :disabled="store.isLoading">
          Tôi đã hiểu, tiếp tục
        </button>
        <button class="confirm-btn confirm-cancel" @click="store.clearPendingOperation()" :disabled="store.isLoading">
          Hủy
        </button>
      </div>
    </div>

    <!-- Loading indicator -->
    <div v-if="store.isLoading" class="loading-bar">
      <div class="loading-progress"></div>
    </div>

    <!-- Axis info -->
    <div class="axis-info" v-if="!compactMode && store.colAxis">
      <span class="axis-tag">Cột: <strong>{{ axisColLabel }}</strong></span>
      <span class="axis-tag">Hàng: <strong>{{ axisRowLabel }}</strong></span>
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

const store = useOlapStore()
const guards = computed(() => store.operationGuards)
const props = withDefaults(defineProps<{ compact?: boolean }>(), { compact: false })
const compactMode = computed(() => props.compact)

const levelMap: Record<string, string> = { Nam: 'Năm', Quy: 'Quý', Thang: 'Tháng' }

const drillLabel = computed(() => {
  const next = { Nam: 'Quý', Quy: 'Tháng', Thang: '—' }[store.currentLevel]
  return `${levelMap[store.currentLevel]} → ${levelMap[next ?? 'Nam'] ?? '—'}`
})

const rollupLabel = computed(() => {
  const prev = { Nam: '—', Quy: 'Năm', Thang: 'Quý' }[store.currentLevel]
  return `${levelMap[store.currentLevel]} → ${levelMap[prev ?? 'Nam'] ?? '—'}`
})

const axisColLabel = computed(() =>
  store.colAxis === 'Ma MH'
    ? 'Mặt Hàng'
    : store.colAxis === 'Ma KH'
      ? 'Khách Hàng'
      : store.colAxis === 'Ma CH'
        ? 'Cửa Hàng'
        : 'Thời Gian'
)
const axisRowLabel = computed(() =>
  store.colAxis === 'Thang' ? pivotAxisLabel.value : 'Thời Gian'
)

const pivotAxisLabel = computed(() => {
  if (store.availablePivotAxes.includes('Ma MH')) return 'Mặt Hàng'
  if (store.availablePivotAxes.includes('Ma KH')) return 'Khách Hàng'
  if (store.availablePivotAxes.includes('Ma CH')) return 'Cửa Hàng'
  return 'Chiều phụ'
})
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

.safety-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.safety-chip {
  border-radius: 999px;
  padding: 0.2rem 0.6rem;
  font-size: 0.7rem;
  border: 1px solid transparent;
}

.safety-chip.ok {
  color: #2f5f1d;
  background: var(--color-state-success-bg);
  border-color: var(--color-state-success-border);
}

.safety-chip.warn {
  color: var(--color-state-warning-text);
  background: var(--color-state-warning-bg);
  border-color: var(--color-state-warning-border);
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
.btn-slice  { background: linear-gradient(135deg, var(--color-brand-primary), #7fb85d); color: #fff; border-color: var(--color-brand-primary); }
.btn-dice   { background: linear-gradient(135deg, #7fb85d, var(--palette-positive-500)); color: #fff; border-color: #7fb85d; }
.btn-pivot  { background: linear-gradient(135deg, var(--color-state-warning-text), var(--color-kpi-critical)); color: #fff; border-color: var(--color-kpi-critical); grid-column: 1 / -1; }
.action-panel.compact .btn-pivot { grid-column: auto; }

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

.confirm-box {
  border-radius: 10px;
  border: 1px solid var(--border);
  padding: 0.75rem;
  background: rgba(15, 23, 42, 0.55);
}

.confirm-box.compact {
  margin-top: 0.2rem;
  padding: 0.55rem;
}

.confirm-box.risk-medium {
  border-color: var(--color-state-warning-border);
  background: var(--color-state-warning-bg);
}

.confirm-box.risk-high {
  border-color: var(--color-state-critical-border);
  background: var(--color-state-critical-bg);
}

.confirm-title {
  font-size: 0.8rem;
  color: var(--text-primary);
  margin-bottom: 0.35rem;
}

.confirm-text {
  margin: 0;
  color: var(--text-muted);
  font-size: 0.76rem;
}

.confirm-actions {
  margin-top: 0.65rem;
  display: flex;
  gap: 0.5rem;
}

.confirm-box.compact .confirm-actions {
  margin-top: 0.45rem;
  gap: 0.35rem;
}

.confirm-btn {
  border-radius: 8px;
  border: 1px solid var(--border);
  padding: 0.35rem 0.6rem;
  font-size: 0.72rem;
  cursor: pointer;
  font-family: inherit;
}

.confirm-run {
  background: var(--color-brand-primary);
  color: #fff;
  border-color: var(--color-brand-primary);
}

.confirm-cancel {
  background: transparent;
  color: var(--text-muted);
}

@media (max-width: 768px) {
  .action-panel { padding: 1rem; }
  .btn-grid { grid-template-columns: 1fr; }
  .btn-pivot { grid-column: auto; }
}
</style>
