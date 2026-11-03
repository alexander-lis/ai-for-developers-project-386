import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import App from './App'
import { client } from './client/client.gen'

it('shows backend status after check', async () => {
  // Request в Node не принимает относительный адрес, в отличие от браузера
  client.setConfig({ baseUrl: 'http://localhost' })
  vi.stubGlobal('fetch', vi.fn(async () => Response.json({ status: 'ok' })))

  render(<App />)
  await userEvent.click(screen.getByRole('button', { name: 'Проверить бекенд' }))

  expect(await screen.findByText('Бекенд: ok')).toBeTruthy()
})
