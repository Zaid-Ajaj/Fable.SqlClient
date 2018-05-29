var path = require("path");
var webpack = require("webpack");
var fableUtils = require("fable-utils");
var webpackbar = require('webpackbar')

function resolve(filePath) {
  return path.join(__dirname, filePath)
}

var babelOptions = fableUtils.resolveBabelOptions({
  presets: [["es2015", { "modules": false }]],
  plugins: ["transform-runtime"]
});

var isProduction = process.argv.indexOf("-p") >= 0;
console.log("Bundling for " + (isProduction ? "production" : "development") + "...");

var basicConfig = {
  devtool: "source-map",
  resolve: {
    modules: [resolve("./node_modules/")]
  },
  node: {
    __dirname: false,
    __filename: false
  },
  module: {
    rules: [
      {
        test: /\.fs(x|proj)?$/,
        use: {
          loader: "fable-loader",
          options: {
            babel: babelOptions,
            define: isProduction ? [] : ["DEBUG"]
          }
        }
      },
      {
        test: /\.js$/,
        exclude: /node_modules/,
        use: {
          loader: 'babel-loader',
          options: babelOptions
        },
      }
    ]
  },
  plugins: [ new webpackbar() ]
};

var mainConfig = Object.assign({
  target: "electron-main",
  entry: resolve("app/Main/Main.fsproj"),
  output: {
    path: resolve("dist"),
    filename: "main.js"
  }
}, basicConfig);

var rendererConfig = Object.assign({
  target: "electron-renderer",
  entry: resolve("app/Renderer/Renderer.fsproj"),
  output: {
    path: resolve("dist"),
    filename: "renderer.js"
  }
}, basicConfig);

module.exports = [mainConfig, rendererConfig]