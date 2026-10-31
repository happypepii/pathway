import { readFileSync } from 'node:fs'
import { Agent } from 'node:https'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

export default defineConfig(({ command, isPreview }) => {
  const certPath = process.env.PATHWAY_DEV_CERT
  const keyPath = process.env.PATHWAY_DEV_KEY
  const port = Number(process.env.PATHWAY_DEV_PORT ?? 5173)
  const apiPort = Number(process.env.PATHWAY_API_PORT ?? 7040)
  if (![port, apiPort].every(value => Number.isInteger(value) && value > 0 && value <= 65535)) {
    throw new Error('Development ports must be integers from 1 to 65535.')
  }
  if (command === 'serve' && !isPreview && (!certPath || !keyPath)) {
    throw new Error('Set PATHWAY_DEV_CERT and PATHWAY_DEV_KEY to trusted localhost PEM files. See README.')
  }
  const cert = certPath ? readFileSync(certPath) : undefined
  return {
    plugins: [vue()],
    server: {
      host: 'localhost',
      port,
      strictPort: true,
      https: cert && keyPath ? { cert, key: readFileSync(keyPath) } : undefined,
      proxy: {
        '/api': {
          target: `https://localhost:${apiPort}`,
          changeOrigin: true,
          secure: true,
          agent: new Agent({ ca: cert }),
        },
      },
    },
  }
})
