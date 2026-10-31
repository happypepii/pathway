import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { expect, test } from '@playwright/test'
import type { GoalDetail, Preview } from '../src/api'

// Explicit live mode only; uses the real registered adapter and local Ollama.
test('real Qwen preview, browser confirmation, PostgreSQL persistence and reload', async ({ page, browser }) => {
  test.setTimeout(180_000)
  const project = process.env.PATHWAY_E2E_PROJECT
  if (!project || !/^pathway-e2e-[0-9a-f-]+$/.test(project)) throw new Error('Disposable test project required')
  const title = 'MVP acceptance: learn balcony gardening'
  await page.goto('/')
  await page.getByRole('button', { name: '新增目標', exact: true }).click()
  await page.getByLabel('目標名稱').fill(title)
  await page.getByLabel('補充說明').fill('Synthetic acceptance goal: grow three suitable plants on a small balcony.')
  const target = new Date()
  target.setUTCFullYear(target.getUTCFullYear() + 1)
  await page.getByLabel('目標日期').fill(target.toISOString().slice(0, 10))
  await page.getByLabel('每週可投入時數').fill('7')
  const createdResponse = page.waitForResponse(r => r.url().endsWith('/api/v1/goals') && r.request().method() === 'POST')
  await page.getByRole('button', { name: '儲存目標', exact: true }).click()
  const created = await (await createdResponse).json() as GoalDetail
  expect(created.id).toMatch(/^[0-9a-f-]{36}$/)
  const goalPath = '/api/v1/goals/' + created.id
  function database() {
    const sql = `SELECT json_build_object('status', g.status, 'answers',
      (SELECT count(*) FROM planning_answers a WHERE a.goal_id=g.id AND a.answer IS NOT NULL),
      'milestones', (SELECT count(*) FROM milestones m WHERE m.goal_id=g.id),
      'stageGuidance', COALESCE((SELECT json_agg(json_build_object('description', m.description,
      'outcome', m.outcome, 'resources', m.resources_json) ORDER BY m.sort_order)
      FROM milestones m WHERE m.goal_id=g.id), '[]'::json),
      'tasks', COALESCE((SELECT json_agg(json_build_object('title', t.title, 'minutes', t.estimated_minutes,
      'description', t.description, 'keyPoints', t.key_points,
      'start', t.scheduled_start_date, 'end', t.scheduled_end_date) ORDER BY m.sort_order,t.sort_order)
      FROM tasks t JOIN milestones m ON t.milestone_id=m.id WHERE m.goal_id=g.id), '[]'::json))
      FROM goals g WHERE g.id='${created.id}'::uuid;`
    return JSON.parse(execFileSync('docker', ['compose', '-p', project!, '-f', 'compose.test.yaml',
      'exec', '-T', 'postgres-test', 'psql', '-U', 'pathway_tests', '-d', 'pathway_tests', '-At', '-c', sql],
      { cwd: fileURLToPath(new URL('../../', import.meta.url)), encoding: 'utf8' }).trim()) as {
        status: string; answers: number; milestones: number;
        stageGuidance: unknown[];
        tasks: { title: string; minutes: number; start: string; end: string; description: string; keyPoints: string[] }[]
      }
  }
  await page.getByLabel('你目前的經驗').fill('Beginner, no gardening experience.')
  await page.getByLabel('完成時，你希望看到什麼具體成果').fill('Keep three suitable plants healthy for eight weeks.')
  await page.getByLabel('有哪些限制、偏好或可使用的資源').fill('Two hours of morning sunlight; inexpensive supplies.')
  const started = Date.now()
  const previewResponse = page.waitForResponse(r => r.url().endsWith(goalPath + '/plan-preview'), { timeout: 110_000 })
  await page.getByRole('button', { name: '產生計畫預覽' }).click()
  const response = await previewResponse
  expect(response.status(), 'real provider preview HTTP status').toBe(200)
  const preview = await response.json() as Preview
  const previewMs = Date.now() - started
  await expect(page.getByRole('heading', { name: '確認之前，先調整成你的步調' })).toBeVisible()
  expect(preview.milestones.length).toBeGreaterThan(0)
  const tasks = preview.milestones.flatMap(m => m.tasks)
  expect(tasks.length).toBeGreaterThan(0)
  expect(tasks.length).toBeLessThanOrEqual(100)
  const path = page.getByRole('navigation', { name: '計畫路線圖' })
  await expect(path.getByRole('link')).toHaveCount(tasks.length)
  for (const stage of preview.milestones) {
    expect(stage.description?.trim().length).toBeGreaterThan(0)
    expect(stage.outcome?.trim().length).toBeGreaterThan(0)
    expect(stage.resources?.length).toBeGreaterThan(0)
    expect(stage.resources!.length).toBeLessThanOrEqual(2)
    for (const resource of stage.resources!) {
      expect(resource.name.trim().length).toBeGreaterThan(0)
      expect(resource.purpose.trim().length).toBeGreaterThan(0)
      expect(resource.searchQuery.trim().length).toBeGreaterThan(0)
    }
  }
  for (const task of tasks) {
    expect(task.title.trim().length).toBeGreaterThan(0)
    expect(Number.isInteger(task.estimatedHours)).toBe(true)
    expect(task.estimatedHours).toBeGreaterThan(0)
    expect(task.scheduledEndDate >= task.scheduledStartDate).toBe(true)
    expect(task.description?.trim().length).toBeGreaterThan(0)
    expect(task.keyPoints?.length).toBeGreaterThan(0)
  }
  expect(database()).toEqual({ status: 'Draft', answers: 3, milestones: 0, stageGuidance: [], tasks: [] })
  await page.getByLabel('任務名稱', { exact: true }).first().fill('MVP acceptance edited task')
  await page.getByLabel('預估小時').first().fill('2')
  const confirmedResponse = page.waitForResponse(r => r.url().endsWith(goalPath + '/plan') && r.request().method() === 'POST')
  await page.getByRole('button', { name: '確認並儲存計畫' }).click()
  const confirmed = await confirmedResponse
  expect(confirmed.status()).toBe(200)
  const saved = await confirmed.json() as GoalDetail
  const savedTasks = saved.milestones.flatMap(m => m.tasks)
  expect(savedTasks).toHaveLength(tasks.length)
  expect(savedTasks[0]?.title).toBe('MVP acceptance edited task')
  expect(database()).toEqual({ status: 'Active', answers: 3, milestones: saved.milestones.length,
    stageGuidance: saved.milestones.map(m => ({ description: m.description, outcome: m.outcome, resources: m.resources })),
    tasks: savedTasks.map(t => ({ title: t.title, minutes: t.estimatedHours * 60,
      description: t.description, keyPoints: t.keyPoints,
      start: t.scheduledStartDate, end: t.scheduledEndDate })) })
  await expect(page.getByRole('checkbox')).toHaveCount(tasks.length)
  await page.reload()
  await page.getByRole('button', { name: new RegExp(title) }).click()
  await expect(page.getByRole('checkbox')).toHaveCount(tasks.length)
  await expect(page.locator('.next-action')).toHaveText('MVP acceptance edited task')
  await expect(path.getByRole('link')).toHaveCount(tasks.length)
  await expect(path.locator('[aria-current="step"]')).toContainText('MVP acceptance edited task')
  const reread = await (await page.request.get(goalPath)).json() as GoalDetail
  expect(reread.milestones).toEqual(saved.milestones)
  await expect(page.locator('.saved-task-block').first().locator('.guidance-description')).toHaveText(savedTasks[0]!.description!)
  const resourcePanel = page.locator('.guidance-resources').first()
  await resourcePanel.locator('summary').click()
  await expect(resourcePanel).toContainText(saved.milestones[0]!.resources![0]!.name)
  const help = page.locator('.saved-task-block').first().locator('.task-help')
  await help.locator('summary').click()
  await expect(help.getByLabel('可複製的求助 prompt')).toHaveValue(/MVP acceptance edited task/)
  await expect(help.getByLabel('可複製的求助 prompt')).not.toHaveValue(/Beginner, no gardening experience/)
  await help.getByLabel('加入規劃回答').check()
  await expect(help.getByLabel('可複製的求助 prompt')).toHaveValue(/Beginner, no gardening experience/)
  await help.locator('summary').click()
  await page.getByRole('checkbox').first().check()
  await expect(page.getByText(`已完成 1 / ${tasks.length} 項任務`, { exact: false })).toBeVisible()
  await page.reload()
  await page.getByRole('button', { name: new RegExp(title) }).click()
  await expect(page.getByRole('checkbox').first()).toBeChecked()
  const other = await browser.newContext({ ignoreHTTPSErrors: true, baseURL: new URL(page.url()).origin })
  const otherPage = await other.newPage()
  await otherPage.goto('/')
  await expect(otherPage.getByRole('heading', { name: '從一個目標開始' })).toBeVisible()
  expect((await otherPage.request.get(goalPath)).status()).toBe(404)
  await other.close()
  const noCsrf = await page.request.post(goalPath + '/plan-preview')
  expect(noCsrf.status()).toBe(400)
  console.log(JSON.stringify({ provider: 'real local Ollama', model: 'qwen3:4b-instruct-2507-q4_K_M',
    previewMs, milestones: saved.milestones.length, tasks: tasks.length,
    databaseVerified: true, guidanceVerified: true, reloadVerified: true, completionVerified: true, ownershipVerified: true }))
})
