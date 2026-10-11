export type CompanyAvatarTone = 'accent' | 'info' | 'success' | 'warning';

const TONES: CompanyAvatarTone[] = ['accent', 'info', 'success', 'warning'];

/** Soft token pairs only (never arbitrary colors); text color keeps AA contrast on the soft fill. */
export const COMPANY_AVATAR_CLASSES: Record<CompanyAvatarTone, string> = {
  accent: 'bg-accent-soft text-accent-soft-fg',
  info: 'bg-info-soft text-info',
  success: 'bg-success-soft text-success',
  warning: 'bg-warning-soft text-warning',
};

/** Same key always yields the same tone, so a company keeps its color across visits. */
export function companyAvatarTone(key: string): CompanyAvatarTone {
  let hash = 5381;
  for (let i = 0; i < key.length; i++) {
    hash = ((hash << 5) + hash + key.charCodeAt(i)) | 0;
  }
  return TONES[Math.abs(hash) % TONES.length]!;
}

/** First letter of the first two words (works for Arabic and Latin names). */
export function companyInitials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) {
    return '?';
  }
  const letters = words.slice(0, 2).map((w) => Array.from(w)[0] ?? '');
  return letters.join('').toUpperCase();
}
