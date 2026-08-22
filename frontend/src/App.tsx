import { Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/authContext'
import { LoginScreen } from './auth/LoginScreen'
import { AppHeader } from './components/AppHeader'
import { ContractsDashboard } from './features/contracts/ContractsDashboard'
import { ContractDetailPage } from './features/contracts/ContractDetailPage'
import { SearchResultsPage } from './features/search/SearchResultsPage'

export default function App() {
  const { isAuthenticated } = useAuth()

  if (!isAuthenticated) {
    return <LoginScreen />
  }

  return (
    <div className="min-h-screen">
      <AppHeader />
      <main className="mx-auto max-w-6xl px-6 py-6">
        <Routes>
          <Route path="/" element={<ContractsDashboard />} />
          <Route path="/contracts/:id" element={<ContractDetailPage />} />
          <Route path="/search" element={<SearchResultsPage />} />
          <Route path="*" element={<ContractsDashboard />} />
        </Routes>
      </main>
    </div>
  )
}
