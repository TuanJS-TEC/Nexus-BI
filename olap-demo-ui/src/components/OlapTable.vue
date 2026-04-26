<template>
  <div class="table-container glass-card" :class="`density-${store.tableDensity}`">
    <div class="table-header">
      <h3 class="table-title">
        Kết Quả OLAP
        <span class="op-chip" v-if="result">{{ result.OperationType }}</span>
      </h3>
      <div class="table-meta" v-if="result">
        <span class="meta-badge">{{ result.Rows.length }} hàng</span>
        <span class="meta-badge">{{ result.Columns.length - 1 }} cột</span>
        <span class="meta-badge important-badge" v-if="importantRowsCount > 0">
          {{ importantRowsCount }} dòng quan trọng
        </span>
        <button class="copy-btn" @click="copyMdx" title="Copy MDX query">MDX</button>
      </div>
    </div>

    <div class="table-loading" v-if="store.isLoading">
      <div v-for="n in 6" :key="n" class="skeleton-line"></div>
    </div>

    <!-- Empty state -->
    <div class="empty-state" v-else-if="!result">
      <p>Đang chờ dữ liệu OLAP mặc định</p>
    </div>

    <!-- Error state -->
    <div class="error-state" v-else-if="!result.Success">
      <p>{{ result.Error }}</p>
      <details v-if="result.Mdx" class="mdx-debug">
        <summary>Xem MDX query</summary>
        <pre>{{ result.Mdx }}</pre>
      </details>
    </div>

    <!-- Data table -->
    <div class="table-scroll" v-else-if="result.Rows.length > 0">
      <table class="olap-table">
        <thead>
          <tr>
            <th v-for="col in result.Columns" :key="col" :class="{ 'col-dim': col === result.Columns[0] }">
              {{ col }}
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="(row, ri) in result.Rows"
            :key="ri"
            :class="{ 'row-even': ri % 2 === 0, 'row-important': isImportantRow(row, ri) }"
          >
            <td
              v-for="col in result.Columns"
              :key="col"
              :class="{ 'cell-dim': col === result.Columns[0], 'cell-value': col !== result.Columns[0] }"
            >
              <button
                v-if="col === result.Columns[0]"
                class="member-link"
                type="button"
                @click="onMemberClick(row[col])"
              >
                {{ row[col] }}
              </button>
              <span v-else class="numeric">{{ formatValue(row[col]) }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- No rows -->
    <div class="empty-state" v-else>
      <p>Không có dữ liệu để hiển thị</p>
    </div>

    <!-- MDX debug panel -->
    <details class="mdx-panel" v-if="result?.Mdx">
      <summary>Xem MDX Query đã thực thi</summary>
      <pre class="mdx-code">{{ result.Mdx }}</pre>
    </details>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useOlapStore } from '@/stores/olapStore'

const store = useOlapStore()
const result = computed(() => store.resultData)

const primaryMeasureColumn = computed(() => {
  const cols = result.value?.Columns ?? []
  return cols.length > 1 ? cols[1] : undefined
})

const importantRowIndexes = computed(() => {
  const res = result.value
  const measureCol = primaryMeasureColumn.value
  if (!res || !res.Rows.length || !measureCol) return new Set<number>()

  const pairs = res.Rows
    .map((row, index) => ({ index, value: Number(row[measureCol]) }))
    .filter(item => Number.isFinite(item.value))

  if (!pairs.length) return new Set<number>()

  const sortedValues = pairs.map(item => item.value).sort((a, b) => a - b)
  const quartileIndex = Math.max(0, Math.floor(sortedValues.length * 0.25) - 1)
  const lowThreshold = sortedValues[quartileIndex]
  const medianValue = sortedValues[Math.floor(sortedValues.length * 0.5)]

  const important = pairs
    .filter(item => item.value <= lowThreshold || item.value <= 0 || item.value < medianValue * 0.6)
    .map(item => item.index)

  return new Set<number>(important)
})

const importantRowsCount = computed(() => importantRowIndexes.value.size)

function isImportantRow(_row: Record<string, unknown>, index: number): boolean {
  return importantRowIndexes.value.has(index)
}

function formatValue(val: unknown): string {
  if (val === null || val === undefined) return '—'
  const n = Number(val)
  if (isNaN(n)) return String(val)
  if (n >= 1_000_000_000) return (n / 1_000_000_000).toFixed(2) + ' Tỷ'
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(2) + ' Tr'
  if (n >= 1_000) return n.toLocaleString('vi-VN')
  return n.toLocaleString('vi-VN', { maximumFractionDigits: 2 })
}

function copyMdx() {
  if (result.value?.Mdx) {
    navigator.clipboard.writeText(result.value.Mdx)
  }
}

async function onMemberClick(value: unknown) {
  const member = String(value ?? '').trim()
  if (!member || store.isLoading) return
  if (store.canDrillDown) {
    await store.drillByMember(member)
    return
  }
  store.setContextByMember(member)
}
</script>

<style scoped>
.table-container {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  overflow: hidden;
}

.table-loading {
  padding: 0 1.5rem 1rem;
  display: grid;
  gap: 0.5rem;
}

.skeleton-line {
  height: 14px;
  border-radius: 999px;
  background: linear-gradient(90deg, #e2e8f0 25%, #f1f5f9 50%, #e2e8f0 75%);
  background-size: 220% 100%;
  animation: shimmer 1.1s infinite linear;
}

@keyframes shimmer {
  from { background-position: 220% 0; }
  to { background-position: -220% 0; }
}

.table-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1.25rem 1.5rem 0;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.table-title {
  font-size: 1rem;
  font-weight: 700;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0;
}

.op-chip {
  font-size: 0.7rem;
  padding: 0.2rem 0.6rem;
  border-radius: 20px;
  background: var(--color-brand-primary);
  color: #fff;
  font-weight: 600;
}

.table-meta {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.meta-badge {
  background: var(--bg-input);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 0.2rem 0.5rem;
  font-size: 0.75rem;
  color: var(--text-muted);
}

.important-badge {
  background: color-mix(in srgb, var(--color-highlight-search) 60%, white);
  border-color: color-mix(in srgb, var(--color-highlight-search) 70%, var(--color-brand-primary));
  color: #4d642c;
}

.copy-btn {
  background: var(--bg-input);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 0.25rem 0.6rem;
  font-size: 0.75rem;
  color: var(--text-muted);
  cursor: pointer;
  font-family: inherit;
  transition: all 0.2s;
}
.copy-btn:hover {
  background: var(--color-brand-primary);
  color: #fff;
  border-color: var(--color-brand-primary);
}

/* Empty / Error states */
.empty-state, .error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 3rem 2rem;
  gap: 0.75rem;
  color: var(--text-muted);
  text-align: center;
}

.error-state { color: var(--color-kpi-critical); }

/* Table */
.table-scroll {
  overflow: auto;
  max-height: 400px;
  padding: 0 1.5rem 1.5rem;
}

.olap-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.85rem;
  min-width: 500px;
}

.olap-table thead th {
  position: sticky;
  top: 0;
  background: var(--bg-card);
  backdrop-filter: blur(10px);
  padding: 0.7rem 1rem;
  text-align: left;
  font-weight: 700;
  color: var(--text-muted);
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  border-bottom: 1px solid var(--border);
  white-space: nowrap;
}

.olap-table thead th.col-dim {
  color: var(--accent);
}

.olap-table tbody tr {
  transition: background 0.15s;
}

.olap-table tbody tr:hover {
  background: color-mix(in srgb, var(--color-table-hover) 60%, white);
}

.olap-table tbody tr.row-even {
  background: color-mix(in srgb, var(--bg-card) 88%, var(--palette-emphasis-150));
}

.olap-table tbody tr.row-important {
  background: color-mix(in srgb, var(--color-highlight-search) 75%, white);
}

.olap-table td {
  padding: 0.6rem 1rem;
  border-bottom: 1px solid rgba(255,255,255,0.04);
  white-space: nowrap;
}

.density-comfortable .olap-table td {
  padding: 0.75rem 1rem;
}

.density-compact .olap-table td {
  padding: 0.45rem 0.8rem;
}

.cell-dim {
  color: var(--text-primary);
  font-weight: 600;
}

.member-link {
  border: none;
  background: transparent;
  padding: 0;
  color: inherit;
  font: inherit;
  font-weight: 600;
  cursor: pointer;
  text-align: left;
}

.member-link:hover {
  color: var(--accent);
  text-decoration: underline;
}

.cell-value { text-align: right; }

.numeric {
  color: var(--color-brand-primary);
  font-variant-numeric: tabular-nums;
  font-weight: 500;
  font-family: "JetBrains Mono", "Fira Code", Consolas, monospace;
}

/* MDX debug */
.mdx-debug, .mdx-panel {
  margin: 0 1.5rem 1.5rem;
  border: 1px solid var(--border);
  border-radius: 8px;
  overflow: hidden;
}

.mdx-debug summary, .mdx-panel summary {
  padding: 0.5rem 1rem;
  cursor: pointer;
  font-size: 0.8rem;
  color: var(--text-muted);
  background: var(--bg-input);
}

.mdx-code {
  padding: 1rem;
  font-size: 0.78rem;
  color: var(--color-interaction-active);
  background: rgba(0,0,0,0.3);
  overflow: auto;
  white-space: pre;
  margin: 0;
  max-height: 200px;
}

@media (max-width: 768px) {
  .table-header {
    padding: 1rem 1rem 0;
    gap: 0.75rem;
  }
  .table-meta {
    width: 100%;
    flex-wrap: wrap;
  }
  .table-scroll {
    padding: 0 1rem 1rem;
    max-height: 360px;
  }
  .mdx-debug,
  .mdx-panel {
    margin: 0 1rem 1rem;
  }
}

@media (max-width: 480px) {
  .table-scroll {
    padding: 0 0.75rem 0.75rem;
    max-height: 320px;
  }
  .olap-table {
    min-width: 420px;
    font-size: 0.8rem;
  }
}
</style>
