export interface PlatformAccessTokenResponse {
  accessToken: string;
  expiresIn: number;
  tokenType: string;
}

export interface PlatformMeResponse {
  id: string;
}

export interface PlatformTenantListItem {
  id: string;
  name: string;
  slug: string;
  industryCode: string;
  status: string;
  planCode: string;
  planNameAr: string;
  planNameEn: string;
  subscriptionStatus: string;
  trialEndsAt: string | null;
  userCount: number;
  createdAt: string;
}

export interface PlatformTenantSummary {
  total: number;
  trial: number;
  expiringSoon: number;
  suspended: number;
}

export interface PlatformTenantListResult {
  items: PlatformTenantListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  summary: PlatformTenantSummary;
}

export interface PlatformTenantDetail {
  id: string;
  company: {
    name: string;
    slug: string;
    industryCode: string;
    defaultLanguage: string;
    timeZone: string;
    status: string;
    hasLogo: boolean;
    createdAt: string;
    trialEndsAt: string | null;
  };
  owner: {
    userId: string;
    fullName: string;
    email: string;
    isActive: boolean;
    lastLoginAt: string | null;
  } | null;
  usage: {
    userCount: number;
    activeUserCount: number;
    activeEmployeeCount: number;
    employeeCap: number | null;
  };
  subscription: {
    planCode: string;
    planNameAr: string;
    planNameEn: string;
    pricingModel: string;
    employeeCap: number | null;
    status: string;
    effectiveState: string;
    readOnly: boolean;
    startsAt: string;
    trialEndsAt: string | null;
    renewsAt: string | null;
    cancelledAt: string | null;
    daysLeft: number | null;
  } | null;
}

export interface PlatformTenantAuditSummary {
  categories: { category: string; last7Days: number; last30Days: number }[];
  recentTenantEvents: {
    id: string;
    at: string;
    action: string;
    entityType: string;
    userId: string | null;
    userName: string | null;
  }[];
  platformActions: {
    id: string;
    at: string;
    action: string;
    adminId: string | null;
    adminName: string | null;
    adminEmail: string | null;
    reason: string | null;
  }[];
}

export interface PlatformPlan {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  pricingModel: string;
  employeeCap: number | null;
  unitPrice: number | null;
  minBilledEmployees: number | null;
  currency: string;
  featuresJson: string;
  features: Record<string, boolean>;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface PlatformUpdatePlanRequest {
  nameAr: string;
  nameEn: string;
  employeeCap: number | null;
  unitPrice: number | null;
  minBilledEmployees: number | null;
  featuresJson: string;
  isActive: boolean;
}

export interface PlatformAuditLogItem {
  id: string;
  at: string;
  tenantId: string;
  tenantName: string | null;
  tenantSlug: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  userId: string | null;
  actorKind: string;
  actorName: string | null;
  actorEmail: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  oldValuesJson: string | null;
  newValuesJson: string | null;
}

export interface PlatformAuditLogListResult {
  items: PlatformAuditLogItem[];
  nextCursor: string | null;
}

export interface PlatformHealthComponent {
  name: string;
  status: string;
  provider: string | null;
  detail: string | null;
  latencyMs: number | null;
}

export interface PlatformHealth {
  status: string;
  checkedAt: string;
  components: PlatformHealthComponent[];
}
