import { useState } from 'react'
import BattleScreen from './components/BattleScreen'

function App() {
  const [depth, setDepth] = useState<1 | 2 | 3 | 4 | 5>(1)

  return (
    <div style={{ width: '100%', height: '100%' }}>
      <BattleScreen depth={depth} onDepthChange={setDepth} />
    </div>
  )
}

export default App
