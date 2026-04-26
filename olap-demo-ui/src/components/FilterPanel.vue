<template>
  <div class="filter-panel glass-card">
    <h3 class="panel-title">Bộ Lọc Dữ Liệu</h3>
    <p class="panel-subtitle">
      Phiên bản mới đã chuyển sang điều hướng trực tiếp trên biểu đồ và bảng.
      Component này được giữ lại để tương thích giao diện cũ.
    </p>
  </div>
</template>

<script setup lang="ts">
</script>

<style scoped>
.filter-panel {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
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

.panel-subtitle {
  margin: -0.25rem 0 0.25rem;
  font-size: 0.78rem;
  line-height: 1.35;
  color: var(--text-muted);
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.filter-group label {
  font-size: 0.78rem;
  font-weight: 600;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.cube-info-box {
  border: 1px solid var(--border);
  border-radius: 10px;
  background: rgba(15, 23, 42, 0.45);
  padding: 0.7rem;
  display: grid;
  gap: 0.3rem;
}

.cube-info-title {
  font-size: 0.8rem;
  font-weight: 700;
  color: var(--text-primary);
}

.cube-info-line {
  font-size: 0.74rem;
  color: var(--text-muted);
}

.cube-info-desc {
  margin-top: 0.2rem;
  font-size: 0.74rem;
  color: var(--text-primary);
}

.select-input {
  background: var(--bg-input);
  border: 1px solid var(--border);
  border-radius: 8px;
  color: var(--text-primary);
  padding: 0.5rem 0.75rem;
  padding-right: 2rem;
  font-size: 0.9rem;
  font-family: inherit;
  cursor: pointer;
  transition: border-color 0.2s, box-shadow 0.2s;
  width: 100%;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.select-input:focus {
  outline: none;
  border-color: var(--accent);
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.15);
}

/* Native dropdown list often uses light background on Windows.
   Force dark text for readability inside the expanded option list. */
.select-input option {
  color: #0f172a;
  background: #ffffff;
}

.toggle-row {
  flex-direction: row;
  align-items: center;
  justify-content: space-between;
}

/* Toggle switch */
.toggle {
  position: relative;
  display: inline-block;
  width: 44px;
  height: 24px;
}

.toggle input { opacity: 0; width: 0; height: 0; }
.toggle input:disabled + .slider {
  opacity: 0.5;
  cursor: not-allowed;
}

.slider {
  position: absolute;
  inset: 0;
  background: var(--border);
  border-radius: 24px;
  cursor: pointer;
  transition: 0.3s;
}

.slider::before {
  content: '';
  position: absolute;
  width: 18px; height: 18px;
  left: 3px; bottom: 3px;
  background: #fff;
  border-radius: 50%;
  transition: 0.3s;
}

.toggle input:checked + .slider { background: var(--accent); }
.toggle input:checked + .slider::before { transform: translateX(20px); }

.level-badge {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.6rem 1rem;
  border-radius: 8px;
  font-size: 0.85rem;
  margin-top: 0.5rem;
}

.level-dot {
  width: 8px; height: 8px;
  border-radius: 50%;
}

.level-nam  { background: rgba(99, 102, 241, 0.15); color: #818cf8; }
.level-nam  .level-dot { background: #818cf8; }
.level-quy  { background: rgba(34, 211, 238, 0.15); color: #22d3ee; }
.level-quy  .level-dot { background: #22d3ee; }
.level-thang{ background: rgba(52, 211, 153, 0.15); color: #34d399; }
.level-thang .level-dot { background: #34d399; }

.governance-box {
  margin-top: 0.35rem;
  border-radius: 10px;
  border: 1px solid rgba(148, 163, 184, 0.35);
  background: rgba(15, 23, 42, 0.45);
  padding: 0.7rem;
}

.gov-title {
  font-size: 0.75rem;
  color: var(--text-primary);
  margin-bottom: 0.35rem;
  font-weight: 600;
}

.gov-list {
  margin: 0;
  padding-left: 1rem;
  display: grid;
  gap: 0.25rem;
  color: var(--text-muted);
  font-size: 0.74rem;
}

.gov-list li.done {
  color: #86efac;
}

@media (max-width: 768px) {
  .filter-panel {
    padding: 1rem;
    gap: 0.85rem;
  }
}
</style>
