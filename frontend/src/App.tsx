import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import './App.css'
import Navbar from './components/Navbar'
import Home from './pages/Home'
import ScrollToTop from './components/ScrollToTop'
import { ThemeProvider } from './context/ThemeContext'
import { PipelineBoard } from './features/pipeline/PipelineBoard'
import { TranscriptEditorPage } from './features/transcript-editor/components/TranscriptEditorPage'

function App() {
  return (
    <ThemeProvider>
      <Router basename={import.meta.env.BASE_URL}>
        <ScrollToTop />
        <div className="app-container">
          <Navbar />
          <main>
            <Routes>
              <Route path="/" element={<PipelineBoard />} />
              <Route path="/home" element={<Home />} />
              <Route path="/editor" element={<TranscriptEditorPage />} />
              <Route path="/editor/:transcriptionJobId" element={<TranscriptEditorPage />} />
            </Routes>
          </main>
          <footer className="container" style={{
            padding: '64px 0',
            borderTop: '1px solid var(--glass-border)',
            marginTop: '120px',
            textAlign: 'center',
            color: 'var(--text-secondary)',
            fontSize: '0.9rem'
          }}>
            <p>© {new Date().getFullYear()} SnipIt</p>
          </footer>
        </div>
      </Router>
    </ThemeProvider>
  )
}

export default App
