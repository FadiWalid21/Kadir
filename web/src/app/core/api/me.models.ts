export interface MeTenantDto {
  id: string;
  name: string;
  slug: string;
  logoUrl?: string | null;
}

export interface MeResponse {
  id: string;
  fullName: string;
  email: string;
  language: string;
  roles: string[];
  permissions: string[];
  tenant: MeTenantDto;
}
