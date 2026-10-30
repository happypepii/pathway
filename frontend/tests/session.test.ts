import { test } from 'node:test'
import assert from 'node:assert/strict'
import { ensureSession } from '../src/session.ts'

test('valid session uses only GET and no POST quota', async () => {
  const requests: string[] = []
  globalThis.fetch = async (url) => {
    requests.push(String(url))
    return new Response(null, { status: 200 })
  }
  await ensureSession()
  assert.deepEqual(requests, ['/api/v1/sessions/me'])
})

test('first visit checks session then obtains CSRF and posts with same-origin credentials', async () => {
  const requests: { url: string; options?: RequestInit }[] = []
  globalThis.fetch = async (url, options) => {
    requests.push({ url: String(url), options })
    if (requests.length === 1) return new Response(null, { status: 401 })
    if (requests.length === 2) return Response.json({ requestToken: 'csrf-request-token' })
    return new Response(null, { status: 201 })
  }
  await ensureSession()
  assert.deepEqual(requests.map(r => r.url), [
    '/api/v1/sessions/me', '/api/v1/antiforgery', '/api/v1/sessions/anonymous',
  ])
  assert.ok(requests.every(r => r.options?.credentials === 'same-origin'))
  assert.equal(requests[2]?.options?.method, 'POST')
  assert.deepEqual(requests[2]?.options?.headers, { 'X-CSRF-TOKEN': 'csrf-request-token' })
})

test('non-401 session errors never trigger creation', async () => {
  let calls = 0
  globalThis.fetch = async () => { calls++; return new Response(null, { status: 503 }) }
  await assert.rejects(ensureSession(), /無法檢查/)
  assert.equal(calls, 1)
})

test('rate limiting is shown without automatic retries', async () => {
  let calls = 0
  globalThis.fetch = async () => {
    calls++
    if (calls === 1) return new Response(null, { status: 401 })
    if (calls === 2) return Response.json({ requestToken: 'csrf' })
    return new Response(null, { status: 429 })
  }
  await assert.rejects(ensureSession(), /次數已達上限/)
  assert.equal(calls, 3)
})

test('CSRF bootstrap failure prevents session POST', async () => {
  let calls = 0
  globalThis.fetch = async () => new Response(null, { status: ++calls === 1 ? 401 : 500 })
  await assert.rejects(ensureSession(), /安全連線/)
  assert.equal(calls, 2)
})
