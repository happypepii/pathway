import { test } from 'node:test'
import assert from 'node:assert/strict'
import { api } from '../src/api.ts'

test('writes obtain CSRF once, send JSON with same-origin cookies, and reuse the in-memory token', async () => {
  const requests: { url: string; options?: RequestInit }[] = []
  globalThis.fetch = async (url, options) => {
    requests.push({ url: String(url), options })
    return requests.length === 1 ? Response.json({ requestToken: 'csrf-only' }) : new Response(null, { status: 204 })
  }
  await api('/tasks/a/completion', 'PATCH', { completed: true })
  await api('/goals/a', 'DELETE')
  assert.deepEqual(requests.map(x => x.url), ['/api/v1/antiforgery', '/api/v1/tasks/a/completion', '/api/v1/goals/a'])
  assert.ok(requests.every(x => x.options?.credentials === 'same-origin'))
  assert.equal(new Headers(requests[1]?.options?.headers).get('X-CSRF-TOKEN'), 'csrf-only')
  assert.equal(requests[1]?.options?.body, '{"completed":true}')
  assert.equal(new Headers(requests[2]?.options?.headers).get('X-CSRF-TOKEN'), 'csrf-only')
})

test('GET returns JSON without a CSRF bootstrap', async () => {
  let calls = 0
  globalThis.fetch = async (_, options) => { calls++; assert.equal(options?.method, 'GET'); return Response.json([{ title: 'Goal' }]) }
  assert.deepEqual(await api('/goals'), [{ title: 'Goal' }])
  assert.equal(calls, 1)
})

test('validation errors remain visible to the form', async () => {
  globalThis.fetch = async () => Response.json({ errors: { title: ['Title required'] } }, { status: 400 })
  await assert.rejects(api('/goals', 'POST', {}), /Title required/)
})

test('expired session does not silently replace credentials or retry writes', async () => {
  let calls = 0
  globalThis.fetch = async () => { calls++; return new Response(null, { status: 401 }) }
  await assert.rejects(api('/goals'), /匿名連線已失效/)
  assert.equal(calls, 1)
})

test('conflict and rate limit errors are surfaced without retry', async () => {
  globalThis.fetch = async () => Response.json({ detail: 'Preview is stale' }, { status: 409 })
  await assert.rejects(api('/goals/a/plan', 'POST', {}), /Preview is stale/)
  globalThis.fetch = async () => new Response(null, { status: 429 })
  await assert.rejects(api('/goals/a/plan-preview', 'POST'), /次數已達上限/)
})
