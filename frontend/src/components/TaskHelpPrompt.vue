<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { buildTaskPrompt, type TaskPromptContext } from '../taskPrompt'

const props = defineProps<TaskPromptContext>()
const includeDescription = ref(false)
const includeAnswers = ref(false)
const stuck = ref('')
const draft = ref('')
const status = ref('')
const output = ref<HTMLTextAreaElement | null>(null)
const hasAnswers = computed(() => ['experience', 'outcome', 'constraints'].some(key => props.answers?.[key]?.trim()))
const generated = computed(() => buildTaskPrompt(props, {
  includeDescription: includeDescription.value, includeAnswers: includeAnswers.value, stuck: stuck.value,
}))
watch(generated, value => { draft.value = value }, { immediate: true })
watch(draft, () => { status.value = '' })

async function copyPrompt() {
  try {
    await navigator.clipboard.writeText(draft.value)
    status.value = '已複製，可貼到你選擇的 LLM。'
  } catch {
    output.value?.focus()
    output.value?.select()
    status.value = '無法存取剪貼簿，已選取文字。請按 Ctrl+C 或 ⌘C 手動複製。'
  }
}
</script>

<template>
  <details class="task-help">
    <summary>這一步怎麼開始？</summary>
    <div class="task-help-body">
      <p>把這一步整理成求助 prompt，貼到你選擇的 LLM 繼續討論。內容只留在目前頁面，不會自動傳送、建立子目標或更改計畫。</p>
      <p class="help-context">已帶入：目標名稱、目前階段、任務說明與重點。</p>
      <label v-if="goal.description" class="help-option"><input v-model="includeDescription" type="checkbox">加入目標補充說明</label>
      <label v-if="hasAnswers" class="help-option"><input v-model="includeAnswers" type="checkbox">加入規劃回答（經驗、成果、限制）</label>
      <label>你卡住的地方（選填）<textarea v-model="stuck" maxlength="2000" rows="2" placeholder="例如：我想不到任何例子，不知道第一步要寫什麼。" /></label>
      <p class="help-context">你可以修改下方內容再複製。調整背景選項或卡住的地方，會重新產生 prompt。</p>
      <label>可複製的求助 prompt<textarea ref="output" v-model="draft" rows="12" spellcheck="false" /></label>
      <button type="button" :disabled="!draft.trim()" @click="copyPrompt">複製 prompt</button>
      <p v-if="status" role="status">{{ status }}</p>
    </div>
  </details>
</template>

<style scoped>
.task-help { margin: .8rem 0; border: 1px solid #d7e2d1; border-radius: 8px; background: #fff; overflow-wrap: anywhere; font-size: .85rem; }
.task-help summary { padding: .75rem; cursor: pointer; color: #315f3d; font-weight: 600; }
.task-help summary:focus-visible { outline: 3px solid #acccb5; outline-offset: 2px; }
.task-help-body { padding: 0 .85rem .85rem; display: grid; gap: .7rem; }
.task-help-body p { margin: 0; }
.help-context { font-size: .8rem; color: #6b7c72; }
.help-option { display: flex; align-items: center; gap: .5rem; }
.help-option input { width: 1.1rem; height: 1.1rem; flex-shrink: 0; accent-color: #296351; }
.task-help-body button { justify-self: start; }
.task-help-body textarea { min-width: 0; }
</style>
