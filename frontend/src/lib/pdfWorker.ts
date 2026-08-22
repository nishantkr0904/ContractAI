import { pdfjs } from 'react-pdf'
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url'
import 'react-pdf/dist/Page/AnnotationLayer.css'
import 'react-pdf/dist/Page/TextLayer.css'

// pdf.js renders on a Web Worker. Vite's `?url` import hashes and serves the worker as
// a static asset, and the URL is assigned once at module load so every <Document> in
// the app shares the single configured worker. Importing this module for its side
// effect (before rendering a viewer) is all that's required.
pdfjs.GlobalWorkerOptions.workerSrc = workerUrl
