/**
 * Landing industry panel data mirrored from backend template seeds
 * (Kadr.Platform.Infrastructure/Templates/Seeds/*.v1.json). Keep in sync when seeds change.
 */

export type IndustryCode = 'retail' | 'clinic' | 'factory' | 'construction' | 'office';

export interface IndustrySeedView {
  code: IndustryCode;
  nameAr: string;
  nameEn: string;
  icon: 'building' | 'users' | 'settings' | 'shield' | 'file';
  shifts: { nameAr: string; nameEn: string; start: string; end: string }[];
  leaveTypes: { nameAr: string; nameEn: string }[];
  documentTypes: { nameAr: string; nameEn: string; hasExpiry: boolean; alertDays: number[] }[];
  jobTitles: { nameAr: string; nameEn: string }[];
  ctaLineAr: string;
  ctaLineEn: string;
}

export const LANDING_INDUSTRY_SEEDS: IndustrySeedView[] = [
  {
    code: 'retail',
    nameAr: 'التجزئة',
    nameEn: 'Retail',
    icon: 'building',
    shifts: [
      { nameAr: 'وردية صباحية', nameEn: 'Morning', start: '09:00', end: '17:00' },
      { nameAr: 'وردية مسائية', nameEn: 'Evening', start: '14:00', end: '22:00' },
      { nameAr: 'وردية نهاية الأسبوع', nameEn: 'Weekend', start: '10:00', end: '18:00' },
    ],
    leaveTypes: [
      { nameAr: 'إجازة سنوية', nameEn: 'Annual leave' },
      { nameAr: 'إجازة مرضية', nameEn: 'Sick leave' },
      { nameAr: 'إجازة بدون راتب', nameEn: 'Unpaid leave' },
      { nameAr: 'إجازة طارئة', nameEn: 'Emergency leave' },
      { nameAr: 'إجازة حج', nameEn: 'Hajj leave' },
    ],
    documentTypes: [
      { nameAr: 'بطاقة شخصية', nameEn: 'National ID', hasExpiry: true, alertDays: [60, 30, 7] },
      { nameAr: 'عقد عمل', nameEn: 'Employment contract', hasExpiry: false, alertDays: [] },
      { nameAr: 'شهادة صحية', nameEn: 'Health certificate', hasExpiry: true, alertDays: [30] },
    ],
    jobTitles: [
      { nameAr: 'مدير متجر', nameEn: 'Store manager' },
      { nameAr: 'كاشير', nameEn: 'Cashier' },
      { nameAr: 'بائع', nameEn: 'Sales associate' },
      { nameAr: 'مشرف وردية', nameEn: 'Shift supervisor' },
    ],
    ctaLineAr: 'ابدأ من قالب التجزئة — ثم خصّص بعد التسجيل.',
    ctaLineEn: 'Start from the retail seed — customize after signup.',
  },
  {
    code: 'clinic',
    nameAr: 'العيادات',
    nameEn: 'Clinic',
    icon: 'users',
    shifts: [
      { nameAr: 'وردية نهارية', nameEn: 'Day', start: '08:00', end: '16:00' },
      { nameAr: 'وردية ليلية', nameEn: 'Night', start: '16:00', end: '00:00' },
    ],
    leaveTypes: [
      { nameAr: 'إجازة سنوية', nameEn: 'Annual leave' },
      { nameAr: 'إجازة مرضية', nameEn: 'Sick leave' },
      { nameAr: 'إجازة بدون راتب', nameEn: 'Unpaid leave' },
      { nameAr: 'إجازة تدريب', nameEn: 'Training leave' },
    ],
    documentTypes: [
      { nameAr: 'بطاقة شخصية', nameEn: 'National ID', hasExpiry: true, alertDays: [30, 7] },
      { nameAr: 'ترخيص مزاولة', nameEn: 'Practice license', hasExpiry: true, alertDays: [90, 30] },
      { nameAr: 'عقد عمل', nameEn: 'Employment contract', hasExpiry: false, alertDays: [] },
    ],
    jobTitles: [
      { nameAr: 'طبيب', nameEn: 'Doctor' },
      { nameAr: 'ممرض', nameEn: 'Nurse' },
      { nameAr: 'استقبال', nameEn: 'Receptionist' },
      { nameAr: 'فني مختبر', nameEn: 'Lab technician' },
    ],
    ctaLineAr: 'ابدأ من قالب العيادات — ثم خصّص بعد التسجيل.',
    ctaLineEn: 'Start from the clinic seed — customize after signup.',
  },
  {
    code: 'factory',
    nameAr: 'المصانع',
    nameEn: 'Factory',
    icon: 'settings',
    shifts: [
      { nameAr: 'وردية أ', nameEn: 'Shift A', start: '06:00', end: '14:00' },
      { nameAr: 'وردية ب', nameEn: 'Shift B', start: '14:00', end: '22:00' },
      { nameAr: 'وردية ج', nameEn: 'Shift C', start: '22:00', end: '06:00' },
    ],
    leaveTypes: [
      { nameAr: 'إجازة سنوية', nameEn: 'Annual leave' },
      { nameAr: 'إجازة مرضية', nameEn: 'Sick leave' },
      { nameAr: 'إجازة بدون راتب', nameEn: 'Unpaid leave' },
      { nameAr: 'إجازة إصابة عمل', nameEn: 'Work injury leave' },
    ],
    documentTypes: [
      { nameAr: 'بطاقة شخصية', nameEn: 'National ID', hasExpiry: true, alertDays: [30] },
      { nameAr: 'شهادة سلامة', nameEn: 'Safety certificate', hasExpiry: true, alertDays: [60, 14] },
      { nameAr: 'عقد عمل', nameEn: 'Employment contract', hasExpiry: false, alertDays: [] },
    ],
    jobTitles: [
      { nameAr: 'مشغل آلات', nameEn: 'Machine operator' },
      { nameAr: 'مشرف إنتاج', nameEn: 'Production supervisor' },
      { nameAr: 'مراقب جودة', nameEn: 'Quality controller' },
      { nameAr: 'فني صيانة', nameEn: 'Maintenance tech' },
    ],
    ctaLineAr: 'ابدأ من قالب المصانع — ثم خصّص بعد التسجيل.',
    ctaLineEn: 'Start from the factory seed — customize after signup.',
  },
  {
    code: 'construction',
    nameAr: 'المقاولات',
    nameEn: 'Construction',
    icon: 'shield',
    shifts: [
      { nameAr: 'وردية موقع', nameEn: 'Site day', start: '07:00', end: '15:00' },
      { nameAr: 'وردية مكتبية', nameEn: 'Office', start: '09:00', end: '17:00' },
    ],
    leaveTypes: [
      { nameAr: 'إجازة سنوية', nameEn: 'Annual leave' },
      { nameAr: 'إجازة مرضية', nameEn: 'Sick leave' },
      { nameAr: 'إجازة بدون راتب', nameEn: 'Unpaid leave' },
      { nameAr: 'إجازة موقع', nameEn: 'Site leave' },
      { nameAr: 'إجازة طارئة', nameEn: 'Emergency leave' },
    ],
    documentTypes: [
      { nameAr: 'بطاقة شخصية', nameEn: 'National ID', hasExpiry: true, alertDays: [30, 7] },
      { nameAr: 'شهادة سلامة', nameEn: 'Safety certificate', hasExpiry: true, alertDays: [30] },
      { nameAr: 'تأمين', nameEn: 'Insurance card', hasExpiry: true, alertDays: [60] },
    ],
    jobTitles: [
      { nameAr: 'مهندس موقع', nameEn: 'Site engineer' },
      { nameAr: 'ملاحظ', nameEn: 'Foreman' },
      { nameAr: 'عامل', nameEn: 'Worker' },
      { nameAr: 'مسؤول سلامة', nameEn: 'Safety officer' },
    ],
    ctaLineAr: 'ابدأ من قالب المقاولات — ثم خصّص بعد التسجيل.',
    ctaLineEn: 'Start from the construction seed — customize after signup.',
  },
  {
    code: 'office',
    nameAr: 'المكاتب',
    nameEn: 'Office',
    icon: 'file',
    shifts: [
      { nameAr: 'وردية صباحية', nameEn: 'Morning', start: '09:00', end: '17:00' },
      { nameAr: 'وردية مسائية', nameEn: 'Evening', start: '13:00', end: '21:00' },
    ],
    leaveTypes: [
      { nameAr: 'إجازة سنوية', nameEn: 'Annual leave' },
      { nameAr: 'إجازة مرضية', nameEn: 'Sick leave' },
      { nameAr: 'إجازة بدون راتب', nameEn: 'Unpaid leave' },
      { nameAr: 'إجازة طارئة', nameEn: 'Emergency leave' },
    ],
    documentTypes: [
      { nameAr: 'بطاقة شخصية', nameEn: 'National ID', hasExpiry: true, alertDays: [30, 7] },
      { nameAr: 'عقد عمل', nameEn: 'Employment contract', hasExpiry: false, alertDays: [] },
      { nameAr: 'شهادة', nameEn: 'Certificate', hasExpiry: false, alertDays: [] },
    ],
    jobTitles: [
      { nameAr: 'مدير', nameEn: 'Manager' },
      { nameAr: 'أخصائي موارد بشرية', nameEn: 'HR specialist' },
      { nameAr: 'محاسب', nameEn: 'Accountant' },
      { nameAr: 'إداري', nameEn: 'Administrator' },
    ],
    ctaLineAr: 'ابدأ من قالب المكاتب — ثم خصّص بعد التسجيل.',
    ctaLineEn: 'Start from the office seed — customize after signup.',
  },
];
