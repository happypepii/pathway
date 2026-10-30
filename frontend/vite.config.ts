import { readFileSync } from 'node:fs'
import { Agent } from 'node:https'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

export default defineConfig(({ command, isPreview }) => {
  const certPath = process.env.PATHWAY_DEV_CERT
  const keyPath = process.env.PATHWAY_DEV_KEY
  if (command === 'serve' && !isPreview && (!certPath || !keyPath)) {
    throw new Error('Set PATHWAY_DEV_CERT and PATHWAY_DEV_KEY to trusted localhost PEM files. See README.')
  }
  const cert = certPath ? readFileSync(certPath) : undefined
  return {
    plugins: [vue()],
    server: {
      host: 'localhost',
      port: 5173,
      strictPort: true,
      https: cert && keyPath ? { cert, key: readFileSync(keyPath) } : undefined,
      proxy: {
        '/api': {
          target: 'https://localhost:7040',
          changeOrigin: true,
          secure: true,
          agent: new Agent({ ca: cert }),
        },
      },
    },
  }
})
