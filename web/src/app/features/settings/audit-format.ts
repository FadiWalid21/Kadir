import { AuditCategory, AuditLogListItem } from '../../core/api/audit.models';

export function formatDateTime(iso: string, lang: string): { date: string; time: string } {
  const d = new Date(iso);
  const locale = lang.startsWith('ar') ? 'ar' : 'en';
  return {
    date: new Intl.DateTimeFormat(locale, {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    }).format(d),
    time: new Intl.DateTimeFormat(locale, {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    }).format(d),
  };
}

export function parseDevice(userAgent: string | null | undefined): string {
  if (!userAgent?.trim()) {
    return '';
  }
  const ua = userAgent;
  let browser = 'Browser';
  if (/Edg\//i.test(ua)) {
    browser = 'Edge';
  } else if (/Chrome\//i.test(ua)) {
    browser = 'Chrome';
  } else if (/Firefox\//i.test(ua)) {
    browser = 'Firefox';
  } else if (/Safari\//i.test(ua) && !/Chrome/i.test(ua)) {
    browser = 'Safari';
  }

  let os = '';
  if (/Windows/i.test(ua)) {
    os = 'Windows';
  } else if (/Mac OS X|Macintosh/i.test(ua)) {
    os = 'macOS';
  } else if (/Android/i.test(ua)) {
    os = 'Android';
  } else if (/iPhone|iPad/i.test(ua)) {
    os = 'iOS';
  } else if (/Linux/i.test(ua)) {
    os = 'Linux';
  }

  return os ? `${browser} · ${os}` : browser;
}

export function auditCategory(item: AuditLogListItem): AuditCategory {
  const action = item.action.toLowerCase();
  if (action.startsWith('auth.')) {
    return 'auth';
  }
  if (action.startsWith('user.') || item.entityType.toLowerCase() === 'user' || item.entityType === 'AppUser') {
    return 'users';
  }
  if (action.startsWith('role.') || item.entityType.toLowerCase() === 'role') {
    return 'roles';
  }
  if (action.startsWith('invitation.')) {
    return 'invitations';
  }
  if (action.startsWith('template.')) {
    return 'templates';
  }
  if (action.startsWith('subscription.') || item.entityType.toLowerCase() === 'subscription') {
    return 'subscription';
  }
  if (!item.userId) {
    return 'system';
  }
  return 'other';
}

export function isFailedLogin(item: AuditLogListItem): boolean {
  return (
    item.action === 'auth.login_failed' || item.action === 'auth.account_locked'
  );
}

export function isSystemActor(item: AuditLogListItem): boolean {
  return !item.userId && !isFailedLogin(item);
}

export function parseJsonRecord(json: string | null | undefined): Record<string, unknown> {
  if (!json?.trim()) {
    return {};
  }
  try {
    const parsed = JSON.parse(json) as unknown;
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
  } catch {
    /* ignore */
  }
  return {};
}

export function diffEntries(
  oldJson: string | null,
  newJson: string | null,
): { field: string; oldValue: string; newValue: string }[] {
  const oldVals = parseJsonRecord(oldJson);
  const newVals = parseJsonRecord(newJson);
  const keys = new Set([...Object.keys(oldVals), ...Object.keys(newVals)]);
  const rows: { field: string; oldValue: string; newValue: string }[] = [];
  for (const key of keys) {
    if (/password|token|secret/i.test(key)) {
      continue;
    }
    const oldValue = formatDiffValue(oldVals[key]);
    const newValue = formatDiffValue(newVals[key]);
    if (oldValue === newValue) {
      continue;
    }
    rows.push({ field: key, oldValue, newValue });
  }
  return rows;
}

function formatDiffValue(value: unknown): string {
  if (value === null || value === undefined) {
    return '—';
  }
  if (typeof value === 'string') {
    return value;
  }
  if (typeof value === 'number' || typeof value === 'boolean') {
    return String(value);
  }
  return JSON.stringify(value);
}

export const AUDIT_ACTION_OPTIONS = [
  'auth.login_succeeded',
  'auth.login_failed',
  'auth.account_locked',
  'role.created',
  'role.updated',
  'role.deleted',
  'user.updated',
  'user.role_changed',
  'user.deactivated',
  'user.reactivated',
  'invitation.created',
  'invitation.resent',
  'invitation.revoked',
  'invitation.accepted',
  'template.update',
  'template.reset',
  'template.set_active',
  'template.add_missing',
  'subscription.plan_changed',
  'subscription.trial_expired',
  'entity.created',
  'entity.updated',
  'entity.deleted',
] as const;

export const AUDIT_ENTITY_OPTIONS = [
  'Auth',
  'role',
  'user',
  'AppUser',
  'invitation',
  'Subscription',
  'shifts',
  'leaveTypes',
  'documentTypes',
  'jobTitles',
  'departments',
  'templates',
] as const;
