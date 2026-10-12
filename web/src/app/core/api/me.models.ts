import type { MeSubscriptionSummary } from './subscription.models';

export interface MeTenantDto {
  id: string;
  name: string;
  slug: string;
  logoUrl?: string | null;
}

export interface MeChecklistDto {
  inviteTeamDone: boolean;
}

export interface MeResponse {
  id: string;
  fullName: string;
  email: string;
  language: string;
  photoUrl?: string | null;
  roles: string[];
  permissions: string[];
  tenant: MeTenantDto;
  checklist: MeChecklistDto;
  subscription?: MeSubscriptionSummary | null;
}

export interface SignInMethodsResponse {
  password: { enabled: boolean };
  google: { linked: boolean; email?: string | null };
}

export interface SetPasswordRequest {
  currentPassword?: string;
  newPassword: string;
}
