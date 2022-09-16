const colors = require('tailwindcss/colors')

module.exports = {
    mode: 'jit',
    content: [
        './wwwroot/index.html',
        './**/*.razor',
        "./node_modules/flowbite/**/*.js"
    ],
    darkMode: 'class',
    theme: {
        extend: {
            colors: {
                cyan: colors.cyan
            }
        },
    },
    variants: {
        extend: {},
    },
    plugins: [
        require('tailwindcss-textshadow'),
        require('flowbite/plugin')
    ]
}