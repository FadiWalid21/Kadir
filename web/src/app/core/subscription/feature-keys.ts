/** Mirrors Kadr.SharedKernel.Constants.FeatureKeys (never sole security). */
export const FeatureKeys = {
  CoreHr: 'core_hr',
  Attendance: 'attendance',
  Leave: 'leave',
  Ess: 'ess',
  Shifts: 'shifts',
  Workflows: 'workflows',
  Reports: 'reports',
  Documents: 'documents',
  DeviceImport: 'device_import',
  CustomFields: 'custom_fields',
  Api: 'api',
  ComplianceDashboards: 'compliance_dashboards',
  PrioritySupport: 'priority_support',
} as const;

export type FeatureKey = (typeof FeatureKeys)[keyof typeof FeatureKeys];
