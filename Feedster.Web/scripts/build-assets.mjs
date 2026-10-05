import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import postcss from 'postcss';
import tailwindcss from '@tailwindcss/postcss';

mkdirSync('wwwroot/css', { recursive: true });
const result = await postcss([tailwindcss({
  optimize: process.argv.includes('--minify') ? { minify: true } : false
})]).process(readFileSync('Styles/app.css', 'utf8'), {
  from: 'Styles/app.css', to: 'wwwroot/css/app.css', map: false
});
writeFileSync('wwwroot/css/app.css', result.css.trimEnd() + '\n');
