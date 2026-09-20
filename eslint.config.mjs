import js from '@eslint/js';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: [
      '**/bin/**',
      '**/obj/**',
      '**/dist/**',
      '**/publish/**',
      '**/node_modules/**',
      '**/.vitepress/dist/**',
      '**/.vitepress/cache/**',
      '**/.worktrees/**',
      '**/docs/superpowers/**',
      '**/scripts/**'
    ]
  },
  js.configs.recommended,
  ...tseslint.configs.recommended
);
