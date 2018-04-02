import fable from 'rollup-plugin-fable';
import cleanup from 'rollup-plugin-cleanup';
import filesize from 'rollup-plugin-filesize';
import { resolveBabelOptions } from 'fable-utils';

const getPlugins = () => [
  fable({
    babel: resolveBabelOptions({
      presets: [['env', { targets: { node: 'current' }, modules: false }]],
      plugins: [],
      babelrc: false
    })
  }),
  cleanup(),
  filesize()
];

export default {
    input: 'test/Tests.fsproj',
    external: ['stream', 'net', 'child_process', 'buffer', 'net', 'tls', 'dns' ,'dgram', 'mssql'],
    output: {
      file: './dist/fable-sqlclient-test.js',
      format: 'cjs'
    },
    plugins: getPlugins()
}