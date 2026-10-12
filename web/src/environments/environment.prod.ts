export const environment = {
  production: true,
  // Overwritten at Vercel build by scripts/write-prod-env.mjs from API_BASE_URL.
  // Local/default relative path works with same-origin or proxy only.
  // Production builds fail if API_BASE_URL is unset (see write-prod-env.mjs).
  apiBaseUrl: '/api/v1',
};
