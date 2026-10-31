<script setup lang="ts">
import { computed } from 'vue'
import type { Milestone } from '../api'

const props = defineProps<{
  goalTitle: string
  milestones: Milestone[]
  preview?: boolean
  datesStale?: boolean
}>()

const stages = computed(() => {
  let step = 0
  let nextFound = false
  return props.milestones.map((milestone, m) => ({
    title: milestone.title || '未命名階段',
    complete: milestone.tasks.length > 0 && milestone.tasks.every(task => task.completed),
    tasks: milestone.tasks.map((task, t) => {
      let state = props.preview ? 'preview' : task.completed ? 'complete' : 'pending'
      if (!props.preview && !task.completed && !nextFound) {
        state = 'next'
        nextFound = true
      }
      return { ...task, step: ++step, state,
        target: props.preview ? `preview-task-${m}-${t}` : `saved-task-${task.id}` }
    }),
  }))
})
const tasks = computed(() => stages.value.flatMap(stage => stage.tasks))
const completed = computed(() => tasks.value.filter(task => task.completed).length)
const stateLabels: Record<string, string> = {
  preview: '尚未儲存', complete: '已完成', next: '下一步', pending: '待完成',
}

function focusTask(target: string) {
  const control = document.getElementById(target)
  if (!control) return
  control.scrollIntoView({ behavior: 'smooth', block: 'center' })
  control.focus({ preventScroll: true })
}
</script>

<template>
  <nav v-if="tasks.length" class="plan-path" aria-label="計畫路線圖">
    <div class="path-heading">
      <div><p class="path-eyebrow">YOUR PATH</p><h2>每一小步，都通往你的目標</h2></div>
      <span class="path-count">{{ stages.length }} 個階段 · {{ tasks.length }} 個步驟</span>
    </div>
    <p class="path-help">沿階段與任務順序前進，點選任務可{{ preview ? '調整內容' : '查看並更新進度' }}。<span class="path-scroll-hint">左右捲動可查看後續階段。</span></p>
    <div class="path-goal"><span aria-hidden="true">◎</span><div><small>你的目標</small><strong>{{ goalTitle }}</strong></div></div>
    <p v-if="preview" class="path-caption">{{ datesStale ? '預覽已修改，確認時會重新計算日期。' : '預覽尚未儲存，確認後才會建立計畫。' }}</p>
    <p v-else class="path-caption">路徑進度 {{ completed }} / {{ tasks.length }}<span v-if="completed === tasks.length"> · 每一小步都完成了！</span></p>
    <div class="path-scroll" tabindex="0" aria-label="依順序排列的階段與任務">
      <ol class="path-stages">
        <li v-for="(stage, m) in stages" :key="m" class="path-stage" :class="{ 'stage-complete': !preview && stage.complete }">
          <div class="path-stage-heading"><span class="path-stage-number">{{ m + 1 }}</span><div><small>階段 {{ m + 1 }}<span v-if="!preview && stage.complete"> · 已完成</span></small><h3>{{ stage.title }}</h3></div></div>
          <ol class="path-tasks">
            <li v-for="task in stage.tasks" :key="task.target" class="path-task">
              <a :href="'#' + task.target" class="path-node" :class="'is-' + task.state"
                :aria-current="task.state === 'next' ? 'step' : undefined"
                :aria-label="'第 ' + task.step + ' 步，' + task.title + '，' + stateLabels[task.state]"
                @click.prevent="focusTask(task.target)">
                <span class="path-marker" aria-hidden="true">{{ task.state === 'complete' ? '✓' : task.step }}</span>
                <span class="path-node-content"><small class="path-state">{{ stateLabels[task.state] }}</small><strong>{{ task.title || '未命名任務' }}</strong>
                  <small v-if="task.description" class="path-description">{{ task.description }}</small>
                  <small>{{ Number.isFinite(task.estimatedHours) && task.estimatedHours > 0 ? task.estimatedHours + ' 小時' : '時數待填' }}</small>
                  <small>{{ datesStale ? '日期待重算' : task.scheduledStartDate + ' → ' + task.scheduledEndDate }}</small>
                </span>
              </a>
            </li>
          </ol>
        </li>
      </ol>
    </div>
    <p class="path-caption">每個節點都是一個可執行的小步驟，連線依計畫順序串起整條路徑。</p>
  </nav>
</template>

<style scoped>
.plan-path { margin: 1.6rem 0; padding: 1.4rem; border: 1px solid #d8e3d6; border-radius: 14px; background: #f4f7f0; min-width: 0; }
.path-heading { display: flex; align-items: center; justify-content: space-between; gap: .8rem; flex-wrap: wrap; }
.path-heading h2 { margin: .15rem 0; }
.path-eyebrow { margin: 0; font-size: .7rem; letter-spacing: .16em; color: #55704e; }
.path-count { font-size: .8rem; color: #55704e; }
.path-help, .path-caption { font-size: .8rem; color: #617367; }
.path-goal { display: flex; align-items: center; gap: .8rem; padding: .8rem 1rem; border: 1px solid #b7ccaf; border-radius: 10px; background: #e8f0e1; overflow-wrap: anywhere; }
.path-goal > span { font-size: 1.9rem; }
.path-goal small, .path-goal strong { display: block; }
.path-goal small { color: #55704e; }
.path-scroll { overflow-x: auto; padding: .3rem .25rem 1rem; }
.path-scroll:focus-visible { outline: 2px solid #557d4d; outline-offset: 2px; }
.path-stages, .path-tasks { list-style: none; margin: 0; padding: 0; }
.path-stages { display: flex; gap: 2rem; align-items: flex-start; }
.path-stage { position: relative; flex: 0 0 16rem; min-width: 0; }
.path-stage:not(:last-child)::after { content: '→'; position: absolute; right: -1.65rem; top: 1.3rem; color: #73906c; font-size: 1.3rem; }
.path-stage-heading { display: flex; align-items: flex-start; gap: .65rem; min-height: 5.5rem; padding: .7rem; border-top: 3px solid #b4c9aa; background: #fff; border-radius: 8px; overflow-wrap: anywhere; }
.path-stage-heading > div { min-width: 0; }
.path-stage-heading h3 { font-size: .95rem; margin: .1rem 0; }
.path-stage-heading small { color: #617367; font-size: .7rem; }
.path-stage-number { flex-shrink: 0; display: grid; place-items: center; width: 1.65rem; height: 1.65rem; border-radius: 50%; background: #edf3e9; font-size: .8rem; }
.stage-complete .path-stage-heading { border-color: #427454; }
.path-tasks { padding: .6rem 0 0 .8rem; }
.path-task { position: relative; padding: .6rem 0 .1rem 1rem; border-left: 2px solid #c2d2bb; }
.path-task::before { content: ''; position: absolute; left: 0; top: 2.3rem; width: 1rem; height: 2px; background: #c2d2bb; }
.path-task:last-child { border-image: linear-gradient(to bottom, #c2d2bb 2.3rem, transparent 2.3rem) 1; }
.path-node { display: flex; align-items: flex-start; gap: .55rem; padding: .8rem; background: white; border: 1px solid #d8e2d3; border-radius: 9px; text-decoration: none; overflow-wrap: anywhere; }
.path-node:hover { border-color: #557d4d; background: #fcfef9; }
.path-node-content { display: grid; gap: .2rem; min-width: 0; }
.path-node strong { font-size: .85rem; font-weight: 600; }
.path-node small { font-size: .7rem; color: #617367; }
.path-node .path-state { font-weight: 600; }
.path-description { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.path-marker { display: grid; place-items: center; flex-shrink: 0; min-width: 1.4rem; height: 1.4rem; border-radius: 50%; background: #edf3e9; font-size: .7rem; }
.is-next { border: 2px solid #52794a; background: #edf5e7; }
.is-next .path-state { color: #315e32; }
.is-next .path-marker { background: #426d3d; color: white; }
.is-complete { background: #f0f4ee; }
.is-complete .path-marker { background: #d8e9d2; color: #315e32; }
@media (max-width: 600px) {
  .plan-path { padding: 1rem .7rem; }
  .path-stages { flex-direction: column; gap: 2rem; }
  .path-stage { flex: auto; width: 100%; }
  .path-stage:not(:last-child)::after { content: '↓'; top: auto; right: auto; left: 1rem; bottom: -1.85rem; }
  .path-stage-heading { min-height: 0; }
  .path-scroll-hint { display: none; }
}
</style>
