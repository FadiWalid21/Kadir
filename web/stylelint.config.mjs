/** @type {import('stylelint').Config} */
export default {
  extends: ['stylelint-config-standard'],
  ignoreFiles: ['**/tokens.css', '**/fonts.css', '**/dist/**', '**/node_modules/**'],
  rules: {
    'color-no-hex': true,
    'function-disallowed-list': ['rgb', 'rgba', 'hsl', 'hsla', 'hwb', 'lab', 'lch', 'oklab', 'oklch'],
    // Only token references allowed — no raw font stacks outside fonts.css
    'declaration-property-value-allowed-list': {
      'font-family': [/^var\(--font-/],
    },
    'value-keyword-case': null,
    'selector-class-pattern': null,
    'custom-property-pattern': null,
    'import-notation': null,
    'at-rule-no-unknown': [
      true,
      {
        ignoreAtRules: ['theme', 'source', 'utility', 'plugin', 'apply', 'tailwind'],
      },
    ],
  },
};
