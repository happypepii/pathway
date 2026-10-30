let csrfToken: string | undefined

export async function api<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}
  if (method !== 'GET') {
    if (!csrfToken) {
      const response = await fetch('/api/v1/antiforgery', { credentials: 'same-origin', cache: 'no-store' })
      if (!response.ok) throw new Error('無法取得安全憑證，請重新整理後重試。')
      csrfToken = ((await response.json()) as { requestToken: string }).requestToken
    }
    headers['X-CSRF-TOKEN'] = csrfToken
  }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const response = await fetch('/api/v1' + path, {
    method, headers, credentials: 'same-origin', cache: 'no-store',
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  })
  if (!response.ok) {
    if (response.status === 401) throw new Error('匿名連線已失效，請重新整理。清除 Cookie 後可能無法找回原有目標。')
    if (response.status === 429) throw new Error('請求次數已達上限，請稍後再試。')
    const problem = await response.json().catch(() => ({})) as { detail?: string; errors?: Record<string, string[]> }
    throw new Error(problem.errors ? Object.values(problem.errors).flat().join(' ') : problem.detail ?? '操作失敗，請稍後重試。')
  }
  if (response.status === 204) return undefined as T
  return await response.json() as T
}

export interface Goal {
  id: string
  title: string
  description: string | null
  targetDate: string
  availableHoursPerWeek: number
  timeZoneId: string
  status: string
  progress?: number
  nextTask?: { id: string; title: string } | null
}

export interface Task {
  id?: string
  title: string
  estimatedHours: number
  scheduledStartDate: string
  scheduledEndDate: string
  completed?: boolean
}
export interface Milestone { id?: string; title: string; tasks: Task[] }
export interface Schedule { totalEstimatedHours: number; isFeasible: boolean; startDate: string; endDate: string; warnings: string[] }
export interface Preview { milestones: Milestone[]; schedule: Schedule; goalVersion: string }
export interface GoalDetail extends Goal {
  goalVersion: string
  questions: { key: string; text: string }[]
  answers: Record<string, string | null>
  milestones: Milestone[]
  totalTasks: number
  completedTasks: number
  schedule?: Schedule | null
}
