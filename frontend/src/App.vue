<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ensureSession } from './session'
import { api, type Goal, type GoalDetail, type Preview, type Task } from './api'

const goals = ref<Goal[]>([])
const selected = ref<GoalDetail | null>(null)
const error = ref('')
const ready = ref(false)
const busy = ref(false)
const showForm = ref(false)
const editing = ref(false)
const preview = ref<Preview | null>(null)
const editedPreview = ref(false)
const answers = reactive({ experience: '', outcome: '', constraints: '' })
const previewHours = computed(() => preview.value?.milestones.flatMap(m => m.tasks).reduce((sum, t) => sum + t.estimatedHours, 0) ?? 0)
const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
const today = new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', day: '2-digit', timeZone: timezone }).format(new Date())
const form = reactive({ title: '', description: '', targetDate: today, availableHoursPerWeek: 7, timeZoneId: timezone })
async function run(action: () => Promise<void>) {
  if (busy.value) return
  busy.value = true
  error.value = ''
  try { await action() } catch (e) { error.value = e instanceof Error ? e.message : '操作失敗，請稍後重試。' }
  finally { busy.value = false }
}
async function refresh() { goals.value = await api<Goal[]>('/goals') }
async function initialize() {
  await ensureSession()
  await refresh()
  ready.value = true
}
function showGoals() {
  if (busy.value) return
  selected.value = null
  showForm.value = false
}
function newGoal() {
  Object.assign(form, { title: '', description: '', targetDate: today, availableHoursPerWeek: 7, timeZoneId: timezone })
  editing.value = false
  showForm.value = true
}
function editGoal() {
  if (!selected.value) return
  const goal = selected.value
  Object.assign(form, { title: goal.title, description: goal.description ?? '', targetDate: goal.targetDate,
    availableHoursPerWeek: goal.availableHoursPerWeek, timeZoneId: goal.timeZoneId })
  editing.value = true
  showForm.value = true
}
async function openGoal(id: string) {
  selected.value = await api<GoalDetail>('/goals/' + id)
  preview.value = null
  editedPreview.value = false
  Object.assign(answers, { experience: '', outcome: '', constraints: '' }, selected.value.answers)
  showForm.value = false
}
async function saveAnswers() {
  await api('/goals/' + selected.value!.id + '/planning-answers', 'PUT', answers)
}
async function generatePlan() {
  await saveAnswers()
  preview.value = await api<Preview>('/goals/' + selected.value!.id + '/plan-preview', 'POST')
  editedPreview.value = false
}
function removeTask(m: number, t: number) {
  preview.value!.milestones[m]!.tasks.splice(t, 1)
  preview.value!.milestones = preview.value!.milestones.filter(x => x.tasks.length > 0)
  editedPreview.value = true
}
async function confirmPlan() {
  const id = selected.value!.id
  await api('/goals/' + id + '/plan', 'POST', {
    goalVersion: preview.value!.goalVersion,
    milestones: preview.value!.milestones.map(m => ({ title: m.title,
      tasks: m.tasks.map(t => ({ title: t.title, estimatedHours: t.estimatedHours })) })),
  })
  await refresh()
  await openGoal(id)
}
function changeTask(event: Event, task: Task) {
  const checkbox = event.target as HTMLInputElement
  if (busy.value) { checkbox.checked = !!task.completed; return }
  void run(async () => {
    try { await toggleTask(task) }
    catch (error) { checkbox.checked = !!task.completed; throw error }
  })
}
async function toggleTask(task: Task) {
  await api('/tasks/' + task.id + '/completion', 'PATCH', { completed: !task.completed })
  await openGoal(selected.value!.id)
  await refresh()
}
async function saveGoal() {
  const goal = await api<Goal>(editing.value ? '/goals/' + selected.value!.id : '/goals', editing.value ? 'PATCH' : 'POST', form)
  await refresh()
  await openGoal(goal.id)
}
async function deleteGoal() {
  if (!selected.value || !window.confirm('確定刪除此目標及所有計畫與進度？此操作無法復原。')) return
  await api('/goals/' + selected.value.id, 'DELETE')
  selected.value = null
  await refresh()
}
onMounted(() => run(initialize))
</script>

<template>
  <div class="shell">
    <header><a class="brand" href="/" :aria-disabled="busy" @click.prevent="showGoals">Pathway<span>一步步，走向你想完成的事。</span></a><span class="badge">匿名工作空間</span></header>
    <main>
      <div v-if="error" role="alert" class="alert">{{ error }} <button v-if="!ready" :disabled="busy" @click="run(initialize)">重試連線</button></div>
      <p v-if="!ready && !error" role="status">正在開啟你的工作空間…</p>
      <template v-if="ready">
        <div class="heading"><div><p class="eyebrow">從想法到行動</p><h1>{{ selected ? selected.title : '把大目標，變成下一小步。' }}</h1></div><button v-if="!showForm" :disabled="busy" @click="newGoal">新增目標</button></div>
        <form v-if="showForm" class="card form" @submit.prevent="run(saveGoal)">
          <h2>{{ editing ? '編輯目標' : '你想完成什麼？' }}</h2>
          <label>目標名稱<input v-model="form.title" required maxlength="200" placeholder="例如：學會照顧陽台植物"></label>
          <label>補充說明（選填）<textarea v-model="form.description" maxlength="2000" rows="3" /></label>
          <div class="columns"><label>目標日期<input v-model="form.targetDate" type="date" :disabled="editing && selected?.status !== 'Draft'" :min="editing && selected?.status !== 'Draft' ? undefined : today" required></label><label>每週可投入時數<input v-model.number="form.availableHoursPerWeek" type="number" :disabled="editing && selected?.status !== 'Draft'" min="1" max="168" step="1" required></label></div>
          <p class="muted">日期依 {{ form.timeZoneId }} 計算。每週時間會平均分配到七天，作為排程估計。</p>
          <div class="actions"><button :disabled="busy" type="submit">{{ busy ? '儲存中…' : '儲存目標' }}</button><button type="button" class="secondary" :disabled="busy" @click="showForm = false">取消</button></div>
        </form>
        <section v-else-if="selected" class="card">
          <button class="link" :disabled="busy" @click="showGoals">← 所有目標</button>
          <p class="badge">{{ selected.status }}</p><p>{{ selected.description }}</p>
          <p>目標日期 {{ selected.targetDate }} · 每週 {{ selected.availableHoursPerWeek }} 小時</p>
          <div class="actions"><button class="secondary" :disabled="busy" @click="editGoal">編輯目標</button><button class="danger" :disabled="busy" @click="run(deleteGoal)">刪除目標</button></div>
          <div v-if="selected.status === 'Draft'" class="planning">
            <template v-if="!preview">
              <h2>讓計畫更適合你</h2>
              <p class="muted">以下問題都可以略過。AI 會根據你的目標提出建議，確認前不會儲存任務。</p>
              <form class="form" @submit.prevent="run(generatePlan)">
                <label v-for="question in selected.questions" :key="question.key">{{ question.text }}（選填）
                  <textarea v-model="answers[question.key as keyof typeof answers]" maxlength="2000" rows="2" />
                </label>
                <div class="actions"><button :disabled="busy" type="submit">{{ busy ? '正在規劃，最多約 90 秒…' : '產生計畫預覽' }}</button><button type="button" class="secondary" :disabled="busy" @click="run(saveAnswers)">儲存回答</button></div>
              </form>
            </template>
            <form v-else @submit.prevent="run(confirmPlan)">
              <h2>確認之前，先調整成你的步調</h2>
              <p>共 {{ previewHours }} 小時。你可以修改名稱、整數時數或移除任務。</p>
              <div v-if="!preview.schedule.isFeasible" class="alert" role="status">{{ preview.schedule.warnings.join(' ') }}</div>
              <p class="muted">{{ editedPreview ? '你已修改預覽；下列日期為原始估計，確認時伺服器會重新計算可行性與日期。' : '預計 ' + preview.schedule.startDate + ' 至 ' + preview.schedule.endDate + '。日期表示容量分配估計，不是每日預約。' }}</p>
              <div v-for="(milestone, m) in preview.milestones" :key="m" class="milestone">
                <label>階段名稱<input v-model="milestone.title" required maxlength="200" @input="editedPreview = true"></label>
                <div v-for="(task, t) in milestone.tasks" :key="t" class="preview-task">
                  <label>任務名稱<input v-model="task.title" required maxlength="200" @input="editedPreview = true"></label>
                  <label>預估小時<input v-model.number="task.estimatedHours" required type="number" min="1" max="1000" step="1" @input="editedPreview = true"></label>
                  <button type="button" class="danger" :disabled="busy" :aria-label="'移除 ' + task.title" @click="removeTask(m, t)">移除</button>
                  <small class="muted">{{ task.scheduledStartDate }} → {{ task.scheduledEndDate }}</small>
                </div>
              </div>
              <p v-if="preview.milestones.length === 0" class="alert">至少保留一項任務才能確認。</p>
              <div class="actions"><button type="submit" :disabled="busy || preview.milestones.length === 0">{{ busy ? '正在確認…' : '確認並儲存計畫' }}</button><button type="button" class="secondary" :disabled="busy" @click="preview = null">返回調整回答</button></div>
            </form>
          </div>
          <div v-else class="planning">
            <h2>{{ selected.status === 'Completed' ? '你完成了這個目標！' : '你的下一小步' }}</h2>
            <p v-if="selected.nextTask" class="next-action">{{ selected.nextTask.title }}</p>
            <p>已完成 {{ selected.completedTasks }} / {{ selected.totalTasks }} 項任務 · {{ selected.progress }}%</p>
            <progress :value="selected.progress" max="100" :aria-label="'目標完成度 ' + selected.progress + '%'" />
            <div v-if="selected.schedule && !selected.schedule.isFeasible" class="alert">{{ selected.schedule.warnings.join(' ') }}</div>
            <div v-for="milestone in selected.milestones" :key="milestone.id" class="milestone">
              <h3>{{ milestone.title }}</h3>
              <label v-for="task in milestone.tasks" :key="task.id" class="saved-task">
                <input type="checkbox" :checked="task.completed" :disabled="busy" @change="changeTask($event, task)">
                <span :class="{ done: task.completed }">{{ task.title }}<small>{{ task.estimatedHours }} 小時 · {{ task.scheduledStartDate }} → {{ task.scheduledEndDate }}</small></span>
              </label>
            </div>
          </div>
        </section>
        <section v-else>
          <div v-if="goals.length === 0" class="card empty"><h2>從一個目標開始</h2><p>寫下你想完成的事，接著一起拆成可行的小步驟。</p><button :disabled="busy" @click="newGoal">建立第一個目標</button></div>
          <div class="goal-grid"><button v-for="goal in goals" :key="goal.id" class="card goal" :disabled="busy" @click="run(() => openGoal(goal.id))"><span class="badge">{{ goal.status }}</span><h2>{{ goal.title }}</h2><p>{{ goal.targetDate }} · 每週 {{ goal.availableHoursPerWeek }} 小時</p><template v-if="goal.status !== 'Draft'"><progress :value="goal.progress" max="100" :aria-label="'完成度 ' + goal.progress + '%'" /><p>{{ goal.progress }}% 完成 · {{ goal.nextTask ? '下一步：' + goal.nextTask.title : '所有任務已完成' }}</p></template><span class="link">查看目標 →</span></button></div>
        </section>
      </template>
    </main>
    <footer>不需註冊，規劃屬於你的下一步。清除 Cookie 或更換裝置後，可能無法存取原有資料。</footer>
  </div>
</template>
