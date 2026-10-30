// Credentials remain in HttpOnly cookies; the CSRF request token stays in memory.
export async function ensureSession(): Promise<void> {
  const current = await fetch('/api/v1/sessions/me', { credentials: 'same-origin', cache: 'no-store' })
  if (current.ok) return
  if (current.status !== 401) throw new Error('無法檢查連線，請稍後重試。')

  const csrf = await fetch('/api/v1/antiforgery', { credentials: 'same-origin', cache: 'no-store' })
  if (!csrf.ok) throw new Error('無法初始化安全連線，請稍後重試。')
  const { requestToken } = await csrf.json() as { requestToken: string }
  const created = await fetch('/api/v1/sessions/anonymous', {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'X-CSRF-TOKEN': requestToken },
  })
  if (created.status === 429) throw new Error('此網路的連線建立次數已達上限，請稍後重試。')
  if (!created.ok) throw new Error('無法建立連線，請稍後重試。')
}
