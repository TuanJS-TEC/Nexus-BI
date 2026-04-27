<template>
  <div class="admin-layout">
    <aside class="sidebar">
      <div class="brand">
        <div class="logo-icon">DWH</div>
        <div>
          <h1 class="brand-name">OLAP Studio</h1>
          <span class="version">v2.4.0</span>
        </div>
      </div>

      <div class="sidebar-section">
        <h3 class="section-title">Điều hướng trực tiếp</h3>
        <p class="nav-guide">Click vào một member trên biểu đồ hoặc bảng để drill xuống cấp chi tiết tiếp theo.</p>
        <p class="nav-guide">Hỗ trợ 3 hierarchy: Thời Gian (Năm→Quý→Tháng), Khách Hàng (Loại KH→Tên KH), Địa Điểm (Bang→Thành phố).</p>
      </div>

      <div class="sidebar-section metadata">
        <h3 class="section-title">Metadata</h3>
        <details open>
          <summary>Bảng dữ liệu</summary>
          <ul>
            <li>
              <Button
                class="cube-item"
                :class="{ active: store.currentFact === 'BanHang' }"
                text
                size="small"
                label="Bán Hàng"
                @click="store.selectFact('BanHang')"
              />
            </li>
            <li>
              <Button
                class="cube-item"
                :class="{ active: store.currentFact === 'TonKho' }"
                text
                size="small"
                label="Tồn Kho"
                @click="store.selectFact('TonKho')"
              />
            </li>
          </ul>
        </details>
        <details open>
          <summary>Measures</summary>
          <ul>
            <li v-for="measure in store.metadata?.Measures ?? []" :key="measure">
              <Button
                class="measure-item"
                :class="{ active: store.selectedMeasure === measure }"
                text
                size="small"
                :label="measure"
                @click="onSelectMeasure(measure)"
              />
            </li>
          </ul>
        </details>
      </div>

      <section class="stats-grid sidebar-stats">
        <Card v-for="stat in stats" :key="stat.label" class="stat-tile">
          <template #content>
            <span class="stat-label">{{ stat.label }}</span>
            <div class="stat-value">{{ stat.value }}</div>
          </template>
        </Card>
      </section>

      <div class="connection-status">
        <div class="status-indicator" :class="{ active: isConnected }"></div>
        <Chip
          :label="isConnected ? 'SSAS Connected' : 'Disconnected'"
          :icon="isConnected ? 'pi pi-check-circle' : 'pi pi-times-circle'"
          class="connection-chip"
          :class="{ connected: isConnected }"
        />
      </div>
    </aside>

    <main class="content-area">
      <header class="top-bar">
        <div class="top-left">
          <Button class="util-btn util-btn-home" label="Home" icon="pi pi-home" @click="onGoHome" />
          <div class="breadcrumb">Workspace / Sales Analysis / {{ store.selectedCube }}</div>
        </div>
        <div class="top-actions">
          <div class="utility-bar">
            <div class="density-toggle">
              <Button
                text
                size="small"
                :class="{ active: store.tableDensity === 'compact' }"
                label="Compact"
                @click="store.tableDensity = 'compact'"
              />
              <Button
                text
                size="small"
                :class="{ active: store.tableDensity === 'comfortable' }"
                label="Comfortable"
                @click="store.tableDensity = 'comfortable'"
              />
            </div>
            <Button class="util-btn" label="CSV" icon="pi pi-download" text @click="exportCsv" />
            <Button class="util-btn util-btn-primary" label="Export Excel" icon="pi pi-file-excel" @click="exportExcel" />
            <Button class="util-btn" label="PDF" icon="pi pi-file-pdf" text @click="exportPdf" />
          </div>
        </div>
      </header>

      <Message v-if="store.errorMessage" class="banner-message" severity="error" closable @close="store.errorMessage = ''">
        {{ store.errorMessage }}
      </Message>
      <Message v-else-if="successMessage" class="banner-message" severity="success">
        {{ successMessage }}
      </Message>
      <section class="operation-panel">
        <ActionButtons />
      </section>

      <div class="scroll-container">
        <details class="mdx-preview" v-if="store.resultData?.Mdx">
          <summary>MDX Preview</summary>
          <pre>{{ store.resultData.Mdx }}</pre>
        </details>

        <div class="data-grid">
          <Card class="shadow-card overview-card">
            <template #content>
              <OlapChart />
            </template>
          </Card>
          <Card class="shadow-card table-card">
            <template #content>
              <OlapTable />
            </template>
          </Card>
        </div>
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import Button from 'primevue/button'
import Card from 'primevue/card'
import Chip from 'primevue/chip'
import Message from 'primevue/message'
import ActionButtons from '@/components/ActionButtons.vue'
import OlapChart from '@/components/OlapChart.vue'
import OlapTable from '@/components/OlapTable.vue'
import { useOlapStore } from '@/stores/olapStore'

const store = useOlapStore()
const isConnected = ref(false)
onMounted(async () => {
  await store.loadCubeMappings()
  await store.loadMetadata()
  isConnected.value = !!store.metadata
  if (!store.resultData) {
    await store.loadDefaultQuery()
  }
})

const stats = computed(() => {
  const r = store.resultData
  const rows = r?.Rows.length ?? 0
  const totalRows = rows
  const freshness = store.lastResultAt ?? store.metadataLoadedAt
  const freshnessText = freshness ? freshness.toLocaleString('vi-VN') : '--'

  return [
    { label: 'Data Freshness', value: freshnessText },
    { label: 'Query Performance', value: store.lastQueryMs ? `${store.lastQueryMs} ms` : '--' },
    { label: 'Rows Processed', value: `${rows} / ${totalRows}` },
    { label: 'Operation', value: store.lastOperation || '--' },
  ]
})

const successMessage = computed(() => {
  if (store.isLoading || store.errorMessage || !store.lastOperation || !store.resultData?.Success) return ''
  return `Nạp dữ liệu thành công: ${store.lastOperation} (${store.resultData.Rows.length} dòng).`
})

async function onSelectMeasure(measure: string) {
  if (!measure || store.selectedMeasure === measure) return
  store.setSelectedMeasure(measure)
}

async function onGoHome() {
  await store.goHomeDashboard()
}

function exportCsv() {
  const result = store.resultData
  if (!result?.Rows?.length) return
  const headers = result.Columns
  const lines = [
    headers.join(','),
    ...result.Rows.map(row => headers.map(h => `"${String(row[h] ?? '').replace(/"/g, '""')}"`).join(',')),
  ]
  const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'olap-export.csv'
  a.click()
  URL.revokeObjectURL(url)
}

function exportExcel() {
  exportCsv()
}

function exportPdf() {
  window.print()
}
</script>

<style scoped>
.admin-layout {
  min-height: 100vh;
  display: flex;
  background: var(--bg-main);
  color: var(--text-main);
  font-family: Inter, Roboto, "Segoe UI", Arial, sans-serif;
}

.sidebar {
  width: 260px;
  background: var(--sidebar-bg);
  color: var(--text-on-dark);
  border-right: 1px solid rgba(154, 216, 114, 0.22);
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.logo-icon {
  width: 42px;
  height: 42px;
  border-radius: 10px;
  display: grid;
  place-items: center;
  background: color-mix(in srgb, var(--color-brand-primary) 35%, #ffffff);
  border: 1px solid color-mix(in srgb, var(--color-interaction-active) 45%, #0f172a);
  font-weight: 700;
  color: var(--text-on-brand);
}

.brand-name {
  margin: 0;
  font-size: 1rem;
  font-weight: 700;
}

.version {
  font-size: 0.75rem;
  color: color-mix(in srgb, var(--text-on-dark) 70%, #000000);
}

.sidebar-section {
  background: rgba(255, 255, 255, 0.06);
  border: 1px solid rgba(154, 216, 114, 0.25);
  border-radius: 12px;
  padding: 0.75rem;
  transition: backdrop-filter 180ms ease, background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease, transform 180ms ease;
}

.section-title {
  margin: 0 0 0.6rem;
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: color-mix(in srgb, var(--text-on-dark) 74%, #000000);
}

.nav-guide {
  margin: 0 0 0.5rem;
  font-size: 0.8rem;
  line-height: 1.45;
  color: color-mix(in srgb, var(--text-on-dark) 76%, #000000);
}

.metadata details {
  margin-bottom: 0.5rem;
}

.metadata {
  background: rgba(255, 255, 255, 0.2);
  border-radius: 16px;
  box-shadow: 0 4px 30px rgba(0, 0, 0, 0.1);
  backdrop-filter: blur(5px);
  -webkit-backdrop-filter: blur(5px);
  border: 1px solid rgba(255, 255, 255, 0.3);
}

.metadata summary {
  cursor: pointer;
  font-size: 0.82rem;
  color: #f8fafc;
  font-weight: 600;
  letter-spacing: 0.01em;
}

.metadata summary:hover {
  color: #d1fae5;
}

.metadata ul {
  margin: 0.35rem 0 0;
  padding-left: 1rem;
  max-height: 120px;
  overflow: auto;
  font-size: 0.79rem;
  color: #e2e8f0;
}

.metadata li:hover {
  color: #bbf7d0;
}

.metadata .section-title {
  color: #ecfeff;
}

.metadata :deep(.p-button) {
  color: #f8fafc;
  border-color: rgba(255, 255, 255, 0.28);
  background: rgba(255, 255, 255, 0.08);
}

.metadata :deep(.p-button:hover) {
  color: #052e16;
  background: rgba(187, 247, 208, 0.9);
  border-color: rgba(134, 239, 172, 0.9);
}

.metadata .cube-item.active,
.metadata .measure-item.active {
  color: #052e16;
  background: rgba(187, 247, 208, 0.92);
  border-color: rgba(74, 222, 128, 0.95);
  font-weight: 700;
}

.cube-item {
  width: 100%;
  text-align: left;
  border: 1px solid transparent;
  color: inherit;
  border-radius: 6px;
  justify-content: flex-start;
  padding: 0.2rem 0.35rem;
}

.cube-item:hover {
  border-color: color-mix(in srgb, var(--color-interaction-active) 35%, transparent);
  color: var(--color-interaction-active);
}

.cube-item.active {
  border-color: color-mix(in srgb, var(--color-interaction-active) 45%, var(--color-brand-primary));
  background: color-mix(in srgb, var(--color-brand-primary) 20%, transparent);
  color: var(--text-on-dark);
  font-weight: 600;
}

.measure-item {
  width: 100%;
  text-align: left;
  border: 1px solid transparent;
  color: inherit;
  border-radius: 6px;
  justify-content: flex-start;
  padding: 0.2rem 0.35rem;
}

.measure-item:hover {
  border-color: color-mix(in srgb, var(--color-interaction-active) 35%, transparent);
  color: var(--color-interaction-active);
}

.measure-item.active {
  border-color: color-mix(in srgb, var(--color-interaction-active) 45%, var(--color-brand-primary));
  background: color-mix(in srgb, var(--color-brand-primary) 20%, transparent);
  color: var(--text-on-dark);
  font-weight: 600;
}

.connection-status {
  margin-top: auto;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.connection-chip {
  background: color-mix(in srgb, var(--text-on-dark) 18%, transparent);
  color: var(--text-on-dark);
}

.connection-chip.connected {
  background: color-mix(in srgb, var(--color-status-connected) 24%, transparent);
  color: var(--text-on-dark);
}

.status-indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: color-mix(in srgb, var(--text-on-dark) 55%, #374151);
}

.status-indicator.active {
  background: var(--color-status-connected);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-status-connected) 30%, transparent);
}

.content-area {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.top-bar {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--border);
  background: var(--color-brand-header-bg);
  color: var(--text-on-brand);
}

.breadcrumb {
  font-size: 0.78rem;
  color: color-mix(in srgb, var(--text-on-brand) 82%, #000000);
}

.top-left {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.top-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.utility-bar {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.density-toggle {
  display: flex;
  border: 1px solid var(--border);
  border-radius: 8px;
  overflow: hidden;
}

.density-toggle :deep(.p-button) {
  border: none;
  border-radius: 0;
  background: #fff;
  padding: 0.35rem 0.6rem;
  font-size: 0.75rem;
  color: var(--text-muted);
  transition: backdrop-filter 180ms ease, background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease, transform 180ms ease;
}

.density-toggle :deep(.p-button.active) {
  background: var(--color-brand-primary);
  color: var(--text-on-brand);
}

.util-btn {
  border: 1px solid var(--border);
  border-radius: 8px;
  background: #fff;
  padding: 0.4rem 0.7rem;
  font-size: 0.75rem;
  color: var(--text-muted);
  transition: backdrop-filter 180ms ease, background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease, transform 180ms ease;
}

.util-btn-primary {
  background: var(--color-brand-primary);
  color: var(--text-on-brand);
  border-color: var(--color-brand-primary);
}

.util-btn-primary:hover {
  background: var(--color-brand-primary-hover);
  border-color: var(--color-brand-primary-hover);
}

.util-btn-home {
  background: var(--color-brand-primary);
  border-color: var(--color-brand-primary);
  color: #ffffff;
  font-weight: 600;
  padding: 0.5rem 0.95rem;
  font-size: 0.82rem;
  min-width: 86px;
}

.util-btn-home:hover {
  background: var(--color-brand-primary-hover);
  border-color: var(--color-brand-primary-hover);
}

.scroll-container {
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  overflow: auto;
}

.banner-message {
  margin: 0.75rem 1rem 0;
  font-size: 0.85rem;
}

.operation-panel {
  margin: 0.75rem 1rem 0;
}

.operation-panel :deep(.action-panel) {
  width: 100%;
  max-width: none;
  border-radius: 12px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.75rem;
}

.sidebar-stats {
  grid-template-columns: 1fr;
  gap: 0.6rem;
}

.sidebar-stats .stat-tile {
  background: rgba(255, 255, 255, 0.08);
  border-color: rgba(154, 216, 114, 0.25);
}

.sidebar-stats .stat-label {
  color: color-mix(in srgb, var(--text-on-dark) 74%, #000000);
}

.sidebar-stats .stat-value {
  color: var(--text-on-dark);
  font-size: 0.93rem;
}

.stat-tile {
  background: #fff;
  padding: 0.75rem;
  border: 1px solid var(--border);
  border-radius: 10px;
  box-shadow: var(--shadow-sm);
  transition: backdrop-filter 180ms ease, background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease, transform 180ms ease;
}

.stat-value {
  margin-top: 0.3rem;
  font-size: 1rem;
  font-weight: 600;
  font-family: "JetBrains Mono", "Fira Code", Consolas, monospace;
}

.stat-label {
  font-size: 0.75rem;
  color: var(--text-muted);
}

.mdx-preview {
  background: #fff;
  border: 1px solid var(--border);
  border-radius: 10px;
  box-shadow: var(--shadow-sm);
  padding: 0.6rem 0.7rem;
  transition: backdrop-filter 180ms ease, background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease, transform 180ms ease;
}

.mdx-preview summary {
  cursor: pointer;
  font-size: 0.8rem;
  color: var(--text-muted);
}

.mdx-preview pre {
  margin: 0.6rem 0 0;
  background: #1f2937;
  color: #f3f4f6;
  border-radius: 8px;
  padding: 0.7rem;
  overflow: auto;
  font-size: 0.73rem;
}

.data-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1rem;
}

.table-card {
  grid-column: 1 / -1;
}

.shadow-card {
  background: #fff;
  border-radius: 12px;
  border: 1px solid var(--border);
  box-shadow: var(--shadow-sm);
  overflow: hidden;
  transition: backdrop-filter 180ms ease, background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease, transform 180ms ease;
}

.sidebar-section:hover,
.stat-tile:hover,
.mdx-preview:hover,
.shadow-card:hover,
.util-btn:hover,
.density-toggle :deep(.p-button:hover) {
  background: color-mix(in srgb, #ffffff 70%, transparent);
  border-color: color-mix(in srgb, var(--color-interaction-active) 38%, var(--border));
  backdrop-filter: blur(10px) saturate(125%);
  -webkit-backdrop-filter: blur(10px) saturate(125%);
  box-shadow: 0 10px 22px -14px rgb(15 23 42 / 35%);
  transform: translateY(-1px);
}

@media (max-width: 1280px) {
  .sidebar {
    width: 230px;
  }
  .top-actions {
    flex-direction: column;
    align-items: flex-end;
  }
}

@media (max-width: 1024px) {
  .admin-layout {
    flex-direction: column;
  }
  .sidebar {
    width: 100%;
    border-right: none;
    border-bottom: 1px solid rgba(154, 216, 114, 0.3);
  }
}

@media (max-width: 768px) {
  .top-bar {
    flex-direction: column;
    align-items: flex-start;
  }
  .top-actions {
    width: 100%;
    align-items: stretch;
  }
  .utility-bar {
    flex-wrap: wrap;
  }
  .data-grid {
    grid-template-columns: 1fr;
  }
}
</style>
