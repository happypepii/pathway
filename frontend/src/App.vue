<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ensureSession } from './session'

const status = ref('正在連線…')
const failed = ref(false)
const loading = ref(false)
async function connect() {
  loading.value = true
  failed.value = false
  status.value = '正在連線…'
  try {
    await ensureSession()
    status.value = '已準備好開始規劃。'
  } catch (error) {
    failed.value = true
    status.value = error instanceof Error ? error.message : '連線失敗，請稍後重試。'
  } finally {
    loading.value = false
  }
}
onMounted(connect)
</script>

<template>
  <main>
    <h1>Pathway</h1>
    <p>Turn overwhelming goals into actionable plans.</p>
    <p role="status">{{ status }}</p>
    <button v-if="failed" :disabled="loading" @click="connect">重試</button>
    <p>目前以匿名方式使用；清除 Cookie 或更換裝置後，可能無法存取原有資料。</p>
  </main>
</template>
