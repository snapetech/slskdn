const CracoLessPlugin = require('@semantic-ui-react/craco-less');

module.exports = {
  plugins: [{ plugin: CracoLessPlugin }],
  webpack: {
    configure: (webpackConfig) => {
      // Disable ESLint errors in build (warnings only)
      const eslintPlugin = webpackConfig.plugins.find(
        (plugin) => plugin.constructor.name === 'ESLintWebpackPlugin',
      );
      if (eslintPlugin) {
        eslintPlugin.options.failOnError = false;
      }

      return webpackConfig;
    },
  },
};
