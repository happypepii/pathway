import { expect, test, type Page } from '@playwright/test'

async function createGoal(page: Page, title: string) {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: '把大目標，變成下一小步。' })).toBeVisible()
  await page.getByRole('button', { name: '新增目標', exact: true }).click()
  await page.getByLabel('目標名稱').fill(title)
  await page.getByLabel('補充說明').fill('A private learning goal')
  const targetDate = new Date()
  targetDate.setUTCFullYear(targetDate.getUTCFullYear() + 1)
  await page.getByLabel('目標日期').fill(targetDate.toISOString().slice(0, 10))
  await page.getByLabel('每週可投入時數').fill('7')
  await page.getByRole('button', { name: '儲存目標', exact: true }).click()
  await expect(page.getByRole('heading', { name: title, exact: true })).toBeVisible()
}

test('complete UI journey persists edits, isolates sessions, tracks and reopens tasks', async ({ page, browser }) => {
  await createGoal(page, '學會照顧陽台植物')
  await page.getByLabel('你目前的經驗').fill('完全沒有經驗')
  await page.getByRole('button', { name: '產生計畫預覽' }).click()
  await expect(page.getByRole('heading', { name: '確認之前，先調整成你的步調' })).toBeVisible()
  await page.getByLabel('任務名稱').first().fill('準備花盆與土壤')
  await page.getByLabel('預估小時').first().fill('2')
  await page.getByRole('button', { name: '移除 閱讀進階資料' }).click()
  await page.getByRole('button', { name: '確認並儲存計畫' }).click()
  await expect(page.getByRole('heading', { name: '你的下一小步' })).toBeVisible()
  await expect(page.getByText('已完成 0 / 2 項任務')).toBeVisible()
  await expect(page.locator('.next-action')).toHaveText('準備花盆與土壤')
  await page.getByRole('checkbox').first().check()
  await expect(page.getByText('已完成 1 / 2 項任務')).toBeVisible()
  await page.getByRole('checkbox').nth(1).check()
  await expect(page.getByRole('heading', { name: '你完成了這個目標！' })).toBeVisible()
  await page.getByRole('checkbox').first().uncheck()
  await expect(page.getByText('已完成 1 / 2 項任務')).toBeVisible()
  await page.reload()
  await page.getByRole('button', { name: /學會照顧陽台植物/ }).click()
  await expect(page.getByText('已完成 1 / 2 項任務')).toBeVisible()
  const cookies = await page.context().cookies()
  const session = cookies.find(c => c.name === '__Host-pathway_session')
  expect(session?.httpOnly).toBe(true)
  expect(session?.secure).toBe(true)
  expect(session?.sameSite).toBe('Lax')
  expect(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length }))).toEqual({ local: 0, session: 0 })

  const other = await browser.newContext({ ignoreHTTPSErrors: true })
  const otherPage = await other.newPage()
  await otherPage.goto(new URL(page.url()).origin)
  await expect(otherPage.getByRole('heading', { name: '從一個目標開始' })).toBeVisible()
  await other.close()

  await page.getByRole('button', { name: '編輯目標', exact: true }).click()
  await page.getByLabel('目標名稱').fill('陽台植物計畫')
  await page.getByRole('button', { name: '儲存目標', exact: true }).click()
  await expect(page.getByRole('heading', { name: '陽台植物計畫', exact: true })).toBeVisible()
  page.once('dialog', dialog => dialog.accept())
  await page.getByRole('button', { name: '刪除目標', exact: true }).click()
  await expect(page.getByRole('heading', { name: '從一個目標開始' })).toBeVisible()
})

test('provider error is shown and leaves goal editable without saved tasks', async ({ page }) => {
  await createGoal(page, '__PROVIDER_FAIL__')
  await page.getByRole('button', { name: '產生計畫預覽' }).click()
  await expect(page.getByRole('alert')).toContainText('規劃服務暫時無法使用')
  await expect(page.getByRole('button', { name: '產生計畫預覽' })).toBeEnabled()
  await expect(page.getByRole('checkbox')).toHaveCount(0)
})

test('navigation preserves the selected goal while planning answers are being saved', async ({ page }) => {
  await createGoal(page, '規劃期間保留目標')
  let releaseSave!: () => void
  let notifySaveStarted!: () => void
  const saveStarted = new Promise<void>(resolve => { notifySaveStarted = resolve })
  const saveReleased = new Promise<void>(resolve => { releaseSave = resolve })
  await page.route('**/planning-answers', async route => {
    notifySaveStarted()
    await saveReleased
    await route.continue()
  })
  await page.getByRole('button', { name: '產生計畫預覽' }).click()
  await saveStarted
  try {
    await expect(page.getByRole('button', { name: '← 所有目標' })).toBeDisabled()
    const brand = page.getByRole('link', { name: /Pathway/ })
    await expect(brand).toHaveAttribute('aria-disabled', 'true')
    // Dispatch a click even though assistive tools treat aria-disabled as disabled.
    await brand.evaluate(element => (element as HTMLAnchorElement).click())
    await expect(page.getByRole('heading', { name: '規劃期間保留目標', exact: true })).toBeVisible()
  } finally {
    releaseSave()
  }
  await expect(page.getByRole('heading', { name: '確認之前，先調整成你的步調' })).toBeVisible()
  await page.getByRole('button', { name: '← 所有目標' }).click()
  await expect(page.getByRole('heading', { name: '把大目標，變成下一小步。' })).toBeVisible()
})

test('a stale preview fails confirmation and preserves editable preview', async ({ page }) => {
  await createGoal(page, '版本衝突測試')
  const goalResponse = await page.request.get('/api/v1/goals')
  const goals = await goalResponse.json() as { id: string; title: string }[]
  const goal = goals.find(g => g.title === '版本衝突測試')!
  await page.getByRole('button', { name: '產生計畫預覽' }).click()
  await expect(page.getByRole('button', { name: '確認並儲存計畫' })).toBeVisible()
  const csrfResponse = await page.request.get('/api/v1/antiforgery')
  const csrf = await csrfResponse.json() as { requestToken: string }
  const changed = await page.request.put('/api/v1/goals/' + goal.id + '/planning-answers', {
    headers: { 'X-CSRF-TOKEN': csrf.requestToken }, data: { experience: 'changed in another tab' },
  })
  expect(changed.status()).toBe(204)
  await page.getByRole('button', { name: '確認並儲存計畫' }).click()
  await expect(page.getByRole('alert')).toContainText('規劃條件已變更')
  await expect(page.getByRole('button', { name: '確認並儲存計畫' })).toBeEnabled()
})
