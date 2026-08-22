import { useCallback, useEffect, useRef, useState } from 'react'
import { useDropzone } from 'react-dropzone'
import { useQueryClient } from '@tanstack/react-query'
import { useToast } from '../../components/toast/toastContext'
import { contractKeys, useContractStatus, useUploadContract } from './hooks'

const MAX_UPLOAD_BYTES = 50 * 1024 * 1024

export function FileUpload() {
  const { showToast } = useToast()
  const queryClient = useQueryClient()
  const upload = useUploadContract()
  const [progress, setProgress] = useState(0)
  const [trackedId, setTrackedId] = useState<string | null>(null)
  const [trackedName, setTrackedName] = useState('')

  // Guards against re-announcing the same contract (React StrictMode double-invokes
  // effects in dev, and the poll may re-deliver a terminal result before it stops).
  const announced = useRef<Set<string>>(new Set())

  const tracked = useContractStatus(trackedId)

  const onDrop = useCallback(
    (accepted: File[]) => {
      const file = accepted[0]
      if (!file) return
      setProgress(0)
      upload.mutate(
        { file, onProgress: setProgress },
        {
          onSuccess: (data) => {
            setTrackedId(data.id)
            setTrackedName(data.file_name)
          },
          onError: () => showToast('Upload failed. Only PDFs up to 50 MB are accepted.', 'error'),
        },
      )
    },
    [upload, showToast],
  )

  const { getRootProps, getInputProps, isDragActive, fileRejections } = useDropzone({
    onDrop,
    accept: { 'application/pdf': ['.pdf'] },
    multiple: false,
    maxSize: MAX_UPLOAD_BYTES,
  })

  useEffect(() => {
    const contract = tracked.data
    if (!contract) return
    const { id, status } = contract
    if (status !== 'PARSED_SUCCESS' && status !== 'PARSED_ERROR') return
    if (announced.current.has(id)) return

    announced.current.add(id)
    setTrackedId(null)
    void queryClient.invalidateQueries({ queryKey: contractKeys.all })
    if (status === 'PARSED_SUCCESS') {
      showToast(`"${trackedName}" parsed successfully.`, 'success')
    } else {
      showToast(`"${trackedName}" could not be parsed.`, 'error')
    }
  }, [tracked.data, trackedName, showToast, queryClient])

  const isUploading = upload.isPending
  const isParsing = trackedId !== null
  const rejection = fileRejections[0]?.errors[0]?.message

  return (
    <div
      {...getRootProps()}
      className={`cursor-pointer rounded-xl border-2 border-dashed p-8 text-center transition-colors ${
        isDragActive
          ? 'border-slate-400 bg-slate-50'
          : 'border-slate-200 bg-white hover:border-slate-300'
      }`}
    >
      <input {...getInputProps()} />

      {isUploading ? (
        <div>
          <p className="text-sm text-slate-600">Uploading… {progress}%</p>
          <div className="mx-auto mt-2 h-1.5 w-48 overflow-hidden rounded-full bg-slate-100">
            <div className="h-full bg-slate-700 transition-all" style={{ width: `${progress}%` }} />
          </div>
        </div>
      ) : isParsing ? (
        <p className="text-sm text-slate-600">
          Parsing “{trackedName}”… this runs in the background.
        </p>
      ) : (
        <div>
          <p className="text-sm font-medium text-slate-700">
            {isDragActive ? 'Drop the PDF to upload' : 'Drag & drop a contract PDF, or click to browse'}
          </p>
          <p className="mt-1 text-xs text-slate-400">PDF only, up to 50 MB</p>
        </div>
      )}

      {rejection && !isUploading && !isParsing && (
        <p className="mt-3 text-xs text-risk-critical">{rejection}</p>
      )}
    </div>
  )
}
