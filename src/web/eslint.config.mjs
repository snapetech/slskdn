import js from '@eslint/js';
import globals from 'globals';
import importPlugin from 'eslint-plugin-import';
import promisePlugin from 'eslint-plugin-promise';
import reactPlugin from 'eslint-plugin-react';
import reactHooksPlugin from 'eslint-plugin-react-hooks';
import vitestPlugin from '@vitest/eslint-plugin';

const browserGlobals = {
  ...globals.browser,
  ...globals.node,
};

const testGlobals = {
  ...browserGlobals,
  ...globals.jest,
  ...vitestPlugin.environments.env.globals,
};

export default [
  {
    ignores: [
      '.vscode/**',
      'build/**',
      'e2e/**',
      'eslint.config.mjs',
      'node_modules/**',
      'package-lock.json',
      'playwright.config.ts',
      'public/**',
      'scripts/**',
      'test-results/**',
      'vite.config.js',
      '**/*.css',
    ],
    linterOptions: {
      reportUnusedDisableDirectives: 'off',
    },
  },
  {
    files: ['src/**/*.{js,jsx}'],
    languageOptions: {
      ecmaVersion: 'latest',
      globals: browserGlobals,
      parserOptions: {
        ecmaFeatures: {
          jsx: true,
        },
        sourceType: 'module',
      },
      sourceType: 'module',
    },
    plugins: {
      import: importPlugin,
      promise: promisePlugin,
      react: reactPlugin,
      'react-hooks': reactHooksPlugin,
    },
    rules: {
      ...js.configs.recommended.rules,
      eqeqeq: ['error', 'always', { null: 'ignore' }],
      'import/no-unassigned-import': [
        'error',
        {
          allow: [
            '@testing-library/jest-dom',
            'semantic-ui-less/semantic.less',
            '**/*.css',
          ],
        },
      ],
      'no-alert': 'off',
      'no-console': 'off',
      'no-param-reassign': 'off',
      'no-shadow': 'off',
      'no-unused-vars': 'off',
      'react/no-array-index-key': 'off',
      'react/prop-types': 'off',
      'react/state-in-constructor': 'off',
    },
    settings: {
      react: {
        version: 'detect',
      },
    },
  },
  {
    files: ['src/**/*.test.{js,jsx}'],
    languageOptions: {
      ecmaVersion: 'latest',
      globals: testGlobals,
      parserOptions: {
        ecmaFeatures: {
          jsx: true,
        },
        sourceType: 'module',
      },
      sourceType: 'module',
    },
    plugins: {
      vitest: vitestPlugin,
    },
    rules: {
      ...vitestPlugin.configs.recommended.rules,
      'vitest/prefer-hooks-in-order': 'off',
    },
  },
];
