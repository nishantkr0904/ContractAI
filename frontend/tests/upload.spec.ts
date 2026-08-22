import { test, expect } from '@playwright/test'

// End-to-end cover for the upload happy path: a PDF dropped on the dashboard is
// accepted, the contract is enqueued, and once background parsing reports success the
// UI raises the "parsed successfully" toast.
//
// The backend is stubbed at the network boundary so the test drives the real React
// flow — dropzone acceptance, the upload mutation, and the status poll that fires the
// toast — with no live API, database, blob store, or parser. Auth is seeded straight
// into localStorage so the app boots onto the dashboard rather than the dev login form.

const CONTRACT_ID = '00000000-0000-4000-8000-000000000001'
const FILE_NAME = 'sample-contract.pdf'

test('uploading a PDF surfaces the success toast once parsing completes', async ({ page }) => {
  await page.addInitScript(() => {
    window.localStorage.setItem('contractai.token', 'e2e-playwright-token')
  })

  // One handler for every /api/v1 call the dashboard makes. The contract detail poll
  // reports PARSED_SUCCESS on the first read, so the FileUpload effect fires the toast
  // right away and the poll then halts.
  await page.route('**/api/v1/**', async (route) => {
    const request = route.request()
    const { pathname } = new URL(request.url())

    if (request.method() === 'POST' && pathname.endsWith('/contracts/upload')) {
      // Mirrors the controller: 202 Accepted, status UPLOADED, and the status link the
      // client polls.
      await route.fulfill({
        status: 202,
        contentType: 'application/json',
        body: JSON.stringify({
          id: CONTRACT_ID,
          file_name: FILE_NAME,
          status: 'UPLOADED',
          created_at: new Date().toISOString(),
          links: { status: `/api/v1/contracts/${CONTRACT_ID}` },
        }),
      })
      return
    }

    if (request.method() === 'GET' && pathname.endsWith(`/contracts/${CONTRACT_ID}`)) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: CONTRACT_ID,
          uploaded_by: null,
          file_name: FILE_NAME,
          file_uri: `s3://contracts/${CONTRACT_ID}.pdf`,
          status: 'PARSED_SUCCESS',
          overall_risk: 'LOW',
          created_at: new Date().toISOString(),
          updated_at: new Date().toISOString(),
        }),
      })
      return
    }

    if (request.method() === 'GET' && pathname.endsWith('/contracts')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: [],
          meta: { current_page: 1, total_pages: 0, total_records: 0 },
        }),
      })
      return
    }

    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' })
  })

  await page.goto('/')

  // The dropzone copy confirms we landed on the dashboard, not the login screen.
  await expect(page.getByText('Drag & drop a contract PDF', { exact: false })).toBeVisible()

  // react-dropzone renders a hidden file input; setInputFiles drives it directly, the
  // programmatic equivalent of dropping a file onto the zone.
  await page.locator('input[type="file"]').setInputFiles({
    name: FILE_NAME,
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.4\n%%EOF\n'),
  })

  await expect(page.getByRole('status')).toContainText('parsed successfully', {
    timeout: 10_000,
  })
})
