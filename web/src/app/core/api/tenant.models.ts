export interface TenantSettingsResponse {
  name: string;
  defaultLanguage: string;
  timeZone: string;
  logoFileId: string | null;
  logoUrl: string | null;
}

export interface UpdateTenantSettingsRequest {
  name: string;
  defaultLanguage?: string | null;
  timeZone?: string | null;
}

export interface TenantLogoResponse {
  logoFileId: string;
  logoUrl: string;
}
