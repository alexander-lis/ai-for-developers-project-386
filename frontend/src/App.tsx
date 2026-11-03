import { useState } from 'react'
import { healthCheck } from '@/client'
import { Button } from '@/components/ui/button'

function App() {
  const [status, setStatus] = useState('не проверен')

  async function check() {
    const { data } = await healthCheck()
    setStatus(data?.status ?? 'недоступен')
  }

  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-4">
      <h1 className="text-2xl font-medium">Календарь звонков</h1>
      <Button onClick={check}>Проверить бекенд</Button>
      <p>Бекенд: {status}</p>
    </main>
  )
}

export default App
