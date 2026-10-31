<script setup lang="ts">
import type { ResourceSuggestion } from '../api'

defineProps<{
  description?: string | null
  outcome?: string | null
  keyPoints?: string[]
  resources?: ResourceSuggestion[]
}>()
</script>

<template>
  <div v-if="description || outcome || keyPoints?.length || resources?.length" class="plan-guidance">
    <p v-if="description" class="guidance-description">{{ description }}</p>
    <p v-if="outcome" class="guidance-outcome"><strong>預期成果：</strong>{{ outcome }}</p>
    <div v-if="keyPoints?.length" class="guidance-points"><strong>重點</strong><ul><li v-for="(point, i) in keyPoints" :key="i">{{ point }}</li></ul></div>
    <details v-if="resources?.length" class="guidance-resources">
      <summary>參考資料與學習資源（{{ resources.length }}）</summary>
      <p class="resource-note">以下是 AI 建議的資源名稱與搜尋方向，尚未查證。請自行確認內容是否適合你的目標。</p>
      <ul><li v-for="(resource, i) in resources" :key="i">
        <strong>{{ resource.name }}</strong><p>{{ resource.purpose }}</p>
        <span>搜尋關鍵字：{{ resource.searchQuery }}</span>
        <a :href="'https://www.google.com/search?q=' + encodeURIComponent(resource.searchQuery)" target="_blank" rel="noopener noreferrer">用關鍵字搜尋 ↗</a>
      </li></ul>
    </details>
  </div>
</template>

<style scoped>
.plan-guidance { font-size: .85rem; line-height: 1.7; color: #405c4e; overflow-wrap: anywhere; }
.guidance-description { white-space: pre-wrap; margin: .6rem 0; }
.guidance-outcome { padding: .65rem .8rem; border-left: 3px solid #afc8a5; background: #eff5e9; border-radius: 4px; }
.guidance-points { margin: .6rem 0; }
.guidance-points ul { margin: .25rem 0; padding-left: 1.3rem; }
.guidance-resources { margin: .9rem 0; padding: .75rem; border: 1px solid #d7e2d1; border-radius: 8px; background: white; }
.guidance-resources summary { cursor: pointer; font-weight: 600; color: #315f3d; }
.guidance-resources ul { padding-left: 1.2rem; margin-bottom: 0; }
.guidance-resources li { padding: .5rem 0; }
.guidance-resources li p { margin: .25rem 0; }
.guidance-resources li span, .guidance-resources li a { display: block; }
.guidance-resources li a { margin-top: .3rem; color: #296351; }
.resource-note { color: #6b7c72; font-size: .8rem; }
</style>
