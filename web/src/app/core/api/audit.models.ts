export interface AuditLogListItem {
  id: string;
  at: string;
  action: string;
  entityType: string;
  entityId: string | null;
  entityDisplayName: string | null;
  userId: string | null;
  userName: string | null;
  userEmail: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  oldValuesJson: string | null;
  newValuesJson: string | null;
}

export interface AuditLogListResult {
  items: AuditLogListItem[];
  nextCursor: string | null;
}

export interface AuditLogListQuery {
  userId?: string | null;
  entityType?: string | null;
  action?: string | null;
  from?: string | null;
  to?: string | null;
  pageSize?: number;
  cursor?: string | null;
}

export type AuditCategory =
  | 'auth'
  | 'users'
  | 'roles'
  | 'invitations'
  | 'templates'
  | 'subscription'
  | 'system'
  | 'other';
