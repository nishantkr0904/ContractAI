// Wire timestamps are ISO 8601 strings; render them in the viewer's locale. A bad or
// empty value degrades to an em dash rather than "Invalid Date".
export function formatDateTime(iso: string): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) {
    return '—'
  }
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}
