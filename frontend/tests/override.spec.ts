import { test, expect } from '@playwright/test'

// End-to-end cover for the human-in-the-loop path: a clause the pipeline scored HIGH is
// re-graded to LOW by a reviewer, and the sidebar repaints at the new severity.
//
// The override is not applied client-side — the mutation invalidates the clause query and
// the server's copy is re-read — so the stub is stateful: GET /clauses reports HIGH until
// the PATCH lands and LOW afterwards. That makes the assertion a real round trip rather
// than a local state flip. The PDF stream is stubbed 404 so the viewer settles into its
// error state instead of waiting on pdf.js; the sidebar under test is unaffected.

const CONTRACT_ID = '00000000-0000-4000-8000-000000000002'
const CLAUSE_ID = '00000000-0000-4000-8000-0000000000aa'
const CLAUSE_TYPE = 'Limitation of Liability'
const EXPLANATION = 'Cap is market standard for this contract value.'

// Deliberately free of the words "high" and "low" so asserting on the badge label cannot
// accidentally match the clause body.
const CLAUSE_TEXT =
  'Provider aggregate liability shall not exceed the fees paid in the preceding twelve months.'

test('overriding a HIGH clause to LOW repaints the sidebar at the new severity', async ({
  page,
}) => {
  await page.addInitScript(() => {
    window.localStorage.setItem('contractai.token', 'e2e-playwright-token')
  })

  let severity = 'HIGH'
  let overridePayload: unknown = null

  await page.route('**/api/v1/**', async (route) => {
    const request = route.request()
    const { pathname } = new URL(request.url())

    if (request.method() === 'PATCH' && pathname.endsWith(`/clauses/${CLAUSE_ID}/risk`)) {
      overridePayload = request.postDataJSON()
      // The reviewer's decision becomes the effective severity for every later read.
      severity = 'LOW'
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: '00000000-0000-4000-8000-0000000000bb',
          contract_clause_id: CLAUSE_ID,
          severity,
          rule_violated: 'LIABILITY-CAP (Human Override)',
          explanation: EXPLANATION,
          updated_at: new Date().toISOString(),
        }),
      })
      return
    }

    if (request.method() === 'GET' && pathname.endsWith(`/contracts/${CONTRACT_ID}/clauses`)) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: [
            {
              id: CLAUSE_ID,
              contract_id: CONTRACT_ID,
              clause_type: { id: '00000000-0000-4000-8000-0000000000cc', name: CLAUSE_TYPE, description: null },
              raw_text: CLAUSE_TEXT,
              page_number: 3,
              byte_offset: 2048,
              confidence_score: 0.94,
              risk_score: {
                id: '00000000-0000-4000-8000-0000000000dd',
                severity,
                rule_violated: 'LIABILITY-CAP',
                explanation: 'Cap is below the contract value.',
              },
              created_at: new Date().toISOString(),
            },
          ],
        }),
      })
      return
    }

    // pdf.js fetches the stream itself; a 404 sends the viewer straight to its error state.
    if (pathname.endsWith(`/contracts/${CONTRACT_ID}/file`)) {
      await route.fulfill({ status: 404, contentType: 'application/json', body: '{}' })
      return
    }

    if (request.method() === 'GET' && pathname.endsWith(`/contracts/${CONTRACT_ID}`)) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: CONTRACT_ID,
          uploaded_by: null,
          file_name: 'master-services-agreement.pdf',
          file_uri: `s3://contracts/${CONTRACT_ID}.pdf`,
          status: 'PARSED_SUCCESS',
          overall_risk: severity,
          created_at: new Date().toISOString(),
          updated_at: new Date().toISOString(),
        }),
      })
      return
    }

    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' })
  })

  await page.goto(`/contracts/${CONTRACT_ID}`)

  const clauseCard = page.getByRole('listitem').filter({ hasText: CLAUSE_TYPE })
  // The card header carries the risk badge. Scoping the label assertion to it keeps the
  // override <select>'s own "High"/"Low" options out of the match.
  const clauseHeader = clauseCard.getByRole('button', { name: new RegExp(CLAUSE_TYPE) })

  await expect(clauseHeader).toContainText('High')
  await expect(clauseCard).toHaveClass(/border-l-risk-high/)

  // Selecting the clause reveals the override form.
  await clauseHeader.click()

  await clauseCard.getByRole('combobox').selectOption('LOW')
  await clauseCard.getByPlaceholder('Why is this the correct severity?').fill(EXPLANATION)
  await clauseCard.getByRole('button', { name: 'Save override' }).click()

  await expect(page.getByRole('status')).toContainText('Risk severity updated.', {
    timeout: 10_000,
  })

  // The visible state change: badge label and the left-edge risk accent both follow the
  // refetched severity.
  await expect(clauseHeader).toContainText('Low')
  await expect(clauseHeader).not.toContainText('High')
  await expect(clauseCard).toHaveClass(/border-l-risk-low/)

  expect(overridePayload).toEqual({ severity: 'LOW', explanation: EXPLANATION })
})
