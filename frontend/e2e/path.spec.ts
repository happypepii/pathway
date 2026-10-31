import { expect, test, type Page } from '@playwright/test'

// Deterministic UI fixture for the path; persistence still uses the real Backend.
// No live inference is needed to test layout, keyboard navigation or progress.
async function previewPath(page: Page, longTitle = false, includeGuidance = true) {
  const names = ['蒐集材料', '選擇盆器', '種下第一株植物', '觀察生長並調整照護']
  if (longTitle) names[0] = 'balcony'.repeat(25)
  await page.route('**/api/v1/goals/*/plan-preview', async route => {
    const goal = await (await page.request.get(route.request().url().replace('/plan-preview', ''))).json() as { goalVersion: string }
    const day = new Date().toISOString().slice(0, 10)
    await route.fulfill({ json: { goalVersion: goal.goalVersion,
      milestones: ['準備環境', '開始實作'].map((title, m) => ({ title,
        ...(includeGuidance ? { description: '了解每個步驟的用途。', outcome: '完成一個可觀察的小成果。',
          resources: [{ name: '植物照護入門指南', purpose: '了解植物需要的環境。', searchQuery: '陽台 植物 照護 入門' }] } : {}),
        tasks: names.slice(m * 2, m * 2 + 2).map(title => ({ title, estimatedHours: 1,
          ...(includeGuidance ? { description: '準備好材料，依步驟操作並確認結果。', keyPoints: ['先確認所需材料。', '完成後檢查結果。'] } : {}),
          scheduledStartDate: day, scheduledEndDate: day })) })),
      schedule: { totalEstimatedHours: 4, isFeasible: true, startDate: day, endDate: day, warnings: [] },
    } })
  })
  await page.goto('/')
  await page.getByRole('button', { name: '新增目標', exact: true }).click()
  await page.getByLabel('目標名稱').fill('路線圖驗收目標')
  await page.getByLabel('補充說明').fill('TEST_PRIVATE_GOAL_NOTES')
  const target = new Date()
  target.setUTCFullYear(target.getUTCFullYear() + 1)
  await page.getByLabel('目標日期').fill(target.toISOString().slice(0, 10))
  await page.getByRole('button', { name: '儲存目標', exact: true }).click()
  await page.getByLabel('你目前的經驗').fill('TEST_PRIVATE_EXPERIENCE')
  await page.getByLabel('完成時，你希望看到什麼具體成果').fill('TEST_PRIVATE_OUTCOME')
  await page.getByLabel('有哪些限制、偏好或可使用的資源').fill('TEST_PRIVATE_CONSTRAINTS')
  await page.getByRole('button', { name: '產生計畫預覽' }).click()
  const path = page.getByRole('navigation', { name: '計畫路線圖' })
  await expect(path).toBeVisible()
  return { path, names }
}

async function deleteTestGoal(page: Page) {
  page.once('dialog', dialog => dialog.accept())
  await page.getByRole('button', { name: '刪除目標', exact: true }).click()
  await expect(page.getByRole('heading', { name: '從一個目標開始' })).toBeVisible()
}

test('path includes each task, follows preview edits and saved cross-stage progress', async ({ page }) => {
  const { path, names } = await previewPath(page)
  await expect(path.getByRole('link')).toHaveCount(4)
  const guidance = page.locator('.milestone').first().locator('.plan-guidance').first()
  await expect(guidance).toContainText('了解每個步驟的用途。')
  await guidance.locator('summary').click()
  await expect(guidance).toContainText('尚未查證')
  await expect(guidance.getByRole('link')).toHaveAttribute('href',
    'https://www.google.com/search?q=' + encodeURIComponent('陽台 植物 照護 入門'))
  await expect(path.getByRole('link')).toHaveText(names.map(name => new RegExp(name)))
  await path.getByRole('link', { name: /選擇盆器/ }).click()
  await expect(page.locator('#preview-task-0-1')).toBeFocused()
  await page.locator('#preview-task-0-0').fill('調整後的第一步')
  await page.getByLabel('預估小時').first().fill('3')
  const first = path.getByRole('link', { name: /調整後的第一步/ })
  await expect(first).toContainText('3 小時')
  await expect(path.getByText('日期待重算', { exact: true })).toHaveCount(4)
  await page.getByRole('button', { name: '移除 選擇盆器', exact: true }).click()
  await expect(path.getByRole('link')).toHaveCount(3)
  await expect(path.getByRole('link', { name: /選擇盆器/ })).toHaveCount(0)
  await page.getByRole('button', { name: '確認並儲存計畫' }).click()
  await expect(page.getByRole('checkbox')).toHaveCount(3)
  await expect(path.locator('[aria-current="step"]')).toContainText('調整後的第一步')
  await path.getByRole('link', { name: /調整後的第一步/ }).click()
  await expect(page.getByRole('checkbox').first()).toBeFocused()
  await page.keyboard.press('Space')
  await expect(path.getByRole('link', { name: /調整後的第一步/ })).toContainText('已完成')
  await expect(path.locator('[aria-current="step"]')).toContainText('種下第一株植物')
  await expect(page.locator('.milestone').first().locator('.guidance-description').first()).toHaveText('了解每個步驟的用途。')
  await expect(page.locator('.saved-task-block').first().locator('.guidance-description')).toHaveText('準備好材料，依步驟操作並確認結果。')
  await expect(path.getByText('階段 1 · 已完成', { exact: true })).toBeVisible()
  if (process.env.PATHWAY_PATH_SCREENSHOTS === '1') {
    await page.locator('.guidance-resources').first().locator('summary').click()
    await page.locator('.milestone').first().screenshot({ path: '/tmp/pathway-guidance-stage.png' })
    await page.screenshot({ path: '/tmp/pathway-path-desktop.png', fullPage: true })
  }
  await page.reload()
  await page.getByRole('button', { name: /路線圖驗收目標/ }).click()
  await expect(path.getByRole('link')).toHaveCount(3)
  await expect(path.locator('[aria-current="step"]')).toContainText('種下第一株植物')
  await page.getByRole('checkbox').nth(1).check()
  await expect(path.locator('[aria-current="step"]')).toContainText('觀察生長並調整照護')
  await page.getByRole('checkbox').nth(2).check()
  await expect(path.locator('[aria-current="step"]')).toHaveCount(0)
  await expect(path.getByText(/每一小步都完成了/)).toBeVisible()
  await page.getByRole('checkbox').first().uncheck()
  await expect(path.locator('[aria-current="step"]')).toContainText('調整後的第一步')
  await deleteTestGoal(page)
})

test('mobile path wraps long task titles and keyboard selection focuses the editor', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  const { path } = await previewPath(page, true)
  await expect(path.getByRole('link')).toHaveCount(4)
  const stages = path.locator('.path-stage')
  const firstStage = await stages.nth(0).boundingBox()
  const secondStage = await stages.nth(1).boundingBox()
  expect(firstStage).not.toBeNull()
  expect(secondStage!.y).toBeGreaterThan(firstStage!.y + firstStage!.height)
  for (const node of await path.getByRole('link').all()) {
    const box = await node.boundingBox()
    expect(box!.x).toBeGreaterThanOrEqual(0)
    expect(box!.x + box!.width).toBeLessThanOrEqual(390)
  }
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true)
  await path.getByRole('link').first().focus()
  await page.keyboard.press('Enter')
  await expect(page.locator('#preview-task-0-0')).toBeFocused()
  if (process.env.PATHWAY_PATH_SCREENSHOTS === '1') {
    await page.screenshot({ path: '/tmp/pathway-path-mobile.png', fullPage: true })
  }
  await deleteTestGoal(page)
})

test('legacy plan without guidance remains usable after confirmation and reload', async ({ page }) => {
  const { path } = await previewPath(page, false, false)
  await expect(page.locator('.plan-guidance')).toHaveCount(0)
  await page.getByRole('button', { name: '確認並儲存計畫' }).click()
  await expect(page.getByRole('checkbox')).toHaveCount(4)
  await page.reload()
  await page.getByRole('button', { name: /路線圖驗收目標/ }).click()
  await expect(path.getByRole('link')).toHaveCount(4)
  await expect(page.locator('.plan-guidance')).toHaveCount(0)
  await deleteTestGoal(page)
})

test('task help selects context, copies an editable prompt locally and works after confirmation', async ({ page, context }) => {
  await previewPath(page)
  const help = page.locator('.preview-task').first().locator('.task-help')
  const apiRequests: string[] = []
  page.on('request', request => { if (request.url().includes('/api/v1/')) apiRequests.push(request.method()) })
  await help.locator('summary').click()
  const output = help.getByLabel('可複製的求助 prompt')
  await expect(output).toHaveValue(/路線圖驗收目標/)
  await expect(output).toHaveValue(/準備環境/)
  await expect(output).toHaveValue(/蒐集材料/)
  await expect(output).not.toHaveValue(/TEST_PRIVATE_/)
  await help.getByLabel('加入目標補充說明').check()
  await expect(output).toHaveValue(/TEST_PRIVATE_GOAL_NOTES/)
  await help.getByLabel('加入規劃回答').check()
  await expect(output).toHaveValue(/TEST_PRIVATE_EXPERIENCE/)
  await expect(output).toHaveValue(/TEST_PRIVATE_OUTCOME/)
  await expect(output).toHaveValue(/TEST_PRIVATE_CONSTRAINTS/)
  await help.getByLabel('加入目標補充說明').uncheck()
  await help.getByLabel('加入規劃回答').uncheck()
  await expect(output).not.toHaveValue(/TEST_PRIVATE_/)
  await help.getByLabel('你卡住的地方').fill('我想不到第一個例子。')
  await expect(output).toHaveValue(/我想不到第一個例子。/)
  await output.fill('我編輯過的求助 prompt')
  await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: new URL(page.url()).origin })
  await help.getByRole('button', { name: '複製 prompt', exact: true }).click()
  await expect(help.getByRole('status')).toContainText('已複製')
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('我編輯過的求助 prompt')
  expect(apiRequests).toEqual([])
  await page.getByRole('button', { name: '確認並儲存計畫' }).click()
  await expect(page.locator('.saved-task-block')).toHaveCount(4)
  const savedHelp = page.locator('.saved-task-block').first().locator('.task-help')
  await savedHelp.locator('summary').click()
  await expect(savedHelp.getByLabel('可複製的求助 prompt')).toHaveValue(/蒐集材料/)
  await expect(savedHelp.getByLabel('可複製的求助 prompt')).not.toHaveValue(/TEST_PRIVATE_/)
  await deleteTestGoal(page)
})

test('mobile legacy task help supports manual copying when clipboard access fails', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await previewPath(page, false, false)
  await page.evaluate(() => {
    Object.defineProperty(navigator, 'clipboard', { configurable: true,
      value: { writeText: async () => { throw new Error('Clipboard denied for test') } } })
  })
  const help = page.locator('.preview-task').first().locator('.task-help')
  await help.locator('summary').focus()
  await page.keyboard.press('Enter')
  const output = help.getByLabel('可複製的求助 prompt')
  await expect(output).toHaveValue(/蒐集材料/)
  await help.getByRole('button', { name: '複製 prompt', exact: true }).click()
  await expect(output).toBeFocused()
  await expect(help.getByRole('status')).toContainText('手動複製')
  expect(await output.evaluate(node => {
    const textarea = node as HTMLTextAreaElement
    return textarea.selectionEnd - textarea.selectionStart === textarea.value.length
  })).toBe(true)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true)
  await deleteTestGoal(page)
})
