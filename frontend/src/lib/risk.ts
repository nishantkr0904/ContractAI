import type { RiskLevel } from '../types/api'

export const RISK_LEVELS: RiskLevel[] = ['UNKNOWN', 'LOW', 'MEDIUM', 'HIGH', 'CRITICAL']

// UNKNOWN is the enum zero value and the API rejects it as an override target, so the
// override control only offers the four real severities.
export const OVERRIDE_LEVELS: Exclude<RiskLevel, 'UNKNOWN'>[] = [
  'LOW',
  'MEDIUM',
  'HIGH',
  'CRITICAL',
]

export const riskLabel: Record<RiskLevel, string> = {
  UNKNOWN: 'Unknown',
  LOW: 'Low',
  MEDIUM: 'Medium',
  HIGH: 'High',
  CRITICAL: 'Critical',
}

// All four map to the semantic risk tokens declared in index.css (@theme), so a token
// change there recolors every badge, dot, and card accent at once.
export const riskDotClass: Record<RiskLevel, string> = {
  UNKNOWN: 'bg-risk-unknown',
  LOW: 'bg-risk-low',
  MEDIUM: 'bg-risk-medium',
  HIGH: 'bg-risk-high',
  CRITICAL: 'bg-risk-critical',
}

export const riskBorderClass: Record<RiskLevel, string> = {
  UNKNOWN: 'border-l-risk-unknown',
  LOW: 'border-l-risk-low',
  MEDIUM: 'border-l-risk-medium',
  HIGH: 'border-l-risk-high',
  CRITICAL: 'border-l-risk-critical',
}

export const riskTextClass: Record<RiskLevel, string> = {
  UNKNOWN: 'text-risk-unknown',
  LOW: 'text-risk-low',
  MEDIUM: 'text-risk-medium',
  HIGH: 'text-risk-high',
  CRITICAL: 'text-risk-critical',
}
