import { useEffect, useMemo, useRef, useState } from 'react'
import { Document, Page } from 'react-pdf'
import { useAuth } from '../../auth/authContext'
import '../../lib/pdfWorker'

interface DocumentViewerProps {
  contractId: string
  page: number
  onPageChange: (page: number) => void
}

const MAX_PAGE_WIDTH = 900

export function DocumentViewer({ contractId, page, onPageChange }: DocumentViewerProps) {
  const { token } = useAuth()
  const containerRef = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(0)
  const [numPages, setNumPages] = useState(0)
  const [failed, setFailed] = useState(false)

  // The full path (not the axios baseURL) because pdf.js fetches the stream itself.
  const fileUrl = `/api/v1/contracts/${contractId}/file`

  // In react-pdf 10 request params live on `options`, not `file`. Memoized so a new
  // object identity each render doesn't force the Document to reload.
  const options = useMemo(
    () => ({ httpHeaders: token ? { Authorization: `Bearer ${token}` } : undefined }),
    [token],
  )

  // Fit the page to the column and re-fit on resize; capped so a wide screen doesn't
  // blow the page up past a readable width.
  useEffect(() => {
    const element = containerRef.current
    if (!element) return
    const observer = new ResizeObserver((entries) => {
      const measured = entries[0]?.contentRect.width ?? 0
      if (measured > 0) {
        setWidth(Math.min(measured, MAX_PAGE_WIDTH))
      }
    })
    observer.observe(element)
    return () => observer.disconnect()
  }, [])

  function handleLoadSuccess(pdf: { numPages: number }) {
    setNumPages(pdf.numPages)
    setFailed(false)
  }

  const currentPage = numPages ? Math.min(Math.max(page, 1), numPages) : 1

  return (
    <div className="flex flex-col">
      <div ref={containerRef} className="overflow-auto rounded-xl border border-slate-200 bg-slate-100 p-4">
        {failed ? (
          <p className="py-12 text-center text-sm text-risk-critical">
            Could not load the document.
          </p>
        ) : (
          <Document
            file={fileUrl}
            options={options}
            onLoadSuccess={handleLoadSuccess}
            onLoadError={() => setFailed(true)}
            loading={<p className="py-12 text-center text-sm text-slate-400">Loading document…</p>}
            error={<p className="py-12 text-center text-sm text-risk-critical">Could not load the document.</p>}
            className="flex justify-center"
          >
            <Page
              pageNumber={currentPage}
              width={width || undefined}
              className="shadow-sm"
            />
          </Document>
        )}
      </div>

      {numPages > 0 && !failed && (
        <div className="mt-3 flex items-center justify-center gap-3 text-sm text-slate-500">
          <button
            type="button"
            onClick={() => onPageChange(Math.max(1, currentPage - 1))}
            disabled={currentPage <= 1}
            className="rounded-lg border border-slate-200 px-3 py-1 disabled:opacity-40"
          >
            Previous
          </button>
          <span>
            Page {currentPage} of {numPages}
          </span>
          <button
            type="button"
            onClick={() => onPageChange(Math.min(numPages, currentPage + 1))}
            disabled={currentPage >= numPages}
            className="rounded-lg border border-slate-200 px-3 py-1 disabled:opacity-40"
          >
            Next
          </button>
        </div>
      )}
    </div>
  )
}
