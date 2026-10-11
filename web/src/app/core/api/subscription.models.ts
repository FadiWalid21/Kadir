export type SubscriptionEffectiveState =
  | 'trialRunning'
  | 'trialExpired'
  | 'active'
  | 'suspended'
  | 'cancelled'
  | 'pastDue';

export interface SubscriptionPlanDto {
  code: string;
  nameAr: string;
  nameEn: string;
  pricingModel: 'flatTier' | 'perEmployee' | string;
  employeeCap: number | null;
  unitPrice: number | null;
  minBilledEmployees: number | null;
  currency: string;
}

export interface SubscriptionUsageDto {
  activeEmployees: number;
  cap: number | null;
}

export interface SubscriptionDto {
  plan: SubscriptionPlanDto;
  status: string;
  effectiveState: SubscriptionEffectiveState | string;
  readOnly: boolean;
  startsAt: string;
  trialStartsAt: string | null;
  trialEndsAt: string | null;
  daysLeft: number | null;
  usage: SubscriptionUsageDto;
  features: Record<string, boolean>;
}

export interface PlanCatalogItemDto {
  code: string;
  nameAr: string;
  nameEn: string;
  pricingModel: 'flatTier' | 'perEmployee' | string;
  employeeCap: number | null;
  unitPrice: number | null;
  minBilledEmployees: number | null;
  currency: string;
  features: Record<string, boolean>;
}

export interface MeSubscriptionSummary {
  state: SubscriptionEffectiveState | string;
  trialEndsAt: string | null;
  daysLeft: number | null;
  readOnly: boolean;
  features: Record<string, boolean>;
}
