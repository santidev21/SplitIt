// Karma configuration for CI-friendly coverage thresholds
module.exports = function (config) {
  config.set({
    basePath: '',
    frameworks: ['jasmine', '@angular-devkit/build-angular'],
    plugins: [
      require('karma-jasmine'),
      require('karma-chrome-launcher'),
      require('karma-jasmine-html-reporter'),
      require('karma-coverage'),
      require('@angular-devkit/build-angular/plugins/karma')
    ],
    client: {
      jasmine: {},
      clearContext: false
    },
    jasmineHtmlReporter: {
      suppressAll: true
    },
    coverageReporter: {
      dir: require('path').join(__dirname, './coverage/split-it-ui'),
      subdir: '.',
      reporters: [
        { type: 'html' },
        { type: 'text-summary' },
        { type: 'lcovonly' },
        { type: 'cobertura' }
      ],
      check: {
        global: {
          statements: 45,
          branches: 20,
          functions: 30,
          lines: 45,
          excludes: ['src/main.ts', 'src/environments/**']
        },
        each: {
          statements: 2,
          branches: 0,
          lines: 2,
          excludes: ['src/**/*.spec.ts', 'src/app/modules/dashboard/components/split-method-dialog/**']
        }
      }
    },
    reporters: ['progress', 'kjhtml', 'coverage'],
    browsers: ['ChromeHeadlessNoSandbox'],
    customLaunchers: {
      ChromeHeadlessNoSandbox: {
        base: 'ChromeHeadless',
        flags: ['--no-sandbox', '--disable-gpu', '--disable-dev-shm-usage', '--disable-setuid-sandbox']
      }
    },
    restartOnFileChange: true,
    singleRun: false,
    browserNoActivityTimeout: 60000
  });
};
